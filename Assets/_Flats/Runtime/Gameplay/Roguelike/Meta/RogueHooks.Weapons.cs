using Flats.Core.Roguelike;
using UnityEngine;

// Weapon and explosion entry points added on 2026-10-04 (owner feedback): pellet counts, semi-automatic handguns, explosion
// radius/damage/marks. Like every RogueHooks entry they are neutral when the mode is inactive or the shooter is not a Roguelike
// player, so Classic modes and enemies (AI.cs has its own fire code) keep their exact behaviour.
public static partial class RogueHooks
{
    /// <summary>Pellets one shell of the player's gun fires: the catalog burst count times RogueArmory.ShotgunPelletMul for a pellet
    /// shotgun, plus Choke. The shell still costs <c>gun.burstCount</c> rounds (FPSController.Shoot). Every copy reads the same
    /// replicated build, so each client spawns the same pellets. Enemies never come here (AI.Shoot).</summary>
    public static int ShellPellets(FPSController player, Gun gun)
    {
        if (gun == null) return 1;
        var rp = Of(player);
        if (rp == null || rp.Stats == null) return gun.burstCount;
        return rp.Stats.ShellPellets(gun.oneShot, gun.grenade, gun.burstCount);
    }

    /// <summary>A Roguelike player's handgun: semi-automatic (one trigger pull, one shot or one burst) with the rules' minimum interval.</summary>
    public static bool SemiAuto(FPSController player, Gun gun)
    {
        return RoguelikeMode.Active && player != null && gun != null && gun.handgun && !player.zombie;
    }

    /// <summary>Seconds the Shoot coroutine waits right after a round (the recoil step) for a Roguelike player's gun.</summary>
    public static float RoundStep(FPSController player, Gun gun, float intervalMul)
    {
        // bounded by the gun's own round interval: the 0.1 s step no longer holds a fast automatic weapon at 600 rpm
        return (float)WeaponRules.RoundStep(SemiAuto(player, gun), gun != null ? gun.rpm : 60f, intervalMul);
    }

    /// <summary>The game time the next round of a Roguelike player's gun is due, given when the round firing now was due
    /// (WeaponRules.NextRoundDue): a round up to RoundPaybackSeconds late keeps its place on the clock.</summary>
    public static float NextRoundDue(float due, float interval)
    {
        return (float)WeaponRules.NextRoundDue(Time.time, due, interval);
    }

    /// <summary>Seconds from one round to the next for a Roguelike player's gun (a handgun: the semi-automatic minimum).</summary>
    public static float RoundInterval(FPSController player, Gun gun, float intervalMul)
    {
        return (float)WeaponRules.RoundInterval(SemiAuto(player, gun), gun != null ? gun.rpm : 60f, intervalMul);
    }

    /// <summary>Explosive Payload (mod.frag_grenades): damage multiplier of every explosion this player causes. Applied where the
    /// blast's damage is set: the thrown grenade, the launcher round (FPSController) and the Demolition kill explosion.</summary>
    public static float ExplosionDamageMul(FPSController player)
    {
        var rp = Of(player);
        return rp == null || rp.Stats == null ? 1f : (float)rp.Stats.ExplosionDamageMul;
    }

    /// <summary>Bigger Boom: radius multiplier of a grenade or launcher blast fired by <paramref name="shooter"/> (1 for enemies).
    /// Bullet.Explode runs on every copy and reads the same replicated build, so every client resolves the same blast.</summary>
    public static float ExplosionRadiusMul(Transform shooter)
    {
        if (!RoguelikeMode.Active || shooter == null) return 1f;
        var rp = shooter.GetComponent<RoguePlayer>();
        return rp == null || rp.Stats == null ? 1f : Mathf.Max(1f, (float)rp.Stats.ExplosionRadiusMul);
    }

    /// <summary>Scales the original grenade blast effect to a larger radius (the same treatment RogueWorldFx gives a kill blast).</summary>
    public static void ScaleBlastEffect(GameObject effect, float radiusMul)
    {
        if (effect == null || radiusMul <= 1.001f) return;
        float k = Mathf.Clamp(radiusMul, 1f, (float)BuildStats.MaxBlastEffectScale);
        foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startSizeMultiplier *= k; main.startSpeedMultiplier *= k;
            var shape = ps.shape;
            if (shape.enabled) shape.radius *= k;
            ps.Play(true);
        }
    }

    /// <summary>
    /// A player's grenade or launcher blast caught an enemy (in the radius, in sight of the blast), whether or not the edge still
    /// dealt damage. With the Marker core the blast marks every enemy it catches, for the thrower's own mark time and multiplier
    /// (RogueEnemyRole reads the marking player's BuildStats). It runs on every copy that simulates the grenade, exactly as a bullet's
    /// mark does (OnBulletHitEnemy): the authority's copy marks too, and the authority is where a marked enemy's extra damage is
    /// applied (RogueEnemyRole.ModifyIncomingDamage). Team Radio is credited first, as for a bullet.
    /// </summary>
    public static void OnExplosionCaughtEnemy(Bullet bullet, DamageReceiver target)
    {
        if (!RoguelikeMode.Active || bullet == null || bullet.shooter == null || target == null || target.userIsPlayer || target.Dead) return;
        var rp = bullet.shooter.GetComponent<RoguePlayer>();
        var role = target.GetComponent<RogueEnemyRole>();
        if (rp == null || rp.Stats == null || rp.Build == null || role == null) return;
        if (Controller != null) Controller.OnMarkedEnemyHit(role, rp);
        if (rp.Stats.MarkDuration > 0 && rp.Build.HasCore("core.marker")) role.Mark(Time.time + (float)rp.Stats.MarkDuration, rp);
    }
}
