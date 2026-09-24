using UnityEngine;
using UnityEngine.UI;

public partial class Menu
{
    Slider musicVolumeSlider;
    Slider masterVolumeSlider;

    void InitializeVolumeSliders()
    {
        var sound = settingsScreen.GetChild(0);
        if (musicVolumeSlider == null)
        {
            musicVolumeSlider = sound.GetChild(0).GetComponentInChildren<Slider>(true);
            musicVolumeSlider.onValueChanged.AddListener(value => SetVolume(true, Mathf.RoundToInt(value), true));
        }
        if (masterVolumeSlider == null)
        {
            masterVolumeSlider = sound.GetChild(1).GetComponentInChildren<Slider>(true);
            masterVolumeSlider.onValueChanged.AddListener(value => SetVolume(false, Mathf.RoundToInt(value), true));
        }
        SetVolume(true, mySettings.sound_bgm, false);
        SetVolume(false, mySettings.sound_all, false);
    }

    void SetVolume(bool music, int value, bool save)
    {
        value = Mathf.Clamp(value, 0, 10);
        bool changed = value != (music ? mySettings.sound_bgm : mySettings.sound_all);
        if (music)
        {
            mySettings.sound_bgm = value;
            bgm1.volume = value / 10f;
            bgm2.volume = Singleplayer.chance ? value / 10f : 0;
        }
        else
        {
            mySettings.sound_all = value;
            ApplyListenerVolume();
        }
        var slider = music ? musicVolumeSlider : masterVolumeSlider;
        if (slider != null) slider.SetValueWithoutNotify(value);
        settingsScreen.GetChild(0).GetChild(music ? 0 : 1).GetChild(1)
            .GetComponent<Text>().text = (value * 10) + "%";
        if (save && changed) SaveDataController.Save();
    }
}
