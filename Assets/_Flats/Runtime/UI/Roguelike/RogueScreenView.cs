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
    public RogueOfferRowView rowTemplate;
    public Image paper;

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
        UnityEngine.Cursor.lockState = CursorLockMode.None; UnityEngine.Cursor.visible = true;
        var menu = Menu.Current;
        if (paper != null && menu != null) { var tint = menu.RogueThemeTint(); var light = Color.Lerp(new Color(tint.r, tint.g, tint.b), Color.white, 0.55f); paper.color = new Color(light.r, light.g, light.b, 1f); }
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
        if (Menu.current == ScreenState) Menu.current = previousState == ScreenState ? "Playing" : previousState;
        FPSController.enableCamRotate = previousCamRotate || Menu.current == "Playing";
        if (Menu.current == "Playing") { UnityEngine.Cursor.lockState = CursorLockMode.Locked; UnityEngine.Cursor.visible = false; }
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
        if (hudCanvas != null && hudWasEnabled) hudCanvas.enabled = true;
        Destroy(gameObject);
    }

    public void SetTitle(string heading, string sub)
    {
        if (title != null) title.text = heading;
        if (subtitle != null) subtitle.text = sub;
    }

    public void ClearRows()
    {
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
        if (rowTemplate == null || rowsContent == null) return null;
        var row = Instantiate(rowTemplate, rowsContent, false);
        row.gameObject.SetActive(true);
        row.name = "Row-" + name;
        if (row.title != null) row.title.text = name;
        if (row.effect != null) row.effect.text = effect;
        if (row.price != null) row.price.text = price;
        if (row.rarity != null) row.rarity.text = rarity;
        if (row.status != null) row.status.text = status;
        if (row.actionLabel != null) row.actionLabel.text = actionText;
        if (row.action != null)
        {
            row.action.gameObject.SetActive(!string.IsNullOrEmpty(actionText));
            row.action.interactable = interactable;
            row.action.onClick.RemoveAllListeners();
            row.action.onClick.AddListener(() => { PlayPress(); if (onAction != null) onAction(); });
        }
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && interactable && row.action != null)
            EventSystem.current.SetSelectedGameObject(row.action.gameObject);
        return row;
    }

    public void SetFooter(string primaryText, Action onPrimary, string secondaryText, Action onSecondary, string note)
    {
        Bind(primary, primaryLabel, primaryText, onPrimary);
        Bind(secondary, secondaryLabel, secondaryText, onSecondary);
        if (footerNote != null) footerNote.text = note ?? "";
    }

    void Bind(Button button, Text label, string text, Action action)
    {
        if (button == null) return;
        bool show = !string.IsNullOrEmpty(text);
        button.gameObject.SetActive(show);
        if (!show) return;
        if (label != null) label.text = text;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => { PlayPress(); if (action != null) action(); });
    }

    static void PlayPress()
    {
        var menu = Menu.Current;
        if (menu != null && menu.pressSE != null) { var src = menu.GetComponent<AudioSource>(); if (src != null) src.PlayOneShot(menu.pressSE); }
    }

    void Update()
    {
        // Menu.Start re-enables the HUD canvas about a second after a scene loads; the screen stays on top until it closes.
        if (hudCanvas != null && hudCanvas.enabled) hudCanvas.enabled = false;
        // Keep controller focus inside the screen; a stray click elsewhere must not strand the pad.
        if (EventSystem.current == null) return;
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected != null && selected.transform.IsChildOf(transform)) return;
        if (primary != null && primary.gameObject.activeInHierarchy && primary.interactable) { EventSystem.current.SetSelectedGameObject(primary.gameObject); return; }
        if (rowsContent != null)
            for (int i = 0; i < rowsContent.childCount; i++)
            {
                var row = rowsContent.GetChild(i).GetComponent<RogueOfferRowView>();
                if (row != null && row.gameObject.activeSelf && row.action != null && row.action.interactable) { EventSystem.current.SetSelectedGameObject(row.action.gameObject); return; }
            }
    }
}
