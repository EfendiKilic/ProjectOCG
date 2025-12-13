using UnityEngine;
using UnityEngine.UI;
using Steamworks;
using System.Collections;

public class SteamAvatarLoader : MonoBehaviour
{
    // Steam avatar'ını Image component'ine yükle
    public static void LoadAvatar(CSteamID steamID, Image targetImage)
    {
        if (targetImage == null) return;
        
        // Orta boy avatar al (64x64)
        int avatarID = SteamFriends.GetMediumFriendAvatar(steamID);
        
        if (avatarID == -1)
        {
            Debug.LogWarning("Avatar henüz yüklenmedi, tekrar denenecek...");
            return;
        }
        
        if (avatarID == 0)
        {
            Debug.LogWarning("Avatar bulunamadı!");
            return;
        }
        
        // Avatar boyutlarını al
        uint width, height;
        bool success = SteamUtils.GetImageSize(avatarID, out width, out height);
        
        if (!success || width == 0 || height == 0)
        {
            Debug.LogWarning("Avatar boyutu alınamadı!");
            return;
        }
        
        // Avatar verisini al
        byte[] avatarData = new byte[width * height * 4]; // RGBA
        success = SteamUtils.GetImageRGBA(avatarID, avatarData, (int)(width * height * 4));
        
        if (!success)
        {
            Debug.LogWarning("Avatar verisi alınamadı!");
            return;
        }
        
        // Texture2D oluştur
        Texture2D avatarTexture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
        avatarTexture.LoadRawTextureData(avatarData);
        avatarTexture.Apply();
        
        // Sprite oluştur ve Image'a ata
        Sprite avatarSprite = Sprite.Create(
            avatarTexture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f)
        );
        
        targetImage.sprite = avatarSprite;
        
        Debug.Log($"Avatar yüklendi: {SteamFriends.GetFriendPersonaName(steamID)}");
    }
    
    // Asenkron avatar yükleme (callback ile)
    public static IEnumerator LoadAvatarAsync(CSteamID steamID, Image targetImage)
    {
        // Avatar yüklenene kadar bekle
        int maxRetries = 10;
        int retries = 0;
        
        while (retries < maxRetries)
        {
            int avatarID = SteamFriends.GetMediumFriendAvatar(steamID);
            
            if (avatarID > 0)
            {
                LoadAvatar(steamID, targetImage);
                yield break;
            }
            
            retries++;
            yield return new WaitForSeconds(0.1f);
        }
        
        Debug.LogWarning("Avatar yüklenemedi, maksimum deneme sayısı aşıldı!");
    }
}