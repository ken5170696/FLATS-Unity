using UnityEngine;
using UnityEngine.UI;

/// <summary>One line of the TAB overview: icon tile, label, value and an optional bar. Reference bag for the authored RogueStatRow prefab.</summary>
public class RogueStatRowView : MonoBehaviour
{
    public Image panel, icon, iconBack, barFill, barBack;
    public Text label, value, sub;

    public void Bind(string iconName, string labelText, string valueText, string subText, float bar, Color tint)
    {
        if (label != null) label.text = labelText ?? "";
        if (value != null) value.text = valueText ?? "";
        if (sub != null) { sub.text = subText ?? ""; sub.gameObject.SetActive(!string.IsNullOrEmpty(subText)); }
        RogueIcons.Apply(icon, iconName);
        if (iconBack != null) iconBack.color = tint;
        bool showBar = bar >= 0f;
        if (barBack != null) barBack.gameObject.SetActive(showBar);
        if (barFill != null && showBar) { barFill.fillAmount = Mathf.Clamp01(bar); barFill.color = tint; }
    }
}
