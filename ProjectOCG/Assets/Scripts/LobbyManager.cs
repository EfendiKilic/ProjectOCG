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
    protected Callback<LobbyChatUpdate_t> lobbyChatUpdate;
    protected Callback<LobbyDataUpdate_t> lobbyDataUpdate; // YENİ
    
    private CSteamID currentLobbyID;
    
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
        lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
        lobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate); // YENİ
        
        Debug.Log("LobbyManager hazır!");
    }
    
    // YENİ: Lobi verisi güncellendiğinde
    void OnLobbyDataUpdate(LobbyDataUpdate_t callback)
    {
        // Sadece mevcut lobimizin verileri güncellendiğinde
        if (callback.m_ulSteamIDLobby == currentLobbyID.m_SteamID)
        {
            CSteamID lobbyID = new CSteamID(callback.m_ulSteamIDLobby);
            
            // Yeni host bilgisini al
            string newHostID = SteamMatchmaking.GetLobbyData(lobbyID, "host");
            string myID = SteamUser.GetSteamID().ToString();
            
            // Ben yeni host oldum mu?
            if (newHostID == myID && !NetworkManager.Instance.isHost)
            {
                Debug.Log("👑 SİZ YENİ HOST OLDUNUZ!");
                NetworkManager.Instance.isHost = true;
                
                // UI'yı güncelle
                if (lobbyUIController != null)
                {
                    lobbyUIController.OnHostChanged(true);
                }
                
                // Chat mesajı
                if (lobbyUIController != null)
                {
                    lobbyUIController.AddChatMessage("SİSTEM", "Siz artık yeni HOST'sunuz!", Color.yellow);
                }
            }
            else if (newHostID != myID && NetworkManager.Instance.isHost)
            {
                // Ben host değilim artık
                Debug.Log("👤 Artık host değilsiniz");
                NetworkManager.Instance.isHost = false;
                
                // UI'yı güncelle
                if (lobbyUIController != null)
                {
                    lobbyUIController.OnHostChanged(false);
                }
            }
            
            // Lobi türü değişimi
            string lobbyType = SteamMatchmaking.GetLobbyData(lobbyID, "type");
            if (lobbyUIController != null)
            {
                lobbyUIController.UpdateLobbyType(lobbyType);
            }
        }
    }
    
    public void CreateLobby()
    {
        Debug.Log("🎮 Lobi oluşturuluyor...");
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 4);
    }
    
    void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("❌ Lobi oluşturulamadı!");
            return;
        }
        
        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        Debug.Log("✅ Lobi oluşturuldu! Lobi ID: " + currentLobbyID);
        
        string lobbyName = SteamFriends.GetPersonaName() + "'nin Lobisi";
        SteamMatchmaking.SetLobbyData(currentLobbyID, "name", lobbyName);
        SteamMatchmaking.SetLobbyData(currentLobbyID, "host", SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(currentLobbyID, "type", "public");
        
        string lobbyCode = GenerateLobbyCode(currentLobbyID);
        SteamMatchmaking.SetLobbyData(currentLobbyID, "code", lobbyCode);
        
        Debug.Log($"📝 Lobi adı: {lobbyName}");
        Debug.Log($"🔑 Lobi kodu: {lobbyCode}");
    }
    
    string GenerateLobbyCode(CSteamID lobbyID)
    {
        ulong lobbyNum = lobbyID.m_SteamID;
        string code = (lobbyNum % 1000000).ToString("D6");
        return code;
    }
    
    public void JoinLobbyByCode(string code)
    {
        Debug.Log($"🔍 Kod ile aranıyor: {code}");
        
        if (string.IsNullOrEmpty(code))
        {
            lobbyUIController.ShowCodeError("Geçersiz kod!");
            return;
        }
        
        SteamMatchmaking.AddRequestLobbyListStringFilter("code", code, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(1);
        SteamMatchmaking.RequestLobbyList();
        
        Debug.Log($"📡 Kod araması başlatıldı: {code}");
    }
    
    public void FindLobbies()
    {
        Debug.Log("🔍 Lobiler aranıyor...");
        SteamMatchmaking.AddRequestLobbyListStringFilter("type", "public", ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.RequestLobbyList();
    }
    
    void OnLobbyList(LobbyMatchList_t callback)
    {
        Debug.Log("📋 Bulunan lobi sayısı: " + callback.m_nLobbiesMatching);
        
        if (callback.m_nLobbiesMatching == 0)
        {
            if (lobbyUIController != null)
            {
                lobbyUIController.ShowCodeError("Lobi bulunamadı!");
            }
            Debug.Log("⚠️ Lobi bulunamadı!");
            return;
        }
        
        for (int i = 0; i < callback.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(i);
            string lobbyName = SteamMatchmaking.GetLobbyData(lobbyID, "name");
            
            int currentPlayers = SteamMatchmaking.GetNumLobbyMembers(lobbyID);
            int maxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lobbyID);
            
            if (currentPlayers >= maxPlayers)
            {
                if (lobbyUIController != null)
                {
                    lobbyUIController.ShowCodeError("Lobi dolu!");
                }
                Debug.Log("⚠️ Lobi dolu!");
                continue;
            }
            
            Debug.Log($"✅ Lobiye katılınıyor: {lobbyName}");
            SteamMatchmaking.JoinLobby(lobbyID);
            break;
        }
    }
    
    void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("📨 Lobiye katılma isteği alındı!");
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }
    
    void OnLobbyEntered(LobbyEnter_t callback)
    {
        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
    
        string hostID = SteamMatchmaking.GetLobbyData(currentLobbyID, "host");
        string myID = SteamUser.GetSteamID().ToString();
    
        if (hostID == myID)
        {
            Debug.Log("👑 Lobiye HOST olarak katıldınız!");
            NetworkManager.Instance.isHost = true;
        }
        else
        {
            Debug.Log("🎮 Lobiye OYUNCU olarak katıldınız!");
            NetworkManager.Instance.isHost = false;
        }
    
        int playerCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyID);
        Debug.Log($"👥 Lobide {playerCount} oyuncu var");
    
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
    
    public void ToggleLobbyType()
    {
        if (!NetworkManager.Instance.isHost)
        {
            Debug.LogWarning("Sadece host lobi türünü değiştirebilir!");
            return;
        }
        
        string currentType = SteamMatchmaking.GetLobbyData(currentLobbyID, "type");
        
        if (currentType == "private")
        {
            SteamMatchmaking.SetLobbyType(currentLobbyID, ELobbyType.k_ELobbyTypePublic);
            SteamMatchmaking.SetLobbyData(currentLobbyID, "type", "public");
            Debug.Log("🌍 Lobi AÇIK yapıldı!");
        }
        else
        {
            SteamMatchmaking.SetLobbyType(currentLobbyID, ELobbyType.k_ELobbyTypePrivate);
            SteamMatchmaking.SetLobbyData(currentLobbyID, "type", "private");
            Debug.Log("🔒 Lobi ÖZEL yapıldı!");
        }
    }
    
    public void KickPlayer(CSteamID playerID)
    {
        if (!NetworkManager.Instance.isHost)
        {
            Debug.LogWarning("Sadece host oyuncu atabilir!");
            return;
        }
        
        NetworkManager.Instance.SendMessageToPlayer(playerID, "KICK");
        Debug.Log($"Oyuncu atıldı: {SteamFriends.GetFriendPersonaName(playerID)}");
    }
    
    public void LeaveLobby()
    {
        if (currentLobbyID != CSteamID.Nil)
        {
            Debug.Log("🚪 Lobiden ayrılıyorsunuz...");
            
            // YENİ: Eğer host isen, yeni host belirle
            if (NetworkManager.Instance.isHost)
            {
                TransferHostBeforeLeaving();
            }
            
            SteamMatchmaking.LeaveLobby(currentLobbyID);
            currentLobbyID = CSteamID.Nil;
        }
    }
    
    // YENİ: Ayrılmadan önce host'u devret
    void TransferHostBeforeLeaving()
    {
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyID);
        
        if (memberCount <= 1)
        {
            // Lobide sadece ben varım, host devri yok
            Debug.Log("Lobide sadece siz varsınız, host devri yapılmıyor");
            return;
        }
        
        CSteamID myID = SteamUser.GetSteamID();
        
        // İlk oyuncuyu (kendim değilse) yeni host yap
        for (int i = 0; i < memberCount; i++)
        {
            CSteamID memberID = SteamMatchmaking.GetLobbyMemberByIndex(currentLobbyID, i);
            
            if (memberID != myID)
            {
                // Bu oyuncuyu yeni host yap
                string newHostName = SteamFriends.GetFriendPersonaName(memberID);
                Debug.Log($"👑 Yeni host: {newHostName}");
                
                SteamMatchmaking.SetLobbyOwner(currentLobbyID, memberID);
                SteamMatchmaking.SetLobbyData(currentLobbyID, "host", memberID.ToString());
                
                // Diğer oyunculara bildir
                NetworkManager.Instance.SendMessageToAll($"HOST_CHANGE|{memberID}|{newHostName}");
                
                break;
            }
        }
    }
    
    void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
    {
        CSteamID userChanged = new CSteamID(callback.m_ulSteamIDUserChanged);
    
        if ((callback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0)
        {
            Debug.Log($"➕ Oyuncu lobiye katıldı!");
        
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
            Debug.Log($"➖ Oyuncu lobiden ayrıldı!");
        
            // YENİ: Ayrılan oyuncu host muydu?
            string currentHostID = SteamMatchmaking.GetLobbyData(currentLobbyID, "host");
            if (currentHostID == userChanged.ToString())
            {
                Debug.Log("⚠️ Host lobiden ayrıldı! Yeni host belirleniyor...");
                
                // Biraz bekle, Steam yeni host'u belirlesin
                StartCoroutine(CheckNewHostAfterDelay());
            }
            
            if (lobbyUIController != null)
            {
                lobbyUIController.OnPlayerLeft(userChanged);
            }
        }
    }
    
    // YENİ: Host değişimini kontrol et (gecikme ile)
    System.Collections.IEnumerator CheckNewHostAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);
        
        if (currentLobbyID == CSteamID.Nil)
            yield break;
        
        // Yeni host kim?
        CSteamID newOwner = SteamMatchmaking.GetLobbyOwner(currentLobbyID);
        string newHostID = SteamMatchmaking.GetLobbyData(currentLobbyID, "host");
        
        // Eğer lobby data güncel değilse, owner'ı kullan
        if (string.IsNullOrEmpty(newHostID) || newHostID == "0")
        {
            Debug.Log($"👑 Steam yeni owner belirledi: {SteamFriends.GetFriendPersonaName(newOwner)}");
            SteamMatchmaking.SetLobbyData(currentLobbyID, "host", newOwner.ToString());
        }
    }
}