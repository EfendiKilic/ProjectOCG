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
    public GameObject invitePanel;
    public GameObject invitePopupPanel; // YENİ
    
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
    
    [Header("Davet Sistemi")]
    public Button openInviteButton;
    public Transform friendListContent;
    public GameObject friendListItemPrefab;
    public Button closeInviteButton;
    
    [Header("Davet Popup")] // YENİ
    public Image inviterAvatarImage;
    public TextMeshProUGUI inviterNameText;
    public TextMeshProUGUI inviteMessageText;
    public Button acceptInviteButton;
    public Button declineInviteButton;
    
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
    
    protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    
    // YENİ: Davet bilgileri
    private CSteamID pendingInviteLobbyID;
    private CSteamID pendingInviterID;
    private Coroutine inviteTimeoutCoroutine;
    
    void Start()
    {
        sendButton.onClick.AddListener(SendChatMessage);
        leaveButton.onClick.AddListener(LeaveLobby);
        copyCodeButton.onClick.AddListener(CopyLobbyCode);
        joinByCodeButton.onClick.AddListener(JoinByCode);
        toggleLobbyTypeButton.onClick.AddListener(ToggleLobbyType);
        openInviteButton.onClick.AddListener(OpenInvitePanel);
        closeInviteButton.onClick.AddListener(CloseInvitePanel);
        
        // YENİ: Davet popup butonları
        acceptInviteButton.onClick.AddListener(AcceptInvite);
        declineInviteButton.onClick.AddListener(DeclineInvite);
        
        chatInputField.onSubmit.AddListener((text) => { SendChatMessage(); });
        codeInputField.onSubmit.AddListener((text) => { JoinByCode(); });
        
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        
        if (codeErrorText != null)
        {
            codeErrorText.gameObject.SetActive(false);
        }
        
        if (invitePanel != null)
        {
            invitePanel.SetActive(false);
        }
        
        // YENİ: Popup başlangıçta kapalı
        if (invitePopupPanel != null)
        {
            invitePopupPanel.SetActive(false);
        }
        
        ShowMainMenu();
    }
    
    // YENİ: Steam'den davet alındığında (OYUN İÇİ POPUP)
    void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("📨 Steam'den lobi daveti alındı!");
        
        CSteamID inviterID = callback.m_steamIDFriend;
        CSteamID lobbyID = callback.m_steamIDLobby;
        
        // Davet bilgilerini sakla
        pendingInviteLobbyID = lobbyID;
        pendingInviterID = inviterID;
        
        // Popup'ı göster
        ShowInvitePopup(inviterID);
    }
    
    // YENİ: Davet popup'ını göster
    void ShowInvitePopup(CSteamID inviterID)
    {
        if (invitePopupPanel == null)
        {
            Debug.LogError("InvitePopupPanel atanmamış!");
            return;
        }
        
        string inviterName = SteamFriends.GetFriendPersonaName(inviterID);
        
        Debug.Log($"🎮 Davet popup'ı açılıyor: {inviterName}");
        
        // Avatar yükle
        if (inviterAvatarImage != null)
        {
            StartCoroutine(SteamAvatarLoader.LoadAvatarAsync(inviterID, inviterAvatarImage));
        }
        
        // İsim
        if (inviterNameText != null)
        {
            inviterNameText.text = inviterName;
        }
        
        // Mesaj
        if (inviteMessageText != null)
        {
            inviteMessageText.text = $"{inviterName} sizi lobisine davet ediyor!";
        }
        
        // Popup'ı aç
        invitePopupPanel.SetActive(true);
        
        // 10 saniye timeout başlat
        if (inviteTimeoutCoroutine != null)
        {
            StopCoroutine(inviteTimeoutCoroutine);
        }
        inviteTimeoutCoroutine = StartCoroutine(InviteTimeout(10f));
    }
    
    // YENİ: 10 saniye timeout
    IEnumerator InviteTimeout(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        
        Debug.Log("⏰ Davet süresi doldu, otomatik reddedildi");
        DeclineInvite();
    }
    
    // YENİ: Daveti kabul et
    void AcceptInvite()
    {
        Debug.Log("✅ Davet kabul edildi!");
        
        // Timeout'u durdur
        if (inviteTimeoutCoroutine != null)
        {
            StopCoroutine(inviteTimeoutCoroutine);
            inviteTimeoutCoroutine = null;
        }
        
        // Popup'ı kapat
        if (invitePopupPanel != null)
        {
            invitePopupPanel.SetActive(false);
        }
        
        // Eğer zaten bir lobideyse, önce çık
        if (currentLobbyID != CSteamID.Nil)
        {
            Debug.Log("Mevcut lobiden ayrılıyorsunuz...");
            FindObjectOfType<LobbyManager>().LeaveLobby();
            NetworkManager.Instance.DisconnectAll();
        }
        
        // Davet edilen lobiye katıl
        Debug.Log($"Lobiye katılınıyor: {pendingInviteLobbyID}");
        SteamMatchmaking.JoinLobby(pendingInviteLobbyID);
        
        // Pending bilgileri temizle
        pendingInviteLobbyID = CSteamID.Nil;
        pendingInviterID = CSteamID.Nil;
    }
    
    // YENİ: Daveti reddet
    void DeclineInvite()
    {
        Debug.Log("❌ Davet reddedildi!");
        
        // Timeout'u durdur
        if (inviteTimeoutCoroutine != null)
        {
            StopCoroutine(inviteTimeoutCoroutine);
            inviteTimeoutCoroutine = null;
        }
        
        // Popup'ı kapat
        if (invitePopupPanel != null)
        {
            invitePopupPanel.SetActive(false);
        }
        
        // Pending bilgileri temizle
        pendingInviteLobbyID = CSteamID.Nil;
        pendingInviterID = CSteamID.Nil;
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
    
    void UpdateInviteButton()
    {
        if (openInviteButton == null || currentLobbyID == CSteamID.Nil)
            return;
        
        int currentPlayers = SteamMatchmaking.GetNumLobbyMembers(currentLobbyID);
        int maxPlayers = 4;
        
        if (currentPlayers >= maxPlayers)
        {
            openInviteButton.interactable = false;
            TextMeshProUGUI buttonText = openInviteButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = "Lobi Dolu";
            }
        }
        else
        {
            openInviteButton.interactable = true;
            TextMeshProUGUI buttonText = openInviteButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = "Arkadaş Davet Et";
            }
        }
    }
    
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
    
    void CloseInvitePanel()
    {
        if (invitePanel != null)
        {
            invitePanel.SetActive(false);
        }
    }
    
    void LoadFriendsList()
    {
        foreach (Transform child in friendListContent)
        {
            Destroy(child.gameObject);
        }
        
        int friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
        Debug.Log($"Steam arkadaş sayısı: {friendCount}");
        
        if (friendCount == 0)
        {
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
            
            GameObject item = Instantiate(friendListItemPrefab, friendListContent);
            
            Image avatarImage = item.transform.Find("AvatarImage")?.GetComponent<Image>();
            if (avatarImage != null)
            {
                StartCoroutine(SteamAvatarLoader.LoadAvatarAsync(friendID, avatarImage));
            }
            
            TextMeshProUGUI nameText = item.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = friendName;
            }
            
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
    
    void InviteFriend(CSteamID friendID)
    {
        if (currentLobbyID == CSteamID.Nil)
        {
            AddChatMessage("SİSTEM", "Lobide değilsiniz!", Color.red);
            return;
        }
        
        string friendName = SteamFriends.GetFriendPersonaName(friendID);
        
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
}