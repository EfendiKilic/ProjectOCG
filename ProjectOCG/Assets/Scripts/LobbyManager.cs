using UnityEngine;
using Steamworks;

public class LobbyManager : MonoBehaviour
{
    public LobbyUI lobbyUI; // Inspector'dan atanacak
    public LobbyUIController lobbyUIController; // Inspector'dan atanacak

    // Callback'ler (Steam'den gelen cevaplar için)
    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> lobbyEntered;
    protected Callback<LobbyMatchList_t> lobbyList;
    
    // Mevcut lobi ID'si
    private CSteamID currentLobbyID;
    
    void Start()
    {
        // Steam bağlantısı var mı kontrol et
        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam başlatılmamış!");
            return;
        }
        
        // Callback'leri kaydet
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        lobbyList = Callback<LobbyMatchList_t>.Create(OnLobbyList); 
        
        Callback<LobbyChatUpdate_t> lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);

        Debug.Log("LobbyManager hazır!");
    }
    
    // ===== OYUN KUR BUTONU İÇİN =====
    public void CreateLobby()
    {
        Debug.Log("🎮 Lobi oluşturuluyor...");
        
        // Public lobi oluştur, maksimum 4 oyuncu
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 4);
    }
    
    // Lobi oluşturulduğunda çağrılır
    void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("❌ Lobi oluşturulamadı!");
            return;
        }
        
        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        Debug.Log("✅ Lobi oluşturuldu! Lobi ID: " + currentLobbyID);
        
        // Lobi bilgilerini ayarla
        string lobbyName = SteamFriends.GetPersonaName() + "'nin Lobisi";
        SteamMatchmaking.SetLobbyData(currentLobbyID, "name", lobbyName);
        SteamMatchmaking.SetLobbyData(currentLobbyID, "host", SteamUser.GetSteamID().ToString());
        
        Debug.Log("📝 Lobi adı: " + lobbyName);
    }
    
    // ===== OYUN BUL BUTONU İÇİN =====
    public void FindLobbies()
    {
        Debug.Log("🔍 Lobiler aranıyor...");
        
        // Mevcut lobileri iste
        SteamMatchmaking.RequestLobbyList();
    }
    
    // Lobi listesi geldiğinde çağrılır
    void OnLobbyList(LobbyMatchList_t callback)
    {
        Debug.Log("📋 Bulunan lobi sayısı: " + callback.m_nLobbiesMatching);
        
        if (callback.m_nLobbiesMatching == 0)
        {
            Debug.Log("⚠️ Boş lobi bulunamadı! Otomatik olarak yeni lobi oluşturuluyor...");
            CreateLobby();
            return;
        }
        
        // İlk bulunan lobiye katıl
        for (int i = 0; i < callback.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(i);
            string lobbyName = SteamMatchmaking.GetLobbyData(lobbyID, "name");
            
            Debug.Log($"✅ Lobi bulundu: {lobbyName}");
            
            // Lobiye katıl
            SteamMatchmaking.JoinLobby(lobbyID);
            break; // Sadece ilkine katıl
        }
    }
    
    // Steam overlay'den lobiye katılma isteği geldiğinde
    void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("📨 Lobiye katılma isteği alındı!");
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }
    
    // Lobiye girildiğinde çağrılır
    void OnLobbyEntered(LobbyEnter_t callback)
    {
        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
    
        // Host mu yoksa oyuncu mu?
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
    
        // Lobideki oyuncu sayısını göster
        int playerCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyID);
        Debug.Log($"👥 Lobide {playerCount} oyuncu var");
    
        // P2P bağlantılarını kur
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
    
    // Lobiden ayrıl
    public void LeaveLobby()
    {
        if (currentLobbyID != CSteamID.Nil)
        {
            Debug.Log("🚪 Lobiden ayrılıyorsunuz...");
            SteamMatchmaking.LeaveLobby(currentLobbyID);
            currentLobbyID = CSteamID.Nil;
        }
    }
    
    // Lobiye oyuncu girdiğinde/çıktığında
    void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
    {
        CSteamID userChanged = new CSteamID(callback.m_ulSteamIDUserChanged);
    
        // Lobiye giriş
        if ((callback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0)
        {
            Debug.Log($"➕ Oyuncu lobiye katıldı!");
        
            if (NetworkManager.Instance.isHost)
            {
                NetworkManager.Instance.ConnectToLobbyMembers(currentLobbyID);
            }
        
            // UI'yı güncelle
            if (lobbyUIController != null)
            {
                lobbyUIController.OnPlayerJoined(userChanged);
            }
        }
    
        // Lobiden çıkış
        if ((callback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft) != 0)
        {
            Debug.Log($"➖ Oyuncu lobiden ayrıldı!");
        
            // UI'yı güncelle
            if (lobbyUIController != null)
            {
                lobbyUIController.OnPlayerLeft(userChanged);
            }
        }
    }
}