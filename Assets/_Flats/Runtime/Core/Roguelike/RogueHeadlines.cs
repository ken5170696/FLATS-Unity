using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Flats.Core.Roguelike
{
    /// <summary>
    /// The one number a reward tile or a shop tile shows large: the benefit itself (the damage a core adds, the seconds an
    /// ultimate lasts), never a threshold, a cap or a range that merely appears first in the sentence. The value is always one
    /// of the effect sentence's own arguments (ItemDef.EffectArgs), or the first number of a fixed sentence, so it cannot drift
    /// from the rules. The format is an English localization key ("+{0}%", "{0} s"). Items whose benefit is not a number
    /// (one more pierce, a second dash charge) have no headline: the tile shows its icon instead.
    /// </summary>
    public static class RogueHeadlines
    {
        struct Entry { public int Arg; public string Format; public Entry(int arg, string format) { Arg = arg; Format = format; } }

        // Arg is the index into ItemDef.EffectArgs(tier); -1 takes the first number of a fixed sentence.
        static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>
        {
            { "stat.health", new Entry(0, "+{0}%") }, { "stat.damage", new Entry(0, "+{0}%") },
            { "stat.magazine", new Entry(0, "+{0}%") }, { "stat.speed", new Entry(0, "+{0}%") },
            { "core.precision", new Entry(0, "+{0}%") },      // headshot damage
            { "core.assault", new Entry(0, "+{0}%") },        // close-range damage
            { "core.suppression", new Entry(1, "+{0}%") },    // the damage cap, not the per-hit step
            { "core.reloadburst", new Entry(1, "+{0}%") },    // the burst's damage, not the magazine share that triggers it
            { "core.ricochet", new Entry(2, "+{0}%") },       // the bonus on a ricochet hit
            { "core.demolition", new Entry(0, "{0}%") },      // the share of the killing damage
            { "core.marker", new Entry(1, "+{0}%") },         // damage taken by marked enemies, not the mark's seconds
            { "core.mobility", new Entry(0, "+{0}%") },       // speed
            { "mod.long_barrel", new Entry(0, "+{0}%") }, { "mod.calm_hands", new Entry(0, "-{0}%") },
            { "mod.close_quarters", new Entry(0, "+{0}%") }, { "mod.adrenaline", new Entry(0, "+{0}%") },
            { "mod.extended_mag", new Entry(0, "+{0}%") }, { "mod.heavy_rounds", new Entry(0, "+{0}%") },
            { "mod.sustained_fire", new Entry(0, "+{0}%") }, { "mod.fast_hands", new Entry(0, "-{0}%") },
            { "mod.tactical_reload", new Entry(0, "+{0}") }, { "mod.burst_extender", new Entry(0, "+{0} s") },
            { "mod.rubber_rounds", new Entry(-1, "{0}%") }, { "mod.angle_finder", new Entry(0, "{0} s") },
            { "mod.bigger_boom", new Entry(0, "+{0}%") }, { "mod.frag_grenades", new Entry(0, "+{0}%") },
            { "mod.shockwave", new Entry(1, "{0} s") },       // the slow's seconds grow with the tier; its strength does not
            { "mod.spotter", new Entry(0, "+{0} s") }, { "mod.bounty_hunter", new Entry(0, "+{0}%") },
            { "mod.spring_legs", new Entry(0, "+{0}%") }, { "mod.quick_revive", new Entry(0, "+{0}%") },
            { "mod.ammo_belt", new Entry(0, "+{0}%") }, { "mod.thick_skin", new Entry(0, "-{0}%") },
            { "tactical.dash", new Entry(-1, "{0} m") }, { "tactical.shield", new Entry(-1, "{0}") },
            { "ult.infinite_fire", new Entry(-1, "{0} s") }, { "ult.lethal_shot", new Entry(-1, "{0} s") },
            { "ult.invincible", new Entry(-1, "{0} s") }, { "ult.enemy_sight", new Entry(-1, "{0} s") },
            { "ult.chain_bullets", new Entry(-1, "{0} s") }, { "ult.homing_bullets", new Entry(-1, "{0} s") },
        };

        static readonly Regex firstNumber = new Regex(@"\d+(\.\d+)?");

        /// <summary>The headline of an item at a tier: its format key and the value to put in it. False when the item has none.</summary>
        public static bool TryGet(ItemDef def, int tier, out string format, out string value)
        {
            format = ""; value = "";
            Entry entry;
            if (def == null || !entries.TryGetValue(def.Id, out entry)) return false;
            if (entry.Arg < 0)
            {
                var match = firstNumber.Match(def.Effect ?? "");
                if (!match.Success) return false;
                value = match.Value;
            }
            else
            {
                var args = def.EffectArgs(tier);
                if (entry.Arg >= args.Length) return false;
                value = args[entry.Arg];
            }
            format = entry.Format;
            return true;
        }

        public static IEnumerable<string> Ids { get { return entries.Keys; } }
    }
}
