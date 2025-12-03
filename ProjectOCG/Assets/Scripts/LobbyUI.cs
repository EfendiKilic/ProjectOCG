using UnityEngine;
using TMPro;
using Steamworks;
using System.Collections.Generic;

public class LobbyUI : MonoBehaviour
{
    [Header("UI Referansları")]
    public TextMeshProUGUI lobbyInfoText;
    
    private CSteamID currentLobbyID;
    private bool isHost = false;
    
    void Start()
    {
        // Başlangıçta boş
        UpdateLobbyInfo("Henüz lobiye katılmadınız...");
    }
    
    void Update()
    {
        // Her saniye güncelle
        if (Time.frameCount % 60 == 0 && currentLobbyID != CSteamID.Nil)
        {
            RefreshLobbyInfo();
        }
    }
    
    // Lobi bilgilerini güncelle
    public void SetLobbyInfo(CSteamID lobbyID, bool isHostPlayer)
    {
        currentLobbyID = lobbyID;
        isHost = isHostPlayer;
        RefreshLobbyInfo();
    }
    
    // Lobi bilgilerini yenile
    void RefreshLobbyInfo()
    {
        if (currentLobbyID == CSteamID.Nil)
        {
            UpdateLobbyInfo("Henüz lobiye katılmadınız...");
            return;
        }
        
        string info = "";
        
        // Lobi adı
        string lobbyName = SteamMatchmaking.GetLobbyData(currentLobbyID, "name");
        info += $"<b>LOBİ: {lobbyName}</b>\n\n";
        
        // Host mu Client mı?
        if (isHost)
        {
            info += "<color=yellow>👑 SİZ HOST'SUNUZ</color>\n\n";
        }
        else
        {
            info += "<color=cyan>🎮 SİZ OYUNCUSUNUZ</color>\n\n";
        }
        
        // Oyuncu listesi
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyID);
        info += $"<b>OYUNCULAR ({memberCount}/4):</b>\n";
        
        for (int i = 0; i < memberCount; i++)
        {
            CSteamID memberID = SteamMatchmaking.GetLobbyMemberByIndex(currentLobbyID, i);
            string memberName = SteamFriends.GetFriendPersonaName(memberID);
            
            // Host işareti
            string hostID = SteamMatchmaking.GetLobbyData(currentLobbyID, "host");
            if (memberID.ToString() == hostID)
            {
                info += $"  👑 {memberName} <color=yellow>(Host)</color>\n";
            }
            else
            {
                info += $"  🎮 {memberName}\n";
            }
        }
        
        // P2P bağlantı durumu
        info += "\n<b>P2P BAĞLANTILAR:</b>\n";
        int connectedCount = NetworkManager.Instance.GetConnectedPlayerCount();
        info += $"  Bağlı oyuncu: {connectedCount}\n";
        
        List<CSteamID> connectedPlayers = NetworkManager.Instance.GetConnectedPlayers();
        foreach (CSteamID playerID in connectedPlayers)
        {
            string playerName = SteamFriends.GetFriendPersonaName(playerID);
            info += $"  ✅ {playerName}\n";
        }
        
        UpdateLobbyInfo(info);
    }
    
    // UI text'i güncelle
    void UpdateLobbyInfo(string text)
    {
        if (lobbyInfoText != null)
        {
            lobbyInfoText.text = text;
        }
    }
    
    // Lobiden çık
    public void ClearLobbyInfo()
    {
        currentLobbyID = CSteamID.Nil;
        isHost = false;
        UpdateLobbyInfo("Lobiden ayrıldınız...");
    }
}