using UnityEngine;
using UnityEngine.UI;

/// <summary>One teammate line on the HUD squad list (authored inside RogueHud.prefab).</summary>
public class RogueHudSquadRow : MonoBehaviour
{
    public Image icon, iconBack, hpFill, hpBack;
    public Text nameText, stateText;
    [Tooltip("Colour of the tactical-shield strip drawn along the top of the health bar.")] public Color shieldColor = new Color(0.45f, 0.75f, 1f);
    public Image shieldFill;

    public void Bind(string name, string iconName, float hp, string state, Color tint) { Bind(name, iconName, hp, 0f, state, tint); }

    public void Bind(string name, string iconName, float hp, float shield, string state, Color tint)
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
        BindShield(Mathf.Clamp01(shield));
    }

    // A teammate's tactical shield: a thin strip over the top of the health bar, cloned from the authored fill the first time
    // a shield is seen (the row template has no shield element of its own; the local vitals panel does).
    void BindShield(float shield)
    {
        if (shieldFill == null)
        {
            if (shield <= 0f || hpFill == null) return;
            shieldFill = Instantiate(hpFill, hpFill.transform.parent);
            shieldFill.name = "ShieldFill";
            shieldFill.transform.SetSiblingIndex(hpFill.transform.GetSiblingIndex() + 1);
            shieldFill.raycastTarget = false;
            // a thinner strip centred on the health bar (the pivot stays: moving it would shift a fixed-size rect); scaling a flat fill image loses nothing
            shieldFill.rectTransform.localScale = new Vector3(1f, 0.45f, 1f);
        }
        shieldFill.fillAmount = shield;
        shieldFill.color = shieldColor;
        if (shieldFill.enabled != shield > 0f) shieldFill.enabled = shield > 0f;
    }
}
