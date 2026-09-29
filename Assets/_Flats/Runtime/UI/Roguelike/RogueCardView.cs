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
        if (iconBack != null) iconBack.color = empty ? new Color(0, 0, 0, 0.08f) : tint;
        if (panel != null) panel.color = empty ? new Color(1, 1, 1, 0.35f) : Color.white;
        if (icon != null) icon.color = empty ? new Color(0, 0, 0, 0.25f) : Color.white;
    }
}
