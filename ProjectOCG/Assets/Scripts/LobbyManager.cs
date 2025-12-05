using UnityEngine;
using Steamworks;

public class LobbyManager : MonoBehaviour
{
    public LobbyUI lobbyUI;
    public LobbyUIController lobbyUIController;

    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> lobbyEntered;
    protected Callback<LobbyMatchList_t> lobbyList;
    
    private CSteamID currentLobbyID;
    private bool isJoiningByCode = false;
    
    void Start()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam başlatılmamış!");
            return;
        }
        
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        lobbyList = Callback<LobbyMatchList_t>.Create(OnLobbyList); 
        
        Callback<LobbyChatUpdate_t> lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);

        Debug.Log("LobbyManager hazır!");
    }
    
    public void CreateLobby()
    {
        Debug.Log("Lobi oluşturuluyor...");
        isJoiningByCode = false;
        // Varsayılan olarak PUBLIC lobi
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 4);
    }
    
    void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("Lobi oluşturulamadı!");
            return;
        }
        
        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        Debug.Log("Lobi oluşturuldu! Lobi ID: " + currentLobbyID);
        
        string lobbyName = SteamFriends.GetPersonaName() + "'nin Lobisi";
        SteamMatchmaking.SetLobbyData(currentLobbyID, "name", lobbyName);
        SteamMatchmaking.SetLobbyData(currentLobbyID, "host", SteamUser.GetSteamID().ToString());
        
        string lobbyCode = GenerateLobbyCode(currentLobbyID);
        SteamMatchmaking.SetLobbyData(currentLobbyID, "code", lobbyCode);
        
        // Varsayılan olarak PUBLIC
        SteamMatchmaking.SetLobbyData(currentLobbyID, "type", "public");
        
        Debug.Log("Lobi adı: " + lobbyName);
        Debug.Log("Lobi kodu: " + lobbyCode);
        Debug.Log("Lobi türü: PUBLIC (varsayılan)");
    }
    
    string GenerateLobbyCode(CSteamID lobbyID)
    {
        string lobbyIDStr = lobbyID.m_SteamID.ToString();
        return lobbyIDStr.Substring(lobbyIDStr.Length - 6);
    }
    
    // YENİ: Lobi türünü değiştir (sadece host)
    public void ToggleLobbyType()
    {
        if (currentLobbyID == CSteamID.Nil)
        {
            Debug.LogError("Aktif lobi yok!");
            return;
        }
        
        // Host kontrolü
        string hostID = SteamMatchmaking.GetLobbyData(currentLobbyID, "host");
        string myID = SteamUser.GetSteamID().ToString();
        
        if (hostID != myID)
        {
            Debug.LogError("Sadece host lobi türünü değiştirebilir!");
            return;
        }
        
        // Mevcut türü al
        string currentType = SteamMatchmaking.GetLobbyData(currentLobbyID, "type");
        
        // Türü değiştir
        if (currentType == "public")
        {
            // Public → Private
            SteamMatchmaking.SetLobbyType(currentLobbyID, ELobbyType.k_ELobbyTypeFriendsOnly);
            SteamMatchmaking.SetLobbyData(currentLobbyID, "type", "private");
            Debug.Log("Lobi türü değiştirildi: PRIVATE");
            
            if (lobbyUIController != null)
            {
                lobbyUIController.UpdateLobbyType("private");
            }
        }
        else
        {
            // Private → Public
            SteamMatchmaking.SetLobbyType(currentLobbyID, ELobbyType.k_ELobbyTypePublic);
            SteamMatchmaking.SetLobbyData(currentLobbyID, "type", "public");
            Debug.Log("Lobi türü değiştirildi: PUBLIC");
            
            if (lobbyUIController != null)
            {
                lobbyUIController.UpdateLobbyType("public");
            }
        }
    }
    
    // Sadece PUBLIC lobileri ara
    public void FindLobbies()
    {
        Debug.Log("Public lobiler aranıyor...");
        isJoiningByCode = false;
        
        // Sadece public lobileri filtrele
        SteamMatchmaking.AddRequestLobbyListStringFilter("type", "public", ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.RequestLobbyList();
    }
    
    // Lobi koduna göre lobiye katıl (HEM PUBLIC HEM PRIVATE)
    public void JoinLobbyByCode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != 6)
        {
            Debug.LogError("Geçersiz lobi kodu! Kod 6 haneli olmalıdır.");
            if (lobbyUIController != null)
            {
                lobbyUIController.ShowCodeError("Geçersiz kod! 6 haneli olmalıdır.");
            }
            return;
        }
        
        Debug.Log("Lobi kodu ile aranıyor: " + code);
        isJoiningByCode = true;
        
        // Kod ile katılırken tür filtresi EKLEME (hem public hem private bulabilsin)
        SteamMatchmaking.AddRequestLobbyListStringFilter("code", code, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.RequestLobbyList();
    }
    
    void OnLobbyList(LobbyMatchList_t callback)
    {
        Debug.Log("Bulunan lobi sayısı: " + callback.m_nLobbiesMatching);
        
        if (callback.m_nLobbiesMatching == 0)
        {
            if (isJoiningByCode)
            {
                Debug.LogError("Lobi kodu bulunamadı!");
                if (lobbyUIController != null)
                {
                    lobbyUIController.ShowCodeError("Lobi bulunamadı!");
                }
                isJoiningByCode = false;
            }
            else
            {
                Debug.Log("Public lobi bulunamadı! Yeni lobi oluşturuluyor...");
                CreateLobby();
            }
            return;
        }
        
        for (int i = 0; i < callback.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(i);
            string lobbyName = SteamMatchmaking.GetLobbyData(lobbyID, "name");
            string lobbyType = SteamMatchmaking.GetLobbyData(lobbyID, "type");
            
            // Eğer kod ile katılmıyorsa ve lobi private ise, atla
            if (!isJoiningByCode && lobbyType == "private")
            {
                Debug.Log($"Private lobi atlandı: {lobbyName}");
                continue;
            }
            
            // Lobi dolu mu kontrol et
            int currentPlayers = SteamMatchmaking.GetNumLobbyMembers(lobbyID);
            int maxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lobbyID);
            
            if (currentPlayers >= maxPlayers)
            {
                Debug.LogError($"Lobi dolu! ({currentPlayers}/{maxPlayers})");
                if (isJoiningByCode && lobbyUIController != null)
                {
                    lobbyUIController.ShowCodeError("Lobi dolu!");
                }
                isJoiningByCode = false;
                continue;
            }
            
            Debug.Log($"Lobiye katılınıyor: {lobbyName} [{lobbyType.ToUpper()}] ({currentPlayers}/{maxPlayers})");
            SteamMatchmaking.JoinLobby(lobbyID);
            isJoiningByCode = false;
            break;
        }
    }
    
    void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("Lobiye katılma isteği alındı!");
        isJoiningByCode = false;
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }
    
    void OnLobbyEntered(LobbyEnter_t callback)
    {
        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        
        // Katılma başarısız mı kontrol et
        if (callback.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            Debug.LogError("Lobiye katılma başarısız! Hata kodu: " + callback.m_EChatRoomEnterResponse);
            
            if (callback.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseFull)
            {
                Debug.LogError("Lobi dolu!");
                if (lobbyUIController != null)
                {
                    lobbyUIController.ShowCodeError("Lobi dolu!");
                }
            }
            return;
        }
    
        string hostID = SteamMatchmaking.GetLobbyData(currentLobbyID, "host");
        string myID = SteamUser.GetSteamID().ToString();
    
        if (hostID == myID)
        {
            Debug.Log("Lobiye HOST olarak katıldınız!");
            NetworkManager.Instance.isHost = true;
        }
        else
        {
            Debug.Log("Lobiye OYUNCU olarak katıldınız!");
            NetworkManager.Instance.isHost = false;
        }
    
        int playerCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyID);
        Debug.Log($"Lobide {playerCount} oyuncu var");
    
        NetworkManager.Instance.ConnectToLobbyMembers(currentLobbyID);
        
        if (lobbyUI != null)
        {
            lobbyUI.SetLobbyInfo(currentLobbyID, NetworkManager.Instance.isHost);
        }
        if (lobbyUIController != null)
        {
            lobbyUIController.ShowLobby(currentLobbyID, NetworkManager.Instance.isHost);
        }
    }
    
    public void LeaveLobby()
    {
        if (currentLobbyID != CSteamID.Nil)
        {
            Debug.Log("Lobiden ayrılıyorsunuz...");
            SteamMatchmaking.LeaveLobby(currentLobbyID);
            currentLobbyID = CSteamID.Nil;
        }
    }
    
    void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
    {
        CSteamID userChanged = new CSteamID(callback.m_ulSteamIDUserChanged);
    
        if ((callback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0)
        {
            Debug.Log("Oyuncu lobiye katıldı!");
        
            if (NetworkManager.Instance.isHost)
            {
                NetworkManager.Instance.ConnectToLobbyMembers(currentLobbyID);
            }
        
            if (lobbyUIController != null)
            {
                lobbyUIController.OnPlayerJoined(userChanged);
            }
        }
    
        if ((callback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft) != 0)
        {
            Debug.Log("Oyuncu lobiden ayrıldı!");
        
            if (lobbyUIController != null)
            {
                lobbyUIController.OnPlayerLeft(userChanged);
            }
        }
    }
}