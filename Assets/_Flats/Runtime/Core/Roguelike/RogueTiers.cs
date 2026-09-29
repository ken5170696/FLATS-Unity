using System;
using System.Collections.Generic;
using System.Globalization;

namespace Flats.Core.Roguelike
{
    /// <summary>Authoring table: each group of three numbers is one parameter at T1/T2/T3.</summary>
    public static class RogueTiers
    {
        private static readonly Dictionary<string, double[]> Values = new Dictionary<string, double[]>(StringComparer.Ordinal)
        {
            { "core.precision", new double[] { 1.25, 1.35, 1.45, 0.9, 0.95, 1, 1, 1, 2 } },
            { "core.assault", new double[] { 1.15, 1.22, 1.3, 0.3, 0.35, 0.4, 2, 2.5, 3 } },
            { "core.suppression", new double[] { 0.4, 0.5, 0.6, 1.2, 1.25, 1.3, 1.2, 1.15, 1.1 } },
            { "core.reloadburst", new double[] { 1.35, 1.45, 1.55, 3, 3.5, 4, 0.6, 0.5, 0.4 } },
            { "core.ricochet", new double[] { 1, 1, 2, 1.3, 1.4, 1.5 } },
            { "core.demolition", new double[] { 0.4, 0.55, 0.7, 6, 7, 8 } },
            { "core.marker", new double[] { 1.12, 1.16, 1.2, 4, 5, 6 } },
            { "core.mobility", new double[] { 1.12, 1.15, 1.18, 1.4, 1.5, 1.6, 0.7, 0.6, 0.5, 0.2, 0.25, 0.3 } },
            { "mod.long_barrel", new double[] { 1.1, 1.16, 1.2 } },
            { "mod.piercing_rounds", new double[] { 1, 1, 1 } },
            { "mod.calm_hands", new double[] { 0.75, 0.65, 0.6 } },
            { "mod.close_quarters", new double[] { 1.1, 1.16, 1.2 } },
            { "mod.adrenaline", new double[] { 0.05, 0.08, 0.1 } },
            { "mod.choke", new double[] { 1, 1, 1 } },
            { "mod.extended_mag", new double[] { 1.25, 1.4, 1.5 } },
            { "mod.heavy_rounds", new double[] { 1.06, 1.1, 1.12, 0.97, 0.96, 0.95 } },
            { "mod.sustained_fire", new double[] { 0.6, 0.7, 0.8 } },
            { "mod.fast_hands", new double[] { 0.75, 0.65, 0.6 } },
            { "mod.tactical_reload", new double[] { 2, 3, 4 } },
            { "mod.burst_extender", new double[] { 5, 6, 7 } },
            { "mod.rubber_rounds", new double[] { 1, 1, 1 } },
            { "mod.double_bounce", new double[] { 1, 1, 1 } },
            { "mod.angle_finder", new double[] { 4, 4, 4 } },
            { "mod.bigger_boom", new double[] { 1.5, 1.8, 2 } },
            { "mod.frag_grenades", new double[] { 1.3, 1.48, 1.6 } },
            { "mod.shockwave", new double[] { 2, 2.5, 3 } },
            { "mod.spotter", new double[] { 3, 5, 6 } },
            { "mod.bounty_hunter", new double[] { 0.1, 0.16, 0.2 } },
            { "mod.team_radio", new double[] { 1, 1, 1 } },
            { "mod.double_dash", new double[] { 2, 2, 2 } },
            { "mod.spring_legs", new double[] { 1.3, 1.4, 1.5 } },
            { "mod.quick_revive", new double[] { 1.4, 1.55, 1.7 } },
            { "mod.ammo_belt", new double[] { 1.3, 1.48, 1.6 } },
            { "mod.thick_skin", new double[] { 0.92, 0.87, 0.84 } },
        };

        public static double Value(string id, int tier, int parameter = 0)
        {
            double[] values;
            if (!Values.TryGetValue(id, out values) || parameter < 0 || parameter >= values.Length / 3) throw new ArgumentException("tier parameter");
            return values[parameter * 3 + Math.Max(0, Math.Min(2, tier - 1))];
        }

        internal static void Hash(Action<string> mix)
        {
            var ids = new List<string>(Values.Keys); ids.Sort(StringComparer.Ordinal);
            foreach (var id in ids) { mix(id); foreach (var value in Values[id]) mix("|" + value.ToString("R", CultureInfo.InvariantCulture)); }
        }
    }
}

