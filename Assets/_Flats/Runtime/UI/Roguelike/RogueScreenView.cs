using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The run screen (shop, reward pick, route choice, chapter end). Reference bag for the authored
/// Prefabs/UI/Roguelike/RogueScreen prefab plus the binding helpers the controller calls.
/// Opening it takes the player out of "Playing" (no shooting or movement, cursor free), closing restores it.
/// </summary>
public class RogueScreenView : MonoBehaviour
{
    public Text title, subtitle, footerNote, primaryLabel, secondaryLabel;
    public RectTransform rowsContent;
    public ScrollRect scroll;
    public Button primary, secondary;
    [Header("Overview")] public Button overview; public Text overviewLabel;   // opens the TAB panel from any run screen (touch has no TAB)
    public RogueOfferRowView rowTemplate;
    public Image paper, titleIcon, walletIcon, primaryIcon, secondaryIcon;
    public Text walletText;
    [Header("Reward cards")] public GameObject cardsRoot; public RectTransform cardsContent; public RogueRewardCardView cardTemplate;

    /// <summary>True while the TAB overview sits on top; the screen then leaves focus handling to it.</summary>
    public static bool Suspended;
    [Header("Reward take feedback")]
    [Min(0.01f)] public float rewardPressSeconds = 0.25f;
    public bool RewardFeedbackPlaying { get; private set; }
    public RogueFitToWidth paperFit;
    public float rewardDesignHeight = 660f;
    float listDesignHeight;

    const string ScreenState = "RogueScreen";
    string previousState;
    GameObject previousSelection;
    bool previousCamRotate;
    Canvas hudCanvas; bool hudWasEnabled;

    public static RogueScreenView Open(RoguelikeController controller)
    {
        var prefab = Resources.Load<GameObject>("UI/Roguelike/RogueScreen");
        if (prefab == null) { Debug.LogWarning("FLATS_ROGUE_UI missing Resources/UI/Roguelike/RogueScreen"); return null; }
        var menuObject = GameObject.Find("Menu");
        if (menuObject == null) return null;
        var go = Instantiate(prefab, menuObject.transform, false);
        go.name = "RogueScreen";
        go.transform.SetAsLastSibling();
        var view = go.GetComponent<RogueScreenView>();
        if (view == null) { Destroy(go); return null; }
        view.Enter();
        return view;
    }

    void Enter()
    {
        previousState = Menu.current;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        previousCamRotate = FPSController.enableCamRotate;
        if (Menu.current == "Playing") Menu.current = ScreenState;
        FPSController.enableCamRotate = false;
        FlatsCursor.Push(this);   // cursor free and gameplay input blocked while the screen is up (one rule for every screen)
        // the long reward note can run under the Skip button on narrow screens; text must never swallow a button's click
        if (footerNote != null) footerNote.raycastTarget = false;
        // the paper keeps its authored Roguelike theme colour (QA-36 G3: no longer tinted by the map, which gave pale pink on pale pink)
        if (rowTemplate == null)
        {
            var rowPrefab = Resources.Load<GameObject>("UI/Roguelike/RogueOfferRow");
            if (rowPrefab != null) rowTemplate = rowPrefab.GetComponent<RogueOfferRowView>();
        }
        // the gameplay HUD steps aside while the run screen is up, as it does for the pause menu
        var hudObject = GameObject.Find("UI");
        hudCanvas = hudObject != null ? hudObject.GetComponent<Canvas>() : null;
        if (hudCanvas != null) { hudWasEnabled = hudCanvas.enabled; hudCanvas.enabled = false; }
    }

    public void Close()
    {
        if (Suspended) { Destroy(gameObject); return; }   // the overview on top owns the input state and restores it when it closes
        if (Menu.current == ScreenState) Menu.current = previousState == ScreenState ? "Playing" : previousState;
        FPSController.enableCamRotate = previousCamRotate || Menu.current == "Playing";
        FlatsCursor.Pop(this);    // after the state is restored: the cursor locks again only when play resumes and nothing else is open
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
        if (hudCanvas != null && (hudWasEnabled || Menu.current == "Playing")) hudCanvas.enabled = true;
        Destroy(gameObject);
    }

    public void SetTitle(string heading, string sub) { SetTitle("Stage", heading, sub, null); }

    public void SetTitle(string iconName, string heading, string sub, string wallet)
    {
        if (title != null) title.text = heading;
        if (subtitle != null) subtitle.text = sub;
        RogueIcons.Apply(titleIcon, iconName);
        if (walletText != null) { walletText.text = wallet ?? ""; walletText.gameObject.SetActive(!string.IsNullOrEmpty(wallet)); }
        if (walletIcon != null) { RogueIcons.Apply(walletIcon, "Coin"); walletIcon.gameObject.SetActive(!string.IsNullOrEmpty(wallet)); }
    }

    /// <summary>Card layout (reward pick) or the list layout (shop, route).</summary>
    public void UseCards(bool cards)
    {
        if (paperFit != null)
        {
            if (listDesignHeight <= 0) listDesignHeight = paperFit.designHeight;
            float height = cards ? rewardDesignHeight : listDesignHeight;
            if (!Mathf.Approximately(paperFit.designHeight, height)) { paperFit.designHeight = height; paperFit.Invalidate(); }
        }
        if (cardsRoot != null) cardsRoot.SetActive(cards);
        if (scroll != null) scroll.gameObject.SetActive(!cards);
        if (cards && cardTemplate == null) { var p = Resources.Load<GameObject>("UI/Roguelike/RogueRewardCard"); if (p != null) cardTemplate = p.GetComponent<RogueRewardCardView>(); }
    }

    public RogueRewardCardView AddCard(string iconName, string name, string rarity, string effect, string actionText, bool interactable, string status, Action onAction)
    {
        if (cardTemplate == null || cardsContent == null) return null;
        var card = Instantiate(cardTemplate, cardsContent, false);
        card.gameObject.SetActive(true);
        card.name = "Card-" + name;
        card.Bind(iconName, name, rarity, effect, actionText, interactable, status,
            () => { if (!RewardFeedbackPlaying) StartCoroutine(TakeFeedback(card, onAction)); }, PlayPress);
        return card;
    }

    IEnumerator TakeFeedback(RogueRewardCardView card, Action onAction)
    {
        RewardFeedbackPlaying = true;
        foreach (var button in GetComponentsInChildren<Selectable>(true)) FlatsUiTheme.SetInteractableNow(button, false);
        float elapsed = 0;
        while (elapsed < rewardPressSeconds)
        {
            // Show the pressed pose immediately, even when the next frame exceeds the feedback duration.
            if (card != null) card.PressFeedback(0.5f + 0.5f * elapsed / Mathf.Max(0.01f, rewardPressSeconds));
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        if (card != null) card.PressFeedback(1);
        RewardFeedbackPlaying = false;
        if (onAction != null) onAction();
    }

    public void ClearRows()
    {
        if (cardsContent != null)
            for (int i = cardsContent.childCount - 1; i >= 0; i--)
            {
                var child = cardsContent.GetChild(i).gameObject;
                if (cardTemplate != null && child == cardTemplate.gameObject) continue;
                child.SetActive(false); Destroy(child);
            }
        if (rowsContent == null) return;
        for (int i = rowsContent.childCount - 1; i >= 0; i--)
        {
            var child = rowsContent.GetChild(i).gameObject;
            if (rowTemplate != null && child == rowTemplate.gameObject) continue;
            child.SetActive(false);
            Destroy(child);
        }
    }

    public RogueOfferRowView AddRow(string name, string effect, string price, string rarity, string actionText, bool interactable, string status, Action onAction)
    {
        return AddRow("", name, effect, price, rarity, actionText, interactable, status, onAction);
    }

    public RogueOfferRowView AddRow(string iconName, string name, string effect, string price, string rarity, string actionText, bool interactable, string status, Action onAction)
    {
        if (rowTemplate == null || rowsContent == null) return null;
        var row = Instantiate(rowTemplate, rowsContent, false);
        row.gameObject.SetActive(true);
        row.name = "Row-" + name;
        RogueOfferRowView.Bind(row, iconName, name, effect, price, rarity, actionText, interactable, status, onAction, PlayPress);
        return row;
    }

    public void SetFooter(string primaryText, Action onPrimary, string secondaryText, Action onSecondary, string note)
    {
        SetFooter(primaryText, "Check", onPrimary, secondaryText, "Quit", onSecondary, note);
    }

    public void SetFooter(string primaryText, string primaryIconName, Action onPrimary, string secondaryText, string secondaryIconName, Action onSecondary, string note)
    {
        Bind(primary, primaryLabel, primaryIcon, primaryText, primaryIconName, onPrimary);
        Bind(secondary, secondaryLabel, secondaryIcon, secondaryText, secondaryIconName, onSecondary);
        if (footerNote != null) footerNote.text = note ?? "";
        LayoutFooter();
    }

    /// <summary>Another full panel (the TAB overview) is drawn over this screen: its paper is hidden meanwhile, because the papers are
    /// slightly translucent and this screen's rows showed through the overview as a ghost.</summary>
    public void SetCovered(bool covered)
    {
        if (paper != null && paper.gameObject.activeSelf == covered) paper.gameObject.SetActive(!covered);
    }

    [Header("Footer layout")]
    [Tooltip("Left inset of a footer button's label while its icon shows, and without one (canvas units).")] public float labelInsetWithIcon = 36f, labelInset = 10f;
    [Tooltip("A footer button grows from its authored width up to this to fit its label on one line.")] public float footerButtonMaxWidth = 260f;
    [Tooltip("Air between the footer buttons, and between the note and the nearest button.")] public float footerGap = 12f;
    bool footerHomeKnown; Vector2 primaryHome, secondaryHome; float primaryWidth, secondaryWidth, noteLeft;

    /// <summary>The footer row from what is visible: a lone secondary button takes the primary's place at the right edge, each button is
    /// as wide as its label needs (icon included), and the note ends before the leftmost button instead of running under it.</summary>
    void LayoutFooter()
    {
        var p = primary != null ? primary.transform as RectTransform : null;
        var s = secondary != null ? secondary.transform as RectTransform : null;
        if (!footerHomeKnown)
        {
            footerHomeKnown = true;
            if (p != null) { primaryHome = p.anchoredPosition; primaryWidth = p.sizeDelta.x; }
            if (s != null) { secondaryHome = s.anchoredPosition; secondaryWidth = s.sizeDelta.x; }
            if (footerNote != null) noteLeft = footerNote.rectTransform.offsetMin.x;
        }
        bool primaryOn = p != null && p.gameObject.activeSelf, secondaryOn = s != null && s.gameObject.activeSelf;
        float right = p != null ? -primaryHome.x : 24f;   // distance of the row's right end from the paper's right edge
        float used = right;
        if (primaryOn) { float w = FitFooterButton(p, primaryLabel, primaryIcon, primaryWidth); p.anchoredPosition = primaryHome; used += w + footerGap; }
        if (secondaryOn)
        {
            float w = FitFooterButton(s, secondaryLabel, secondaryIcon, secondaryWidth);
            s.anchoredPosition = new Vector2(-used, secondaryHome.y);
            used += w + footerGap;
        }
        if (footerNote != null)
        {
            var note = footerNote.rectTransform;
            note.offsetMin = new Vector2(noteLeft + overviewGrowth, note.offsetMin.y);
            note.offsetMax = new Vector2(-used, note.offsetMax.y);
        }
    }

    [Tooltip("The Overview button grows from its authored width up to this to keep its label and key cap on one line.")] public float overviewMaxWidth = 210f;
    float overviewWidth = -1f, overviewGrowth;

    /// <summary>"Overview  [Tab]" is longer in some languages than the authored button: it widens to the label and the note moves along.</summary>
    void FitOverview()
    {
        var rt = overview != null ? overview.transform as RectTransform : null;
        if (rt == null || overviewLabel == null) return;
        if (overviewWidth < 0) overviewWidth = rt.sizeDelta.x;
        var label = overviewLabel.rectTransform;
        float padding = label.anchorMin.x != label.anchorMax.x ? label.offsetMin.x - label.offsetMax.x : 46f;
        float width = Mathf.Clamp(overviewLabel.preferredWidth + padding + 4f, overviewWidth, Mathf.Max(overviewWidth, overviewMaxWidth));
        rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
        overviewGrowth = width - overviewWidth;
        if (footerHomeKnown) LayoutFooter();
    }

    float FitFooterButton(RectTransform button, Text label, Image icon, float authoredWidth)
    {
        bool hasIcon = icon != null && icon.gameObject.activeSelf;
        float inset = hasIcon ? labelInsetWithIcon : labelInset;
        float width = authoredWidth;
        if (label != null)
        {
            var rt = label.rectTransform;
            rt.offsetMin = new Vector2(inset, rt.offsetMin.y); rt.offsetMax = new Vector2(-labelInset, rt.offsetMax.y);
            float needed = label.preferredWidth + inset + labelInset;
            width = Mathf.Clamp(needed, authoredWidth, Mathf.Max(authoredWidth, footerButtonMaxWidth));
        }
        button.sizeDelta = new Vector2(width, button.sizeDelta.y);
        return width;
    }

    /// <summary>Rewrites the primary button's label and the footer note in place (a countdown ticking once per second), keeping the
    /// button, its action and the controller focus as they are.</summary>
    public void SetPrimaryText(string label, string note)
    {
        if (primaryLabel != null && primary != null && primary.gameObject.activeSelf && primaryLabel.text != (label ?? "")) primaryLabel.text = label ?? "";
        if (footerNote != null && footerNote.text != (note ?? "")) footerNote.text = note ?? "";
        if (footerHomeKnown) LayoutFooter();
    }

    /// <summary>Footer buttons stay visible but greyed when the local player may not use them (a non-host at the chapter end).</summary>
    public void SetFooterInteractable(bool primaryOn, bool secondaryOn)
    {
        FlatsUiTheme.SetInteractableNow(primary, primaryOn);
        FlatsUiTheme.SetInteractableNow(secondary, secondaryOn);
    }

    Color primaryBaseColor; bool primaryColorKnown;
    /// <summary>Marks the primary button as a state that is on (the player is ready): the label then says what pressing it undoes.</summary>
    public void SetPrimaryHighlight(bool on)
    {
        var image = primary != null ? primary.targetGraphic as Image : null;
        if (image == null) return;
        if (!primaryColorKnown) { primaryBaseColor = image.color; primaryColorKnown = true; }
        image.color = on ? FlatsUiTheme.WithAlpha(FlatsUiTheme.Rogue.positive, primaryBaseColor.a) : primaryBaseColor;
    }

    void Bind(Button button, Text label, Image icon, string text, string iconName, Action action)
    {
        if (button == null) return;
        bool show = !string.IsNullOrEmpty(text);
        button.gameObject.SetActive(show);
        if (!show) return;
        if (label != null) label.text = text;
        if (icon != null) { RogueIcons.Apply(icon, iconName); icon.gameObject.SetActive(icon.sprite != null); }
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => { PlayPress(); if (action != null) action(); });
    }

    // polled every frame by Update: the scene search is repeated at most five times a second
    static ConfirmationDialogView confirmCache; static float confirmCheckedAt = -1f;
    static bool confirmOpen()
    {
        if (confirmCache == null || Time.unscaledTime - confirmCheckedAt > 0.2f) { confirmCache = FindObjectOfType<ConfirmationDialogView>(); confirmCheckedAt = Time.unscaledTime; }
        return confirmCache != null && confirmCache.gameObject.activeInHierarchy;
    }

    static void PlayPress() { RogueAudio.Click(); }

    /// <summary>Binds the Overview button; an empty label hides it.</summary>
    public void SetOverview(string label, Action onClick)
    {
        if (overview == null) return;
        bool show = !string.IsNullOrEmpty(label);
        if (overview.gameObject.activeSelf != show) overview.gameObject.SetActive(show);
        if (!show) return;
        if (overviewLabel != null) overviewLabel.text = label;
        FitOverview();
        overview.onClick.RemoveAllListeners();
        overview.onClick.AddListener(() => { PlayPress(); if (onClick != null) onClick(); });
    }

    void Update()
    {
        // Menu.Start re-enables the HUD canvas about a second after a scene loads; the screen stays on top until it closes.
        if (hudCanvas != null && hudCanvas.enabled) hudCanvas.enabled = false;
        if (Suspended || RewardFeedbackPlaying) return;   // the TAB overview owns input and focus while it is open
        // Escape / pad Cancel steps out of the shop during Prep (reopen with Interact); the pause menu is reachable from there.
        var pad = InControl.InputManager.ActiveDevice;
        if (Input.GetKeyDown(KeyCode.Escape) || (pad != null && pad.Action2.WasPressed))
        {
            var ctrl = RoguelikeController.Instance;
            if (ctrl != null && !confirmOpen()) { ctrl.DismissScreen(); return; }
        }
        // Keep controller focus inside the screen; a stray click elsewhere must not strand the pad.
        if (EventSystem.current == null || confirmOpen()) return;
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected != null && selected.transform.IsChildOf(transform)) return;
        if (primary != null && primary.gameObject.activeInHierarchy && primary.interactable) { EventSystem.current.SetSelectedGameObject(primary.gameObject); return; }
        if (cardsRoot != null && cardsRoot.activeSelf && cardsContent != null)
            for (int i = 0; i < cardsContent.childCount; i++)
            {
                var card = cardsContent.GetChild(i).GetComponent<RogueRewardCardView>();
                if (card != null && card.gameObject.activeSelf && card.action != null && card.action.interactable) { EventSystem.current.SetSelectedGameObject(card.action.gameObject); return; }
            }
        if (rowsContent != null)
            for (int i = 0; i < rowsContent.childCount; i++)
            {
                var row = rowsContent.GetChild(i).GetComponent<RogueOfferRowView>();
                if (row != null && row.gameObject.activeSelf && row.action != null && row.action.interactable) { EventSystem.current.SetSelectedGameObject(row.action.gameObject); return; }
            }
    }
}
