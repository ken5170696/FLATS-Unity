using System;
using System.Collections.Generic;
using System.Globalization;

namespace Flats.Core.Roguelike
{
    /// <summary>
    /// Authoring table: each group of <see cref="Count"/> numbers is one parameter at T1..T5. T4 and T5 continue the T2 to T3 step with
    /// diminishing returns; integer parameters (pierce, bounces) stay at their T3 value. Single-tier items repeat one value. Two rows are
    /// ADDED to their core by RogueBuild (mod.sustained_fire: cap bonus, mod.burst_extender: seconds), so the mod is never a no-op at a
    /// high core tier. mod.spring_legs stops at T3 (RogueCatalog.MaxTier): the jump clamp is x1.5 and maps are built for it.
    /// </summary>
    public static class RogueTiers
    {
        private static readonly Dictionary<string, double[]> Values = new Dictionary<string, double[]>(StringComparer.Ordinal)
        {
            { "core.precision", new double[] { 1.25, 1.35, 1.45, 1.53, 1.6,  0.9, 0.95, 1, 1, 1,  1, 1, 2, 2, 2 } },
            { "core.assault", new double[] { 1.15, 1.22, 1.3, 1.36, 1.4,  0.3, 0.35, 0.4, 0.43, 0.45,  2, 2.5, 3, 3.5, 4 } },
            { "core.suppression", new double[] { 0.4, 0.5, 0.6, 0.65, 0.7,  1.2, 1.25, 1.3, 1.33, 1.35,  1.2, 1.15, 1.1, 1.07, 1.05 } },
            { "core.reloadburst", new double[] { 1.35, 1.45, 1.55, 1.62, 1.68,  3, 3.5, 4, 4.5, 5,  0.6, 0.5, 0.4, 0.35, 0.3 } },
            { "core.ricochet", new double[] { 1, 1, 2, 2, 2,  1.3, 1.4, 1.5, 1.58, 1.65 } },
            { "core.demolition", new double[] { 0.4, 0.55, 0.7, 0.8, 0.9,  6, 7, 8, 8.5, 9 } },
            { "core.marker", new double[] { 1.12, 1.16, 1.2, 1.23, 1.25,  4, 5, 6, 6.5, 7 } },
            { "core.mobility", new double[] { 1.12, 1.15, 1.18, 1.2, 1.22,  1.4, 1.5, 1.6, 1.68, 1.75,  0.7, 0.6, 0.5, 0.45, 0.4,  0.2, 0.25, 0.3, 0.33, 0.35 } },
            { "mod.long_barrel", new double[] { 1.1, 1.16, 1.2, 1.23, 1.25 } },
            { "mod.piercing_rounds", new double[] { 1, 1, 1, 1, 1 } },
            { "mod.calm_hands", new double[] { 0.75, 0.65, 0.6, 0.55, 0.5 } },
            { "mod.close_quarters", new double[] { 1.1, 1.16, 1.2, 1.23, 1.25 } },
            { "mod.adrenaline", new double[] { 0.05, 0.08, 0.1, 0.11, 0.12 } },
            { "mod.choke", new double[] { 1, 1, 1, 1, 1 } },
            { "mod.extended_mag", new double[] { 1.25, 1.4, 1.5, 1.58, 1.65 } },
            { "mod.heavy_rounds", new double[] { 1.06, 1.1, 1.12, 1.14, 1.15,  0.97, 0.96, 0.95, 0.95, 0.95 } },
            { "mod.sustained_fire", new double[] { 0.2, 0.25, 0.3, 0.33, 0.35 } },
            { "mod.fast_hands", new double[] { 0.75, 0.65, 0.6, 0.57, 0.55 } },
            { "mod.tactical_reload", new double[] { 2, 3, 4, 5, 6 } },
            { "mod.burst_extender", new double[] { 2, 2.5, 3, 3.5, 4 } },
            { "mod.rubber_rounds", new double[] { 1, 1, 1, 1, 1 } },
            { "mod.double_bounce", new double[] { 1, 1, 1, 1, 1 } },
            { "mod.angle_finder", new double[] { 4, 4, 4, 4, 4 } },
            { "mod.bigger_boom", new double[] { 1.5, 1.8, 2, 2.1, 2.2 } },
            { "mod.frag_grenades", new double[] { 1.3, 1.48, 1.6, 1.68, 1.75 } },
            { "mod.shockwave", new double[] { 2, 2.5, 3, 3.5, 4 } },
            { "mod.spotter", new double[] { 3, 5, 6, 6.5, 7 } },
            { "mod.bounty_hunter", new double[] { 0.1, 0.16, 0.2, 0.23, 0.25 } },
            { "mod.team_radio", new double[] { 1, 1, 1, 1, 1 } },
            { "mod.double_dash", new double[] { 2, 2, 2, 2, 2 } },
            { "mod.spring_legs", new double[] { 1.3, 1.4, 1.5, 1.5, 1.5 } },
            { "mod.quick_revive", new double[] { 1.4, 1.55, 1.7, 1.8, 1.9 } },
            { "mod.ammo_belt", new double[] { 1.3, 1.48, 1.6, 1.7, 1.8 } },
            { "mod.thick_skin", new double[] { 0.92, 0.87, 0.84, 0.82, 0.8 } },
        };

        /// <summary>Values per parameter (the highest tier any item can reach).</summary>
        public const int Count = 5;

        public static double Value(string id, int tier, int parameter = 0)
        {
            double[] values;
            if (!Values.TryGetValue(id, out values) || parameter < 0 || parameter >= values.Length / Count) throw new ArgumentException("tier parameter");
            return values[parameter * Count + Math.Max(0, Math.Min(Count - 1, tier - 1))];
        }

        /// <summary>Authoring guard: every row holds whole groups of <see cref="Count"/> values.</summary>
        internal static void Validate(List<string> errors)
        {
            foreach (var pair in Values) if (pair.Value.Length == 0 || pair.Value.Length % Count != 0) errors.Add("tier row is not groups of " + Count + ": " + pair.Key);
        }

        internal static void Hash(Action<string> mix)
        {
            var ids = new List<string>(Values.Keys); ids.Sort(StringComparer.Ordinal);
            foreach (var id in ids) { mix(id); foreach (var value in Values[id]) mix("|" + value.ToString("R", CultureInfo.InvariantCulture)); }
        }
    }
}

