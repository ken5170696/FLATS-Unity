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
        var control = mt.GetChild(6).GetChild(2);
        foreach (Transform child in control) controlOptions.Add(child.gameObject);
        var ui = new ModCenterWidgets(FlatsLocalizedText.GetSourceFont(bt[0]), () => PlayMenuSound(pressSE));
        bindingsPanel = ui.Panel("Bindings", control, 0, -18, 680, 350, new Color(.31f, .24f, .29f)).gameObject;
        ui.Button("GeneralControls", control, "General", -225, 180, 200, 34, () => ShowBindings(false, false));
        ui.Button("KeyboardControls", control, "Keyboard / Mouse", 0, 180, 220, 34, () => ShowBindings(true, false));
        ui.Button("GamepadControls", control, "Controller", 225, 180, 200, 34, () => ShowBindings(true, true));
        for (int i = 0; i < FlatsControls.KeyboardActions.Length; i++)
        {
            int row = i;
            var button = ui.Button("Binding" + i, bindingsPanel.transform, "", i < 6 ? -168 : 168, 125 - (i % 6) * 38, 320, 38,
                () => BeginBinding(row), ModCenterWidgets.ControlColor);
            button.GetComponentInChildren<Text>().fontSize = 16;
            bindingRows.Add(button);
        }
        bindingStatus = ui.Text("Status", bindingsPanel.transform, "", 0, -148, 650, 40, 15, Color.white);
        ui.Button("ResetBindings", bindingsPanel.transform, "Restore defaults", 185, -103, 280, 32, () =>
        { FlatsControls.ResetBindings(bindingPad); RefreshBindings(); bindingStatus.text = "Default bindings restored."; });
        ui.Text("FixedMenuKeys", control, "Menu: Esc / Start   |   Confirm: Enter / A   |   Back: Esc / B", 0, -218, 680, 26, 14, Color.white);
        ShowBindings(true, false);
    }

    void ShowBindings(bool show, bool pad)
    {
        if (FlatsControls.Capturing) return;
        bindingPad = pad;
        foreach (var child in controlOptions) child.SetActive(!show);
        bindingsPanel.SetActive(show);
        if (show) RefreshBindings();
    }

    string BindingAction(int index) => (bindingPad ? FlatsControls.PadActions : FlatsControls.KeyboardActions)[index];
    static string ActionName(string action, bool pad)
    {
        if (action == "Change") return pad ? "Change / hold to pick up" : "Change weapon";
        if (action == "Jump" && pad) return "Jump / hold to sprint";
        if (action == "Scope") return "Toggle aim";
        if (action == "Interact") return "Pick up / exchange";
        return action;
    }
    void RefreshBindings()
    {
        bindingLanguage = FlatsLocalization.Language;
        int count = (bindingPad ? FlatsControls.PadActions : FlatsControls.KeyboardActions).Length;
        for (int i = 0; i < bindingRows.Count; i++)
        {
            bindingRows[i].gameObject.SetActive(i < count);
            if (i < count)
            {
                string action = BindingAction(i);
                bindingRows[i].GetComponentInChildren<Text>().text = FlatsLocalization.Translate(ActionName(action, bindingPad)) + "   ·   " + FlatsControls.Label(action, bindingPad);
            }
        }
        bindingStatus.text = bindingPad ? "Select an action, then press a controller button.\nLeft stick: move. Right stick: look. Start: menu." : "Select an action, then press a key or mouse button.\nEsc cancels. Duplicate bindings are rejected.";
    }
    void BeginBinding(int index)
    {
        if (captureAction != null) return;
        if (bindingPad && InputManager.ActiveDevice.Name == "None")
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
        bindingStatus.text = "Release all buttons, then press the new binding.\nEsc / Start cancels. Timeout: 10 seconds.";
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
        if (!bindingsPanel.activeInHierarchy || (bindingPad && captureDevice != InputManager.ActiveDevice) || !Application.isFocused || Input.GetKeyDown(KeyCode.Escape) || InputManager.ActiveDevice.CommandWasPressed || Time.unscaledTime - captureStarted > 10)
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
        if (!Application.isMobilePlatform && current != "Play")
            buttons[4].transform.parent.gameObject.SetActive(current != "Settings");
    }
}
