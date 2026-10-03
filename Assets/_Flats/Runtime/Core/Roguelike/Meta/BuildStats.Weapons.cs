using System;

namespace Flats.Core.Roguelike
{
    /// <summary>
    /// Weapon handling numbers of the run mods added on 2026-10-04 (Rapid Fire, Heavy Swing, Quick Swing, the five-tier Choke).
    /// Defaults are neutral, so a build without these mods computes exactly as before. BuildStats.Compute sets them from RogueTiers
    /// and <see cref="ClampWeapons"/> bounds them with the other clamps.
    /// </summary>
    public sealed partial class BuildStats
    {
        /// <summary>Rapid Fire: rounds per second multiplier of the gun in hand (the Shoot coroutine divides its round interval by it).
        /// Handguns are semi-automatic (WeaponRules.HandgunMinInterval) and do not read it.</summary>
        public double FireRateMul = 1;
        /// <summary>Quick Swing: multiplier of a melee swing's windup and recovery time (0.5 = twice as fast).</summary>
        public double MeleeSwingTimeMul = 1;

        // owner request 2026-10-04: no stacking caps. Only safety bounds remain (a swing cannot take zero time, the blast effect has a visual size limit).
        public const double MinMeleeSwingTimeMul = 0.35;
        /// <summary>Most extra pellets Choke may add to one shell (its top tier).</summary>
        public const int MaxExtraPellets = 10;
        /// <summary>Bounds of the two explosion multipliers every explosion the player causes reads (grenades, launcher rounds, kill explosions).</summary>
        public const double MaxBlastEffectScale = 3.0;

        static void ClampWeapons(BuildStats s)
        {
            s.FireRateMul = Math.Max(1.0, s.FireRateMul);
            s.MeleeSwingTimeMul = Math.Max(MinMeleeSwingTimeMul, Math.Min(1.0, s.MeleeSwingTimeMul));
            s.ExtraPellets = Math.Max(0, Math.Min(MaxExtraPellets, s.ExtraPellets));
            s.ExplosionRadiusMul = Math.Max(1.0, s.ExplosionRadiusMul);
            s.ExplosionDamageMul = Math.Max(1.0, s.ExplosionDamageMul);
        }

        /// <summary>Pellets one shell of this build fires: the shell's own pellets (RogueArmory.ShellPellets) plus Choke on a pellet shotgun.</summary>
        public int ShellPellets(bool oneShot, bool grenade, int burst)
        {
            return RogueArmory.ShellPellets(oneShot, grenade, burst) + (RogueArmory.IsPelletShell(oneShot, grenade, burst) ? ExtraPellets : 0);
        }

        /// <summary>Radius of a grenade or launcher blast of <paramref name="baseRadius"/> metres for this build (Bigger Boom).</summary>
        public double BlastRadius(double baseRadius) { return Math.Max(0, baseRadius) * ExplosionRadiusMul; }

        /// <summary>Damage lost per metre from the blast centre: a larger blast keeps its damage over a proportionally longer distance,
        /// so the edge of a bigger blast is not an empty ring.</summary>
        public double BlastFalloffPerMetre(double baseFalloff) { return Math.Max(0, baseFalloff) / ExplosionRadiusMul; }
    }
}
