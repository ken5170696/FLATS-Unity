using System;
using InControl;
using UnityEngine;

// Gameplay bindings are independent of the fixed menu submit/cancel controls.
public static class FlatsControls
{
    // "Shop" (Roguelike: reopen the dismissed shop), "Melee" (Roguelike melee weapon, QA-39; it used the fixed V key and D-pad
    // left before) and "Overview" (Roguelike run overview, QA-41; it used the fixed Tab key and the pad's Back/View button) were
    // added after bindings were first saved; saves without them get a free default. New actions are appended so the authored
    // binding rows keep their indices.
    public static readonly string[] KeyboardActions = { "Forward", "Backward", "Left", "Right", "Jump", "Sprint", "Fire", "Aim", "Reload", "Change", "Grenade", "Interact", "Ultimate", "Tactical", "Shop", "Melee", "Overview" };
    public static readonly string[] PadActions = { "Jump", "Sprint", "Fire", "Aim", "Reload", "Change", "Grenade", "Scope", "Ultimate", "Tactical", "Shop", "Melee", "Overview" };
    static readonly KeyCode[] defaults = { KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.Space, KeyCode.LeftShift, KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.R, KeyCode.E, KeyCode.G, KeyCode.Q, KeyCode.F, KeyCode.C, KeyCode.B, KeyCode.V, KeyCode.Tab };
    // Default of a late-added action when an older save already put its default on another action: the first key or button
    // no other action uses, in the order the actions were added.
    static readonly string[] lateActions = { "Shop", "Melee", "Overview" };
    static readonly KeyCode[][] lateKeys =
    {
        new[] { KeyCode.B, KeyCode.H, KeyCode.N, KeyCode.T, KeyCode.Y, KeyCode.U, KeyCode.J, KeyCode.K },
        new[] { KeyCode.V, KeyCode.X, KeyCode.Z, KeyCode.T, KeyCode.Y, KeyCode.H, KeyCode.N, KeyCode.M },
        new[] { KeyCode.Tab, KeyCode.BackQuote, KeyCode.O, KeyCode.I, KeyCode.P, KeyCode.L, KeyCode.K, KeyCode.M },
    };
    static readonly InputControlType[][] lateButtons =
    {
        new[] { InputControlType.DPadRight, InputControlType.DPadUp },
        new[] { InputControlType.DPadLeft, InputControlType.DPadUp, InputControlType.DPadDown, InputControlType.Action2 },
        new[] { InputControlType.Back, InputControlType.DPadUp, InputControlType.DPadDown, InputControlType.Action2 },
    };
    public static event Action Changed;
    public static bool Capturing { get; set; }
    public static bool UsingGamepad { get; set; }
    static string Key(string action, bool pad) => "controls.v1." + (pad ? "pad." : "key.") + action;
    public const string AimModeKey = "controls.v1.aimMode";
    // Keyboard and mouse aim: "toggle" (default, the original behaviour) or "hold".
    public static bool HoldToAim
    {
        get => FlatsPreferences.GetString(AimModeKey) == "hold";
        set { FlatsPreferences.SetString(AimModeKey, value ? "hold" : "toggle"); FlatsPreferences.Save(); Changed?.Invoke(); }
    }
    public const string SprintModeKey = "controls.v1.sprintMode";
    // Keyboard and mouse sprint: "hold" (default, the original behaviour) or "toggle" (press to start, press again to stop).
    // It follows the Sprint binding. A controller keeps its own rule (a click toggles, holding also sprints; see FPSController).
    public static bool ToggleSprint
    {
        get => FlatsPreferences.GetString(SprintModeKey) == "toggle";
        set { FlatsPreferences.SetString(SprintModeKey, value ? "toggle" : "hold"); FlatsPreferences.Save(); Changed?.Invoke(); }
    }
    public const string WheelSwitchKey = "controls.v1.wheelSwitch";
    // Keyboard and mouse: a mouse wheel notch also switches weapons, like the Change binding. "on" (default) or "off".
    public static bool WheelSwitch
    {
        get => FlatsPreferences.GetString(WheelSwitchKey) != "off";
        set { FlatsPreferences.SetString(WheelSwitchKey, value ? "on" : "off"); FlatsPreferences.Save(); Changed?.Invoke(); }
    }
    // Either wheel direction switches (a player holds two guns). A flick of the wheel reports several notches over a few frames:
    // one switch per WheelSwitchCooldown seconds, and the scroll that arrives during the cooldown is dropped rather than queued.
    public const float WheelSwitchCooldown = 0.3f;
    static float wheelSwitchAt = -1f;
    static int wheelSwitchFrame = -1;
    static bool wheelSwitchThisFrame;
    public static bool WheelSwitchDown()
    {
        if (wheelSwitchFrame == Time.frameCount) return wheelSwitchThisFrame;
        wheelSwitchFrame = Time.frameCount; wheelSwitchThisFrame = false;
        if (!WheelSwitch || Capturing || !Application.isFocused || Mathf.Abs(Input.mouseScrollDelta.y) < 0.01f) return false;
        if (Time.unscaledTime - wheelSwitchAt < WheelSwitchCooldown) return false;
        wheelSwitchAt = Time.unscaledTime; wheelSwitchThisFrame = true;
        return true;
    }
    public const string KillCinematicKey = "ui.v1.killCinematic";
    // Headshot and mortal-shot slow-motion camera. "off" keeps only the text notice.
    public static bool KillCinematic
    {
        get => FlatsPreferences.GetString(KillCinematicKey) != "off";
        set { FlatsPreferences.SetString(KillCinematicKey, value ? "on" : "off"); FlatsPreferences.Save(); Changed?.Invoke(); }
    }
    public const string DamageNumbersKey = "ui.v1.damageNumbers";
    // Damage numbers on hit targets (Roguelike Survival, QA-48): "off", "floating" (one rising number per hit) or "stacked" (the
    // default, Apex-style: rapid hits on one target add up into one number). Saves from before QA-48 hold "on", read as stacked.
    // "off" hides the numbers; the hit marker and sounds stay. Settings row "DamageNumbers" shows DamageNumberStyleNames.
    public static readonly string[] DamageNumberStyleNames = { "OFF", "Floating", "Stacked" };
    public static Flats.Core.Roguelike.DamageNumberMode DamageNumberStyle
    {
        get => Flats.Core.Roguelike.DamageNumberRules.ParseMode(FlatsPreferences.GetString(DamageNumbersKey));
        set { FlatsPreferences.SetString(DamageNumbersKey, Flats.Core.Roguelike.DamageNumberRules.StoredValue(value)); FlatsPreferences.Save(); Changed?.Invoke(); }
    }
    // Any damage numbers at all, for callers that only need on/off; turning it on picks the default (stacked).
    public static bool DamageNumbers
    {
        get => DamageNumberStyle != Flats.Core.Roguelike.DamageNumberMode.Off;
        set { if (value != DamageNumbers) DamageNumberStyle = value ? Flats.Core.Roguelike.DamageNumberMode.Stacked : Flats.Core.Roguelike.DamageNumberMode.Off; }
    }
    public const string AimSensitivityKey = "controls.v1.aimSensitivity";
    // Look sensitivity while aimed, on the camera sensitivity scale (Low 1, Normal 2, High 3).
    // Index 0 follows the camera sensitivity, the original behaviour. Weapon zoom still slows it.
    public static readonly float[] AimSensitivities = { 0f, 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 4f };
    public static readonly string[] AimSensitivityNames = { "Match camera", "Very Low", "Low", "Low+", "Normal", "Normal+", "High", "Very High" };
    static int aimSensitivityIndex = -1;
    public static int AimSensitivityIndex
    {
        get
        {
            if (aimSensitivityIndex < 0)
                aimSensitivityIndex = int.TryParse(FlatsPreferences.GetString(AimSensitivityKey), out int saved) && saved >= 0 && saved < AimSensitivities.Length ? saved : 0;
            return aimSensitivityIndex;
        }
        set
        {
            aimSensitivityIndex = (value % AimSensitivities.Length + AimSensitivities.Length) % AimSensitivities.Length;
            FlatsPreferences.SetString(AimSensitivityKey, aimSensitivityIndex.ToString());
            FlatsPreferences.Save(); Changed?.Invoke();
        }
    }
    // An imported save replaces preferences; read the stored value again on next use.
    public static void ReloadAimSensitivity() { aimSensitivityIndex = -1; }
    public static float AimSensitivity(float cameraSensitivity)
    {
        float aimed = AimSensitivities[AimSensitivityIndex];
        return aimed > 0f ? aimed : cameraSensitivity;
    }
    public static KeyCode Keyboard(string action)
    {
        int index = Array.IndexOf(KeyboardActions, action);
        if (index < 0) throw new ArgumentException(action);
        if (Enum.TryParse(FlatsPreferences.GetString(Key(action, false)), out KeyCode value) && ValidKey(value)) return value;
        int late = Array.IndexOf(lateActions, action);
        if (late < 0) return defaults[index];
        // resolved once per frame: every Down("Shop") would otherwise read all the other bindings again
        if (lateKeyFrame[late] != Time.frameCount) { lateKeyFrame[late] = Time.frameCount; lateKey[late] = FreeDefault(lateKeys[late], action, late); }
        return lateKey[late];
    }
    static readonly int[] lateKeyFrame = { -1, -1, -1 }, lateButtonFrame = { -1, -1, -1 };
    static readonly KeyCode[] lateKey = new KeyCode[lateActions.Length];
    static readonly InputControlType[] lateButton = new InputControlType[lateActions.Length];
    static void ForgetLateDefaults() { for (int i = 0; i < lateActions.Length; i++) lateKeyFrame[i] = lateButtonFrame[i] = -1; }
    // Only a late action's own free default reads the other bindings, and it skips a later late action that has no saved binding
    // (that one resolves against this one instead), so this cannot recurse: Shop never waits for Melee's default.
    static bool SkipsOther(string other, int late, bool pad)
    {
        int otherLate = Array.IndexOf(lateActions, other);
        return otherLate > late && !FlatsPreferences.HasKey(Key(other, pad));
    }
    static KeyCode FreeDefault(KeyCode[] candidates, string action, int late)
    {
        foreach (var key in candidates)
        {
            bool used = false;
            foreach (string other in KeyboardActions) if (other != action && !SkipsOther(other, late, false) && Keyboard(other) == key) { used = true; break; }
            if (!used) return key;
        }
        return candidates[0];
    }
    static InputControlType FreeDefault(InputControlType[] candidates, string action, int late)
    {
        foreach (var button in candidates)
        {
            bool used = false;
            foreach (string other in PadActions) if (other != action && !SkipsOther(other, late, true) && Pad(other) == button) { used = true; break; }
            if (!used) return button;
        }
        return candidates[0];
    }
    public static bool ValidKey(KeyCode key) => key > KeyCode.None && key < KeyCode.JoystickButton0 && key != KeyCode.Escape && Enum.IsDefined(typeof(KeyCode), key);
    public static bool Held(string action) => !Capturing && Input.GetKey(Keyboard(action));
    public static bool Down(string action) => !Capturing && Input.GetKeyDown(Keyboard(action));
    public static float Axis(string positive, string negative) => (Held(positive) ? 1 : 0) - (Held(negative) ? 1 : 0);
    public static readonly InputControlType[] PadButtons = { InputControlType.Action1, InputControlType.Action2, InputControlType.Action3, InputControlType.Action4, InputControlType.LeftBumper, InputControlType.RightBumper, InputControlType.LeftTrigger, InputControlType.RightTrigger, InputControlType.LeftStickButton, InputControlType.RightStickButton, InputControlType.DPadUp, InputControlType.DPadDown, InputControlType.DPadLeft, InputControlType.DPadRight, InputControlType.Back };
    // InControl counts only the face buttons as "buttons"; bumpers, stick clicks, the
    // D-pad and Start/Back are separate controls. These helpers treat every bindable
    // button as controller input.
    public static bool AnyPadButtonHeld(InputDevice device)
    {
        if (device == null || device == InputDevice.Null) return false;
        if (device.AnyButtonIsPressed || device.CommandIsPressed) return true;
        foreach (var button in PadButtons) if (device.GetControl(button).IsPressed) return true;
        return false;
    }
    // Input.anyKey also reports held joystick buttons; this is true only for a keyboard
    // key or mouse button.
    public static bool KeyboardOrMouseKeyHeld()
    {
        if (!Input.anyKey) return false;
        for (var key = KeyCode.JoystickButton0; key <= KeyCode.Joystick8Button19; key++)
            if (Input.GetKey(key)) return false;
        return true;
    }
    public static InputControlType Pad(string action)
    {
        int index = Array.IndexOf(PadActions, action);
        if (index < 0) throw new ArgumentException(action);
        if (Enum.TryParse(FlatsPreferences.GetString(Key(action, true)), out InputControlType value) && Array.IndexOf(PadButtons, value) >= 0) return value;
        int late = Array.IndexOf(lateActions, action);
        if (late < 0) return FlatsGamepad.DefaultButton(index);
        if (lateButtonFrame[late] != Time.frameCount) { lateButtonFrame[late] = Time.frameCount; lateButton[late] = FreeDefault(lateButtons[late], action, late); }
        return lateButton[late];
    }
    static bool State(InputControl control, int edge) => edge == 1 ? control.WasPressed : edge == 2 ? control.WasReleased : control.IsPressed;
    public static bool PadState(string action, int edge = 0)
    {
        if (Capturing) return false;
        var device = InputManager.ActiveDevice;
        if (FlatsPreferences.HasKey(Key(action, true))) return State(device.GetControl(Pad(action)), edge);
        // Preserve old per-device mappings until that action is rebound or reset.
        string legacy = action == "Grenade" ? "Pick" : action == "Aim" ? "Zoom" : action;
        if (Menu.customControlEnabled && Menu.customControl.TryGetValue(legacy, out string binding)) return LegacyPad(binding, edge);
        return State(device.GetControl(Pad(action)), edge);
    }
    // Old per-device mappings store "joystick 1 button N" (a KeyCode name, not an Input
    // Manager axis, so Input.GetButton would throw) or "joystick 1 analog N".
    public static bool LegacyPad(string binding, int edge = 0)
    {
        try
        {
            if (binding.Contains("analog")) return edge == 2 ? Input.GetAxis(binding) < .8f : Input.GetAxis(binding) > .8f;
            return edge == 1 ? Input.GetKeyDown(binding) : edge == 2 ? Input.GetKeyUp(binding) : Input.GetKey(binding);
        }
        catch (ArgumentException)
        {
            return false;
        }
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
        var style = FlatsGamepad.DeviceStyle(InputManager.ActiveDevice);
        return FlatsGamepad.Glyph(Pad(action), style);
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
    // A button already used by another action is swapped: that action takes this
    // action's previous button, so nothing is left unbound. `swapped` names it.
    public static bool Bind(string action, string value, bool pad, out string swapped)
    {
        swapped = null;
        if (Array.IndexOf(pad ? PadActions : KeyboardActions, action) < 0) return false;
        if (pad ? !Enum.TryParse(value, out InputControlType p) || Array.IndexOf(PadButtons, p) < 0 : !Enum.TryParse(value, out KeyCode k) || !ValidKey(k)) return false;
        string previous = pad ? Pad(action).ToString() : Keyboard(action).ToString();
        foreach (string other in pad ? PadActions : KeyboardActions)
        {
            if (other == action) continue;
            if ((pad ? Pad(other).ToString() : Keyboard(other).ToString()) == value) swapped = other;
            // A default secondary button (RB fire, LB aim, D-pad up scope) is released by
            // saving that action's primary button explicitly.
            else if (pad && !FlatsPreferences.HasKey(Key(other, true)) &&
                ((other == "Fire" && value == "RightBumper") || (other == "Aim" && value == "LeftBumper") || (other == "Scope" && value == "DPadUp")))
                FlatsPreferences.SetString(Key(other, true), Pad(other).ToString());
        }
        if (swapped != null && previous != value) FlatsPreferences.SetString(Key(swapped, pad), previous);
        FlatsPreferences.SetString(Key(action, pad), value);
        ForgetLateDefaults();
        FlatsPreferences.Save(); Changed?.Invoke(); return true;
    }
    // Used by layout presets; call NotifyChanged once afterwards.
    public static void SetPad(string action, InputControlType button)
    {
        if (Array.IndexOf(PadActions, action) < 0 || Array.IndexOf(PadButtons, button) < 0) throw new ArgumentException(action);
        FlatsPreferences.SetString(Key(action, true), button.ToString());
        ForgetLateDefaults();
    }
    public static void NotifyChanged() { ForgetLateDefaults(); FlatsPreferences.Save(); Changed?.Invoke(); }
    public static void ResetBindings(bool pad)
    {
        foreach (string action in pad ? PadActions : KeyboardActions) FlatsPreferences.DeleteKey(Key(action, pad));
        if (pad) { FlatsPreferences.DeleteKey("controllermapping"); Menu.customControlEnabled = false; }
        ForgetLateDefaults();
        FlatsPreferences.Save(); Changed?.Invoke();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() { Changed = null; Capturing = false; UsingGamepad = false; ForgetLateDefaults(); }
}
