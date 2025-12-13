using UnityEngine;
using Discord;

public class DiscordManager : MonoBehaviour
{
    public static DiscordManager Instance;
    
    private Discord.Discord discord;
    private long applicationID = 1449384809053945906; // BURAYA DISCORD APPLICATION ID'NİZİ YAPIŞTIRIN
    
    private long startTime;
    
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
            return;
        }
        
        InitializeDiscord();
    }
    
    void InitializeDiscord()
    {
        try
        {
            discord = new Discord.Discord(applicationID, (ulong)Discord.CreateFlags.Default);
            startTime = System.DateTimeOffset.Now.ToUnixTimeSeconds();
            
            //Debug.Log("✅ Discord Rich Presence başlatıldı!");
            
            // Başlangıç durumunu ayarla
            UpdateActivity("Ana Menüde", "Oyuna Başlıyor");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Discord başlatılamadı: {e.Message}");
        }
    }
    
    void Update()
    {
        if (discord != null)
        {
            discord.RunCallbacks();
        }
    }
    
    // Discord durumunu güncelle
    public void UpdateActivity(string state, string details)
    {
        if (discord == null) return;
        
        var activity = new Discord.Activity
        {
            State = state,              // "Lobide Bekliyor"
            Details = details,          // "4 Kişilik Lobi"
            Timestamps = 
            {
                Start = startTime       // Oyuna başlama zamanı
            },
            Assets = 
            {
                LargeImage = "game_logo",   // Discord Developer Portal'da yükleyeceğiniz resim
                LargeText = "Oyun Logosu"
            }
        };
        
        discord.GetActivityManager().UpdateActivity(activity, (result) =>
        {
            if (result == Discord.Result.Ok)
            {
                //Debug.Log($"✅ Discord durumu güncellendi: {details}");
            }
            else
            {
                Debug.LogWarning($"⚠️ Discord güncellenemedi: {result}");
            }
        });
    }
    
    void OnApplicationQuit()
    {
        if (discord != null)
        {
            discord.Dispose();
        }
    }
}