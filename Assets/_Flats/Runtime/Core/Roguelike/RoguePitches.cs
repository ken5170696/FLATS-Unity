using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    /// <summary>
    /// The pitch of an item: a few words that say what it does for the player, shown large on a reward card or a shop row, above
    /// the full effect sentence (ItemDef.Effect), which keeps every number. A pitch is an English localization key and never holds
    /// a number, so it cannot drift from the rules; the sentence under it is the source of truth for the mechanic.
    /// </summary>
    public static class RoguePitches
    {
        static readonly Dictionary<string, string> pitches = new Dictionary<string, string>
        {
            { "core.precision", "Headshots that pierce" },
            { "core.assault", "Get close, hit hard" },
            { "core.suppression", "Keep firing, keep growing" },
            { "core.reloadburst", "Reload into a damage burst" },
            { "core.ricochet", "Bullets bounce off walls" },
            { "core.demolition", "Kills explode" },
            { "core.marker", "Mark them for the squad" },
            { "core.mobility", "Move fast, strike on landing" },
            { "mod.long_barrel", "Stronger headshots" },
            { "mod.piercing_rounds", "Pierce one more enemy" },
            { "mod.calm_hands", "Tighter aim" },
            { "mod.close_quarters", "More damage up close" },
            { "mod.adrenaline", "Kills heal you" },
            { "mod.choke", "One more shotgun pellet" },
            { "mod.extended_mag", "Bigger magazine" },
            { "mod.heavy_rounds", "Hit harder, move slower" },
            { "mod.sustained_fire", "Higher Suppression cap" },
            { "mod.fast_hands", "Faster reload" },
            { "mod.tactical_reload", "Reloads give ammo back" },
            { "mod.burst_extender", "Longer Reload Burst" },
            { "mod.rubber_rounds", "Ricochets keep full damage" },
            { "mod.double_bounce", "One more bounce" },
            { "mod.angle_finder", "Ricochets mark enemies" },
            { "mod.bigger_boom", "Bigger kill explosions" },
            { "mod.frag_grenades", "Stronger grenades" },
            { "mod.shockwave", "Explosions slow enemies" },
            { "mod.spotter", "Marks last longer" },
            { "mod.bounty_hunter", "Marked kills pay more" },
            { "mod.team_radio", "Team hits charge your ultimate" },
            { "mod.double_dash", "Dash twice" },
            { "mod.spring_legs", "Jump higher" },
            { "mod.quick_revive", "Revive faster" },
            { "mod.ammo_belt", "More reserve ammo" },
            { "mod.thick_skin", "Take less damage" },
            { "tactical.doublejump", "Jump again in mid-air" },
            { "tactical.dash", "Dash forward" },
            { "tactical.shield", "Raise a shield" },
            { "ult.infinite_fire", "Unlimited ammo, no reloads" },
            { "ult.lethal_shot", "Every hit kills" },
            { "ult.invincible", "Take no damage" },
            { "ult.emergency_revive", "No one stays down" },
            { "ult.enemy_sight", "See every enemy nearby" },
            { "ult.chain_bullets", "Hits chain to nearby enemies" },
            { "ult.homing_bullets", "Bullets seek enemies" },
            { "supply.ammo", "Refill ammo" },
            { "supply.medkit", "A shield as big as your health" },
            { "supply.repair", "Full health and ammo" },
            { "stat.health", "More health" },
            { "stat.damage", "More damage" },
            { "stat.magazine", "Bigger magazine" },
            { "stat.speed", "Move faster" },
        };

        /// <summary>The item's pitch, or "" when it has none (weapons describe themselves by their numbers).</summary>
        public static string Of(string itemId)
        {
            string pitch;
            return itemId != null && pitches.TryGetValue(itemId, out pitch) ? pitch : "";
        }

        public static IEnumerable<string> Ids { get { return pitches.Keys; } }
    }
}
