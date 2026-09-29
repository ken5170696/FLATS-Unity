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
        public const double ConsecutiveWindow = 0.45;   // seconds after a cycle within which the next round counts as sustained fire

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
