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
    readonly List<Button> controlTabs = new List<Button>();
    readonly List<Text> bindingLabels = new List<Text>();
    readonly List<Text> bindingDetails = new List<Text>();
    readonly List<GameObject> controlTabMarkers = new List<GameObject>();
    Button bindingPrevious, bindingNext;
    Text bindingPageLabel;
    int bindingPage;
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
        // Keep the authored Settings panel. New controls borrow its typography,
        // animated button states and live theme materials instead of a second UI skin.
        var buttonStyle = control.GetChild(0).Find("Plus").GetComponent<Button>();
        var textStyle = control.GetChild(0).GetChild(0).GetComponent<Text>();
        bindingsPanel = ControlRect("Bindings", control, 0, 0, 600, 340).gameObject;
        string[] tabNames = { "GeneralControls", "KeyboardControls", "GamepadControls" };
        string[] tabLabels = { "General", "Keyboard / Mouse", "Controller" };
        for (int i = 0; i < tabNames.Length; i++)
        {
            int tab = i;
            var button = ControlButton(tabNames[i], control, tabLabels[i], (i - 1) * 205, 205, 190, 42,
                buttonStyle, textStyle, () => ShowBindings(tab != 0, tab == 2));
            controlTabs.Add(button);
            var marker = ControlRect("ActiveTab", button.transform, 0, -21, 190, 2).gameObject.AddComponent<Image>();
            marker.color = Color.white; marker.raycastTarget = false;
            controlTabMarkers.Add(marker.gameObject);
        }
        for (int i = 0; i < FlatsControls.KeyboardActions.Length; i++)
        {
            int row = i;
            var button = ControlButton("Binding" + i, bindingsPanel.transform, "", 110, 0, 240, 40,
                buttonStyle, textStyle, () => BeginBinding(row));
            bindingRows.Add(button);
            bindingLabels.Add(ControlText("Action", button.transform, "", -240, 0, 240, 40, textStyle, 20, TextAnchor.MiddleLeft));
            bindingDetails.Add(ControlText("Detail", button.transform, "", -240, -17, 240, 20, textStyle, 13, TextAnchor.MiddleLeft));
        }
        bindingStatus = ControlText("Status", bindingsPanel.transform, "", 0, -149, 520, 30, textStyle, 14, TextAnchor.MiddleLeft);
        bindingPrevious = ControlButton("PreviousPage", bindingsPanel.transform, "<", -265, -205, 50, 42,
            buttonStyle, textStyle, () => ChangeBindingPage(-1));
        bindingPageLabel = ControlText("Page", bindingsPanel.transform, "", -180, -205, 100, 42, textStyle, 20, TextAnchor.MiddleCenter);
        bindingNext = ControlButton("NextPage", bindingsPanel.transform, ">", -95, -205, 50, 42,
            buttonStyle, textStyle, () => ChangeBindingPage(1));
        ControlButton("ResetBindings", bindingsPanel.transform, "Restore defaults", 190, -205, 220, 42,
            buttonStyle, textStyle, () =>
            { FlatsControls.ResetBindings(bindingPad); RefreshBindings(); bindingStatus.text = "Default bindings restored."; });
        // Include the new button animators in the same shared-material resolver.
        themedGraphics = transform.root.GetComponentsInChildren<Graphic>(true);
        ShowBindings(!Application.isMobilePlatform, false);
    }

    static RectTransform ControlRect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    static Text ControlText(string name, Transform parent, string value, float x, float y, float width, float height,
        Text style, int size, TextAnchor alignment)
    {
        var text = ControlRect(name, parent, x, y, width, height).gameObject.AddComponent<FlatsLocalizedText>();
        text.font = FlatsLocalizedText.GetSourceFont(style);
        text.fontSize = size; text.fontStyle = style.fontStyle;
        text.color = Color.white; text.alignment = alignment;
        text.raycastTarget = false; text.supportRichText = false;
        text.text = value;
        return text;
    }
    Button ControlButton(string name, Transform parent, string value, float x, float y, float width, float height,
        Button style, Text textStyle, Action action)
    {
        var rect = ControlRect(name, parent, x, y, width, height);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = style.image.sprite; image.type = style.image.type;
        image.material = ResolveThemeMaterial(style.image.material); image.color = Color.white;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image; button.transition = style.transition;
        button.colors = style.colors; button.animationTriggers = style.animationTriggers;
        var animator = rect.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = style.GetComponent<Animator>().runtimeAnimatorController;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        ControlText("Label", rect, value, 0, 0, width - 16, height, textStyle, 20, TextAnchor.MiddleCenter);
        button.onClick.AddListener(() => { PlayMenuSound(pressSE); action(); });
        return button;
    }

    void ShowBindings(bool show, bool pad)
    {
        if (FlatsControls.Capturing) return;
        bindingPad = pad;
        bindingPage = 0;
        foreach (var child in controlOptions) child.SetActive(!show);
        bindingsPanel.SetActive(show);
        int selectedTab = !show ? 0 : pad ? 2 : 1;
        for (int i = 0; i < controlTabs.Count; i++)
        {
            controlTabMarkers[i].SetActive(i == selectedTab);
        }
        if (show) RefreshBindings();
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
        int perPage = bindingPad ? 4 : 6;
        int first = bindingPage * perPage;
        for (int i = 0; i < bindingRows.Count; i++)
        {
            bool visible = i >= first && i < first + perPage && i < count;
            bindingRows[i].gameObject.SetActive(visible);
            if (visible)
            {
                string action = BindingAction(i);
                ((RectTransform)bindingRows[i].transform).anchoredPosition = new Vector2(110, (bindingPad ? 115 : 125) - (i - first) * (bindingPad ? 70 : 45));
                bindingRows[i].transform.Find("Label").GetComponent<Text>().text = FlatsControls.Label(action, bindingPad);
                bindingLabels[i].text = ActionName(action, false);
                string detail = bindingPad && action == "Jump" ? "Hold to sprint" : bindingPad && action == "Change" ? "Hold to pick up" : "";
                bindingDetails[i].text = detail;
                bindingLabels[i].rectTransform.anchoredPosition = new Vector2(-240, detail.Length == 0 ? 0 : 7);
            }
        }
        bindingPageLabel.text = (bindingPage + 1) + " / 2";
        bindingStatus.text = "Select a binding to change it.";
    }
    void ChangeBindingPage(int direction)
    {
        if (FlatsControls.Capturing || releasePending) return;
        bindingPage = (bindingPage + direction + 2) % 2;
        RefreshBindings();
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
        if (!Application.isMobilePlatform && current != "Play")
            buttons[4].transform.parent.gameObject.SetActive(current != "Settings");
    }
}
