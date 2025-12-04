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
        
        Debug.Log("NetworkManager hazır!");
    }
    
    void Update()
    {
        // Gelen mesajları kontrol et
        ReceiveMessages();
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
            0
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
    
    void ReceiveMessages()
    {
        uint packetSize;
    
        while (SteamNetworking.IsP2PPacketAvailable(out packetSize, 0))
        {
            byte[] data = new byte[packetSize];
            CSteamID senderID;
        
            if (SteamNetworking.ReadP2PPacket(data, packetSize, out uint bytesRead, out senderID, 0))
            {
                // İlk 6 byte "VOICE|" mı kontrol et
                if (bytesRead > 6)
                {
                    string prefix = System.Text.Encoding.UTF8.GetString(data, 0, 6);
                
                    if (prefix == "VOICE|")
                    {
                        // Ses verisi
                        byte[] voiceData = new byte[bytesRead - 6];
                        System.Buffer.BlockCopy(data, 6, voiceData, 0, voiceData.Length);
                    
                        // VoiceManager'a ilet
                        VoiceManager.Instance?.ReceiveVoiceData(senderID, voiceData);
                        continue;
                    }
                }
            
                // Normal mesaj
                string message = System.Text.Encoding.UTF8.GetString(data, 0, (int)bytesRead);
                Debug.Log($"📥 Mesaj alındı ← {SteamFriends.GetFriendPersonaName(senderID)}: {message}");
                HandleMessage(senderID, message);
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
    
    // Ses verisini tüm oyunculara gönder
    public void SendVoiceToAll(byte[] voiceData)
    {
        foreach (CSteamID playerID in connectedPlayers)
        {
            SendVoiceToPlayer(playerID, voiceData);
        }
    }

    // Belirli bir oyuncuya ses gönder
    public void SendVoiceToPlayer(CSteamID targetID, byte[] voiceData)
    {
        // "VOICE|" prefix ekle
        byte[] prefix = System.Text.Encoding.UTF8.GetBytes("VOICE|");
        byte[] data = new byte[prefix.Length + voiceData.Length];
    
        System.Buffer.BlockCopy(prefix, 0, data, 0, prefix.Length);
        System.Buffer.BlockCopy(voiceData, 0, data, prefix.Length, voiceData.Length);
    
        bool success = SteamNetworking.SendP2PPacket(
            targetID,
            data,
            (uint)data.Length,
            EP2PSend.k_EP2PSendUnreliableNoDelay, // Ses için hızlı gönderim
            0
        );
    
        if (!success)
        {
            Debug.LogWarning($"⚠️ Ses verisi gönderilemedi!");
        }
    }
}