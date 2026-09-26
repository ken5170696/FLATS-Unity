using System.Linq;
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
    GameObject controllerPreview;
    FlatsGamepad.Style bindingStyle;
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
        var preview = bindingsPanel.transform.Find("ControllerPreview");
        controllerPreview = preview != null ? preview.gameObject : null;
        for (int i = 0; i < FlatsControls.KeyboardActions.Length; i++) BindAuthoredRow(keyboardRows, keyboardLabels, keyboardDetails, i, keyboardList);
        for (int i = 0; i < FlatsControls.PadActions.Length; i++) BindAuthoredRow(controllerRows, controllerLabels, controllerDetails, i, controllerList);
        bindingStatus = bindingsPanel.transform.Find("Status").GetComponent<Text>();
        bindingsPanel.transform.Find("ResetBindings").GetComponent<Button>().onClick.AddListener(() =>
        {
            FlatsControls.ResetBindings(bindingPad);
            if (bindingPad) FlatsGamepad.Reset();
            RefreshBindings(); RefreshPersonalRows();
            bindingStatus.text = bindingPad ? "Default controller settings restored." : "Default bindings restored.";
        });
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
        SetRowValue("PadPreset", FlatsGamepad.CurrentPreset() < 0 ? "Custom" : FlatsGamepad.PresetNames[FlatsGamepad.CurrentPreset()]);
        SetRowValue("PadLookH", FlatsGamepad.SpeedLabel(FlatsGamepad.LookHIndex));
        SetRowValue("PadLookV", FlatsGamepad.SpeedLabel(FlatsGamepad.LookVIndex));
        SetRowValue("PadCurve", FlatsGamepad.Curves[FlatsGamepad.CurveIndex]);
        SetRowValue("PadLookDeadzone", FlatsGamepad.DeadzoneLabel(FlatsGamepad.LookDeadzoneIndex));
        SetRowValue("PadMoveDeadzone", FlatsGamepad.DeadzoneLabel(FlatsGamepad.MoveDeadzoneIndex));
        SetRowValue("PadVibration", FlatsGamepad.VibrationLabel(FlatsGamepad.VibrationIndex));
    }
    void SetRowValue(string row, string value)
    {
        var t = SettingsRow(row);
        if (t != null) t.GetChild(1).GetComponent<Text>().text = value;
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
        else if (row.name == "PadPreset")
        {
            int count = FlatsGamepad.PresetNames.Length, current = FlatsGamepad.CurrentPreset();
            FlatsGamepad.ApplyPreset(current < 0 ? (direction > 0 ? 0 : count - 1) : (current + direction + count) % count);
            if (bindingsPanel != null && bindingsPanel.activeSelf) RefreshBindings();
        }
        else if (row.name == "PadLookH") FlatsGamepad.LookHIndex += direction;
        else if (row.name == "PadLookV") FlatsGamepad.LookVIndex += direction;
        else if (row.name == "PadCurve") FlatsGamepad.CurveIndex += direction;
        else if (row.name == "PadLookDeadzone") FlatsGamepad.LookDeadzoneIndex += direction;
        else if (row.name == "PadMoveDeadzone") FlatsGamepad.MoveDeadzoneIndex += direction;
        else if (row.name == "PadVibration") FlatsGamepad.VibrationIndex += direction;
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

    // A controller player opening a Settings page lands on its first option rather than
    // on Back; the Control page opens on the controller list.
    void FocusDetailForController()
    {
        if (currentDetail == null || Input.GetJoystickNames().Length == 0 || PointerFocusPolicy.PointerActive) return;
        if (current == "Settings" && currentDetail.transform == settingsScreen.GetChild(2) && bindingsPanel != null && bindingsPanel.activeSelf)
        {
            ShowBindings(true, true);
            if (controllerRows.Count > 0) { EventSystem.current.SetSelectedGameObject(controllerRows[0].gameObject); return; }
        }
        foreach (var selectable in currentDetail.GetComponentsInChildren<Selectable>())
        {
            if (selectable is Scrollbar || !selectable.IsInteractable()) continue;
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            return;
        }
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
            bindingDetails[i].text = bindingPad && action == "Sprint" ? "Click to toggle" : bindingPad && action == "Change" ? "Hold to pick up" :
                !bindingPad && action == "Aim" ? (FlatsControls.HoldToAim ? "Hold to aim" : "Press to toggle") : "";
            // The action name fills the row height: centred alone, at the top above a hint line.
            bindingLabels[i].alignment = bindingDetails[i].text.Length > 0 ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
        }
        keyboardList.gameObject.SetActive(!bindingPad && bindingsPanel.activeSelf);
        controllerList.gameObject.SetActive(bindingPad && bindingsPanel.activeSelf);
        if (controllerPreview != null) controllerPreview.SetActive(bindingPad && bindingsPanel.activeSelf);
        bindingStyle = FlatsGamepad.DeviceStyle(InputManager.ActiveDevice);
        bindingStatus.text = bindingPad && Input.GetJoystickNames().Length > 0
            ? FlatsGamepad.Glyph(InputControlType.LeftBumper, bindingStyle) + " / " + FlatsGamepad.Glyph(InputControlType.RightBumper, bindingStyle) + ": next section. Select an option to change it."
            : "Select a binding to change it.";
    }
    void BeginBinding(int index)
    {
        // A capture that just ended waits for its button to be released. Starting another
        // one then re-captured the row at once and recorded the disabled input modules as
        // the state to restore, which left the menu without input.
        if (captureAction != null || releasePending || Time.frameCount <= suppressControlFrame) return;
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
        // The EventSystem keeps processing its last module even when every module is
        // disabled, and that module no longer updates its button state: the A press that
        // opened capture kept submitting to this row every frame. Stop navigation and
        // submit events until the capture's buttons are released.
        EventSystem.current.sendNavigationEvents = false;
        captureButton.transform.Find("Label").GetComponent<Text>().text = FlatsLocalization.Translate("Press a button...");
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
            if (!Input.anyKey && InputManager.Devices.All(pad => !pad.AnyButtonIsPressed && !pad.CommandIsPressed))
            {
                releasePending = false;
                standaloneModule.enabled = restoreStandalone;
                inControlModule.enabled = restoreInControl;
                EventSystem.current.sendNavigationEvents = true;
                suppressControlFrame = Time.frameCount + 1;
            }
            return true;
        }
        if (bindingsPanel != null && bindingsPanel.activeInHierarchy &&
            (bindingLanguage != FlatsLocalization.Language || (bindingPad && bindingStyle != FlatsGamepad.DeviceStyle(InputManager.ActiveDevice)))) RefreshBindings();
        if (captureAction == null)
        {
            if (bindingPad && controllerList != null && controllerList.gameObject.activeInHierarchy)
            {
                var device = InputManager.ActiveDevice;
                if (device.LeftBumper.WasPressed) JumpSection(-1);
                else if (device.RightBumper.WasPressed) JumpSection(1);
            }
            return Time.frameCount <= suppressControlFrame;
        }
        // Every attached controller is read, not only InControl's active device: with a
        // second controller connected (or one whose sticks drift), the active device can
        // be another pad, and presses and Start on the pad in hand were never seen.
        var pads = InputManager.Devices;
        if (!bindingsPanel.activeInHierarchy || (bindingPad && captureDevice != InputDevice.Null && !captureDevice.IsAttached) || !Application.isFocused || Input.GetKeyDown(KeyCode.Escape) || pads.Any(pad => pad.CommandWasPressed) || Time.unscaledTime - captureStarted > 10)
        { FinishBinding("Binding cancelled. Previous binding kept."); return true; }
        if (!captureReady)
        {
            captureReady = Time.unscaledTime - captureStarted > .2f && !Input.anyKey && pads.All(pad => !pad.AnyButtonIsPressed && pad.LeftTrigger < .2f && pad.RightTrigger < .2f);
            return true;
        }
        string value = null;
        if (bindingPad)
        {
            foreach (var pad in pads)
            {
                foreach (var button in FlatsControls.PadButtons)
                    if (pad.GetControl(button).WasPressed) { value = button.ToString(); break; }
                if (value != null) break;
            }
        }
        else foreach (var key in captureKeys)
            if (FlatsControls.ValidKey(key) && Input.GetKeyDown(key)) { value = key.ToString(); break; }
        if (value != null)
        {
            bool saved = FlatsControls.Bind(captureAction, value, bindingPad, out string swapped);
            FinishBinding(!saved ? "Binding not saved." : swapped == null ? "Binding saved." :
                FlatsLocalization.Translate("Swapped with: ") + FlatsLocalization.Translate(ActionName(swapped, bindingPad)));
            RefreshPersonalRows();
        }
        return true;
    }
    // Controller list sections start with rows named "Section…"; the bumpers select the
    // first control of the previous or next section.
    void JumpSection(int direction)
    {
        var content = controllerList.content;
        var selected = EventSystem.current.currentSelectedGameObject;
        int row = 0;
        for (int i = 0; i < content.childCount; i++)
            if (selected != null && selected.transform.IsChildOf(content.GetChild(i))) row = i;
        var sections = new List<int>();
        for (int i = 0; i < content.childCount; i++) if (content.GetChild(i).name.StartsWith("Section")) sections.Add(i);
        if (sections.Count == 0) return;
        int currentSection = 0;
        for (int s = 0; s < sections.Count; s++) if (sections[s] <= row) currentSection = s;
        int next = (currentSection + direction + sections.Count) % sections.Count;
        for (int i = sections[next] + 1; i < content.childCount; i++)
        {
            var target = content.GetChild(i).GetComponentInChildren<Selectable>();
            if (target != null && target.IsInteractable()) { EventSystem.current.SetSelectedGameObject(target.gameObject); PlayMenuSound(pressSE); return; }
        }
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
