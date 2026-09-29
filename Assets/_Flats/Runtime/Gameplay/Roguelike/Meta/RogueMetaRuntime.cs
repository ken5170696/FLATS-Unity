using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// Runs the meta loadout on one Flatman: armory weapon numbers on the Gun components, the
/// weapon trait state of the gun in hand, and the mechanic skills. Every number comes from
/// BuildStats / WeaponRules (Flats.Core.Roguelike); this component only keeps time and applies.
/// Lives next to RoguePlayer; the legacy code reaches it only through RogueHooks, guarded by
/// RoguelikeMode.Active, so Classic modes never create or read it.
/// Symmetry: Bind() re-applies from scratch and Unbind() restores the authored Gun values, so a
/// respec, a loadout change, a weapon swap or leaving the run leaves no effect behind.
/// </summary>
public class RogueMetaRuntime : MonoBehaviour
{
    public BuildStats Stats { get; private set; }
    public ContributionLedger Ledger { get; } = new ContributionLedger();
    public bool IsMine { get; private set; }

    FPSController fps;
    DamageReceiver receiver;
    RoguePlayer player;
    readonly Dictionary<int, WeaponTraitState> traitStates = new Dictionary<int, WeaponTraitState>();
    readonly Dictionary<int, RangedWeaponDef> appliedDefs = new Dictionary<int, RangedWeaponDef>();

    // mechanic skill clocks (owner copy)
    bool freshMagazine;
    int berserkerStacks; float berserkerUntil;
    int rhythmStacks; float rhythmUntil;
    float firingSince = -1, lastRoundAt = -10;
    float adrenalUntil, adrenalReadyAt;
    float squadLinkUntil;
    float stillSince; Vector3 lastPosition;
    float aimStillSince;
    float aimRaisedAt = -10f; bool wasZoomed;
    public float AimRaisedAt { get { return aimRaisedAt; } }
    int killsSinceScavenge;
    bool guardianUsedThisStage;
    int lastStageDepth = -1;
    public int MeleeKills { get; private set; }
    /// <summary>Set by the melee swing when it lands; a kill within the window after it counts as a melee kill.</summary>
    public float LastMeleeHit { get; set; } = -10f;
    public bool RecentMeleeHit { get { return Time.time - LastMeleeHit < 0.5f; } }
    /// <summary>Co-op: the authority confirmed a melee kill before the Die RPC arrives; the next kill is that one.</summary>
    public bool PendingMeleeKill { get; set; }
    readonly Dictionary<string, int> weaponKills = new Dictionary<string, int>();

    public static RogueMetaRuntime Of(Component c) { return c != null ? c.GetComponent<RogueMetaRuntime>() : null; }

    public static RogueMetaRuntime Ensure(GameObject go)
    {
        var r = go.GetComponent<RogueMetaRuntime>();
        return r != null ? r : go.AddComponent<RogueMetaRuntime>();
    }

    void Awake()
    {
        fps = GetComponent<FPSController>();
        receiver = GetComponent<DamageReceiver>();
        player = GetComponent<RoguePlayer>();
        IsMine = Menu.network == 0 || (GetComponent<PhotonView>() != null && GetComponent<PhotonView>().isMine);
        Stats = BuildStats.Compute(new PlayerBuild());
        lastPosition = transform.position;
    }

    RogueEffectRowView effectRow;

    void Start()
    {
        // the local player's HUD shows its own effects as they fire
        if (!IsMine) return;
        var ui = GameObject.Find("UI");
        if (ui != null) effectRow = RogueEffectRowView.Open(ui.transform);
    }

    // ------------------------------------------------------------------ binding
    /// <summary>Called by RoguePlayer.ApplyBuild with the freshly computed stats (every copy, so every client fires the same numbers).</summary>
    public void Bind(BuildStats stats)
    {
        Stats = stats ?? BuildStats.Compute(new PlayerBuild());
        ApplyGuns();
    }

    /// <summary>Restores every Gun this component changed to its authored catalog values and removes skins.</summary>
    public void Unbind()
    {
        if (fps == null || fps.primaryWeapons == null) return;
        foreach (var index in new List<int>(appliedDefs.Keys)) RestoreGun(index);
        appliedDefs.Clear();
        traitStates.Clear();
        Stats = BuildStats.Compute(new PlayerBuild());
    }

    void OnDestroy() { Unbind(); if (effectRow != null) Destroy(effectRow.gameObject); }

    /// <summary>Resolved base magazine for a model index (armory override or catalog). RoguePlayer applies build multipliers on top.</summary>
    public int BaseMagazine(int modelIndex)
    {
        var def = Stats != null ? Stats.WeaponForModel(modelIndex) : null;
        return def != null ? RogueArmory.Resolve(def).Magazine : GunInfo.limitAmmo[modelIndex];
    }

    public int BaseReserve(int modelIndex)
    {
        var def = Stats != null ? Stats.WeaponForModel(modelIndex) : null;
        return def != null ? RogueArmory.Resolve(def).Reserve : GunInfo.limitMaxAmmo[modelIndex];
    }

    void ApplyGuns()
    {
        if (fps == null || fps.primaryWeapons == null) return;
        int count = Mathf.Min(fps.primaryWeapons.childCount, Flats.Core.WeaponCatalog.Count);
        for (int i = 0; i < count; i++)
        {
            var def = Stats.WeaponForModel(i);
            RangedWeaponDef old; appliedDefs.TryGetValue(i, out old);
            if (def == null) { if (old != null) RestoreGun(i); continue; }
            var gun = fps.primaryWeapons.GetChild(i).GetComponent<Gun>();
            if (gun == null) continue;
            var r = RogueArmory.Resolve(def);
            gun.damage = (float)r.Damage;
            gun.rpm = (float)r.Rpm;
            gun.accuracy = (float)r.Accuracy;
            gun.reloadTime = (float)r.Reload;
            gun.headshotBonus = (float)r.HeadshotBonus;
            gun.burstCount = r.Burst;
            var sight = Stats.SightForModel(i);
            gun.zoom = sight != null ? (float)sight.Magnification : 1f;   // aimed look scale follows the chosen sight, not the weapon's legacy cap
            appliedDefs[i] = def;
            if (old != def) traitStates[i] = new WeaponTraitState(def, sight);
            ApplySkin(i, def);
        }
    }

    void RestoreGun(int i)
    {
        if (fps == null || fps.primaryWeapons == null || i >= fps.primaryWeapons.childCount) return;
        var gun = fps.primaryWeapons.GetChild(i).GetComponent<Gun>();
        if (gun != null)
        {
            gun.damage = GunInfo.damage[i]; gun.rpm = GunInfo.rpm[i]; gun.accuracy = GunInfo.accuracy[i];
            gun.reloadTime = GunInfo.reloadTime[i]; gun.headshotBonus = GunInfo.headshotBonus[i];
            gun.burstCount = GunInfo.burstCount[i]; gun.zoom = GunInfo.zoom[i];
        }
        ClearSkin(i);
        appliedDefs.Remove(i);
        traitStates.Remove(i);
    }

    // Skins are authored attachment prefabs (RogueWeaponSkin, Codex B). Until they exist the call is a no-op.
    void ApplySkin(int i, RangedWeaponDef def)
    {
        RogueWeaponSkinBridge.Apply(fps.primaryWeapons.GetChild(i), def);
        if (fps.secondaryWeapons != null && i < fps.secondaryWeapons.childCount) RogueWeaponSkinBridge.Apply(fps.secondaryWeapons.GetChild(i), def);
    }

    void ClearSkin(int i)
    {
        RogueWeaponSkinBridge.Clear(fps.primaryWeapons.GetChild(i));
        if (fps.secondaryWeapons != null && i < fps.secondaryWeapons.childCount) RogueWeaponSkinBridge.Clear(fps.secondaryWeapons.GetChild(i));
    }

    // ------------------------------------------------------------------ gun in hand
    public int HandModel { get { return fps != null ? fps.primaryWeaponIndex : -1; } }
    public RangedWeaponDef HandDef { get { RangedWeaponDef d; return appliedDefs.TryGetValue(HandModel, out d) ? d : null; } }
    public SightDef HandSight { get { return Stats != null ? Stats.SightForModel(HandModel) : null; } }
    public bool HandFrenzy { get { var st = HandState; return st != null && st.FrenzyActive; } }
    WeaponTraitState HandState { get { WeaponTraitState s; return traitStates.TryGetValue(HandModel, out s) ? s : null; } }
    Gun HandGun { get { return fps != null && fps.primaryWeapon != null ? fps.primaryWeapon.GetComponent<Gun>() : null; } }

    /// <summary>Per round, on every copy that runs the Shoot RPC. Deterministic from the replicated build and the same RPC sequence.</summary>
    public ShotModifiers NextRound(bool aiming, bool newTriggerPull)
    {
        var gun = HandGun;
        var ctx = new ShotContext
        {
            Now = Time.time, Aiming = aiming, Moving = Moving, NewTriggerPull = newTriggerPull,
            RoundsInMagazine = gun != null ? gun.currentAmmo : 0, MagazineCapacity = gun != null ? gun.limitAmmo : 0,
            AimedStillSeconds = aiming && aimStillSince > 0 ? Time.time - aimStillSince : 0,
        };
        var state = HandState;
        var m = state != null ? state.NextRound(ctx) : ShotModifiers.Neutral;
        // skills that shape the round
        if (!aiming) m.SpreadMul *= Stats.HipSpreadMul;
        else
        {
            float settle = (float)WeaponRules.SettleSpread(Time.time - aimRaisedAt, AdsTimeMul());   // unsteady right after raising the sight
            if (settle > 1f) m.SpreadMul *= settle;
        }
        if (aiming && Stats.SteadyBreathSpread > 0 && !Moving && ctx.AimedStillSeconds >= Stats.SteadyBreathSeconds) m.SpreadMul *= 1 - Stats.SteadyBreathSpread;
        if (newTriggerPull) aimStillSince = aiming ? Time.time : 0;   // firing restarts the patient/steady clock
        if (firingSince < 0 || Time.time - lastRoundAt > 0.6f) firingSince = Time.time;
        lastRoundAt = Time.time;
        return m;
    }

    public void NoteInterval(float seconds) { var s = HandState; if (s != null) s.NoteInterval(seconds); }

    public float PreFireDelay() { var s = HandState; return s != null ? (float)s.PreFireDelay(Time.time) : 0f; }

    /// <summary>Stamps a spawned bullet with what the hit will need (fresh-magazine headshot, the weapon that fired it).</summary>
    public void StampBullet(Bullet b)
    {
        if (b == null) return;
        b.rogueWeaponModel = HandModel;
        if (freshMagazine && Stats.FreshMagazineHeadshot) { b.rogueForceHeadshot = true; freshMagazine = false; Ledger.Trigger("sk.fresh_mag"); }
    }

    bool Moving { get { return fps != null && (transform.position - lastPosition).sqrMagnitude > 0.0004f; } }

    // ------------------------------------------------------------------ hits
    /// <summary>
    /// Hit-time multiplier from the meta layer (weapon distance/elite rules, opening shot, conditional skill envelope) plus the
    /// enemy effects of the weapon and skills, requested through the authority. Returns the multiplier; credits contributions.
    /// </summary>
    public float OnHit(Bullet bullet, DamageReceiver target, bool headshot, float distance, float damageBefore)
    {
        if (bullet == null || target == null) return 1f;
        RangedWeaponDef def; appliedDefs.TryGetValue(bullet.rogueWeaponModel, out def);
        var role = target.GetComponent<RogueEnemyRole>();
        bool elite = role != null && (role.Elite || role.RoleId == "role.finale");
        var h = WeaponRules.OnHit(def, distance, headshot, elite);
        double mul = h.DamageMul;
        if (h.IgnoreFrontReduction && role != null) mul *= FrontReductionRefund(role, bullet.shooter);
        if (h.DamageMul > 1) Ledger.Damage(h.Source, damageBefore * mul, h.DamageMul);
        // conditional meta skills, one envelope
        var c = CombatState();
        c.OpeningShot = Stats.OpeningShotBonus > 0 && FirstHit(target);
        double meta = Stats.MetaDamageMul(c);
        if (meta > 1) Ledger.Damage(MetaSource(c), damageBefore * mul * meta, meta);
        mul *= meta;
        if (IsMine)
        {
            // weapon-trait stun, slow and knockback are requested by RogueRangedStatus (one request per hit); skills add their own slow
            if (Stats.HitSlowFraction > 0) { RogueEnemyStatus.Request(target.gameObject, 0f, (float)Stats.HitSlowFraction, (float)Stats.HitSlowSeconds, Vector3.zero, transform); Ledger.Trigger("sk.suppressive"); }
            if (h.StunSeconds > 0 || h.KnockbackMeters > 0 || h.SlowFraction > 0) Ledger.Trigger(h.Source);
            if (h.MagazineRefund > 0) Refund(h.MagazineRefund, h.Source);
        }
        if (headshot) OnHeadshot();
        return (float)mul;
    }

    // spawn health per enemy, recorded at the first hit this client saw (enemies spawn at full health)
    static readonly Dictionary<int, float> spawnHealth = new Dictionary<int, float>();
    static bool FirstHit(DamageReceiver target)
    {
        int id = target.GetInstanceID();
        if (spawnHealth.ContainsKey(id)) return false;
        spawnHealth[id] = target.hitPoints;
        return true;
    }
    static float SpawnHealth(DamageReceiver target) { float hp; return spawnHealth.TryGetValue(target.GetInstanceID(), out hp) ? hp : target.hitPoints; }
    public static void ResetFreshTargets() { spawnHealth.Clear(); }

    /// <summary>Armor Breaker: undo the shield bearer's frontal reduction that RogueEnemyRole.ModifyIncomingDamage will apply (same 60 degree test).</summary>
    static float FrontReductionRefund(RogueEnemyRole role, Transform shooter)
    {
        if (role.Def == null || role.Def.FrontReduction <= 0 || shooter == null) return 1f;
        Vector3 toShooter = shooter.position - role.transform.position; toShooter.y = 0;
        return Vector3.Angle(role.transform.forward, toShooter) <= 60f ? 1f / (1f - (float)role.Def.FrontReduction) : 1f;
    }

    /// <summary>Executioner: a headshot finishes a wounded regular enemy. Returns the damage to apply (at least the remaining health).</summary>
    public float Execute(DamageReceiver target, float damage, bool headshot)
    {
        if (!headshot || Stats.ExecuteBelow <= 0 || target == null) return damage;
        var role = target.GetComponent<RogueEnemyRole>();
        if (role == null || role.Elite || role.RoleId == "role.finale") return damage;
        float max = SpawnHealth(target);
        if (target.hitPoints - damage <= 0 || target.hitPoints > max * (float)Stats.ExecuteBelow) return damage;
        Ledger.Damage("sk.executioner", target.hitPoints + 1f, (target.hitPoints + 1f) / Mathf.Max(1f, damage));
        return target.hitPoints + 1f;
    }

    void OnHeadshot()
    {
        var s = HandState; if (s != null) s.OnHeadshot();
        if (Stats.RhythmStep > 0)
        {
            rhythmStacks = Time.time < rhythmUntil ? Mathf.Min(BuildStats.MaxSkillStacks, rhythmStacks + 1) : 1;
            rhythmUntil = Time.time + (float)Stats.RhythmWindow;
        }
    }

    BuildStats.MetaCombat CombatState()
    {
        return new BuildStats.MetaCombat
        {
            BerserkerStacks = Time.time < berserkerUntil ? berserkerStacks : 0,
            RhythmStacks = Time.time < rhythmUntil ? rhythmStacks : 0,
            Shredding = Stats.ShredderBonus > 0 && firingSince > 0 && Time.time - lastRoundAt < 0.6f && Time.time - firingSince >= Stats.ShredderSeconds,
            SquadLinked = Time.time < squadLinkUntil,
            BelowHalfHealth = HealthFraction() < 0.5f,
            HoldingLine = Stats.HoldLineReduction > 0 && Time.time - stillSince >= Stats.HoldLineSeconds,
        };
    }

    string MetaSource(BuildStats.MetaCombat c)
    {
        if (c.BerserkerStacks > 0) return "sk.berserker";
        if (c.RhythmStacks > 0) return "sk.rhythm";
        if (c.Shredding) return "sk.shredder";
        if (c.OpeningShot) return "sk.opening_shot";
        if (c.SquadLinked) return "sk.squad_link";
        return "";
    }

    // ------------------------------------------------------------------ incoming damage
    float rescueShield, rescueShieldUntil;
    /// <summary>Rescue Shield: a bubble after a revive (both players). Replaced, never stacked.</summary>
    public void GrantRescueShield(float points, float seconds)
    {
        if (points <= 0 || seconds <= 0) return;
        rescueShield = points; rescueShieldUntil = Time.time + seconds;
        RogueMetaFeedback.Pulse(this, "sk.rescue_shield");
    }

    /// <summary>Meta reduction of incoming damage (Rescue Shield, Juggernaut, Hold the Line), bounded by the envelope. Credits the absorbed amount.</summary>
    public float IncomingDamage(float damage)
    {
        if (damage <= 0) return damage;
        if (rescueShield > 0 && Time.time < rescueShieldUntil)
        {
            float absorbed = Mathf.Min(rescueShield, damage);
            rescueShield -= absorbed; damage -= absorbed;
            Ledger.Absorbed("sk.rescue_shield", absorbed);
            if (damage <= 0) return 0f;
        }
        var c = CombatState();
        double mul = Stats.MetaDamageTakenMul(c);
        if (mul < 1) Ledger.Absorbed(c.BelowHalfHealth && Stats.JuggernautReduction > 0 ? "sk.juggernaut" : "sk.hold_line", damage * (1 - mul));
        float after = damage * (float)mul;
        // Adrenal Rush and solo Squad Link fire on the way down
        if (IsMine)
        {
            float frac = HealthFraction(), next = receiver != null ? Mathf.Max(0, receiver.hitPoints - after) / Mathf.Max(1f, MaxHealth()) : frac;
            if (Stats.AdrenalSeconds > 0 && frac >= BuildStats.AdrenalThreshold && next < BuildStats.AdrenalThreshold && Time.time >= adrenalReadyAt)
            {
                adrenalUntil = Time.time + (float)Stats.AdrenalSeconds; adrenalReadyAt = Time.time + (float)BuildStats.AdrenalCooldown;
                Ledger.Trigger("sk.adrenal"); RogueMetaFeedback.Pulse(this, "sk.adrenal");
            }
            if (Menu.network == 0 && Stats.SquadLinkSeconds > 0 && frac >= BuildStats.SquadLinkSoloThreshold && next < BuildStats.SquadLinkSoloThreshold) TriggerSquadLink();
        }
        return after;
    }

    /// <summary>Guardian Angel: once per stage a lethal hit leaves the player standing. True when it fired.</summary>
    public bool TryGuardian()
    {
        if (!IsMine || Stats.GuardianSeconds <= 0) return false;
        var ctrl = RoguelikeController.Instance;
        int depth = ctrl != null && ctrl.State != null ? ctrl.State.depth : 0;
        if (depth != lastStageDepth) { lastStageDepth = depth; guardianUsedThisStage = false; }
        if (guardianUsedThisStage) return false;
        guardianUsedThisStage = true;
        if (receiver != null) receiver.hitPoints = 1f;
        StartCoroutine(Invulnerable((float)Stats.GuardianSeconds));
        Ledger.Trigger("sk.guardian"); RogueMetaFeedback.Pulse(this, "sk.guardian");
        return true;
    }

    System.Collections.IEnumerator Invulnerable(float seconds)
    {
        DamageReceiver.invincibility = true;
        yield return new WaitForSeconds(seconds);
        DamageReceiver.invincibility = false;
    }

    public void TriggerSquadLink()
    {
        if (Stats.SquadLinkSeconds <= 0) return;
        squadLinkUntil = Time.time + (float)Stats.SquadLinkSeconds;
        Ledger.Trigger("sk.squad_link"); RogueMetaFeedback.Pulse(this, "sk.squad_link");
    }

    // ------------------------------------------------------------------ movement, aim, swap, reload
    float affixSlowUntil, affixSlowFactor = 1f;
    /// <summary>An elite Suppressor's hit: the strongest active slow wins, the latest expiry wins.</summary>
    public void ApplyAffixSlow(float fraction, float seconds)
    {
        if (Time.time >= affixSlowUntil) affixSlowFactor = 1f;
        affixSlowFactor = Mathf.Min(affixSlowFactor, 1f - Mathf.Clamp01(fraction));
        affixSlowUntil = Mathf.Max(affixSlowUntil, Time.time + seconds);
        RogueMetaFeedback.Pulse(this, "af.suppressor");
    }

    /// <summary>Movement multiplier that depends on the moment: the gun in hand (Run and Gun / heavy), Adrenal Rush, Squad Link, an elite's slow.</summary>
    public float MoveSpeedMul()
    {
        float m = (float)WeaponRules.MoveSpeedMul(HandDef);
        if (Time.time < affixSlowUntil) m *= affixSlowFactor;
        if (Time.time < adrenalUntil) m *= 1f + (float)Stats.AdrenalSpeed;
        if (Time.time < squadLinkUntil) m *= 1f + (float)Stats.SquadLinkBonus;
        return m;
    }

    /// <summary>Aim-in time multiplier (skills × weapon × sight); the camera approaches the sight 1/x as fast.</summary>
    public float AdsTimeMul()
    {
        var sight = HandSight;
        return (float)(Stats.AdsTimeMul * WeaponRules.AdsTimeMul(HandDef, sight != null ? sight.AdsTimeMul : 1));
    }

    public bool CanAim() { return WeaponRules.CanAim(HandDef); }

    /// <summary>Swap time: the slower of the two weapons involved decides (a Quick Draw handgun still waits for a slow-swap launcher).</summary>
    public float SwapTimeMul()
    {
        RangedWeaponDef other = null;
        if (fps != null) appliedDefs.TryGetValue(fps.secondaryWeaponIndex, out other);
        return (float)(Stats.SwapTimeMul * System.Math.Max(WeaponRules.SwapTimeMul(HandDef), WeaponRules.SwapTimeMul(other)));
    }

    /// <summary>Multiplier of the reload in progress, fixed when it started (RogueHooks.ReloadTimeMul reads it).</summary>
    public float PendingReloadMul { get; private set; } = 1f;

    /// <summary>Reload start: weapon trait and Endless Belt decide this reload's time. Berserker stacks end with a reload.</summary>
    public void PrepareReload(int magazineBefore, int capacity) { PendingReloadMul = ReloadTimeMul(magazineBefore, capacity); }

    float ReloadTimeMul(int magazineBefore, int capacity)
    {
        double m = WeaponRules.ReloadTimeMul(HandDef, magazineBefore <= 0);
        if (Stats.EndlessBeltMul < 1 && capacity > 0 && magazineBefore * 2 >= capacity) { m *= Stats.EndlessBeltMul; Ledger.Trigger("sk.endless_belt"); }
        berserkerStacks = 0;
        return (float)m;
    }

    public void OnReloadCompleted(int magazineAfter, int capacity)
    {
        PendingReloadMul = 1f;
        var s = HandState; if (s != null) s.OnReloadCompleted();
        if (Stats.FreshMagazineHeadshot && capacity > 0 && magazineAfter >= capacity) freshMagazine = true;
    }

    // ------------------------------------------------------------------ kills
    /// <summary>The owner's kill, with how it was made. Refunds, stacks, marks, statistics.</summary>
    public void OnKill(Transform victim, bool headshot, bool melee)
    {
        if (!IsMine) return;
        // solo: the swing's damage call is still on the stack; co-op: the authority's confirmation came first
        melee = melee || RogueMeleeAuthority.MeleeHit || PendingMeleeKill;
        PendingMeleeKill = false;
        if (melee)
        {
            MeleeKills++;
            Count(Stats.MeleeWeaponDef != null ? Stats.MeleeWeaponDef.Id : "");
            // Brawler's refill is done by RogueMeleeStats.ConfirmKill (the authority-confirmed melee kill)
        }
        else
        {
            var def = HandDef;
            Count(def != null ? def.Id : "");
            var s = HandState;
            if (s != null) { int back = s.OnKill(); if (back > 0) Refund(back, def.Id); if (s.FrenzyActive) Ledger.Trigger(def.Id); }
        }
        if (Stats.KillReloadFraction > 0) RefillFromReserve(Stats.KillReloadFraction, "sk.kill_reload");
        if (Stats.BerserkerStep > 0)
        {
            berserkerStacks = Time.time < berserkerUntil ? Mathf.Min(BuildStats.MaxSkillStacks, berserkerStacks + 1) : 1;
            berserkerUntil = Time.time + (float)BuildStats.BerserkerWindow;
        }
        if (Stats.ScavengerEvery > 0 && ++killsSinceScavenge >= Stats.ScavengerEvery)
        {
            killsSinceScavenge = 0;
            var gun = HandGun;
            if (gun != null) { int add = Mathf.CeilToInt(gun.limitMaxAmmo * (float)Stats.ScavengerFraction); gun.maxAmmo = Mathf.Min(gun.limitMaxAmmo, gun.maxAmmo + add); Ledger.Trigger("sk.scavenger"); RogueMetaFeedback.Pulse(this, "sk.scavenger"); }
        }
        if (headshot && Stats.HeadshotKillMarkRadius > 0 && victim != null)
        {
            var rp = player;
            int marked = 0;
            foreach (var enemy in GameObject.FindGameObjectsWithTag("Enemy"))
            {
                if (enemy.transform == victim || Vector3.Distance(enemy.transform.position, victim.position) > Stats.HeadshotKillMarkRadius) continue;
                var role = enemy.GetComponent<RogueEnemyRole>();
                if (role != null && rp != null) { role.Mark(Time.time + (float)Stats.HeadshotKillMarkSeconds, rp); marked++; }
            }
            if (marked > 0) { Ledger.Trigger("sk.spotter_eye", marked); RogueMetaFeedback.Pulse(this, "sk.spotter_eye"); }
        }
    }

    void Count(string id) { if (string.IsNullOrEmpty(id)) return; int n; weaponKills.TryGetValue(id, out n); weaponKills[id] = n + 1; }

    public string[] WeaponKillEntries()
    {
        var list = new List<string>();
        foreach (var kv in weaponKills) list.Add(kv.Key + "|" + kv.Value);
        return list.ToArray();
    }

    void Refund(int rounds, string source)
    {
        var gun = HandGun;
        if (gun == null || rounds <= 0) return;
        int before = gun.currentAmmo;
        gun.currentAmmo = Mathf.Min(gun.limitAmmo, gun.currentAmmo + rounds);
        if (gun.currentAmmo > before) { Ledger.Trigger(source); RogueMetaFeedback.Pulse(this, source); }
    }

    void RefillFromReserve(double fraction, string source)
    {
        var gun = HandGun;
        if (gun == null) return;
        int want = Mathf.Min(gun.limitAmmo - gun.currentAmmo, Mathf.CeilToInt(gun.limitAmmo * (float)fraction));
        int moved = Mathf.Min(want, gun.maxAmmo);
        if (moved <= 0) return;
        gun.currentAmmo += moved; gun.maxAmmo -= moved;
        Ledger.Trigger(source); RogueMetaFeedback.Pulse(this, source);
    }

    // ------------------------------------------------------------------ revive
    public float ReviveHealthFraction() { if (Stats.ReviveHealthFraction > 0.3) Ledger.Trigger("sk.second_wind"); return (float)Stats.ReviveHealthFraction; }

    // ------------------------------------------------------------------ bookkeeping
    float MaxHealth() { return player != null ? player.MaxHealth() : 1000f; }
    float HealthFraction() { return receiver != null ? Mathf.Clamp01(receiver.hitPoints / Mathf.Max(1f, MaxHealth())) : 1f; }

    void Update()
    {
        if (!IsMine) return;
        bool moved = (transform.position - lastPosition).sqrMagnitude > 0.0004f;
        if (moved) { stillSince = Time.time; if (aimStillSince > 0) aimStillSince = Time.time; }
        if (fps != null && fps.isZoom) { if (aimStillSince <= 0) aimStillSince = Time.time; if (!wasZoomed) aimRaisedAt = Time.time; } else aimStillSince = 0;
        wasZoomed = fps != null && fps.isZoom;
        lastPosition = transform.position;
    }

    /// <summary>Run end / scene exit: timers and stacks end; the ledger stays readable until the result screen took it.</summary>
    public void CancelAll()
    {
        berserkerStacks = 0; rhythmStacks = 0; adrenalUntil = 0; squadLinkUntil = 0; freshMagazine = false; firingSince = -1;
        foreach (var s in traitStates.Values) s.Reset();
    }
}
