using UnityEngine;
using Steamworks;
using System.Collections;

public class LobbyManager : MonoBehaviour
{
    public LobbyUI lobbyUI;
    public LobbyUIController lobbyUIController;

    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> lobbyEntered;
    protected Callback<LobbyMatchList_t> lobbyList;
    protected Callback<LobbyChatUpdate_t> lobbyChatUpdate;
    protected Callback<LobbyDataUpdate_t> lobbyDataUpdate;
    
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
        lobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
        
        Debug.Log("LobbyManager hazır!");
    }
    
    void OnLobbyDataUpdate(LobbyDataUpdate_t callback)
{
    if (callback.m_ulSteamIDLobby == currentLobbyID.m_SteamID)
    {
        CSteamID lobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        
        // GERÇEK STEAM OWNER'I AL
        CSteamID realOwner = SteamMatchmaking.GetLobbyOwner(lobbyID);
        CSteamID myID = SteamUser.GetSteamID();
        
        string lobbyDataHost = SteamMatchmaking.GetLobbyData(lobbyID, "host");
        
        Debug.Log($"📡 Lobby Data Update!");
        Debug.Log($"   Gerçek Owner: {SteamFriends.GetFriendPersonaName(realOwner)}");
        Debug.Log($"   Lobby Data Host: {lobbyDataHost}");
        Debug.Log($"   Ben: {SteamFriends.GetPersonaName()}");
        
        // Eğer gerçek owner bensem ve henüz host değilsem
        if (realOwner == myID && !NetworkManager.Instance.isHost)
        {
            Debug.Log("👑 SİZ YENİ HOST OLDUNUZ!");
            NetworkManager.Instance.isHost = true;
            
            // Lobby data'yı da güncelle
            SteamMatchmaking.SetLobbyData(lobbyID, "host", myID.ToString());
            
            if (lobbyUIController != null)
            {
                lobbyUIController.OnHostChanged(true);
                lobbyUIController.AddChatMessage("SİSTEM", "Siz artık yeni HOST'sunuz!", Color.yellow);
            }
        }
        // Eğer gerçek owner ben değilsem ve host'sam
        else if (realOwner != myID && NetworkManager.Instance.isHost)
        {
            Debug.Log("👤 Artık host değilsiniz");
            NetworkManager.Instance.isHost = false;
            
            if (lobbyUIController != null)
            {
                lobbyUIController.OnHostChanged(false);
                string newHostName = SteamFriends.GetFriendPersonaName(realOwner);
                lobbyUIController.AddChatMessage("SİSTEM", $"{newHostName} artık yeni HOST!", Color.yellow);
            }
        }
        
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
    
        // SADECE PUBLIC LOBİLERİ ARA
        SteamMatchmaking.AddRequestLobbyListStringFilter("code", code, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListStringFilter("type", "public", ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(1);
        SteamMatchmaking.RequestLobbyList();
    
        Debug.Log($"📡 Kod araması başlatıldı (sadece public lobiler): {code}");
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
            // Hiç lobi yok, yeni lobi oluştur
            Debug.Log("⚠️ Boş lobi bulunamadı! Otomatik olarak yeni lobi oluşturuluyor...");
            CreateLobby();
            return;
        }
    
        // Uygun lobi bul (dolu olmayanları kontrol et)
        bool foundAvailableLobby = false;
    
        for (int i = 0; i < callback.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(i);
            string lobbyName = SteamMatchmaking.GetLobbyData(lobbyID, "name");
        
            int currentPlayers = SteamMatchmaking.GetNumLobbyMembers(lobbyID);
            int maxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lobbyID);
        
            Debug.Log($"Lobi kontrol ediliyor: {lobbyName} ({currentPlayers}/{maxPlayers})");
        
            if (currentPlayers >= maxPlayers)
            {
                Debug.Log($"⚠️ Lobi dolu: {lobbyName}");
                continue; // Bu lobi dolu, bir sonrakine bak
            }
        
            // Uygun lobi bulundu!
            Debug.Log($"✅ Uygun lobiye katılınıyor: {lobbyName}");
            SteamMatchmaking.JoinLobby(lobbyID);
            foundAvailableLobby = true;
            break;
        }
    
        // Hiçbir uygun lobi bulunamadı (hepsi dolu)
        if (!foundAvailableLobby)
        {
            Debug.Log(" Tüm lobiler dolu! Yeni lobi oluşturuluyor...");
            CreateLobby();
        }
    }
    
    void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        Debug.Log(" Lobiye katılma isteği alındı!");
    
        CSteamID inviterID = callback.m_steamIDFriend;
        CSteamID lobbyID = callback.m_steamIDLobby;
    
        string inviterName = SteamFriends.GetFriendPersonaName(inviterID);
        Debug.Log($" Davet gönderen: {inviterName}");
    
        // POPUP'I AÇ (LobbyUIController üzerinden)
        if (lobbyUIController != null)
        {
            lobbyUIController.ShowInvitePopupFromLobbyManager(inviterID, lobbyID);
        }
        else
        {
            Debug.LogError(" LobbyUIController bulunamadı! Popup açılamıyor!");
        
            // Fallback: Direkt katıl (eski davranış)
            SteamMatchmaking.JoinLobby(lobbyID);
        }
    }
    
    void OnLobbyEntered(LobbyEnter_t callback)
    {
        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);

        // GERÇEK STEAM OWNER'I AL
        CSteamID realOwner = SteamMatchmaking.GetLobbyOwner(currentLobbyID);
        CSteamID myID = SteamUser.GetSteamID();
    
        // Lobby data'daki host bilgisini kontrol et
        string hostID = SteamMatchmaking.GetLobbyData(currentLobbyID, "host");
    
        Debug.Log($"🔍 Gerçek Steam Owner: {SteamFriends.GetFriendPersonaName(realOwner)}");
        Debug.Log($"📝 Lobby Data Host: {hostID}");
        Debug.Log($"👤 Benim ID'm: {myID}");
    
        // Eğer lobby data boş veya yanlışsa, gerçek owner'ı kullan
        if (string.IsNullOrEmpty(hostID) || hostID != realOwner.ToString())
        {
            Debug.Log("⚠️ Lobby data yanlış, düzeltiliyor...");
            SteamMatchmaking.SetLobbyData(currentLobbyID, "host", realOwner.ToString());
            hostID = realOwner.ToString();
        }
    
        // BEN HOST MUYUM?
        if (realOwner == myID)
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
    
    // YENİ: Coroutine ile host devri
    public void LeaveLobby()
    {
        if (currentLobbyID != CSteamID.Nil)
        {
            Debug.Log("🚪 Lobiden ayrılma işlemi başlatılıyor...");
            
            // Eğer host isen, coroutine başlat
            if (NetworkManager.Instance.isHost)
            {
                StartCoroutine(TransferHostAndLeave());
            }
            else
            {
                // Host değilsen direkt çık
                PerformLeaveLobby();
            }
        }
    }
    
    IEnumerator TransferHostAndLeave()
    {
        Debug.Log(" Host devir işlemi başlıyor...");
    
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyID);
    
        if (memberCount <= 1)
        {
            Debug.Log("Lobide sadece siz varsınız, direkt ayrılıyorsunuz");
            PerformLeaveLobby();
            yield break;
        }
    
        CSteamID myID = SteamUser.GetSteamID();
        CSteamID newHostID = CSteamID.Nil;
        string newHostName = "";
    
        // İlk oyuncuyu (kendim değilse) bul
        for (int i = 0; i < memberCount; i++)
        {
            CSteamID memberID = SteamMatchmaking.GetLobbyMemberByIndex(currentLobbyID, i);
        
            if (memberID != myID)
            {
                newHostID = memberID;
                newHostName = SteamFriends.GetFriendPersonaName(memberID);
                break;
            }
        }
    
        if (newHostID == CSteamID.Nil)
        {
            Debug.LogWarning("Yeni host bulunamadı!");
            PerformLeaveLobby();
            yield break;
        }
    
        Debug.Log($"👑 Yeni host belirlendi: {newHostName}");
    
        // 1. Steam owner'ı değiştir (Steam bunu otomatik yapacak ama biz de ayarlayalım)
        SteamMatchmaking.SetLobbyOwner(currentLobbyID, newHostID);
    
        // 2. Lobby data'yı HEMEN güncelle
        SteamMatchmaking.SetLobbyData(currentLobbyID, "host", newHostID.ToString());
    
        // 3. Chat'e yaz (diğer oyuncular görsün)
        if (lobbyUIController != null)
        {
            lobbyUIController.AddChatMessage("SİSTEM", $"Host {newHostName} oyuncusuna devredildi!", Color.yellow);
        }
    
        // 4. Diğer oyunculara bildir
        NetworkManager.Instance.SendMessageToAll($"HOST_CHANGE|{newHostID}|{newHostName}");
    
        Debug.Log($"✅ Host başarıyla devredildi: {newHostName}");
    
        // 5. Biraz bekle (mesajların iletilmesi için)
        yield return new WaitForSeconds(0.5f);
    
        // 6. Şimdi lobiden ayrıl
        Debug.Log("🚪 Eski host lobiden ayrılıyor...");
        PerformLeaveLobby();
    }
    
    // YENİ: Gerçek ayrılma işlemi
    void PerformLeaveLobby()
    {
        if (currentLobbyID != CSteamID.Nil)
        {
            SteamMatchmaking.LeaveLobby(currentLobbyID);
            currentLobbyID = CSteamID.Nil;
            Debug.Log("✅ Lobiden ayrıldınız");
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
            Debug.Log($"➖ Oyuncu lobiden ayrıldı: {SteamFriends.GetFriendPersonaName(userChanged)}");
        
            if (lobbyUIController != null)
            {
                lobbyUIController.OnPlayerLeft(userChanged);
            }
        
            // ÖNEMLI: Eğer ayrılan oyuncu host idiyse, yeni host'u kontrol et
            CSteamID realOwner = SteamMatchmaking.GetLobbyOwner(currentLobbyID);
            CSteamID myID = SteamUser.GetSteamID();
        
            if (realOwner == myID && !NetworkManager.Instance.isHost)
            {
                Debug.Log("Eski host ayrıldı, SİZ YENİ HOST'SUNUZ!");
                NetworkManager.Instance.isHost = true;
            
                // Lobby data'yı güncelle
                SteamMatchmaking.SetLobbyData(currentLobbyID, "host", myID.ToString());
            
                if (lobbyUIController != null)
                {
                    lobbyUIController.OnHostChanged(true);
                    lobbyUIController.AddChatMessage("SİSTEM", "Eski host ayrıldı, siz artık yeni HOST'sunuz!", Color.yellow);
                }
            }
        }
    }
}
