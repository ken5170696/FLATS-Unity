using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One authored action row of the shooting-range menu's detail panel: what it does on the left, its current value on the
/// right. The view binds text and action; the look (block, focus frame, type) is the prefab's.</summary>
public sealed class RogueRangeOption : MonoBehaviour
{
    public Button button;
    public Image face;
    public Text label, value;
    Action action;

    public void Bind(string caption, string current, bool enabled, bool primary, Action onPress)
    {
        if (label != null) label.text = caption ?? "";
        if (value != null) value.text = current ?? "";
        action = onPress;
        var theme = FlatsUiTheme.Rogue;
        if (face != null) face.color = !enabled ? FlatsUiTheme.WithAlpha(theme.supply, .55f) : primary ? theme.brandPrimary : theme.ink;
        if (button != null && button.interactable != enabled) button.interactable = enabled;
    }

    public void Press() { if (button != null && button.interactable && action != null) action(); }
}
