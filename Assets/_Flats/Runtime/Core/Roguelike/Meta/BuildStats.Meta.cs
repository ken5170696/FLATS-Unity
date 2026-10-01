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

        public const double MaxAdsSpeedup = 0.4, MinSwapTimeMul = 0.3;
        // rule constants of the mechanic skills (read by the adapter and by MetaText)
        public const int MaxSkillStacks = 6;
        public const double AdrenalThreshold = 0.35, AdrenalCooldown = 10, BerserkerWindow = 5, SquadLinkSoloThreshold = 0.25;

        /// <summary>
        /// Envelope of the design rule: whatever combination of conditional skills is active at once,
        /// meta skills add at most +30% damage (six stacks), and cut incoming damage to no less
        /// than 80%. No meta health bonus is granted. Run items (cores, mods) are separate.
        /// </summary>
        public const double MaxMetaDamageBonus = 0.30, MinMetaDamageTakenMul = 0.80;
        /// <summary>Fresh Magazine: a forced headshot never deals more than this multiple of the body shot (handgun and sniper headshot bonuses are x5).</summary>
        public const double FreshMagazineMaxMul = 2.0;

        public struct MetaCombat
        {
            public int BerserkerStacks, RhythmStacks;
            public bool Shredding, OpeningShot, SquadLinked, BelowHalfHealth, HoldingLine;
        }

        /// <summary>Outgoing damage multiplier of the conditional meta skills, clamped to the envelope.</summary>
        public double MetaDamageMul(MetaCombat c)
        {
            double m = 1;
            if (BerserkerStep > 0) m *= 1 + BerserkerStep * Math.Max(0, Math.Min(MaxSkillStacks, c.BerserkerStacks));
            if (RhythmStep > 0) m *= 1 + RhythmStep * Math.Max(0, Math.Min(MaxSkillStacks, c.RhythmStacks));
            if (ShredderBonus > 0 && c.Shredding) m *= 1 + ShredderBonus;
            if (OpeningShotBonus > 0 && c.OpeningShot) m *= 1 + OpeningShotBonus;
            if (SquadLinkBonus > 0 && c.SquadLinked) m *= 1 + SquadLinkBonus;
            // The strongest active declared bonus sets the envelope. At <=3 stacks the old
            // 15% envelope remains; Squad Link permits 20%, six stacks permit 30%.
            double cap = 0.15;
            cap = Math.Max(cap, BerserkerStep * Math.Max(0, Math.Min(MaxSkillStacks, c.BerserkerStacks)));
            cap = Math.Max(cap, RhythmStep * Math.Max(0, Math.Min(MaxSkillStacks, c.RhythmStacks)));
            if (c.SquadLinked) cap = Math.Max(cap, SquadLinkBonus);
            return Math.Min(1 + Math.Min(MaxMetaDamageBonus, cap), m);
        }

        /// <summary>Incoming damage multiplier of the conditional meta skills, clamped to the envelope.</summary>
        public double MetaDamageTakenMul(MetaCombat c)
        {
            double m = 1;
            if (JuggernautReduction > 0 && c.BelowHalfHealth) m *= 1 - JuggernautReduction;
            if (HoldLineReduction > 0 && c.HoldingLine) m *= 1 - HoldLineReduction;
            return Math.Max(MinMetaDamageTakenMul, m);
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
            s.AdsTimeMul = Math.Max(MaxAdsSpeedup, Math.Min(2.5, s.AdsTimeMul));
            s.SwapTimeMul = Math.Max(MinSwapTimeMul, Math.Min(2.5, s.SwapTimeMul));
            s.UltimateChargeMul = Math.Max(0.5, Math.Min(1.5, s.UltimateChargeMul));
            s.ShieldCooldownMul = Math.Max(0.5, Math.Min(1.5, s.ShieldCooldownMul));
            s.MeleeDamageMul = Math.Max(0.5, Math.Min(2.0, s.MeleeDamageMul));
            s.HipSpreadMul = Math.Max(0.4, Math.Min(2.0, s.HipSpreadMul));
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
