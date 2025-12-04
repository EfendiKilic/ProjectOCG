using UnityEngine;
using Steamworks;
using System.Collections.Generic;

/// <summary>
/// Steam'in native P2P Voice Chat sistemi
/// REPO ve Lethal Company gibi oyunlar bu sistemi kullanır
/// Discord/Steam Party Chat kalitesinde
/// </summary>
public class VoiceManager : MonoBehaviour
{
    public static VoiceManager Instance;
    
    [Header("Kontroller")]
    public bool isMicrophoneOn = false;
    public bool isHeadphoneOn = false;
    
    [Header("Ayarlar")]
    [Range(0.5f, 5f)]
    public float outputVolume = 2.0f;
    
    // Susturulmuş oyuncular
    private HashSet<CSteamID> mutedPlayers = new HashSet<CSteamID>();
    
    // AudioSource pool
    private Dictionary<CSteamID, AudioSource> audioSources = new Dictionary<CSteamID, AudioSource>();
    
    // Steam voice buffer
    private const uint VOICE_BUFFER_SIZE = 20480; // 20KB
    private byte[] voiceBuffer = new byte[VOICE_BUFFER_SIZE];
    
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
    
    void Update()
    {
        // Mikrofon açıksa Steam'e kaydet
        if (isMicrophoneOn)
        {
            SendVoice();
        }
    }
    
    // ===== MİKROFON =====
    
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
        if (!SteamManager.Initialized)
        {
            Debug.LogError("❌ Steam başlatılmamış!");
            return;
        }
        
        isMicrophoneOn = true;
        SteamUser.StartVoiceRecording();
        
        Debug.Log("🎤 Mikrofon AÇIK");
    }
    
    void StopMicrophone()
    {
        isMicrophoneOn = false;
        SteamUser.StopVoiceRecording();
        
        Debug.Log("🎤 Mikrofon KAPALI");
    }
    
    // ===== KULAKLIK =====
    
    public void ToggleHeadphone()
    {
        isHeadphoneOn = !isHeadphoneOn;
        
        Debug.Log($"🎧 Kulaklık: {(isHeadphoneOn ? "AÇIK" : "KAPALI")}");
        
        if (!isHeadphoneOn)
        {
            // Tüm sesleri durdur
            foreach (var source in audioSources.Values)
            {
                if (source != null) source.Stop();
            }
        }
    }
    
    // ===== SES GÖNDERME =====
    
    void SendVoice()
    {
        uint bytesAvailable = 0;
        
        // Steam'den mevcut ses verisini kontrol et
        if (SteamUser.GetAvailableVoice(out bytesAvailable) == EVoiceResult.k_EVoiceResultOK)
        {
            if (bytesAvailable > 0)
            {
                uint bytesWritten = 0;
                
                // Sıkıştırılmış sesi al (Steam'in kendi codec'i)
                EVoiceResult result = SteamUser.GetVoice(
                    true, // compressed
                    voiceBuffer,
                    VOICE_BUFFER_SIZE,
                    out bytesWritten
                );
                
                if (result == EVoiceResult.k_EVoiceResultOK && bytesWritten > 0)
                {
                    // Veriyi hazırla
                    byte[] voiceData = new byte[bytesWritten];
                    System.Buffer.BlockCopy(voiceBuffer, 0, voiceData, 0, (int)bytesWritten);
                    
                    // Tüm oyunculara gönder
                    NetworkManager.Instance.SendVoiceToAll(voiceData);
                }
            }
        }
    }
    
    // ===== SES ALMA =====
    
    public void ReceiveVoiceData(CSteamID senderID, byte[] compressedVoice)
    {
        if (!isHeadphoneOn) return;
        if (mutedPlayers.Contains(senderID)) return;
        
        // AudioSource oluştur (ilk kez)
        if (!audioSources.ContainsKey(senderID))
        {
            CreateAudioSource(senderID);
        }
        
        // Steam ile decompress et
        uint bytesWritten = 0;
        uint sampleRate = 11025; // 11kHz (optimum kalite/performans)
        byte[] pcmBuffer = new byte[22050]; // 2 saniye buffer
        
        EVoiceResult result = SteamUser.DecompressVoice(
            compressedVoice,
            (uint)compressedVoice.Length,
            pcmBuffer,
            (uint)pcmBuffer.Length,
            out bytesWritten,
            sampleRate
        );
        
        if (result == EVoiceResult.k_EVoiceResultOK && bytesWritten > 0)
        {
            // PCM → Float
            float[] samples = ConvertToFloat(pcmBuffer, (int)bytesWritten);
            
            // AudioClip oluştur
            AudioClip clip = AudioClip.Create(
                "Voice",
                samples.Length,
                1, // mono
                (int)sampleRate,
                false
            );
            clip.SetData(samples, 0);
            
            // Oynat
            AudioSource source = audioSources[senderID];
            source.clip = clip;
            source.volume = outputVolume;
            source.Play();
        }
    }
    
    // AudioSource oluştur
    void CreateAudioSource(CSteamID playerID)
    {
        string playerName = SteamFriends.GetFriendPersonaName(playerID);
        
        GameObject obj = new GameObject($"Voice_{playerName}");
        obj.transform.SetParent(transform);
        
        AudioSource source = obj.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f; // 2D
        source.priority = 0; // En yüksek öncelik
        
        audioSources[playerID] = source;
        
        Debug.Log($"🎙️ {playerName} konuşmaya başladı");
    }
    
    // PCM → Float dönüşüm
    float[] ConvertToFloat(byte[] pcm, int length)
    {
        int sampleCount = length / 2;
        float[] samples = new float[sampleCount];
        
        for (int i = 0; i < sampleCount; i++)
        {
            short pcmSample = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
            samples[i] = pcmSample / 32768f;
        }
        
        return samples;
    }
    
    // ===== OYUNCU SUSTURMA =====
    
    public void ToggleMutePlayer(CSteamID playerID)
    {
        if (mutedPlayers.Contains(playerID))
        {
            mutedPlayers.Remove(playerID);
            Debug.Log("🔊 Susturma kaldırıldı");
        }
        else
        {
            mutedPlayers.Add(playerID);
            Debug.Log("🔇 Oyuncu susturuldu");
            
            if (audioSources.ContainsKey(playerID))
            {
                audioSources[playerID].Stop();
            }
        }
    }
    
    public bool IsPlayerMuted(CSteamID playerID)
    {
        return mutedPlayers.Contains(playerID);
    }
    
    void OnDestroy()
    {
        StopMicrophone();
        
        foreach (var source in audioSources.Values)
        {
            if (source != null) Destroy(source.gameObject);
        }
        
        audioSources.Clear();
    }
}