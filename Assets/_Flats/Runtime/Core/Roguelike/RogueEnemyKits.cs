using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    public enum EnemyTactical { None = 0, Dash = 1, Shield = 2 }

    /// <summary>
    /// What one enemy carries besides its role: the Brawler's melee weapon, the Rogue Operative's player-style build (cores, an armory
    /// gun, a melee weapon, stat tiers, a tactical). Every copy derives the same kit from the run id and the enemy's instance id
    /// (the same scheme as <see cref="EliteAffixes.For"/>), so the authority's choice is what every client shows and nothing extra
    /// is replicated.
    /// </summary>
    public sealed class EnemyKit
    {
        public string RoleId = "", MeleeId = "", RangedId = "";
        public string[] Cores = new string[0];
        public int HealthTier, DamageTier, SpeedTier;
        public EnemyTactical Tactical = EnemyTactical.None;

        public MeleeDef Melee { get { return RogueArmory.MeleeWeapon(MeleeId); } }
        public RangedWeaponDef Ranged { get { return RogueArmory.Weapon(RangedId); } }
        public bool HasCore(string id) { return Array.IndexOf(Cores, id) >= 0; }

        public double HealthMul { get { return 1 + EnemyOperativeRules.HealthPerTier * HealthTier; } }
        public double DamageMul { get { return 1 + EnemyOperativeRules.DamagePerTier * DamageTier; } }
        public double SpeedMul
        {
            get
            {
                double m = 1 + EnemyOperativeRules.SpeedPerTier * SpeedTier;
                if (HasCore("core.mobility")) m *= 1 + EnemyOperativeRules.MobilitySpeed;
                var melee = Melee;
                // a melee-only enemy carries its weapon's weight all the time, as the player does
                if (RangedId.Length == 0 && melee != null) m *= melee.MoveMul;
                return m;
            }
        }
    }

    public static class EnemyKits
    {
        public const string BrawlerRole = "role.brawler", OperativeRole = "role.operative";
        /// <summary>Stat tiers an operative may hold in one stat, and in total.</summary>
        public const int MaxStatTier = 4, MaxStatPoints = 9;

        // instance ids start again at 1 on every stage: without the stage in the seed, enemy number N carried the same weapon all run
        static RogueRng Rng(string runId, int instanceId, string stageKey)
        {
            ulong h = 14695981039346656037UL;
            string seed = (runId ?? "") + "#kit#" + instanceId + (string.IsNullOrEmpty(stageKey) ? "" : "#" + stageKey);
            foreach (char c in seed) { h ^= c; h = unchecked(h * 1099511628211UL); }
            return new RogueRng(h);
        }

        /// <summary>Armory guns an operative may carry: the fourteen models the legacy AI can hold as a primary (no LMG, no launcher).</summary>
        public static List<RangedWeaponDef> OperativeGuns()
        {
            var list = new List<RangedWeaponDef>();
            foreach (var w in RogueArmory.Ranged)
                if (w.BaseModel >= 0 && w.BaseModel <= 13 && w.Class != WeaponClass.LMG && w.Class != WeaponClass.Launcher) list.Add(w);
            return list;
        }

        /// <summary>Cores an operative may roll. Ricochet needs a second player to arc to, so a solo run never rolls it.</summary>
        public static List<string> OperativeCores(int players)
        {
            var list = new List<string>();
            foreach (var c in RogueCatalog.Cores) if (c.Id != "core.ricochet" || players >= 2) list.Add(c.Id);
            return list;
        }

        /// <summary>Stat points of an operative: one more per chapter, bounded.</summary>
        public static int StatPoints(int depth) { return Math.Min(MaxStatPoints, 1 + RogueDepth.ChapterOf(depth)); }

        /// <summary>Cores an operative carries: one, and two from chapter 4.</summary>
        public static int CoreCount(int depth) { return RogueDepth.ChapterOf(depth) >= 4 ? 2 : 1; }

        /// <summary>The kit of one enemy; null for a role that carries none. Deterministic from the arguments.</summary>
        public static EnemyKit For(string runId, int instanceId, string roleId, int depth, int players)
        { return For(runId, instanceId, roleId, depth, players, ""); }

        /// <summary>The kit of one enemy of one stage. <paramref name="stageKey"/> names the stage (a value every copy holds, such as
        /// the depth and the encounter id); empty keeps the seed of the overload without it.</summary>
        public static EnemyKit For(string runId, int instanceId, string roleId, int depth, int players, string stageKey)
        {
            var def = RogueCatalog.Role(roleId);
            if (def == null || (!def.Melee && !def.Operative)) return null;
            var rng = Rng(runId, instanceId, stageKey);
            var kit = new EnemyKit { RoleId = roleId };
            kit.MeleeId = RogueArmory.Melee[rng.Range(0, RogueArmory.Melee.Length)].Id;
            if (!def.Operative) return kit;

            var guns = OperativeGuns();
            if (guns.Count > 0) kit.RangedId = guns[rng.Range(0, guns.Count)].Id;
            var pool = OperativeCores(players);
            int cores = Math.Min(CoreCount(depth), pool.Count);
            var picked = new List<string>();
            while (picked.Count < cores) { int i = rng.Range(0, pool.Count); picked.Add(pool[i]); pool.RemoveAt(i); }
            kit.Cores = picked.ToArray();
            int points = StatPoints(depth);
            for (int guard = 0; points > 0 && guard < 64; guard++)
            {
                int stat = rng.Range(0, 3);
                if (stat == 0 && kit.HealthTier < MaxStatTier) { kit.HealthTier++; points--; }
                else if (stat == 1 && kit.DamageTier < MaxStatTier) { kit.DamageTier++; points--; }
                else if (stat == 2 && kit.SpeedTier < MaxStatTier) { kit.SpeedTier++; points--; }
            }
            kit.Tactical = kit.HasCore("core.mobility") || rng.Chance(0.5) ? EnemyTactical.Dash : EnemyTactical.Shield;
            return kit;
        }

        /// <summary>How many operatives one stage may field (the director replaces the rest with riflemen).</summary>
        public static int OperativeStageCap(int depth, int players) { return RogueDepth.ChapterOf(depth) >= 4 || players >= 3 ? 2 : 1; }
        /// <summary>Brawlers one wave may hold: one in chapter 1 (the player meets the role before it comes in groups), three after.</summary>
        public const int BrawlersPerWave = 3, BrawlersPerWaveChapter1 = 1;
        public static int BrawlerWaveCap(int depth) { return RogueDepth.ChapterOf(depth) <= 1 ? BrawlersPerWaveChapter1 : BrawlersPerWave; }
        /// <summary>Sampling weights (the rifleman is 3, the common roles 1).</summary>
        public const double BrawlerSampleWeight = 0.9, OperativeSampleWeight = 0.25;
    }

    /// <summary>
    /// A melee weapon in an enemy's hand. MeleeDef numbers are tuned for a player hitting enemies (380-950 and more, x2.5 or more
    /// from behind); against a player they are converted here to a share of the player's base health, so the weapon keeps its
    /// character (light and fast, heavy and slow, backstab, slam) without one-shotting anyone. Timing, reach, knockback and the
    /// specials are derived from the same weapon row.
    /// </summary>
    public static class EnemyMeleeRules
    {
        /// <summary>The player's health before defence and Vitality (DamageReceiver): shares below are of this number.</summary>
        public const double PlayerBaseHealth = 1000;
        /// <summary>A light weapon (Damage at or below LightDamage) takes LightShare of base health per hit, a heavy one (HeavyDamage or more) HeavyShare.</summary>
        public const double LightShare = 0.20, HeavyShare = 0.35, LightDamage = 240, HeavyDamage = 700;
        /// <summary>Ground slam multiplier; caps of the weapon's own backstab (S1) and combo finisher (S2) multipliers when an enemy uses them.</summary>
        public const double SlamShareMul = 1.5, BackstabCap = 2.0, FinisherCap = 1.6, ThrownShareMul = 0.8;
        /// <summary>No single hit exceeds MaxShare before depth scaling, nor HardCapShare after it.</summary>
        public const double MaxShare = 0.60, HardCapShare = 0.80;
        /// <summary>Every hit of a combo weapon (the katana's three) together takes at most this share, after every multiplier.</summary>
        public const double ComboTotalShare = 0.70;
        /// <summary>Frontal damage reduction of an enemy's deflecting weapon (the katana) during each wind-up: the enemy riot shield's, not a full block.</summary>
        public const double DeflectFrontReduction = 0.6;
        /// <summary>Share of the stage's enemy damage growth (RogueDepth.EnemyDamage relative to depth 1, Normal) a melee hit takes.</summary>
        public const double DepthShare = 0.5;

        public const double TelegraphBase = 0.38, TelegraphPerWindup = 1.6, MinTelegraph = 0.30, ComboFollowScale = 0.6;
        public const double RecoveryBase = 0.30, RecoveryPerRecovery = 1.5, MinRecovery = 0.25;
        /// <summary>Reach (m, root to root) is the weapon's Range plus this; the strike only lands inside StrikeHalfAngle of the direction
        /// locked LockLeadSeconds before the hit, and within VerticalReach of the attacker's height.</summary>
        public const double ReachPadding = 1.5, StrikeHalfAngle = 55, LockLeadSeconds = 0.18, VerticalReach = 4.5;
        public const double KnockbackScale = 0.6, KnockbackCap = 6;
        /// <summary>Shock on a player: a heavy slow, never a loss of control. Slam: a lighter, shorter slow.</summary>
        public const double ShockSlow = 0.7, ShockSecondsScale = 0.8, ShockMinSeconds = 0.6, ShockMaxSeconds = 1.5, SlamSlow = 0.5, SlamSlowSeconds = 0.8;
        public const double GuardReductionScale = 0.6, GuardReductionCap = 0.6, GuardHalfAngle = 60, DeflectHalfAngle = 70;
        public const double ThrowMinRange = 9, ThrowMaxRangeCap = 30, ThrowSpeed = 24, ThrowHitRadius = 1.8, ThrowTelegraph = 0.8, ThrowRearmSeconds = 4.5;
        public const int MaxComboHits = 3, MaxFlurryStacks = 3;

        static double Clamp01(double v) { return v < 0 ? 0 : v > 1 ? 1 : v; }

        public static double BaseShare(MeleeDef d)
        {
            if (d == null) return 0;
            return LightShare + (HeavyShare - LightShare) * Clamp01((d.Damage - LightDamage) / (HeavyDamage - LightDamage));
        }

        /// <summary>Share of the player's base health one hit takes at depth 1, before role and kit multipliers.</summary>
        public static double HitShare(MeleeDef d, bool finisher, bool fromBehind, bool thrown)
        {
            if (d == null) return 0;
            double s = BaseShare(d);
            if (d.Special == MeleeSpecial.GroundSlam) s *= SlamShareMul;
            if (d.Special == MeleeSpecial.Backstab && fromBehind) s *= Math.Max(1, Math.Min(d.S1, BackstabCap));
            if (d.Special == MeleeSpecial.Combo) s = ComboShare(d, s, finisher);
            if (thrown) s *= ThrownShareMul;
            return Math.Min(MaxShare, s);
        }

        /// <summary>One hit of a combo weapon. The whole combo (every hit landing) takes at most <see cref="ComboTotalShare"/>: when the
        /// weapon's own numbers add up to more, the plain hits come down (not below a light hit) and the finisher takes what is left.</summary>
        static double ComboShare(MeleeDef d, double plain, bool finisher)
        {
            int hits = ComboHits(d);
            double fin = Math.Max(1, Math.Min(d.S2, FinisherCap));
            if (hits <= 1) return Math.Min(ComboTotalShare, finisher ? plain * fin : plain);
            double weights = hits - 1 + fin;
            if (plain * weights > ComboTotalShare) plain = Math.Max(Math.Min(plain, LightShare), ComboTotalShare / weights);
            if (!finisher) return plain;
            return Math.Max(0, Math.Min(plain * fin, ComboTotalShare - plain * (hits - 1)));
        }

        /// <summary>The most one hit takes of the player's base health after every multiplier (depth, role, kit, an operative's cores, a
        /// mark, elite affixes): <see cref="HardCapShare"/>, and for a combo weapon its depth 1 share, so the full combo stays inside
        /// <see cref="ComboTotalShare"/> whatever is stacked on it. The adapter clamps the final damage to this.</summary>
        public static double HitCapShare(MeleeDef d, bool finisher)
        {
            if (d == null || d.Special != MeleeSpecial.Combo) return HardCapShare;
            return Math.Min(HardCapShare, HitShare(d, finisher, false, false));
        }

        /// <summary>Growth of a melee hit with the stage's enemy damage multiplier: 1 at depth 1 on Normal, half of the bullets' growth after.</summary>
        public static double DepthScale(double enemyDamageMul)
        {
            double baseline = RogueDepth.EnemyDamage(1, 1);
            if (!(enemyDamageMul > 0) || !(baseline > 0)) return 1;
            return Math.Max(0.5, 1 + (enemyDamageMul / baseline - 1) * DepthShare);
        }

        /// <summary>Damage one enemy melee hit deals to a player, before the player's own mitigation.</summary>
        public static double PlayerDamage(MeleeDef d, bool finisher, bool fromBehind, bool thrown, double enemyDamageMul, double roleDamageMul, double kitDamageMul)
        {
            double share = HitShare(d, finisher, fromBehind, thrown) * DepthScale(enemyDamageMul) * Math.Max(0, roleDamageMul) * Math.Max(0, kitDamageMul);
            return PlayerBaseHealth * Math.Min(HitCapShare(d, finisher), share);
        }

        /// <summary>Seconds of visible wind-up before the hit lands.</summary>
        public static double Telegraph(MeleeDef d, int flurryStacks, int comboIndex)
        {
            if (d == null) return MinTelegraph;
            double t = TelegraphBase + d.Windup * TelegraphPerWindup;
            if (d.Special == MeleeSpecial.Flurry) t /= 1 + d.S1 * Math.Max(0, Math.Min(FlurryStacks(d), flurryStacks));
            if (d.Special == MeleeSpecial.Combo && comboIndex > 0) t *= ComboFollowScale;
            return Math.Max(MinTelegraph, t);
        }

        public static double Recovery(MeleeDef d, int flurryStacks)
        {
            if (d == null) return MinRecovery;
            double t = RecoveryBase + d.Recovery * RecoveryPerRecovery;
            if (d.Special == MeleeSpecial.Flurry) t /= 1 + d.S1 * Math.Max(0, Math.Min(FlurryStacks(d), flurryStacks));
            return Math.Max(MinRecovery, t);
        }

        public static double Reach(MeleeDef d) { return d == null ? 0 : d.Range + ReachPadding; }
        public static int ComboHits(MeleeDef d) { return d != null && d.Special == MeleeSpecial.Combo ? Math.Max(1, Math.Min(MaxComboHits, (int)d.S1)) : 1; }
        public static int FlurryStacks(MeleeDef d) { return d != null && d.Special == MeleeSpecial.Flurry ? Math.Max(0, Math.Min(MaxFlurryStacks, (int)d.S2)) : 0; }
        public static bool Slams(MeleeDef d) { return d != null && d.Special == MeleeSpecial.GroundSlam; }
        public static double SlamRadius(MeleeDef d) { return Slams(d) ? Math.Max(2, d.S1) : 0; }
        /// <summary>Distance in front of the attacker at which a slam lands.</summary>
        public static double SlamOffset(MeleeDef d) { return Reach(d) * 0.5; }

        /// <summary>Metres a hit pushes a player (bat, shield bash, slam), scaled down from the player's version and capped.</summary>
        public static double KnockbackMeters(MeleeDef d) { return Math.Min(KnockbackCap, MeleeRules.KnockbackMeters(d) * KnockbackScale); }

        /// <summary>Slow a hit puts on a player: fraction and seconds (0 when the weapon has none).</summary>
        public static double SlowFraction(MeleeDef d) { return d == null ? 0 : d.Special == MeleeSpecial.Shock ? ShockSlow : d.Special == MeleeSpecial.GroundSlam ? SlamSlow : 0; }
        public static double SlowSeconds(MeleeDef d)
        {
            if (d == null) return 0;
            if (d.Special == MeleeSpecial.Shock) return Math.Max(ShockMinSeconds, Math.Min(ShockMaxSeconds, d.S1 * ShockSecondsScale));
            return d.Special == MeleeSpecial.GroundSlam ? SlamSlowSeconds : 0;
        }

        /// <summary>Frontal damage reduction of an enemy carrying a guard weapon (while it is not recovering from a swing).</summary>
        public static double GuardReduction(MeleeDef d) { return d != null && d.Special == MeleeSpecial.Guard ? Math.Min(GuardReductionCap, d.S1 * GuardReductionScale) : 0; }
        public static bool Throws(MeleeDef d) { return d != null && d.Special == MeleeSpecial.Throw; }
        public static double ThrowRange(MeleeDef d) { return Throws(d) ? Math.Min(ThrowMaxRangeCap, d.S1) : 0; }
        public static bool Deflects(MeleeDef d) { return d != null && d.Special == MeleeSpecial.Combo; }
        /// <summary>Frontal damage reduction while a deflecting weapon is in a wind-up (0 for every other weapon).</summary>
        public static double DeflectReduction(MeleeDef d) { return Deflects(d) ? DeflectFrontReduction : 0; }

        /// <summary>Authoring guard for the conversion: every armory melee weapon lands inside the fair band.</summary>
        public static List<string> Validate()
        {
            var errors = new List<string>();
            foreach (var m in RogueArmory.Melee)
            {
                double plain = HitShare(m, false, false, false), worst = HitShare(m, true, true, false);
                if (plain < LightShare - 1e-9 || plain > HeavyShare * SlamShareMul + 1e-9) errors.Add("enemy melee share out of band: " + m.Id);
                if (worst > MaxShare + 1e-9) errors.Add("enemy melee heavy hit above the cap: " + m.Id);
                if (Telegraph(m, MaxFlurryStacks, MaxComboHits) < MinTelegraph - 1e-9) errors.Add("enemy melee telegraph too short: " + m.Id);
                if (KnockbackMeters(m) > KnockbackCap + 1e-9) errors.Add("enemy melee knockback above the cap: " + m.Id);
                if (PlayerDamage(m, true, true, false, 100, 10, 10) > PlayerBaseHealth * HardCapShare + 1e-6) errors.Add("enemy melee damage above the hard cap: " + m.Id);
            }
            return errors;
        }
    }

    /// <summary>
    /// The Rogue Operative: an enemy built like a player. Numbers of its stat tiers, of each core's effect on its attacks, of its
    /// tacticals and of the human-like aim and reaction model live here; the adapter (RogueEnemyOperative) only reads them.
    /// </summary>
    public static class EnemyOperativeRules
    {
        public const double HealthPerTier = 0.10, DamagePerTier = 0.08, SpeedPerTier = 0.04;

        // ---- cores on its attacks
        public const double PrecisionFarBonus = 0.20, PrecisionFarRange = 30, PrecisionAimMul = 0.6;
        public const double AssaultCloseBonus = 0.25, AssaultCloseRange = 12, AssaultBuffSeconds = 4, AssaultReduction = 0.30, AssaultSpeed = 0.20;
        public const double SuppressionStep = 0.06, SuppressionCap = 0.30, SuppressionWindowSeconds = 2;
        public const double ReloadBurstBonus = 0.25, ReloadBurstSeconds = 4;
        public const double RicochetShare = 0.30, RicochetRadius = 8;
        public const double DemolitionShare = 0.30, DemolitionRadius = 7, DemolitionFuseSeconds = 1.2;
        public const double MarkBonus = 0.15, MarkSeconds = 5;
        public const double MobilitySpeed = 0.15, MobilityDashCooldownMul = 0.6;
        /// <summary>Everything above multiplied together never exceeds this (the stage's depth multiplier comes on top, as for every enemy).</summary>
        public const double OutgoingCap = 1.6;
        /// <summary>One round of an operative's gun takes at most this share of the player's base health (EnemyMeleeRules.PlayerBaseHealth)
        /// after everything: its build, its mark, elite affixes and the stage's depth multiplier.</summary>
        public const double ShotCapShare = 0.5;

        // ---- tacticals
        public const double ShieldCapacity = 500, ShieldSeconds = 5, ShieldCooldownSeconds = 14;
        public const double DashMeters = 7, DashSeconds = 0.2, DashCooldownSeconds = 7;
        /// <summary>A player whose aim is within AimedAngle degrees of the operative for AimedReactionSeconds makes it dodge.</summary>
        public const double AimedAngleDegrees = 4, AimedReactionSeconds = 0.3, AimedRange = 70;

        // ---- human-like aim and reaction
        public const double ReactionMinSeconds = 0.2, ReactionMaxSeconds = 0.5, ReacquirePenaltySeconds = 0.15, ReacquireAfterSeconds = 3;
        /// <summary>Seconds the aim trails a moving target; first-shot error after acquiring (degrees); spring that settles it (rad/s, damping ratio below 1 overshoots).</summary>
        public const double AimLagSeconds = 0.16, AcquireErrorDegrees = 4, AimSpringOmega = 9, AimSpringDamping = 0.45;
        public const double WanderDegrees = 0.35, MovingAimMul = 1.3, MaxAimErrorDegrees = 8;
        /// <summary>Bursts fired before a short pause, and the pause (seconds).</summary>
        public const int BurstsMin = 2, BurstsMax = 4;
        public const double BurstPauseMin = 0.4, BurstPauseMax = 0.9;

        // ---- trait of its gun on a player
        public const double StaggerSlow = 0.3, TraitKnockbackMeters = 1.5;

        public static double SuppressionMul(int stacks) { return 1 + Math.Min(SuppressionCap, SuppressionStep * Math.Max(0, stacks)); }

        /// <summary>Multiplier of a hit by an operative on a player from its build: stat tiers, cores, and the trait/drawback of its gun
        /// that change damage by distance. <paramref name="melee"/> hits skip the gun's rules.</summary>
        public static double OutgoingMul(EnemyKit kit, double distance, int suppressionStacks, bool reloadBurst, bool melee)
        {
            if (kit == null) return 1;
            double m = 1;
            if (kit.HasCore("core.precision") && distance >= PrecisionFarRange) m *= 1 + PrecisionFarBonus;
            if (kit.HasCore("core.assault") && distance <= AssaultCloseRange) m *= 1 + AssaultCloseBonus;
            if (kit.HasCore("core.suppression")) m *= SuppressionMul(suppressionStacks);
            if (kit.HasCore("core.reloadburst") && reloadBurst) m *= 1 + ReloadBurstBonus;
            var gun = melee ? null : kit.Ranged;
            if (gun != null)
            {
                if (gun.Trait == TraitKind.LongRangeBonus && distance >= gun.T2) m *= 1 + gun.T1;
                if (gun.Trait == TraitKind.CloseRangeBonus && distance <= gun.T2) m *= 1 + gun.T1;
                if (gun.Drawback == DrawbackKind.DamageFalloff && distance > gun.D2) m *= 1 - gun.D1;
            }
            m = Math.Min(OutgoingCap, m);
            // the melee conversion (EnemyMeleeRules.PlayerDamage) already took the kit's firepower tiers
            return melee ? m : m * kit.DamageMul;
        }

        /// <summary>Damage a marked player takes from every enemy.</summary>
        public static double MarkedMul(bool marked) { return marked ? 1 + MarkBonus : 1; }

        /// <summary>True when a trait of an armory gun changes what an operative's hit does to a player (the rest are skipped).</summary>
        public static bool TraitApplies(TraitKind trait)
        {
            switch (trait)
            {
                case TraitKind.SlowOnHit: case TraitKind.CloseKnockback: case TraitKind.StaggerOnHit:
                case TraitKind.LongRangeBonus: case TraitKind.CloseRangeBonus: return true;
                default: return false;
            }
        }

        public static double DashCooldown(EnemyKit kit) { return DashCooldownSeconds * (kit != null && kit.HasCore("core.mobility") ? MobilityDashCooldownMul : 1); }

        /// <summary>Range (m) the operative likes to fight at with its gun.</summary>
        public static double PreferredRange(RangedWeaponDef gun)
        {
            if (gun == null) return 35;
            switch (gun.Class)
            {
                case WeaponClass.Shotgun: return 12;
                case WeaponClass.SMG: return 24;
                case WeaponClass.Handgun: return 20;
                case WeaponClass.Sniper: return 70;
                default: return 38;
            }
        }

        /// <summary>Distance under which it draws its melee weapon instead of shooting (a shotgun keeps shooting until point blank).</summary>
        public static double MeleeSwitchRange(RangedWeaponDef gun) { return gun != null && gun.Class == WeaponClass.Shotgun ? 4.5 : 7.5; }

        /// <summary>
        /// One step of the aim error (degrees, two axes): a damped spring pulls the error toward <paramref name="goalX"/>/<paramref name="goalY"/>
        /// (where the lag behind a moving target and the wander put it). Under-damped, so a flick overshoots before it settles.
        /// </summary>
        public static void AimStep(ref double errX, ref double errY, ref double velX, ref double velY, double goalX, double goalY, double dt)
        {
            if (!(dt > 0)) return;
            if (dt > 0.05) dt = 0.05;   // a long frame must not blow the integration up
            double w = AimSpringOmega, z = AimSpringDamping;
            velX += (w * w * (goalX - errX) - 2 * z * w * velX) * dt; errX += velX * dt;
            velY += (w * w * (goalY - errY) - 2 * z * w * velY) * dt; errY += velY * dt;
            if (errX > MaxAimErrorDegrees) errX = MaxAimErrorDegrees; else if (errX < -MaxAimErrorDegrees) errX = -MaxAimErrorDegrees;
            if (errY > MaxAimErrorDegrees) errY = MaxAimErrorDegrees; else if (errY < -MaxAimErrorDegrees) errY = -MaxAimErrorDegrees;
        }

        /// <summary>Degrees the aim trails a target crossing at <paramref name="lateralSpeed"/> m/s at <paramref name="distance"/> m.</summary>
        public static double LagDegrees(double lateralSpeed, double distance, bool precision, bool selfMoving)
        {
            if (!(distance > 0.5)) distance = 0.5;
            double deg = Math.Atan2(lateralSpeed * AimLagSeconds, distance) * 180 / Math.PI;
            if (precision) deg *= PrecisionAimMul;
            if (selfMoving) deg *= MovingAimMul;
            return deg;
        }
    }

    /// <summary>
    /// The slows enemies put on one player (an elite Suppressor, a stun baton, a sledgehammer's slam, an operative's gun trait).
    /// Two rules keep them from locking a player in place:
    ///  - a weaker slow never extends a stronger one. The stronger one runs to its own end; if the weaker one is still running
    ///    then, the player drops to the weaker strength for what is left of it.
    ///  - after <see cref="MaxContinuousSeconds"/> of being slowed without a break the slow ends and the player cannot be slowed
    ///    for <see cref="ImmunitySeconds"/>. A break shorter than <see cref="GapSeconds"/> does not restart the count.
    /// Times are seconds on any clock the caller keeps to (the adapter passes Time.time).
    /// </summary>
    public sealed class EnemySlowState
    {
        public const double MaxContinuousSeconds = 3, ImmunitySeconds = 2, GapSeconds = 1;

        bool active;
        double strongFactor = 1, strongUntil, weakFactor = 1, weakUntil, since, immuneUntil, lastEnd = double.NegativeInfinity;

        static double Clamp01(double v) { return v < 0 ? 0 : v > 1 ? 1 : v; }

        void End(double at) { active = false; strongFactor = weakFactor = 1; strongUntil = weakUntil = 0; lastEnd = at; }

        void Advance(double now)
        {
            if (!active) return;
            double end = Math.Max(strongUntil, weakUntil), cutoff = since + MaxContinuousSeconds;
            if (end > cutoff && now >= cutoff) { End(cutoff); immuneUntil = cutoff + ImmunitySeconds; return; }
            if (now >= end) { End(end); return; }
            if (now >= strongUntil) { strongFactor = weakFactor; strongUntil = weakUntil; weakFactor = 1; weakUntil = 0; }
        }

        /// <summary>A hit that slows by <paramref name="fraction"/> (0..1) for <paramref name="seconds"/>. False when nothing changed
        /// (no slow in it, or the player is immune right now).</summary>
        public bool Apply(double now, double fraction, double seconds)
        {
            Advance(now);
            double factor = 1 - Clamp01(fraction);
            if (!(seconds > 0) || !(factor < 1) || now < immuneUntil) return false;
            double until = now + seconds;
            if (!active)
            {
                if (now - lastEnd > GapSeconds) since = now;
                else if (now >= since + MaxContinuousSeconds) { immuneUntil = now + ImmunitySeconds; return false; }
                active = true; strongFactor = factor; strongUntil = until; weakFactor = 1; weakUntil = 0;
                return true;
            }
            if (factor < strongFactor)
            {
                // stronger: it takes over, and what it replaces goes on behind it when that lasts longer
                if (strongUntil > until) Tail(now, strongFactor, strongUntil);
                strongFactor = factor; strongUntil = until;
            }
            else if (factor == strongFactor) strongUntil = Math.Max(strongUntil, until);
            else if (until > strongUntil) Tail(now, factor, until);   // weaker: only what outlasts the stronger one counts
            return true;
        }

        void Tail(double now, double factor, double until)
        {
            if (weakUntil <= now) { weakFactor = factor; weakUntil = until; return; }
            weakFactor = Math.Min(weakFactor, factor); weakUntil = Math.Max(weakUntil, until);
        }

        /// <summary>Movement multiplier right now (1 when not slowed).</summary>
        public double Factor(double now) { Advance(now); return active ? strongFactor : 1; }
        /// <summary>When the strength in force right now ends (0 when not slowed): a weaker slow may follow it.</summary>
        public double SegmentUntil(double now) { Advance(now); return active ? Math.Min(strongUntil, since + MaxContinuousSeconds) : 0; }
        public bool Immune(double now) { Advance(now); return now < immuneUntil; }
        public void Clear() { active = false; strongFactor = weakFactor = 1; strongUntil = weakUntil = 0; since = 0; immuneUntil = 0; lastEnd = double.NegativeInfinity; }
    }
}
