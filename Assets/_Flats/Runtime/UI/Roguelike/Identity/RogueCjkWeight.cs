using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Chinese text never uses the synthetic bold of FontStyle.Bold (UI design spec §3.2, G4): the fallback glyphs smear. Latin text keeps
/// the authored style. Hierarchy in Chinese comes from size and colour instead. Re-applied when the language changes.
/// </summary>
[DisallowMultipleComponent]
public sealed class RogueCjkWeight : MonoBehaviour
{
    [Tooltip("Style used for English (the authored style).")] public FontStyle latinStyle = FontStyle.Bold;
    [Tooltip("Style used for Traditional Chinese.")] public FontStyle chineseStyle = FontStyle.Normal;
    Text text;

    void OnEnable()
    {
        if (text == null) text = GetComponent<Text>();
        FlatsLocalization.Changed += Apply;
        Apply();
    }

    void OnDisable() { FlatsLocalization.Changed -= Apply; }

    void Apply()
    {
        if (text == null) return;
        var style = FlatsLocalization.IsChinese ? chineseStyle : latinStyle;
        if (text.fontStyle != style) text.fontStyle = style;
    }
}
