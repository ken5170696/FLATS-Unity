using UnityEngine;

/// <summary>Player actions the Roguelike rules can refuse.</summary>
public enum RogueAction { Fire, Aim, Reload, SwitchWeapon, Grenade, Melee, Sprint, Dash, Ultimate, Interact, HoldInteract, Carry, Drop }

/// <summary>
/// One permission rule for what a Roguelike player may do right now, read from the player's current activity (F39).
/// Keyboard, gamepad and touch all end in the same FPSController coroutines (Shoot, Reload, ChangeWeapons,
/// ThrowGrenade, Smash, Zoom), so the owner's send sites and those coroutine entries ask here. Classic modes are
/// never gated: every call returns true when Roguelike is not active.
///
///                    Fire Aim Reload Switch Grenade Melee Sprint Dash Ult Interact Hold Carry Drop
///   normal            Y    Y    Y      Y      Y      Y     Y      Y    Y    Y       Y    Y     -
///   carrying          -    -    -      -      -      -     Y      -    Y    Y       -    -     Y
///   downed            -    -    -      -      -      -     -      -   (1)   -       -    -     -
///   holding (2)       Y    Y    Y      -      Y      Y     Y      Y    Y    Y       Y    -     -
///   melee swing       -    -    -      -      -      -     Y      Y    Y    Y       -    -     -
///   menu open (3)     -    -    -      -      -      -     -      -    -    -       -    -     -
///
///   (1) only an equipped Emergency Revive (crawling is movement, not an action here).
///   (2) a hold interaction (repair, charge, vent, revive) in progress. Combat actions win: firing, aiming,
///       reloading, a grenade or a melee swing cancel the hold (RogueInteraction re-checks it every frame).
///   (3) owner only: the shop, overview, pause or meta hub (Menu.current is not "Playing").
///   Switch is also refused for a moment after an Interact press consumed by a Roguelike object, because a
///   gamepad's Change button is both Interact and weapon switch (X015). Dash is refused while carrying: the carry's
///   price is mobility (Mobility core lifts it through CarrySpeedMul), and a 20 m dash would skip most of it.
///
/// Owner-only conditions (menu, hold, interact window) are unknown to other copies; for them the rule only uses
/// replicated state (downed, carrying, melee swing).
/// </summary>
public static class RogueActionGate
{
    public const float SwitchAfterInteractSeconds = 0.4f;
    const string CarryingReason = "Put down what you are carrying first";
    const string DownedReason = "Can't do that while down";

    static float interactConsumedAt = -10f;
    static int meleeRequestFrame = -10;

    public static bool Allows(FPSController fps, RogueAction action) { return Refusal(fps, action) == null; }

    /// <summary>Null when the action is allowed; otherwise "" (refuse silently) or an English translation key saying why.</summary>
    public static string Refusal(FPSController fps, RogueAction action)
    {
        if (!RoguelikeMode.Active || fps == null) return null;
        var rp = fps.GetComponent<RoguePlayer>();
        bool owner = rp != null ? rp.IsMine : Menu.network == 0;
        if (owner && Menu.current != "Playing") return "";
        if (rp != null && rp.Downed) return action == RogueAction.Ultimate && EmergencyReviveEquipped(rp) ? null : DownedReason;
        bool carrying = rp != null && (rp.Carrying || RogueCarryable.IsCarrying(fps.gameObject));
        switch (action)
        {
            case RogueAction.Drop: return carrying ? null : "";
            case RogueAction.Carry: if (carrying) return "You are already carrying something"; break;
            case RogueAction.Fire: case RogueAction.Aim: case RogueAction.Reload: case RogueAction.SwitchWeapon:
            case RogueAction.Grenade: case RogueAction.Melee: case RogueAction.Dash: case RogueAction.HoldInteract:
                if (carrying) return CarryingReason; break;
        }
        var melee = fps.GetComponent<RogueMelee>();
        if (melee != null && melee.Busy)
        {
            switch (action)
            {
                case RogueAction.Fire: case RogueAction.Aim: case RogueAction.Reload: case RogueAction.SwitchWeapon:
                case RogueAction.Grenade: case RogueAction.Melee: case RogueAction.HoldInteract: case RogueAction.Carry:
                    return "";
            }
        }
        if (owner && RogueInteraction.LocalHolding && (action == RogueAction.SwitchWeapon || action == RogueAction.Carry)) return "";
        if (owner && action == RogueAction.SwitchWeapon && Time.unscaledTime - interactConsumedAt < SwitchAfterInteractSeconds) return "";
        return null;
    }

    static bool EmergencyReviveEquipped(RoguePlayer rp)
    {
        var ctrl = RoguelikeController.Instance;
        var me = ctrl != null && ctrl.State != null ? ctrl.State.Player(rp.Key) : null;
        return me != null && me.build != null && me.build.ultimate == "ult.emergency_revive";
    }

    /// <summary>A Roguelike object consumed an Interact press (pickup, drop, reopen): the same pad press must not also switch weapons.</summary>
    public static void NoteInteractConsumed() { interactConsumedAt = Time.unscaledTime; }

    /// <summary>An explicit melee input (the melee key, pad button or touch melee button) is about to call Smash this frame.</summary>
    public static void NoteMeleeRequest(FPSController fps) { meleeRequestFrame = Time.frameCount; }

    /// <summary>
    /// Owner side of Smash: true when this frame's Smash comes from a melee input rather than from Fire near an object
    /// (the legacy auto-smash, X5). RogueMelee/RogueMeleeHUD may call NoteMeleeRequest; without it the melee bindings
    /// are read directly. On touch, the melee button is the only Smash that is not a held Fire button or a fire tap.
    /// </summary>
    public static bool ExplicitMeleeThisFrame(FPSController fps)
    {
        if (meleeRequestFrame == Time.frameCount) return true;
        var melee = fps != null ? fps.GetComponent<RogueMelee>() : null;
        if (melee == null) return false;
        if (Input.GetKeyDown(melee.MeleeKey)) return true;
        var pad = InControl.InputManager.ActiveDevice;
        if (pad != null && pad.GetControl(melee.MeleeButton).WasPressed) return true;
        return RogueInput.IsTouch && !ETCInput.GetButton("Fire") && !FPSController.tapFiring;
    }

    /// <summary>
    /// Ends what cannot continue when a player goes down or starts carrying: aiming (owner), a reload in progress (every
    /// copy; its ammunition is never committed) and the local hold interaction. RoguePlayer.TryDown and the carry pickup call it.
    /// </summary>
    public static void CancelConflicts(FPSController fps, string reason)
    {
        if (!RoguelikeMode.Active || fps == null) return;
        fps.RogueCancelWeaponConflicts();
        var rp = fps.GetComponent<RoguePlayer>();
        if (rp == null || rp.IsMine) RogueInteraction.CancelLocalHold(reason);
    }
}
