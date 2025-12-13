using UnityEngine;
using DG.Tweening;

public class MainMenuManager : MonoBehaviour
{
    public GameObject SettingsPanel;
    
    public Vector2 settingsClosedPosition = new Vector2(600f, 0f);
    public Vector2 settingsOpenPosition = new Vector2(0f, 0f);
    public float settingsAnimationDuration = 0.5f;
    public Ease settingsAnimationEase = Ease.OutBack;
    
    private RectTransform settingsPanelRectTransform;
    private bool isSettingsPanelOpen = false;

    void Start()
    {
        if (SettingsPanel != null)
        {
            settingsPanelRectTransform = SettingsPanel.GetComponent<RectTransform>();
            settingsPanelRectTransform.anchoredPosition = settingsClosedPosition;
        }
    }
    
    public void OpenSettings()
    {
        Debug.Log("Opening Settings");
        
        if (isSettingsPanelOpen || settingsPanelRectTransform == null) return;
        
        isSettingsPanelOpen = true;
        SettingsPanel.SetActive(true);
        
        settingsPanelRectTransform.DOAnchorPos(settingsOpenPosition, settingsAnimationDuration)
            .SetEase(settingsAnimationEase)
            .SetUpdate(true);
    }

    public void CloseSettings()
    {
        Debug.Log("Close Settings");
        
        if (!isSettingsPanelOpen || settingsPanelRectTransform == null) return;
        
        isSettingsPanelOpen = false;
        
        settingsPanelRectTransform.DOAnchorPos(settingsClosedPosition, settingsAnimationDuration)
            .SetEase(settingsAnimationEase)
            .SetUpdate(true)
            .OnComplete(() => 
            {
                SettingsPanel.SetActive(false);
            });
    }
}