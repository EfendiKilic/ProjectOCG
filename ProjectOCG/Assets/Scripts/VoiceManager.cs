using UnityEngine;
using Steamworks;
using System.Collections.Generic;
using System.Collections;

public class VoiceManager : MonoBehaviour
{
    public static VoiceManager Instance;
    
    [Header("Ayarlar")]
    public int recordFrequency = 44100; // 44.1kHz (CD kalitesi)
    public int bufferLengthMs = 100; // 100ms buffer (daha smooth)
    public float volumeThreshold = 0.005f; // Sessizlik eşiği
    
    [Header("Kendi Durumum")]
    public bool isMicrophoneOn = false;
    public bool isHeadphoneOn = false;
    
    // Mikrofon
    private AudioClip microphoneClip;
    private string microphoneDevice;
    private int lastSamplePosition = 0;
    private float[] audioBuffer;
    private int bufferSize;
    
    // Susturulmuş oyuncular
    private HashSet<CSteamID> mutedPlayers = new HashSet<CSteamID>();
    
    // Ses oynatma için AudioSource'lar + Jitter Buffer
    private Dictionary<CSteamID, AudioSource> playerAudioSources = new Dictionary<CSteamID, AudioSource>();
    private Dictionary<CSteamID, Queue<float[]>> audioBuffers = new Dictionary<CSteamID, Queue<float[]>>();
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        // Buffer boyutunu hesapla
        bufferSize = (int)(recordFrequency * bufferLengthMs / 1000f);
        audioBuffer = new float[bufferSize];
        
        // Mikrofon cihazını al
        if (Microphone.devices.Length > 0)
        {
            microphoneDevice = Microphone.devices[0];
            Debug.Log($"🎤 Mikrofon bulundu: {microphoneDevice}");
        }
        else
        {
            Debug.LogWarning("⚠️ Mikrofon bulunamadı!");
        }
    }
    
    void Update()
    {
        // Mikrofon açıksa ses gönder
        if (isMicrophoneOn && Microphone.IsRecording(microphoneDevice))
        {
            ProcessMicrophone();
        }
    }
    
    // ===== MİKROFON KONTROLÜ =====
    
    public void ToggleMicrophone()
    {
        if (isMicrophoneOn)
        {
            StopMicrophone();
        }
        else
        {
            StartMicrophone();
        }
    }
    
    void StartMicrophone()
    {
        if (string.IsNullOrEmpty(microphoneDevice))
        {
            Debug.LogError("❌ Mikrofon bulunamadı!");
            return;
        }
        
        isMicrophoneOn = true;
        
        // Daha uzun buffer (1 saniye) ama küçük parçalar gönder
        microphoneClip = Microphone.Start(microphoneDevice, true, 1, recordFrequency);
        lastSamplePosition = 0;
        
        Debug.Log("🎤 Mikrofon açıldı!");
    }
    
    void StopMicrophone()
    {
        if (Microphone.IsRecording(microphoneDevice))
        {
            Microphone.End(microphoneDevice);
        }
        
        isMicrophoneOn = false;
        Debug.Log("🎤 Mikrofon kapandı!");
    }
    
    // ===== KULAKLIK KONTROLÜ =====
    
    public void ToggleHeadphone()
    {
        isHeadphoneOn = !isHeadphoneOn;
        
        if (isHeadphoneOn)
        {
            Debug.Log("🎧 Kulaklık açıldı!");
        }
        else
        {
            Debug.Log("🎧 Kulaklık kapandı!");
            
            // Tüm sesleri durdur
            foreach (var audioSource in playerAudioSources.Values)
            {
                if (audioSource != null)
                {
                    audioSource.Stop();
                }
            }
            
            // Buffer'ları temizle
            foreach (var buffer in audioBuffers.Values)
            {
                buffer.Clear();
            }
        }
    }
    
    // ===== SES İŞLEME (İYİLEŞTİRİLMİŞ) =====
    
    void ProcessMicrophone()
    {
        int currentPosition = Microphone.GetPosition(microphoneDevice);
    
        if (currentPosition < 0)
        {
            Debug.LogWarning("⚠️ Mikrofon pozisyonu geçersiz!");
            return;
        }
    
        if (currentPosition == lastSamplePosition)
            return;
    
        // Kaç sample var?
        int sampleCount = currentPosition - lastSamplePosition;
        if (sampleCount < 0)
            sampleCount += microphoneClip.samples;
    
        Debug.Log($"🎤 Sample count: {sampleCount}, Buffer size: {bufferSize}");
    
        // Yeterli veri var mı?
        if (sampleCount < bufferSize)
        {
            Debug.Log($"⚠️ Yeterli veri yok: {sampleCount} < {bufferSize}");
            return;
        }
    
        // Veriyi al
        microphoneClip.GetData(audioBuffer, lastSamplePosition);
    
        // Ses seviyesini kontrol et
        float volume = GetAudioVolume(audioBuffer);
        Debug.Log($"🔊 Ses seviyesi: {volume:F4} (Eşik: {volumeThreshold})");
    
        if (volume > volumeThreshold)
        {
            // Sıkıştırma ve gönderme
            byte[] voiceData = EncodeAudio(audioBuffer);
            Debug.Log($"📤 SES GÖNDERİLİYOR! Boyut: {voiceData.Length} bytes");
            NetworkManager.Instance.SendVoiceToAll(voiceData);
        }
        else
        {
            Debug.Log($"🔇 Ses çok düşük, gönderilmedi");
        }
    
        // Pozisyonu güncelle
        lastSamplePosition = (lastSamplePosition + bufferSize) % microphoneClip.samples;
    }
    
    // ===== SES ALMA (İYİLEŞTİRİLMİŞ) =====
    
    public void ReceiveVoiceData(CSteamID senderID, byte[] voiceData)
    {
        // Kulaklık kapalıysa çalma
        if (!isHeadphoneOn)
            return;
        
        // Susturulmuşsa çalma
        if (mutedPlayers.Contains(senderID))
            return;
        
        // Sesi decode et
        float[] samples = DecodeAudio(voiceData);
        
        // Jitter buffer'a ekle
        if (!audioBuffers.ContainsKey(senderID))
        {
            audioBuffers[senderID] = new Queue<float[]>();
        }
        
        audioBuffers[senderID].Enqueue(samples);
        
        // AudioSource oluştur
        if (!playerAudioSources.ContainsKey(senderID))
        {
            CreateAudioSource(senderID);
        }
        
        // Eğer yeterli buffer varsa oynat
        AudioSource source = playerAudioSources[senderID];
        if (audioBuffers[senderID].Count >= 2 && !source.isPlaying)
        {
            StartCoroutine(PlayBufferedAudio(senderID));
        }
    }
    
    // AudioSource oluştur
    void CreateAudioSource(CSteamID senderID)
    {
        GameObject audioObj = new GameObject($"Voice_{senderID}");
        audioObj.transform.SetParent(transform);
        
        AudioSource audioSource = audioObj.AddComponent<AudioSource>();
        audioSource.loop = false;
        audioSource.spatialBlend = 0; // 2D ses
        audioSource.volume = 1f;
        audioSource.priority = 0; // En yüksek öncelik
        
        playerAudioSources.Add(senderID, audioSource);
    }
    
    // Buffer'dan oynat (smooth)
    IEnumerator PlayBufferedAudio(CSteamID senderID)
    {
        AudioSource source = playerAudioSources[senderID];
        Queue<float[]> buffer = audioBuffers[senderID];
        
        while (buffer.Count > 0 && isHeadphoneOn && !mutedPlayers.Contains(senderID))
        {
            float[] samples = buffer.Dequeue();
            
            AudioClip clip = AudioClip.Create("VoiceClip", samples.Length, 1, recordFrequency, false);
            clip.SetData(samples, 0);
            
            source.clip = clip;
            source.Play();
            
            // Clip bitene kadar bekle
            yield return new WaitForSeconds((float)samples.Length / recordFrequency);
        }
    }
    
    // ===== OYUNCU SUSTURMA =====
    
    public void ToggleMutePlayer(CSteamID playerID)
    {
        if (mutedPlayers.Contains(playerID))
        {
            mutedPlayers.Remove(playerID);
            Debug.Log($"🔊 Susturma kaldırıldı");
        }
        else
        {
            mutedPlayers.Add(playerID);
            Debug.Log($"🔇 Susturuldu");
            
            // Sesini durdur
            if (playerAudioSources.ContainsKey(playerID))
            {
                playerAudioSources[playerID].Stop();
            }
            
            // Buffer'ı temizle
            if (audioBuffers.ContainsKey(playerID))
            {
                audioBuffers[playerID].Clear();
            }
        }
    }
    
    public bool IsPlayerMuted(CSteamID playerID)
    {
        return mutedPlayers.Contains(playerID);
    }
    
    // ===== SES KALİTESİ FONKSİYONLARI =====
    
    float GetAudioVolume(float[] samples)
    {
        float sum = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += Mathf.Abs(samples[i]);
        }
        return sum / samples.Length;
    }
    
    // Encode (16-bit PCM)
    byte[] EncodeAudio(float[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2];
        
        for (int i = 0; i < samples.Length; i++)
        {
            short value = (short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue);
            bytes[i * 2] = (byte)(value & 0xFF);
            bytes[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }
        
        return bytes;
    }
    
    // Decode (16-bit PCM)
    float[] DecodeAudio(byte[] bytes)
    {
        float[] samples = new float[bytes.Length / 2];
        
        for (int i = 0; i < samples.Length; i++)
        {
            short value = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
            samples[i] = value / (float)short.MaxValue;
        }
        
        return samples;
    }
    
    void OnApplicationQuit()
    {
        StopMicrophone();
    }
}