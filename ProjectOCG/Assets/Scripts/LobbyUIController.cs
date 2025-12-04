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
    
    public void RefreshPlayerList()
{
    // Listeyi temizle
    foreach (var item in playerListItems.Values)
    {
        Destroy(item);
    }
    playerListItems.Clear();
    
    if (currentLobbyID == CSteamID.Nil) return;
    
    int memberCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyID);
    string hostID = SteamMatchmaking.GetLobbyData(currentLobbyID, "host");
    CSteamID mySteamID = SteamUser.GetSteamID();
    
    for (int i = 0; i < memberCount; i++)
    {
        CSteamID memberID = SteamMatchmaking.GetLobbyMemberByIndex(currentLobbyID, i);
        string memberName = SteamFriends.GetFriendPersonaName(memberID);
        bool isMe = (memberID == mySteamID);
        
        // Liste item oluştur
        GameObject item = Instantiate(playerListItemPrefab, playerListContent);
        
        // Avatar
        Image avatarImage = item.transform.Find("AvatarImage")?.GetComponent<Image>();
        if (avatarImage != null)
        {
            StartCoroutine(SteamAvatarLoader.LoadAvatarAsync(memberID, avatarImage));
        }
        
        // İsim
        TextMeshProUGUI nameText = item.GetComponentInChildren<TextMeshProUGUI>();
        if (memberID.ToString() == hostID)
        {
            nameText.text = $"👑 {memberName} <color=yellow>(Host)</color>";
        }
        else
        {
            nameText.text = $"🎮 {memberName}";
        }
        
        // ===== BUTONLAR =====
        
        // 1. MİKROFON BUTONU (sadece kendin için görünür)
        Button micButton = item.transform.Find("MicrophoneButton")?.GetComponent<Button>();
        if (micButton != null)
        {
            if (isMe)
            {
                // Kendi mikrofonunu kontrol et
                UpdateMicButtonText(micButton);
                micButton.onClick.RemoveAllListeners();
                micButton.onClick.AddListener(() => {
                    VoiceManager.Instance.ToggleMicrophone();
                    UpdateMicButtonText(micButton);
                });
            }
            else
            {
                // Diğer oyuncular için gizle
                micButton.gameObject.SetActive(false);
            }
        }
        
        // 2. KULAKLIK BUTONU (sadece kendin için görünür)
        Button headphoneButton = item.transform.Find("HeadphoneButton")?.GetComponent<Button>();
        if (headphoneButton != null)
        {
            if (isMe)
            {
                // Kendi kulaklığını kontrol et
                UpdateHeadphoneButtonText(headphoneButton);
                headphoneButton.onClick.RemoveAllListeners();
                headphoneButton.onClick.AddListener(() => {
                    VoiceManager.Instance.ToggleHeadphone();
                    UpdateHeadphoneButtonText(headphoneButton);
                });
            }
            else
            {
                // Diğer oyuncular için gizle
                headphoneButton.gameObject.SetActive(false);
            }
        }
        
        // 3. DİĞERİNİ SUSTUR BUTONU (sadece diğer oyuncular için görünür)
        Button muteOtherButton = item.transform.Find("MuteOtherButton")?.GetComponent<Button>();
        if (muteOtherButton != null)
        {
            if (isMe)
            {
                // Kendini susturamazsın, gizle
                muteOtherButton.gameObject.SetActive(false);
            }
            else
            {
                // Diğer oyuncuyu susturma butonu
                UpdateMuteOtherButtonText(muteOtherButton, memberID);
                
                CSteamID capturedID = memberID;
                muteOtherButton.onClick.RemoveAllListeners();
                muteOtherButton.onClick.AddListener(() => {
                    VoiceManager.Instance.ToggleMutePlayer(capturedID);
                    UpdateMuteOtherButtonText(muteOtherButton, capturedID);
                });
            }
        }
        
        playerListItems.Add(memberID, item);
    }
    
    Debug.Log($"Oyuncu listesi güncellendi: {memberCount} oyuncu");
}

    // Mikrofon buton textini güncelle
    void UpdateMicButtonText(Button micButton)
    {
        Image buttonImage = micButton.GetComponent<Image>();
        TextMeshProUGUI text = micButton.GetComponentInChildren<TextMeshProUGUI>();
    
        if (buttonImage != null)
        {
            buttonImage.color = VoiceManager.Instance.isMicrophoneOn ? Color.green : Color.red;
        }
    
        if (text != null)
        {
            text.text = "mic"; // Sabit emoji
        }
    }

    void UpdateHeadphoneButtonText(Button headphoneButton)
    {
        Image buttonImage = headphoneButton.GetComponent<Image>();
        TextMeshProUGUI text = headphoneButton.GetComponentInChildren<TextMeshProUGUI>();
    
        if (buttonImage != null)
        {
            buttonImage.color = VoiceManager.Instance.isHeadphoneOn ? Color.green : Color.red;
        }
    
        if (text != null)
        {
            text.text = "hs"; // Sabit emoji
        }
    }

    void UpdateMuteOtherButtonText(Button muteButton, CSteamID playerID)
    {
        Image buttonImage = muteButton.GetComponent<Image>();
        TextMeshProUGUI text = muteButton.GetComponentInChildren<TextMeshProUGUI>();
    
        bool isMuted = VoiceManager.Instance.IsPlayerMuted(playerID);
    
        if (buttonImage != null)
        {
            buttonImage.color = isMuted ? Color.red : Color.white;
        }
    
        if (text != null)
        {
            text.text = "mute"; // Sabit emoji
        }
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
    
        // Oyuncu listesini temizle
        foreach (var item in playerListItems.Values)
        {
            Destroy(item);
        }
        playerListItems.Clear();
    
        // CHAT GEÇMİŞİNİ TEMİZLE
        ClearChatHistory();
    
        currentLobbyID = CSteamID.Nil;
    }

// Chat geçmişini temizle
    void ClearChatHistory()
    {
        // Chat content'indeki tüm mesajları sil
        foreach (Transform child in chatContent)
        {
            Destroy(child.gameObject);
        }
    
        Debug.Log("Chat geçmişi temizlendi");
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