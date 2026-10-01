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

    /// <summary>Hit-time multiplier of the meta layer for a player's bullet on an enemy (shotgun range profile, weapon range/elite
    /// rules, conditional skills). Direct rounds only: every pellet of a shell is scaled by its own distance (QA-49).</summary>
    public static float MetaHitMul(Bullet bullet, DamageReceiver target, bool headshot, Vector3 hitPoint, float damage)
    {
        if (!RoguelikeMode.Active || bullet == null || bullet.shooter == null || target == null || target.userIsPlayer || bullet.grenade || bullet.rogueKind != 0) return 1f;
        var m = RogueMetaRuntime.Of(bullet.shooter);
        if (m == null) return 1f;
        float distance = Vector3.Distance(bullet.StartPosition, hitPoint);
        float range = ShotgunRangeMul(m, bullet.rogueWeaponModel, distance);
        // the weapon and skill rules credit their share of the damage this pellet actually deals
        return range * m.OnHit(bullet, target, headshot, distance, damage * range);
    }

    /// <summary>Shotgun range profile of a player's round (1 for other weapons): the armory row of the model that fired it, or the
    /// catalog model when the loadout has no row for it (a shotgun taken from the ground).</summary>
    static float ShotgunRangeMul(RogueMetaRuntime m, int model, float distance)
    {
        var def = m != null && m.Stats != null ? m.Stats.WeaponForModel(model) : null;
        return (float)WeaponRules.ShotgunRangeMul(distance, def, model);
    }

    /// <summary>A ricochet leaving a player's direct round: the round's shotgun falloff at the bounce point (never the close bonus),
    /// so a far pellet cannot bounce back to full damage. Ricochets of ricochets already carry it.</summary>
    public static float RicochetRangeMul(Bullet bullet, Vector3 bouncePoint)
    {
        if (!RoguelikeMode.Active || bullet == null || bullet.shooter == null || bullet.rogueKind != 0) return 1f;
        return Mathf.Min(1f, ShotgunRangeMul(RogueMetaRuntime.Of(bullet.shooter), bullet.rogueWeaponModel, Vector3.Distance(bullet.StartPosition, bouncePoint)));
    }

    /// <summary>An enemy's shotgun pellet on a player: the far falloff only, never above x1 (QA-49). 1 for other enemy weapons.
    /// Wiring point: Bullet.OnCollisionEnter, enemy round on a player, before ApplyBulletDamage.</summary>
    public static float EnemyShotgunRangeMul(Bullet bullet, Vector3 hitPoint)
    {
        if (!RoguelikeMode.Active || bullet == null || bullet.shooter == null || bullet.grenade) return 1f;
        var ai = bullet.shooter.GetComponent<AI>();
        var gun = ai != null && ai.primaryWeapon != null ? ai.primaryWeapon.GetComponent<Gun>() : null;
        return gun == null ? 1f : (float)WeaponRules.EnemyShotgunRangeMul(Vector3.Distance(bullet.StartPosition, hitPoint), gun.id);
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
    /// <summary>Display only: the local player's armory variant of a legacy model (name and resolved numbers),
    /// so the HUD, the TAB weapon rows and the shop show the weapon that actually fires. Falls back to the model.</summary>
    public static Flats.Core.WeaponDefinition MetaWeaponDisplay(int model, Flats.Core.WeaponDefinition fallback)
    {
        if (!RoguelikeMode.Active || fallback == null) return fallback;
        var rp = Local;
        var v = rp != null && rp.Stats != null ? rp.Stats.WeaponForModel(model) : null;
        if (v == null && rp != null && rp.Build != null && rp.Build.meta != null && !rp.Build.meta.Empty) v = MetaRun.ShopVariant(rp.Build, model);   // shop: the variant a purchase would give
        if (v == null) return fallback;
        var r = RogueArmory.Resolve(v);
        return new Flats.Core.WeaponDefinition(v.Name, r.Magazine, r.Reserve, r.Burst, (float)r.Damage, (float)r.Rpm, (float)r.Accuracy, (float)r.Reload, (float)r.HeadshotBonus,
            fallback.zoom, fallback.oneShot, fallback.handgun, fallback.grenade);
    }

    /// <summary>Display only (QA-49): the localized range profile of the local player's variant of a model, the same variant
    /// <see cref="MetaWeaponDisplay"/> shows ("" for weapons without one). The close and far lines are joined by <paramref name="separator"/>.</summary>
    public static string MetaWeaponRangeText(int model, string separator)
    {
        if (!RoguelikeMode.Active || model < 0 || model >= Flats.Core.WeaponCatalog.Count) return "";
        var rp = Local;
        var v = rp != null && rp.Stats != null ? rp.Stats.WeaponForModel(model) : null;
        if (v == null && rp != null && rp.Build != null && rp.Build.meta != null && !rp.Build.meta.Empty) v = MetaRun.ShopVariant(rp.Build, model);
        var line = v != null ? MetaText.RangeSummary(v) : MetaText.RangeSummary(model);
        return string.IsNullOrEmpty(line.Template) ? "" : RogueMetaUI.L(line).Replace("\n", separator ?? "\n");
    }
}
