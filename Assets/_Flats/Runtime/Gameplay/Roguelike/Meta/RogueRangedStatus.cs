using Flats.Core.Roguelike;
using UnityEngine;

public sealed class RogueRangedStatus : MonoBehaviour
{
    RangedWeaponDef weapon;
    public static void Capture(Bullet bullet)
    {
        if (bullet == null || bullet.shooter == null) return;
        if (bullet.GetComponent<RogueRangedStatus>() != null) return;
        var player = bullet.shooter.GetComponent<RoguePlayer>();
        var fc = bullet.shooter.GetComponent<FPSController>();
        if (player == null || fc == null || fc.MeleeCurrentGun == null) return;
        var snapshot = bullet.GetComponent<RogueRangedStatus>();
        if (snapshot == null) snapshot = bullet.gameObject.AddComponent<RogueRangedStatus>();
        snapshot.weapon = player.Stats.WeaponForModel(fc.MeleeCurrentGun.id);
    }
    public static void OnHit(Bullet bullet, DamageReceiver target, bool headshot)
    {
        var player = bullet.shooter.GetComponent<RoguePlayer>();
        var fc = bullet.shooter.GetComponent<FPSController>();
        if (player == null || !player.IsMine || fc == null || fc.MeleeCurrentGun == null || bullet.rogueKind != 0) return;
        var snapshot = bullet.GetComponent<RogueRangedStatus>();
        var def = snapshot != null ? snapshot.weapon : player.Stats.WeaponForModel(fc.MeleeCurrentGun.id);
        var role = target.GetComponent<RogueEnemyRole>();
        var effects = WeaponRules.OnHit(def, Vector3.Distance(bullet.shooter.position, target.transform.position), headshot, role != null && (role.Elite || role.RoleId == "role.finale"));
        RogueEnemyStatus.Request(target.gameObject, (float)effects.StunSeconds, (float)effects.SlowFraction, (float)effects.SlowSeconds,
            (target.transform.position - bullet.shooter.position).normalized * (float)effects.KnockbackMeters, bullet.shooter);
    }
}
