using Flats.Core.Roguelike;
using UnityEngine;

public sealed class RogueRangedStatus : MonoBehaviour
{
    RangedWeaponDef weapon;
    /// <summary>The armory row that gun model fires with on this player (RogueMetaRuntime: the loadout's row, or the model's original
    /// for a gun taken from the ground); the loadout's own row while the runtime is not there yet.</summary>
    static RangedWeaponDef AppliedDef(RoguePlayer player, int model)
    {
        var meta = RogueMetaRuntime.Of(player);
        return meta != null ? meta.WeaponDefForModel(model) : player.Stats.WeaponForModel(model);
    }
    public static void Capture(Bullet bullet)
    {
        if (bullet == null || bullet.shooter == null) return;
        // a hand-thrown grenade was not fired by the gun in hand (no weapon stamp): it must not carry that gun's on-hit trait
        // (a Viper in hand made thrown grenades slow, a Grenade Launcher made them stun)
        if (bullet.grenade && bullet.rogueWeaponModel < 0) return;
        if (bullet.GetComponent<RogueRangedStatus>() != null) return;
        var player = bullet.shooter.GetComponent<RoguePlayer>();
        var fc = bullet.shooter.GetComponent<FPSController>();
        if (player == null || fc == null || fc.MeleeCurrentGun == null) return;
        var snapshot = bullet.GetComponent<RogueRangedStatus>();
        if (snapshot == null) snapshot = bullet.gameObject.AddComponent<RogueRangedStatus>();
        snapshot.weapon = AppliedDef(player, fc.MeleeCurrentGun.id);
    }
    public static void OnHit(Bullet bullet, DamageReceiver target, bool headshot)
    {
        var player = bullet.shooter.GetComponent<RoguePlayer>();
        var fc = bullet.shooter.GetComponent<FPSController>();
        if (player == null || !player.IsMine || fc == null || fc.MeleeCurrentGun == null || bullet.rogueKind != 0) return;
        var snapshot = bullet.GetComponent<RogueRangedStatus>();
        var def = snapshot != null ? snapshot.weapon : AppliedDef(player, fc.MeleeCurrentGun.id);
        var role = target.GetComponent<RogueEnemyRole>();
        var effects = WeaponRules.OnHit(def, Vector3.Distance(bullet.shooter.position, target.transform.position), headshot, role != null && (role.Elite || role.RoleId == "role.finale"));
        // an ordinary round carries no status: no request (in co-op that was one RPC to the master for every hit)
        if (effects.StunSeconds <= 0 && (effects.SlowFraction <= 0 || effects.SlowSeconds <= 0) && effects.KnockbackMeters <= 0) return;
        RogueEnemyStatus.Request(target.gameObject, (float)effects.StunSeconds, (float)effects.SlowFraction, (float)effects.SlowSeconds,
            (target.transform.position - bullet.shooter.position).normalized * (float)effects.KnockbackMeters, bullet.shooter);
    }
}
