using System;
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
        card.Bind(iconName, name, rarity, effect, actionText, interactable, status, onAction, PlayPress);
        return card;
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
    }

    /// <summary>Rewrites the primary button's label and the footer note in place (a countdown ticking once per second), keeping the
    /// button, its action and the controller focus as they are.</summary>
    public void SetPrimaryText(string label, string note)
    {
        if (primaryLabel != null && primary != null && primary.gameObject.activeSelf && primaryLabel.text != (label ?? "")) primaryLabel.text = label ?? "";
        if (footerNote != null && footerNote.text != (note ?? "")) footerNote.text = note ?? "";
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
        overview.onClick.RemoveAllListeners();
        overview.onClick.AddListener(() => { PlayPress(); if (onClick != null) onClick(); });
    }

    void Update()
    {
        // Menu.Start re-enables the HUD canvas about a second after a scene loads; the screen stays on top until it closes.
        if (hudCanvas != null && hudCanvas.enabled) hudCanvas.enabled = false;
        if (Suspended) return;   // the TAB overview owns input and focus while it is open
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
