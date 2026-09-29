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
    static bool touchInteractPending, touchOverviewPending;
    static int touchInteractFrame = -1, touchOverviewFrame = -1;

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
    /// <summary>Clears touch state when the HUD goes away so a lost PointerUp never leaves the button "held".</summary>
    public static void ResetTouch() { touchInteractHeld = false; touchInteractPending = touchOverviewPending = false; touchInteractFrame = touchOverviewFrame = -1; }

    // ---- actions
    public static bool InteractHeld { get { return FlatsControls.Held("Interact") || FlatsControls.PadState("Change", 0) || touchInteractHeld; } }
    public static bool InteractDown { get { return FlatsControls.Down("Interact") || FlatsControls.PadState("Change", 1) || Consume(ref touchInteractPending, ref touchInteractFrame); } }

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
    public static string InteractLabel
    {
        get
        {
            if (Current == Scheme.Touch) return RoguelikeController.T("Interact");
            var label = FlatsControls.Label("Interact", Current == Scheme.Gamepad);
            return string.IsNullOrEmpty(label) ? RoguelikeController.T("Interact") : label;
        }
    }

    /// <summary>Caption of the Overview button on the run screens: the panel name plus the key that also opens it.</summary>
    public static string OverviewLabel
    {
        get
        {
            var name = RoguelikeController.T("Overview");
            switch (Current)
            {
                case Scheme.Gamepad: return name + "  Back";
                case Scheme.Touch: return name;
                default: return name + "  TAB";
            }
        }
    }

    /// <summary>Short key hint for footer notes; empty on touch, where the button itself is the hint.</summary>
    public static string OverviewHint
    {
        get
        {
            switch (Current)
            {
                case Scheme.Gamepad: return RoguelikeController.T("Back: overview");
                case Scheme.Touch: return "";
                default: return RoguelikeController.T("TAB: overview");
            }
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
