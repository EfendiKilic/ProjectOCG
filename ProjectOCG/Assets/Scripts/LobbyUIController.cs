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
    
    void Start()
    {
        sendButton.onClick.AddListener(SendChatMessage);
        leaveButton.onClick.AddListener(LeaveLobby);
        copyCodeButton.onClick.AddListener(CopyLobbyCode);
        joinByCodeButton.onClick.AddListener(JoinByCode);
        toggleLobbyTypeButton.onClick.AddListener(ToggleLobbyType);
        
        chatInputField.onSubmit.AddListener((text) => { SendChatMessage(); });
        codeInputField.onSubmit.AddListener((text) => { JoinByCode(); });
        
        if (codeErrorText != null)
        {
            codeErrorText.gameObject.SetActive(false);
        }
        
        ShowMainMenu();
    }
    
    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        
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
            
            // YENİ: Kick butonu (sadece host, kendisi hariç)
            Button kickButton = item.transform.Find("KickButton")?.GetComponent<Button>();
            if (kickButton != null)
            {
                // Sadece host görebilir VE kendisi değilse
                if (isHost && !isMe)
                {
                    kickButton.gameObject.SetActive(true);
                    
                    // Listener ekle
                    CSteamID playerToKick = memberID; // Capture edilecek
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
    }
    
    // YENİ: Oyuncu at
    void KickPlayer(CSteamID playerID)
    {
        string playerName = SteamFriends.GetFriendPersonaName(playerID);
        
        AddChatMessage("SİSTEM", $"{playerName} odadan atıldı!", Color.red);
        
        FindObjectOfType<LobbyManager>().KickPlayer(playerID);
    }
    
    // YENİ: Atıldığında çağrılır
    public void OnKickedByHost()
    {
        AddChatMessage("SİSTEM", "Host tarafından odadan atıldınız!", Color.red);
        
        // 2 saniye bekle, sonra lobiden çık
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
}