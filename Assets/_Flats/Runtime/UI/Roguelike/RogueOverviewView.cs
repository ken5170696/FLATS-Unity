using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>兩頁總覽；固定畫面由 Prefab 授權，控制器僅綁定資料。</summary>
public class RogueOverviewView : MonoBehaviour
{
    [Serializable] public class Tab { public Button button; public Image back, icon; public Text label, keycap; public string iconName, key; }
    public Tab[] tabs = new Tab[0];
    public Text title, subtitle, footer, closeLabel, closeKey, actionLabel, actionKey, shopLabel, shopKey;
    public Image titleIcon, paper, detailIcon;
    public RectTransform rowsContent, inlineDetail;
    public ScrollRect scroll;
    public Button close, action, shop;
    public FlatsOverviewTile smallTemplate, wideTemplate, squadTemplate;
    public FlatsDetailPanel detail;
    public FlatsOverviewDetail structuredDetail;
    public Text wallet;
    public FlatsOverviewWall wall;
    public FlatsOverviewLayout layout;
    public FlatsActionBar actionBar;
    public CanvasGroup inputGroup;
    public float padScrollSpeed = 600, activationDelay = .35f;
    public int Current { get; private set; }
    public event Action<int> TabChanged;
    public event Action Disabled;
    public readonly List<FlatsOverviewTile> Tiles = new List<FlatsOverviewTile>();
    readonly Dictionary<string, FlatsOverviewTile> byKey = new Dictionary<string, FlatsOverviewTile>();
    readonly HashSet<string> used = new HashSet<string>();
    public FlatsOverviewTile FocusedTile { get; private set; }
    string previousState, focusKey, detailSignature;
    bool previousCamRotate, previousSuspended, hudWasEnabled, messageWasEnabled, entered, released, first = true, navigationDirty, confirmWasOpen;
    Canvas hudCanvas, messageCanvas;
    bool focusHeld, revealPending;
    string requestedKey;
    ScrollRect detailScroll;
    void HoldFocusPolicy(bool hold) { if (hold == focusHeld) return; focusHeld = hold; if (hold) PointerFocusPolicy.Hold(this); else PointerFocusPolicy.Release(this); }
    GameObject previousSelection;
    ConfirmationDialogView confirm;
    Canvas ownCanvas;
    float nextDialogCheck, nextHints, activateAfter;
    int rowSequence;
    Text[] glyphLabels; int glyphFrames;
    FlatsOverviewTile detailTarget;
    public void CollectDetails(FlatsOverviewTile target) { detailTarget = target; }
    public bool AcceptsInput { get { return entered && !released && isActiveAndEnabled && (confirm == null || !confirm.gameObject.activeInHierarchy) && Menu.current == "RogueScreen"; } }
    public static RogueOverviewView Open()
    {
        var prefab = Resources.Load<RogueOverviewView>("UI/Roguelike/RogueOverview");
        var menu = GameObject.Find("Menu"); if (prefab == null || menu == null) return null;
        var view = prefab.GetComponent<Canvas>() != null ? Instantiate(prefab) : Instantiate(prefab, menu.transform, false);
        view.name = "RogueOverview"; view.Enter(); return view;
    }
    void Enter()
    {
        previousState = Menu.current; previousCamRotate = FPSController.enableCamRotate;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        previousSuspended = RogueScreenView.Suspended;
        var under = FindObjectOfType<RogueScreenView>(); if (under != null) under.SetCovered(true);
        if (Menu.current == "Playing") Menu.current = "RogueScreen";
        RogueScreenView.Suspended = true; FPSController.enableCamRotate = false; FlatsCursor.Push(this);
        var hud = GameObject.Find("UI"); hudCanvas = hud != null ? hud.GetComponent<Canvas>() : null;
        var message = GameObject.Find("Message"); messageCanvas = message != null ? message.GetComponent<Canvas>() : null;
        if (hudCanvas != null) { hudWasEnabled = hudCanvas.enabled; hudCanvas.enabled = false; }
        if (messageCanvas != null) { messageWasEnabled = messageCanvas.enabled; messageCanvas.enabled = false; }
        HoldFocusPolicy(true);
        ownCanvas = GetComponent<Canvas>();
        detailScroll = structuredDetail == null ? null : structuredDetail.GetComponentInChildren<ScrollRect>(true) ?? structuredDetail.GetComponentInParent<ScrollRect>();
        entered = true; Font.textureRebuilt += FontAtlasChanged; activateAfter = Time.unscaledTime + activationDelay;
        for (int i = 0; i < tabs.Length; i++) { int index = i; tabs[i].button.onClick.AddListener(() => { if (AcceptsInput) { RogueAudio.Click(); Select(index); } }); }
        close.onClick.AddListener(() => { if (AcceptsInput) RogueAudio.Click(); RequestClose(); });
        action.onClick.AddListener(ActivateFocused);
        PaintTabs(); UpdateHints();
        if (actionBar != null) { actionBar.enabled = false; ReflowActions(); }
    }
    /// <summary>The focused tile's action (Remove), from the action button or from Submit on the tile itself. The focus is the tile
    /// last clicked, selected or submitted, never one the pointer merely crossed on its way to the button.</summary>
    public void ActivateFocused()
    {
        if (!AcceptsInput || Time.unscaledTime < activateAfter || FocusedTile == null || !FocusedTile.Available || FocusedTile.Action == null) return;
        activateAfter = Time.unscaledTime + activationDelay; RogueAudio.Click(); FocusedTile.Action();
    }
    /// <summary>Opens the next fill with this tile focused (the briefing's Details goes straight to the objective's guide).</summary>
    public void RequestFocus(string key) { requestedKey = key; }
    static bool escapeHeldAfterClose;
    /// <summary>True until the Esc press that closed the overview is released: the menu underneath opens the pause menu on Esc
    /// key-up and must not act on the same press.</summary>
    public static bool BlocksMenuInput
    {
        get
        {
            if (!escapeHeldAfterClose) return false;
            if (Input.GetKey(KeyCode.Escape) || Input.GetKeyUp(KeyCode.Escape)) return true;
            escapeHeldAfterClose = false;
            return false;
        }
    }
    int guardedFrame = -1;
    /// <summary>The pause menu closed over the overview: the B or click that closed it neither closes the overview nor activates a tile.</summary>
    public void GuardInput() { guardedFrame = Time.frameCount; activateAfter = Time.unscaledTime + activationDelay; }
    void RequestClose() { if (!AcceptsInput) return; escapeHeldAfterClose = Input.GetKey(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Escape); var c = RoguelikeController.Instance; if (c != null && c.OverviewOpen) c.CloseOverview(); else Close(); }
    public void Close() { Release(); Destroy(gameObject); }
    void Release()
    {
        if (!entered || released) return;
        released = true; Font.textureRebuilt -= FontAtlasChanged; RogueScreenView.Suspended = previousSuspended;
        // a screen that was closed this frame still exists until the frame ends: it is not a screen below any more
        var under = FindObjectOfType<RogueScreenView>(); bool below = under != null && under.gameObject.activeInHierarchy && !under.Closed;
        if (below) { under.SetCovered(false); under.RebuildNavigation(); }
        if (Menu.current == "RogueScreen" && !below) Menu.current = previousState == "RogueScreen" ? "Playing" : previousState;
        if (Menu.current == "Playing" || Menu.current == "RogueScreen") FPSController.enableCamRotate = !below && (previousCamRotate || Menu.current == "Playing");
        FlatsCursor.Pop(this);
        HoldFocusPolicy(false);
        if (!below)
        {
            bool playing = Menu.current == "Playing";
            if (hudCanvas != null) hudCanvas.enabled = hudWasEnabled || playing;
            if (messageCanvas != null) messageCanvas.enabled = messageWasEnabled || playing;
            if (EventSystem.current != null && (previousSelection == null || previousSelection.activeInHierarchy)) EventSystem.current.SetSelectedGameObject(previousSelection);
        }
    }
    void OnDisable() { Release(); if (Disabled != null) Disabled(); }
    void OnDestroy() { Release(); }
    public void Select(int index)
    {
        if (tabs.Length == 0) return;
        Current = (index % tabs.Length + tabs.Length) % tabs.Length; first = true; focusKey = null;
        PaintTabs(); activateAfter = Time.unscaledTime + activationDelay;
        if (TabChanged != null) TabChanged(Current);
    }
    void PaintTabs()
    {
        for (int i = 0; i < tabs.Length; i++) { tabs[i].back.color = i == Current ? FlatsUiTheme.Rogue.brandPrimary : FlatsUiTheme.WithAlpha(FlatsUiTheme.Rogue.ink, .8f); FlatsOverviewTile.Put(tabs[i].label, RoguelikeController.T(i == 0 ? "My equipment" : "Squad and run")); }
    }
    public void SetHeader(string iconName, string heading, string sub) { FlatsOverviewTile.Put(title, heading); FlatsOverviewTile.Put(subtitle, sub); PaintTabs(); }
    public void SetWallet(string value) { FlatsOverviewTile.Put(wallet, value.Replace("$", "<size=22>$</size>")); }
    public void SetFooter(string text) { FlatsOverviewTile.Put(footer, text); }
    public void SetShop(bool available, Action open)
    {
        if (shop.interactable != available) navigationDirty = true;
        shop.interactable = available; shop.onClick.RemoveAllListeners();
        if (open != null) shop.onClick.AddListener(() => { if (AcceptsInput && Time.unscaledTime >= activateAfter) { RogueAudio.Click(); open(); } });
        FlatsOverviewTile.Put(shopLabel, RoguelikeController.T(available ? "Shop" : "Shop between stages"));
    }
    public void ClearRows() { ClearRows(false); }
    public void ClearRows(bool keepScroll) { used.Clear(); Tiles.Clear(); rowSequence = 0; if (!first && FocusedTile != null) focusKey = FocusedTile.Key; }
    public FlatsOverviewTile AddTile(string key, string iconName, string kind, string name, string pitch, string value, Color tint, string effect, string next = "", string headline = "", bool wide = false, Action callback = null, string actionText = "", bool available = false, Sprite sprite = null)
    {
        FlatsOverviewTile tile;
        if (!byKey.TryGetValue(key, out tile) || tile == null) { tile = Instantiate(key.StartsWith("player-") ? squadTemplate : wide ? wideTemplate : smallTemplate, rowsContent, false); tile.owner = this; tile.Key = key; tile.name = "Tile-" + key; byKey[key] = tile; navigationDirty = true; }
        if (!tile.gameObject.activeSelf) { tile.gameObject.SetActive(true); navigationDirty = true; }
        tile.Bind(kind, name, pitch, value, sprite != null ? sprite : FlatsOverviewTile.Icon(iconName), tint, effect, next, headline, callback, actionText, available);
        used.Add(key); Tiles.Add(tile); return tile;
    }
    // 保留既有 Meta 的綁定入口，完整文字移至焦點說明。
    public RogueStatRowView AddStat(string iconName, string label, string value, string sub, float bar, Color tint)
    {
        if (detailTarget != null) { detailTarget.Extra += "\n" + label + " " + value + "\n" + sub; return null; }
        AddTile("info-" + rowSequence++, iconName, "", "", label, value != null && value.Length < 9 ? value : "", tint, (value ?? "") + (string.IsNullOrEmpty(sub) ? "" : "\n" + sub), "", value != null && value.Length < 9 ? value : "", true); return null;
    }
    public void FitBody()
    {
        foreach (var pair in byKey) if (!used.Contains(pair.Key) && pair.Value.gameObject.activeSelf) { pair.Value.gameObject.SetActive(false); navigationDirty = true; }
        int position = 0;
        for (int i = 0; i < Tiles.Count; i++) { if (Tiles[i].transform.GetSiblingIndex() != position) { Tiles[i].transform.SetSiblingIndex(position); navigationDirty = true; } position++; if (Screen.height > Screen.width && Tiles[i] == FocusedTile) position++; }
        FlatsOverviewTile target = null;
        if (requestedKey != null) { byKey.TryGetValue(requestedKey, out target); if (target != null) requestedKey = null; }
        if (target == null && !first && focusKey != null) byKey.TryGetValue(focusKey, out target);
        if (target == null || !target.gameObject.activeSelf) target = Tiles.Count > 0 ? Tiles[0] : null;
        if (target != null) { Focus(target); if (first && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target.gameObject); }
        if (first) { scroll.verticalNormalizedPosition = 1; first = false; navigationDirty = true; }
    }
    public void Focus(FlatsOverviewTile tile)
    {
        // the list scrolls to a tile when the focus moves to it, not on every data sync: a list the player dragged or scrolled
        // with the stick must stay where they left it
        if (FocusedTile != tile) { if (FocusedTile != null) FocusedTile.SetFocus(false); FocusedTile = tile; focusKey = tile.Key; tile.SetFocus(true); revealPending = true; if (Screen.height > Screen.width) navigationDirty = true; }
        ShowDetail(tile);
    }
    public void ShowDetail(FlatsOverviewTile tile)
    {
        string signature = tile.Key + tile.Kind + tile.Detail + tile.Next + tile.DetailNumber + tile.Available + tile.Metadata + tile.Removal + tile.Extra + (tile.Stats == null ? "" : string.Join("|", tile.Stats));
        if (detailSignature != signature)
        {
            detailSignature = signature; glyphFrames = 4; if (Screen.height > Screen.width) navigationDirty = true;
            structuredDetail.Bind(tile, detail); detail.inlineHost = inlineDetail; detail.Reflow();
            detailIcon.sprite = tile.icon.sprite; detailIcon.enabled = string.IsNullOrEmpty(tile.DetailNumber);
            FlatsOverviewTile.Put(actionLabel, string.IsNullOrEmpty(tile.ActionLabel) ? RoguelikeController.T("This item cannot be removed") : tile.ActionLabel);
            bool available = tile.Available && tile.Action != null; if (action.interactable != available) navigationDirty = true; action.interactable = available;
        }
        if (Screen.height > Screen.width && inlineDetail.GetSiblingIndex() != tile.transform.GetSiblingIndex() + 1) inlineDetail.SetSiblingIndex(tile.transform.GetSiblingIndex() + 1);
    }
    public void LayoutChanged()
    {
        glyphFrames = 4;
        bool tall = Screen.height > Screen.width; inlineDetail.gameObject.SetActive(tall); detail.inlineHost = inlineDetail; detail.Reflow();
        if (FocusedTile != null) ShowDetail(FocusedTile); if (actionBar != null) ReflowActions(); navigationDirty = true;
    }
    public float actionGap = 16, removeWidth = 400, shopWidth = 320;
    void ReflowActions()
    {
        actionBar.Reflow();
        var a = (RectTransform)action.transform; var s = (RectTransform)shop.transform;
        if (Screen.height > Screen.width) { a.offsetMax -= new Vector2(actionGap / 2,0); s.offsetMin += new Vector2(actionGap / 2,0); return; }
        a.anchorMin = Vector2.zero; a.anchorMax = new Vector2(0,1); a.offsetMin = new Vector2(0,8); a.offsetMax = new Vector2(removeWidth,-8);
        s.anchorMin = Vector2.zero; s.anchorMax = new Vector2(0,1); s.offsetMin = new Vector2(removeWidth+actionGap,8); s.offsetMax = new Vector2(removeWidth+actionGap+shopWidth,-8);
    }
    // "A / Cross" when the pad's family is unknown: the small key cap keeps the first name only (it showed a clipped "A /")
    static string FirstName(string glyph) { int cut = glyph != null ? glyph.IndexOf(" / ") : -1; return cut > 0 ? glyph.Substring(0, cut) : glyph; }
    void UpdateHints()
    {
        var scheme = RogueInput.Current; var style = FlatsGamepad.DeviceStyle(InControl.InputManager.ActiveDevice);
        FlatsOverviewTile.Put(tabs[0].keycap, scheme == RogueInput.Scheme.Touch ? "" : scheme == RogueInput.Scheme.Gamepad ? FlatsGamepad.Glyph(InControl.InputControlType.LeftBumper, style) : RogueInput.TabPreviousKey);
        FlatsOverviewTile.Put(tabs[1].keycap, scheme == RogueInput.Scheme.Touch ? "" : scheme == RogueInput.Scheme.Gamepad ? FlatsGamepad.Glyph(InControl.InputControlType.RightBumper, style) : RogueInput.TabNextKey);
        FlatsOverviewTile.Put(closeKey, RogueIcons.KeyHint("Overview")); FlatsOverviewTile.Put(shopKey, RogueIcons.KeyHint("Shop"));
        FlatsOverviewTile.Put(actionKey, scheme == RogueInput.Scheme.Touch ? "" : scheme == RogueInput.Scheme.Gamepad ? FirstName(FlatsGamepad.Glyph(InControl.InputControlType.Action1, style)) : "Enter");
        FlatsOverviewTile.Put(closeLabel, RoguelikeController.T("Close"));
        foreach (var t in new[] { tabs[0].keycap, tabs[1].keycap, closeKey, shopKey, actionKey }) if (t != null && t.transform.parent.gameObject.activeSelf != !string.IsNullOrEmpty(t.text)) t.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(t.text));
    }
    void Update()
    {
        if (released) return;
        if (hudCanvas != null && hudCanvas.enabled) hudCanvas.enabled = false;
        if (messageCanvas != null && messageCanvas.enabled) messageCanvas.enabled = false;
        if (Time.unscaledTime >= nextDialogCheck) { nextDialogCheck = Time.unscaledTime + .1f; confirm = FindObjectOfType<ConfirmationDialogView>(); }
        bool dialog = confirm != null && confirm.gameObject.activeInHierarchy;
        if (ownCanvas != null) ownCanvas.enabled = !dialog && Menu.current == "RogueScreen";
        HoldFocusPolicy(!dialog && Menu.current == "RogueScreen");   // a dialog on top uses the policy like any menu
        if (inputGroup.interactable != !dialog) inputGroup.interactable = !dialog;
        if (inputGroup.blocksRaycasts != !dialog) inputGroup.blocksRaycasts = !dialog;
        if (confirmWasOpen && !dialog) { navigationDirty = true; activateAfter = Time.unscaledTime + activationDelay; if (FocusedTile != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(FocusedTile.gameObject); }
        confirmWasOpen = dialog;
        if (Time.unscaledTime >= nextHints) { nextHints = Time.unscaledTime + .5f; UpdateHints(); }
        if (!AcceptsInput) return;
        var pad = InControl.InputManager.ActiveDevice;
        if (RogueInput.TabPreviousDown) Select(Current - 1); else if (RogueInput.TabNextDown) Select(Current + 1);
        if (Time.frameCount != guardedFrame && (Input.GetKeyDown(KeyCode.Escape) || (pad != null && pad.Action2.WasPressed))) { RequestClose(); return; }
        if (RogueInput.ShopDown && shop.interactable) shop.onClick.Invoke();
        // a tile that was removed, replaced or hidden leaves the selection on an inactive object: directions would do nothing
        var es = EventSystem.current;
        if (es != null && !RogueInput.IsTouch && FocusedTile != null && FocusedTile.gameObject.activeInHierarchy)
        { var selected = es.currentSelectedGameObject; if (selected == null || !selected.activeInHierarchy || !selected.transform.IsChildOf(transform)) es.SetSelectedGameObject(FocusedTile.gameObject); }
        // right stick: the tile wall when it scrolls, otherwise the detail panel (long guides, the total stats)
        if (pad != null && Mathf.Abs(pad.RightStickY.Value) > .15f)
        {
            float range = scroll.content.rect.height - scroll.viewport.rect.height;
            var target = range > 1 || detailScroll == null ? scroll : detailScroll;
            if (target != scroll) range = target.content.rect.height - target.viewport.rect.height;
            if (range > 1) target.verticalNormalizedPosition = Mathf.Clamp01(target.verticalNormalizedPosition + pad.RightStickY.Value * padScrollSpeed * Time.unscaledDeltaTime / range);
        }
    }
    void FontAtlasChanged(Font font) { if (!released) glyphFrames = 2; }
    void LateUpdate()
    {
        if (navigationDirty) { glyphLabels = GetComponentsInChildren<Text>(true); glyphFrames = 4; }
        if (glyphFrames > 0 && glyphLabels != null) { glyphFrames--; foreach (var t in glyphLabels) if (t != null && t.isActiveAndEnabled && t.font != null && t.font.dynamic) t.font.RequestCharactersInTexture(t.text, Mathf.RoundToInt(t.fontSize * t.pixelsPerUnit), t.fontStyle); foreach (var t in glyphLabels) if (t != null && t.isActiveAndEnabled) { t.cachedTextGenerator.Invalidate(); t.SetVerticesDirty(); } }
        if (!navigationDirty) { if (revealPending && FocusedTile != null) { Reveal(FocusedTile); revealPending = false; } return; }
        navigationDirty = false; LayoutRebuilder.ForceRebuildLayoutImmediate(rowsContent);
        if (Screen.height > Screen.width)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)detail.transform);
            float preferred = structuredDetail.PreferredHeight();
            if (Mathf.Abs(wall.detailHeight - preferred) > 1) { wall.detailHeight = preferred; LayoutRebuilder.ForceRebuildLayoutImmediate(rowsContent); }
        }
        var buttons = new List<Selectable>(); foreach (var tile in Tiles) buttons.Add(tile); foreach (var tab in tabs) buttons.Add(tab.button);
        buttons.Add(close); if (action.interactable) buttons.Add(action); if (shop.interactable) buttons.Add(shop);
        foreach (var b in buttons) { var n = new Navigation { mode = Navigation.Mode.Explicit }; n.selectOnLeft = Neighbour(b, buttons, Vector2.left); n.selectOnRight = Neighbour(b, buttons, Vector2.right); n.selectOnUp = Neighbour(b, buttons, Vector2.up); n.selectOnDown = Neighbour(b, buttons, Vector2.down); b.navigation = n; }
        if (revealPending && FocusedTile != null) Reveal(FocusedTile);
        revealPending = false;
    }
    static Selectable Neighbour(Selectable from, List<Selectable> all, Vector2 direction)
    {
        Vector2 origin = ((RectTransform)from.transform).TransformPoint(((RectTransform)from.transform).rect.center); float best = float.MaxValue; Selectable result = null;
        foreach (var to in all) { if (to == from) continue; Vector2 delta = (Vector2)((RectTransform)to.transform).TransformPoint(((RectTransform)to.transform).rect.center) - origin; float along = Vector2.Dot(delta, direction); if (along <= 1) continue; float cross = Mathf.Abs(Vector2.Dot(delta, new Vector2(-direction.y, direction.x))); float score = along + cross * 3; if (score < best) { best = score; result = to; } } return result;
    }
    public void Reveal(FlatsOverviewTile tile)
    {
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, tile.transform); var rect = scroll.viewport.rect;
        float delta = bounds.max.y > rect.yMax ? rect.yMax - bounds.max.y : bounds.min.y < rect.yMin ? rect.yMin - bounds.min.y : 0;
        if (delta != 0) { scroll.content.anchoredPosition += new Vector2(0, delta); scroll.StopMovement(); }
    }
}
