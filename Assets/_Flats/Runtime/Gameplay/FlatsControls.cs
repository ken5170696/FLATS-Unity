using System;
using InControl;
using UnityEngine;

// Gameplay bindings are independent of the fixed menu submit/cancel controls.
public static class FlatsControls
{
    public static readonly string[] KeyboardActions = { "Forward", "Backward", "Left", "Right", "Jump", "Sprint", "Fire", "Aim", "Reload", "Change", "Grenade", "Interact" };
    public static readonly string[] PadActions = { "Jump", "Sprint", "Fire", "Aim", "Reload", "Change", "Grenade", "Scope" };
    static readonly KeyCode[] defaults = { KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.Space, KeyCode.LeftShift, KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.R, KeyCode.E, KeyCode.G, KeyCode.Q };
    static readonly InputControlType[] padDefaults = { InputControlType.Action1, InputControlType.LeftStickButton, InputControlType.RightTrigger, InputControlType.LeftTrigger, InputControlType.Action3, InputControlType.Action4, InputControlType.Action2, InputControlType.RightStickButton };
    public static event Action Changed;
    public static bool Capturing { get; set; }
    public static bool UsingGamepad { get; set; }
    static string Key(string action, bool pad) => "controls.v1." + (pad ? "pad." : "key.") + action;
    public static KeyCode Keyboard(string action)
    {
        int index = Array.IndexOf(KeyboardActions, action);
        if (index < 0) throw new ArgumentException(action);
        return Enum.TryParse(FlatsPreferences.GetString(Key(action, false)), out KeyCode value) && ValidKey(value) ? value : defaults[index];
    }
    public static bool ValidKey(KeyCode key) => key > KeyCode.None && key < KeyCode.JoystickButton0 && key != KeyCode.Escape && Enum.IsDefined(typeof(KeyCode), key);
    public static bool Held(string action) => !Capturing && Input.GetKey(Keyboard(action));
    public static bool Down(string action) => !Capturing && Input.GetKeyDown(Keyboard(action));
    public static float Axis(string positive, string negative) => (Held(positive) ? 1 : 0) - (Held(negative) ? 1 : 0);
    public static readonly InputControlType[] PadButtons = { InputControlType.Action1, InputControlType.Action2, InputControlType.Action3, InputControlType.Action4, InputControlType.LeftBumper, InputControlType.RightBumper, InputControlType.LeftTrigger, InputControlType.RightTrigger, InputControlType.LeftStickButton, InputControlType.RightStickButton, InputControlType.DPadUp, InputControlType.DPadDown, InputControlType.DPadLeft, InputControlType.DPadRight };
    public static InputControlType Pad(string action)
    {
        int index = Array.IndexOf(PadActions, action);
        if (index < 0) throw new ArgumentException(action);
        return Enum.TryParse(FlatsPreferences.GetString(Key(action, true)), out InputControlType value) && Array.IndexOf(PadButtons, value) >= 0 ? value : padDefaults[index];
    }
    static bool State(InputControl control, int edge) => edge == 1 ? control.WasPressed : edge == 2 ? control.WasReleased : control.IsPressed;
    public static bool PadState(string action, int edge = 0)
    {
        if (Capturing) return false;
        var device = InputManager.ActiveDevice;
        if (FlatsPreferences.HasKey(Key(action, true))) return State(device.GetControl(Pad(action)), edge);
        // Preserve old per-device mappings until that action is rebound or reset.
        string legacy = action == "Grenade" ? "Pick" : action == "Aim" ? "Zoom" : action;
        if (Menu.customControlEnabled && Menu.customControl.TryGetValue(legacy, out string binding))
        {
            if (binding.Contains("analog")) return edge == 2 ? Input.GetAxis(binding) < .8f : Input.GetAxis(binding) > .8f;
            return edge == 1 ? Input.GetButtonDown(binding) : edge == 2 ? Input.GetButtonUp(binding) : Input.GetButton(binding);
        }
        bool result = State(device.GetControl(Pad(action)), edge);
        if (action == "Fire") result |= State(device.RightBumper, edge);
        if (action == "Aim") result |= State(device.LeftBumper, edge);
        if (action == "Scope") result |= State(device.DPadUp, edge);
        return result;
    }
    public static string Label(string action, bool pad)
    {
        if (!pad)
        {
            var key = Keyboard(action);
            if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6) return "Mouse " + ((int)key - (int)KeyCode.Mouse0 + 1);
            return key.ToString().Replace("LeftShift", "Left Shift").Replace("RightShift", "Right Shift")
                .Replace("LeftControl", "Left Ctrl").Replace("RightControl", "Right Ctrl");
        }
        string legacy = action == "Grenade" ? "Pick" : action == "Aim" ? "Zoom" : action;
        if (!FlatsPreferences.HasKey(Key(action, true)) && Menu.customControlEnabled && Menu.customControl.TryGetValue(legacy, out string binding)) return binding;
        string label = PadLabel(Pad(action));
        if (!FlatsPreferences.HasKey(Key(action, true)))
        {
            if (action == "Fire") label += " / RB";
            if (action == "Aim") label += " / LB";
            if (action == "Scope") label += " / D-pad Up";
        }
        return label;
    }
    public static string PadLabel(InputControlType button)
    {
        switch (button)
        {
            case InputControlType.Action1: return "A / Cross";
            case InputControlType.Action2: return "B / Circle";
            case InputControlType.Action3: return "X / Square";
            case InputControlType.Action4: return "Y / Triangle";
            case InputControlType.LeftTrigger: return "LT / L2";
            case InputControlType.RightTrigger: return "RT / R2";
            case InputControlType.LeftBumper: return "LB / L1";
            case InputControlType.RightBumper: return "RB / R1";
            case InputControlType.LeftStickButton: return "LS / L3";
            case InputControlType.RightStickButton: return "RS / R3";
            default: return button.ToString();
        }
    }
    public static bool Bind(string action, string value, bool pad, out string conflict)
    {
        conflict = null;
        if (Array.IndexOf(pad ? PadActions : KeyboardActions, action) < 0) return false;
        if (pad ? !Enum.TryParse(value, out InputControlType p) || Array.IndexOf(PadButtons, p) < 0 : !Enum.TryParse(value, out KeyCode k) || !ValidKey(k)) return false;
        foreach (string other in pad ? PadActions : KeyboardActions)
        {
            if (other == action) continue;
            if ((pad ? Pad(other).ToString() : Keyboard(other).ToString()) == value)
            { conflict = other; return false; }
            // Default secondary buttons remain active until their own action is customized.
            if (pad && !FlatsPreferences.HasKey(Key(other, true)) &&
                ((other == "Fire" && value == "RightBumper") || (other == "Aim" && value == "LeftBumper") || (other == "Scope" && value == "DPadUp")))
            { conflict = other; return false; }
        }
        FlatsPreferences.SetString(Key(action, pad), value);
        FlatsPreferences.Save(); Changed?.Invoke(); return true;
    }
    public static void ResetBindings(bool pad)
    {
        foreach (string action in pad ? PadActions : KeyboardActions) FlatsPreferences.DeleteKey(Key(action, pad));
        if (pad) { FlatsPreferences.DeleteKey("controllermapping"); Menu.customControlEnabled = false; }
        FlatsPreferences.Save(); Changed?.Invoke();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() { Changed = null; Capturing = false; UsingGamepad = false; }
}
