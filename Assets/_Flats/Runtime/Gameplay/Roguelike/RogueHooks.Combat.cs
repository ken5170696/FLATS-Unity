using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

// Combat effect chains and world modifiers. Every derived damage goes back through
// DamageReceiver.ApplyDamage, so the same authority path (owner reports, master decides)
// applies; the rules in Flats.Core.Roguelike.EffectChainRules bound depth and budgets.
public static partial class RogueHooks
{
    // QA-11: the ranges come from the rules (RogueCatalog), the same numbers the item descriptions print
    static readonly float ChainRange = (float)RogueCatalog.ChainRange, HomingRange = (float)RogueCatalog.HomingRange, HomingAngle = (float)RogueCatalog.HomingAngleDegrees;
    static readonly int ChainTargets = RogueCatalog.ChainMaxTargets;
    static int worldMask = -1;
    static int WorldMask { get { if (worldMask < 0) worldMask = LayerMask.GetMask("Default"); return worldMask; } }
    /// <summary>Live, undead enemies without a tag search: the role registry every spawned enemy joins.</summary>
    static IEnumerable<RogueEnemyRole> LiveEnemies()
    {
        var all = RogueEnemyRole.All;
        for (int i = 0; i < all.Count; i++)
        {
            var role = all[i]; if (role == null) continue;
            var receiver = role.GetComponent<DamageReceiver>();
            if (receiver == null || receiver.Dead) continue;
            yield return role;
        }
    }

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

        // chain bullets: arc to up to ChainTargets other living enemies within ChainRange in sight of the hit one, at the chain share of the damage, never counted as headshots
        if (rp.ChainBullets && rp.Chain.CanTrigger(new DamageContext(rp.Key, bullet.rogueRootShot), DamageKind.Chain) && rp.Chain.TryStartChain(rp.Key))
        {
            var candidates = new List<ChainCandidate>();
            var byId = new Dictionary<string, RogueEnemyRole>();
            foreach (var other in LiveEnemies())
            {
                // a dying enemy (its AI goes at the end of the frame, its body stays five seconds) is no chain target (QA-11)
                var enemy = other.gameObject;
                if (enemy == target.gameObject || !Targetable(enemy)) continue;
                float d = Vector3.Distance(target.transform.position, enemy.transform.position);
                bool visible = d <= ChainRange && !Physics.Linecast(target.transform.position + Vector3.up * 2f, enemy.transform.position + Vector3.up * 2f, WorldMask);
                string id = enemy.GetInstanceID().ToString();
                byId[id] = other;
                candidates.Add(new ChainCandidate(id, d, visible));
            }
            var chosen = EffectChainRules.ChainTargets(candidates, null, ChainTargets, ChainRange);
            var ctx = new DamageContext(rp.Key, bullet.rogueRootShot).Derived(DamageKind.Chain);
            foreach (var id in chosen)
            {
                RogueEnemyRole hit;
                if (!byId.TryGetValue(id, out hit) || !rp.Chain.TryRegisterDerivedHit(ctx, id)) continue;
                var dr = hit.GetComponent<DamageReceiver>();
                if (dr != null) dr.ApplyDamage(damage * (float)TriggerCoefficients.Chain, -1, bullet.shooter);
                RogueWorldFx.Arc(target.transform.position + Vector3.up * 2f, hit.transform.position + Vector3.up * 2f, bullet.shooter);
            }
            rp.Chain.FinishChain(rp.Key);
        }
    }

    /// <summary>After an enemy hit: continue the bullet through at 60% when the build or the weapon that fired it pierces. Returns true when a new bullet was spawned.</summary>
    public static bool TryPenetrate(Bullet bullet, Collision col)
    {
        if (!RoguelikeMode.Active || bullet == null || bullet.shooter == null) return false;
        var rp = bullet.shooter.GetComponent<RoguePlayer>();
        if (rp == null) return false;
        // the build's depth (Precision, Piercing Rounds) plus the Pierce trait of the weapon that fired this round; every copy
        // that simulates the bullet reads the same replicated build and the same stamped model (WeaponRules.PenetrateDepth)
        var meta = RogueMetaRuntime.Of(bullet.shooter);
        var weapon = meta != null ? meta.WeaponDefForModel(bullet.rogueWeaponModel) : null;
        int nextDepth = bullet.rogueDepth + 1;
        if (!WeaponRules.CanPenetrate(nextDepth, rp.Stats.PenetrateDepth, weapon)) return false;
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
        // QA-49: a shotgun pellet's ricochet keeps the long-range falloff of the distance it flew to the wall (no close bonus)
        SpawnDerived(bullet, origin, reflected, (float)rp.Stats.RicochetDamageMul * (float)rp.Stats.RicochetHitBonus * RicochetRangeMul(bullet, col.contacts[0].point), DamageKind.Ricochet, nextDepth, null);
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
        b.rogueWeaponModel = source.rogueWeaponModel;   // a Pierce weapon's round keeps piercing after its first enemy (TryPenetrate)
        rb.gameObject.layer = source.gameObject.layer;
        rb.linearVelocity = dir * 1500f;
        if (ignore != null) foreach (var c in ignore.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(rb.GetComponent<Collider>(), c);
    }

    /// <summary>Homing bullets: steer toward the head of the most aligned visible enemy inside a 15 degree cone. Called from
    /// Bullet.Update.</summary>
    public static void SteerHoming(Bullet bullet, Rigidbody rb)
    {
        if (!RoguelikeMode.Active || bullet == null || rb == null || bullet.shooter == null || bullet.grenade || bullet.rogueKind != (int)DamageKind.Direct) return;
        var rp = bullet.shooter.GetComponent<RoguePlayer>();
        if (rp == null || !rp.HomingBullets) return;
        Vector3 dir = rb.linearVelocity.normalized;
        // QA-11: a round keeps the enemy it already steers toward while that one stays valid (alive, in the cone and range, in sight);
        // re-picking the best dot every frame let two close enemies trade the round back and forth
        Transform locked; homingLocks.TryGetValue(bullet, out locked);
        if (locked != null && !HomingValid(locked.gameObject, bullet, dir)) locked = null;
        Transform best = locked; float bestDot = 0;
        if (best == null)
            foreach (var candidate in LiveEnemies())   // the role registry instead of a tag search: this runs for every homing bullet every frame
            {
                var enemy = candidate.gameObject;
                if (!HomingValid(enemy, bullet, dir)) continue;
                float dot = Vector3.Dot(dir, (HomingAimPoint(enemy.transform) - bullet.transform.position).normalized);
                if (best == null || dot > bestDot) { best = enemy.transform; bestDot = dot; }
            }
        if (homingLocks.Count > 128) { var stale = new List<Bullet>(); foreach (var k in homingLocks.Keys) if (k == null) stale.Add(k); foreach (var k in stale) homingLocks.Remove(k); }
        if (best == null) { homingLocks.Remove(bullet); return; }
        homingLocks[bullet] = best;
        // QA-42: aim at the head hitbox. The old point (root + 2 m) sat at the knees of the 4x-scale enemies, so homing rounds
        // hit their feet. A round flies about 25 m per frame and crosses the 60 m homing range in two or three frames, so a turn
        // capped at 8 degrees per frame rarely arrived before the round passed; the cone (HomingAngle) already bounds the
        // correction, so the round turns fully onto the head as soon as it locks.
        Vector3 wanted = (HomingAimPoint(best) - bullet.transform.position).normalized;
        Vector3 steered = Vector3.RotateTowards(dir, wanted, HomingAngle * Mathf.Deg2Rad, 0f);
        rb.linearVelocity = steered * rb.linearVelocity.magnitude;
    }

    static readonly Dictionary<Bullet, Transform> homingLocks = new Dictionary<Bullet, Transform>();
    static readonly Dictionary<Transform, Transform> homingHeads = new Dictionary<Transform, Transform>();

    /// <summary>Where a homing round aims on an enemy: the centre of its head hitbox (the "CameraTarget" collider that Bullet
    /// scores as a headshot), else the head bone, else the top of its body.</summary>
    public static Vector3 HomingAimPoint(Transform enemy)
    {
        Transform head;
        if (!homingHeads.TryGetValue(enemy, out head) || head == null)
        {
            head = null;
            foreach (var c in enemy.GetComponentsInChildren<Collider>())
                if (c.gameObject.name == "CameraTarget") { head = c.transform; break; }
            if (head == null) head = enemy.Find(RogueRoleMarker.HeadPath);
            if (homingHeads.Count > 256) { var gone = new List<Transform>(); foreach (var k in homingHeads.Keys) if (k == null) gone.Add(k); foreach (var k in gone) homingHeads.Remove(k); }
            homingHeads[enemy] = head;
        }
        if (head != null)
        {
            var col = head.GetComponent<Collider>();
            return col != null && col.enabled ? col.bounds.center : head.position;
        }
        return RogueInteraction.BodyOf(enemy.gameObject).Head;
    }

    /// <summary>Alive on this copy: not dying (Die ran) and not a kill this client already predicted.</summary>
    static bool Targetable(GameObject enemy)
    {
        var dr = enemy != null ? enemy.GetComponent<DamageReceiver>() : null;
        return dr != null && !dr.Dead && !RogueKillPrediction.IsPredictedDead(enemy);
    }

    /// <summary>Homing rule for one enemy: alive, within HomingRange of the round, inside the HomingAngle cone, and in plain sight.</summary>
    static bool HomingValid(GameObject enemy, Bullet bullet, Vector3 dir)
    {
        if (enemy == null || enemy.GetComponent<AI>() == null || !Targetable(enemy)) return false;
        Vector3 aim = HomingAimPoint(enemy.transform);
        Vector3 to = aim - bullet.transform.position;
        if (to.magnitude > HomingRange) return false;
        if (!EffectChainRules.HomingSteer(Vector3.Dot(dir, to.normalized), HomingAngle)) return false;
        return !Physics.Linecast(bullet.transform.position, aim, WorldMask);
    }

    /// <summary>Demolition core: a kill explodes once (bounded per second), hurting other enemies only.</summary>
    public static void OnKillExplosion(Transform killer, Transform victim, float killingDamage)
    {
        if (!RoguelikeMode.Active || killer == null || victim == null) return;
        var rp = killer.GetComponent<RoguePlayer>();
        if (rp == null || rp.Stats.ExplodeOnKillFraction <= 0 || !rp.IsMine) return;
        if (!rp.Chain.TryStartExplosion(rp.Key, Time.time)) return;
        float radius = (float)rp.Stats.ExplosionRadius;   // the Demolition tier radius (6/7/8 m) times Bigger Boom, as the rules state (it was a fixed 6 m)
        float damage = Mathf.Max(50f, killingDamage) * (float)rp.Stats.ExplodeOnKillFraction;
        RogueWorldFx.KillBlast(victim.position + Vector3.up, radius, killer);   // QA-45: the original grenade blast at this radius, on every client
        foreach (var hit in Physics.OverlapSphere(victim.position, radius, LayerMask.GetMask("BlueTeam")))
        {
            var dr = hit.GetComponentInParent<DamageReceiver>();
            if (dr == null || dr.userIsPlayer || dr.transform == victim || !Targetable(dr.gameObject)) continue;
            // QA-11: walls stop the blast, as they stop grenades (Bullet.ExplosionBlockMask); it used to reach through a building
            if (Physics.Linecast(victim.position + Vector3.up * 2f, dr.transform.position + Vector3.up * 2f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore)) continue;
            dr.ApplyDamage(damage, -1, killer);
            if (rp.Stats.ExplosionSlow > 0) { var role = dr.GetComponent<RogueEnemyRole>(); if (role != null) role.Slow(Time.time + (rp.Stats.ShockwaveSeconds > 0 ? (float)rp.Stats.ShockwaveSeconds : 2f), 1f - (float)rp.Stats.ExplosionSlow); }   // Shockwave tier duration (2/2.5/3 s)
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
