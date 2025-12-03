using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steamworks;
using System.Collections.Generic;

public class LobbyUIController : MonoBehaviour
{
    [Header("Paneller")]
    public GameObject mainMenuPanel;
    public GameObject lobbyPanel;
    
    [Header("Lobi Bilgileri")]
    public TextMeshProUGUI lobbyTitleText;
    public Transform playerListContent;
    public GameObject playerListItemPrefab;
    
    [Header("Chat")]
    public Transform chatContent;
    public GameObject chatMessagePrefab;
    public TMP_InputField chatInputField;
    public Button sendButton;
    
    [Header("Diğer")]
    public Button leaveButton;
    
    private CSteamID currentLobbyID;
    private Dictionary<CSteamID, GameObject> playerListItems = new Dictionary<CSteamID, GameObject>();
    
    void Start()
    {
        // Buton eventlerini bağla
        sendButton.onClick.AddListener(SendChatMessage);
        leaveButton.onClick.AddListener(LeaveLobby);
        
        // Enter tuşu ile mesaj gönder
        chatInputField.onSubmit.AddListener((text) => { SendChatMessage(); });
        
        // Başlangıçta ana menü göster
        ShowMainMenu();
    }
    
    // Ana menüyü göster
    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        lobbyPanel.SetActive(false);
    }
    
    // Lobi panelini göster
    public void ShowLobby(CSteamID lobbyID, bool isHost)
    {
        currentLobbyID = lobbyID;
        
        mainMenuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        
        // Lobi başlığını güncelle
        string lobbyName = SteamMatchmaking.GetLobbyData(lobbyID, "name");
        lobbyTitleText.text = $"LOBİ: {lobbyName}";
        
        // Oyuncu listesini güncelle
        RefreshPlayerList();
        
        // Hoş geldin mesajı
        if (isHost)
        {
            AddChatMessage("SİSTEM", "Lobi oluşturuldu! Oyuncular bekleniyor...", Color.yellow);
        }
        else
        {
            AddChatMessage("SİSTEM", "Lobiye katıldınız!", Color.green);
        }
    }
    
    // Oyuncu listesini yenile
    public void RefreshPlayerList()
    {
        // Önce tüm listeyi temizle
        foreach (var item in playerListItems.Values)
        {
            Destroy(item);
        }
        playerListItems.Clear();
        
        if (currentLobbyID == CSteamID.Nil) return;
        
        // Lobideki oyuncuları ekle
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyID);
        string hostID = SteamMatchmaking.GetLobbyData(currentLobbyID, "host");
        
        for (int i = 0; i < memberCount; i++)
        {
            CSteamID memberID = SteamMatchmaking.GetLobbyMemberByIndex(currentLobbyID, i);
            string memberName = SteamFriends.GetFriendPersonaName(memberID);
            
            // Liste item oluştur
            GameObject item = Instantiate(playerListItemPrefab, playerListContent);
            TextMeshProUGUI nameText = item.GetComponentInChildren<TextMeshProUGUI>();
            
            // Host işareti ekle
            if (memberID.ToString() == hostID)
            {
                nameText.text = $"👑 {memberName} <color=yellow>(Host)</color>";
            }
            else
            {
                nameText.text = $"🎮 {memberName}";
            }
            
            playerListItems.Add(memberID, item);
        }
        
        Debug.Log($"Oyuncu listesi güncellendi: {memberCount} oyuncu");
    }
    
    // Chat mesajı ekle
    public void AddChatMessage(string sender, string message, Color color)
    {
        GameObject messageObj = Instantiate(chatMessagePrefab, chatContent);
        TextMeshProUGUI messageText = messageObj.GetComponentInChildren<TextMeshProUGUI>();
        
        messageText.text = $"<b>{sender}:</b> {message}";
        messageText.color = color;
        
        // Scroll'u en alta kaydır
        Canvas.ForceUpdateCanvases();
        ScrollRect scrollRect = chatContent.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
    
    // Chat mesajı gönder
    void SendChatMessage()
    {
        string message = chatInputField.text.Trim();
        
        if (string.IsNullOrEmpty(message))
            return;
        
        // Mesajı kendin için göster
        string myName = SteamFriends.GetPersonaName();
        AddChatMessage(myName, message, Color.white);
        
        // Mesajı diğer oyunculara gönder
        NetworkManager.Instance.SendMessageToAll($"CHAT|{myName}|{message}");
        
        // Input'u temizle
        chatInputField.text = "";
        chatInputField.ActivateInputField();
    }
    
    // Dışarıdan chat mesajı alındığında
    public void ReceiveChatMessage(string senderName, string message)
    {
        AddChatMessage(senderName, message, Color.cyan);
    }
    
    // Lobiden çık
    void LeaveLobby()
    {
        // LobbyManager'dan çık
        FindObjectOfType<LobbyManager>().LeaveLobby();
        
        // NetworkManager bağlantılarını kes
        NetworkManager.Instance.DisconnectAll();
        
        // Ana menüye dön
        ShowMainMenu();
        
        // Listeyi temizle
        foreach (var item in playerListItems.Values)
        {
            Destroy(item);
        }
        playerListItems.Clear();
        
        currentLobbyID = CSteamID.Nil;
    }
    
    // Oyuncu lobiye katıldığında
    public void OnPlayerJoined(CSteamID playerID)
    {
        string playerName = SteamFriends.GetFriendPersonaName(playerID);
        AddChatMessage("SİSTEM", $"{playerName} lobiye katıldı!", Color.green);
        RefreshPlayerList();
    }
    
    // Oyuncu lobiden ayrıldığında
    public void OnPlayerLeft(CSteamID playerID)
    {
        string playerName = SteamFriends.GetFriendPersonaName(playerID);
        AddChatMessage("SİSTEM", $"{playerName} lobiden ayrıldı!", Color.red);
        RefreshPlayerList();
    }
}