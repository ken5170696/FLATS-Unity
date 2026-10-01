using UnityEngine;
using UnityEngine.UI;

/// <summary>Local transaction receipt. The prefab owns every visual, position and motion parameter.</summary>
public sealed class RogueAcquisitionView : MonoBehaviour
{
    public CanvasGroup group;
    public RectTransform card;
    public Image icon, iconBack;
    public Text heading, title, pitch;
    [Min(0.01f)] public float duration = 1.2f, enterSeconds = 0.18f, fadeSeconds = 0.3f;
    public float startScale = 0.82f, peakScale = 1.06f;
    [Header("Authored notification layout")]
    public Vector2 screenAnchor = new Vector2(0.5f, 0f), screenOffset = new Vector2(0f, 8f);
    public Vector2 combatAnchor = new Vector2(0.5f, 0f), combatOffset = new Vector2(0f, 104f);
    [Tooltip("The card's pivot in each place: under the header of a run screen it hangs from its top edge, in combat it stands on its bottom edge.")]
    public Vector2 screenPivot = new Vector2(0.5f, 1f), combatPivot = new Vector2(0.5f, 0f);
    public float minimumWidth = 180f, maximumWidth = 540f, edgePadding = 12f;
    public float textInset = 48f, textRightPadding = 12f, headingGap = 8f;
    public bool reduceMotion;
    static RogueAcquisitionView shown;
    float started;
    Vector3 homeScale;

    public static void Show(string iconName, string name, string pitchText, Color tint)
    {
        if (shown == null)
        {
            var prefab = Resources.Load<RogueAcquisitionView>("UI/Roguelike/RogueAcquisition");
            if (prefab == null) return;
            shown = Instantiate(prefab);
        }
        shown.Bind(iconName, name, pitchText, tint);
    }

    void Awake()
    {
        homeScale = card != null ? card.localScale : Vector3.one;
        if (group != null) { group.alpha = 0; group.blocksRaycasts = false; group.interactable = false; }
    }

    public void Bind(string iconName, string name, string pitchText, Color tint)
    {
        var screen = FindFirstObjectByType<RogueScreenView>();
        reduceMotion = screen != null && screen.reduceMotion;
        if (iconBack != null) iconBack.color = tint;
        RogueIcons.Apply(icon, iconName);
        if (heading != null) heading.text = RoguelikeController.T("Acquired");
        if (title != null) title.text = name ?? "";
        if (pitch != null) { pitch.text = pitchText ?? ""; pitch.gameObject.SetActive(!string.IsNullOrEmpty(pitchText)); }
        bool replacing = group != null && group.alpha > 0f;
        Layout();
        started = Time.unscaledTime - (replacing ? enterSeconds : 0f);
        Paint(replacing ? enterSeconds : 0f);
    }

    void Update()
    {
        float elapsed = Time.unscaledTime - started;
        if (elapsed >= duration) { Destroy(gameObject); return; }
        Paint(elapsed);
    }

    void LateUpdate() { Layout(); }

    void Layout()
    {
        if (card == null || title == null || pitch == null || heading == null) return;
        bool screenOpen = Menu.current != "Playing";
        card.anchorMin = card.anchorMax = screenOpen ? screenAnchor : combatAnchor;
        card.pivot = screenOpen ? screenPivot : combatPivot;
        card.anchoredPosition = screenOpen ? screenOffset : combatOffset;
        var canvas = (RectTransform)transform;
        float headingWidth = heading.preferredWidth;
        float firstLine = headingWidth + headingGap + title.preferredWidth;
        float width = Mathf.Clamp(Mathf.Max(firstLine, pitch.preferredWidth) + textInset + textRightPadding,
            minimumWidth, Mathf.Min(maximumWidth, canvas.rect.width - edgePadding * 2f));
        card.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        heading.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, headingWidth);
        var name = title.rectTransform;
        name.anchoredPosition = new Vector2(textInset + headingWidth + headingGap, name.anchoredPosition.y);
        name.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1f, width - name.anchoredPosition.x - textRightPadding));
        pitch.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - textInset - textRightPadding);
    }

    void Paint(float elapsed)
    {
        float enter = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, enterSeconds));
        float scale = enter < 0.7f ? Mathf.Lerp(startScale, peakScale, Mathf.SmoothStep(0, 1, enter / 0.7f))
            : Mathf.Lerp(peakScale, 1, (enter - 0.7f) / 0.3f);
        if (card != null) card.localScale = homeScale * (reduceMotion ? 1 : scale);
        if (group != null) group.alpha = Mathf.Min(enter, Mathf.Clamp01((duration - elapsed) / Mathf.Max(0.01f, fadeSeconds)));
    }

    void OnDestroy() { if (shown == this) shown = null; }
}
