using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One active-effect chip on the HUD effect row (QA-51): a square icon block (plate, icon, a frame whose ring drains with the
/// time left), a stack badge in its corner ("x3", "+24%") and, in Full mode, a short name on a tray to the right. Debuffs use
/// the red frame, plate and name colours. Authored in Resources/UI/Roguelike/Meta/RogueEffectRow (ChipTemplate); colours,
/// sizes and the pop are Inspector values. RogueEffectRowView binds and updates it; the chip only presents.
/// </summary>
public class RogueEffectChip : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] Image icon;
    [SerializeField] Text label;
    [SerializeField, Tooltip("Filled (Radial 360) frame over the track: the share of the effect's time left")] Image ring;
    [SerializeField, Tooltip("The frame's track under the ring; always shown")] Image frame;
    [SerializeField, Tooltip("Square behind the icon")] Image plate;
    [SerializeField, Tooltip("Plate behind the name (the chip's own Image)")] Image tray;
    [SerializeField, Tooltip("Corner badge with the stack count or bonus")] GameObject stackRoot;
    [SerializeField] Text stackText;
    [SerializeField] CanvasGroup group;

    [Header("Buff colours")]
    [SerializeField] Color buffRing = new Color(0.239f, 0.863f, 0.518f, 1f);
    [SerializeField] Color buffFrame = new Color(1f, 1f, 1f, 0.16f);
    [SerializeField] Color buffPlate = new Color(0.149f, 0.157f, 0.2f, 1f);
    [SerializeField] Color buffLabel = new Color(1f, 1f, 1f, 1f);
    [Header("Debuff colours")]
    [SerializeField] Color debuffRing = new Color(1f, 0.353f, 0.416f, 1f);
    [SerializeField] Color debuffFrame = new Color(1f, 0.353f, 0.416f, 0.55f);
    [SerializeField] Color debuffPlate = new Color(0.29f, 0.102f, 0.133f, 1f);
    [SerializeField] Color debuffLabel = new Color(1f, 0.64f, 0.67f, 1f);

    [Header("Size (canvas units)")]
    [SerializeField, Tooltip("Width and height of the icon block; the chip is this wide without a name")] float iconBlock = 32f;
    [SerializeField, Tooltip("Space between the icon block and the name")] float labelGap = 6f;
    [SerializeField, Tooltip("Space after the name")] float labelPad = 8f;
    [SerializeField, Tooltip("Longest name before the chip stops growing (names are short labels; this only guards a translation)")] float maxLabelWidth = 150f;

    [Header("Stack badge (sized in code from its text; it hangs off the block's lower right corner, clear of the icon)")]
    [SerializeField, Tooltip("Badge height (canvas units)")] float stackHeight = 12f;
    [SerializeField, Tooltip("Space left and right of the badge text (canvas units)")] float stackPadding = 3f;
    [SerializeField, Tooltip("Widest badge (canvas units)")] float stackMaxWidth = 44f;

    [Header("Motion (unscaled time)")]
    [SerializeField, Tooltip("Scale over time when the chip appears")] AnimationCurve pop = new AnimationCurve(new Keyframe(0, 1.12f), new Keyframe(0.15f, 1f));
    [SerializeField, Tooltip("Share of the pop replayed when the stack text changes")] [Range(0f, 1f)] float bump = 0.5f;

    bool labelShown, negative;
    string shownStacks;
    float popStarted = -10f, popAmount = 1f, popEnd;

    /// <summary>The label is set to its English key: a FlatsLocalizedText translates it and picks the font itself.</summary>
    public void Bind(Sprite sprite, string labelKey, bool isNegative)
    {
        negative = isNegative;
        if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
        if (label != null)
        {
            label.text = label is FlatsLocalizedText ? (labelKey ?? "") : FlatsLocalization.Translate(labelKey ?? "");
            label.color = negative ? debuffLabel : buffLabel;
        }
        if (frame != null) frame.color = negative ? debuffFrame : buffFrame;
        if (ring != null) ring.color = negative ? debuffRing : buffRing;
        if (plate != null) plate.color = negative ? debuffPlate : buffPlate;
        shownStacks = null;
        SetStacks("");
        SetRing(-1f);
        SetAlpha(1f);
        Relayout();   // a recycled chip gets a new name: measure it again
    }

    /// <summary>Icon-only chips are just the block, so a busy row stays narrow.</summary>
    public void ShowLabel(bool show)
    {
        if (show == labelShown) return;
        labelShown = show;
        Relayout();
    }

    /// <summary>After a language change: the name picks its font again, then the chip is measured again.</summary>
    public void RefreshLanguage()
    {
        if (label is FlatsLocalizedText) label.text = label.text;   // its setter chooses the Latin or CJK font for the new language
        Relayout();
    }

    /// <summary>Width from the name as it reads now.</summary>
    public void Relayout()
    {
        if (label != null && label.gameObject.activeSelf != labelShown) label.gameObject.SetActive(labelShown);
        float width = iconBlock;
        if (labelShown && label != null) width += labelGap + Mathf.Min(maxLabelWidth, Mathf.Ceil(label.preferredWidth)) + labelPad;
        var rt = (RectTransform)transform;
        if (rt.sizeDelta.x != width || rt.sizeDelta.y != iconBlock)
        {
            rt.sizeDelta = new Vector2(width, iconBlock);
            var row = rt.parent as RectTransform;
            if (row != null) LayoutRebuilder.MarkLayoutForRebuild(row);   // the row's layout group places chips by their size
        }
        if (tray != null) tray.enabled = labelShown;
    }

    /// <summary>"x3", "+24%" or empty (no badge). Texts come from a cache, so an unchanged value is the same string.</summary>
    public void SetStacks(string text)
    {
        text = text ?? "";
        if (ReferenceEquals(text, shownStacks) || text == shownStacks) return;
        bool grew = !string.IsNullOrEmpty(shownStacks) && ValueOf(text) > ValueOf(shownStacks);
        shownStacks = text;
        bool show = text.Length > 0;
        if (stackText != null) stackText.text = text;
        if (stackRoot != null && stackRoot.activeSelf != show) stackRoot.SetActive(show);
        if (show) SizeStack();
        if (show && grew) Pop(bump);   // a new stack or a bigger bonus: a small nudge (never on decay, never a bounce)
    }

    /// <summary>The badge takes its text's width (QA-36 round 2: a layout-fitted badge grew to the whole block and hid the icon).</summary>
    void SizeStack()
    {
        if (stackRoot == null || stackText == null) return;
        var rt = stackRoot.transform as RectTransform;
        if (rt == null) return;
        float width = Mathf.Min(stackMaxWidth, Mathf.Ceil(stackText.preferredWidth) + stackPadding * 2f);
        var size = new Vector2(width, stackHeight);
        if ((rt.sizeDelta - size).sqrMagnitude > 0.01f) rt.sizeDelta = size;
    }

    /// <summary>The signed number in a badge text ("+24%" 24, "-20%" -20, "x3" 3), without allocating.</summary>
    static int ValueOf(string text)
    {
        int v = 0; bool negative = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '-' && v == 0) negative = true;
            else if (c >= '0' && c <= '9') v = v * 10 + (c - '0');
        }
        return negative ? -v : v;
    }

    /// <summary>Ring share 0..1; below 0 hides the ring (the track stays).</summary>
    public void SetRing(float fraction)
    {
        if (ring == null) return;
        bool show = fraction >= 0f;
        if (ring.enabled != show) ring.enabled = show;
        if (show) ring.fillAmount = Mathf.Clamp01(fraction);
    }

    public void SetAlpha(float alpha)
    {
        if (group != null) group.alpha = Mathf.Clamp01(alpha);
    }

    /// <summary>Plays the appear scale (amount 1) or a share of it.</summary>
    public void Pop(float amount = 1f)
    {
        popStarted = Time.unscaledTime; popAmount = amount;
        popEnd = pop != null && pop.length > 0 ? pop[pop.length - 1].time : 0f;   // AnimationCurve.keys copies: read once here, not every frame
    }

    void OnDisable() { popStarted = -10f; transform.localScale = Vector3.one; }

    void Update()
    {
        float age = Time.unscaledTime - popStarted;
        float k = age >= popEnd || pop == null || pop.length == 0 ? 1f : 1f + (pop.Evaluate(age) - 1f) * popAmount;
        if (transform.localScale.x != k) transform.localScale = new Vector3(k, k, 1f);
    }
}
