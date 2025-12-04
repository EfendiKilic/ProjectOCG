using UnityEngine;
using Steamworks;
using System.Collections.Generic;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance;
    
    // P2P mesaj callback'i
    protected Callback<P2PSessionRequest_t> p2pSessionRequest;
    
    // Bağlı oyuncular
    private List<CSteamID> connectedPlayers = new List<CSteamID>();
    
    // Bu oyuncu host mu?
    public bool isHost = false;
    
    // KANAL SİSTEMİ (yeni!)
    private const int CHANNEL_MESSAGES = 0; // Normal mesajlar
    private const int CHANNEL_VOICE = 1;    // Ses verisi (hızlı)
    
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
        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam başlatılmamış!");
            return;
        }
        
        // P2P bağlantı isteği callback'i
        p2pSessionRequest = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
        
        Debug.Log("NetworkManager hazır! (Steam Native optimizasyonlu)");
    }
    
    void Update()
    {
        // Gelen mesajları kontrol et (her iki kanal)
        ReceiveMessages();
        ReceiveVoiceData();
    }
    
    // P2P bağlantı isteği geldiğinde
    void OnP2PSessionRequest(P2PSessionRequest_t callback)
    {
        CSteamID remoteSteamID = callback.m_steamIDRemote;
        Debug.Log("📨 P2P bağlantı isteği alındı: " + SteamFriends.GetFriendPersonaName(remoteSteamID));
        
        // Bağlantıyı kabul et
        SteamNetworking.AcceptP2PSessionWithUser(remoteSteamID);
        
        // Oyuncu listesine ekle
        if (!connectedPlayers.Contains(remoteSteamID))
        {
            connectedPlayers.Add(remoteSteamID);
            Debug.Log("✅ Oyuncu bağlandı: " + SteamFriends.GetFriendPersonaName(remoteSteamID));
            Debug.Log($"👥 Toplam bağlı oyuncu: {connectedPlayers.Count + 1}"); // +1 kendimiz
        }
    }
    
    // Host lobideki tüm oyunculara bağlan
    public void ConnectToLobbyMembers(CSteamID lobbyID)
    {
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyID);
        Debug.Log($"🔗 Lobideki {memberCount} oyuncuya bağlanılıyor...");
        
        for (int i = 0; i < memberCount; i++)
        {
            CSteamID memberID = SteamMatchmaking.GetLobbyMemberByIndex(lobbyID, i);
            
            // Kendimize bağlanma
            if (memberID == SteamUser.GetSteamID())
                continue;
            
            // Zaten bağlıysa atlama
            if (connectedPlayers.Contains(memberID))
                continue;
            
            Debug.Log("🔗 Bağlanılıyor: " + SteamFriends.GetFriendPersonaName(memberID));
            
            // P2P mesaj gönder (bağlantı başlatır)
            SendMessageToPlayer(memberID, "HELLO");
            
            connectedPlayers.Add(memberID);
        }
        
        Debug.Log($"✅ Tüm oyunculara bağlandı! Toplam: {connectedPlayers.Count}");
    }
    
    // Belirli bir oyuncuya mesaj gönder
    public void SendMessageToPlayer(CSteamID targetID, string message)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes(message);
        
        bool success = SteamNetworking.SendP2PPacket(
            targetID,
            data,
            (uint)data.Length,
            EP2PSend.k_EP2PSendReliable,
            CHANNEL_MESSAGES // Kanal 0
        );
        
        if (success)
        {
            Debug.Log($"📤 Mesaj gönderildi → {SteamFriends.GetFriendPersonaName(targetID)}: {message}");
        }
        else
        {
            Debug.LogWarning($"⚠️ Mesaj gönderilemedi: {message}");
        }
    }
    
    // Tüm oyunculara mesaj gönder
    public void SendMessageToAll(string message)
    {
        Debug.Log($"📢 Herkese mesaj gönderiliyor: {message}");
        
        foreach (CSteamID playerID in connectedPlayers)
        {
            SendMessageToPlayer(playerID, message);
        }
    }
    
    // Normal mesajları al (Kanal 0)
    void ReceiveMessages()
    {
        uint packetSize;
    
        while (SteamNetworking.IsP2PPacketAvailable(out packetSize, CHANNEL_MESSAGES))
        {
            byte[] data = new byte[packetSize];
            CSteamID senderID;
        
            if (SteamNetworking.ReadP2PPacket(data, packetSize, out uint bytesRead, out senderID, CHANNEL_MESSAGES))
            {
                // Normal mesaj
                string message = System.Text.Encoding.UTF8.GetString(data, 0, (int)bytesRead);
                Debug.Log($"📥 Mesaj alındı ← {SteamFriends.GetFriendPersonaName(senderID)}: {message}");
                HandleMessage(senderID, message);
            }
        }
    }
    
    // Ses verilerini al (Kanal 1) - YENİ!
    void ReceiveVoiceData()
    {
        uint packetSize;
    
        while (SteamNetworking.IsP2PPacketAvailable(out packetSize, CHANNEL_VOICE))
        {
            byte[] voiceData = new byte[packetSize];
            CSteamID senderID;
        
            if (SteamNetworking.ReadP2PPacket(voiceData, packetSize, out uint bytesRead, out senderID, CHANNEL_VOICE))
            {
                // Direkt VoiceManager'a ilet (prefix yok!)
                if (VoiceManager.Instance != null)
                {
                    byte[] actualData = new byte[bytesRead];
                    System.Array.Copy(voiceData, actualData, bytesRead);
                    
                    VoiceManager.Instance.ReceiveVoiceData(senderID, actualData);
                }
            }
        }
    }
    
    // Gelen mesajları işle
    void HandleMessage(CSteamID senderID, string message)
    {
        // Chat mesajı mı?
        if (message.StartsWith("CHAT|"))
        {
            string[] parts = message.Split('|');
            if (parts.Length >= 3)
            {
                string senderName = parts[1];
                string chatMessage = parts[2];
            
                // UI'ya chat mesajını ekle
                LobbyUIController lobbyUI = FindObjectOfType<LobbyUIController>();
                if (lobbyUI != null)
                {
                    lobbyUI.ReceiveChatMessage(senderName, chatMessage);
                }
            }
            return;
        }
    
        // Diğer mesajlar
        if (message == "HELLO")
        {
            Debug.Log("👋 Selamlaşma mesajı alındı!");
            SendMessageToPlayer(senderID, "HELLO_BACK");
        }
        else if (message == "HELLO_BACK")
        {
            Debug.Log("👋 Selamlaşma cevabı alındı!");
        }
    }
    
    // Bağlantıları temizle
    public void DisconnectAll()
    {
        foreach (CSteamID playerID in connectedPlayers)
        {
            SteamNetworking.CloseP2PSessionWithUser(playerID);
        }
        
        connectedPlayers.Clear();
        Debug.Log("🚪 Tüm bağlantılar kapatıldı");
    }
    
    void OnApplicationQuit()
    {
        DisconnectAll();
    }
    
    // Bağlı oyuncu sayısını döndür
    public int GetConnectedPlayerCount()
    {
        return connectedPlayers.Count;
    }
    
    // Bağlı oyuncu listesini döndür
    public List<CSteamID> GetConnectedPlayers()
    {
        return connectedPlayers;
    }
    
    // ===== SES GÖNDERİMİ (OPTİMİZE EDİLMİŞ) =====
    
    // Ses verisini tüm oyunculara gönder
    public void SendVoiceToAll(byte[] voiceData)
    {
        foreach (CSteamID playerID in connectedPlayers)
        {
            SendVoiceToPlayer(playerID, voiceData);
        }
    }

    // Belirli bir oyuncuya ses gönder (PREFİX YOK - KANAL 1)
    public void SendVoiceToPlayer(CSteamID targetID, byte[] voiceData)
    {
        bool success = SteamNetworking.SendP2PPacket(
            targetID,
            voiceData,
            (uint)voiceData.Length,
            EP2PSend.k_EP2PSendUnreliableNoDelay, // En hızlı mod
            CHANNEL_VOICE // Kanal 1 (ses için ayrı kanal)
        );
        
        // Sadece hata durumunda log (spam önleme)
        if (!success)
        {
            Debug.LogWarning($"⚠️ Ses gönderilemedi: {SteamFriends.GetFriendPersonaName(targetID)}");
        }
    }
}