using System;

namespace Flats.Core.Roguelike
{
    /// <summary>Timing and damage of one swing, decided before the swing starts.</summary>
    public struct MeleeSwing
    {
        public double Windup, Recovery, DamageMul;
        public int ComboIndex;             // 0-based position in a Combo weapon's chain
        public bool Finisher;
    }

    /// <summary>
    /// Melee rules shared by every copy. The swing timing runs on the owner; hit resolution
    /// (who was hit, backstab, stun, knockback) is computed from the same functions on the
    /// authority, so the owner's prediction and the master's ruling cannot disagree.
    /// </summary>
    public static class MeleeRules
    {
        public const double ComboWindow = 0.8, FlurryWindow = 1.2, BackstabAngle = 70, EliteStunFloor = 0.25;
        public const double ThrowHoldSeconds = 0.45, ThrowSpeed = 28, PickupRadius = 2.2;
        public const double GuardMoveMultiplier = 0.65;

        /// <summary>Damage of one hit on one target before build multipliers.</summary>
        public static double HitDamage(MeleeDef d, MeleeSwing swing, bool fromBehind)
        {
            if (d == null) return 0;
            double dmg = d.Damage * swing.DamageMul;
            if (d.Special == MeleeSpecial.Backstab && fromBehind) dmg *= d.S1;
            return dmg;
        }

        /// <summary>True when the attacker stands behind the target: the angle between the target's facing and the direction to the attacker exceeds 180 - BackstabAngle.</summary>
        public static bool IsBehind(double targetForwardX, double targetForwardZ, double toAttackerX, double toAttackerZ)
        {
            double lf = Math.Sqrt(targetForwardX * targetForwardX + targetForwardZ * targetForwardZ);
            double la = Math.Sqrt(toAttackerX * toAttackerX + toAttackerZ * toAttackerZ);
            if (lf < 1e-6 || la < 1e-6) return false;
            double cos = (targetForwardX * toAttackerX + targetForwardZ * toAttackerZ) / (lf * la);
            double angle = Math.Acos(Math.Max(-1, Math.Min(1, cos))) * 180 / Math.PI;
            return angle >= 180 - BackstabAngle;
        }

        public static double StunSeconds(MeleeDef d, bool eliteOrFinale)
        {
            if (d == null || d.Special != MeleeSpecial.Shock) return 0;
            return eliteOrFinale ? Math.Max(EliteStunFloor, d.S1 * d.S2) : d.S1;
        }

        public static double KnockbackMeters(MeleeDef d)
        {
            if (d == null) return 0;
            if (d.Special == MeleeSpecial.Knockback) return d.S1;
            if (d.Special == MeleeSpecial.Guard) return d.S2;
            if (d.Special == MeleeSpecial.GroundSlam) return 1.5;
            return 0;
        }

        /// <summary>Radius of the hit volume: a ground slam hits every enemy around the impact point.</summary>
        public static double HitRadius(MeleeDef d) { return d == null ? 0 : d.Special == MeleeSpecial.GroundSlam ? d.S1 : d.Radius; }
        public static bool HitsAll(MeleeDef d) { return d != null && (d.Special == MeleeSpecial.GroundSlam); }

        /// <summary>Incoming damage multiplier for the carrier of a guard weapon.</summary>
        public static double GuardDamageMul(MeleeDef d, bool guarding, bool frontal)
        {
            if (d == null || d.Special != MeleeSpecial.Guard || !guarding || !frontal) return 1;
            return 1 - d.S1;
        }

        /// <summary>Katana: bullets that hit during the first S3 seconds of a swing are deflected (no damage).</summary>
        public static bool Deflects(MeleeDef d, double secondsIntoSwing)
        {
            return d != null && d.Special == MeleeSpecial.Combo && secondsIntoSwing >= 0 && secondsIntoSwing <= d.S3;
        }

        public static bool BlocksFire(MeleeDef d, bool guarding) { return d != null && d.Special == MeleeSpecial.Guard && guarding; }
    }

    /// <summary>Owner-side swing sequencing (combo chain, flurry stacks). Reset on death, swap of loadout or run end.</summary>
    public sealed class MeleeState
    {
        public MeleeDef Def { get; private set; }
        double lastSwingEnd = -100, lastHit = -100;
        int comboIndex, flurry;
        public bool AxeThrown;

        public MeleeState(MeleeDef def) { Def = def; }

        public int FlurryStacks { get { return flurry; } }

        public void Reset() { lastSwingEnd = -100; lastHit = -100; comboIndex = 0; flurry = 0; AxeThrown = false; }

        public MeleeSwing Begin(double now)
        {
            var s = new MeleeSwing { Windup = Def.Windup, Recovery = Def.Recovery, DamageMul = 1 };
            if (Def.Special == MeleeSpecial.Combo)
            {
                if (now - lastSwingEnd > MeleeRules.ComboWindow) comboIndex = 0;
                s.ComboIndex = comboIndex;
                s.Finisher = comboIndex == (int)Def.S1 - 1;
                if (s.Finisher) s.DamageMul = Def.S2;
                comboIndex = s.Finisher ? 0 : comboIndex + 1;
            }
            if (Def.Special == MeleeSpecial.Flurry)
            {
                if (now - lastHit > MeleeRules.FlurryWindow) flurry = 0;
                double speed = 1 + Def.S1 * flurry;
                s.Windup /= speed; s.Recovery /= speed;
            }
            return s;
        }

        public void End(double now) { lastSwingEnd = now; }

        public void OnHit(double now)
        {
            if (Def.Special == MeleeSpecial.Flurry)
            {
                if (now - lastHit > MeleeRules.FlurryWindow) flurry = 0;
                flurry = Math.Min((int)Def.S2, flurry + 1);
            }
            lastHit = now;
        }
    }
}
