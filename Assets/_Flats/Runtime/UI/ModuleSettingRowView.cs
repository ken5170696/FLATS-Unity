using System;
using System.Globalization;
using Flats.Modules;
using UnityEngine;
using UnityEngine.UI;

// One authored row for any declared module setting. The form picks the controls that
// match the setting type; layout, typography and colours stay editable in the prefab.
public sealed class ModuleSettingRowView : MonoBehaviour
{
    public FlatsLocalizedText label;
    public Text value;
    [Tooltip("Choice and colour settings step through values with these buttons.")]
    public Button previous, next;
    public Slider slider;
    public Button toggle;
    public Image toggleTrack;
    public RectTransform toggleThumb;
    public Image swatch;
    public Color enabledColor = new Color(.8f, .098f, .4f);
    public Color disabledColor = new Color(.7f, .68f, .7f);
    [Tooltip("Colours offered for colour settings, in order.")]
    public Color[] palette =
    {
        Color.white, Color.black, new Color(1f, .2f, .2f), new Color(.2f, 1f, .3f), new Color(.2f, .9f, 1f),
        new Color(1f, .3f, 1f), new Color(1f, .85f, .1f), new Color(1f, .55f, .1f), new Color(.8f, .098f, .4f),
    };

    ModuleSettingSpec spec;
    Action<string> changed;
    bool binding;

    public void Bind(ModuleSettingSpec setting, string current, Action<string> onChanged)
    {
        spec = setting; changed = onChanged; binding = true;
        label.text = setting.label;
        bool numeric = setting.type == "int" || setting.type == "float";
        bool stepped = setting.type == "choice" || setting.type == "color";
        if (slider != null) slider.gameObject.SetActive(numeric);
        if (previous != null) previous.gameObject.SetActive(stepped);
        if (next != null) next.gameObject.SetActive(stepped);
        if (toggle != null) toggle.gameObject.SetActive(setting.type == "bool");
        if (swatch != null) swatch.gameObject.SetActive(setting.type == "color");
        if (numeric && slider != null)
        {
            slider.onValueChanged.RemoveAllListeners();
            slider.minValue = setting.min; slider.maxValue = setting.max;
            slider.wholeNumbers = setting.type == "int";
            slider.onValueChanged.AddListener(v => { if (!binding) Emit(v.ToString("R", CultureInfo.InvariantCulture)); });
        }
        Listen(previous, () => Step(-1));
        Listen(next, () => Step(1));
        Listen(toggle, () => Emit(Show() == "true" ? "false" : "true"));
        Show(current);
        binding = false;
    }

    string shown;
    string Show() { return shown; }

    public void Show(string current)
    {
        shown = current; binding = true;
        switch (spec.type)
        {
            case "bool":
                bool on = current == "true";
                if (toggleTrack != null) toggleTrack.color = on ? enabledColor : disabledColor;
                if (toggleThumb != null) toggleThumb.anchoredPosition = new Vector2(on ? 14 : -14, 0);
                value.text = on ? "On" : "Off";
                break;
            case "int":
            case "float":
                float number = float.Parse(current, CultureInfo.InvariantCulture);
                if (slider != null) slider.SetValueWithoutNotify(number);
                value.text = spec.type == "int" ? current : number.ToString("0.##", CultureInfo.InvariantCulture);
                break;
            case "color":
                if (swatch != null && ColorUtility.TryParseHtmlString(current, out var c)) swatch.color = c;
                value.text = current.ToUpperInvariant();
                break;
            default:
                // Choice values are ids; the catalogue translates their display names.
                value.text = ChoiceName(current);
                break;
        }
        binding = false;
    }

    static string ChoiceName(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        var text = new System.Text.StringBuilder();
        foreach (char c in id)
        {
            if (char.IsUpper(c) && text.Length > 0) text.Append(' ');
            text.Append(text.Length == 0 ? char.ToUpperInvariant(c) : c);
        }
        return text.ToString();
    }

    void Step(int direction)
    {
        if (spec.type == "choice")
        {
            int index = Array.IndexOf(spec.choices, shown);
            int count = spec.choices.Length;
            Emit(spec.choices[((index < 0 ? 0 : index) + direction + count) % count]);
        }
        else if (spec.type == "color" && palette.Length > 0)
        {
            int index = Array.FindIndex(palette, p => "#" + ColorUtility.ToHtmlStringRGB(p).ToLowerInvariant() == (shown ?? "").Substring(0, Math.Min(7, (shown ?? "").Length)).ToLowerInvariant());
            var colour = palette[((index < 0 ? -1 : index) + direction + palette.Length) % palette.Length];
            Emit("#" + ColorUtility.ToHtmlStringRGB(colour).ToLowerInvariant());
        }
    }

    void Emit(string next) { changed?.Invoke(next); }

    static void Listen(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }
}
