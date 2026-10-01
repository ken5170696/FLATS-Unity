using UnityEngine;
using UnityEngine.UI;

/// <summary>Small grid card for the TAB overview (authored RogueCard prefab): icon tile, title, one-line sub text.</summary>
public class RogueCardView : MonoBehaviour
{
    public Image panel, iconBack, icon;
    public Text title, sub;

    public void Bind(string iconName, string titleText, string subText, Color tint, bool empty)
    {
        if (title != null) title.text = titleText ?? "";
        if (sub != null) sub.text = subText ?? "";
        RogueIcons.Apply(icon, iconName);
        // an empty slot is a sunken, quiet card; a filled one the raised card with its category block (Roguelike theme)
        var theme = FlatsUiTheme.Rogue;
        if (iconBack != null) iconBack.color = empty ? theme.lineSubtle : tint;
        if (panel != null) panel.color = empty ? FlatsUiTheme.WithAlpha(theme.surfaceSunken, 0.9f) : theme.surfaceRaised;
        if (icon != null) icon.color = empty ? theme.textMuted : Color.white;
    }
}
