using System;

namespace Flats.Core.Roguelike
{
    /// <summary>What the shooter's copy knows when a round leaves the barrel.</summary>
    public struct ShotContext
    {
        public double Now;                 // seconds, any monotonic clock
        public bool Aiming, Moving;
        public int RoundsInMagazine;       // before this round is spent
        public int MagazineCapacity;
        public double AimedStillSeconds;   // time aimed without firing or moving (PatientShot)
        public bool NewTriggerPull;        // first round of a trigger pull
    }

    /// <summary>Per-round modifiers. Multipliers default to 1; the adapter multiplies them into the legacy numbers.</summary>
    public struct ShotModifiers
    {
        public double DamageMul, SpreadMul, IntervalMul, PreFireDelay;
        public bool FreeRound;             // DoubleTap: an extra round that costs no ammunition
        public string DamageSource;        // effect id credited with the extra damage ("" = none)
        public static ShotModifiers Neutral { get { return new ShotModifiers { DamageMul = 1, SpreadMul = 1, IntervalMul = 1, DamageSource = "" }; } }
    }

    /// <summary>Effects a hit applies to the enemy. Resolved on the shooter's copy, executed by the authority.</summary>
    public struct HitEffects
    {
        public double DamageMul;           // distance/elite multipliers of the weapon
        public double SlowFraction, SlowSeconds, StunSeconds, KnockbackMeters;
        public bool IgnoreFrontReduction;
        public int MagazineRefund;
        public string Source;
        public static HitEffects None { get { return new HitEffects { DamageMul = 1, Source = "" }; } }
    }

    /// <summary>Stateless numbers of an armory weapon that do not depend on the fight.</summary>
    public static class WeaponRules
    {
        public const double ConsecutiveWindow = 0.45;
        /// <summary>Seconds the aim needs to steady after the sight comes up, before weapon, sight and skill multipliers.</summary>
        public const double BaseSettleSeconds = 0.35;
        /// <summary>Spread multiplier the instant the sight comes up; it falls linearly to 1 over the settle time.</summary>
        public const double UnsettledSpread = 2.5;

        /// <summary>Spread multiplier <paramref name="secondsAimed"/> after raising the sight, for a settle time multiplier.</summary>
        public static double SettleSpread(double secondsAimed, double settleMul)
        {
            double settle = BaseSettleSeconds * Math.Max(0.1, settleMul);
            if (secondsAimed >= settle) return 1;
            return 1 + (UnsettledSpread - 1) * (1 - Math.Max(0, secondsAimed) / settle);
        }
   // seconds after a cycle within which the next round counts as sustained fire

        public static double MoveSpeedMul(RangedWeaponDef d)
        {
            if (d == null) return 1;
            double m = 1;
            if (d.Trait == TraitKind.RunAndGun) m *= 1 + d.T1;
            if (d.Drawback == DrawbackKind.MoveSlow) m *= 1 - d.D1;
            return m;
        }

        /// <summary>Aim-in time multiplier of weapon and sight together (lower is faster).</summary>
        public static double AdsTimeMul(RangedWeaponDef d, double sightAdsMul)
        {
            double m = sightAdsMul > 0 ? sightAdsMul : 1;
            if (d == null) return m;
            if (d.Trait == TraitKind.SnapAim) m *= 1 - d.T1;
            if (d.Drawback == DrawbackKind.SlowAds) m *= 1 + d.D1;
            return m;
        }

        public static bool CanAim(RangedWeaponDef d) { return d == null || d.Drawback != DrawbackKind.NoAds; }

        public static double SwapTimeMul(RangedWeaponDef d)
        {
            if (d == null) return 1;
            double m = 1;
            if (d.Trait == TraitKind.QuickDraw) m *= 1 - d.T1;
            if (d.Drawback == DrawbackKind.SlowSwap) m *= 1 + d.D1;
            return m;
        }

        /// <summary>
        /// Swap time multiplier of a swap between two carried weapons (the one in hand and the one coming out; either may be null).
        /// Quick Draw promises "swapping to or from it", so the fastest Quick Draw of the two always applies; a Slow Swap weapon
        /// slows the swap by the heaviest of the two. When both are involved they multiply (Handgun 1 with Assault Rifle 3:
        /// 0.4 x 1.6 = 0.64, still faster than a plain swap). Taking the slower of the two, as this used to, meant Quick Draw
        /// never applied at all: the other weapon's multiplier is always at least 1.
        /// </summary>
        public static double SwapTimeMul(RangedWeaponDef a, RangedWeaponDef b)
        {
            double quick = 1, slow = 1;
            foreach (var d in new[] { a, b })
            {
                if (d == null) continue;
                if (d.Trait == TraitKind.QuickDraw) quick = Math.Min(quick, 1 - d.T1);
                if (d.Drawback == DrawbackKind.SlowSwap) slow = Math.Max(slow, 1 + d.D1);
            }
            return quick * slow;
        }

        /// <summary>Reload time multiplier on top of the resolved reload time (SlowReload is already in ReloadMul).</summary>
        public static double ReloadTimeMul(RangedWeaponDef d, bool magazineEmpty)
        {
            if (d != null && d.Trait == TraitKind.EmptyReloadFast && magazineEmpty) return 1 - d.T1;
            return 1;
        }

        /// <summary>Damage multiplier decided at the hit (distance and target), plus the enemy effects of the weapon.</summary>
        public static HitEffects OnHit(RangedWeaponDef d, double distance, bool headshot, bool elite)
        {
            var h = HitEffects.None;
            if (d == null) return h;
            switch (d.Trait)
            {
                case TraitKind.LongRangeBonus: if (distance >= d.T2) { h.DamageMul *= 1 + d.T1; h.Source = d.Id; } break;
                case TraitKind.CloseRangeBonus: if (distance <= d.T2) { h.DamageMul *= 1 + d.T1; h.Source = d.Id; } break;
                case TraitKind.ArmorBreaker: h.IgnoreFrontReduction = true; if (elite) { h.DamageMul *= 1 + d.T1; h.Source = d.Id; } break;
                case TraitKind.SlowOnHit: h.SlowFraction = d.T1; h.SlowSeconds = d.T2; h.Source = d.Id; break;
                case TraitKind.StaggerOnHeadshot: if (headshot) { h.StunSeconds = d.T1; h.Source = d.Id; } break;
                case TraitKind.StaggerOnHit: if (distance <= d.T2) { h.StunSeconds = d.T1; h.Source = d.Id; } break;
                case TraitKind.CloseKnockback: if (distance <= d.T2) { h.KnockbackMeters = d.T1; h.Source = d.Id; } break;
                case TraitKind.HeadshotRefund: if (headshot) { h.MagazineRefund = (int)d.T1; h.Source = d.Id; } break;
                case TraitKind.Concussion: h.StunSeconds = d.T1; h.Source = d.Id; break;
            }
            if (d.Drawback == DrawbackKind.DamageFalloff && distance > d.D2) h.DamageMul *= 1 - d.D1;
            return h;
        }

        /// <summary>Extra enemies a round passes through.</summary>
        public static int Pierce(RangedWeaponDef d) { return d != null && d.Trait == TraitKind.Pierce ? (int)d.T1 : 0; }

        /// <summary>The most enemies one round may pass through: the build's two (EffectChainRules) plus the deepest Pierce weapon (2).</summary>
        public const int MaxPenetrateDepth = 4;

        /// <summary>
        /// Enemies a round fired from <paramref name="d"/> passes through: the build's depth (Precision core, Piercing Rounds; bounded
        /// as EffectChainRules bounds it) plus the Pierce trait of the weapon that fired the round. The weapon's share is added on
        /// top of the build's cap, so the trait is never silently cut by a build that already pierces.
        /// </summary>
        public static int PenetrateDepth(int buildDepth, RangedWeaponDef d)
        {
            int build = Math.Max(0, Math.Min(EffectChainRules.MaxDepth, buildDepth));
            return Math.Min(MaxPenetrateDepth, build + Math.Max(0, Pierce(d)));
        }

        /// <summary>Whether a round may continue as the <paramref name="nextDepth"/>-th derived round (1 = through the first enemy).
        /// Without a Pierce weapon this is exactly EffectChainRules.CanTrigger(DamageKind.Penetrate, nextDepth).</summary>
        public static bool CanPenetrate(int nextDepth, int buildDepth, RangedWeaponDef d)
        {
            return nextDepth >= 1 && nextDepth <= PenetrateDepth(buildDepth, d);
        }

        // ------------------------------------------------------------------ QA-49: shotgun range profile
        // Roguelike shotguns hit hard up close and are very weak at range. The multiplier applies to every direct pellet at the hit
        // (RogueHooks.MetaHitMul), on top of the headshot multiplier and the weapon's own trait and drawback (OnHit). Derived damage
        // that scales from a pellet's hit (penetration, chain, kill explosion) inherits it; a ricochet takes it at the bounce point.
        // Distances are world metres from where the round left the barrel to the hit point (a Flatman is about 2 m wide, 6 m tall).
        // Classic modes never call this.

        /// <summary>Pellet shotguns: x1.5 within 13 m, easing to x1 at 20 m, x1 up to 25 m, falling to x0.1 at 40 m and beyond (playtest 2026-10-01: the strong band reached a little too short).</summary>
        public const double PelletCloseMul = 1.5, PelletCloseEnd = 13, PelletNeutralFrom = 20, PelletFalloffFrom = 25, PelletFarFrom = 40, PelletFarMul = 0.1;
        /// <summary>Slug Gun (one heavy slug, meant for middle range): a milder profile, x1.2 within 13 m and x0.5 from 50 m.</summary>
        public const double SlugCloseMul = 1.2, SlugCloseEnd = 13, SlugNeutralFrom = 20, SlugFalloffFrom = 30, SlugFarFrom = 50, SlugFarMul = 0.5;

        /// <summary>
        /// A damage-by-distance profile: <see cref="CloseMul"/> up to <see cref="CloseEnd"/>, linear to x1 at <see cref="NeutralFrom"/>,
        /// x1 up to <see cref="FalloffFrom"/>, linear down to <see cref="FarMul"/> at <see cref="FarFrom"/> and flat beyond.
        /// Non-increasing with distance (CloseMul >= 1 >= FarMul).
        /// </summary>
        public sealed class RangeProfile
        {
            public readonly double CloseMul, CloseEnd, NeutralFrom, FalloffFrom, FarFrom, FarMul;
            public RangeProfile(double closeMul, double closeEnd, double neutralFrom, double falloffFrom, double farFrom, double farMul)
            {
                if (!(closeMul >= 1) || !(farMul > 0) || !(farMul <= 1) || !(closeEnd >= 0) || !(neutralFrom > closeEnd) || !(falloffFrom >= neutralFrom) || !(farFrom > falloffFrom))
                    throw new ArgumentOutOfRangeException("closeMul", "range profile must be ordered and non-increasing");
                CloseMul = closeMul; CloseEnd = closeEnd; NeutralFrom = neutralFrom; FalloffFrom = falloffFrom; FarFrom = farFrom; FarMul = farMul;
            }

            public double At(double distance)
            {
                if (double.IsNaN(distance)) return 1;
                if (distance <= CloseEnd) return CloseMul;
                if (distance < NeutralFrom) return CloseMul + (1 - CloseMul) * (distance - CloseEnd) / (NeutralFrom - CloseEnd);
                if (distance <= FalloffFrom) return 1;
                if (distance < FarFrom) return 1 + (FarMul - 1) * (distance - FalloffFrom) / (FarFrom - FalloffFrom);
                return FarMul;
            }
        }

        public static readonly RangeProfile PelletProfile = new RangeProfile(PelletCloseMul, PelletCloseEnd, PelletNeutralFrom, PelletFalloffFrom, PelletFarFrom, PelletFarMul);
        public static readonly RangeProfile SlugProfile = new RangeProfile(SlugCloseMul, SlugCloseEnd, SlugNeutralFrom, SlugFalloffFrom, SlugFarFrom, SlugFarMul);
        /// <summary>Enemy shotguns (rushers): the same long-range falloff, never a close bonus.</summary>
        public static readonly RangeProfile EnemyPelletProfile = new RangeProfile(1, PelletCloseEnd, PelletNeutralFrom, PelletFalloffFrom, PelletFarFrom, PelletFarMul);

        /// <summary>The range profile of an armory weapon: pellet shotguns and the slug gun; null for every other class.</summary>
        public static RangeProfile Profile(RangedWeaponDef d)
        {
            if (d == null || d.Class != WeaponClass.Shotgun) return null;
            return RogueArmory.Resolve(d).Pellets > 1 ? PelletProfile : SlugProfile;
        }

        /// <summary>A legacy gun model with no armory row (a shotgun taken from the ground that the loadout has no variant of).</summary>
        public static RangeProfile ProfileForModel(int baseModel) { return IsPelletModel(baseModel) ? PelletProfile : null; }

        /// <summary>Catalog models that fire a spread of pellets per shell (the two shotguns).</summary>
        public static bool IsPelletModel(int baseModel)
        {
            if (baseModel < 0 || baseModel >= WeaponCatalog.Count) return false;
            var b = WeaponCatalog.GetDefault(baseModel);
            return b.oneShot && !b.grenade && b.burstCount > 1;
        }

        /// <summary>Damage multiplier of a direct shotgun hit at <paramref name="distance"/> m (1 for other weapons).</summary>
        public static double ShotgunRangeMul(double distance, RangedWeaponDef def)
        {
            var p = Profile(def);
            return p == null ? 1 : p.At(distance);
        }

        /// <summary>As <see cref="ShotgunRangeMul(double, RangedWeaponDef)"/>; without an armory row the catalog model decides.</summary>
        public static double ShotgunRangeMul(double distance, RangedWeaponDef def, int baseModel)
        {
            var p = def != null ? Profile(def) : ProfileForModel(baseModel);
            return p == null ? 1 : p.At(distance);
        }

        /// <summary>An enemy's shotgun pellet: falls off with distance like the player's, capped at x1 up close.</summary>
        public static double EnemyShotgunRangeMul(double distance, int baseModel) { return IsPelletModel(baseModel) ? EnemyPelletProfile.At(distance) : 1; }
    }

    /// <summary>
    /// Runtime state of the weapon in hand, kept by the copy that fires (the owner) and fed by the
    /// legacy Shoot/Reload coroutines through the adapter. Everything is a pure function of the
    /// calls made, so the rule tests replay a trigger sequence and measure the result.
    /// Reset() is the symmetric undo: swapping, reloading into a new magazine or leaving the run.
    /// </summary>
    public sealed class WeaponTraitState
    {
        public RangedWeaponDef Def { get; private set; }
        public SightDef Sight { get; private set; }
        int consecutive;
        double lastRoundAt = -100, lastInterval = 0.1;
        bool frenzy, followUp;
        int triggerPulls;

        public const int MaxPatientSteps = 3;

        public WeaponTraitState(RangedWeaponDef def, SightDef sight) { Def = def; Sight = sight ?? RogueArmory.Sights[0]; }

        public bool FrenzyActive { get { return frenzy; } }
        public int Consecutive { get { return consecutive; } }

        public void Reset() { consecutive = 0; lastRoundAt = -100; frenzy = false; followUp = false; triggerPulls = 0; }

        /// <summary>Called once per round, before it is spent. Returns what the adapter multiplies in.</summary>
        public ShotModifiers NextRound(ShotContext c)
        {
            var m = ShotModifiers.Neutral;
            if (Def == null) return m;
            if (c.Now - lastRoundAt > lastInterval + WeaponRules.ConsecutiveWindow) consecutive = 0;
            if (c.NewTriggerPull)
            {
                triggerPulls++;
                if (Def.Drawback == DrawbackKind.LongSpinup && consecutive == 0) m.PreFireDelay = Def.D1;
                if (Def.Trait == TraitKind.DoubleTap && Def.T1 >= 1 && triggerPulls % (int)Def.T1 == 0) { m.FreeRound = true; m.DamageSource = Def.Id; }
            }
            switch (Def.Trait)
            {
                case TraitKind.KillFrenzy: if (frenzy) { m.IntervalMul /= 1 + Def.T1; m.DamageSource = Def.Id; } break;
                case TraitKind.FollowUp: if (followUp && c.NewTriggerPull) { m.IntervalMul /= 1 + Def.T1; followUp = false; m.DamageSource = Def.Id; } break;
                case TraitKind.SustainedAccuracy: m.SpreadMul *= 1 - Math.Min(Def.T2, Def.T1 * consecutive); break;
                case TraitKind.SpinUpDamage: { double bonus = Math.Min(Def.T2, Def.T1 * consecutive); if (bonus > 0) { m.DamageMul *= 1 + bonus; m.DamageSource = Def.Id; } } break;
                case TraitKind.LastRoundDouble: if (c.RoundsInMagazine == 1 && c.MagazineCapacity > 1) { m.DamageMul *= Def.T1; m.DamageSource = Def.Id; } break;
                case TraitKind.PatientShot:
                    if (c.Aiming && c.NewTriggerPull) { int steps = Math.Min(MaxPatientSteps, (int)Math.Floor(c.AimedStillSeconds / Math.Max(0.05, Def.T2))); if (steps > 0) { m.DamageMul *= 1 + Def.T1 * steps; m.DamageSource = Def.Id; } }
                    break;
            }
            if (Def.Drawback == DrawbackKind.HeavyRecoil) m.SpreadMul *= 1 + Math.Min(Def.D2, Def.D1 * consecutive);
            if (Def.Drawback == DrawbackKind.NoHipFire && !c.Aiming) m.SpreadMul *= Def.D1;
            if (c.Aiming && c.Moving) m.SpreadMul *= Sight.AimMoveSpreadMul;
            consecutive++;
            lastRoundAt = c.Now;
            return m;
        }

        /// <summary>Delay before the first round of a trigger pull (LongSpinup), asked before the coroutine fires anything.</summary>
        public double PreFireDelay(double now)
        {
            if (Def == null || Def.Drawback != DrawbackKind.LongSpinup) return 0;
            return now - lastRoundAt > lastInterval + WeaponRules.ConsecutiveWindow ? Def.D1 : 0;
        }

        /// <summary>The legacy gap after a round, so "consecutive" matches the real cadence.</summary>
        public void NoteInterval(double seconds) { lastInterval = Math.Max(0.05, seconds); }

        /// <summary>A kill by this weapon. Returns rounds to put back into the magazine.</summary>
        public int OnKill()
        {
            if (Def == null) return 0;
            if (Def.Trait == TraitKind.KillFrenzy) frenzy = true;
            return Def.Trait == TraitKind.AmmoOnKill ? (int)Def.T1 : 0;
        }

        public void OnHeadshot() { if (Def != null && Def.Trait == TraitKind.FollowUp) followUp = true; }

        /// <summary>A new magazine: frenzy ends with the magazine it was earned in.</summary>
        public void OnReloadCompleted() { frenzy = false; consecutive = 0; }
    }
}
