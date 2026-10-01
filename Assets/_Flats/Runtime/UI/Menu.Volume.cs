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
            musicVolumeSlider = sound.Find("Volume-BGM").GetComponentInChildren<Slider>(true);
            musicVolumeSlider.onValueChanged.AddListener(value => SetVolume(true, Mathf.RoundToInt(value), true));
        }
        if (masterVolumeSlider == null)
        {
            masterVolumeSlider = sound.Find("Volume-All").GetComponentInChildren<Slider>(true);
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
            // Applied at once at the level of the page shown: full while playing, the menu's reduced level while a menu
            // or the pause menu is open (CloseMenu restores the full level on resume).
            ApplyListenerVolume();
            // A dragged slider makes no sound of its own, and with the simulation frozen in the pause menu there may be
            // nothing playing to judge the new level by: one menu tick per step makes the change audible. The Plus and
            // Minus buttons (save == false) already play their own click.
            if (save && changed) PlayMenuSound(pressSE);
        }
        var slider = music ? musicVolumeSlider : masterVolumeSlider;
        if (slider != null) slider.SetValueWithoutNotify(value);
        SettingValue(0, music ? "Volume-BGM" : "Volume-All").text = (value * 10) + "%";
        if (save && changed) SaveDataController.Save();
    }
}
