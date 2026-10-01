using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Authored run screen. Owns presentation and focus; decisions stay in the controller.</summary>
public class RogueScreenView : MonoBehaviour
{
    public Text title, subtitle, footerNote, primaryLabel, secondaryLabel, overviewLabel, walletText, squadText;
    public RectTransform rowsContent, cardsContent, ownedContent;
    public ScrollRect scroll, rewardScroll;
    public Button primary, secondary, overview, rerollPaid, rerollTicket, manage;
    public Text rerollPaidLabel, rerollTicketLabel;
    public RogueOfferRowView rowTemplate;
    public RogueRewardCardView cardTemplate;
    public Image paper, titleIcon, walletIcon, primaryIcon, secondaryIcon;
    public GameObject cardsRoot, inventoryRoot;
    public CanvasGroup inputGroup;
    public FlatsDetailPanel detail;
    public Image detailIcon;
    public RectTransform buildRoot, buildContent, buildViewport;
    public Text buildHeading, buildSlots;
    public FlatsTile buildTemplate;
    public bool HasSquad { get { return squadText != null && !string.IsNullOrEmpty(squadText.text); } }
    Canvas messageCanvas; bool messageWasEnabled;
    GameObject coveredSelection, lastRunSelection, requestedCoverSelection;
    public FlatsTileBackdrop backdrop;
    public FlatsTileRunLayout layout;
    public FlatsActionBar actionBar;
    public static bool Suspended;
    [Min(.01f)] public float rewardPressSeconds = .25f;
    public bool reduceMotion;
    public float walletSeconds = .4f;
    public bool RewardFeedbackPlaying { get; private set; }
    public bool CardsMode { get; private set; }
    public bool RouteMode { get; private set; }
    public FlatsTileOffer FocusedTile { get; private set; }
    public readonly List<FlatsTileOffer> Tiles = new List<FlatsTileOffer>();
    public bool AcceptsInput { get { return !Suspended && !RewardFeedbackPlaying && !covered && !ConfirmOpen() && (Menu.current == ScreenState || Menu.current == "MainMenu" || Menu.current == ""); } }
    const string ScreenState = "RogueScreen";
    string previousState, focusKey;
    GameObject previousSelection;
    bool previousCamRotate, hudWasEnabled, covered, firstPopulation = true;
    Canvas hudCanvas;
    int focusIndex;
    float walletElapsed;
    long walletFrom, walletTo, walletShown;
    bool walletKnown;
    string acquiredKey; float acquiredUntil;
    Text[] glyphLabels; float glyphWarmUntil; Vector2 glyphScreen;
    PointerFocusPolicy focusPolicy; bool focusPolicyWasEnabled;
    static ConfirmationDialogView confirmCache; static float confirmCheckedAt = -1;

    public static RogueScreenView Open(RoguelikeController controller)
    {
        var prefab = Resources.Load<RogueScreenView>("UI/Roguelike/RogueScreen");
        var menu = GameObject.Find("Menu"); if (prefab == null || menu == null) return null;
        var view = prefab.GetComponent<Canvas>() != null ? Instantiate(prefab) : Instantiate(prefab, menu.transform, false);
        view.name = "RogueScreen"; view.Enter(); return view;
    }
    void Enter()
    {
        previousState = Menu.current; previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        previousCamRotate = FPSController.enableCamRotate; if (Menu.current == "Playing") Menu.current = ScreenState;
        focusPolicy = EventSystem.current != null ? EventSystem.current.GetComponent<PointerFocusPolicy>() : null;
        if (focusPolicy != null) { focusPolicyWasEnabled = focusPolicy.enabled; focusPolicy.enabled = false; }
        FPSController.enableCamRotate = false; FlatsCursor.Push(this);
        var hud = GameObject.Find("UI"); hudCanvas = hud != null ? hud.GetComponent<Canvas>() : null;
        if (hudCanvas != null) { hudWasEnabled = hudCanvas.enabled; hudCanvas.enabled = false; }
        var message = GameObject.Find("Message"); messageCanvas = message != null ? message.GetComponent<Canvas>() : null;
        if (messageCanvas != null) { messageWasEnabled = messageCanvas.enabled; messageCanvas.enabled = false; }
        if (manage != null) manage.onClick.AddListener(() => { if (!AcceptsInput) return; inventoryRoot.SetActive(!inventoryRoot.activeSelf); RebuildNavigation(); });
    }
    public void Close()
    {
        if (Suspended) { Destroy(gameObject); return; }
        if (Menu.current == ScreenState) Menu.current = previousState == ScreenState ? "Playing" : previousState;
        FPSController.enableCamRotate = previousCamRotate || Menu.current == "Playing";
        FlatsCursor.Pop(this);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
        if (hudCanvas != null && (hudWasEnabled || Menu.current == "Playing")) hudCanvas.enabled = true;
        if (messageCanvas != null) messageCanvas.enabled = messageWasEnabled;
        Destroy(gameObject);
    }
    void OnDestroy() { if (focusPolicy != null) focusPolicy.enabled = focusPolicyWasEnabled; FlatsCursor.Pop(this); }
    public void SetTitle(string heading, string sub) { SetTitle("Stage", heading, sub, null); }
    public void SetTitle(string iconName, string heading, string sub, string wallet)
    {
        title.text = heading; subtitle.text = sub;
        if (walletText == null) return;
        walletText.gameObject.SetActive(!string.IsNullOrEmpty(wallet) && !CardsMode);
        decimal value;
        if (!string.IsNullOrEmpty(wallet) && decimal.TryParse(wallet.TrimStart('$'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out value))
        {
            long minor = (long)Math.Round(value * 100);
            if (!walletKnown) { walletFrom = walletShown = walletTo = minor; walletKnown = true; walletElapsed = walletSeconds; }
            else if (walletTo != minor) { walletFrom = walletShown; walletTo = minor; walletElapsed = 0; }
            PaintWallet();
        }
        else walletText.text = wallet ?? "";
    }
    public void UseCards(bool cards)
    {
        if (CardsMode != cards) { firstPopulation = true; focusKey = null; focusIndex = 0; }
        CardsMode = cards; RouteMode = false;
        if (cardsRoot != null) cardsRoot.SetActive(cards);
        if (scroll != null) scroll.gameObject.SetActive(!cards);
        if (walletText != null) walletText.gameObject.SetActive(!cards);
    }
    public void SetRouteMode() { RouteMode = true; }
    public void SetSquad(string text) { if (squadText != null) squadText.text = text ?? ""; }
    public void SetBuild(Flats.Core.Roguelike.PlayerBuild build)
    {
        if (buildRoot == null || buildContent == null || buildTemplate == null || build == null) return;
        buildHeading.text = RoguelikeController.T("Current equipment");
        buildSlots.text = RoguelikeController.T("Cores {0}/{1}", build.cores.Length, Flats.Core.Roguelike.RogueCatalog.MaxCores) + " · " + RoguelikeController.T("Mods {0}/{1}", build.mods.Length, Flats.Core.Roguelike.RogueCatalog.MaxMods);
        for (int i = buildContent.childCount - 1; i >= 0; i--) { var child = buildContent.GetChild(i).gameObject; child.SetActive(false); Destroy(child); }
        var ids = new List<string>(build.cores); ids.AddRange(build.mods);
        if (!string.IsNullOrEmpty(build.tactical)) ids.Add(build.tactical);
        if (!string.IsNullOrEmpty(build.ultimate)) ids.Add(build.ultimate);
        foreach (string id in ids)
        {
            var def = Flats.Core.Roguelike.RogueCatalog.Item(id); if (def == null) continue;
            var chip = Instantiate(buildTemplate, buildContent, false); chip.gameObject.SetActive(true);
            var token = def.Kind == Flats.Core.Roguelike.ItemKind.Core ? FlatsUiTheme.Token.Core : def.Kind == Flats.Core.Roguelike.ItemKind.Mod ? FlatsUiTheme.Token.Mod : def.Kind == Flats.Core.Roguelike.ItemKind.Tactical ? FlatsUiTheme.Token.Tactical : FlatsUiTheme.Token.Ultimate;
            chip.Bind(RoguelikeController.T(def.Name), "", "", build.Tier(id) > 0 ? build.Tier(id).ToString() : "", null, token);
            chip.navigation = new Navigation { mode = Navigation.Mode.None }; chip.enabled = false;
        }
    }
    public void FocusAction() { foreach (var tile in Tiles) tile.SetFocus(false); }

    public void ClearRows()
    {
        if (FocusedTile != null) { focusKey = FocusedTile.ItemKey; focusIndex = Tiles.IndexOf(FocusedTile); }
        if (detail != null) { detail.inlineHost = null; detail.Reflow(); }
        Tiles.Clear(); FocusedTile = null;
        foreach (var parent in new[] { cardsContent, rowsContent, ownedContent })
            if (parent != null) for (int i = parent.childCount - 1; i >= 0; i--) { var go = parent.GetChild(i).gameObject; if (buildRoot != null && go == buildRoot.gameObject) continue; go.SetActive(false); Destroy(go); }
        foreach (var button in new[] { rerollPaid, rerollTicket, manage }) if (button != null) button.gameObject.SetActive(false);
        if (inventoryRoot != null) inventoryRoot.SetActive(false);
    }
    public RogueRewardCardView AddCard(string iconName, string name, string rarity, string effect, string actionText, bool interactable, string status, Action onAction)
    {
        if (cardTemplate == null || cardsContent == null) return null;
        var card = Instantiate(cardTemplate, cardsContent, false); card.name = "Reward-" + name; card.gameObject.SetActive(true);
        card.Bind(iconName, name, rarity, effect, actionText, interactable, status, () => { if (AcceptsInput) StartCoroutine(TakeFeedback(card, onAction)); }, null);
        Register(card.tile); return card;
    }
    IEnumerator TakeFeedback(RogueRewardCardView card, Action onAction)
    {
        RewardFeedbackPlaying = true;
        if (backdrop != null) { backdrop.reduceMotion = reduceMotion; backdrop.Burst(); }
        float elapsed = 0;
        while (elapsed < rewardPressSeconds)
        {
            if (card != null) card.PressFeedback(elapsed / Mathf.Max(.01f, rewardPressSeconds));
            yield return null;
            if (Suspended || covered || ConfirmOpen() || Menu.current != ScreenState && Menu.current != "MainMenu" && Menu.current != "") continue;
            elapsed += Time.unscaledDeltaTime;
        }
        RewardFeedbackPlaying = false;
        if (onAction != null) onAction();
    }
    public RogueOfferRowView AddRow(string name, string effect, string price, string rarity, string actionText, bool interactable, string status, Action onAction)
    { return AddRow("", name, effect, price, rarity, actionText, interactable, status, onAction); }
    public RogueOfferRowView AddRow(string iconName, string name, string effect, string price, string rarity, string actionText, bool interactable, string status, Action onAction)
    {
        if (iconName == "Reload" && string.IsNullOrEmpty(rarity))
        {
            bool ticket = actionText == RoguelikeController.T("Use ticket");
            Bind(ticket ? rerollTicket : rerollPaid, ticket ? rerollTicketLabel : rerollPaidLabel,
                ticket ? RoguelikeController.T("Ticket") + " · " + status : RoguelikeController.T("Reroll") + " " + price + " · " + status, onAction);
            FlatsUiTheme.SetInteractableNow(ticket ? rerollTicket : rerollPaid, interactable); return null;
        }
        bool owned = actionText == RoguelikeController.T("Remove");
        var parent = owned ? ownedContent : rowsContent;
        if (rowTemplate == null || parent == null) return null;
        var row = Instantiate(rowTemplate, parent, false); row.name = "Offer-" + name; row.gameObject.SetActive(true);
        RogueOfferRowView.Bind(row, iconName, name, effect, price, rarity, actionText, interactable, status, onAction, null);
        row.tile.owner = this; row.tile.reduceMotion = reduceMotion;
        if (owned) { manage.gameObject.SetActive(true); row.tile.SetPitch(RoguelikeController.T("Remove")); }
        else Register(row.tile);
        return row;
    }
    void Register(FlatsTileOffer tile)
    { if (tile == null) return; tile.owner = this; tile.reduceMotion = reduceMotion; Tiles.Add(tile); if (firstPopulation) tile.Enter(Tiles.Count - 1); }
    public void FinishBinding()
    {
        if (!CardsMode && !RouteMode) { int core = Tiles.FindIndex(t => t.hero); if (core > 0) { var hero = Tiles[core]; Tiles.RemoveAt(core); Tiles.Insert(0, hero); } }
        FlatsTileOffer next = !string.IsNullOrEmpty(focusKey) ? Tiles.Find(t => t.ItemKey == focusKey) : null;
        if (next == null && Tiles.Count > 0) next = Tiles[Mathf.Clamp(focusIndex, 0, Tiles.Count - 1)];
        if (next != null) Focus(next);
        if (CardsMode) Bind(primary, primaryLabel, RoguelikeController.T("Take"), () => { if (FocusedTile != null) FocusedTile.Activate(); });
        if (CardsMode && primary != null) FlatsUiTheme.SetInteractableNow(primary, next != null && next.Available);
        if (layout != null) layout.Reflow();
        Canvas.ForceUpdateCanvases(); RebuildNavigation(); firstPopulation = false;
        glyphLabels = GetComponentsInChildren<Text>(true); glyphWarmUntil = Time.unscaledTime + 1;
        if (Time.unscaledTime < acquiredUntil) foreach (var tile in Tiles) if (tile.ItemKey == acquiredKey) tile.Acquired();
        var es = EventSystem.current;
        if (es != null && AcceptsInput && !RogueInput.IsTouch && (es.currentSelectedGameObject == null || !es.currentSelectedGameObject.activeInHierarchy))
        { var first = next != null && next.Available ? (Selectable)next : primary != null && primary.gameObject.activeSelf && primary.interactable ? primary : overview; if (first != null) es.SetSelectedGameObject(first.gameObject); }
        if (es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.transform.IsChildOf(layout.footer)) FocusAction();
    }
    public void Focus(FlatsTileOffer tile)
    {
        if (tile == null) return;
        glyphWarmUntil = Time.unscaledTime + .5f;
        FocusedTile = tile;
        foreach (var other in Tiles) other.SetFocus(other == tile);
        if (detail != null)
        {
            detail.Bind((RouteMode ? tile.pitch.text + " · " : "") + tile.itemName.text + (string.IsNullOrEmpty(tile.tag.text) ? "" : " · " + tile.tag.text), RouteMode ? "" : tile.DetailNumber ?? "", tile.Description,
                (tile.DetailNext ?? "").Replace("\n", " · "), Screen.height > Screen.width && !RouteMode ? tile.inlineHost : null);
            if (detailIcon != null) { detailIcon.sprite = tile.icon.sprite; detailIcon.color = tile.Tint; detailIcon.enabled = string.IsNullOrEmpty(tile.DetailNumber) || RouteMode; }
            detail.category.color = tile.Tint;
        }
        if (CardsMode && primary != null) FlatsUiTheme.SetInteractableNow(primary, tile.Available);
        if (layout != null) layout.Reflow();
        if (detail != null) detail.category.color = tile.Tint;
    }
    public void NotifyAcquired(string id)
    { acquiredKey = id; acquiredUntil = Time.unscaledTime + .3f; foreach (var tile in Tiles) if (tile.ItemKey == id) tile.Acquired(); if (backdrop != null) { backdrop.reduceMotion = reduceMotion; backdrop.Burst(); } }
    public void SetFooter(string p, Action a, string s, Action b, string note) { SetFooter(p, "Check", a, s, "Quit", b, note); }
    public void SetFooter(string p, string pi, Action a, string s, string si, Action b, string note)
    { Bind(primary, primaryLabel, p, a); Bind(secondary, secondaryLabel, s, b); if (footerNote != null) footerNote.text = CardsMode ? "" : note ?? ""; }
    public void SetFooterInteractable(bool p, bool s) { FlatsUiTheme.SetInteractableNow(primary, p); FlatsUiTheme.SetInteractableNow(secondary, s); }
    public void SetPrimaryText(string label, string note) { if (primaryLabel != null) primaryLabel.text = label ?? ""; if (footerNote != null) footerNote.text = note ?? ""; }
    public void SetPrimaryHighlight(bool on) { /* BrandPrimary remains the action; the original label expresses readiness. */ }
    public void SetCovered(bool value)
    {
        if (value && !covered && EventSystem.current != null)
        { var selected = EventSystem.current.currentSelectedGameObject; coveredSelection = requestedCoverSelection != null ? requestedCoverSelection : selected != null && selected.transform.IsChildOf(transform) ? selected : lastRunSelection; requestedCoverSelection = null; }
        covered = value; if (paper != null) paper.gameObject.SetActive(!value);
        if (focusPolicy != null) focusPolicy.enabled = value && focusPolicyWasEnabled;
        if (!value && EventSystem.current != null && coveredSelection != null && coveredSelection.activeInHierarchy) EventSystem.current.SetSelectedGameObject(coveredSelection);
    }
    public void SetOverview(string label, Action click) { Bind(overview, overviewLabel, string.IsNullOrEmpty(label) ? null : RoguelikeController.T("Overview"), click); }
    void Bind(Button button, Text label, string text, Action action)
    {
        if (button == null) return; button.gameObject.SetActive(!string.IsNullOrEmpty(text)); if (label != null) label.text = text ?? "";
        button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => { if (!AcceptsInput) return; if (button == overview) requestedCoverSelection = button.gameObject; RogueAudio.Click(); if (action != null) action(); });
    }
    public void RebuildNavigation()
    {
        var buttons = new List<Selectable>();
        bool drawer = inventoryRoot != null && inventoryRoot.activeSelf;
        foreach (var button in GetComponentsInChildren<Button>())
            if (button.IsActive() && button.IsInteractable() && button.gameObject.activeInHierarchy && (!drawer || button.transform.IsChildOf(inventoryRoot.transform) || button.transform.IsChildOf(layout.footer))) buttons.Add(button);
        foreach (var button in buttons)
        {
            var n = new Navigation { mode = Navigation.Mode.Explicit };
            n.selectOnLeft = Neighbour(button, buttons, Vector2.left); n.selectOnRight = Neighbour(button, buttons, Vector2.right);
            n.selectOnUp = Neighbour(button, buttons, Vector2.up); n.selectOnDown = Neighbour(button, buttons, Vector2.down);
            button.navigation = n;
        }
    }
    Rect NavigationRect(Selectable target)
    {
        var rt = (RectTransform)target.transform; var corners = new Vector3[4]; rt.GetWorldCorners(corners);
        Vector2 min = transform.InverseTransformPoint(corners[0]), max = transform.InverseTransformPoint(corners[2]);
        // Footer is beyond the last logical row, even when that row must be scrolled into view.
        if (target.transform.IsChildOf(layout.footer))
        {
            float shift = 0;
            layout.footer.GetWorldCorners(corners); float footerTop = transform.InverseTransformPoint(corners[2]).y;
            foreach (var tile in Tiles) { Vector2 pos = transform.InverseTransformPoint(tile.transform.position); shift = Mathf.Min(shift, pos.y - ((RectTransform)tile.transform).rect.height - footerTop - 16); }
            min.y += shift; max.y += shift;
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    Selectable Neighbour(Selectable origin, List<Selectable> buttons, Vector2 direction)
    {
        var a = NavigationRect(origin); Selectable result = null; float best = float.PositiveInfinity;
        bool horizontal = direction.x != 0;
        foreach (var button in buttons)
        {
            if (button == origin) continue; var b = NavigationRect(button);
            Vector2 delta = b.center - a.center; float forward = Vector2.Dot(delta, direction); if (forward <= 1) continue;
            float overlap = horizontal ? Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) : Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            if (overlap <= 1) continue;
            float separation = horizontal ? (direction.x > 0 ? b.xMin - a.xMax : a.xMin - b.xMax) : (direction.y > 0 ? b.yMin - a.yMax : a.yMin - b.yMax);
            if (separation < -1) continue;
            float cross = horizontal ? Mathf.Max(0, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax)) : Mathf.Max(0, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
            float distance = horizontal ? Mathf.Max(0, direction.x > 0 ? b.xMin - a.xMax : a.xMin - b.xMax) : Mathf.Max(0, direction.y > 0 ? b.yMin - a.yMax : a.yMin - b.yMax);
            float score = cross * 1000 + distance + Mathf.Abs(horizontal ? delta.y : delta.x) * .05f;
            if (score < best) { best = score; result = button; }
        }
        return result;
    }
    static bool ConfirmOpen()
    { if (confirmCache == null || Time.unscaledTime - confirmCheckedAt > .2f) { confirmCache = FindFirstObjectByType<ConfirmationDialogView>(); confirmCheckedAt = Time.unscaledTime; } return confirmCache != null && confirmCache.gameObject.activeInHierarchy; }
    void PaintWallet()
    {
        glyphWarmUntil = Time.unscaledTime + .5f;
        string n = Flats.Core.Roguelike.RogueMoney.Format(walletShown); int dot = n.IndexOf('.'); string unit = "<size=" + FlatsUiTheme.Rogue.tileBody.size + ">";
        walletText.text = unit + "$</size>" + (dot < 0 ? n : n.Substring(0, dot) + unit + n.Substring(dot) + "</size>");
    }
    void LateUpdate()
    {
        var size = new Vector2(Screen.width, Screen.height);
        if (size != glyphScreen) { glyphScreen = size; glyphWarmUntil = Time.unscaledTime + 1; RebuildNavigation(); }
        if (Time.unscaledTime > glyphWarmUntil || glyphLabels == null) return;
        foreach (var label in glyphLabels)
            if (label != null && label.isActiveAndEnabled && label.font != null && label.font.dynamic)
                label.font.RequestCharactersInTexture(label.text + "0123456789.$ /+-", Mathf.RoundToInt(label.fontSize * label.pixelsPerUnit), label.fontStyle);
        foreach (var label in glyphLabels)
            if (label != null && label.isActiveAndEnabled) { label.cachedTextGenerator.Invalidate(); label.SetVerticesDirty(); }
    }
    void Update()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas != null) canvas.enabled = !ConfirmOpen() && (Menu.current == ScreenState || Menu.current == "MainMenu" || Menu.current == "Main" || Menu.current == "");
        if (focusPolicy != null) focusPolicy.enabled = focusPolicyWasEnabled && (covered || canvas != null && !canvas.enabled);
        if (hudCanvas != null && hudCanvas.enabled) hudCanvas.enabled = false;
        if (messageCanvas != null && messageCanvas.enabled) messageCanvas.enabled = false;
        if (inputGroup != null) { inputGroup.interactable = AcceptsInput; inputGroup.blocksRaycasts = AcceptsInput; }
        if (walletKnown && walletText != null && walletElapsed < walletSeconds)
        { walletElapsed += Time.unscaledDeltaTime; float p = Mathf.Clamp01(walletElapsed / Mathf.Max(.001f, walletSeconds)); walletShown = walletFrom + (long)Math.Round((walletTo - walletFrom) * p); PaintWallet(); walletText.rectTransform.localScale = Vector3.one * (reduceMotion ? 1 : 1 + .12f * Mathf.Sin(p * Mathf.PI)); }
        if (!AcceptsInput) return;
        if (EventSystem.current != null) { var selected = EventSystem.current.currentSelectedGameObject; if (selected != null && selected.transform.IsChildOf(transform)) lastRunSelection = selected; }
        var pad = InControl.InputManager.ActiveDevice;
        if (Input.GetKeyDown(KeyCode.Escape) || pad != null && pad.Action2.WasPressed)
        { if (inventoryRoot != null && inventoryRoot.activeSelf) { inventoryRoot.SetActive(false); RebuildNavigation(); return; } var ctrl = RoguelikeController.Instance; if (ctrl != null) ctrl.DismissScreen(); }
        var es = EventSystem.current;
        if (es != null && (es.currentSelectedGameObject == null || !es.currentSelectedGameObject.activeInHierarchy) && !RogueInput.IsTouch)
        { foreach (var tile in Tiles) if (tile.Available) { es.SetSelectedGameObject(tile.gameObject); return; } if (overview != null) es.SetSelectedGameObject(overview.gameObject); }
    }
}
