using UnityEngine;
using UnityEngine.UI;

public partial class Menu
{
    Slider musicVolumeSlider;
    Slider masterVolumeSlider;

    void InitializeVolumeSliders()
    {
        var sound = mt.GetChild(6).GetChild(0);
        if (musicVolumeSlider == null)
            musicVolumeSlider = CreateVolumeSlider(sound.GetChild(0), true);
        if (masterVolumeSlider == null)
            masterVolumeSlider = CreateVolumeSlider(sound.GetChild(1), false);
        SetVolume(true, mySettings.sound_bgm, false);
        SetVolume(false, mySettings.sound_all, false);
    }

    Slider CreateVolumeSlider(Transform row, bool music)
    {
        // Keep the authored label and +/- controls, including their navigation,
        // animation and localization. Append children to preserve legacy indices.
        var readout = row.GetChild(1).GetComponent<Text>();
        readout.rectTransform.anchoredPosition = new Vector2(190, 0);
        readout.rectTransform.sizeDelta = new Vector2(70, 50);
        readout.raycastTarget = false;

        var root = VolumeRect("VolumeSlider", row, new Vector2(174, 50));
        var hit = root.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        var slider = root.gameObject.AddComponent<Slider>();
        slider.minValue = 0;
        slider.maxValue = 10;
        slider.wholeNumbers = true;
        slider.direction = Slider.Direction.LeftToRight;

        var track = VolumeRect("Track", root, new Vector2(154, 4));
        VolumeImage(track, new Color(1, 1, 1, .25f));
        var fill = VolumeRect("Fill", track, Vector2.zero);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        VolumeImage(fill, Color.white);
        slider.fillRect = fill;

        var handleArea = VolumeRect("HandleArea", root, new Vector2(154, 50));
        var handle = VolumeRect("Handle", handleArea, new Vector2(18, 24));
        var handleImage = VolumeImage(handle, Color.white);
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.transition = Selectable.Transition.ColorTint;
        var colors = slider.colors;
        colors.normalColor = new Color(.85f, .85f, .85f, 1);
        colors.highlightedColor = colors.selectedColor = Color.white;
        colors.pressedColor = new Color(.6f, .6f, .6f, 1);
        colors.fadeDuration = .08f;
        slider.colors = colors;
        // Automatic navigation lets horizontal input adjust by one step, while
        // vertical input moves between rows. Pointer/touch uses the full 50px row.
        slider.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        slider.onValueChanged.AddListener(value => SetVolume(music, Mathf.RoundToInt(value), true));
        return slider;
    }

    static RectTransform VolumeRect(string name, Transform parent, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        return rect;
    }

    static Image VolumeImage(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
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
        mt.GetChild(6).GetChild(0).GetChild(music ? 0 : 1).GetChild(1)
            .GetComponent<Text>().text = (value * 10) + "%";
        if (save && changed) SaveDataController.Save();
    }
}
