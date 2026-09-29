using UnityEngine;

/// <summary>
/// One place for the Roguelike mode's own inputs across keyboard, gamepad and touch. Keyboard uses the FLATS
/// bindings (Interact, Tab), a gamepad uses the pad bindings (Change = interact, Back = overview, bumpers = tabs),
/// and a phone uses the two touch buttons the Roguelike HUD shows (RogueTouchButton feeds them here).
/// </summary>
public static class RogueInput
{
    public enum Scheme { Keyboard, Gamepad, Touch }

    /// <summary>Editor-only validation override; null follows the real devices.</summary>
    public static Scheme? DebugScheme;

    static bool touchInteractHeld;
    // A touch press is latched until the first reader of a later frame consumes it, then it reads true for the
    // rest of that frame only (the same one-frame semantics as GetKeyDown), so a press between two frames is
    // never lost and never fires twice.
    static bool touchInteractPending, touchOverviewPending, touchUltimatePending, touchTacticalPending;
    static int touchInteractFrame = -1, touchOverviewFrame = -1, touchUltimateFrame = -1, touchTacticalFrame = -1;

    static bool Consume(ref bool pending, ref int frame)
    {
        if (pending) { pending = false; frame = Time.frameCount; }
        return frame == Time.frameCount;
    }

    public static Scheme Current
    {
        get
        {
            if (DebugScheme.HasValue) return DebugScheme.Value;
            if (Application.isMobilePlatform || (Input.touchSupported && !Input.mousePresent && Input.GetJoystickNames().Length == 0)) return Scheme.Touch;
            return FlatsControls.UsingGamepad ? Scheme.Gamepad : Scheme.Keyboard;
        }
    }

    public static bool IsTouch { get { return Current == Scheme.Touch; } }

    // ---- touch buttons (RogueTouchButton on the HUD)
    public static void TouchInteractDown() { touchInteractHeld = true; touchInteractPending = true; }
    public static void TouchInteractUp() { touchInteractHeld = false; }
    public static void TouchOverview() { touchOverviewPending = true; }
    public static void TouchUltimate() { touchUltimatePending = true; }
    public static void TouchTactical() { touchTacticalPending = true; }
    /// <summary>Clears touch state when the HUD goes away so a lost PointerUp never leaves the button "held".</summary>
    public static void ResetTouch()
    {
        touchInteractHeld = false; touchInteractPending = touchOverviewPending = touchUltimatePending = touchTacticalPending = false;
        touchInteractFrame = touchOverviewFrame = touchUltimateFrame = touchTacticalFrame = -1;
    }

    // ---- actions
    public static bool InteractHeld { get { return FlatsControls.Held("Interact") || FlatsControls.PadState("Change", 0) || touchInteractHeld; } }
    public static bool InteractDown { get { return FlatsControls.Down("Interact") || FlatsControls.PadState("Change", 1) || Consume(ref touchInteractPending, ref touchInteractFrame); } }

    /// <summary>Ultimate and tactical: the bound keys, the pad buttons, or a tap on the HUD's ability slots on a phone.</summary>
    public static bool UltimateDown { get { return FlatsControls.Down("Ultimate") || FlatsControls.PadState("Ultimate", 1) || Consume(ref touchUltimatePending, ref touchUltimateFrame); } }
    public static bool TacticalDown { get { return FlatsControls.Down("Tactical") || FlatsControls.PadState("Tactical", 1) || Consume(ref touchTacticalPending, ref touchTacticalFrame); } }

    public static bool OverviewToggle
    {
        get
        {
            if (Input.GetKeyDown(KeyCode.Tab) || Consume(ref touchOverviewPending, ref touchOverviewFrame)) return true;
            var pad = InControl.InputManager.ActiveDevice;
            return pad != null && pad.GetControl(InControl.InputControlType.Back).WasPressed;
        }
    }

    /// <summary>Label of the interact control for prompts: the key name, the pad button, or the touch button's caption.</summary>
    public static string InteractLabel { get { return KeyText("Interact"); } }

    // ---- key names for prompts. Actions: the FLATS keyboard actions (Interact, Ultimate, Tactical...) plus "Overview".
    // A gamepad interacts with its Change button (the pad has no Interact binding) and opens the overview with Back.

    /// <summary>The binding for a key cap on the HUD: "E", "M4", "Shift", "RB". Empty on touch, where the slot itself is tapped.</summary>
    public static string KeyCap(string action) { return Current == Scheme.Touch ? "" : Binding(action, true); }

    /// <summary>The binding inside a sentence, bracketed like a key cap: "[E]", "[Mouse button 4]", "[RB]"; on touch the on-screen control.
    /// Plain text: some dialogs that show these sentences have rich text off.</summary>
    public static string KeyText(string action)
    {
        if (Current == Scheme.Touch)
            return RoguelikeController.T(action == "Overview" ? "the Overview button" : action == "Ultimate" ? "the Ultimate slot" : action == "Tactical" ? "the Tactical slot" : "Interact");
        var name = Binding(action, false);
        return string.IsNullOrEmpty(name) ? RoguelikeController.T(action) : "[" + name + "]";
    }

    static string Binding(string action, bool cap)
    {
        if (Current == Scheme.Gamepad)
        {
            string padAction = action == "Interact" ? "Change" : action;
            if (action != "Overview" && System.Array.IndexOf(FlatsControls.PadActions, padAction) < 0) return "";   // FlatsControls.Pad throws for unknown actions
            string label = action == "Overview" ? "Back" : FlatsControls.Label(padAction, true);
            // "LB / L1" when the pad's family is unknown: a key cap keeps the first name only
            if (cap && label != null && label.Contains(" / ")) label = label.Substring(0, label.IndexOf(" / "));
            return label;
        }
        if (action != "Overview" && System.Array.IndexOf(FlatsControls.KeyboardActions, action) < 0) return "";
        return KeyName(action == "Overview" ? KeyCode.Tab : FlatsControls.Keyboard(action), cap);
    }

    /// <summary>Readable key name. Mouse side buttons (Mouse3..Mouse6 in Unity) are "Mouse button 4".."7", the numbering games and
    /// mouse software use; caps shorten them to "M4".</summary>
    public static string KeyName(KeyCode key, bool cap)
    {
        if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6)
        {
            int n = key - KeyCode.Mouse0;
            if (cap) return n == 0 ? "LMB" : n == 1 ? "RMB" : n == 2 ? "MMB" : "M" + (n + 1);
            return n == 0 ? RoguelikeController.T("Left Click") : n == 1 ? RoguelikeController.T("Right Click") : n == 2 ? RoguelikeController.T("Middle Click") : RoguelikeController.T("Mouse button {0}", n + 1);
        }
        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((int)(key - KeyCode.Alpha0)).ToString();
        if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return "Num " + (int)(key - KeyCode.Keypad0);
        switch (key)
        {
            case KeyCode.LeftShift: return "Shift";
            case KeyCode.RightShift: return cap ? "RShift" : "Right Shift";
            case KeyCode.LeftControl: return "Ctrl";
            case KeyCode.RightControl: return cap ? "RCtrl" : "Right Ctrl";
            case KeyCode.LeftAlt: return "Alt";
            case KeyCode.RightAlt: return cap ? "RAlt" : "Right Alt";
            case KeyCode.Space: return cap ? "Space" : RoguelikeController.T("Space");
            case KeyCode.Return: case KeyCode.KeypadEnter: return "Enter";
            case KeyCode.Backspace: return cap ? "Bksp" : "Backspace";
            case KeyCode.Delete: return "Del";
            case KeyCode.Insert: return "Ins";
            case KeyCode.PageUp: return "PgUp";
            case KeyCode.PageDown: return "PgDn";
            case KeyCode.CapsLock: return "Caps";
            case KeyCode.UpArrow: return "Up";
            case KeyCode.DownArrow: return "Down";
            case KeyCode.LeftArrow: return "Left";
            case KeyCode.RightArrow: return "Right";
            case KeyCode.BackQuote: return "`";
            case KeyCode.Minus: return "-";
            case KeyCode.Equals: return "=";
            case KeyCode.LeftBracket: return "[";
            case KeyCode.RightBracket: return "]";
            case KeyCode.Semicolon: return ";";
            case KeyCode.Quote: return "'";
            case KeyCode.Comma: return ",";
            case KeyCode.Period: return ".";
            case KeyCode.Slash: return "/";
            case KeyCode.Backslash: return "\\";
            case KeyCode.Tab: return "Tab";
            case KeyCode.None: return "";
            default: return key.ToString();
        }
    }

    /// <summary>Caption of the Overview button on the run screens: the panel name plus the key that also opens it.</summary>
    public static string OverviewLabel
    {
        get
        {
            var name = RoguelikeController.T("Overview");
            return Current == Scheme.Touch ? name : name + "  " + KeyText("Overview");
        }
    }

    /// <summary>Overview footer line for the active scheme.</summary>
    public static string OverviewFooter(string wallet)
    {
        switch (Current)
        {
            case Scheme.Gamepad: return RoguelikeController.T("Back or B closes   LB or RB switches tabs   Wallet ${0}", wallet);
            case Scheme.Touch: return RoguelikeController.T("Tap a tab to switch   Close with the button   Wallet ${0}", wallet);
            default: return RoguelikeController.T("TAB or Esc closes   Q or E switches tabs   Wallet ${0}", wallet);
        }
    }
}
