using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Combat feedback drawn on its own overlay canvas (authored Resources/UI/CombatFeedback prefab): the hit-direction arcs around the
/// crosshair (QA-35, fed by DamageDirectionIndicator.Report), the hit marker for the local player's own hits on any target (QA-37,
/// CombatFeedbackView.ReportHit) and the kill-money popup (QA-27, fed by RogueHudView.ShowBounty). Every mode can use it: nothing
/// here depends on the Roguelike run, and without the prefab every call is a safe no-op (one warning).
///
/// Why an overlay canvas: the gameplay HUD is a Screen Space - Camera canvas, and a sight's screen-space overlay (ScopeOverlay,
/// Screen Space - Overlay, sorting 900) always draws above every camera-space canvas, so anything on the HUD disappears while aiming
/// through such a sight. This canvas sorts above it (authored sorting order) and is shown only while a match is being played and the
/// HUD itself is visible, so it never draws over the pause menu, run screens or results. It has no raycaster: it never takes a click.
/// While aiming, the money popup moves from just under the crosshair to aimPosition, lower in the lens, off the aiming point.
/// The damage numbers (QA-48, DamageNumberView on the DamageNumbers child) live here for the same reason: they stay readable through
/// a sight and are kept inside its lens.
/// Every size, colour and time is a serialized design parameter on the prefab.
/// </summary>
public sealed class CombatFeedbackView : MonoBehaviour
{
    public const string ResourcePath = "UI/CombatFeedback";

    [Tooltip("This view's overlay canvas (sorted above sight overlays); enabled only while there is something to show during play.")]
    public Canvas canvas;

    [Header("Hit direction (QA-35)")]
    [Tooltip("Parent of the arcs, centred on the crosshair.")] public RectTransform arcRoot;
    [Tooltip("Authored arc (pointing up = straight ahead); instanced for each direction shown.")] public Image arcTemplate;
    [Tooltip("Distance from the crosshair to the middle of the arc band (canvas units).")] public float arcRadius = 112f;
    [Tooltip("Radius of the arc band's centre line as a fraction of the arc image's width (authored with the DamageArc sprite).")]
    public float spriteRadiusFraction = 0.42f;
    public Color arcColor = new Color(1f, 0.16f, 0.2f, 0.95f);
    [Tooltip("Damage of one hit that shows a fully opaque arc; smaller hits are fainter, repeated hits add up.")] public float fullDamage = 150f;
    [Tooltip("Opacity of the faintest arc (a graze), before fading.")] [Range(0f, 1f)] public float minAlpha = 0.45f;
    [Tooltip("Seconds an arc stays at full strength, then seconds it takes to fade out.")] public float holdSeconds = 0.3f, fadeSeconds = 1f;
    [Tooltip("Hits from directions closer than this (degrees) refresh the same arc instead of adding one.")] public float mergeDegrees = 30f;
    [Tooltip("Most arcs shown at once; a new direction replaces the faintest.")] [Min(1)] public int maxArcs = 6;
    [Tooltip("Arc size right after a hit, easing back to 1 over arcPunchSeconds (the only motion; no camera shake).")] public float arcPunchScale = 1.12f, arcPunchSeconds = 0.15f;

    [Header("Hit marker (QA-37)")]
    [Tooltip("Four ticks around the crosshair confirming that the local player's shot hit something (an enemy, a drone, a device).")]
    public CanvasGroup hitMarker;
    public Graphic[] hitMarkerTicks = new Graphic[0];
    public Color hitColor = new Color(1f, 1f, 1f, 0.95f), headshotHitColor = new Color(1f, 0.82f, 0.2f, 1f), killHitColor = new Color(1f, 0.25f, 0.3f, 1f);
    [Tooltip("Seconds a tick stays, and its size right after the hit easing back to 1.")] public float hitSeconds = 0.22f, markerPunchScale = 1.35f;
    [Tooltip("Hit sound for targets that have none of their own (drones, devices); played only when the caller asks for it.")]
    public AudioSource cueSource; public AudioClip hitCue; [Range(0f, 1f)] public float hitCueVolume = 0.55f;

    [Header("Kill money (QA-27)")]
    public CanvasGroup bountyGroup; public Text bountyText;
    [Tooltip("Popup position (canvas units from the centre) from the hip and while aiming; the aiming one stays clear of the reticle.")]
    public Vector2 hipPosition = new Vector2(0f, -34f), aimPosition = new Vector2(0f, -110f);
    [Tooltip("How far the popup rises, seconds it holds, seconds it fades.")] public float bountyRise = 18f, bountyHoldSeconds = 0.6f, bountyFadeSeconds = 0.4f;

    [Header("Damage numbers (QA-48)")]
    [Tooltip("Screen-space damage numbers drawn on this overlay (above sight overlays); null disables them with one warning.")]
    public DamageNumberView damageNumbers;

    sealed class Arc { public Image image; public Vector3 source; public float angle, strength, hitAt; }
    readonly List<Arc> arcs = new List<Arc>();
    readonly List<Arc> pool = new List<Arc>();
    static CombatFeedbackView instance;
    static bool missingWarned;
    float bountyShownAt = -10f;
    Canvas hudCanvas; float hudCheckAt = -10f;
    FPSController localFps; float localCheckAt = -10f;

    /// <summary>The scene's view, created from the authored prefab on first use; null (with one warning) when the prefab is missing.</summary>
    public static CombatFeedbackView Ensure()
    {
        if (instance != null) return instance;
        var prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab == null)
        {
            if (!missingWarned) { missingWarned = true; Debug.LogWarning("FLATS_COMBAT_FEEDBACK missing Resources/" + ResourcePath); }
            return null;
        }
        var go = Instantiate(prefab);
        go.name = "CombatFeedback";
        var view = go.GetComponent<CombatFeedbackView>();
        if (view == null) { Destroy(go); return null; }
        instance = view;
        return view;
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (arcTemplate != null) arcTemplate.gameObject.SetActive(false);
        if (bountyGroup != null) bountyGroup.alpha = 0f;
        if (hitMarker != null) hitMarker.alpha = 0f;
        if (canvas != null) canvas.enabled = false;
    }

    void OnDestroy() { if (instance == this) instance = null; }

    // ---------------------------------------------------------------- hit direction
    public void ReportDamage(Vector3 source, float damage)
    {
        if (arcTemplate == null || arcRoot == null) return;
        var cam = Camera.main;
        float angle = cam != null ? AngleTo(cam.transform, source) : 0f;
        if (float.IsNaN(angle)) angle = 0f;   // straight above or below: shown ahead rather than not at all
        float strength = fullDamage > 0f ? Mathf.Clamp01(damage / fullDamage) : 1f;
        float now = Time.unscaledTime;
        Arc arc = null;
        foreach (var a in arcs) if (Mathf.Abs(Mathf.DeltaAngle(a.angle, angle)) <= mergeDegrees) { arc = a; break; }
        if (arc == null)
        {
            if (arcs.Count >= Mathf.Max(1, maxArcs))
            {
                // the faintest arc (after its fade) makes room for the new direction
                Arc faintest = arcs[0];
                foreach (var a in arcs) if (Visible(a, now) < Visible(faintest, now)) faintest = a;
                arc = faintest;
                arc.strength = 0f;
            }
            else
            {
                arc = pool.Count > 0 ? pool[pool.Count - 1] : null;
                if (arc != null) pool.RemoveAt(pool.Count - 1);
                else
                {
                    var image = Instantiate(arcTemplate, arcRoot, false);
                    image.raycastTarget = false;
                    arc = new Arc { image = image };
                }
                arc.strength = 0f;
                arc.image.gameObject.SetActive(true);
                arcs.Add(arc);
            }
        }
        // a new hit from the same side adds to what is still showing, never more than full
        arc.strength = Mathf.Clamp01(Visible(arc, now) + strength);
        arc.source = source; arc.angle = angle; arc.hitAt = now;
        float size = arcRadius / Mathf.Max(0.05f, spriteRadiusFraction);
        arc.image.rectTransform.sizeDelta = new Vector2(size, size);
    }

    /// <summary>Degrees from the camera's heading to the source around the vertical axis (positive: to the right); NaN when the
    /// source is straight above or below the camera.</summary>
    static float AngleTo(Transform cam, Vector3 source)
    {
        Vector3 forward = cam.forward; forward.y = 0f;
        if (forward.sqrMagnitude < 1e-6f) { forward = cam.up; forward.y = 0f; }   // looking straight down or up: the top of the screen
        Vector3 to = source - cam.position; to.y = 0f;
        if (to.sqrMagnitude < 1e-4f || forward.sqrMagnitude < 1e-6f) return float.NaN;
        return Vector3.SignedAngle(forward, to, Vector3.up);
    }

    float Fade(Arc a, float now)
    {
        float age = now - a.hitAt;
        if (age <= holdSeconds) return 1f;
        return fadeSeconds > 0f ? Mathf.Clamp01(1f - (age - holdSeconds) / fadeSeconds) : 0f;
    }

    float Visible(Arc a, float now) { return a.strength * Fade(a, now); }

    void TickArcs(float now)
    {
        var cam = Camera.main;
        for (int i = arcs.Count - 1; i >= 0; i--)
        {
            var a = arcs[i];
            float fade = Fade(a, now);
            if (fade <= 0f || a.image == null)
            {
                if (a.image != null) a.image.gameObject.SetActive(false);
                arcs.RemoveAt(i);
                if (a.image != null) pool.Add(a);
                continue;
            }
            // the arc keeps pointing at where the hit came from while the player turns
            if (cam != null) { float angle = AngleTo(cam.transform, a.source); if (!float.IsNaN(angle)) a.angle = angle; }
            var rect = a.image.rectTransform;
            rect.localRotation = Quaternion.Euler(0f, 0f, -a.angle);
            float age = now - a.hitAt;
            float punch = arcPunchSeconds > 0f && age < arcPunchSeconds ? Mathf.Lerp(arcPunchScale, 1f, age / arcPunchSeconds) : 1f;
            rect.localScale = new Vector3(punch, punch, 1f);
            var c = arcColor;
            c.a = arcColor.a * Mathf.Lerp(minAlpha, 1f, a.strength) * fade;
            a.image.color = c;
        }
    }

    // ---------------------------------------------------------------- hit marker
    float hitAt = -10f, lastCueAt = -10f; bool hitStrong;

    /// <summary>
    /// The local player's shot hit something (QA-37): a short tick around the crosshair. Works for any target, enemy or not.
    /// kill: the hit destroyed or killed it (red, larger); headshot: gold. playCue: also play the hit sound here, for targets that have
    /// no hit sound of their own (a drone, a device); enemies already play theirs, so pass false for them.
    /// </summary>
    public static void ReportHit(bool kill, bool headshot, bool playCue)
    {
        var view = Ensure();
        if (view != null) view.ShowHit(kill, headshot, playCue);
    }

    public void ShowHit(bool kill, bool headshot, bool playCue)
    {
        if (hitMarker == null) return;
        float now = Time.unscaledTime;
        hitAt = now; hitStrong = kill;
        var color = kill ? killHitColor : headshot ? headshotHitColor : hitColor;
        foreach (var tick in hitMarkerTicks) if (tick != null) tick.color = color;
        if (playCue && hitCue != null && cueSource != null && now - lastCueAt > 0.05f) { cueSource.PlayOneShot(hitCue, hitCueVolume); lastCueAt = now; }
    }

    bool TickHit(float now)
    {
        if (hitMarker == null) return false;
        float t = now - hitAt;
        if (t > hitSeconds) { if (hitMarker.alpha != 0f) hitMarker.alpha = 0f; return false; }
        float k = Mathf.Clamp01(t / Mathf.Max(0.01f, hitSeconds));
        hitMarker.alpha = 1f - k * k;
        float scale = Mathf.Lerp(hitStrong ? markerPunchScale * 1.2f : markerPunchScale, 1f, Mathf.Clamp01(t / 0.08f));
        hitMarker.transform.localScale = new Vector3(scale, scale, 1f);
        return true;
    }

    // ---------------------------------------------------------------- kill money
    /// <summary>Shows the money text; false when the prefab has no popup (the caller then uses its own).</summary>
    public bool ShowBounty(string text)
    {
        if (bountyGroup == null || bountyText == null) return false;
        bountyText.text = text ?? "";
        bountyShownAt = Time.unscaledTime;
        bountyGroup.alpha = 1f;
        return true;
    }

    bool TickBounty(float now)
    {
        if (bountyGroup == null) return false;
        float t = now - bountyShownAt;
        if (t > bountyHoldSeconds + bountyFadeSeconds) { if (bountyGroup.alpha != 0f) bountyGroup.alpha = 0f; return false; }
        bountyGroup.alpha = t < bountyHoldSeconds ? 1f : 1f - (t - bountyHoldSeconds) / Mathf.Max(0.01f, bountyFadeSeconds);
        var rect = (RectTransform)bountyGroup.transform;
        rect.anchoredPosition = (LocalAiming(now) ? aimPosition : hipPosition) + new Vector2(0f, bountyRise * Mathf.Min(1f, t / Mathf.Max(0.01f, bountyHoldSeconds)));
        return true;
    }

    bool LocalAiming(float now)
    {
        if (ScopeViewPresenter.Current != null) return true;
        if (now >= localCheckAt)
        {
            localCheckAt = now + 0.5f;
            var go = RoguelikeController.FindLocalPlayer();
            localFps = go != null ? go.GetComponent<FPSController>() : null;
        }
        return localFps != null && localFps.isZoom;
    }

    // ---------------------------------------------------------------- visibility
    /// <summary>Only while a match is being played and the gameplay HUD is showing (not over the pause menu, a run screen or results).</summary>
    bool PlayVisible(float now)
    {
        if (Menu.current != "Playing") return false;
        if (now >= hudCheckAt)
        {
            hudCheckAt = now + 1f;
            var hud = GameObject.Find("UI");
            hudCanvas = hud != null ? hud.GetComponent<Canvas>() : null;
        }
        return hudCanvas == null || hudCanvas.enabled;
    }

    void LateUpdate()
    {
        float now = Time.unscaledTime;
        TickArcs(now);
        bool money = TickBounty(now);
        bool hit = TickHit(now);
        bool numbers = damageNumbers != null && damageNumbers.Tick(now);
        bool show = (arcs.Count > 0 || money || hit || numbers) && PlayVisible(now);
        if (canvas != null && canvas.enabled != show) canvas.enabled = show;
    }
}
