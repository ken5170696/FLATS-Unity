using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD row of active meta effects for the local player: when a skill or weapon trait actually fires
/// (RogueMetaFeedback.Triggered) a chip with its icon pops in, a short cue plays, and timed effects
/// show a draining ring. Chips are instances of the authored template; layout, colours, sizes and the
/// cue live on the prefab (Resources/UI/Roguelike/Meta/RogueEffectRow).
/// </summary>
public class RogueEffectRowView : MonoBehaviour
{
    [SerializeField] RectTransform container;
    [SerializeField] RogueEffectChip chipTemplate;
    [SerializeField] AudioSource cueSource;
    [SerializeField] AudioClip cue;
    [SerializeField, Range(0f, 1f)] float cueVolume = 0.35f;
    [SerializeField, Tooltip("Seconds an instant effect (refund, mark, reload) stays visible")] float instantSeconds = 1.4f;
    [SerializeField, Tooltip("At most this many chips at once; the oldest makes room")] int maxChips = 5;

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

    readonly Dictionary<string, RogueEffectChip> live = new Dictionary<string, RogueEffectChip>();
    readonly Queue<RogueEffectChip> pool = new Queue<RogueEffectChip>();
    float lastCue = -10f;

    public static RogueEffectRowView Open(Transform hudParent)
    {
        var prefab = Resources.Load<GameObject>("UI/Roguelike/Meta/RogueEffectRow");
        if (prefab == null || hudParent == null) { Debug.LogWarning("FLATS_ROGUE_UI missing Resources/UI/Roguelike/Meta/RogueEffectRow"); return null; }
        var canvas = hudParent.GetComponentInParent<Canvas>();
        var go = Instantiate(prefab, canvas != null ? canvas.transform : hudParent, false);
        go.name = "RogueEffectRow";
        return go.GetComponent<RogueEffectRowView>();
    }

    void Awake() { if (chipTemplate != null) chipTemplate.gameObject.SetActive(false); }
    void OnEnable() { RogueMetaFeedback.Triggered += OnTriggered; SettingsChanged += ApplySettings; ApplySettings(); }
    void OnDisable() { RogueMetaFeedback.Triggered -= OnTriggered; SettingsChanged -= ApplySettings; }

    void ApplySettings()
    {
        if (group != null) group.alpha = CurrentOpacity;
        if (CurrentMode == Mode.Off) foreach (var k in new List<string>(live.Keys)) Recycle(k);
        Relabel();
    }

    /// <summary>Labels only while few effects are active and the player wants them; icons carry the row otherwise.</summary>
    void Relabel()
    {
        bool labels = CurrentMode == Mode.Full && live.Count <= LabelLimit;
        foreach (var c in live.Values) c.ShowLabel(labels);
    }

    void OnTriggered(string source, float seconds)
    {
        if (chipTemplate == null || container == null || CurrentMode == Mode.Off) return;
        RogueEffectChip chip;
        if (!live.TryGetValue(source, out chip))
        {
            if (live.Count >= maxChips) RemoveOldest();
            chip = pool.Count > 0 ? pool.Dequeue() : Instantiate(chipTemplate, container, false);
            string icon, name;
            RogueMetaFeedback.Describe(source, out icon, out name);
            chip.Bind(RogueMetaFeedback.IconSprite(icon), FlatsLocalization.Translate(name));
            chip.gameObject.SetActive(true);
            chip.transform.SetAsLastSibling();
            live[source] = chip;
        }
        chip.Restart(seconds > 0 ? seconds : instantSeconds, seconds > 0);
        Relabel();
        if (cue != null && cueSource != null && Time.unscaledTime - lastCue > 0.15f) { cueSource.PlayOneShot(cue, cueVolume); lastCue = Time.unscaledTime; }
    }

    void RemoveOldest()
    {
        string oldest = null; float least = float.MaxValue;
        foreach (var kv in live) if (kv.Value.Remaining < least) { least = kv.Value.Remaining; oldest = kv.Key; }
        if (oldest != null) Recycle(oldest);
    }

    void Recycle(string source)
    {
        var chip = live[source];
        live.Remove(source);
        chip.gameObject.SetActive(false);
        pool.Enqueue(chip);
        Relabel();
    }

    void Update()
    {
        List<string> done = null;
        foreach (var kv in live) if (kv.Value.Remaining <= 0) (done ?? (done = new List<string>())).Add(kv.Key);
        if (done != null) foreach (var k in done) Recycle(k);
    }
}
