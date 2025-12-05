using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steamworks;
using System.Collections.Generic;
using System.Collections; // YENİ

public class LobbyUIController : MonoBehaviour
{
    [Header("Paneller")]
    public GameObject mainMenuPanel;
    public GameObject lobbyPanel;
    
    [Header("Ana Menü - Kod Girişi")] // YENİ
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
    
    [Header("Chat")]
    public Transform chatContent;
    public GameObject chatMessagePrefab;
    public TMP_InputField chatInputField;
    public Button sendButton;
    
    [Header("Diğer")]
    public Button leaveButton;
    
    private CSteamID currentLobbyID;
    private string currentLobbyCode;
    private Dictionary<CSteamID, GameObject> playerListItems = new Dictionary<CSteamID, GameObject>();
    
    void Start()
    {
        sendButton.onClick.AddListener(SendChatMessage);
        leaveButton.onClick.AddListener(LeaveLobby);
        copyCodeButton.onClick.AddListener(CopyLobbyCode);
        joinByCodeButton.onClick.AddListener(JoinByCode); // YENİ
        
        chatInputField.onSubmit.AddListener((text) => { SendChatMessage(); });
        codeInputField.onSubmit.AddListener((text) => { JoinByCode(); }); // YENİ
        
        // YENİ: Hata mesajını gizle
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
        
        // YENİ: Input'u temizle
        if (codeInputField != null)
        {
            codeInputField.text = "";
        }
        if (codeErrorText != null)
        {
            codeErrorText.gameObject.SetActive(false);
        }
    }
    
    // YENİ: Kod ile lobiye katıl
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
        
        // Hata mesajını gizle
        if (codeErrorText != null)
        {
            codeErrorText.gameObject.SetActive(false);
        }
        
        // LobbyManager'a gönder
        FindObjectOfType<LobbyManager>().JoinLobbyByCode(code);
    }
    
    // YENİ: Hata mesajı göster
    public void ShowCodeError(string errorMessage)
    {
        if (codeErrorText != null)
        {
            codeErrorText.text = errorMessage;
            codeErrorText.gameObject.SetActive(true);
            
            // 3 saniye sonra gizle
            StartCoroutine(HideCodeErrorAfterDelay(3f));
        }
    }
    
    // YENİ: Hata mesajını gecikmeyle gizle
    IEnumerator HideCodeErrorAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (codeErrorText != null)
        {
            codeErrorText.gameObject.SetActive(false);
        }
    }
    
    public void ShowLobby(CSteamID lobbyID, bool isHost)
    {
        currentLobbyID = lobbyID;
        
        mainMenuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        
        string lobbyName = SteamMatchmaking.GetLobbyData(lobbyID, "name");
        lobbyTitleText.text = $"LOBİ: {lobbyName}";
        
        currentLobbyCode = SteamMatchmaking.GetLobbyData(lobbyID, "code");
        if (lobbyCodeText != null)
        {
            lobbyCodeText.text = $"Lobi Kodu: {currentLobbyCode}";
        }
        
        RefreshPlayerList();
        
        if (isHost)
        {
            AddChatMessage("SİSTEM", "Lobi oluşturuldu! Oyuncular bekleniyor...", Color.yellow);
            AddChatMessage("SİSTEM", $"Lobi kodu: {currentLobbyCode}", Color.cyan);
        }
        else
        {
            AddChatMessage("SİSTEM", "Lobiye katıldınız!", Color.green);
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
        
        for (int i = 0; i < memberCount; i++)
        {
            CSteamID memberID = SteamMatchmaking.GetLobbyMemberByIndex(currentLobbyID, i);
            string memberName = SteamFriends.GetFriendPersonaName(memberID);
            
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
            
            playerListItems.Add(memberID, item);
        }
        
        Debug.Log($"Oyuncu listesi güncellendi: {memberCount} oyuncu");
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