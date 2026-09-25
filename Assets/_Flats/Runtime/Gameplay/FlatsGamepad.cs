using System;
using System.Collections.Generic;
using System.Linq;
using InControl;
using UnityEngine;

// Controller preferences: layout presets, look speed per axis, response curve,
// stick deadzones, vibration and device-specific button names. Defaults follow common
// console FPS settings: Standard (exponential) response, 1x speeds, the device
// profile's own deadzones and a short turn boost at full horizontal deflection.
public static class FlatsGamepad
{
    public const string LookHKey = "controls.v1.pad.lookH", LookVKey = "controls.v1.pad.lookV", CurveKey = "controls.v1.pad.curve",
        LookDeadzoneKey = "controls.v1.pad.lookDeadzone", MoveDeadzoneKey = "controls.v1.pad.moveDeadzone", VibrationKey = "controls.v1.pad.vibration";
    public static readonly string[] Keys = { LookHKey, LookVKey, CurveKey, LookDeadzoneKey, MoveDeadzoneKey, VibrationKey };

    public static readonly float[] SpeedSteps = { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f };
    const int DefaultSpeed = 2;
    public static readonly string[] Curves = { "Linear", "Standard", "Dynamic" };
    // -1 keeps the controller profile's own deadzone (the original behaviour).
    public static readonly int[] DeadzoneSteps = { -1, 0, 5, 10, 15, 20, 25, 30 };
    public static readonly int[] VibrationSteps = { 0, 25, 50, 75, 100 };
    const int DefaultVibration = 4;
    const int DefaultCurve = 1;

    // Button per PadActions entry: Jump, Sprint, Fire, Aim, Reload, Change, Grenade, Scope.
    public static readonly string[] PresetNames = { "Default", "Tactical", "Bumper Jumper" };
    static readonly InputControlType[][] presets =
    {
        // Common console FPS layout: grenade on the right bumper.
        new[] { InputControlType.Action1, InputControlType.LeftStickButton, InputControlType.RightTrigger, InputControlType.LeftTrigger, InputControlType.Action3, InputControlType.Action4, InputControlType.RightBumper, InputControlType.RightStickButton },
        // Grenade on B, as in the earlier FLATS layout; the bumpers stay free.
        new[] { InputControlType.Action1, InputControlType.LeftStickButton, InputControlType.RightTrigger, InputControlType.LeftTrigger, InputControlType.Action3, InputControlType.Action4, InputControlType.Action2, InputControlType.RightStickButton },
        // Jump on the left bumper lets players jump without leaving the look stick.
        new[] { InputControlType.LeftBumper, InputControlType.LeftStickButton, InputControlType.RightTrigger, InputControlType.LeftTrigger, InputControlType.Action3, InputControlType.Action4, InputControlType.RightBumper, InputControlType.RightStickButton },
    };
    public static InputControlType DefaultButton(int action) => presets[0][action];

    public static event Action Changed;

    static int Index(string key, int count, int fallback)
        => int.TryParse(FlatsPreferences.GetString(key), out int value) && value >= 0 && value < count ? value : fallback;
    static void Store(string key, int value, int count)
    {
        FlatsPreferences.SetString(key, ((value % count + count) % count).ToString());
        FlatsPreferences.Save(); version++; Changed?.Invoke();
    }
    public static bool Valid(string key, string value)
    {
        if (!int.TryParse(value, out int index) || index < 0) return false;
        if (key == LookHKey || key == LookVKey) return index < SpeedSteps.Length;
        if (key == CurveKey) return index < Curves.Length;
        if (key == LookDeadzoneKey || key == MoveDeadzoneKey) return index < DeadzoneSteps.Length;
        if (key == VibrationKey) return index < VibrationSteps.Length;
        return false;
    }

    public static int LookHIndex { get => Index(LookHKey, SpeedSteps.Length, DefaultSpeed); set => Store(LookHKey, value, SpeedSteps.Length); }
    public static int LookVIndex { get => Index(LookVKey, SpeedSteps.Length, DefaultSpeed); set => Store(LookVKey, value, SpeedSteps.Length); }
    public static int CurveIndex { get => Index(CurveKey, Curves.Length, DefaultCurve); set => Store(CurveKey, value, Curves.Length); }
    public static int LookDeadzoneIndex { get => Index(LookDeadzoneKey, DeadzoneSteps.Length, 0); set => Store(LookDeadzoneKey, value, DeadzoneSteps.Length); }
    public static int MoveDeadzoneIndex { get => Index(MoveDeadzoneKey, DeadzoneSteps.Length, 0); set => Store(MoveDeadzoneKey, value, DeadzoneSteps.Length); }
    public static int VibrationIndex { get => Index(VibrationKey, VibrationSteps.Length, DefaultVibration); set => Store(VibrationKey, value, VibrationSteps.Length); }
    public static float Vibration => VibrationSteps[VibrationIndex] / 100f;

    public static string SpeedLabel(int index) => SpeedSteps[index].ToString("0.##") + "x";
    public static string DeadzoneLabel(int index) => DeadzoneSteps[index] < 0 ? "Auto" : DeadzoneSteps[index] + "%";
    public static string VibrationLabel(int index) => VibrationSteps[index] == 0 ? "OFF" : VibrationSteps[index] + "%";

    // Look stick after the deadzone: horizontal/vertical speed and the response curve
    // on the stick's magnitude, so diagonal direction is kept. The result is per frame
    // in LookRotationPolicy's gamepad units (2 degrees yaw, 1.5 pitch per camera
    // sensitivity step), scaled by the frame time so the turn rate does not depend on
    // the frame rate: at Normal sensitivity, full deflection turns 180 degrees/s and
    // pitches 135 degrees/s.
    public const float UnitsPerSecond = 45f;
    // Holding the stick fully sideways ramps yaw up to TurnBoost x after a short delay,
    // so large turns are quick while small corrections stay precise. Not while aiming.
    public const float TurnBoost = 1.6f, TurnBoostDelay = .12f, TurnBoostRamp = .35f;
    static float boostTime;
    public static Vector2 Look(Vector2 stick, float deltaTime, bool aiming)
    {
        float magnitude = Mathf.Clamp01(stick.magnitude);
        if (magnitude <= 0f) { boostTime = 0f; return Vector2.zero; }
        deltaTime = Mathf.Min(deltaTime, .1f);
        float curved = Curve(magnitude, CurveIndex);
        var shaped = stick / stick.magnitude * curved;
        float boost = 1f;
        if (!aiming && magnitude > .95f && Mathf.Abs(stick.x) > .85f * magnitude)
        {
            boostTime += deltaTime;
            boost = 1f + (TurnBoost - 1f) * Mathf.SmoothStep(0f, 1f, (boostTime - TurnBoostDelay) / TurnBoostRamp);
        }
        else boostTime = 0f;
        float scale = UnitsPerSecond * deltaTime;
        return new Vector2(shaped.x * SpeedSteps[LookHIndex] * boost * scale, shaped.y * SpeedSteps[LookVIndex] * scale);
    }
    public static float Curve(float magnitude, int curve)
    {
        switch (curve)
        {
            // Slower near the centre for fine aim, full speed at the edge.
            case 1: return magnitude * magnitude;
            // Quick start, steadier middle, fast edge: slope 1.4, 0.6, 1.4 at 0, 0.5, 1, smooth throughout.
            case 2: return magnitude + .4f * Mathf.Sin(2f * Mathf.PI * magnitude) / (2f * Mathf.PI);
            default: return magnitude;
        }
    }

    // Stick deadzones are applied by InControl (circular, rescaled). The profile value is
    // remembered per device so "Auto" restores it.
    static readonly Dictionary<InputDevice, float[]> profileDeadzones = new Dictionary<InputDevice, float[]>();
    public static void ApplyDeadzones(InputDevice device)
    {
        if (device == null || device == InputDevice.Null) return;
        var sides = new[] { device.LeftStickLeft, device.LeftStickRight, device.LeftStickUp, device.LeftStickDown, device.RightStickLeft, device.RightStickRight, device.RightStickUp, device.RightStickDown };
        if (!profileDeadzones.TryGetValue(device, out var original))
            profileDeadzones[device] = original = sides.Select(s => s.LowerDeadZone).ToArray();
        for (int i = 0; i < sides.Length; i++)
        {
            int step = DeadzoneSteps[i < 4 ? MoveDeadzoneIndex : LookDeadzoneIndex];
            sides[i].LowerDeadZone = rawTest ? 0f : step < 0 ? original[i] : step / 100f;
        }
    }
    public static void ApplyDeadzones() { foreach (var device in InputManager.Devices) ApplyDeadzones(device); applied = null; }
    // InControl clears its device events on setup, so callers apply on use instead:
    // cheap unless the active device or the settings changed.
    static InputDevice applied;
    static int appliedVersion = -1, version;
    public static void EnsureApplied(InputDevice device)
    {
        if (device == applied && appliedVersion == version) return;
        ApplyDeadzones(device); applied = device; appliedVersion = version;
    }
    // Deadzone radius in effect for the look (right) or move (left) stick, for the menu tester.
    public static float Deadzone(InputDevice device, bool look)
    {
        int step = DeadzoneSteps[look ? LookDeadzoneIndex : MoveDeadzoneIndex];
        if (step >= 0) return step / 100f;
        if (device == null || device == InputDevice.Null) return 0f;
        return profileDeadzones.TryGetValue(device, out var original) ? original[look ? 4 : 0] : (look ? device.RightStickLeft : device.LeftStickLeft).LowerDeadZone;
    }
    // While the menu stick tester is shown, InControl's lower deadzone is 0, so the stick
    // value is the raw sample rescaled by the upper deadzone; RawStick undoes that scale.
    // Gameplay never runs with the tester open.
    static bool rawTest;
    public static bool RawTest
    {
        get => rawTest;
        set { if (rawTest == value) return; rawTest = value; version++; }
    }
    public static Vector2 RawStick(InputDevice device, bool look)
    {
        if (device == null || device == InputDevice.Null) return Vector2.zero;
        var stick = look ? device.RightStick.Vector : device.LeftStick.Vector;
        float upper = look ? device.RightStickLeft.UpperDeadZone : device.LeftStickLeft.UpperDeadZone;
        return stick.magnitude >= 1f ? stick : stick * upper;
    }

    public static void Vibrate(InputDevice device, float intensity)
    {
        float scaled = intensity * Vibration;
        if (device != null && scaled > 0f) device.Vibrate(scaled);
    }

    // Layout presets. The current layout is "Custom" when it matches none of them.
    public static int CurrentPreset()
    {
        for (int p = 0; p < presets.Length; p++)
            if (Enumerable.Range(0, FlatsControls.PadActions.Length).All(i => FlatsControls.Pad(FlatsControls.PadActions[i]) == presets[p][i])) return p;
        return -1;
    }
    public static void ApplyPreset(int preset)
    {
        for (int i = 0; i < FlatsControls.PadActions.Length; i++) FlatsControls.SetPad(FlatsControls.PadActions[i], presets[preset][i]);
        FlatsControls.NotifyChanged();
    }
    public static void Reset()
    {
        foreach (var key in Keys) FlatsPreferences.DeleteKey(key);
        FlatsPreferences.Save(); version++; Changed?.Invoke();
    }

    // Button names follow the connected controller family.
    public enum Style { Generic, Xbox, PlayStation }
    public static Style DeviceStyle(InputDevice device)
    {
        if (device == null || device == InputDevice.Null) return Style.Generic;
        var style = device.DeviceStyle.ToString();
        if (style.StartsWith("Xbox", StringComparison.Ordinal)) return Style.Xbox;
        if (style.StartsWith("PlayStation", StringComparison.Ordinal)) return Style.PlayStation;
        return Style.Generic;
    }
    public static string Glyph(InputControlType button, Style style)
    {
        string xbox, ps;
        switch (button)
        {
            case InputControlType.Action1: xbox = "A"; ps = "Cross"; break;
            case InputControlType.Action2: xbox = "B"; ps = "Circle"; break;
            case InputControlType.Action3: xbox = "X"; ps = "Square"; break;
            case InputControlType.Action4: xbox = "Y"; ps = "Triangle"; break;
            case InputControlType.LeftTrigger: xbox = "LT"; ps = "L2"; break;
            case InputControlType.RightTrigger: xbox = "RT"; ps = "R2"; break;
            case InputControlType.LeftBumper: xbox = "LB"; ps = "L1"; break;
            case InputControlType.RightBumper: xbox = "RB"; ps = "R1"; break;
            case InputControlType.LeftStickButton: xbox = "LS"; ps = "L3"; break;
            case InputControlType.RightStickButton: xbox = "RS"; ps = "R3"; break;
            case InputControlType.DPadUp: xbox = ps = "D-pad Up"; break;
            case InputControlType.DPadDown: xbox = ps = "D-pad Down"; break;
            case InputControlType.DPadLeft: xbox = ps = "D-pad Left"; break;
            case InputControlType.DPadRight: xbox = ps = "D-pad Right"; break;
            default: return button.ToString();
        }
        return style == Style.Xbox ? xbox : style == Style.PlayStation ? ps : xbox == ps ? xbox : xbox + " / " + ps;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    // Profile deadzones are kept: a device that survives into the next session must not
    // record an already customised value as its "Auto" baseline.
    static void ResetSession() { Changed = null; applied = null; rawTest = false; boostTime = 0f; version++; }
    // An imported save replaces preferences.
    public static void Reload() { version++; }
}
