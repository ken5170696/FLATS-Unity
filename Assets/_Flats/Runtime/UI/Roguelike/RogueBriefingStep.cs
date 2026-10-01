using UnityEngine;
using UnityEngine.UI;

/// <summary>One pictured step of the mission card (authored template inside RogueHud/Briefing): number, icon, a short instruction and,
/// for a step done with a control, the player's own binding in a key cap. Instances are made from the template at runtime.</summary>
public class RogueBriefingStep : MonoBehaviour
{
    public Image back, icon;
    public Text number, text;
    [Tooltip("Key cap shown when the step names a control (hidden on touch, where the on-screen button is the control).")] public GameObject keyCap;
    public Text keyText;
    [Tooltip("Colour of the tile while this step is the current one on the tracker; idle otherwise.")] public Color currentColor = new Color(0.8f, 0.098f, 0.4f, 0.95f), idleColor = new Color(1f, 1f, 1f, 0.08f);

    public void Bind(int index, string iconName, string line, string key, bool current)
    {
        if (number != null) number.text = (index + 1).ToString();
        RogueIcons.Apply(icon, iconName);
        if (text != null) text.text = line ?? "";
        bool hasKey = !string.IsNullOrEmpty(key);
        if (keyCap != null && keyCap.activeSelf != hasKey) keyCap.SetActive(hasKey);
        if (keyText != null) keyText.text = key ?? "";
        if (back != null) back.color = current ? currentColor : idleColor;
    }
}
