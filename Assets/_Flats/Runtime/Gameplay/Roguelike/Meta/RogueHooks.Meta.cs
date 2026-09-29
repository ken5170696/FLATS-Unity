using Flats.Core.Roguelike;
using UnityEngine;

// Entry points of the meta layer for legacy gameplay code. Every one is a no-op (neutral value)
// when the mode is inactive or the player carries no meta runtime, so Classic behaviour is exact.
public static partial class RogueHooks
{
    static RogueMetaRuntime Meta(Component c) { return RoguelikeMode.Active ? RogueMetaRuntime.Of(c) : null; }

    /// <summary>Per round, before the bullet is spawned: spread, damage, cadence and free rounds of the gun in hand and the skills.</summary>
    public static ShotModifiers MetaShot(FPSController player, bool aiming, bool newTriggerPull)
    {
        var m = Meta(player);
        return m == null ? ShotModifiers.Neutral : m.NextRound(aiming, newTriggerPull);
    }

    /// <summary>Seconds to wait before the first round of a trigger pull (a cold machine-gun belt).</summary>
    public static float MetaPreFireDelay(FPSController player)
    {
        var m = Meta(player);
        return m == null ? 0f : m.PreFireDelay();
    }

    /// <summary>The gap the legacy coroutine is about to wait, so sustained-fire rules track the real cadence.</summary>
    public static void MetaNoteInterval(FPSController player, float seconds)
    {
        var m = Meta(player);
        if (m != null) m.NoteInterval(seconds);
    }

    public static void MetaStampBullet(FPSController player, Bullet bullet)
    {
        var m = Meta(player);
        if (m != null) m.StampBullet(bullet);
    }

    /// <summary>Hit-time multiplier of the meta layer for a player's bullet on an enemy (weapon range/elite rules, conditional skills).</summary>
    public static float MetaHitMul(Bullet bullet, DamageReceiver target, bool headshot, Vector3 hitPoint, float damage)
    {
        if (!RoguelikeMode.Active || bullet == null || bullet.shooter == null || target == null || target.userIsPlayer || bullet.grenade || bullet.rogueKind != 0) return 1f;
        var m = RogueMetaRuntime.Of(bullet.shooter);
        return m == null ? 1f : m.OnHit(bullet, target, headshot, Vector3.Distance(bullet.StartPosition, hitPoint), damage);
    }

    /// <summary>Executioner: the damage to apply for a headshot on a wounded regular enemy.</summary>
    public static float MetaExecute(Bullet bullet, DamageReceiver target, float damage, bool headshot)
    {
        if (!RoguelikeMode.Active || bullet == null || bullet.shooter == null || target == null || target.userIsPlayer) return damage;
        var m = RogueMetaRuntime.Of(bullet.shooter);
        return m == null ? damage : m.Execute(target, damage, headshot);
    }

    /// <summary>Speed multiplier for the camera's approach to the sight (1 / aim-in time multiplier).</summary>
    public static float MetaAdsSpeed(FPSController player)
    {
        var m = Meta(player);
        return m == null ? 1f : 1f / Mathf.Max(0.2f, m.AdsTimeMul());
    }

    public static bool MetaCanAim(FPSController player)
    {
        var m = Meta(player);
        return m == null || m.CanAim();
    }

    public static float MetaSwapTimeMul(FPSController player)
    {
        var m = Meta(player);
        return m == null ? 1f : m.SwapTimeMul();
    }

    public static void MetaReloadCompleted(FPSController player, int magazineAfter, int capacity)
    {
        var m = Meta(player);
        if (m != null) m.OnReloadCompleted(magazineAfter, capacity);
    }

    /// <summary>
    /// Spawn: the local player enters a Roguelike run with the active loadout's armory weapons and sights
    /// instead of the Classic character loadout (which is left untouched on disk).
    /// </summary>
    public static void MetaStartLoadout(ref int primary, ref int secondary, ref int primarySight, ref int secondarySight)
    {
        if (!RoguelikeMode.Active) return;
        var loadout = MetaProfiles.ToLoadout(RogueMetaStore.Current);
        var pw = RogueArmory.Weapon(loadout.primary); var sw = RogueArmory.Weapon(loadout.secondary);
        if (pw == null || sw == null || pw.BaseModel == sw.BaseModel) return;
        primary = pw.BaseModel; secondary = sw.BaseModel;
        var ps = RogueArmory.Sight(loadout.primarySight); var ss = RogueArmory.Sight(loadout.secondarySight);
        primarySight = ps != null ? ps.Index : 0; secondarySight = ss != null ? ss.Index : 0;
    }

    /// <summary>The run's heat (0 outside a run), read from the replicated run state.</summary>
    public static int Heat()
    {
        var c = RoguelikeController.Instance;
        return RoguelikeMode.Active && c != null && c.State != null ? c.State.heat : 0;
    }
}
