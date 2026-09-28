using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The TAB overview: a tabbed panel (Shop, Player, Squad, Weapons, Run) over the game. Reference bag for the
/// authored Resources/UI/Roguelike/RogueOverview prefab. Tabs are authored buttons; content rows instantiate the
/// authored RogueStatRow / RogueOfferRow templates. The controller fills it; every action is a command.
/// </summary>
public class RogueOverviewView : MonoBehaviour
{
    [Serializable] public class Tab { public Button button; public Image back, icon; public Text label; public string iconName; public string key; }
    public Tab[] tabs = new Tab[0];
    public Text title, subtitle, footer, closeLabel;
    public Image titleIcon, paper;
    public RectTransform rowsContent;
    public ScrollRect scroll;
    public Button close;
    public RogueStatRowView statTemplate;
    public RogueOfferRowView offerTemplate;
    public Color tabActive = new Color(0.8f, 0.098f, 0.4f, 1f), tabIdle = new Color(1f, 1f, 1f, 0.35f), tabTextActive = Color.white, tabTextIdle = new Color(0.2f, 0.2f, 0.2f, 1f);

    public int Current { get; private set; }
    public event Action<int> TabChanged;
    const string ScreenState = "RogueScreen";
    string previousState; bool previousCamRotate; GameObject previousSelection;
    Canvas hudCanvas; bool hudWasEnabled;

    public static RogueOverviewView Open()
    {
        var prefab = Resources.Load<GameObject>("UI/Roguelike/RogueOverview");
        var menuObject = GameObject.Find("Menu");
        if (prefab == null || menuObject == null) { Debug.LogWarning("FLATS_ROGUE_UI missing Resources/UI/Roguelike/RogueOverview"); return null; }
        var go = Instantiate(prefab, menuObject.transform, false);
        go.name = "RogueOverview";
        go.transform.SetAsLastSibling();
        var view = go.GetComponent<RogueOverviewView>();
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
        UnityEngine.Cursor.lockState = CursorLockMode.None; UnityEngine.Cursor.visible = true;
        RogueScreenView.Suspended = true;
        var menu = Menu.Current;
        if (paper != null && menu != null) { var tint = menu.RogueThemeTint(); var light = Color.Lerp(new Color(tint.r, tint.g, tint.b), Color.white, 0.55f); paper.color = new Color(light.r, light.g, light.b, 1f); }
        if (statTemplate == null) { var p = Resources.Load<GameObject>("UI/Roguelike/RogueStatRow"); if (p != null) statTemplate = p.GetComponent<RogueStatRowView>(); }
        if (offerTemplate == null) { var p = Resources.Load<GameObject>("UI/Roguelike/RogueOfferRow"); if (p != null) offerTemplate = p.GetComponent<RogueOfferRowView>(); }
        var hudObject = GameObject.Find("UI");
        hudCanvas = hudObject != null ? hudObject.GetComponent<Canvas>() : null;
        if (hudCanvas != null) { hudWasEnabled = hudCanvas.enabled; hudCanvas.enabled = false; }
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i; var tab = tabs[i];
            if (tab == null || tab.button == null) continue;
            RogueIcons.Apply(tab.icon, tab.iconName);
            tab.button.onClick.RemoveAllListeners();
            tab.button.onClick.AddListener(() => { PlayPress(); Select(index); });
        }
        if (close != null) { close.onClick.RemoveAllListeners(); close.onClick.AddListener(() => { PlayPress(); var c = RoguelikeController.Instance; if (c != null) c.CloseOverview(); else Close(); }); }
        PaintTabs();
    }

    public void Close()
    {
        RogueScreenView.Suspended = false;
        if (Menu.current == ScreenState) Menu.current = previousState == ScreenState ? "Playing" : previousState;
        FPSController.enableCamRotate = previousCamRotate || Menu.current == "Playing";
        if (Menu.current == "Playing") { UnityEngine.Cursor.lockState = CursorLockMode.Locked; UnityEngine.Cursor.visible = false; }
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
        if (hudCanvas != null && (hudWasEnabled || Menu.current == "Playing")) hudCanvas.enabled = true;
        Destroy(gameObject);
    }

    public void Select(int index)
    {
        if (tabs.Length == 0) return;
        Current = (index % tabs.Length + tabs.Length) % tabs.Length;
        PaintTabs();
        if (EventSystem.current != null && tabs[Current] != null && tabs[Current].button != null) EventSystem.current.SetSelectedGameObject(tabs[Current].button.gameObject);
        if (TabChanged != null) TabChanged(Current);
    }

    void PaintTabs()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            var tab = tabs[i]; if (tab == null) continue;
            bool active = i == Current;
            if (tab.back != null) tab.back.color = active ? tabActive : tabIdle;
            if (tab.label != null) tab.label.color = active ? tabTextActive : tabTextIdle;
            if (tab.icon != null) tab.icon.color = active ? tabTextActive : tabTextIdle;
        }
    }

    public void SetHeader(string iconName, string heading, string sub)
    {
        RogueIcons.Apply(titleIcon, iconName);
        if (title != null) title.text = heading ?? "";
        if (subtitle != null) subtitle.text = sub ?? "";
    }

    public void SetFooter(string text) { if (footer != null) footer.text = text ?? ""; }

    public void ClearRows()
    {
        if (rowsContent == null) return;
        for (int i = rowsContent.childCount - 1; i >= 0; i--)
        {
            var child = rowsContent.GetChild(i).gameObject;
            if ((statTemplate != null && child == statTemplate.gameObject) || (offerTemplate != null && child == offerTemplate.gameObject)) continue;
            child.SetActive(false);
            Destroy(child);
        }
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    /// <summary>bar &lt; 0 hides the bar. A null value keeps the row as a plain heading line.</summary>
    public RogueStatRowView AddStat(string iconName, string label, string value, string sub, float bar, Color tint)
    {
        if (statTemplate == null || rowsContent == null) return null;
        var row = Instantiate(statTemplate, rowsContent, false);
        row.gameObject.SetActive(true);
        row.name = "Stat-" + label;
        row.Bind(iconName, label, value, sub, bar, tint);
        return row;
    }

    public RogueOfferRowView AddOffer(string iconName, string name, string effect, string price, string rarity, string actionText, bool interactable, string status, Action onAction)
    {
        if (offerTemplate == null || rowsContent == null) return null;
        var row = Instantiate(offerTemplate, rowsContent, false);
        row.gameObject.SetActive(true);
        row.name = "Offer-" + name;
        RogueOfferRowView.Bind(row, iconName, name, effect, price, rarity, actionText, interactable, status, onAction, PlayPress);
        return row;
    }

    static bool confirmOpen() { var v = FindObjectOfType<ConfirmationDialogView>(); return v != null && v.gameObject.activeInHierarchy; }

    static void PlayPress()
    {
        var menu = Menu.Current;
        if (menu != null && menu.pressSE != null) { var src = menu.GetComponent<AudioSource>(); if (src != null) src.PlayOneShot(menu.pressSE); }
    }

    void Update()
    {
        if (hudCanvas != null && hudCanvas.enabled) hudCanvas.enabled = false;
        if (confirmOpen()) return;
        var pad = InControl.InputManager.ActiveDevice;
        // Q/E or the pad bumpers cycle tabs; TAB and Escape close (the controller owns the toggle so it can refresh state).
        if (Input.GetKeyDown(KeyCode.Q) || (pad != null && pad.LeftBumper.WasPressed)) Select(Current - 1);
        else if (Input.GetKeyDown(KeyCode.E) || (pad != null && pad.RightBumper.WasPressed)) Select(Current + 1);
        for (int i = 0; i < tabs.Length && i < 9; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Select(i);
        if (Input.GetKeyDown(KeyCode.Escape) || (pad != null && pad.Action2.WasPressed))
        {
            var ctrl = RoguelikeController.Instance;
            if (ctrl != null) ctrl.CloseOverview(); else Close();
            return;
        }
        if (EventSystem.current == null) return;
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected != null && selected.transform.IsChildOf(transform)) return;
        if (tabs.Length > Current && tabs[Current] != null && tabs[Current].button != null) EventSystem.current.SetSelectedGameObject(tabs[Current].button.gameObject);
    }
}
