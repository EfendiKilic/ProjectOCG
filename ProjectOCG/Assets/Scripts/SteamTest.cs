using UnityEngine;
using Steamworks;

public class SteamTest : MonoBehaviour
{
    void Start()
    {
        if (SteamManager.Initialized)
        {
            string userName = SteamFriends.GetPersonaName();
            Debug.Log("✅ Steam bağlantısı BAŞARILI!");
            Debug.Log("Steam Kullanıcı Adı: " + userName);
            Debug.Log("Steam ID: " + SteamUser.GetSteamID());
        }
        else
        {
            Debug.LogError("❌ Steam bağlantısı BAŞARISIZ!");
        }
    }
}
