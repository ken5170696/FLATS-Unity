using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One tile of the shooting-range menu and what its detail panel offers.</summary>
public sealed class RogueRangeEntry
{
    public string key = "", title = "", tag = "", subtitle = "", number = "", body = "";
    public Sprite icon;
    public FlatsUiTheme.Token token = FlatsUiTheme.Token.Ink;
    public bool selected;
    public readonly List<RogueRangeAction> actions = new List<RogueRangeAction>();

    public RogueRangeEntry Add(string label, string value, Action press, bool enabled = true, bool primary = false, bool closes = false)
    {
        actions.Add(new RogueRangeAction { label = label, value = value, press = press, enabled = enabled, primary = primary, closes = closes });
        return this;
    }
}

public sealed class RogueRangeAction
{
    public string label = "", value = "";
    public Action press;
    public bool enabled = true, primary, closes;
}

/// <summary>
/// The shooting-range menu: an authored screen (Resources/UI/Roguelike/Range/RogueRangeMenu) with tabs, a wall of tiles and a detail
/// panel whose rows act on the focused tile. The view only binds: tiles are instances of the authored tile template, the rows are
/// the authored option rows, and what they say and do comes from RogueRangePages. Opening it takes the pointer and blocks gameplay
/// input like every other run screen (FlatsCursor); closing puts the HUD, the cursor and the menu state back.
/// </summary>
public sealed class RogueRangeView : MonoBehaviour
{
    public const string ResourcePath = "UI/Roguelike/Range/RogueRangeMenu";

    [Serializable] public class Tab { public Button button; public Image back; public Text label, keycap; }
    public Tab[] tabs = new Tab[0];
    public Text heading, status, detailTag, detailTitle, detailBody, closeLabel, closeKey, hint;
    public Image detailIcon;
    public ScrollRect wallScroll, detailScroll;
    public RectTransform frame, header, tabBar, wallArea, detailArea, detailHead, bar, wallContent;
    public FlatsTileWall wall;
    public FlatsTile tileTemplate;
    public RogueRangeOption[] options = new RogueRangeOption[0];
    public Button close;
    public CanvasGroup group;
    public CanvasScaler scaler;
    [Header("Layout (canvas units)")]
    public Vector2 landscapeReference = new Vector2(1920, 1080);
    public Vector2 portraitReference = new Vector2(1080, 1920);
    public float margin = 32, desktopSideMargin = 80, maxWidth = 1760, headerHeight = 104, tabHeight = 56, barHeight = 72, gap = 16;
    public float detailWidth = 560, detailHeadHeight = 112, optionHeight = 56, optionGap = 8;
    public float portraitHeaderHeight = 170, portraitHeadingHeight = 96, portraitDetailFraction = .5f, portraitDetailHeadHeight = 92, portraitOptionHeight = 60, portraitTabHeight = 72;
    public float compactAspect = 1.45f, compactDetailWidth = 420;
    [Tooltip("Least height the description keeps; the action rows get shorter (never below 36) before the text loses more.")]
    public float minBodyHeight = 132, portraitMinBody = 96;
    [Tooltip("Room under the description's last line: the line spacing puts the glyphs (Chinese most of all) a little below the text's own height, and the scroll view must not cut them.")]
    public float bodyBottomRoom = 8;
    [Tooltip("Description font size on a portrait screen (the authored size is kept in landscape): 22 units are about 8 px on a 390 px wide phone.")]
    public int portraitBodyFont = 30;
    [Tooltip("Left inset of a tab or button label with and without its key cap, and the inset of the other three sides.")]
    public float labelInsetWithKey = 76, labelInset = 8, labelInsetVertical = 4;
    int authoredBodyFont;
    public float activationDelay = .25f, padScrollSpeed = 900, refreshSeconds = .5f;

    public int Current { get; private set; }

    RoguelikeController controller;
    RogueRangeRuntime runtime;
    readonly Dictionary<string, FlatsTile> pool = new Dictionary<string, FlatsTile>();
    readonly List<FlatsTile> shown = new List<FlatsTile>();
    readonly List<RogueRangeEntry> entries = new List<RogueRangeEntry>();
    readonly Dictionary<FlatsTile, RogueRangeEntry> byTile = new Dictionary<FlatsTile, RogueRangeEntry>();
    RogueRangeEntry focused;
    string focusedKey;
    string previousState;
    bool previousCamRotate, hudWasEnabled, messageWasEnabled, entered, released, navigationDirty = true, layoutDirty = true, focusFirst = true;
    Canvas hudCanvas, messageCanvas, ownCanvas;
    GameObject previousSelection;
    float activateAfter, nextRefresh;
    Vector2 lastSize; int shownOptions, lastScheme = -1; bool lastChinese;
    string signature = "";

    public bool AcceptsInput { get { return entered && !released && isActiveAndEnabled && Menu.current == "RogueScreen" && !FlatsMenuDialog.BlocksMenuInput; } }

    public static RogueRangeView Open(RoguelikeController controller, RogueRangeRuntime runtime)
    {
        var prefab = Resources.Load<RogueRangeView>(ResourcePath);
        if (prefab == null) { Debug.LogWarning("FLATS_ROGUE_RANGE missing Resources/" + ResourcePath); return null; }
        var view = Instantiate(prefab);
        view.name = "RogueRangeMenu";
        view.controller = controller; view.runtime = runtime;
        view.Enter();
        return view;
    }

    void Enter()
    {
        previousState = Menu.current; previousCamRotate = FPSController.enableCamRotate;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (Menu.current == "Playing") Menu.current = "RogueScreen";
        FPSController.enableCamRotate = false;
        FlatsCursor.Push(this); PointerFocusPolicy.Hold(this);
        var hud = GameObject.Find("UI"); hudCanvas = hud != null ? hud.GetComponent<Canvas>() : null;
        var message = GameObject.Find("Message"); messageCanvas = message != null ? message.GetComponent<Canvas>() : null;
        if (hudCanvas != null) { hudWasEnabled = hudCanvas.enabled; hudCanvas.enabled = false; }
        if (messageCanvas != null) { messageWasEnabled = messageCanvas.enabled; messageCanvas.enabled = false; }
        ownCanvas = GetComponent<Canvas>();
        entered = true; activateAfter = Time.unscaledTime + activationDelay;
        for (int i = 0; i < tabs.Length; i++) { int index = i; tabs[i].button.onClick.AddListener(() => { if (AcceptsInput) { RogueAudio.Click(); Select(index); } }); }
        close.onClick.AddListener(() => { if (AcceptsInput && Time.unscaledTime >= activateAfter) { RogueAudio.Click(); RequestClose(); } });
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            options[i].button.onClick.AddListener(() => PressOption(index));
        }
        FlatsLocalization.Changed += LanguageChanged;
        Select(lastTab);
    }

    static int lastTab;   // reopening returns to the tab the player was on (the session's, never saved)

    void LanguageChanged() { signature = ""; Rebuild(true); }

    public void Close() { Release(); Destroy(gameObject); }

    void RequestClose()
    {
        if (Input.GetKey(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Escape)) RogueOverviewView.HoldEscapeUntilReleased();   // the same Esc must not open the pause menu
        Close();
    }

    void Release()
    {
        if (!entered || released) return;
        released = true;
        FlatsLocalization.Changed -= LanguageChanged;
        if (Menu.current == "RogueScreen") Menu.current = previousState == "RogueScreen" ? "Playing" : previousState;
        if (Menu.current == "Playing") FPSController.enableCamRotate = true;
        else FPSController.enableCamRotate = previousCamRotate;
        FlatsCursor.Pop(this); PointerFocusPolicy.Release(this);
        bool playing = Menu.current == "Playing";
        if (hudCanvas != null) hudCanvas.enabled = hudWasEnabled || playing;
        if (messageCanvas != null) messageCanvas.enabled = messageWasEnabled || playing;
        if (EventSystem.current != null && (previousSelection == null || previousSelection.activeInHierarchy)) EventSystem.current.SetSelectedGameObject(previousSelection);
        if (runtime != null) runtime.MenuClosed(this);
    }

    void OnDisable() { Release(); }
    void OnDestroy() { Release(); }

    // ---------------------------------------------------------------- pages
    public void Select(int index)
    {
        if (tabs.Length == 0) return;
        Current = (index % tabs.Length + tabs.Length) % tabs.Length; lastTab = Current;
        focusedKey = null; focused = null; focusFirst = true; signature = "";
        activateAfter = Time.unscaledTime + activationDelay;
        Rebuild(true);
        if (wallScroll != null) { wallScroll.StopMovement(); wallScroll.verticalNormalizedPosition = 1f; }
    }

    /// <summary>Reads the page again and binds it: after every action, and twice a second while open (the state changes under it).</summary>
    public void Rebuild(bool force)
    {
        if (released || controller == null) return;
        entries.Clear();
        RogueRangePages.Build(controller, runtime, this, Current, entries);
        var sb = new System.Text.StringBuilder(1024);
        sb.Append(Current).Append(FlatsLocalization.IsChinese ? 'z' : 'e').Append((int)RogueInput.Current);
        foreach (var e in entries)
        {
            sb.Append('|').Append(e.key).Append(e.title).Append(e.number).Append(e.subtitle).Append(e.selected ? '1' : '0').Append(e.body.Length);
            foreach (var a in e.actions) sb.Append(';').Append(a.label).Append(a.value).Append(a.enabled ? '1' : '0');
        }
        string now = sb.ToString();
        string statusText = RogueRangePages.Status(controller, runtime);
        if (status != null && status.text != statusText) status.text = statusText;
        if (!force && now == signature) return;
        signature = now;

        for (int i = 0; i < tabs.Length; i++)
        {
            tabs[i].back.color = i == Current ? FlatsUiTheme.Rogue.brandPrimary : FlatsUiTheme.WithAlpha(FlatsUiTheme.Rogue.ink, .85f);
            Put(tabs[i].label, RogueRangePages.TabName(i));
        }
        Put(heading, RoguelikeController.T("Shooting range"));
        Put(closeLabel, RoguelikeController.T("Close"));

        shown.Clear(); byTile.Clear();
        var used = new HashSet<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            FlatsTile tile;
            if (!pool.TryGetValue(entry.key, out tile) || tile == null)
            {
                tile = Instantiate(tileTemplate, wallContent, false);
                tile.name = "Tile-" + entry.key;
                var captured = tile;
                tile.focused = t => TileFocused(captured);
                pool[entry.key] = tile; navigationDirty = true;
            }
            if (!tile.gameObject.activeSelf) { tile.gameObject.SetActive(true); navigationDirty = true; }
            if (tile.transform.GetSiblingIndex() != i) { tile.transform.SetSiblingIndex(i); navigationDirty = true; }
            var bound = entry;
            tile.Bind(entry.title, entry.tag, entry.subtitle, entry.number, entry.icon, entry.token, () => TileClicked(bound));
            // names and descriptions arrive translated: the tile must not look them up again
            tile.title.text = entry.title; tile.label.text = entry.tag; tile.subtitle.text = entry.subtitle;
            tile.SetSelected(entry.selected);
            used.Add(entry.key); shown.Add(tile); byTile[tile] = entry;
        }
        foreach (var pair in pool) if (!used.Contains(pair.Key) && pair.Value != null && pair.Value.gameObject.activeSelf) { pair.Value.gameObject.SetActive(false); navigationDirty = true; }

        RogueRangeEntry target = null;
        if (focusedKey != null) foreach (var e in entries) if (e.key == focusedKey) target = e;
        if (target == null && entries.Count > 0) target = entries[0];
        Focus(target);
        if (focusFirst && target != null)
        {
            focusFirst = false;
            if (EventSystem.current != null && !RogueInput.IsTouch) EventSystem.current.SetSelectedGameObject(pool[target.key].gameObject);
        }
        layoutDirty = true;
    }

    void TileFocused(FlatsTile tile)
    {
        // the pointer crossing a tile on its way to the detail panel changes nothing: only a selected (or clicked) tile takes the panel
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject != tile.gameObject) return;
        RogueRangeEntry entry;
        if (byTile.TryGetValue(tile, out entry)) Focus(entry);
    }

    void TileClicked(RogueRangeEntry entry)
    {
        if (!AcceptsInput || Time.unscaledTime < activateAfter) return;
        Focus(entry);
        // keyboard and pad: Enter / A on a tile moves into its actions (one press when it has a single action runs it)
        if (PointerFocusPolicy.PointerActive || EventSystem.current == null) return;
        for (int i = 0; i < shownOptions; i++)
            if (options[i].button.interactable) { RogueAudio.Click(); EventSystem.current.SetSelectedGameObject(options[i].gameObject); return; }
    }

    void Focus(RogueRangeEntry entry)
    {
        focused = entry; focusedKey = entry != null ? entry.key : null;
        Put(detailTitle, entry != null ? entry.title : "");
        Put(detailTag, entry != null ? entry.tag : "");
        string bodyBefore = detailBody != null ? detailBody.text : "";
        Put(detailBody, entry != null ? entry.body : "");
        if (detailBody != null && detailBody.text != bodyBefore) layoutDirty = true;   // the description's height follows its text
        if (detailIcon != null) { detailIcon.sprite = entry != null ? entry.icon : null; detailIcon.enabled = detailIcon.sprite != null; }
        int count = entry != null ? Mathf.Min(entry.actions.Count, options.Length) : 0;
        for (int i = 0; i < options.Length; i++)
        {
            bool on = i < count;
            if (options[i].gameObject.activeSelf != on) { options[i].gameObject.SetActive(on); navigationDirty = true; }
            if (!on) continue;
            var a = entry.actions[i];
            bool was = options[i].button.interactable;
            options[i].Bind(a.label, a.value, a.enabled, a.primary, null);
            if (was != a.enabled) navigationDirty = true;
        }
        if (shownOptions != count) { shownOptions = count; layoutDirty = true; navigationDirty = true; }
    }

    void PressOption(int index)
    {
        if (!AcceptsInput || Time.unscaledTime < activateAfter || focused == null || index >= focused.actions.Count) return;
        var action = focused.actions[index];
        if (!action.enabled || action.press == null) return;
        activateAfter = Time.unscaledTime + 0.12f;
        RogueAudio.Click();
        action.press();
        if (action.closes) { Close(); return; }
        if (released) return;
        Rebuild(true);
        nextRefresh = Time.unscaledTime + 0.15f;   // the authority's answer arrives a moment later
    }

    // ---------------------------------------------------------------- input
    void Update()
    {
        if (released) return;
        // an instance nobody opened (the prefab on its own: a layout preview, the text-fit check) only lays itself out (LateUpdate)
        if (!entered) return;
        if (controller == null || !controller.RangeActive) { Close(); return; }
        // the pause menu, the result or a scene change took the state: this screen does not stay under them
        if (Menu.current != "RogueScreen") { Close(); return; }
        if (hudCanvas != null && hudCanvas.enabled) hudCanvas.enabled = false;
        if (messageCanvas != null && messageCanvas.enabled) messageCanvas.enabled = false;
        bool dialog = FlatsMenuDialog.BlocksMenuInput;
        if (group != null) { group.interactable = !dialog; group.blocksRaycasts = !dialog; }
        if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + refreshSeconds; if (!Input.GetMouseButton(0)) Rebuild(false); }
        int scheme = (int)RogueInput.Current * 31 + RogueInput.TabPreviousKey.GetHashCode() + RogueInput.TabNextKey.GetHashCode() * 7;
        if (scheme != lastScheme || lastChinese != FlatsLocalization.IsChinese) { lastScheme = scheme; lastChinese = FlatsLocalization.IsChinese; UpdateHints(); }
        if (!AcceptsInput) return;
        var pad = InControl.InputManager.ActiveDevice;
        if (RogueInput.TabPreviousDown) { Select(Current - 1); return; }
        if (RogueInput.TabNextDown) { Select(Current + 1); return; }
        var es = EventSystem.current;
        bool back = Input.GetKeyDown(KeyCode.Escape) || (pad != null && pad.Action2.WasPressed);
        if (back && Time.unscaledTime >= activateAfter)
        {
            // from an action row: back to its tile; from the wall: close
            var selected = es != null ? es.currentSelectedGameObject : null;
            bool inOptions = false;
            for (int i = 0; i < shownOptions; i++) if (selected == options[i].gameObject) inOptions = true;
            if (inOptions && focused != null && pool.ContainsKey(focused.key) && !PointerFocusPolicy.PointerActive) { es.SetSelectedGameObject(pool[focused.key].gameObject); activateAfter = Time.unscaledTime + 0.1f; }
            else { RequestClose(); return; }
        }
        if (Time.unscaledTime >= activateAfter && !RogueInput.IsTouch && RogueInput.ShopDown) { RequestClose(); return; }   // the key that opened it closes it
        if (es != null && !RogueInput.IsTouch && !PointerFocusPolicy.PointerActive)
        {
            var selected = es.currentSelectedGameObject;
            if ((selected == null || !selected.activeInHierarchy || !selected.transform.IsChildOf(transform)) && focused != null && pool.ContainsKey(focused.key))
                es.SetSelectedGameObject(pool[focused.key].gameObject);
        }
        if (pad != null && Mathf.Abs(pad.RightStickY.Value) > .15f)
        {
            float range = wallScroll.content.rect.height - wallScroll.viewport.rect.height;
            var target = range > 1 || detailScroll == null ? wallScroll : detailScroll;
            if (target != wallScroll) range = target.content.rect.height - target.viewport.rect.height;
            if (range > 1) target.verticalNormalizedPosition = Mathf.Clamp01(target.verticalNormalizedPosition + pad.RightStickY.Value * padScrollSpeed * Time.unscaledDeltaTime / range);
        }
    }

    static string FirstName(string glyph) { int cut = glyph != null ? glyph.IndexOf(" / ") : -1; return cut > 0 ? glyph.Substring(0, cut) : glyph; }

    void UpdateHints()
    {
        var scheme = RogueInput.Current; var style = FlatsGamepad.DeviceStyle(InControl.InputManager.ActiveDevice);
        bool touch = scheme == RogueInput.Scheme.Touch, pad = scheme == RogueInput.Scheme.Gamepad;
        Put(closeKey, touch ? "" : pad ? FirstName(FlatsGamepad.Glyph(InControl.InputControlType.Action2, style)) : "Esc");
        if (closeKey != null) closeKey.transform.parent.gameObject.SetActive(!touch);
        InsetLabel(closeLabel, !touch && closeKey != null);
        string previous = touch ? "" : pad ? FirstName(FlatsGamepad.Glyph(InControl.InputControlType.LeftBumper, style)) : RogueInput.TabPreviousKey;
        string next = touch ? "" : pad ? FirstName(FlatsGamepad.Glyph(InControl.InputControlType.RightBumper, style)) : RogueInput.TabNextKey;
        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i].keycap == null) continue;
            string cap = i == 0 ? previous : i == tabs.Length - 1 ? next : "";
            Put(tabs[i].keycap, cap);
            tabs[i].keycap.transform.parent.gameObject.SetActive(cap != "");
            InsetLabel(tabs[i].label, cap != "");
        }
        Put(hint, touch ? RoguelikeController.T("Tap a tile, then one of its actions.")
            : pad ? RoguelikeController.T("{0} choose   {1} back   {2} / {3} tabs", FirstName(FlatsGamepad.Glyph(InControl.InputControlType.Action1, style)), FirstName(FlatsGamepad.Glyph(InControl.InputControlType.Action2, style)), previous, next)
            : RoguelikeController.T("Click a tile, then one of its actions.   {0} / {1} tabs", previous, next));
        layoutDirty = true;
    }

    /// <summary>A label gives up room on the left only while its key cap is shown (the middle tabs and every touch button have none).</summary>
    void InsetLabel(Text label, bool hasKey)
    {
        if (label == null) return;
        var r = label.rectTransform;
        var min = new Vector2(hasKey ? labelInsetWithKey : labelInset, labelInsetVertical); var max = new Vector2(-labelInset, -labelInsetVertical);
        if (r.offsetMin != min) r.offsetMin = min;
        if (r.offsetMax != max) r.offsetMax = max;
    }

    // ---------------------------------------------------------------- layout and navigation
    static void Place(RectTransform r, float x, float y, float w, float h)
    { r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); }

    void LateUpdate()
    {
        if (released) return;
        // the canvas decides, not the screen: a window or a test view can differ from Screen for a frame
        if (ownCanvas == null) ownCanvas = GetComponent<Canvas>();
        var pixels = ownCanvas != null ? ownCanvas.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
        bool portrait = pixels.width > 0 && pixels.height > 0 ? pixels.height > pixels.width : Screen.height > Screen.width;
        if (scaler != null)
        {
            var reference = portrait ? portraitReference : landscapeReference; float match = portrait ? 0 : 1;
            if (scaler.referenceResolution != reference || scaler.matchWidthOrHeight != match) { scaler.referenceResolution = reference; scaler.matchWidthOrHeight = match; layoutDirty = true; }
        }
        var size = ((RectTransform)transform).rect.size;
        if (size != lastSize || layoutDirty) { lastSize = size; layoutDirty = false; Arrange(size, portrait); navigationDirty = true; }
        if (!navigationDirty) return;
        navigationDirty = false;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(wallContent);
        var all = new List<Selectable>();
        foreach (var tile in shown) all.Add(tile);
        foreach (var tab in tabs) all.Add(tab.button);
        all.Add(close);
        for (int i = 0; i < shownOptions; i++) if (options[i].button.interactable) all.Add(options[i].button);
        foreach (var b in all)
        {
            var n = new Navigation { mode = Navigation.Mode.Explicit };
            n.selectOnLeft = Neighbour(b, all, Vector2.left); n.selectOnRight = Neighbour(b, all, Vector2.right);
            n.selectOnUp = Neighbour(b, all, Vector2.up); n.selectOnDown = Neighbour(b, all, Vector2.down);
            b.navigation = n;
        }
    }

    static Selectable Neighbour(Selectable from, List<Selectable> all, Vector2 direction)
    {
        var fr = (RectTransform)from.transform;
        Vector2 origin = fr.TransformPoint(fr.rect.center); float best = float.MaxValue; Selectable result = null;
        foreach (var to in all)
        {
            if (to == from) continue;
            var tr = (RectTransform)to.transform;
            Vector2 delta = (Vector2)tr.TransformPoint(tr.rect.center) - origin;
            float along = Vector2.Dot(delta, direction);
            if (along <= 1) continue;
            float cross = Mathf.Abs(Vector2.Dot(delta, new Vector2(-direction.y, direction.x)));
            float score = along + cross * 3;
            if (score < best) { best = score; result = to; }
        }
        return result;
    }

    void Arrange(Vector2 size, bool portrait)
    {
        if (size.x <= 0 || size.y <= 0) return;
        var safe = Screen.safeArea; float sx = size.x / Mathf.Max(1, Screen.width), sy = size.y / Mathf.Max(1, Screen.height);
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        bool compact = !portrait && aspect < compactAspect;
        float side = portrait || RogueInput.IsTouch || compact ? margin : desktopSideMargin;
        float w = Mathf.Min(maxWidth, safe.width * sx - side * 2), h = safe.height * sy - margin * 2;
        Place(frame, safe.x * sx + (safe.width * sx - w) / 2, (Screen.height - safe.yMax) * sy + margin, w, h);
        float hh = portrait ? portraitHeaderHeight : headerHeight, th = portrait ? portraitTabHeight : tabHeight;
        Place(header, 0, 0, w, hh);
        // heading on the left, the live status on the right (portrait: under it)
        if (portrait) { Place(heading.rectTransform, 0, 0, w, portraitHeadingHeight); Place(status.rectTransform, 0, portraitHeadingHeight + 4, w, hh - portraitHeadingHeight - 4); status.alignment = TextAnchor.UpperLeft; }
        else { Place(heading.rectTransform, 0, 0, w * .45f, hh); Place(status.rectTransform, w * .45f, 0, w * .55f, hh); status.alignment = TextAnchor.MiddleRight; }
        Place(tabBar, 0, hh + gap, w, th);
        float tw = (w - gap * (tabs.Length - 1)) / Mathf.Max(1, tabs.Length);
        for (int i = 0; i < tabs.Length; i++) Place((RectTransform)tabs[i].button.transform, i * (tw + gap), 0, tw, th);
        Place(bar, 0, h - barHeight, w, barHeight);
        float closeWidth = portrait ? w * .42f : Mathf.Min(320, w * .28f);
        Place((RectTransform)close.transform, 0, 0, closeWidth, barHeight);
        Place(hint.rectTransform, closeWidth + gap, 0, w - closeWidth - gap, barHeight);
        float top = hh + gap + th + gap, available = Mathf.Max(200, h - top - barHeight - gap);
        float oh = portrait ? portraitOptionHeight : optionHeight, dh = portrait ? portraitDetailHeadHeight : detailHeadHeight;
        if (portrait)
        {
            float detail = Mathf.Round(available * portraitDetailFraction);
            Place(wallArea, 0, top, w, available - detail - gap);
            Place(detailArea, 0, top + available - detail, w, detail);
        }
        else
        {
            float dw = compact ? Mathf.Min(compactDetailWidth, w * .4f) : Mathf.Min(detailWidth, w * .36f);
            Place(wallArea, 0, top, w - dw - gap, available);
            Place(detailArea, w - dw, top, dw, available);
        }
        // inside the detail panel: head (icon, tag, title), the description (scrolls), then the action rows from the bottom up
        float dwidth = detailArea.sizeDelta.x, dheight = detailArea.sizeDelta.y, pad = 16;
        Place(detailHead, pad, pad, dwidth - pad * 2, dh);
        float optionsHeight = shownOptions > 0 ? shownOptions * oh + (shownOptions - 1) * optionGap : 0;
        // a small screen keeps at least a few lines of description: the rows shrink before the text disappears
        float minBody = portrait ? portraitMinBody : minBodyHeight;
        if (optionsHeight > dheight - dh - pad * 3 - minBody && shownOptions > 0)
        {
            oh = Mathf.Max(36, (dheight - dh - pad * 3 - minBody - (shownOptions - 1) * optionGap) / shownOptions);
            optionsHeight = shownOptions * oh + (shownOptions - 1) * optionGap;
        }
        float bodyHeight = Mathf.Max(0, dheight - dh - pad * 3 - optionsHeight - (optionsHeight > 0 ? pad : 0));
        Place((RectTransform)detailScroll.transform, pad, pad + dh + pad, dwidth - pad * 2, bodyHeight);
        float oy = dheight - pad - optionsHeight;
        for (int i = 0; i < shownOptions; i++) Place((RectTransform)options[i].transform, pad, oy + i * (oh + optionGap), dwidth - pad * 2, oh);
        // the description is as tall as its text at this width, plus the room its last line needs
        if (detailBody != null)
        {
            if (authoredBodyFont == 0) authoredBodyFont = detailBody.fontSize;
            int font = portrait ? Mathf.Max(authoredBodyFont, portraitBodyFont) : authoredBodyFont;
            if (detailBody.fontSize != font) detailBody.fontSize = font;
            var body = detailBody.rectTransform;
            float wanted = Mathf.Ceil(detailBody.preferredHeight) + bodyBottomRoom;
            if (Mathf.Abs(body.sizeDelta.y - wanted) > .5f) body.sizeDelta = new Vector2(body.sizeDelta.x, wanted);
        }
        if (detailScroll != null) detailScroll.verticalNormalizedPosition = 1f;
    }

    static void Put(Text label, string value) { if (label != null && label.text != (value ?? "")) label.text = value ?? ""; }
}
