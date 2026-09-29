using UnityEngine;

/// <summary>
/// One place for the Roguelike mode's own inputs across keyboard, gamepad and touch. Keyboard uses the FLATS
/// bindings (Interact, Shop, Tab), a gamepad uses the pad bindings (hold Change = interact, Shop, Back = overview, bumpers = tabs),
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
    static bool touchInteractPending, touchOverviewPending, touchUltimatePending, touchTacticalPending, touchShopPending;
    static int touchInteractFrame = -1, touchOverviewFrame = -1, touchUltimateFrame = -1, touchTacticalFrame = -1, touchShopFrame = -1;

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
    /// <summary>A dedicated touch shop button, if the HUD authors one; without it the touch Interact button also reopens the shop.</summary>
    public static void TouchShop() { touchShopPending = true; }
    /// <summary>Clears touch state when the HUD goes away so a lost PointerUp never leaves the button "held".</summary>
    public static void ResetTouch()
    {
        touchInteractHeld = false; touchInteractPending = touchOverviewPending = touchUltimatePending = touchTacticalPending = touchShopPending = false;
        touchInteractFrame = touchOverviewFrame = touchUltimateFrame = touchTacticalFrame = touchShopFrame = -1;
    }

    // ---- actions
    // A gamepad has no Interact binding: it interacts by holding the Change button. FPSController switches weapons when Change is
    // released within FPSController.holdTime and treats a longer press as a hold, so the pad interaction starts only once the press
    // has lasted longer than that: a short press only switches weapons, a hold only interacts (X015).
    const float PadHoldMargin = 0.05f;
    static int padFrame = -1;
    static float padHeldSince = -1f;
    static bool padHoldReached, padHoldStartedNow;

    static void TickPadHold()
    {
        if (padFrame == Time.frameCount) return;
        padFrame = Time.frameCount;
        padHoldStartedNow = false;
        if (!FlatsControls.PadState("Change", 0)) { padHeldSince = -1f; padHoldReached = false; return; }
        // game time, like FPSController's own press timer, so a slow-motion kill camera does not split the two apart
        if (padHeldSince < 0f) padHeldSince = Time.time;
        if (!padHoldReached && Time.time - padHeldSince > FPSController.holdTime + PadHoldMargin) { padHoldReached = true; padHoldStartedNow = true; }
    }

    static bool PadInteractHeld { get { TickPadHold(); return padHoldReached; } }
    static bool PadInteractDown { get { TickPadHold(); return padHoldStartedNow; } }

    /// <summary>True when the interaction is a hold of a shared button (gamepad: hold Change), so prompts can say "Hold [X]".</summary>
    public static bool InteractIsHold { get { return Current == Scheme.Gamepad; } }

    public static bool InteractHeld { get { return FlatsControls.Held("Interact") || PadInteractHeld || touchInteractHeld; } }
    public static bool InteractDown { get { return FlatsControls.Down("Interact") || PadInteractDown || Consume(ref touchInteractPending, ref touchInteractFrame); } }

    /// <summary>Reopens the dismissed shop: the bound Shop key or pad button. On a phone the contextual Interact button (or a dedicated
    /// shop button, when one is authored) does it, as there is no other control for it.</summary>
    public static bool ShopDown
    {
        get
        {
            if (FlatsControls.Down("Shop") || FlatsControls.PadState("Shop", 1) || Consume(ref touchShopPending, ref touchShopFrame)) return true;
            return Current == Scheme.Touch && Consume(ref touchInteractPending, ref touchInteractFrame);
        }
    }

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

    // ---- tab switching on the overview and the headquarters. The keys come from the bindings so they never share a key with a
    // gameplay action (Interact is Q and Change is E by default): the first pair with neither key bound. The pad uses the bumpers.
    static readonly KeyCode[] tabPairs = { KeyCode.Q, KeyCode.E, KeyCode.Z, KeyCode.X, KeyCode.LeftBracket, KeyCode.RightBracket, KeyCode.PageUp, KeyCode.PageDown };
    static KeyCode tabPrevious = KeyCode.Z, tabNext = KeyCode.X;
    static int tabKeysFrame = -1;

    static void ResolveTabKeys()
    {
        if (tabKeysFrame == Time.frameCount) return;
        tabKeysFrame = Time.frameCount;
        for (int i = 0; i + 1 < tabPairs.Length; i += 2)
            if (!KeyInUse(tabPairs[i]) && !KeyInUse(tabPairs[i + 1])) { tabPrevious = tabPairs[i]; tabNext = tabPairs[i + 1]; return; }
        tabPrevious = KeyCode.PageUp; tabNext = KeyCode.PageDown;   // every pair bound: keep the page keys
    }

    static bool KeyInUse(KeyCode key)
    {
        if (key == KeyCode.V || key == KeyCode.Tab) return true;   // fixed Roguelike keys: melee and the overview
        foreach (string action in FlatsControls.KeyboardActions) if (FlatsControls.Keyboard(action) == key) return true;
        return false;
    }

    /// <summary>Previous tab: the free keyboard key of the pair or the pad's left bumper.</summary>
    public static bool TabPreviousDown
    {
        get
        {
            ResolveTabKeys();
            var pad = InControl.InputManager.ActiveDevice;
            return (!FlatsControls.Capturing && Input.GetKeyDown(tabPrevious)) || (pad != null && pad.LeftBumper.WasPressed);
        }
    }

    /// <summary>Next tab: the free keyboard key of the pair or the pad's right bumper.</summary>
    public static bool TabNextDown
    {
        get
        {
            ResolveTabKeys();
            var pad = InControl.InputManager.ActiveDevice;
            return (!FlatsControls.Capturing && Input.GetKeyDown(tabNext)) || (pad != null && pad.RightBumper.WasPressed);
        }
    }

    /// <summary>Key cap names of the keyboard tab keys ("Z", "X"), for footers.</summary>
    public static string TabPreviousKey { get { ResolveTabKeys(); return KeyName(tabPrevious, true); } }
    public static string TabNextKey { get { ResolveTabKeys(); return KeyName(tabNext, true); } }

    // ---- key names for prompts. Actions: the FLATS keyboard actions (Interact, Shop, Ultimate, Tactical...) plus "Overview".
    // A gamepad interacts by holding its Change button (the pad has no Interact binding) and opens the overview with Back.

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
            default: return RoguelikeController.T("{0} or Esc closes   {1} / {2} switches tabs   Wallet ${3}", "Tab", TabPreviousKey, TabNextKey, wallet);
        }
    }
}
