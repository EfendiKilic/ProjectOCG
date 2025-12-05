using UnityEngine;
using Steamworks;
using System.Collections.Generic;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance;
    
    protected Callback<P2PSessionRequest_t> p2pSessionRequest;
    
    private List<CSteamID> connectedPlayers = new List<CSteamID>();
    
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
        
        p2pSessionRequest = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
        
        Debug.Log("NetworkManager hazır!");
    }
    
    void Update()
    {
        ReceiveMessages();
    }
    
    void OnP2PSessionRequest(P2PSessionRequest_t callback)
    {
        CSteamID remoteSteamID = callback.m_steamIDRemote;
        string playerName = SteamFriends.GetFriendPersonaName(remoteSteamID);
        
        SteamNetworking.AcceptP2PSessionWithUser(remoteSteamID);
        
        if (!connectedPlayers.Contains(remoteSteamID))
        {
            connectedPlayers.Add(remoteSteamID);
            Debug.Log($"Oyuncu bağlandı: {playerName} (Toplam: {connectedPlayers.Count + 1})");
        }
    }
    
    public void ConnectToLobbyMembers(CSteamID lobbyID)
    {
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyID);
        
        for (int i = 0; i < memberCount; i++)
        {
            CSteamID memberID = SteamMatchmaking.GetLobbyMemberByIndex(lobbyID, i);
            
            if (memberID == SteamUser.GetSteamID())
                continue;
            
            if (connectedPlayers.Contains(memberID))
                continue;
            
            SendMessageToPlayer(memberID, "HELLO");
            connectedPlayers.Add(memberID);
        }
        
        Debug.Log($"Lobiye bağlandı. Toplam oyuncu: {connectedPlayers.Count}");
    }
    
    public void SendMessageToPlayer(CSteamID targetID, string message)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes(message);
        
        bool success = SteamNetworking.SendP2PPacket(
            targetID,
            data,
            (uint)data.Length,
            EP2PSend.k_EP2PSendReliable
        );
        
        if (!success)
        {
            Debug.LogWarning($"Mesaj gönderilemedi: {message}");
        }
    }
    
    public void SendMessageToAll(string message)
    {
        foreach (CSteamID playerID in connectedPlayers)
        {
            SendMessageToPlayer(playerID, message);
        }
    }
    
    void ReceiveMessages()
    {
        uint packetSize;
    
        while (SteamNetworking.IsP2PPacketAvailable(out packetSize))
        {
            byte[] data = new byte[packetSize];
            CSteamID senderID;
        
            if (SteamNetworking.ReadP2PPacket(data, packetSize, out uint bytesRead, out senderID))
            {
                string message = System.Text.Encoding.UTF8.GetString(data, 0, (int)bytesRead);
                HandleMessage(senderID, message);
            }
        }
    }
    
    void HandleMessage(CSteamID senderID, string message)
    {
        // Chat mesajı
        if (message.StartsWith("CHAT|"))
        {
            string[] parts = message.Split('|');
            if (parts.Length >= 3)
            {
                string senderName = parts[1];
                string chatMessage = parts[2];
            
                LobbyUIController lobbyUI = FindObjectOfType<LobbyUIController>();
                if (lobbyUI != null)
                {
                    lobbyUI.ReceiveChatMessage(senderName, chatMessage);
                }
            }
            return;
        }
    
        // Bağlantı mesajları
        if (message == "HELLO")
        {
            SendMessageToPlayer(senderID, "HELLO_BACK");
        }
    }
    
    public void DisconnectAll()
    {
        foreach (CSteamID playerID in connectedPlayers)
        {
            SteamNetworking.CloseP2PSessionWithUser(playerID);
        }
        
        connectedPlayers.Clear();
        Debug.Log("Tüm bağlantılar kapatıldı");
    }
    
    void OnApplicationQuit()
    {
        DisconnectAll();
    }
    
    public int GetConnectedPlayerCount()
    {
        return connectedPlayers.Count;
    }
    
    public List<CSteamID> GetConnectedPlayers()
    {
        return connectedPlayers;
    }
}