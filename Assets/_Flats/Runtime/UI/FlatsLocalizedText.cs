using UnityEngine;
using UnityEngine.UI;

// Localize presentation only: legacy menu code can still read the original value.
// Player-authored text is explicitly excluded and input values are never translated.
public sealed class FlatsLocalizedText : Text
{
    public bool translate = true;
    Font originalFont;
    public Font SourceFont => originalFont != null ? originalFont : font;
    public static Font GetSourceFont(Text label) => label is FlatsLocalizedText localized ? localized.SourceFont : label.font;
    bool rendering;
    public override string text
    {
        get => base.text;
        set { base.text = value; UpdateFont(); }
    }
    string DisplayText
    {
        get
        {
            var input = GetComponentInParent<InputField>(true);
            return translate && (input == null || input.placeholder == this)
                ? FlatsLocalization.Translate(m_Text) : m_Text;
        }
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        FlatsLocalization.Changed += RefreshLanguage;
        RefreshLanguage();
    }
    protected override void OnDisable()
    {
        FlatsLocalization.Changed -= RefreshLanguage;
        base.OnDisable();
    }
    void RefreshLanguage()
    {
        UpdateFont();
        SetAllDirty();
    }
    void UpdateFont()
    {
        if (!Application.isPlaying) return;
        if (originalFont == null) originalFont = font;
        bool needsChinese = false;
        foreach (char character in DisplayText ?? "")
            if (character >= '\u2e80') { needsChinese = true; break; }
        font = needsChinese && FlatsLocalization.ChineseFont != null
            ? FlatsLocalization.ChineseFont : originalFont;
    }
    protected override void OnPopulateMesh(VertexHelper helper)
    {
        if (!Application.isPlaying || rendering) { base.OnPopulateMesh(helper); return; }
        string source = m_Text;
        rendering = true;
        try { m_Text = DisplayText; base.OnPopulateMesh(helper); }
        finally { m_Text = source; rendering = false; }
    }
    public override float preferredWidth => Measure(true);
    public override float preferredHeight => Measure(false);
    float Measure(bool width)
    {
        string source = m_Text;
        try
        {
            if (Application.isPlaying && !rendering) m_Text = DisplayText;
            return width ? base.preferredWidth : base.preferredHeight;
        }
        finally { m_Text = source; }
    }
}
