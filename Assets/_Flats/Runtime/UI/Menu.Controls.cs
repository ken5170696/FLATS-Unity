using System;
using System.Collections.Generic;
using InControl;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class Menu
{
    GameObject bindingsPanel;
    readonly List<GameObject> controlOptions = new List<GameObject>();
    readonly List<Button> bindingRows = new List<Button>();
    readonly List<Text> bindingLabels = new List<Text>();
    readonly List<Text> bindingDetails = new List<Text>();
    readonly List<Button> keyboardRows = new List<Button>();
    readonly List<Button> controllerRows = new List<Button>();
    readonly List<Text> keyboardLabels = new List<Text>();
    readonly List<Text> controllerLabels = new List<Text>();
    readonly List<Text> keyboardDetails = new List<Text>();
    readonly List<Text> controllerDetails = new List<Text>();
    // Keyboard and controller bindings are two scrolling lists in SettingsScreen → Control → Bindings.
    ScrollRect keyboardList, controllerList;
    Text controlCategoryLabel;
    int controlCategory;
    Text bindingStatus;
    bool bindingPad;
    string captureAction;
    float captureStarted;
    bool captureReady;
    bool releasePending;
    InputDevice captureDevice;
    string bindingLanguage;
    int suppressControlFrame;
    bool restoreStandalone, restoreInControl;
    Button captureButton;
    static readonly KeyCode[] captureKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));

    void InitializeControls()
    {
        if (bindingsPanel != null) return;
        var control = settingsScreen.GetChild(2);
        // All layout, fonts, materials and animation are authored in SettingsScreen.prefab.
        // General-setting rows are the Control children with Plus/Minus buttons.
        foreach (Transform child in control) if (child.Find("Plus") != null) controlOptions.Add(child.gameObject);
        bindingsPanel = control.Find("Bindings").gameObject;
        controlCategoryLabel = control.Find("ControlType").GetComponent<Text>();
        control.Find("PreviousControlType").GetComponent<Button>().onClick.AddListener(() => ChangeControlCategory(-1));
        control.Find("NextControlType").GetComponent<Button>().onClick.AddListener(() => ChangeControlCategory(1));
        keyboardList = bindingsPanel.transform.Find("KeyboardList").GetComponent<ScrollRect>();
        controllerList = bindingsPanel.transform.Find("ControllerList").GetComponent<ScrollRect>();
        for (int i = 0; i < FlatsControls.KeyboardActions.Length; i++) BindAuthoredRow(keyboardRows, keyboardLabels, keyboardDetails, i, keyboardList);
        for (int i = 0; i < FlatsControls.PadActions.Length; i++) BindAuthoredRow(controllerRows, controllerLabels, controllerDetails, i, controllerList);
        bindingStatus = bindingsPanel.transform.Find("Status").GetComponent<Text>();
        bindingsPanel.transform.Find("ResetBindings").GetComponent<Button>().onClick.AddListener(() =>
        { FlatsControls.ResetBindings(bindingPad); RefreshBindings(); bindingStatus.text = "Default bindings restored."; });
        ShowBindings(!Application.isMobilePlatform, false);
        RefreshPersonalRows();
        // Language can also change outside this page, for example by importing a save.
        FlatsLocalization.Changed += RefreshPersonalRows;
    }
    // Authored settings rows (Index/Count/Plus/Minus) whose Plus and Minus call PlusMinus.
    // They are looked up by name so they can be placed on any Settings page.
    Transform SettingsRow(string name)
    {
        foreach (var child in settingsScreen.GetComponentsInChildren<Transform>(true))
            if (child.name == name && child.Find("Plus") != null) return child;
        return null;
    }
    void RefreshPersonalRows()
    {
        var language = SettingsRow("Language");
        if (language != null)
        {
            var value = language.GetChild(1).GetComponent<Text>();
            value.text = FlatsLocalization.IsChinese ? "中文" : "English";
            value.font = FlatsLocalization.ChineseFont;
        }
        var aim = SettingsRow("AimMode");
        if (aim != null) aim.GetChild(1).GetComponent<Text>().text = FlatsControls.HoldToAim ? "Hold" : "Toggle";
        var aimSensitivity = SettingsRow("AimSensitivity");
        if (aimSensitivity != null) aimSensitivity.GetChild(1).GetComponent<Text>().text = FlatsControls.AimSensitivityNames[FlatsControls.AimSensitivityIndex];
        var killCinematic = SettingsRow("KillCinematic");
        if (killCinematic != null) killCinematic.GetChild(1).GetComponent<Text>().text = FlatsControls.KillCinematic ? "ON" : "OFF";
    }
    // The contextual touch Interact button follows the action buttons to the same side.
    static void SetInteractAnchor(Transform hud, ETCBase.RectAnchor anchor)
    {
        var interact = hud.Find("Interact");
        if (interact != null) interact.GetComponent<ETCButton>().anchor = anchor;
    }
    bool ChangePersonalRow(Transform row, int direction)
    {
        if (row.name == "Language")
        {
            FlatsLocalization.SetLanguage(FlatsLocalization.IsChinese ? "en" : "zh-Hant");
            RefreshLanguageButton();
        }
        else if (row.name == "AimMode") FlatsControls.HoldToAim = !FlatsControls.HoldToAim;
        else if (row.name == "AimSensitivity") FlatsControls.AimSensitivityIndex += direction;
        else if (row.name == "KillCinematic") FlatsControls.KillCinematic = !FlatsControls.KillCinematic;
        else return false;
        RefreshPersonalRows();
        return true;
    }
    void BindAuthoredRow(List<Button> rows, List<Text> labels, List<Text> details, int actionIndex, ScrollRect list)
    {
        var row = list.content.Find("Binding" + actionIndex + "/Button");
        var button = row.GetComponent<Button>();
        rows.Add(button);
        labels.Add(row.Find("Action").GetComponent<Text>());
        details.Add(row.Find("Detail").GetComponent<Text>());
        int captured = actionIndex;
        button.onClick.AddListener(() => BeginBinding(captured));
    }

    void ShowBindings(bool show, bool pad)
    {
        if (FlatsControls.Capturing) return;
        bindingPad = pad;
        bindingRows.Clear(); bindingLabels.Clear(); bindingDetails.Clear();
        bindingRows.AddRange(pad ? controllerRows : keyboardRows);
        bindingLabels.AddRange(pad ? controllerLabels : keyboardLabels);
        bindingDetails.AddRange(pad ? controllerDetails : keyboardDetails);
        foreach (var child in controlOptions) child.SetActive(!show);
        bindingsPanel.SetActive(show);
        controlCategory = !show ? 0 : pad ? 2 : 1;
        controlCategoryLabel.text = !show ? "General" : pad ? "Controller" : "Keyboard / Mouse";
        if (show) RefreshBindings();
    }

    void ChangeControlCategory(int direction)
    {
        if (FlatsControls.Capturing || releasePending) return;
        int category = (controlCategory + direction + 3) % 3;
        ShowBindings(category != 0, category == 2);
    }

    string BindingAction(int index) => (bindingPad ? FlatsControls.PadActions : FlatsControls.KeyboardActions)[index];
    static string ActionName(string action, bool pad)
    {
        if (action == "Change") return pad ? "Change / hold to pick up" : "Change weapon";
        if (action == "Jump" && pad) return "Jump / hold to sprint";
        if (action == "Scope") return "Toggle aim";
        if (action == "Interact") return "Pick up / exchange";
        if (action == "Left") return "Move left";
        if (action == "Right") return "Move right";
        return action;
    }
    void RefreshBindings()
    {
        bindingLanguage = FlatsLocalization.Language;
        int count = (bindingPad ? FlatsControls.PadActions : FlatsControls.KeyboardActions).Length;
        for (int i = 0; i < bindingRows.Count; i++)
        {
            string action = BindingAction(i);
            bindingRows[i].transform.Find("Label").GetComponent<Text>().text = FlatsControls.Label(action, bindingPad);
            bindingLabels[i].text = ActionName(action, false);
            bindingDetails[i].text = bindingPad && action == "Jump" ? "Hold to sprint" : bindingPad && action == "Change" ? "Hold to pick up" :
                !bindingPad && action == "Aim" ? (FlatsControls.HoldToAim ? "Hold to aim" : "Press to toggle") : "";
        }
        keyboardList.gameObject.SetActive(!bindingPad && bindingsPanel.activeSelf);
        controllerList.gameObject.SetActive(bindingPad && bindingsPanel.activeSelf);
        bindingStatus.text = "Select a binding to change it.";
    }
    void BeginBinding(int index)
    {
        if (captureAction != null) return;
        if (bindingPad && InputManager.Devices.Count == 0)
        { bindingStatus.text = "Connect a controller to bind its buttons."; return; }
        captureAction = BindingAction(index);
        captureButton = bindingRows[index];
        captureStarted = Time.unscaledTime;
        captureReady = false;
        captureDevice = InputManager.ActiveDevice;
        FlatsControls.Capturing = true;
        restoreStandalone = standaloneModule.enabled;
        restoreInControl = inControlModule.enabled;
        standaloneModule.enabled = inControlModule.enabled = false;
        captureButton.transform.Find("Label").GetComponent<Text>().text = "Press a button...";
        bindingStatus.text = "Release, then press a new binding. Esc / Start cancels.";
    }
    void FinishBinding(string message)
    {
        captureAction = null;
        FlatsControls.Capturing = false;
        suppressControlFrame = Time.frameCount + 1;
        releasePending = true;
        RefreshBindings(); bindingStatus.text = message;
        if (captureButton != null) EventSystem.current.SetSelectedGameObject(captureButton.gameObject);
    }
    bool TickBindingCapture()
    {
        if (releasePending)
        {
            if (!Input.anyKey && !InputManager.ActiveDevice.AnyButtonIsPressed && !InputManager.ActiveDevice.CommandIsPressed)
            {
                releasePending = false;
                standaloneModule.enabled = restoreStandalone;
                inControlModule.enabled = restoreInControl;
                suppressControlFrame = Time.frameCount + 1;
            }
            return true;
        }
        if (bindingsPanel != null && bindingsPanel.activeInHierarchy && bindingLanguage != FlatsLocalization.Language) RefreshBindings();
        if (captureAction == null) return Time.frameCount <= suppressControlFrame;
        if (!bindingsPanel.activeInHierarchy || (bindingPad && captureDevice != InputDevice.Null && !captureDevice.IsAttached) || !Application.isFocused || Input.GetKeyDown(KeyCode.Escape) || InputManager.ActiveDevice.CommandWasPressed || Time.unscaledTime - captureStarted > 10)
        { FinishBinding("Binding cancelled. Previous binding kept."); return true; }
        if (!captureReady)
        {
            captureReady = Time.unscaledTime - captureStarted > .2f && !Input.anyKey && !InputManager.ActiveDevice.AnyButtonIsPressed && InputManager.ActiveDevice.LeftTrigger < .2f && InputManager.ActiveDevice.RightTrigger < .2f;
            return true;
        }
        string value = null;
        if (bindingPad)
        {
            foreach (var button in FlatsControls.PadButtons)
                if (InputManager.ActiveDevice.GetControl(button).WasPressed) { value = button.ToString(); break; }
        }
        else foreach (var key in captureKeys)
            if (FlatsControls.ValidKey(key) && Input.GetKeyDown(key)) { value = key.ToString(); break; }
        if (value != null)
        {
            bool saved = FlatsControls.Bind(captureAction, value, bindingPad, out string conflict);
            FinishBinding(saved ? "Binding saved." : FlatsLocalization.Translate("Already assigned to: ") + FlatsLocalization.Translate(ActionName(conflict ?? captureAction, bindingPad)));
        }
        return true;
    }
    bool HandleControlNavigation(int button)
    {
        if (FlatsControls.Capturing || releasePending || Time.frameCount <= suppressControlFrame) return true;
        // The old desktop/controller diagram now has one entry point under Control.
        if (current == "Settings" && button == 4 && !Application.isMobilePlatform) return true;
        return false;
    }
    void RefreshControlTile()
    {
        tileArtwork.SetDesktopSettingsLayout(!Application.isMobilePlatform && current == "Settings");
        // Only suppress the obsolete settings entry. The menu Animator owns
        // visibility everywhere else, including title, detail and gameplay.
        // Forcing this shared slot on resurrected Leaderboard over the HUD.
        if (!Application.isMobilePlatform && current == "Settings")
            buttons[4].transform.parent.gameObject.SetActive(false);
    }
}
