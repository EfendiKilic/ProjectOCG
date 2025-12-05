using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steamworks;
using System.Collections.Generic;
using System.Collections;

public class LobbyUIController : MonoBehaviour
{
    [Header("Paneller")]
    public GameObject mainMenuPanel;
    public GameObject lobbyPanel;
    public GameObject invitePanel; // YENİ
    
    [Header("Ana Menü - Kod Girişi")]
    public TMP_InputField codeInputField;
    public Button joinByCodeButton;
    public TextMeshProUGUI codeErrorText;
    
    [Header("Lobi Bilgileri")]
    public TextMeshProUGUI lobbyTitleText;
    public Transform playerListContent;
    public GameObject playerListItemPrefab;
    
    [Header("Lobi Kodu")]
    public TextMeshProUGUI lobbyCodeText;
    public Button copyCodeButton;
    public Button toggleLobbyTypeButton;
    public TextMeshProUGUI lobbyTypeText;
    
    [Header("Davet Sistemi")] // YENİ
    public Button openInviteButton;
    public Transform friendListContent;
    public GameObject friendListItemPrefab;
    public Button closeInviteButton;
    
    [Header("Chat")]
    public Transform chatContent;
    public GameObject chatMessagePrefab;
    public TMP_InputField chatInputField;
    public Button sendButton;
    
    [Header("Diğer")]
    public Button leaveButton;
    
    private CSteamID currentLobbyID;
    private string currentLobbyCode;
    private bool isHost = false;
    private Dictionary<CSteamID, GameObject> playerListItems = new Dictionary<CSteamID, GameObject>();
    
    // YENİ
    protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    
    void Start()
    {
        sendButton.onClick.AddListener(SendChatMessage);
        leaveButton.onClick.AddListener(LeaveLobby);
        copyCodeButton.onClick.AddListener(CopyLobbyCode);
        joinByCodeButton.onClick.AddListener(JoinByCode);
        toggleLobbyTypeButton.onClick.AddListener(ToggleLobbyType);
        openInviteButton.onClick.AddListener(OpenInvitePanel); // YENİ
        closeInviteButton.onClick.AddListener(CloseInvitePanel); // YENİ
        
        chatInputField.onSubmit.AddListener((text) => { SendChatMessage(); });
        codeInputField.onSubmit.AddListener((text) => { JoinByCode(); });
        
        // YENİ: Steam davet callback'i
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        
        if (codeErrorText != null)
        {
            codeErrorText.gameObject.SetActive(false);
        }
        
        if (invitePanel != null)
        {
            invitePanel.SetActive(false);
        }
        
        ShowMainMenu();
    }
    
    // YENİ: Steam'den davet alındığında
    void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("Steam'den lobi daveti alındı!");
        
        // Eğer zaten bir lobideyse, önce çık
        if (currentLobbyID != CSteamID.Nil)
        {
            FindObjectOfType<LobbyManager>().LeaveLobby();
            NetworkManager.Instance.DisconnectAll();
        }
        
        // Davet edilen lobiye katıl
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
        
        AddChatMessage("SİSTEM", "Davete katılıyorsunuz...", Color.cyan);
    }
    
    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        
        if (invitePanel != null)
        {
            invitePanel.SetActive(false);
        }
        
        if (codeInputField != null)
        {
            codeInputField.text = "";
        }
        if (codeErrorText != null)
        {
            codeErrorText.gameObject.SetActive(false);
        }
    }
    
    void JoinByCode()
    {
        string code = codeInputField.text.Trim().ToUpper();
        
        if (string.IsNullOrEmpty(code))
        {
            ShowCodeError("Lütfen bir kod girin!");
            return;
        }
        
        if (code.Length != 6)
        {
            ShowCodeError("Kod 6 haneli olmalıdır!");
            return;
        }
        
        if (codeErrorText != null)
        {
            codeErrorText.gameObject.SetActive(false);
        }
        
        FindObjectOfType<LobbyManager>().JoinLobbyByCode(code);
    }
    
    public void ShowCodeError(string errorMessage)
    {
        if (codeErrorText != null)
        {
            codeErrorText.text = errorMessage;
            codeErrorText.gameObject.SetActive(true);
            
            StartCoroutine(HideCodeErrorAfterDelay(3f));
        }
    }
    
    IEnumerator HideCodeErrorAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (codeErrorText != null)
        {
            codeErrorText.gameObject.SetActive(false);
        }
    }
    
    public void ShowLobby(CSteamID lobbyID, bool hostStatus)
    {
        currentLobbyID = lobbyID;
        isHost = hostStatus;
        
        mainMenuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        
        string lobbyName = SteamMatchmaking.GetLobbyData(lobbyID, "name");
        lobbyTitleText.text = $"LOBİ: {lobbyName}";
        
        currentLobbyCode = SteamMatchmaking.GetLobbyData(lobbyID, "code");
        if (lobbyCodeText != null)
        {
            lobbyCodeText.text = $"Lobi Kodu: {currentLobbyCode}";
        }
        
        string lobbyType = SteamMatchmaking.GetLobbyData(lobbyID, "type");
        UpdateLobbyType(lobbyType);
        
        if (toggleLobbyTypeButton != null)
        {
            toggleLobbyTypeButton.gameObject.SetActive(isHost);
        }
        
        // YENİ: Davet butonunu güncelle
        UpdateInviteButton();
        
        RefreshPlayerList();
        
        if (isHost)
        {
            AddChatMessage("SİSTEM", "Lobi oluşturuldu! Oyuncular bekleniyor...", Color.yellow);
            AddChatMessage("SİSTEM", $"Lobi kodu: {currentLobbyCode}", Color.cyan);
            AddChatMessage("SİSTEM", "Lobi türünü değiştirmek için butona tıklayın.", new Color(0.7f, 0.7f, 1f));
        }
        else
        {
            AddChatMessage("SİSTEM", "Lobiye katıldınız!", Color.green);
        }
    }
    
    void ToggleLobbyType()
    {
        if (!isHost)
        {
            AddChatMessage("SİSTEM", "Sadece host lobi türünü değiştirebilir!", Color.red);
            return;
        }
        
        FindObjectOfType<LobbyManager>().ToggleLobbyType();
    }
    
    public void UpdateLobbyType(string lobbyType)
    {
        if (lobbyTypeText != null)
        {
            if (lobbyType == "private")
            {
                lobbyTypeText.text = "🔒 ÖZEL LOBİ";
                lobbyTypeText.color = new Color(1f, 0.5f, 0f);
                
                if (toggleLobbyTypeButton != null)
                {
                    TextMeshProUGUI buttonText = toggleLobbyTypeButton.GetComponentInChildren<TextMeshProUGUI>();
                    if (buttonText != null)
                    {
                        buttonText.text = "Açık Yap";
                    }
                }
                
                AddChatMessage("SİSTEM", "Lobi artık ÖZEL! Sadece kodla katılınabilir.", new Color(1f, 0.5f, 0f));
            }
            else
            {
                lobbyTypeText.text = "🌍 AÇIK LOBİ";
                lobbyTypeText.color = new Color(0f, 0.8f, 0.2f);
                
                if (toggleLobbyTypeButton != null)
                {
                    TextMeshProUGUI buttonText = toggleLobbyTypeButton.GetComponentInChildren<TextMeshProUGUI>();
                    if (buttonText != null)
                    {
                        buttonText.text = "Özel Yap";
                    }
                }
                
                if (isHost)
                {
                    AddChatMessage("SİSTEM", "Lobi artık AÇIK! Herkes katılabilir.", new Color(0f, 0.8f, 0.2f));
                }
            }
        }
    }
    
    void CopyLobbyCode()
    {
        if (!string.IsNullOrEmpty(currentLobbyCode))
        {
            GUIUtility.systemCopyBuffer = currentLobbyCode;
            AddChatMessage("SİSTEM", "Lobi kodu kopyalandı!", Color.green);
            Debug.Log("Lobi kodu kopyalandı: " + currentLobbyCode);
        }
    }
    
    // YENİ: Davet butonunu güncelle (lobi doluysa devre dışı)
    void UpdateInviteButton()
    {
        if (openInviteButton == null || currentLobbyID == CSteamID.Nil)
            return;
        
        int currentPlayers = SteamMatchmaking.GetNumLobbyMembers(currentLobbyID);
        int maxPlayers = 4; // Maksimum oyuncu sayısı
        
        if (currentPlayers >= maxPlayers)
        {
            // Lobi dolu, butonu devre dışı bırak
            openInviteButton.interactable = false;
            TextMeshProUGUI buttonText = openInviteButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = "Lobi Dolu";
            }
        }
        else
        {
            // Lobi dolu değil, butonu aktif et
            openInviteButton.interactable = true;
            TextMeshProUGUI buttonText = openInviteButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = "Arkadaş Davet Et";
            }
        }
    }
    
    // YENİ: Davet panelini aç
    void OpenInvitePanel()
    {
        if (currentLobbyID == CSteamID.Nil)
        {
            AddChatMessage("SİSTEM", "Lobide değilsiniz!", Color.red);
            return;
        }
        
        int currentPlayers = SteamMatchmaking.GetNumLobbyMembers(currentLobbyID);
        if (currentPlayers >= 4)
        {
            AddChatMessage("SİSTEM", "Lobi dolu! Davet gönderemezsiniz.", Color.red);
            return;
        }
        
        if (invitePanel != null)
        {
            invitePanel.SetActive(true);
            LoadFriendsList();
        }
    }
    
    // YENİ: Davet panelini kapat
    void CloseInvitePanel()
    {
        if (invitePanel != null)
        {
            invitePanel.SetActive(false);
        }
    }
    
    // YENİ: Steam arkadaş listesini yükle
    void LoadFriendsList()
    {
        // Önceki listeyi temizle
        foreach (Transform child in friendListContent)
        {
            Destroy(child.gameObject);
        }
        
        int friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
        Debug.Log($"Steam arkadaş sayısı: {friendCount}");
        
        if (friendCount == 0)
        {
            // Arkadaş yoksa bilgi mesajı
            GameObject emptyMessage = new GameObject("EmptyMessage");
            emptyMessage.transform.SetParent(friendListContent);
            TextMeshProUGUI text = emptyMessage.AddComponent<TextMeshProUGUI>();
            text.text = "Steam arkadaşınız yok.";
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 24;
            text.color = Color.gray;
            return;
        }
        
        for (int i = 0; i < friendCount; i++)
        {
            CSteamID friendID = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
            string friendName = SteamFriends.GetFriendPersonaName(friendID);
            EPersonaState friendState = SteamFriends.GetFriendPersonaState(friendID);
            
            // Liste itemini oluştur
            GameObject item = Instantiate(friendListItemPrefab, friendListContent);
            
            // Avatar
            Image avatarImage = item.transform.Find("AvatarImage")?.GetComponent<Image>();
            if (avatarImage != null)
            {
                StartCoroutine(SteamAvatarLoader.LoadAvatarAsync(friendID, avatarImage));
            }
            
            // İsim
            TextMeshProUGUI nameText = item.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = friendName;
            }
            
            // Durum (Online/Offline)
            TextMeshProUGUI statusText = item.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
            if (statusText != null)
            {
                if (friendState == EPersonaState.k_EPersonaStateOffline)
                {
                    statusText.text = "⚫ Çevrimdışı";
                    statusText.color = Color.gray;
                }
                else
                {
                    statusText.text = "🟢 Çevrimiçi";
                    statusText.color = Color.green;
                }
            }
            
            // Davet butonu
            Button inviteButton = item.transform.Find("InviteButton")?.GetComponent<Button>();
            if (inviteButton != null)
            {
                CSteamID capturedFriendID = friendID;
                inviteButton.onClick.RemoveAllListeners();
                inviteButton.onClick.AddListener(() => {
                    InviteFriend(capturedFriendID);
                });
            }
        }
    }
    
    // YENİ: Arkadaşı davet et
    void InviteFriend(CSteamID friendID)
    {
        if (currentLobbyID == CSteamID.Nil)
        {
            AddChatMessage("SİSTEM", "Lobide değilsiniz!", Color.red);
            return;
        }
        
        string friendName = SteamFriends.GetFriendPersonaName(friendID);
        
        // Steam'in native davet sistemini kullan
        bool success = SteamMatchmaking.InviteUserToLobby(currentLobbyID, friendID);
        
        if (success)
        {
            AddChatMessage("SİSTEM", $"{friendName} davet edildi!", Color.green);
            Debug.Log($"Davet gönderildi: {friendName}");
        }
        else
        {
            AddChatMessage("SİSTEM", $"{friendName} davet edilemedi!", Color.red);
            Debug.LogError($"Davet gönderilemedi: {friendName}");
        }
        
        // Paneli kapat
        CloseInvitePanel();
    }
    
    public void RefreshPlayerList()
    {
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
            
            GameObject item = Instantiate(playerListItemPrefab, playerListContent);
            
            Image avatarImage = item.transform.Find("AvatarImage")?.GetComponent<Image>();
            if (avatarImage != null)
            {
                StartCoroutine(SteamAvatarLoader.LoadAvatarAsync(memberID, avatarImage));
            }
            
            TextMeshProUGUI nameText = item.GetComponentInChildren<TextMeshProUGUI>();
            if (memberID.ToString() == hostID)
            {
                nameText.text = $"👑 {memberName} <color=yellow>(Host)</color>";
            }
            else
            {
                nameText.text = $"🎮 {memberName}";
            }
            
            Button kickButton = item.transform.Find("KickButton")?.GetComponent<Button>();
            if (kickButton != null)
            {
                if (isHost && !isMe)
                {
                    kickButton.gameObject.SetActive(true);
                    
                    CSteamID playerToKick = memberID;
                    kickButton.onClick.RemoveAllListeners();
                    kickButton.onClick.AddListener(() => {
                        KickPlayer(playerToKick);
                    });
                }
                else
                {
                    kickButton.gameObject.SetActive(false);
                }
            }
            
            playerListItems.Add(memberID, item);
        }
        
        Debug.Log($"Oyuncu listesi güncellendi: {memberCount} oyuncu");
        
        // YENİ: Davet butonunu güncelle
        UpdateInviteButton();
    }
    
    void KickPlayer(CSteamID playerID)
    {
        string playerName = SteamFriends.GetFriendPersonaName(playerID);
        
        AddChatMessage("SİSTEM", $"{playerName} odadan atıldı!", Color.red);
        
        FindObjectOfType<LobbyManager>().KickPlayer(playerID);
    }
    
    public void OnKickedByHost()
    {
        AddChatMessage("SİSTEM", "Host tarafından odadan atıldınız!", Color.red);
        
        StartCoroutine(KickDelayedExit());
    }
    
    IEnumerator KickDelayedExit()
    {
        yield return new WaitForSeconds(2f);
        LeaveLobby();
    }
    
    public void AddChatMessage(string sender, string message, Color color)
    {
        GameObject messageObj = Instantiate(chatMessagePrefab, chatContent);
        TextMeshProUGUI messageText = messageObj.GetComponentInChildren<TextMeshProUGUI>();
        
        messageText.text = $"<b>{sender}:</b> {message}";
        messageText.color = color;
        
        Canvas.ForceUpdateCanvases();
        ScrollRect scrollRect = chatContent.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
    
    void SendChatMessage()
    {
        string message = chatInputField.text.Trim();
        
        if (string.IsNullOrEmpty(message))
            return;
        
        string myName = SteamFriends.GetPersonaName();
        AddChatMessage(myName, message, Color.white);
        
        NetworkManager.Instance.SendMessageToAll($"CHAT|{myName}|{message}");
        
        chatInputField.text = "";
        chatInputField.ActivateInputField();
    }
    
    public void ReceiveChatMessage(string senderName, string message)
    {
        AddChatMessage(senderName, message, Color.cyan);
    }
    
    void LeaveLobby()
    {
        FindObjectOfType<LobbyManager>().LeaveLobby();
        NetworkManager.Instance.DisconnectAll();
        
        ShowMainMenu();
        
        foreach (var item in playerListItems.Values)
        {
            Destroy(item);
        }
        playerListItems.Clear();
        
        ClearChatHistory();
        
        currentLobbyID = CSteamID.Nil;
        currentLobbyCode = "";
        isHost = false;
    }

    void ClearChatHistory()
    {
        foreach (Transform child in chatContent)
        {
            Destroy(child.gameObject);
        }
        
        Debug.Log("Chat geçmişi temizlendi");
    }
    
    public void OnPlayerJoined(CSteamID playerID)
    {
        string playerName = SteamFriends.GetFriendPersonaName(playerID);
        AddChatMessage("SİSTEM", $"{playerName} lobiye katıldı!", Color.green);
        RefreshPlayerList();
    }
    
    public void OnPlayerLeft(CSteamID playerID)
    {
        string playerName = SteamFriends.GetFriendPersonaName(playerID);
        AddChatMessage("SİSTEM", $"{playerName} lobiden ayrıldı!", Color.red);
        RefreshPlayerList();
    }
    
    public void OnHostChanged(bool isNewHost)
    {
        isHost = isNewHost;
    
        if (toggleLobbyTypeButton != null)
        {
            toggleLobbyTypeButton.gameObject.SetActive(isHost);
        }
    
        UpdateInviteButton();
    
        RefreshPlayerList();
    }
}