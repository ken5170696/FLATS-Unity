using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD list of the local player's active effects (QA-51), bottom left above the health panel: every buff and debuff with an
/// icon, a draining ring when it has a clock, a stack badge ("x3", "+24%") and, in Full mode with few effects, its name.
/// Two inputs: triggers (RogueMetaFeedback.Triggered: a refund, a mark, an elite's harder hit) and states the owner's
/// RogueMetaRuntime reports every frame through SetState (Kill Frenzy, Berserker stacks, Assault Rush, Slowed, Jammed...).
/// What is shown, in which order and for how long is Core's EffectRowModel; this view instances the authored chip template,
/// plays the cue and places the row. Layout, colours, sizes and the cue live on the prefab (Resources/UI/Roguelike/Meta/RogueEffectRow).
/// Only the owner's runtime opens a row, so teammates' effects never show on your HUD.
/// </summary>
public class RogueEffectRowView : MonoBehaviour
{
    [SerializeField] RectTransform container;
    [SerializeField] RogueEffectChip chipTemplate;
    [SerializeField] AudioSource cueSource;
    [SerializeField] AudioClip cue;
    [SerializeField, Range(0f, 1f)] float cueVolume = 0.35f;
    [SerializeField, Tooltip("Seconds an instant effect (refund, mark, reload) stays visible")] float instantSeconds = 1.4f;
    [SerializeField, Tooltip("At most this many chips at once; debuffs first, then effects with a clock, then conditions, then notices")] int maxChips = 5;
    [SerializeField, Tooltip("Seconds a chip takes to fade when its effect ends")] float fadeSeconds = 0.3f;
    [Header("Placement")]
    [SerializeField, Tooltip("Anchored position on a narrow canvas (a phone held upright), above the touch Interact button. " +
        "Elsewhere the row stays where the prefab's RectTransform puts it.")] Vector2 narrowPosition = new Vector2(12f, 212f);
    [SerializeField, Tooltip("Canvas width (units) below which the narrow position is used (RogueHudView.narrowWidth uses the same value)")] float narrowWidth = 640f;

    /// <summary>More active chips than this show icons only (the hub help text reads the same constant).</summary>
    public const int LabelLimit = 3;
    [SerializeField] CanvasGroup group;

    // ---- player preference (Roguelike hub > Settings): Full, IconsOnly or Off, and opacity 0.5..1
    public enum Mode { Full = 0, IconsOnly = 1, Off = 2 }
    const string ModeKey = "RogueEffectRowMode", OpacityKey = "RogueEffectRowOpacity";
    public const float DefaultOpacity = 0.8f, MinOpacity = 0.5f;
    public static Mode CurrentMode { get { return (Mode)Mathf.Clamp(FlatsPreferences.GetInt(ModeKey, 0), 0, 2); } }
    public static float CurrentOpacity { get { float v; return float.TryParse(FlatsPreferences.GetString(OpacityKey, ""), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? Mathf.Clamp(v, MinOpacity, 1f) : DefaultOpacity; } }
    public static event System.Action SettingsChanged;
    public static void SetPreferences(Mode mode, float opacity)
    {
        FlatsPreferences.SetInt(ModeKey, (int)mode);
        FlatsPreferences.SetString(OpacityKey, Mathf.Clamp(opacity, MinOpacity, 1f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        FlatsPreferences.Save();
        if (SettingsChanged != null) SettingsChanged();
    }

    readonly EffectRowModel model = new EffectRowModel();
    readonly Dictionary<string, RogueEffectChip> live = new Dictionary<string, RogueEffectChip>();
    readonly Queue<RogueEffectChip> pool = new Queue<RogueEffectChip>();
    readonly List<string> scratch = new List<string>();
    float lastCue = -10f;
    int syncedVersion = -1, layoutNarrow = -1;
    Mode mode;
    Vector2 authoredPosition;
    RectTransform parentRect;

    public static RogueEffectRowView Open(Transform hudParent)
    {
        var prefab = Resources.Load<GameObject>("UI/Roguelike/Meta/RogueEffectRow");
        if (prefab == null || hudParent == null) { Debug.LogWarning("FLATS_ROGUE_UI missing Resources/UI/Roguelike/Meta/RogueEffectRow"); return null; }
        var canvas = hudParent.GetComponentInParent<Canvas>();
        var go = Instantiate(prefab, canvas != null ? canvas.transform : hudParent, false);
        go.name = "RogueEffectRow";
        return go.GetComponent<RogueEffectRowView>();
    }

    void Awake()
    {
        if (chipTemplate != null) chipTemplate.gameObject.SetActive(false);
        model.Capacity = Mathf.Max(1, maxChips); model.InstantSeconds = instantSeconds; model.FadeSeconds = fadeSeconds;
        var rt = (RectTransform)transform;
        authoredPosition = rt.anchoredPosition;
        parentRect = transform.parent as RectTransform;
    }

    void OnEnable()
    {
        RogueMetaFeedback.Triggered += OnTriggered; SettingsChanged += ApplySettings; FlatsLocalization.Changed += OnLanguageChanged;
        ApplySettings();
    }

    void OnDisable() { RogueMetaFeedback.Triggered -= OnTriggered; SettingsChanged -= ApplySettings; FlatsLocalization.Changed -= OnLanguageChanged; }

    void ApplySettings()
    {
        mode = CurrentMode;
        if (group != null) group.alpha = CurrentOpacity;
        if (container != null && container.gameObject.activeSelf != (mode != Mode.Off)) container.gameObject.SetActive(mode != Mode.Off);
        syncedVersion = -1;   // labels follow the mode
    }

    void OnLanguageChanged() { foreach (var c in live.Values) c.RefreshLanguage(); }

    // ------------------------------------------------------------------ inputs
    /// <summary>
    /// A state of the local player, reported every frame by its owner while the row exists (true or false). <paramref name="fraction"/>
    /// is the ring (0..1, the share of its time left) or below 0 for none; <paramref name="stacks"/> the corner text ("x3", "+24%")
    /// or empty. Debuff look and label come from the effect table (Core PlayerEffects). Allocation-free for known ids.
    /// </summary>
    public void SetState(string id, bool active, float fraction = -1f, string stacks = null)
    {
        if (!active && model.Find(id) == null) return;   // the common case: nothing to end
        var def = RogueMetaFeedback.Effect(id);
        if (def == null) return;
        if (model.SetState(def, active, fraction, stacks, Time.time) && def.Cue) PlayCue();
    }

    /// <summary>Everything ends at once (run end, the player object leaving).</summary>
    public void Clear()
    {
        model.Clear();
        scratch.Clear();
        foreach (var k in live.Keys) scratch.Add(k);
        foreach (var k in scratch) Recycle(k);
        syncedVersion = model.Version;
    }

    void OnTriggered(string source, float seconds)
    {
        var def = RogueMetaFeedback.Effect(source);
        if (def == null) return;
        model.Pulse(def, seconds, Time.time);
        if (def.Kind != PlayerEffectKind.State && def.Cue) PlayCue();   // every trigger of a notice cues, as before (rate-limited)
    }

    void PlayCue()
    {
        if (mode == Mode.Off || cue == null || cueSource == null || Time.unscaledTime - lastCue <= 0.15f) return;
        cueSource.PlayOneShot(cue, cueVolume);
        lastCue = Time.unscaledTime;
    }

    // ------------------------------------------------------------------ presentation
    void Update()
    {
        float now = Time.time;
        model.Tick(now);
        UpdatePlacement();
        if (mode == Mode.Off || chipTemplate == null || container == null) return;
        if (model.Version != syncedVersion) Sync();
        var entries = model.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            RogueEffectChip chip;
            if (!e.Shown || !live.TryGetValue(e.Id, out chip)) continue;
            chip.SetRing((float)e.Fraction(now));
            chip.SetStacks(e.Stacks);
            chip.SetAlpha((float)e.Alpha(now, fadeSeconds));
        }
    }

    /// <summary>Chips for the shown entries, oldest nearest the health panel (the container stacks upward), labels by count.</summary>
    void Sync()
    {
        syncedVersion = model.Version;
        scratch.Clear();
        foreach (var kv in live) { var e = model.Find(kv.Key); if (e == null || !e.Shown) scratch.Add(kv.Key); }
        for (int i = 0; i < scratch.Count; i++) Recycle(scratch[i]);
        var entries = model.Entries;
        int sibling = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (!e.Shown) continue;
            RogueEffectChip chip;
            if (!live.TryGetValue(e.Id, out chip))
            {
                chip = pool.Count > 0 ? pool.Dequeue() : Instantiate(chipTemplate, container, false);
                chip.gameObject.SetActive(true);
                chip.Bind(RogueMetaFeedback.IconSprite(e.Def.Icon), e.Def.Label, e.Def.Negative);
                chip.Pop();
                live[e.Id] = chip;
            }
            chip.transform.SetSiblingIndex(sibling++);
        }
        bool labels = PlayerEffects.ShowLabels(mode == Mode.Full, model.ShownCount, LabelLimit);
        foreach (var c in live.Values) c.ShowLabel(labels);
    }

    void Recycle(string id)
    {
        RogueEffectChip chip;
        if (!live.TryGetValue(id, out chip)) return;
        live.Remove(id);
        chip.gameObject.SetActive(false);
        pool.Enqueue(chip);
    }

    // ------------------------------------------------------------------ placement driven by the HUD (QA-36 round 2)
    float drivenFloor = float.NaN;

    /// <summary>The HUD stacks the row on top of whatever sits under it (health panel, invulnerability badge, hint line, touch keys):
    /// the row's bottom goes to this height above the canvas bottom. NaN hands placement back to the prefab and the narrow position.</summary>
    public void SetFloor(float bottom)
    {
        drivenFloor = bottom;
        UpdatePlacement();
    }

    /// <summary>Widest shown chip (canvas units); 0 while no chip shows. The HUD only moves the row for what it really overlaps.</summary>
    public float ContentWidth
    {
        get
        {
            if (mode == Mode.Off) return 0f;
            float w = 0f;
            foreach (var c in live.Values) if (c != null && c.gameObject.activeSelf) w = Mathf.Max(w, ((RectTransform)c.transform).rect.width);
            return w;
        }
    }

    static readonly Vector3[] contentCorners = new Vector3[4];
    static readonly List<Graphic> contentGraphics = new List<Graphic>();
    /// <summary>The shown chips' bounds in another rect's local space (off-screen waypoint markers keep out of it). Every visible part
    /// counts, including the stack badge that hangs outside the chip's own rect (QA-36 round 5).</summary>
    public bool TryGetContentRect(RectTransform space, out Rect rect)
    {
        rect = default(Rect);
        if (space == null || mode == Mode.Off) return false;
        bool any = false; float xMin = 0f, yMin = 0f, xMax = 0f, yMax = 0f;
        foreach (var c in live.Values)
        {
            if (c == null || !c.gameObject.activeInHierarchy) continue;
            contentGraphics.Clear();
            c.GetComponentsInChildren(false, contentGraphics);
            foreach (var g in contentGraphics)
            {
                if (g == null || !g.enabled || g.color.a <= 0.01f) continue;
                g.rectTransform.GetWorldCorners(contentCorners);
                for (int i = 0; i < 4; i++)
                {
                    Vector2 p = space.InverseTransformPoint(contentCorners[i]);
                    if (!any) { xMin = xMax = p.x; yMin = yMax = p.y; any = true; }
                    else { xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x); yMin = Mathf.Min(yMin, p.y); yMax = Mathf.Max(yMax, p.y); }
                }
            }
        }
        if (any) rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        return any;
    }

    /// <summary>A phone held upright puts the touch Interact button where the row would be: the row moves up there. When the HUD
    /// drives the floor (SetFloor), the row sits exactly on top of the pieces under it instead.</summary>
    void UpdatePlacement()
    {
        var rt = (RectTransform)transform;
        if (parentRect == null || rt.anchorMin != Vector2.zero || rt.anchorMax != Vector2.zero) return;   // the narrow offset is for the bottom-left row
        if (!float.IsNaN(drivenFloor))
        {
            var driven = new Vector2(authoredPosition.x, drivenFloor);
            if ((rt.anchoredPosition - driven).sqrMagnitude > 0.01f) rt.anchoredPosition = driven;
            layoutNarrow = -1;   // the narrow position applies again if the HUD stops driving
            return;
        }
        int narrow = parentRect.rect.width < narrowWidth ? 1 : 0;
        if (narrow == layoutNarrow) return;
        layoutNarrow = narrow;
        rt.anchoredPosition = narrow == 1 ? narrowPosition : authoredPosition;
    }
}
