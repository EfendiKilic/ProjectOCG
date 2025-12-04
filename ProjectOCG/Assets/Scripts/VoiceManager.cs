using UnityEngine;
using Steamworks;
using System.Collections.Generic;

public class VoiceManager : MonoBehaviour
{
    public static VoiceManager Instance;
    
    [Header("Ayarlar")]
    public int recordFrequency = 16000; // 16kHz
    public int recordLength = 1; // 1 saniye
    
    [Header("Kendi Durumum")]
    public bool isMicrophoneOn = false; // Mikrofonum açık mı?
    public bool isHeadphoneOn = false; // Kulaklığım açık mı?
    
    // Mikrofon
    private AudioClip microphoneClip;
    private string microphoneDevice;
    private int lastSamplePosition = 0;
    
    // Susturulmuş oyuncular (diğer oyuncuları ben susturdum)
    private HashSet<CSteamID> mutedPlayers = new HashSet<CSteamID>();
    
    // Ses oynatma için AudioSource'lar
    private Dictionary<CSteamID, AudioSource> playerAudioSources = new Dictionary<CSteamID, AudioSource>();
    
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
        // Mikrofon açıksa ve kayıt yapılıyorsa ses gönder
        if (isMicrophoneOn && Microphone.IsRecording(microphoneDevice))
        {
            SendVoiceData();
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
        microphoneClip = Microphone.Start(microphoneDevice, true, recordLength, recordFrequency);
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
        }
    }
    
    // ===== SES GÖNDERİMİ =====
    
    void SendVoiceData()
    {
        int currentPosition = Microphone.GetPosition(microphoneDevice);
        
        if (currentPosition < 0 || currentPosition == lastSamplePosition)
            return;
        
        int sampleCount = currentPosition - lastSamplePosition;
        if (sampleCount < 0)
            sampleCount += microphoneClip.samples;
        
        if (sampleCount > 0)
        {
            float[] samples = new float[sampleCount];
            microphoneClip.GetData(samples, lastSamplePosition);
            
            // Ses seviyesini kontrol et
            float volume = GetAudioVolume(samples);
            if (volume > 0.01f)
            {
                byte[] voiceData = FloatArrayToByteArray(samples);
                NetworkManager.Instance.SendVoiceToAll(voiceData);
            }
            
            lastSamplePosition = currentPosition;
        }
    }
    
    // ===== SES ALMA =====
    
    public void ReceiveVoiceData(CSteamID senderID, byte[] voiceData)
    {
        // Kulaklık kapalıysa sesi çalma
        if (!isHeadphoneOn)
            return;
        
        // Bu oyuncuyu susturmuşsam sesi çalma
        if (mutedPlayers.Contains(senderID))
            return;
        
        float[] samples = ByteArrayToFloatArray(voiceData);
        
        // AudioSource oluştur veya al
        if (!playerAudioSources.ContainsKey(senderID))
        {
            GameObject audioObj = new GameObject($"Voice_{senderID}");
            audioObj.transform.SetParent(transform);
            AudioSource audioSource = audioObj.AddComponent<AudioSource>();
            audioSource.loop = false;
            audioSource.spatialBlend = 0;
            playerAudioSources.Add(senderID, audioSource);
        }
        
        AudioSource source = playerAudioSources[senderID];
        AudioClip clip = AudioClip.Create("VoiceClip", samples.Length, 1, recordFrequency, false);
        clip.SetData(samples, 0);
        source.clip = clip;
        source.Play();
    }
    
    // ===== OYUNCU SUSTURMA =====
    
    public void ToggleMutePlayer(CSteamID playerID)
    {
        if (mutedPlayers.Contains(playerID))
        {
            mutedPlayers.Remove(playerID);
            Debug.Log($"🔊 {SteamFriends.GetFriendPersonaName(playerID)} susturması kaldırıldı");
        }
        else
        {
            mutedPlayers.Add(playerID);
            Debug.Log($"🔇 {SteamFriends.GetFriendPersonaName(playerID)} susturuldu");
            
            // Bu oyuncunun sesini durdur
            if (playerAudioSources.ContainsKey(playerID))
            {
                playerAudioSources[playerID].Stop();
            }
        }
    }
    
    public bool IsPlayerMuted(CSteamID playerID)
    {
        return mutedPlayers.Contains(playerID);
    }
    
    // ===== YARDIMCI FONKSİYONLAR =====
    
    float GetAudioVolume(float[] samples)
    {
        float sum = 0;
        foreach (float sample in samples)
        {
            sum += Mathf.Abs(sample);
        }
        return sum / samples.Length;
    }
    
    byte[] FloatArrayToByteArray(float[] floats)
    {
        byte[] bytes = new byte[floats.Length * 2];
        
        for (int i = 0; i < floats.Length; i++)
        {
            short value = (short)(floats[i] * short.MaxValue);
            bytes[i * 2] = (byte)(value & 0xFF);
            bytes[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }
        
        return bytes;
    }
    
    float[] ByteArrayToFloatArray(byte[] bytes)
    {
        float[] floats = new float[bytes.Length / 2];
        
        for (int i = 0; i < floats.Length; i++)
        {
            short value = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
            floats[i] = value / (float)short.MaxValue;
        }
        
        return floats;
    }
    
    void OnApplicationQuit()
    {
        StopMicrophone();
    }
}
