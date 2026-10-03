using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    /// <summary>
    /// The out-of-run loadout a player brings into a run: learned skills, armory weapons, sights and
    /// melee. Replicated inside PlayerBuild, so every copy derives the same BuildStats from it.
    /// </summary>
    [Serializable]
    public sealed class MetaLoadout
    {
        public int level = 1;
        public string[] skills = new string[0];
        public string primary = "", secondary = "", melee = "";
        public string primarySight = "", secondarySight = "";
        public string[] unlocked = new string[0];     // armory ids the player owns (shop filter)

        public MetaLoadout Clone()
        {
            return new MetaLoadout
            {
                level = level, skills = (string[])skills.Clone(), primary = primary, secondary = secondary, melee = melee,
                primarySight = primarySight, secondarySight = secondarySight, unlocked = (string[])unlocked.Clone(),
            };
        }

        public bool Empty { get { return skills.Length == 0 && string.IsNullOrEmpty(primary) && string.IsNullOrEmpty(melee); } }
    }

    /// <summary>Fields added by the meta layer. Defaults are neutral, so a build without meta computes exactly as before.</summary>
    public sealed partial class BuildStats
    {
        // stat multipliers
        public double AdsTimeMul = 1, SwapTimeMul = 1, UltimateChargeMul = 1, ShieldCooldownMul = 1, MeleeDamageMul = 1, HipSpreadMul = 1;
        // mechanics (0 / false = not learned)
        public bool FreshMagazineHeadshot, MeleeKillRefill;
        public double HeadshotKillMarkRadius, HeadshotKillMarkSeconds;
        public double SteadyBreathSpread, SteadyBreathSeconds;
        public double OpeningShotBonus, ExecuteBelow, RhythmStep, RhythmWindow;
        public double KillReloadFraction, AdrenalSpeed, AdrenalSeconds, BerserkerStep, JuggernautReduction;
        public double HitSlowFraction, HitSlowSeconds, HoldLineReduction, HoldLineSeconds;
        public int ScavengerEvery; public double ScavengerFraction;
        public double EndlessBeltMul = 1, ShredderBonus, ShredderSeconds;
        public double RescueShieldPoints, RescueShieldSeconds, ReviveHealthFraction = 0.3;
        public int StarterMods;
        public double GuardianSeconds, SquadLinkBonus, SquadLinkSeconds;
        // weapons in hand (resolved from the loadout; null outside a meta run)
        public RangedWeaponDef PrimaryWeaponDef, SecondaryWeaponDef;
        public MeleeDef MeleeWeaponDef;
        public SightDef PrimarySightDef, SecondarySightDef;

        /// <summary>Safety floors only (2026-10-04: no stacking caps): a time or a spread multiplier never reaches zero.</summary>
        public const double MinAdsTimeMul = 0.1, MinSwapTimeMul = 0.1, MinShieldCooldownMul = 0.1, MinHipSpreadMul = 0.05, MinUltimateChargeMul = 0.1;
        // rule constants of the mechanic skills (read by the adapter and by MetaText)
        public const int MaxSkillStacks = 6;
        public const double AdrenalThreshold = 0.35, AdrenalCooldown = 10, BerserkerWindow = 5, SquadLinkSoloThreshold = 0.25;

        // Owner request 2026-10-04: conditional skills have no shared envelope any more. Their damage bonuses multiply with each
        // other and with the run items, and their damage reductions multiply down to the common floor (BuildStats.MinDamageTakenMul).
        // MaxSkillStacks stays: it is the number of stacks Berserker and Marksman's Rhythm can hold, part of each skill's own rule.
        /// <summary>Fresh Magazine: a forced headshot never deals more than this multiple of the body shot (handgun and sniper headshot bonuses are x5).</summary>
        public const double FreshMagazineMaxMul = 2.0;

        public struct MetaCombat
        {
            public int BerserkerStacks, RhythmStacks;
            public bool Shredding, OpeningShot, SquadLinked, BelowHalfHealth, HoldingLine;
        }

        /// <summary>Outgoing damage multiplier of the conditional meta skills: every active one multiplies in, without a cap.</summary>
        public double MetaDamageMul(MetaCombat c)
        {
            double m = 1;
            if (BerserkerStep > 0) m *= 1 + BerserkerStep * Math.Max(0, Math.Min(MaxSkillStacks, c.BerserkerStacks));
            if (RhythmStep > 0) m *= 1 + RhythmStep * Math.Max(0, Math.Min(MaxSkillStacks, c.RhythmStacks));
            if (ShredderBonus > 0 && c.Shredding) m *= 1 + ShredderBonus;
            if (OpeningShotBonus > 0 && c.OpeningShot) m *= 1 + OpeningShotBonus;
            if (SquadLinkBonus > 0 && c.SquadLinked) m *= 1 + SquadLinkBonus;
            return m;
        }

        /// <summary>Incoming damage multiplier of the conditional meta skills; only the safety floor bounds it.</summary>
        public double MetaDamageTakenMul(MetaCombat c)
        {
            double m = 1;
            if (JuggernautReduction > 0 && c.BelowHalfHealth) m *= 1 - JuggernautReduction;
            if (HoldLineReduction > 0 && c.HoldingLine) m *= 1 - HoldLineReduction;
            return Math.Max(MinDamageTakenMul, m);
        }

        /// <summary>Folds the meta loadout in. Called by Compute after run items and before the clamps.</summary>
        static void ApplyMeta(BuildStats s, PlayerBuild b)
        {
            var meta = b.meta;
            if (meta == null) return;
            SkillTree.Apply(s, meta.skills);
            s.PrimaryWeaponDef = RogueArmory.Weapon(meta.primary);
            s.SecondaryWeaponDef = RogueArmory.Weapon(meta.secondary);
            s.MeleeWeaponDef = RogueArmory.MeleeWeapon(meta.melee);
            s.PrimarySightDef = RogueArmory.Sight(meta.primarySight);
            s.SecondarySightDef = RogueArmory.Sight(meta.secondarySight);
            if (s.MeleeWeaponDef != null) s.SpeedMul *= s.MeleeWeaponDef.MoveMul;
        }

        static void ClampMeta(BuildStats s)
        {
            // floors only: no bonus is capped here, except the ultimate charge speed (the one total cap the owner asked for)
            s.AdsTimeMul = Math.Max(MinAdsTimeMul, s.AdsTimeMul);
            s.SwapTimeMul = Math.Max(MinSwapTimeMul, s.SwapTimeMul);
            s.UltimateChargeMul = Math.Max(MinUltimateChargeMul, Math.Min(MaxUltimateChargeMul, s.UltimateChargeMul));
            s.ShieldCooldownMul = Math.Max(MinShieldCooldownMul, s.ShieldCooldownMul);
            s.MeleeDamageMul = Math.Max(0.5, s.MeleeDamageMul);
            ClampWeapons(s);   // BuildStats.Weapons.cs: fire rate, melee swing time, extra pellets
            s.HipSpreadMul = Math.Max(MinHipSpreadMul, s.HipSpreadMul);
            s.ReviveHealthFraction = Math.Max(0.1, Math.Min(1.0, s.ReviveHealthFraction));
        }

        /// <summary>The armory weapon whose base model is <paramref name="modelIndex"/> in this build, or null.</summary>
        public RangedWeaponDef WeaponForModel(int modelIndex)
        {
            if (PrimaryWeaponDef != null && PrimaryWeaponDef.BaseModel == modelIndex) return PrimaryWeaponDef;
            if (SecondaryWeaponDef != null && SecondaryWeaponDef.BaseModel == modelIndex) return SecondaryWeaponDef;
            return null;
        }

        public SightDef SightForModel(int modelIndex)
        {
            if (PrimaryWeaponDef != null && PrimaryWeaponDef.BaseModel == modelIndex) return PrimarySightDef;
            if (SecondaryWeaponDef != null && SecondaryWeaponDef.BaseModel == modelIndex) return SecondarySightDef;
            return null;
        }
    }
}
