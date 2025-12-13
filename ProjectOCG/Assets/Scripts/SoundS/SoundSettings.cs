using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SoundSettings : MonoBehaviour
{
    public AudioMixer mainMixer;
    public Slider ambianceSlider;
    public Slider musicSlider;
    public Slider effectSlider;

    private void Start()
    {
        // Ambiance
        if (PlayerPrefs.HasKey("ambianceVolume"))
        {
            LoadAmbianceVolume();
        }
        else
        { 
            SetAmbianceVolume();
        }

        // Music
        if (PlayerPrefs.HasKey("musicVolume"))
        {
            LoadMusicVolume();
        }
        else
        { 
            SetMusicVolume();
        }

        // Effect
        if (PlayerPrefs.HasKey("effectVolume"))
        {
            LoadEffectVolume();
        }
        else
        { 
            SetEffectVolume();
        }
    }

    // AMBIANCE
    public void SetAmbianceVolume()
    {
        float volume = ambianceSlider.value;
        mainMixer.SetFloat("ambiance", Mathf.Log10(volume) * 20);
        PlayerPrefs.SetFloat("ambianceVolume", volume);
    }

    private void LoadAmbianceVolume()
    {
        ambianceSlider.value = PlayerPrefs.GetFloat("ambianceVolume");
        SetAmbianceVolume();
    }

    // MUSIC
    public void SetMusicVolume()
    {
        float volume = musicSlider.value;
        mainMixer.SetFloat("music", Mathf.Log10(volume) * 20);
        PlayerPrefs.SetFloat("musicVolume", volume);
    }

    private void LoadMusicVolume()
    {
        musicSlider.value = PlayerPrefs.GetFloat("musicVolume");
        SetMusicVolume();
    }

    // EFFECT
    public void SetEffectVolume()
    {
        float volume = effectSlider.value;
        mainMixer.SetFloat("effect", Mathf.Log10(volume) * 20);
        PlayerPrefs.SetFloat("effectVolume", volume);
    }

    private void LoadEffectVolume()
    {
        effectSlider.value = PlayerPrefs.GetFloat("effectVolume");
        SetEffectVolume();
    }
}