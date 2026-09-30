using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

// Combat effect chains and world modifiers. Every derived damage goes back through
// DamageReceiver.ApplyDamage, so the same authority path (owner reports, master decides)
// applies; the rules in Flats.Core.Roguelike.EffectChainRules bound depth and budgets.
public static partial class RogueHooks
{
    const float ChainRange = 10f, ExplosionRadius = 6f, HomingRange = 60f;

    /// <summary>A player bullet hit an enemy. Runs on every client that simulates the bullet; derived damage is reported only by the owner (ApplyDamage rule).</summary>
    public static void OnBulletHitEnemy(Bullet bullet, DamageReceiver target, float damage, bool headshot)
    {
        if (!RoguelikeMode.Active || bullet == null || bullet.shooter == null || target == null) return;
        if (RoguelikeMode.Active) RogueRangedStatus.OnHit(bullet, target, headshot);
        var rp = bullet.shooter.GetComponent<RoguePlayer>();
        if (rp == null) return;
        var role = target.GetComponent<RogueEnemyRole>();
        if (role != null) role.lastHitDamage = damage;
        rp.OnHit(string.IsNullOrEmpty(bullet.rogueTrigger) ? bullet.rogueRootShot : bullet.rogueTrigger);   // pellets, ricochets and penetrations of one trigger pull share one suppression stack
        // direct hits mark only with the Marker core; ricochets mark only with Angle Finder (which alone must not turn every
        // direct hit into a team-wide mark)
        bool ricochet = bullet.rogueKind == (int)DamageKind.Ricochet;
        if (role != null && Controller != null) Controller.OnMarkedEnemyHit(role, rp);   // Team Radio credits the current marker before a re-mark (authority only)
        if (rp.Stats.MarkDuration > 0 && role != null && (ricochet ? rp.Build.HasMod("mod.angle_finder") : rp.Build.HasCore("core.marker")))
            role.Mark(Time.time + (float)rp.Stats.MarkDuration, rp);
        if (bullet.rogueKind != (int)DamageKind.Direct) return;   // derived hits never trigger more effects of the same family beyond the rules below

        // chain bullets: arc to up to three other enemies within 10 m at 50% damage, never counted as headshots
        if (rp.ChainBullets && rp.Chain.CanTrigger(new DamageContext(rp.Key, bullet.rogueRootShot), DamageKind.Chain) && rp.Chain.TryStartChain(rp.Key))
        {
            var candidates = new List<ChainCandidate>();
            foreach (var enemy in GameObject.FindGameObjectsWithTag("Enemy"))
            {
                if (enemy == target.gameObject || enemy.GetComponent<AI>() == null) continue;
                float d = Vector3.Distance(target.transform.position, enemy.transform.position);
                bool visible = d <= ChainRange && !Physics.Linecast(target.transform.position + Vector3.up * 2f, enemy.transform.position + Vector3.up * 2f, LayerMask.GetMask("Default"));
                candidates.Add(new ChainCandidate(enemy.GetInstanceID().ToString(), d, visible));
            }
            var chosen = EffectChainRules.ChainTargets(candidates, null, 3, ChainRange);
            var ctx = new DamageContext(rp.Key, bullet.rogueRootShot).Derived(DamageKind.Chain);
            foreach (var id in chosen)
                foreach (var enemy in GameObject.FindGameObjectsWithTag("Enemy"))
                    if (enemy.GetInstanceID().ToString() == id && rp.Chain.TryRegisterDerivedHit(ctx, id))
                    {
                        var dr = enemy.GetComponent<DamageReceiver>();
                        if (dr != null) dr.ApplyDamage(damage * (float)TriggerCoefficients.Chain, -1, bullet.shooter);
                        RogueWorldFx.Arc(target.transform.position + Vector3.up * 2f, enemy.transform.position + Vector3.up * 2f, bullet.shooter);
                    }
            rp.Chain.FinishChain(rp.Key);
        }
    }

    /// <summary>After an enemy hit: continue the bullet through at 60% when the build pierces. Returns true when a new bullet was spawned.</summary>
    public static bool TryPenetrate(Bullet bullet, Collision col)
    {
        if (!RoguelikeMode.Active || bullet == null || bullet.shooter == null) return false;
        var rp = bullet.shooter.GetComponent<RoguePlayer>();
        if (rp == null || rp.Stats.PenetrateDepth <= 0) return false;
        int nextDepth = bullet.rogueDepth + 1;
        if (!rp.Chain.CanTrigger(DamageKind.Penetrate, nextDepth)) return false;
        var rb = bullet.GetComponent<Rigidbody>();
        Vector3 dir = rb != null && rb.linearVelocity.sqrMagnitude > 1f ? rb.linearVelocity.normalized : bullet.transform.forward;
        Vector3 origin = col.contacts[0].point + dir * 1.5f;
        SpawnDerived(bullet, origin, dir, (float)TriggerCoefficients.PenetrateSecondTarget, DamageKind.Penetrate, nextDepth, col.transform.root);
        return true;
    }

    /// <summary>Wall hit: bounce once (or twice with the mod) at the build's ricochet damage. Returns true when a bounce was spawned.</summary>
    public static bool TryRicochet(Bullet bullet, Collision col)
    {
        if (!RoguelikeMode.Active || bullet == null || bullet.shooter == null || bullet.grenade) return false;
        var rp = bullet.shooter.GetComponent<RoguePlayer>();
        if (rp == null || rp.Stats.RicochetBounces <= 0) return false;
        int nextDepth = bullet.rogueDepth + 1;
        if (!rp.Chain.CanTrigger(DamageKind.Ricochet, nextDepth)) return false;
        var rb = bullet.GetComponent<Rigidbody>();
        Vector3 dir = rb != null && rb.linearVelocity.sqrMagnitude > 1f ? rb.linearVelocity.normalized : bullet.transform.forward;
        Vector3 reflected = Vector3.Reflect(dir, col.contacts[0].normal);
        Vector3 origin = col.contacts[0].point + reflected * 0.5f;
        SpawnDerived(bullet, origin, reflected, (float)rp.Stats.RicochetDamageMul * (float)rp.Stats.RicochetHitBonus, DamageKind.Ricochet, nextDepth, null);
        return true;
    }

    static void SpawnDerived(Bullet source, Vector3 origin, Vector3 dir, float fraction, DamageKind kind, int depth, Transform ignore)
    {
        var fps = source.shooter.GetComponent<FPSController>();
        if (fps == null || fps.bullet == null) return;
        var rb = Object.Instantiate(fps.bullet, origin, Quaternion.LookRotation(dir)) as Rigidbody;
        if (rb == null) return;
        var b = rb.GetComponent<Bullet>();
        b.shooter = source.shooter;
        b.damage = source.damage * fraction;
        b.rogueKind = (int)kind; b.rogueDepth = depth; b.rogueRootShot = source.rogueRootShot; b.rogueTrigger = source.rogueTrigger;
        rb.gameObject.layer = source.gameObject.layer;
        rb.linearVelocity = dir * 1500f;
        if (ignore != null) foreach (var c in ignore.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(rb.GetComponent<Collider>(), c);
    }

    /// <summary>Homing bullets: steer toward the nearest visible enemy inside a 15 degree cone. Called from Bullet.Update.</summary>
    public static void SteerHoming(Bullet bullet, Rigidbody rb)
    {
        if (!RoguelikeMode.Active || bullet == null || rb == null || bullet.shooter == null || bullet.grenade || bullet.rogueKind != (int)DamageKind.Direct) return;
        var rp = bullet.shooter.GetComponent<RoguePlayer>();
        if (rp == null || !rp.HomingBullets) return;
        Vector3 dir = rb.linearVelocity.normalized;
        Transform best = null; float bestDot = 0;
        foreach (var enemy in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            if (enemy.GetComponent<AI>() == null) continue;
            Vector3 to = enemy.transform.position + Vector3.up * 2f - bullet.transform.position;
            if (to.magnitude > HomingRange) continue;
            float dot = Vector3.Dot(dir, to.normalized);
            if (!EffectChainRules.HomingSteer(dot, 15)) continue;
            if (Physics.Linecast(bullet.transform.position, enemy.transform.position + Vector3.up * 2f, LayerMask.GetMask("Default"))) continue;
            if (best == null || dot > bestDot) { best = enemy.transform; bestDot = dot; }
        }
        if (best == null) return;
        Vector3 wanted = (best.position + Vector3.up * 2f - bullet.transform.position).normalized;
        Vector3 steered = Vector3.RotateTowards(dir, wanted, 8f * Mathf.Deg2Rad * Time.deltaTime * 60f, 0f);   // bounded turn rate
        rb.linearVelocity = steered * rb.linearVelocity.magnitude;
    }

    /// <summary>Demolition core: a kill explodes once (bounded per second), hurting other enemies only.</summary>
    public static void OnKillExplosion(Transform killer, Transform victim, float killingDamage)
    {
        if (!RoguelikeMode.Active || killer == null || victim == null) return;
        var rp = killer.GetComponent<RoguePlayer>();
        if (rp == null || rp.Stats.ExplodeOnKillFraction <= 0 || !rp.IsMine) return;
        if (!rp.Chain.TryStartExplosion(rp.Key, Time.time)) return;
        float radius = ExplosionRadius * (float)rp.Stats.ExplosionRadiusMul;
        float damage = Mathf.Max(50f, killingDamage) * (float)rp.Stats.ExplodeOnKillFraction;
        RogueWorldFx.Burst(victim.position + Vector3.up, radius, killer);
        foreach (var hit in Physics.OverlapSphere(victim.position, radius, LayerMask.GetMask("BlueTeam")))
        {
            var dr = hit.GetComponentInParent<DamageReceiver>();
            if (dr == null || dr.userIsPlayer || dr.transform == victim) continue;
            dr.ApplyDamage(damage, -1, killer);
            if (rp.Stats.ExplosionSlow > 0) { var role = dr.GetComponent<RogueEnemyRole>(); if (role != null) role.Slow(Time.time + 2f, 1f - (float)rp.Stats.ExplosionSlow); }
        }
    }

    // ---------------------------------------------------------------- world modifiers read by legacy code
    /// <summary>Gravity multiplier for the falling part of a Roguelike airborne arc (see FPSController); jumps rise unchanged.</summary>
    public const float FallGravityMul = 2f;

    public static float GravityScale(FPSController player)
    {
        if (!RoguelikeMode.Active || RoguelikeController.Instance == null) return 1f;
        return RoguelikeController.Instance.GravityScaleAt(player.transform.position);
    }

    /// <summary>A planted lure crate pulls every enemy's attention. Null when none is active.</summary>
    public static Transform LureTarget()
    {
        return RoguelikeMode.Active && RoguelikeController.Instance != null ? RoguelikeController.Instance.LureTarget : null;
    }

    /// <summary>Power reroute halves enemy sight for its duration.</summary>
    public static float EnemyRangeScale()
    {
        return RoguelikeMode.Active && RoguelikeController.Instance != null && RoguelikeController.Instance.PowerRerouted ? 0.5f : 1f;
    }

    public static bool JumpBlocked(FPSController player)
    {
        var rp = Of(player);
        return rp != null && rp.Downed;
    }

    /// <summary>A downed player cannot melee either (the smash paths do not look at enableFire). Checked on every copy.</summary>
    public static bool MeleeBlocked(FPSController player) { return JumpBlocked(player); }

    /// <summary>The desktop fire send site asks before dispatching: the whole Roguelike fire rule (carrying, downed, menu, melee swing).</summary>
    public static bool CarryingBlocksFire(FPSController player)
    {
        return !RogueActionGate.Allows(player, RogueAction.Fire);
    }

    /// <summary>Ammo crate: reserves back to the build's reserve capacity on every carried weapon; magazines are untouched (no free rounds in the chamber).</summary>
    public static void RefillAmmo(FPSController fps)
    {
        if (fps == null || fps.primaryWeapons == null) return;
        for (int i = 0; i < fps.primaryWeapons.childCount; i++) { var gun = fps.primaryWeapons.GetChild(i).GetComponent<Gun>(); if (gun != null) gun.maxAmmo = gun.limitMaxAmmo; }
    }

    /// <summary>Weapon purchase: swap the primary through the existing ExchangeWeapons contract (solo direct, co-op via a scene gun the authority spawns).</summary>
    public static System.Collections.IEnumerator EquipWeapon(RoguelikeController controller, FPSController fps, int index)
    {
        if (fps == null || index < 0 || index >= Flats.Core.WeaponCatalog.Count) yield break;
        if (Menu.network == 0)
        {
            fps.StartCoroutine("ExchangeWeapons", new int[5] { index, GunInfo.limitAmmo[index], GunInfo.limitMaxAmmo[index], 0, 0 });
            yield break;
        }
        // co-op: ask the authority for a scene gun; it answers with an "equip" event carrying the view id
        controller.Command(new RogueCommandMessage { kind = "objective", text = "equip:" + index });
    }

    /// <summary>Bullets that hit a world object with RogueDamageable (device, carrier, drone) route their damage to it.</summary>
    public static bool OnBulletHitWorld(Bullet bullet, Collision col)
    {
        if (!RoguelikeMode.Active || col == null) return false;
        var damageable = col.collider.GetComponentInParent<RogueDamageable>();
        if (damageable == null) return false;
        damageable.Hit(bullet.damage, bullet.shooter);
        return true;
    }
}
