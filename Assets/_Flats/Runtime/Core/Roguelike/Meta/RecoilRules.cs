using System;

namespace Flats.Core.Roguelike
{
    /// <summary>One round's camera kick for the shooter's own view.</summary>
    public struct RecoilKick
    {
        public double PitchDegrees;      // upward
        public double YawDegrees;        // largest sideways drift, either direction
    }

    /// <summary>
    /// Weapon handling numbers of a Roguelike run that the legacy catalogue never had: a base inaccuracy and a per-round kick.
    ///
    /// The legacy spread is Random(+-(100 - accuracy)). Thirteen of the sixteen base models have accuracy 100, so every spread
    /// multiplier (hip fire, No Hip Fire, Heavy Recoil, aim settle, Calm Hands, Steady Breath) multiplied zero and did nothing on
    /// them. A Roguelike round therefore never has less than a small base inaccuracy: clearly present from the hip, barely there
    /// while aiming, so those multipliers act on every weapon and aiming is the accurate way to shoot. Classic modes keep the
    /// legacy numbers untouched.
    /// </summary>
    public static class RecoilRules
    {
        /// <summary>Least inaccuracy (legacy units: 1500 forward, so 8 is about 0.3 degrees) of a round fired from the hip.</summary>
        public const double HipBaseInaccuracy = 8;
        /// <summary>Least inaccuracy of an aimed round (about 0.06 degrees): a settled sight is all but exact.</summary>
        public const double AimBaseInaccuracy = 1.5;

        public const double AimKickMul = 0.7;
        public const double YawFraction = 0.3;
        public const double MinVariantMul = 0.75, MaxVariantMul = 1.6;

        /// <summary>Half-width of the random spread of one round, in the legacy units, before the per-round multipliers.</summary>
        public static double Inaccuracy(double accuracy, bool aiming)
        {
            return Math.Max(100 - accuracy, aiming ? AimBaseInaccuracy : HipBaseInaccuracy);
        }

        /// <summary>Upward kick in degrees of one round of a base model fired from the hip.</summary>
        public static double ClassKick(WeaponDefinition d)
        {
            if (d == null) return 0;
            if (d.grenade) return 2.2;
            if (d.oneShot && d.burstCount > 1) return 2.6;            // shotguns: one shell
            if (d.oneShot) return 3.0;                                // bolt-action rifles
            if (d.handgun) return 1.1;
            if (d.limitAmmo >= 100) return 0.55;                      // machine gun
            if (d.zoom >= 3) return 0.5;                              // assault rifles
            return 0.32;                                              // submachine guns
        }

        /// <summary>
        /// The kick of one round. <paramref name="damageRatio"/> is the weapon's damage over its base model's (a harder-hitting
        /// armory variant kicks more); <paramref name="recoilMul"/> comes from the round's modifiers (Heavy Recoil).
        /// </summary>
        public static RecoilKick Kick(WeaponDefinition baseModel, double damageRatio, bool aiming, double recoilMul)
        {
            double variant = damageRatio > 0 ? Math.Sqrt(damageRatio) : 1;
            if (variant < MinVariantMul) variant = MinVariantMul;
            if (variant > MaxVariantMul) variant = MaxVariantMul;
            double pitch = ClassKick(baseModel) * variant * (aiming ? AimKickMul : 1) * (recoilMul > 0 ? recoilMul : 1);
            return new RecoilKick { PitchDegrees = pitch, YawDegrees = pitch * YawFraction };
        }
    }
}
