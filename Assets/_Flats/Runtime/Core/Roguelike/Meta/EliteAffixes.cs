using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    /// <summary>An elite enemy's borrowed skill. Parameters are read by the rules below and by MetaText; nothing else holds numbers.</summary>
    public sealed class AffixDef
    {
        public string Id, Name, Icon;
        public double V1, V2, V3;
        public int MinHeat;
        public string MirrorsSkill = "";   // the player skill it echoes (for the codex and the tooltip)
    }

    /// <summary>
    /// Elite affixes: from Heat 2 elites (and at higher heat finale targets) carry skills borrowed from the
    /// players' tree. Every client derives the same affixes from the run id and the enemy's instance id, so
    /// nothing extra is replicated. Counts grow with heat; the pool opens up with heat as well.
    /// </summary>
    public static class EliteAffixes
    {
        public static readonly AffixDef[] All =
        {
            new AffixDef { Id = "af.berserker", Name = "Berserker", Icon = "Fire", MinHeat = 2, MirrorsSkill = "sk.berserker", V1 = 0.25, V2 = 0.15, V3 = 5 },   // speed, damage, seconds after an ally dies within 15 m
            new AffixDef { Id = "af.suppressor", Name = "Suppressor", Icon = "Snow", MinHeat = 2, MirrorsSkill = "sk.suppressive", V1 = 0.20, V2 = 1.0 },           // player slow fraction, seconds on every hit
            new AffixDef { Id = "af.opening", Name = "Ambusher", Icon = "Flag", MinHeat = 2, MirrorsSkill = "sk.opening_shot", V1 = 0.40 },                        // bonus against a player at full health
            new AffixDef { Id = "af.shield_aura", Name = "Shield Bearer Aura", Icon = "Shield", MinHeat = 4, MirrorsSkill = "sk.rescue_shield", V1 = 0.20, V2 = 10 },  // damage reduction for other enemies within V2 m
            new AffixDef { Id = "af.marksman", Name = "Deadeye", Icon = "Sight", MinHeat = 4, MirrorsSkill = "sk.headhunter", V1 = 0.25, V2 = 30 },               // bonus beyond V2 m
            new AffixDef { Id = "af.guardian", Name = "Last Stand", Icon = "Wings", MinHeat = 6, MirrorsSkill = "sk.guardian", V1 = 1.5 },                        // survives the first lethal hit, V1 s invulnerable
        };

        public const double BerserkerRange = 15;

        public static AffixDef Def(string id) { foreach (var a in All) if (a.Id == id) return a; return null; }

        /// <summary>How many affixes an enemy carries at a heat level.</summary>
        public static int Count(int heat, bool elite, bool finale)
        {
            heat = RogueHeat.Clamp(heat);
            if (heat < 2) return 0;
            if (finale) return heat >= 8 ? 2 : heat >= 5 ? 1 : 0;
            if (!elite) return 0;
            return heat >= 8 ? 2 : 1;
        }

        /// <summary>The affixes of one enemy: deterministic from run id and instance id, distinct, from the pool open at this heat.</summary>
        public static string[] For(string runId, int instanceId, int heat, bool elite, bool finale)
        {
            int n = Count(heat, elite, finale);
            if (n <= 0) return new string[0];
            var pool = new List<AffixDef>();
            foreach (var a in All) if (a.MinHeat <= heat) pool.Add(a);
            ulong h = 14695981039346656037UL;
            foreach (char c in (runId ?? "") + "#" + instanceId) { h ^= c; h = unchecked(h * 1099511628211UL); }
            var rng = new RogueRng(h);
            var picked = new List<string>();
            while (picked.Count < n && pool.Count > 0) { int i = rng.Range(0, pool.Count); picked.Add(pool[i].Id); pool.RemoveAt(i); }
            return picked.ToArray();
        }

        public static bool Has(IList<string> affixes, string id) { return affixes != null && affixes.IndexOf(id) >= 0; }

        /// <summary>Damage an affixed enemy deals to a player (ambusher on a full-health player, deadeye at range, berserker while enraged).</summary>
        public static double OutgoingMul(IList<string> affixes, bool playerAtFullHealth, double distance, bool enraged)
        {
            double m = 1;
            if (affixes == null) return m;
            foreach (var id in affixes)
            {
                var a = Def(id); if (a == null) continue;
                switch (id)
                {
                    case "af.opening": if (playerAtFullHealth) m *= 1 + a.V1; break;
                    case "af.marksman": if (distance >= a.V2) m *= 1 + a.V1; break;
                    case "af.berserker": if (enraged) m *= 1 + a.V2; break;
                }
            }
            return m;
        }

        /// <summary>Incoming damage multiplier for an enemy standing within an ally's shield aura (never its own).</summary>
        public static double AuraMul(bool insideAnotherAura) { return insideAnotherAura ? 1 - Def("af.shield_aura").V1 : 1; }
    }
}
