using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    /// <summary>A translatable line: an English template with {0}.. placeholders and the values to put in.</summary>
    public struct TextLine
    {
        public string Template; public string[] Args;
        public TextLine(string template, params string[] args) { Template = template; Args = args ?? new string[0]; }
        /// <summary>English rendering (tests and logs). The adapter renders through FlatsLocalization with the same template.</summary>
        public override string ToString() { return Args.Length == 0 ? Template : string.Format(Template, Args); }
    }

    /// <summary>
    /// Every number a player reads about skills, weapons, sights and heat is produced here from the
    /// same fields the rules read. Localization tables hold templates only ("{0}%"), never numbers.
    /// </summary>
    public static class MetaText
    {
        static string P(double f) { return RogueArmory.Pct(Math.Abs(f)); }
        static string N(double v) { return RogueArmory.Num(v); }

        public static TextLine Effect(SkillEffect e)
        {
            switch (e.Kind)
            {
                case SkillEffectKind.HeadshotDamage: return new TextLine("Headshot damage +{0}%.", P(e.V1));
                case SkillEffectKind.MoveSpeed: return new TextLine("Movement speed +{0}%.", P(e.V1));
                case SkillEffectKind.ReloadTime: return new TextLine("Reload time -{0}%.", P(e.V1));
                case SkillEffectKind.AdsTime: return new TextLine("Aim-in time -{0}%.", P(e.V1));
                case SkillEffectKind.SwapTime: return new TextLine("Weapon swap time -{0}%.", P(e.V1));
                case SkillEffectKind.Magazine: return new TextLine("Magazine capacity +{0}%.", P(e.V1));
                case SkillEffectKind.Reserve: return new TextLine("Reserve ammunition +{0}%.", P(e.V1));
                case SkillEffectKind.MaxHealth: return new TextLine("Maximum health +{0}%.", P(e.V1));
                case SkillEffectKind.DamageTaken: return new TextLine("Damage taken -{0}%.", P(e.V1));
                case SkillEffectKind.ReviveSpeed: return new TextLine("Revive speed +{0}%.", P(e.V1));
                case SkillEffectKind.UltimateCharge: return new TextLine("Ultimate charge +{0}%.", P(e.V1));
                case SkillEffectKind.TacticalCooldown: return new TextLine("Tactical cooldown -{0}%.", P(e.V1));
                case SkillEffectKind.MeleeDamage: return new TextLine("Melee damage +{0}%.", P(e.V1));
                case SkillEffectKind.HipSpread: return new TextLine("Hip-fire spread -{0}%.", P(e.V1));
                case SkillEffectKind.FreshMagazineHeadshot: return new TextLine("The first round after a full reload always counts as a headshot.");
                case SkillEffectKind.HeadshotKillMark: return new TextLine("A headshot kill marks enemies within {0} m for {1} s.", N(e.V1), N(e.V2));
                case SkillEffectKind.SteadyBreath: return new TextLine("Aiming without moving for {1} s: spread -{0}%.", P(e.V1), N(e.V2));
                case SkillEffectKind.OpeningShot: return new TextLine("+{0}% damage to enemies at full health.", P(e.V1));
                case SkillEffectKind.Executioner: return new TextLine("A headshot kills a regular enemy below {0}% health.", P(e.V1));
                case SkillEffectKind.HeadshotRhythm: return new TextLine("Each headshot within {1} s of the last: +{0}% damage, up to {2} stacks.", P(e.V1), N(e.V2), BuildStats.MaxSkillStacks.ToString());
                case SkillEffectKind.KillReload: return new TextLine("A kill refills {0}% of the magazine from your reserve.", P(e.V1));
                case SkillEffectKind.MeleeKillRefill: return new TextLine("A melee kill refills the magazine of the weapon in your hands from your reserve.");
                case SkillEffectKind.AdrenalRush: return new TextLine("Dropping below {2}% health: +{0}% speed for {1} s ({3} s cooldown).", P(e.V1), N(e.V2), P(BuildStats.AdrenalThreshold), N(BuildStats.AdrenalCooldown));
                case SkillEffectKind.Berserker: return new TextLine("Each kill within {1} s: +{0}% damage, up to {2} stacks. Reloading clears the stacks.", P(e.V1), N(BuildStats.BerserkerWindow), BuildStats.MaxSkillStacks.ToString());
                case SkillEffectKind.Juggernaut: return new TextLine("Below half health: damage taken -{0}%. Movement speed -{1}%.", P(e.V1), P(e.V2));
                case SkillEffectKind.SuppressiveSlow: return new TextLine("Your hits slow enemies by {0}% for {1} s.", P(e.V1), N(e.V2));
                case SkillEffectKind.HoldTheLine: return new TextLine("After {1} s without moving: damage taken -{0}%.", P(e.V1), N(e.V2));
                case SkillEffectKind.Scavenger: return new TextLine("Every {0} kills refill {1}% of your reserve.", N(e.V1), P(e.V2));
                case SkillEffectKind.EndlessBelt: return new TextLine("Reloading with at least half the magazine left takes {0}% of the time.", P(e.V1));
                case SkillEffectKind.Shredder: return new TextLine("After {1} s of continuous fire: +{0}% damage until you stop.", P(e.V1), N(e.V2));
                case SkillEffectKind.RescueShield: return new TextLine("Reviving gives you and the teammate a {0}-point shield for {1} s.", N(e.V1), N(e.V2));
                case SkillEffectKind.SecondWind: return new TextLine("You get up from a revive with {0}% health.", P(e.V1));
                case SkillEffectKind.StarterMod: return new TextLine("Start every run with {0} random common mod.", N(e.V1));
                case SkillEffectKind.GuardianAngel: return new TextLine("Once per stage a lethal hit leaves you standing, invulnerable for {0} s.", N(e.V1));
                case SkillEffectKind.SquadLink: return new TextLine("A teammate going down (solo: you below {2}% health) gives +{0}% damage and speed for {1} s.", P(e.V1), N(e.V2), P(BuildStats.SquadLinkSoloThreshold));
            }
            return new TextLine(e.Kind.ToString());
        }

        public static List<TextLine> Skill(SkillDef n)
        {
            var list = new List<TextLine>();
            foreach (var e in n.Effects) list.Add(Effect(e));
            return list;
        }

        public static TextLine Trait(RangedWeaponDef w)
        {
            switch (w.Trait)
            {
                case TraitKind.KillFrenzy: return new TextLine("After a kill: fire rate +{0}% for the rest of the magazine.", P(w.T1));
                case TraitKind.SustainedAccuracy: return new TextLine("Every round of sustained fire tightens spread by {0}%, up to {1}%.", P(w.T1), P(w.T2));
                case TraitKind.HeadshotRefund: return new TextLine("A headshot puts {0} round back into the magazine.", N(w.T1));
                case TraitKind.RunAndGun: return new TextLine("Movement speed +{0}% while in hand.", P(w.T1));
                case TraitKind.SlowOnHit: return new TextLine("Hits slow enemies by {0}% for {1} s.", P(w.T1), N(w.T2));
                case TraitKind.AmmoOnKill: return new TextLine("A kill puts {0} rounds back into the magazine.", N(w.T1));
                case TraitKind.LongRangeBonus: return new TextLine("+{0}% damage beyond {1} m.", P(w.T1), N(w.T2));
                case TraitKind.CloseRangeBonus: return new TextLine("+{0}% damage within {1} m.", P(w.T1), N(w.T2));
                case TraitKind.DoubleTap: return new TextLine("Every {0} trigger pulls: one extra round for free.", N(w.T1));
                case TraitKind.Pierce: return new TextLine("Rounds pass through {0} enemies.", N(w.T1));
                case TraitKind.ArmorBreaker: return new TextLine("Ignores shields. +{0}% damage to elites.", P(w.T1));
                case TraitKind.SnapAim: return new TextLine("Aim-in time -{0}%.", P(w.T1));
                case TraitKind.StaggerOnHeadshot: return new TextLine("Headshots stagger the enemy for {0} s.", N(w.T1));
                case TraitKind.PatientShot: return new TextLine("Every {1} s aimed without firing: next shot +{0}% damage, up to {2} steps.", P(w.T1), N(w.T2), WeaponTraitState.MaxPatientSteps.ToString());
                case TraitKind.EmptyReloadFast: return new TextLine("Reloading an empty magazine is {0}% faster.", P(w.T1));
                case TraitKind.CloseKnockback: return new TextLine("Hits within {1} m knock enemies back {0} m.", N(w.T1), N(w.T2));
                case TraitKind.StaggerOnHit: return new TextLine("Hits within {1} m stagger the enemy for {0} s.", N(w.T1), N(w.T2));
                case TraitKind.FollowUp: return new TextLine("After a headshot the next shot cycles {0}% faster.", P(w.T1));
                case TraitKind.QuickDraw: return new TextLine("Swapping to or from it is {0}% faster.", P(w.T1));
                case TraitKind.LastRoundDouble: return new TextLine("The last round in the magazine deals x{0} damage.", N(w.T1));
                case TraitKind.SpinUpDamage: return new TextLine("Every round of sustained fire adds +{0}% damage, up to +{1}%.", P(w.T1), P(w.T2));
                case TraitKind.Concussion: return new TextLine("Explosions stagger enemies for {0} s.", N(w.T1));
            }
            return new TextLine("");
        }

        public static TextLine Drawback(DrawbackKind kind, double d1, double d2)
        {
            switch (kind)
            {
                case DrawbackKind.DamageFalloff: return new TextLine("-{0}% damage beyond {1} m.", P(d1), N(d2));
                case DrawbackKind.LowReserve: return new TextLine("Reserve ammunition -{0}%.", P(d1));
                case DrawbackKind.SlowReload: return new TextLine("Reload time +{0}%.", P(d1));
                case DrawbackKind.SmallMagazine: return new TextLine("Small magazine.");
                case DrawbackKind.SlowAds: return new TextLine("Aim-in time +{0}%.", P(d1));
                case DrawbackKind.HeavyRecoil: return new TextLine("Every round of sustained fire widens spread by {0}%, up to {1}%.", P(d1), P(d2));
                case DrawbackKind.SlowSwap: return new TextLine("Weapon swap time +{0}%.", P(d1));
                case DrawbackKind.MoveSlow: return new TextLine("Movement speed -{0}% while carried.", P(d1));
                case DrawbackKind.NoHipFire: return new TextLine("Hip-fire spread x{0}: aim to hit anything.", N(d1));
                case DrawbackKind.NoAds: return new TextLine("Cannot aim down sights.");
                case DrawbackKind.WeakHeadshot: return new TextLine("Headshot bonus -{0}%.", P(d1));
                case DrawbackKind.LongSpinup: return new TextLine("{0} s delay before the first round of a burst.", N(d1));
            }
            return new TextLine("");
        }

        public static TextLine Drawback(RangedWeaponDef w)
        {
            if (w.Drawback == DrawbackKind.SmallMagazine) return new TextLine("Magazine {0} rounds.", RogueArmory.Resolve(w).Magazine.ToString());
            return Drawback(w.Drawback, w.D1, w.D2);
        }

        public static TextLine MeleeSpecialText(MeleeDef m)
        {
            switch (m.Special)
            {
                case MeleeSpecial.Backstab: return new TextLine("Strikes from behind deal x{0} damage.", N(m.S1));
                case MeleeSpecial.Knockback: return new TextLine("Knocks enemies back {0} m.", N(m.S1));
                case MeleeSpecial.GroundSlam: return new TextLine("Slams the ground: hits every enemy within {0} m.", N(m.S1));
                case MeleeSpecial.Combo: return new TextLine("{0}-hit combo; the finisher deals x{1}. Deflects bullets for the first {2} s of a swing.", N(m.S1), N(m.S2), N(m.S3));
                case MeleeSpecial.Throw: return new TextLine("Hold melee to throw it up to {0} m. Walk over it to pick it up.", N(m.S1));
                case MeleeSpecial.Guard: return new TextLine("Hold melee to guard: frontal damage -{0}%, but you cannot shoot. Bash pushes {1} m.", P(m.S1), N(m.S2));
                case MeleeSpecial.Shock: return new TextLine("Stuns for {0} s (elites and bosses {1}% of that).", N(m.S1), P(m.S2));
                case MeleeSpecial.Flurry: return new TextLine("Each hit in a row swings {0}% faster, up to {1} stacks.", P(m.S1), N(m.S2));
            }
            return new TextLine("");
        }

        public static TextLine MeleeDrawback(MeleeDef m)
        {
            if (m.Drawback != DrawbackKind.None) return Drawback(m.Drawback, m.D1, 0);
            return new TextLine("Swing {0} s, recovery {1} s.", N(m.Windup), N(m.Recovery));
        }

        public static TextLine Sight(SightDef s)
        {
            return new TextLine("{0}x magnification. Aim-in time x{1}. Spread while moving and aiming x{2}.", N(s.Magnification), N(s.AdsTimeMul), N(s.AimMoveSpreadMul));
        }

        public static TextLine Heat(int level)
        {
            if (level < 1 || level > RogueHeat.MaxHeat) return new TextLine("");
            var m = RogueHeat.Levels[level - 1];
            string arg = RogueHeat.Arg(m);
            return arg == "" ? new TextLine(m.Text) : new TextLine(m.Text, arg);
        }
    }
}
