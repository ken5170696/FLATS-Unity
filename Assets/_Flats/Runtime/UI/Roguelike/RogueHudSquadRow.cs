using UnityEngine;
using UnityEngine.UI;

/// <summary>One teammate line on the HUD squad list (authored inside RogueHud.prefab).</summary>
public class RogueHudSquadRow : MonoBehaviour
{
    public Image icon, iconBack, hpFill, hpBack;
    public Text nameText, stateText;

    public void Bind(string name, string iconName, float hp, string state, Color tint)
    {
        if (nameText != null) nameText.text = name ?? "";
        if (stateText != null)
        {
            stateText.text = state ?? "";
            // The state pill (Downed / Dead / Carry...) is a dark badge; an empty one reads as a stray black block.
            var pill = stateText.transform.parent;
            if (pill != null && pill != transform && pill.gameObject.activeSelf != !string.IsNullOrEmpty(state)) pill.gameObject.SetActive(!string.IsNullOrEmpty(state));
        }
        RogueIcons.Apply(icon, iconName);
        if (iconBack != null) iconBack.color = tint;
        if (hpFill != null) { hpFill.fillAmount = Mathf.Clamp01(hp); hpFill.color = tint; }
    }
}
