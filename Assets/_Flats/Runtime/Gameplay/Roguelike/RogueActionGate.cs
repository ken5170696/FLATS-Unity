using UnityEngine;

/// <summary>Player actions the Roguelike rules can refuse. PickupWeapon (taking or exchanging a ground weapon) was added last.</summary>
public enum RogueAction { Fire, Aim, Reload, SwitchWeapon, Grenade, Melee, Sprint, Dash, Ultimate, Interact, HoldInteract, Carry, Drop, PickupWeapon }

/// <summary>
/// One permission rule for what a Roguelike player may do right now, read from the player's current activity (F39, QA-14).
/// Keyboard, gamepad and touch all end in the same FPSController coroutines (Shoot, Reload, ChangeWeapons,
/// ThrowGrenade, Smash, Zoom), so the owner's send sites and those coroutine entries ask here; the ground-weapon offer
/// (DroppedGun, FPSController.TryExchangeGroundWeapon), sprint (FPSController.UpdateSprint), dash, ultimate
/// (RoguePlayer) and the Roguelike interactions (RogueInteraction, RogueCarryable) ask here too. Classic modes are
/// never gated: every call returns true when Roguelike is not active.
///
///                  Fire Aim Reload Switch Grenade Melee Sprint Dash Ult Interact Hold Carry Drop Pickup
///   idle / move     Y    Y    Y      Y      Y      Y     Y      Y    Y    Y       Y    Y     -     Y
///   sprint (4)      s    s    Y      Y      Y      Y     Y      s    Y    Y       Y    s     -     Y
///   aiming (5)      Y    Y    s      s      s      s     s      s    Y    Y       s    s     -     s
///   firing (6)      b    Y    b      b      b      b     s      Y    Y    Y       s    s     -     b
///   reloading       b    b    b      b      b      b     Y      Y    Y    Y       s    s     -     b
///   switching       b    b    b      b      b      b     Y      Y    Y    Y       s    Y     -     b
///   dashing         Y    -    Y      Y      Y      Y     s      Y    Y    Y       Y    Y     -     -
///   carrying        -    -    -      -      -      -     Y      -    Y    Y       -    -     Y     -
///   holding (2)     s    s    s      -      s      s     Y      Y    Y    Y       Y    -     -     -
///   melee swing     -    -    -      -      -      -     Y      Y    Y    Y       -    -     -     -
///   menu open (3)   -    -    -      -      -      -     -      -    -    -       -    -     -     -
///   downed          -    -    -      -      -      -     -      -   (1)   -       -    -     -     -
///   dead            no controller: FPSController is destroyed with the body; a respawn starts a new one.
///
///   Y allowed; - refused here; b refused by the weapon itself while it is busy (enableFire is off; a shot's cooldown,
///   a reload, a weapon change), not by this rule; s allowed, and it ends or pauses the activity of that row (see the notes).
///   (1) only an equipped Emergency Revive (crawling is movement, not an action here).
///   (2) a hold interaction (repair, charge, vent, revive) in progress. Combat actions win: firing, aiming,
///       reloading, a grenade or a melee swing cancel the hold (RogueInteraction re-checks it every frame).
///   (3) owner only: the shop, overview, pause or meta hub, or any dialog, and for a moment after one closes
///       (FlatsCursor.GameplayInput is false). A sprint ends (hard stop).
///   (4) sprint is an explicit input state (FPSController.UpdateSprint), never a speed: firing and aiming end it.
///   (5) aiming wins over sprinting: sprint pauses (held) or ends (toggled) while aiming.
///   (6) a held trigger pauses or ends the sprint; a shot from a sprint first raises the weapon (SprintOutSeconds).
///   Switch is also refused for a moment after an Interact press consumed by a Roguelike object, because a
///   gamepad's Change button is both Interact and weapon switch (X015). The same Interact press, or one while a
///   Roguelike target has the player's focus, never also takes a ground weapon (Pickup). Dash is refused while
///   carrying: the carry's price is mobility (Mobility core lifts it through CarrySpeedMul), and a 20 m dash would
///   skip most of it. A dash ends aiming (hold-to-aim takes it up again after the dash) and pauses the sprint.
///   Starting to carry or going down ends aiming, the sprint, the rest of a burst, a reload in progress and the local
///   hold (CancelConflicts); a weapon change or a grenade already sent completes on every copy. Dropping, delivering,
///   a teammate taking the item, the carrier leaving, going down or dying clear the carry state, and every action
///   returns with it: nothing here or in the weapon code is latched by a carry.
///
/// Owner-only conditions (menu, hold, interact window, dash, focus) are unknown to other copies; for them the rule only
/// uses replicated state (downed, carrying, melee swing).
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
        // owner input only while it may act: playing and no screen or dialog open or just closed (FlatsCursor: the click or press
        // that closes a dialog must not also fire, aim or interact)
        if (owner && !FlatsCursor.GameplayInput) return "";
        if (rp != null && rp.Downed) return action == RogueAction.Ultimate && EmergencyReviveEquipped(rp) ? null : DownedReason;
        bool carrying = rp != null && (rp.Carrying || RogueCarryable.IsCarrying(fps.gameObject));
        switch (action)
        {
            case RogueAction.Drop: return carrying ? null : "";
            case RogueAction.Carry: if (carrying) return "You are already carrying something"; break;
            case RogueAction.Fire: case RogueAction.Aim: case RogueAction.Reload: case RogueAction.SwitchWeapon:
            case RogueAction.Grenade: case RogueAction.Melee: case RogueAction.Dash: case RogueAction.HoldInteract:
            case RogueAction.PickupWeapon:
                if (carrying) return CarryingReason; break;
            case RogueAction.Ultimate:
                // a body shield is a defensive tool, not a platform for an ultimate (QA-44); an objective crate keeps the old rule
                if (carrying && RogueBodyShield.CarriedBy(fps.gameObject) != null) return CarryingReason; break;
        }
        var melee = fps.GetComponent<RogueMelee>();
        if (melee != null && melee.Busy)
        {
            switch (action)
            {
                case RogueAction.Fire: case RogueAction.Aim: case RogueAction.Reload: case RogueAction.SwitchWeapon:
                case RogueAction.Grenade: case RogueAction.Melee: case RogueAction.HoldInteract: case RogueAction.Carry:
                case RogueAction.PickupWeapon:
                    return "";
            }
        }
        // a dash ends aiming and cannot scoop up a weapon on the way (the dash's own start calls Zoom(false))
        if (rp != null && rp.Dashing && (action == RogueAction.Aim || action == RogueAction.PickupWeapon)) return "";
        if (owner && RogueInteraction.LocalHolding && (action == RogueAction.SwitchWeapon || action == RogueAction.Carry || action == RogueAction.PickupWeapon)) return "";
        if (owner && action == RogueAction.SwitchWeapon && Time.unscaledTime - interactConsumedAt < SwitchAfterInteractSeconds) return "";
        // Interact (and the pad's held Change) belongs to a focused Roguelike target (a crate, a device, a downed teammate):
        // the same press must not also exchange the ground weapon under the player's feet.
        if (owner && action == RogueAction.PickupWeapon && (RogueInteraction.AnyFocus || Time.unscaledTime - interactConsumedAt < SwitchAfterInteractSeconds)) return "";
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
        if (RogueMelee.MeleePressed()) return true;
        return RogueInput.IsTouch && !ETCInput.GetButton("Fire") && !FPSController.tapFiring;
    }

    /// <summary>
    /// Ends what cannot continue when a player goes down or starts carrying: aiming and the sprint (owner), the rest of a burst
    /// and a reload in progress (every copy; the reload's ammunition is never committed) and the local hold interaction.
    /// RoguePlayer.TryDown and the carry pickup call it.
    /// </summary>
    public static void CancelConflicts(FPSController fps, string reason)
    {
        if (!RoguelikeMode.Active || fps == null) return;
        fps.RogueCancelWeaponConflicts();
        var rp = fps.GetComponent<RoguePlayer>();
        if (rp == null || rp.IsMine) RogueInteraction.CancelLocalHold(reason);
    }
}
