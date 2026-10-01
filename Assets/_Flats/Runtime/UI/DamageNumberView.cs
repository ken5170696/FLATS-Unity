using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space damage numbers (QA-48) on the CombatFeedback overlay canvas, which sorts above a raised sight's screen-space lens
/// (ScopeOverlay, sorting 900), so numbers stay readable scoped and from the hip. Authored as CombatFeedback/DamageNumbers with one
/// inactive DamageNumberItem template; a fixed pool is instanced from it once, so hits never allocate UI objects.
///
/// Modes (FlatsControls.DamageNumberStyle): Stacked (default, Apex-style) adds rapid hits on one target into one number beside it that
/// pops on each add, holds after the last hit and fades; a headshot flashes the stack in the headshot colour and size with the
/// crosshair icon; a kill turns it into the kill colour with the skull icon and holds longer. Floating shows one rising number per hit.
/// Teammates' hits (co-op) are smaller, grey and have no icons. Words (Immune) always float.
///
/// Placement: every number sits beside its target's icon row (enemies) or anchor (objects), projected each frame. While a magnifying
/// sight is raised it is projected through the sight camera into the lens (the overlay lens of ScopeViewPresenter, or the world lens
/// on the weapon) and kept inside the lens circle; on the open screen it is kept on screen. In both cases it stays outside a clear
/// radius around the crosshair. Every size is a share of the screen's shorter side, so 16:9, 21:9, 4:3 and portrait read alike.
/// Every size, colour and time below is a serialized design parameter on the prefab; the rules are Flats.Core.Roguelike.DamageNumberRules.
/// </summary>
public sealed class DamageNumberView : MonoBehaviour
{
    [Tooltip("Authored number (kept inactive); instanced into a fixed pool when the view starts.")]
    public DamageNumberItem template;
    [Tooltip("Most numbers on screen at once. A new number beyond this reuses the one whose last hit is oldest.")]
    [Min(4)] public int maxNumbers = 24;

    [Header("Size")]
    [Tooltip("Number height as a fraction of the screen's shorter side (0.034 = 37 px at 1080p).")]
    public float numberHeight = 0.034f;
    [Tooltip("Never smaller than this many screen pixels.")] public float minPixels = 18f;
    [Tooltip("Size of the headshot look, the kill confirmation, derived hits (chain, explosion, ricochet, homing), teammates' hits and words.")]
    public float headshotScale = 1.2f, killScale = 1.3f, derivedScale = 0.85f, teammateScale = 0.7f, wordScale = 0.8f;
    [Tooltip("Icon height and the gap to the number, as fractions of the number's font size.")]
    public float iconHeight = 0.8f, iconGap = 0.15f;
    [Tooltip("Shown left of a number while it has the headshot look / the kill look.")]
    public Sprite headshotIcon, killIcon;

    [Header("Colours (opaque; fading uses the item's CanvasGroup)")]
    public Color normalColor = new Color(1f, 1f, 1f, 1f);
    public Color headshotColor = new Color(1f, 0.784f, 0.239f, 1f), killColor = new Color(1f, 0.302f, 0.369f, 1f);
    public Color chainColor = new Color(0.45f, 0.82f, 1f, 1f), explosionColor = new Color(1f, 0.6f, 0.2f, 1f),
        ricochetColor = new Color(0.78f, 0.95f, 0.45f, 1f), homingColor = new Color(0.8f, 0.6f, 1f, 1f);
    public Color teammateColor = new Color(0.66f, 0.67f, 0.72f, 1f);
    [Range(0f, 1f)] public float teammateAlpha = 0.8f;

    [Header("Stacked (default mode)")]
    [Tooltip("A hit this soon after the previous one on the same target adds to its number.")] public float stackWindow = 0.8f;
    [Tooltip("Seconds at full opacity after the last hit, and after a killing hit.")] public float stackHold = 0.8f, killHold = 1.0f;
    [Tooltip("Seconds to fade out after the hold.")] public float fadeSeconds = 0.35f;
    [Tooltip("Scale right after each add / after the killing hit, easing back to 1 over punchSeconds.")]
    public float punchScale = 1.3f, killPunchScale = 1.45f, punchSeconds = 0.12f;
    [Tooltip("Seconds the headshot colour, size and icon last after a headshot adds to the stack.")] public float headshotFlashSeconds = 0.35f;

    [Header("Floating")]
    [Tooltip("Seconds at full opacity (a kill holds floatKillHold), then seconds to fade.")] public float floatHold = 0.5f, floatKillHold = 0.7f, floatFade = 0.3f;
    [Tooltip("Rise over the lifetime, and random horizontal/vertical spread, as fractions of the screen's shorter side.")]
    public float floatRise = 0.06f, floatSpread = 0.03f;
    [Tooltip("Floating numbers on one target share lanes so consecutive hits never print over each other: they alternate right and " +
        "left of the target, and every second pair starts one line higher (QA-36 round 2).")] [Min(1)] public int floatLanes = 4;
    [Tooltip("Height of one lane, in number heights.")] public float floatLaneStep = 1.15f;

    [Header("Placement (fractions of the screen's shorter side)")]
    [Tooltip("Gap between the target's icon row (or an object's anchor) and the number.")] public float sideGap = 0.012f;
    [Tooltip("No number covers this radius around the crosshair (the lens reticle while scoped).")] public float crosshairClear = 0.06f;
    [Tooltip("Distance kept from the screen edges.")] public float edgeMargin = 0.02f;
    [Tooltip("Distance kept from the lens edge while scoped.")] public float lensMargin = 0.012f;
    [Tooltip("World metres from an object's anchor (a drone's top) to its side, where its number starts.")] public float objectHalfWidth = 0.3f;

    sealed class Entry
    {
        public DamageNumberItem item;
        public DamageNumberState state;
        public GameObject target;
        public Vector3 point, offset;
        public bool enemy, teammate, stacked, word, cjk, visible;
        public DamageKind kind;
        public Color baseColor;
        public float side = 1f, spreadX, spreadY;
        public int laneRow;
        public int shown = -1, look = -1;
        public string text = "";
        public float labelWidth, groupWidth;
    }

    readonly List<Entry> entries = new List<Entry>();
    readonly List<DamageNumberState> states = new List<DamageNumberState>();
    Canvas rootCanvas;
    Font numberFont;
    FontStyle numberStyle = FontStyle.Bold;
    int activeCount, fontSize = -1, screenW = -1, screenH = -1;
    float canvasScale = 1f, refSide = 1080f, maxFactor = 1f;
    static readonly string[] numberText = new string[1000];
    static readonly Vector3[] corners = new Vector3[4];

    // lens of the raised sight, found once per frame while numbers show
    bool lensOn;
    Vector2 lensCentre;
    float lensRadius, lensImageRadius, lensTanV, lensAspect;
    Vector3 lensPos, lensRight, lensUp, lensForward;
    FPSController localFps; float localCheckAt = -10f;
    Transform sightRoot; FlatsSightTarget sightTarget; RawImage sightImage; Mask sightMask; Camera sightCamera;

    DamageNumberTiming StackTiming
    {
        get
        {
            return new DamageNumberTiming { Window = stackWindow, Hold = stackHold, KillHold = killHold, Fade = fadeSeconds, PunchSeconds = punchSeconds,
                PunchScale = punchScale, KillPunchScale = killPunchScale, HeadshotFlash = headshotFlashSeconds };
        }
    }

    DamageNumberTiming FloatTiming
    {
        get
        {
            // a floating headshot keeps its look for its whole life
            return new DamageNumberTiming { Window = 0, Hold = floatHold, KillHold = floatKillHold, Fade = floatFade, PunchSeconds = punchSeconds,
                PunchScale = punchScale, KillPunchScale = killPunchScale, HeadshotFlash = floatKillHold + floatHold + floatFade + 1f };
        }
    }

    void Awake()
    {
        if (template == null) { Debug.LogWarning("FLATS_DAMAGE_NUMBERS no template on " + name); return; }
        template.gameObject.SetActive(false);
        var canvas = GetComponentInParent<Canvas>();
        rootCanvas = canvas != null ? canvas.rootCanvas : null;
        if (template.label != null) { numberFont = template.label.font; numberStyle = template.label.fontStyle; }
        maxFactor = Mathf.Max(1f, headshotScale, killScale);
        int count = Mathf.Max(4, maxNumbers);
        for (int i = 0; i < count; i++)
        {
            var item = Instantiate(template, template.transform.parent, false);
            item.name = "Number";
            if (item.rect == null) item.rect = (RectTransform)item.transform;
            if (item.label != null) item.label.raycastTarget = false;
            if (item.icon != null) { item.icon.raycastTarget = false; item.icon.enabled = false; }
            var e = new Entry { item = item, state = new DamageNumberState() };
            entries.Add(e); states.Add(e.state);
        }
    }

    void OnEnable() { FlatsControls.Changed += OnSettingChanged; }
    void OnDisable() { FlatsControls.Changed -= OnSettingChanged; }

    void OnSettingChanged() { if (FlatsControls.DamageNumberStyle == DamageNumberMode.Off) Clear(); }

    /// <summary>Hides every number (the setting turned them off).</summary>
    public void Clear() { foreach (var e in entries) if (e.state.Active) Release(e); }

    // ---------------------------------------------------------------- reports

    /// <summary>A settled hit on an enemy (the caller checked the setting and passes its mode). kill: this hit left it at no health.</summary>
    public void ReportEnemy(GameObject target, float damage, bool headshot, bool kill, DamageKind kind, bool teammate, DamageNumberMode mode)
    {
        if (target == null || mode == DamageNumberMode.Off || entries.Count == 0) return;
        Report(target, RogueRoleMarker.RowCenter(target), true, damage, headshot, kill, kind, teammate, mode, KindColor(kind, teammate));
    }

    /// <summary>A hit on a world object (drone, carrier, device): stacked per object like an enemy, anchored at worldPoint.</summary>
    public void ReportObject(GameObject target, Vector3 worldPoint, float damage, bool kill, DamageNumberMode mode)
    {
        if (mode == DamageNumberMode.Off || entries.Count == 0) return;
        Report(target, worldPoint, false, damage, false, kill, DamageKind.Direct, false, target != null ? mode : DamageNumberMode.Floating, normalColor);
    }

    /// <summary>A short word over a point (Immune): always floating, in the given colour.</summary>
    public void ReportWord(Vector3 worldPoint, string text, Color color)
    {
        if (string.IsNullOrEmpty(text) || entries.Count == 0) return;
        double now = Time.unscaledTime;
        var e = Acquire(now);
        Setup(e, null, worldPoint, false, false, false, DamageKind.Direct, color);
        e.word = true;
        e.state.Start(now, 0, false, false);
        SetText(e, text);
    }

    void Report(GameObject target, Vector3 anchor, bool enemy, float damage, bool headshot, bool kill, DamageKind kind, bool teammate, DamageNumberMode mode, Color color)
    {
        double now = Time.unscaledTime;
        bool stacked = mode == DamageNumberMode.Stacked && target != null;
        Entry e = stacked ? Find(target, teammate) : null;
        if (e != null && DamageNumberRules.Merges(e.state, now, StackTiming))
        {
            e.state.Add(now, damage, headshot, kill);   // the stack keeps the colour and size of the hit that opened it
            SetNumber(e);
            return;
        }
        if (e == null) e = Acquire(now);   // an expired stack of the same target restarts in place
        Setup(e, target, anchor, enemy, teammate, stacked, kind, color);
        e.state.Start(now, damage, headshot, kill);
        SetNumber(e);
    }

    void Setup(Entry e, GameObject target, Vector3 anchor, bool enemy, bool teammate, bool stacked, DamageKind kind, Color color)
    {
        e.target = target; e.enemy = enemy; e.teammate = teammate; e.stacked = stacked; e.word = false; e.kind = kind; e.baseColor = color;
        e.point = anchor; e.offset = target != null && !enemy ? anchor - target.transform.position : Vector3.zero;
        e.side = 1f; e.spreadX = 0f; e.spreadY = 0f; e.laneRow = 0;
        if (!stacked)
        {
            // the lane follows how many floating numbers of the same target (or the same spot, for words) are still showing
            int busy = 0;
            foreach (var o in entries)
                if (o != e && o.state.Active && !o.stacked && (target != null ? o.target == target : o.target == null && (o.point - anchor).sqrMagnitude < 0.25f)) busy++;
            int lane = busy % Mathf.Max(1, floatLanes);
            e.side = lane % 2 == 0 ? 1f : -1f;
            e.laneRow = lane / 2;
            e.spreadX = Random.value * 0.3f;   // a little life, far below one number's width
        }
        e.look = -1; e.shown = -1; e.visible = false;
        if (!e.item.gameObject.activeSelf) { e.item.gameObject.SetActive(true); activeCount++; }
        if (e.item.group != null) e.item.group.alpha = 0f;   // placed on the next LateUpdate
    }

    Entry Find(GameObject target, bool teammate)
    {
        foreach (var e in entries) if (e.state.Active && e.stacked && !e.word && e.target == target && e.teammate == teammate) return e;
        return null;
    }

    Entry Acquire(double now)
    {
        int i = DamageNumberRules.ReuseIndex(states);
        var e = entries[i < 0 ? 0 : i];
        if (e.state.Active) Release(e);
        return e;
    }

    void Release(Entry e)
    {
        e.state.Clear();
        e.target = null;
        if (e.item.gameObject.activeSelf) { e.item.gameObject.SetActive(false); activeCount--; }
    }

    Color KindColor(DamageKind kind, bool teammate)
    {
        if (teammate) return teammateColor;
        switch (kind)
        {
            case DamageKind.Chain: return chainColor;
            case DamageKind.Explosion: return explosionColor;
            case DamageKind.Ricochet: return ricochetColor;
            case DamageKind.Homing: return homingColor;
            default: return normalColor;
        }
    }

    // ---------------------------------------------------------------- text

    static string NumberText(int value)
    {
        if (value < 0) value = 0;
        if (value >= numberText.Length) return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var s = numberText[value];
        if (s == null) numberText[value] = s = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return s;
    }

    void SetNumber(Entry e)
    {
        int shown = e.state.Shown;
        if (shown == e.shown) return;
        e.shown = shown;
        SetText(e, NumberText(shown));
    }

    void SetText(Entry e, string text)
    {
        var label = e.item.label;
        if (label == null) return;
        bool cjk = false;
        foreach (char c in text) if (c >= '⺀') { cjk = true; break; }
        if (cjk != e.cjk || label.font == null)
        {
            e.cjk = cjk;
            // numerals and Latin words use the authored bold font; Chinese uses the CJK font without synthetic bold (UI spec 3.2)
            label.font = cjk && FlatsLocalization.ChineseFont != null ? FlatsLocalization.ChineseFont : numberFont;
            label.fontStyle = cjk ? FontStyle.Normal : numberStyle;
        }
        e.text = text;
        label.text = text;
        if (fontSize > 0) label.fontSize = fontSize;
        e.labelWidth = label.preferredWidth;
        e.look = -1;   // re-lays the icon next to the new width
    }

    // ---------------------------------------------------------------- frame

    /// <summary>Places, colours and fades every number; true while any is showing. Called by CombatFeedbackView.LateUpdate.</summary>
    public bool Tick(float nowSeconds)
    {
        if (activeCount <= 0) return false;
        double now = nowSeconds;
        UpdateScreen();
        FindLens(nowSeconds);
        Camera main = lensOn ? null : Camera.main;
        var stack = StackTiming; var floating = FloatTiming;
        float clearPx = crosshairClear * refSide, gapPx = sideGap * refSide, edgePx = edgeMargin * refSide, lensPx = lensMargin * refSide;
        float enemyHalf = RogueRoleMarker.BorderSize * 0.5f + RogueRoleMarker.StatusGap + RogueRoleMarker.StatusSize;
        placedBoxes.Clear();
        foreach (var e in entries)
        {
            if (!e.state.Active) continue;
            var timing = e.stacked ? stack : floating;
            if (DamageNumberRules.Expired(e.state, now, timing)) { Release(e); continue; }
            if (e.target != null) e.point = e.enemy ? RogueRoleMarker.RowCenter(e.target) : e.target.transform.position + e.offset;

            // look: colour, icon and size
            bool kill = e.state.Killed && !e.teammate && !e.word;
            bool head = !kill && !e.teammate && !e.word && DamageNumberRules.HeadshotFlashing(e.state, now, timing);
            int look = e.word ? 4 : e.teammate ? 3 : kill ? 2 : head ? 1 : 0;
            if (look != e.look) ApplyLook(e, look);
            float factor = (e.word ? wordScale : e.teammate ? teammateScale : e.kind != DamageKind.Direct ? derivedScale : 1f)
                * (kill ? killScale : head ? headshotScale : 1f);
            float scale = factor * (float)DamageNumberRules.Punch(e.state, now, timing) / maxFactor;
            float unitsToPx = scale * canvasScale;
            float halfW = e.groupWidth * 0.5f * unitsToPx, halfH = fontSize * 0.6f * unitsToPx;

            // anchor on screen, and how far the icon row (or object) reaches to the side
            Vector2 c, edge;
            Vector3 right = lensOn ? lensRight : (main != null ? main.transform.right : Vector3.right);
            bool placed = lensOn ? ProjectLens(e.point, out c) : ProjectScreen(main, e.point, out c);
            if (placed) placed = lensOn ? (!e.teammate || (c - lensCentre).magnitude <= lensRadius)
                : DamageNumberRules.NearScreen(c.x, c.y, screenW, screenH, 0.1);
            if (!placed) { Hide(e); continue; }
            float reach = 0f;
            if (lensOn ? ProjectLens(e.point + right * (e.enemy ? enemyHalf : objectHalfWidth), out edge) : ProjectScreen(main, e.point + right * (e.enemy ? enemyHalf : objectHalfWidth), out edge))
                reach = (edge - c).magnitude;

            double x = c.x + e.side * (reach + gapPx + halfW + (e.stacked ? 0f : e.spreadX * floatSpread * refSide));
            double y = c.y;
            if (!e.stacked)
                y += DamageNumberRules.Rise(now - e.state.StartedAt, DamageNumberRules.HoldOf(e.state, timing) + timing.Fade) * floatRise * refSide + e.spreadY * floatSpread * refSide
                     + e.laneRow * floatLaneStep * 2f * halfH;
            if (lensOn) DamageNumberRules.PlaceInLens(ref x, ref y, lensCentre.x, lensCentre.y, lensRadius, clearPx, Mathf.Sqrt(halfW * halfW + halfH * halfH), lensPx);
            else
            {
                DamageNumberRules.PlaceOnScreen(ref x, ref y, screenW, screenH, screenW * 0.5, screenH * 0.5, clearPx, halfW, halfH, edgePx);
                // no free spot near its column (a burst of a dozen floats): the number skips this frame rather than print over a panel
                if (!Separate(ref x, ref y, halfW, halfH)) { Hide(e); continue; }
            }

            var rect = e.item.rect;
            rect.position = new Vector3((float)x, (float)y, 0f);   // a Screen Space - Overlay canvas: world units are screen pixels
            rect.localScale = new Vector3(scale, scale, 1f);
            if (e.item.group != null) e.item.group.alpha = (float)DamageNumberRules.Alpha(e.state, now, timing) * (e.teammate ? teammateAlpha : 1f);
            e.visible = true;
        }
        return activeCount > 0;
    }

    // ---------------------------------------------------------------- keeping numbers apart (QA-36 round 3)
    [Header("Separation (screen pixels)")]
    [Tooltip("Space kept between two numbers, and between a number and a HUD panel (objective tracker, health, weapon...).")] public float separationGap = 4f;
    readonly List<Rect> placedBoxes = new List<Rect>();

    /// <summary>
    /// A number never prints over another number shown this frame or over a Roguelike HUD panel (RogueHudView.ScreenPanels, which
    /// already carry a gap). It takes the nearest free spot on its own column: its place, then one step up, one step down, two up, two
    /// down... (QA-36 round 5: stepping up past a number and then down away from a panel made the two rules undo each other, so the top
    /// number stayed on the objective panel). Without a free spot it keeps its place. Hip fire only: a scoped number stays in its lens.
    /// </summary>
    bool Separate(ref double x, ref double y, float halfW, float halfH)
    {
        float step = 2f * halfH + separationGap;
        double start = y;
        // QA-36 round 6: a burst could use every candidate and the number fell back to its first spot, inside a card; now the search
        // covers the column up to the screen edges, and a number with no free spot is hidden for the frame
        int reach = Mathf.Max(12, Mathf.CeilToInt(screenH / Mathf.Max(1f, step)) * 2);
        for (int k = 0; k <= reach; k++)
        {
            double candidate = start + (k == 0 ? 0 : ((k + 1) / 2) * step * (k % 2 == 1 ? 1 : -1));
            if (candidate < halfH || candidate > screenH - halfH) continue;
            if (!Free((float)x, (float)candidate, halfW, halfH)) continue;
            y = candidate;
            placedBoxes.Add(new Rect((float)x - halfW, (float)y - halfH, 2f * halfW, 2f * halfH));
            return true;
        }
        return false;
    }

    bool Free(float x, float y, float halfW, float halfH)
    {
        var box = new Rect(x - halfW, y - halfH, 2f * halfW, 2f * halfH);
        var padded = new Rect(box.x - separationGap, box.y - separationGap, box.width + 2f * separationGap, box.height + 2f * separationGap);
        foreach (var other in placedBoxes) if (other.Overlaps(padded)) return false;
        foreach (var panel in RogueHudView.ScreenPanels) if (panel.Overlaps(box)) return false;
        foreach (var marker in RogueHudView.ScreenMarkers) if (marker.Overlaps(box)) return false;   // waypoint icons and labels
        return true;
    }

    void Hide(Entry e)
    {
        if (!e.visible) return;
        e.visible = false;
        if (e.item.group != null) e.item.group.alpha = 0f;
    }

    void ApplyLook(Entry e, int look)
    {
        e.look = look;
        Color color = look == 4 ? e.baseColor : look == 3 ? teammateColor : look == 2 ? killColor : look == 1 ? headshotColor : e.baseColor;
        color.a = 1f;
        if (e.item.label != null) e.item.label.color = color;
        Sprite sprite = look == 2 ? killIcon : look == 1 ? headshotIcon : null;
        var icon = e.item.icon;
        bool showIcon = icon != null && sprite != null;
        float iconSize = showIcon ? fontSize * iconHeight : 0f, gap = showIcon ? fontSize * iconGap : 0f;
        e.groupWidth = e.labelWidth + iconSize + gap;
        if (icon != null)
        {
            icon.enabled = showIcon;
            if (showIcon)
            {
                icon.sprite = sprite; icon.color = color; icon.preserveAspect = true;
                icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
                icon.rectTransform.anchoredPosition = new Vector2(-e.groupWidth * 0.5f + iconSize * 0.5f, 0f);
            }
        }
        if (e.item.label != null) e.item.label.rectTransform.anchoredPosition = new Vector2((iconSize + gap) * 0.5f, 0f);
    }

    /// <summary>Font size and canvas scale follow the screen: numbers are a share of its shorter side at every aspect.</summary>
    void UpdateScreen()
    {
        float scaleNow = rootCanvas != null && rootCanvas.scaleFactor > 0f ? rootCanvas.scaleFactor : 1f;
        if (Screen.width == screenW && Screen.height == screenH && Mathf.Approximately(scaleNow, canvasScale) && fontSize > 0) return;
        screenW = Screen.width; screenH = Screen.height; canvasScale = scaleNow;
        refSide = Mathf.Max(1f, Mathf.Min(screenW, screenH));
        maxFactor = Mathf.Max(1f, headshotScale, killScale);
        float px = Mathf.Max(minPixels, numberHeight * refSide) * maxFactor;
        int size = Mathf.Clamp(Mathf.RoundToInt(px / canvasScale), 1, 300);
        if (size == fontSize) return;
        fontSize = size;
        foreach (var e in entries)
        {
            if (e.item.label == null) continue;
            e.item.label.fontSize = fontSize;
            if (e.state.Active) { e.labelWidth = e.item.label.preferredWidth; e.look = -1; }
        }
    }

    // ---------------------------------------------------------------- projection

    bool ProjectScreen(Camera cam, Vector3 world, out Vector2 screen)
    {
        screen = Vector2.zero;
        if (cam == null) return false;
        var p = cam.WorldToScreenPoint(world);
        if (p.z <= 0.05f) return false;
        screen = new Vector2(p.x, p.y);
        return true;
    }

    bool ProjectLens(Vector3 world, out Vector2 screen)
    {
        screen = lensCentre;
        Vector3 d = world - lensPos;
        double px, py;
        if (!DamageNumberRules.LensOffset(Vector3.Dot(d, lensRight), Vector3.Dot(d, lensUp), Vector3.Dot(d, lensForward), lensTanV, lensAspect, lensImageRadius, out px, out py)) return false;
        screen = new Vector2(lensCentre.x + (float)px, lensCentre.y + (float)py);
        return true;
    }

    /// <summary>
    /// The local player's raised magnifying sight, if any: the screen-space lens of ScopeViewPresenter (scope.view overlay; centred,
    /// OverlayDiameter across) or the sight's world-space lens on the weapon, measured through the weapon camera. The sight camera
    /// sits at the eye; its image may be rolled half a turn for a lens canvas mounted backwards, so the unrolled basis is used.
    /// </summary>
    void FindLens(float now)
    {
        lensOn = false;
        Camera sight = null;
        var presenter = ScopeViewPresenter.Current;
        if (presenter != null && presenter.OverlayActive && presenter.OverlayDiameter > 1f)
        {
            CacheSight(presenter.transform);
            sight = sightCamera;
            lensCentre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            lensRadius = lensImageRadius = presenter.OverlayDiameter * 0.5f;
        }
        else
        {
            if (now >= localCheckAt)
            {
                localCheckAt = now + 0.5f;
                var go = RoguelikeController.FindLocalPlayer();
                localFps = go != null ? go.GetComponent<FPSController>() : null;
            }
            var fps = localFps;
            if (fps == null || !fps.isZoom || fps.primarySightIndex == 0 || fps.primaryWeapon == null || fps.primaryWeapon.childCount <= 2) return;
            var anchor = fps.primaryWeapon.GetChild(2);
            var target = anchor.GetComponentInChildren<FlatsSightTarget>(true);
            if (target == null || target.Magnification <= 1.01f) return;   // a reflex sight shows the world at 1x: the open-screen rules apply
            CacheSight(target.transform);
            var eye = fps.MeleeGunCamera;
            if (sightImage == null || !sightImage.isActiveAndEnabled || eye == null || !eye.isActiveAndEnabled) return;
            if (!ScreenCircle(sightImage.rectTransform, eye, out lensCentre, out lensImageRadius)) return;
            lensRadius = lensImageRadius;
            Vector2 maskCentre; float maskRadius;
            if (sightMask != null && sightMask.isActiveAndEnabled && ScreenCircle(sightMask.rectTransform, eye, out maskCentre, out maskRadius) && maskRadius > 1f)
                lensRadius = Mathf.Min(lensRadius, maskRadius);
            sight = sightCamera;
        }
        if (sight == null || lensRadius <= 1f) return;
        var st = sight.transform;
        lensPos = st.position; lensForward = st.forward; lensUp = st.up; lensRight = st.right;
        var main = Camera.main;
        if (main != null && main != sight && Vector3.Dot(lensUp, main.transform.up) < 0f) { lensUp = -lensUp; lensRight = -lensRight; }
        lensTanV = Mathf.Tan(sight.fieldOfView * 0.5f * Mathf.Deg2Rad);
        lensAspect = sight.aspect > 0f ? sight.aspect : 1f;
        lensOn = lensTanV > 0f;
    }

    void CacheSight(Transform root)
    {
        if (root == sightRoot && sightCamera != null) return;
        sightRoot = root;
        sightTarget = root != null ? root.GetComponent<FlatsSightTarget>() : null;
        sightCamera = root != null ? root.GetComponentInChildren<Camera>(true) : null;
        sightImage = root != null ? root.GetComponentInChildren<RawImage>(true) : null;
        sightMask = root != null ? root.GetComponentInChildren<Mask>(true) : null;
    }

    /// <summary>Screen centre and half height (pixels) of a world-space rect seen through cam; false when any corner is behind it.</summary>
    static bool ScreenCircle(RectTransform rect, Camera cam, out Vector2 centre, out float radius)
    {
        centre = Vector2.zero; radius = 0f;
        rect.GetWorldCorners(corners);
        var a = cam.WorldToScreenPoint(corners[0]);
        var b = cam.WorldToScreenPoint(corners[1]);
        var c = cam.WorldToScreenPoint(corners[2]);
        if (a.z <= 0f || b.z <= 0f || c.z <= 0f) return false;
        centre = new Vector2((a.x + c.x) * 0.5f, (a.y + c.y) * 0.5f);
        radius = new Vector2(b.x - a.x, b.y - a.y).magnitude * 0.5f;
        return radius > 0f;
    }
}
