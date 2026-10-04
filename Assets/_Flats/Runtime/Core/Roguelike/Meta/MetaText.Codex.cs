using System;
using System.Collections.Generic;
using System.Globalization;

namespace Flats.Core.Roguelike
{
    /// <summary>One number of an item's tier table: which template argument it is, what it is called and how it is written.</summary>
    public sealed class CodexTierParam
    {
        public readonly int Arg; public readonly string Label, Format;
        public CodexTierParam(int arg, string label, string format) { Arg = arg; Label = label ?? ""; Format = format ?? "{0}"; }
    }

    /// <summary>One rendered cell of a tier row: label key, format key ("+{0}%") and the value from ItemDef.EffectArgs.</summary>
    public struct CodexCell { public string Label, Format, Value; }

    /// <summary>One armory melee weapon in an enemy's hand: the numbers EnemyMeleeRules derives from the weapon row.</summary>
    public sealed class CodexEnemyMelee { public string Id, Name; public TextLine Hit, Special; }

    /// <summary>
    /// The codex (headquarters, "Records &amp; codex"): plain-language explanations of every item, enemy role, elite affix, mission
    /// and lifetime statistic. Every string is an English translation key (FlatsChinese.txt holds its row). Explanations never hold
    /// a number: numbers come from the rule tables through templates (ItemDef.EffectArgs, EnemyRoleDef, EnemyMeleeRules,
    /// EliteAffixes, RogueEncounterGuide), so a balance change moves the text with it. <see cref="Validate"/> is the authoring guard.
    /// </summary>
    public static class RogueCodex
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static string N(double v) { return RogueArmory.Num(v); }
        static string P(double fraction) { return RogueArmory.Pct(Math.Abs(fraction)); }
        static CodexTierParam Tp(int arg, string label, string format) { return new CodexTierParam(arg, label, format); }

        // ------------------------------------------------------------------ items
        /// <summary>The kinds the codex explains (the four build kinds the profile records as seen, plus the stat upgrades).</summary>
        public static readonly ItemKind[] ItemKinds = { ItemKind.Stat, ItemKind.Core, ItemKind.Mod, ItemKind.Tactical, ItemKind.Ultimate };

        public static bool IsCodexKind(ItemKind kind) { return Array.IndexOf(ItemKinds, kind) >= 0; }

        public static IEnumerable<ItemDef> Items()
        {
            foreach (var i in RogueCatalog.Stats) yield return i;
            foreach (var i in RogueCatalog.Cores) yield return i;
            foreach (var i in RogueCatalog.Mods) yield return i;
            foreach (var i in RogueCatalog.Tacticals) yield return i;
            foreach (var i in RogueCatalog.Ultimates) yield return i;
        }

        /// <summary>True when a new run can still hand the item out (RogueShop.Obtainable: the shop's own exclusion rule).</summary>
        public static bool Obtainable(ItemDef def) { return def != null && RogueShop.Obtainable(def.Id); }

        /// <summary>True when the codex page lists the item for a profile: everything a run can still hand out, plus an item that
        /// no longer can but that this profile already owned once (<paramref name="seen"/>: the profile's seenItems; null = none).
        /// An item nobody can get any more is not a hole in everybody else's "Discovered" count.</summary>
        public static bool Listed(ItemDef def, ICollection<string> seen)
        {
            return def != null && (Obtainable(def) || (seen != null && seen.Contains(def.Id)));
        }

        /// <summary>The items the codex page lists for a profile (<see cref="Listed"/>), in the order of <see cref="Items()"/>.</summary>
        public static IEnumerable<ItemDef> Items(ICollection<string> seen)
        {
            foreach (var i in Items()) if (Listed(i, seen)) yield return i;
        }

        /// <summary>How many items of a kind the page lists for a profile: the denominator of its "Discovered" count.</summary>
        public static int ItemCount(ItemKind kind, ICollection<string> seen)
        {
            int n = 0; foreach (var i in Items(seen)) if (i.Kind == kind) n++;
            return n;
        }

        /// <summary>"How to use it": when it helps, what it goes with, what to watch for. One paragraph per entry, no numbers.</summary>
        static readonly Dictionary<string, string[]> guides = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            // ---- stats
            { "stat.health", new[] { "The safest pick when you keep getting knocked down.", "Regeneration and Adrenaline heal a share of your maximum health, so more health makes both of them heal more.", "Buying a tier also heals you by the amount it adds." } },
            { "stat.damage", new[] { "Every bullet you fire hits harder, at any range and against any enemy.", "It multiplies with the bonuses of cores and mods, so it is never wasted." } },
            { "stat.magazine", new[] { "More rounds before each reload.", "Best on weapons with a small magazine, and with the Suppression core, which rewards long bursts.", "Every tier adds at least one round." } },
            { "stat.speed", new[] { "You run faster all the time: easier to reach cover, get away from melee enemies and reach a downed teammate.", "It does not change how fast you aim or reload." } },
            { "stat.regen", new[] { "Heals you a little every second, in and out of a fight, but not while you are downed.", "Strong in long stages and together with Vitality. It will not save you from one big burst of damage." } },
            // ---- cores
            { "core.precision", new[] { "For players who aim at heads: headshots hit much harder, and each bullet goes on through the enemies behind the first one.", "At low tiers body shots are a little weaker, so it suits accurate rifles and sniper rifles better than spray weapons.", "Goes well with Long Barrel, Piercing Rounds and Calm Hands." } },
            { "core.assault", new[] { "For fighting up close: you deal more damage inside the close range and a little less far away.", "Each kill at close range makes you tougher and faster for a few seconds, so keep moving from one kill to the next.", "Goes well with shotguns and SMGs, Close Quarters and Adrenaline." } },
            { "core.suppression", new[] { "For weapons that hold the trigger down: every hit adds a little damage, up to a cap.", "Stop hitting for a moment and the bonus drains away step by step. A reload keeps half of it.", "It also gives a bigger magazine, and at low tiers a slower reload.", "Goes well with Extended Magazine, Sustained Fire and the Infinite Fire ultimate." } },
            { "core.reloadburst", new[] { "Fire most of the magazine, reload, and for the next few seconds you deal bonus damage.", "Reloading too early, before enough of the magazine is fired, gives nothing.", "Best with small magazines and quick reloads. Goes well with Fast Hands and Burst Extender." } },
            { "core.ricochet", new[] { "Bullets that hit a wall bounce on and can still hit an enemy, and a bounced hit deals bonus damage.", "Strong indoors and in corridors. Weak on open ground, where there is nothing to bounce off.", "Rubber Rounds, Double Bounce and Angle Finder build on it." } },
            { "core.demolition", new[] { "Every enemy you kill blows up and hurts the enemies around it.", "The blast is a share of the damage of the killing hit, so a big hit makes a big blast. A blast never sets off another blast.", "Best against tight groups. Bigger Boom, Explosive Payload and Shockwave build on it." } },
            { "core.marker", new[] { "Enemies you hit are marked for a few seconds. A marked enemy takes more damage from the whole squad, not only from you.", "Whenever anyone kills a marked enemy, your ultimate charges.", "A team core: tag many enemies and let teammates finish them. Spotter and Bounty Hunter build on it." } },
            { "core.mobility", new[] { "You move and revive faster, carry mission objects without slowing down, and your dash comes back sooner.", "The first shot right after a dash or after landing from a jump hits harder: jump or dash first, then shoot.", "Goes well with the Dash and Double Jump tacticals and with weapons that hit hard with one shot." } },
            // ---- mods
            { "mod.long_barrel", new[] { "A plain boost to headshots. Take it if you aim for heads anyway.", "It multiplies with the Precision core. It does nothing for body shots." } },
            { "mod.piercing_rounds", new[] { "Each bullet passes through one more enemy. Useful when enemies line up in corridors and doorways.", "There is a limit to how many enemies one bullet can pierce. The shop stops offering this once you are at the limit." } },
            { "mod.calm_hands", new[] { "Your shots spread less, but only while you aim down the sight.", "Good for automatic weapons at medium and long range. No use if you fire from the hip." } },
            { "mod.close_quarters", new[] { "More damage inside the close range. It is the same range the Assault core uses, and the two multiply.", "For shotguns, SMGs and anyone who fights inside rooms." } },
            { "mod.adrenaline", new[] { "Every kill gives back a share of your maximum health, a few times per second at most.", "It keeps you alive in the middle of a crowd. Against one tough enemy it does nothing until that enemy dies." } },
            { "mod.choke", new[] { "Adds pellets to every shell of a pellet shotgun without using more ammunition, so each shot simply hits harder.", "It does nothing for other guns or for the Slug Gun. The shop only offers it while you carry a pellet shotgun." } },
            { "mod.rapid_fire", new[] { "Your guns fire faster: more damage per second, and more ammunition used.", "Handguns gain nothing. They already fire as fast as you can pull the trigger." } },
            { "mod.heavy_swing", new[] { "Melee hits and thrown melee weapons deal more damage. Take it if you finish enemies with melee.", "Needs a melee weapon from the armory in your loadout. The shop does not offer it otherwise." } },
            { "mod.quick_swing", new[] { "The wind-up before a melee hit and the pause after it both get shorter. Heavy weapons such as the Sledgehammer gain the most.", "Needs a melee weapon from the armory in your loadout. The shop does not offer it otherwise." } },
            { "mod.extended_mag", new[] { "A bigger magazine: longer bursts and fewer reloads. It multiplies with the Magazine stat.", "Careful with the Reload Burst core: a bigger magazine takes longer to fire off." } },
            { "mod.heavy_rounds", new[] { "Every bullet hits harder, and you pay with a little movement speed.", "Fine if you hold a position. Think twice if you depend on running." } },
            { "mod.sustained_fire", new[] { "Raises how far the Suppression bonus can grow.", "Needs the Suppression core. Without that core it does nothing, so the shop only offers it once you have the core." } },
            { "mod.fast_hands", new[] { "Shorter reloads for every gun.", "Good with any slow-reloading weapon, and with the Reload Burst core, where every reload starts a damage window." } },
            { "mod.tactical_reload", new[] { "Each reload puts a few rounds back into your reserve, so you run out of ammunition later.", "Best with small magazines that reload often." } },
            { "mod.burst_extender", new[] { "The damage window of Reload Burst lasts longer.", "Needs the Reload Burst core. Without that core it does nothing, so the shop only offers it once you have the core." } },
            { "mod.rubber_rounds", new[] { "A bounced bullet normally loses part of its damage. With this it keeps all of it.", "Needs bullets that bounce: the Ricochet core or Double Bounce." } },
            { "mod.double_bounce", new[] { "One more wall bounce for every bullet. On its own it gives your bullets one bounce, even without the Ricochet core.", "There is a limit to how many times a bullet can bounce. The shop stops offering this once you are at the limit." } },
            { "mod.angle_finder", new[] { "An enemy hit by a bounced bullet is marked for a few seconds, and a marked enemy takes more damage from everyone.", "Needs bullets that bounce: the Ricochet core or Double Bounce.", "If you already have the Marker core, your hits mark anyway and this adds nothing." } },
            { "mod.bigger_boom", new[] { "Every explosion you cause reaches further: hand grenades, grenade-launcher rounds and the kill explosions of the Demolition core.", "It works without any core, as long as you use grenades." } },
            { "mod.frag_grenades", new[] { "Every explosion you cause hurts more: hand grenades, grenade-launcher rounds and the kill explosions of the Demolition core.", "Take it together with Bigger Boom for large, strong blasts." } },
            { "mod.shockwave", new[] { "The kill explosions of the Demolition core also slow the enemies they hit. Grenades do not slow.", "Needs the Demolition core. Without that core it does nothing, so the shop only offers it once you have the core." } },
            { "mod.spotter", new[] { "Your marks last longer, so teammates have more time to use the bonus.", "Needs a way to mark enemies: the Marker core, Angle Finder, the Enemy Sight ultimate or Static Mark." } },
            { "mod.bounty_hunter", new[] { "Killing a marked enemy pays extra coins to the whole squad, up to a limit in each stage.", "Needs a way to mark enemies: the Marker core, Angle Finder, the Enemy Sight ultimate or Static Mark.", "A money mod: it does not make you stronger by itself." } },
            { "mod.team_radio", new[] { "It is not offered in runs at the moment, so you cannot find it in a new run.", "A run saved before that change may still hold it." } },
            { "mod.double_dash", new[] { "The Dash tactical holds more charges, so you can dash several times in a row. Charges come back one at a time.", "Needs the Dash tactical. Without it this does nothing, and the shop does not offer it." } },
            { "mod.triple_jump", new[] { "One more jump in the air, on top of the one Double Jump gives.", "Needs the Double Jump tactical. Without it this does nothing, and the shop does not offer it." } },
            { "mod.shield_boost", new[] { "The Shield tactical absorbs more damage and stays up longer.", "Needs the Shield tactical. Without it this does nothing, and the shop does not offer it." } },
            { "mod.ult_cooldown", new[] { "Your ultimate fills faster.", "Needs an ultimate. All charge-speed bonuses together stop at a fixed limit, which the effect line shows." } },
            { "mod.superconductor", new[] { "While Chain Bullets is active, each hit arcs to more enemies and the chain jumps further.", "Needs the Chain Bullets ultimate. With any other ultimate it does nothing, and the shop does not offer it." } },
            { "mod.static_mark", new[] { "While Chain Bullets is active, every enemy a chain touches is marked and takes more damage from the whole squad.", "Needs the Chain Bullets ultimate. With any other ultimate it does nothing, and the shop does not offer it." } },
            { "mod.spring_legs", new[] { "Higher jumps: reach ledges and rooftops, and jump over melee enemies.", "Jump height has a fixed limit, and the top tier already reaches it." } },
            { "mod.quick_revive", new[] { "You get a downed teammate back up faster.", "Only useful in co-op. In a solo run there is nobody to revive." } },
            { "mod.ammo_belt", new[] { "A larger ammunition reserve for both weapons.", "Take it when you keep running dry before the stage ends." } },
            { "mod.thick_skin", new[] { "Everything that hurts you hurts a little less.", "Simple and always useful. It multiplies with other damage reduction." } },
            // ---- tacticals
            { "tactical.doublejump", new[] { "Always on: press Jump again in the air to change direction, reach high ground or get over a melee swing.", "Triple Jump adds one more jump in the air.", "You carry one tactical. Taking another one replaces it." } },
            { "tactical.dash", new[] { "Press the tactical button to dash forward: leave a bad spot, close in with a shotgun or reach a teammate.", "It stops at walls and ledges. Charges refill one at a time.", "Dash Charges gives you more charges, and the Mobility core refills them sooner.", "You carry one tactical. Taking another one replaces it." } },
            { "tactical.shield", new[] { "Press the tactical button to raise a shield that soaks up damage for a short time. Then it has a cooldown.", "Use it before you cross open ground or revive someone. Pressing again replaces the shield; shields never add up.", "Shield Boost makes it bigger and longer.", "You carry one tactical. Taking another one replaces it." } },
            // ---- ultimates
            { "ult.infinite_fire", new[] { "For a short time you never run dry and never reload. Hold the trigger and keep it on target.", "Best with fast-firing weapons and the Suppression core, whose bonus needs unbroken fire. It does not make the gun fire faster.", "Kills by the squad charge the ultimate. You carry one; taking another replaces it." } },
            { "ult.lethal_shot", new[] { "For a few seconds anything you hit dies, elites included. Finale targets do not die at once, but take far more damage.", "Save it for a wave of elites, or for the moment a finale target is exposed. A fast-firing gun kills the most in that time.", "Kills by the squad charge the ultimate. You carry one; taking another replaces it." } },
            { "ult.invincible", new[] { "For a few seconds bullets, melee hits and gas cannot hurt you. It covers only you, not your teammates.", "Use it to revive someone under fire, to carry an objective through an ambush or to survive a bad position.", "Kills by the squad charge the ultimate. You carry one; taking another replaces it." } },
            { "ult.emergency_revive", new[] { "In co-op: with a full charge, use it to bring downed or dead teammates back at once, with their whole build.", "Solo: you do not press anything. With a full charge, a hit that would kill you leaves you alive instead, and the charge is spent.", "It recharges and works again. Kills by the squad charge the ultimate. You carry one; taking another replaces it." } },
            { "ult.enemy_sight", new[] { "Every enemy on the map is marked and shown through walls for a while, including the ones that arrive during it.", "Marked enemies take more damage from the whole squad, and your mark mods (Spotter, Bounty Hunter) work on them.", "Use it at the start of a big wave, or to find the last hidden enemies. Kills by the squad charge the ultimate. You carry one; taking another replaces it." } },
            { "ult.chain_bullets", new[] { "While it lasts, each hit jumps as lightning to nearby enemies, and from them to more enemies.", "Strongest against a tight crowd; against a single enemy it adds nothing. Chain damage never counts as a headshot.", "Superconductor and Static Mark build on it. Kills by the squad charge the ultimate. You carry one; taking another replaces it." } },
            { "ult.homing_bullets", new[] { "While it lasts, your bullets bend toward the enemy closest to where you aim, if you can see it.", "Good with fast or inaccurate weapons and against fast enemies. You still have to aim roughly at the target.", "Kills by the squad charge the ultimate. You carry one; taking another replaces it." } },
        };

        /// <summary>The numbers of an item that change with its tier, in the order of the tier table's columns.</summary>
        static readonly Dictionary<string, CodexTierParam[]> tierParams = new Dictionary<string, CodexTierParam[]>(StringComparer.Ordinal)
        {
            { "core.precision", new[] { Tp(0, "Headshot damage", "+{0}%"), Tp(1, "Enemies pierced", "{0}"), Tp(3, "Body shots", "x{0}") } },
            { "core.assault", new[] { Tp(0, "Close-range damage", "+{0}%"), Tp(5, "Damage reduction after a kill", "{0}%"), Tp(4, "Lasts", "{0} s") } },
            { "core.suppression", new[] { Tp(1, "Damage cap", "+{0}%"), Tp(4, "Magazine", "+{0}%"), Tp(5, "Reload time", "+{0}%") } },
            { "core.reloadburst", new[] { Tp(0, "Magazine to fire first", "{0}%"), Tp(1, "Bonus damage", "+{0}%"), Tp(2, "Lasts", "{0} s") } },
            { "core.ricochet", new[] { Tp(0, "Bounces", "{0}"), Tp(2, "Bounced hits", "+{0}%") } },
            { "core.demolition", new[] { Tp(0, "Blast damage", "{0}%"), Tp(1, "Radius", "{0} m") } },
            { "core.marker", new[] { Tp(0, "Mark lasts", "{0} s"), Tp(1, "Marked enemies take", "+{0}%") } },
            { "core.mobility", new[] { Tp(0, "Movement speed", "+{0}%"), Tp(1, "Revive speed", "+{0}%"), Tp(2, "Dash cooldown", "x{0}"), Tp(4, "First shot", "+{0}%") } },
            { "mod.long_barrel", new[] { Tp(0, "Headshot damage", "+{0}%") } },
            { "mod.calm_hands", new[] { Tp(0, "Spread while aiming", "-{0}%") } },
            { "mod.close_quarters", new[] { Tp(0, "Close-range damage", "+{0}%") } },
            { "mod.adrenaline", new[] { Tp(0, "Health per kill", "{0}%") } },
            { "mod.choke", new[] { Tp(0, "Extra pellets", "+{0}") } },
            { "mod.rapid_fire", new[] { Tp(0, "Fire rate", "+{0}%") } },
            { "mod.heavy_swing", new[] { Tp(0, "Melee damage", "+{0}%") } },
            { "mod.quick_swing", new[] { Tp(0, "Swing time", "-{0}%") } },
            { "mod.extended_mag", new[] { Tp(0, "Magazine", "+{0}%") } },
            { "mod.heavy_rounds", new[] { Tp(0, "Damage", "+{0}%"), Tp(1, "Movement speed", "-{0}%") } },
            { "mod.sustained_fire", new[] { Tp(0, "Damage cap", "+{0}%") } },
            { "mod.fast_hands", new[] { Tp(0, "Reload time", "-{0}%") } },
            { "mod.tactical_reload", new[] { Tp(0, "Rounds returned", "{0}") } },
            { "mod.burst_extender", new[] { Tp(0, "Lasts", "+{0} s") } },
            { "mod.bigger_boom", new[] { Tp(0, "Radius", "+{0}%") } },
            { "mod.frag_grenades", new[] { Tp(0, "Blast damage", "+{0}%") } },
            { "mod.shockwave", new[] { Tp(1, "Slow lasts", "{0} s") } },
            { "mod.spotter", new[] { Tp(0, "Mark lasts", "+{0} s") } },
            { "mod.bounty_hunter", new[] { Tp(0, "Bounty", "+{0}%") } },
            { "mod.double_dash", new[] { Tp(0, "Dash charges", "{0}") } },
            { "mod.shield_boost", new[] { Tp(0, "Shield absorbs", "+{0}%"), Tp(1, "Lasts", "+{0} s") } },
            { "mod.ult_cooldown", new[] { Tp(0, "Charge speed", "+{0}%") } },
            { "mod.superconductor", new[] { Tp(0, "More enemies per hit", "+{0}"), Tp(1, "More jumps", "+{0}") } },
            { "mod.static_mark", new[] { Tp(0, "Mark lasts", "{0} s") } },
            { "mod.spring_legs", new[] { Tp(0, "Jump height", "+{0}%") } },
            { "mod.quick_revive", new[] { Tp(0, "Revive speed", "+{0}%") } },
            { "mod.ammo_belt", new[] { Tp(0, "Reserve ammunition", "+{0}%") } },
            { "mod.thick_skin", new[] { Tp(0, "Damage taken", "-{0}%") } },
        };

        /// <summary>What the stat adds with every tier bought, as one sentence (template: total, tiers).</summary>
        static readonly Dictionary<string, string> statTotals = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "stat.health", "With all {1} tiers: +{0}% maximum health." },
            { "stat.damage", "With all {1} tiers: +{0}% weapon damage." },
            { "stat.magazine", "With all {1} tiers: +{0}% magazine capacity." },
            { "stat.speed", "With all {1} tiers: +{0}% movement speed." },
            { "stat.regen", "With all {1} tiers: {0}% of maximum health every second." },
        };

        public static string[] ItemGuide(string id) { string[] g; return id != null && guides.TryGetValue(id, out g) ? g : new string[0]; }

        /// <summary>The top tier of the stat and what every tier together gives; an empty line for anything else.</summary>
        public static TextLine StatTotal(ItemDef def)
        {
            string template;
            if (def == null || def.Kind != ItemKind.Stat || !statTotals.TryGetValue(def.Id, out template)) return new TextLine("");
            double step = def.Id == "stat.health" ? BuildStats.HealthPerTier : def.Id == "stat.damage" ? BuildStats.DamagePerTier : def.Id == "stat.magazine" ? BuildStats.MagazinePerTier
                : def.Id == "stat.speed" ? BuildStats.SpeedPerTier : BuildStats.RegenPerTier;
            int tiers = RogueCatalog.MaxTier(def.Id);
            return new TextLine(template, P(step * tiers), tiers.ToString(Inv));
        }

        /// <summary>True when the item has more than one tier and at least one number that changes between them.</summary>
        public static bool HasTierTable(ItemDef def)
        {
            return def != null && (def.Kind == ItemKind.Core || def.Kind == ItemKind.Mod) && RogueCatalog.MaxTier(def.Id) > 1 && VaryingArgs(def).Count > 0;
        }

        /// <summary>Indices of the effect arguments whose value differs between two tiers of the item.</summary>
        public static List<int> VaryingArgs(ItemDef def)
        {
            var result = new List<int>();
            if (def == null || def.Values == null) return result;
            int max = RogueCatalog.MaxTier(def.Id);
            var first = def.EffectArgs(1);
            for (int a = 0; a < first.Length; a++)
                for (int t = 2; t <= max; t++) { var args = def.EffectArgs(t); if (a < args.Length && args[a] != first[a]) { result.Add(a); break; } }
            return result;
        }

        /// <summary>The cells of one tier's row. Empty when the item has no tier table.</summary>
        public static CodexCell[] TierCells(ItemDef def, int tier)
        {
            CodexTierParam[] list;
            if (def == null || !tierParams.TryGetValue(def.Id, out list)) return new CodexCell[0];
            var args = def.EffectArgs(tier);
            var cells = new CodexCell[list.Length];
            for (int i = 0; i < list.Length; i++) cells[i] = new CodexCell { Label = list[i].Label, Format = list[i].Format, Value = list[i].Arg < args.Length ? args[list[i].Arg] : "" };
            return cells;
        }

        /// <summary>Slot rule of the item's kind: how many you hold and what happens when you take another.</summary>
        public static TextLine ItemSlots(ItemDef def)
        {
            if (def == null) return new TextLine("");
            switch (def.Kind)
            {
                case ItemKind.Core: return new TextLine("You can hold {0} cores at once. Taking a core you already own raises its tier.", RogueCatalog.MaxCores.ToString(Inv));
                case ItemKind.Mod: return new TextLine("You can hold {0} mods at once. Taking a mod you already own raises its tier.", RogueCatalog.MaxMods.ToString(Inv));
                case ItemKind.Stat: return new TextLine("Stats take no slot. Each tier costs a little more than the one before.");
                default: return new TextLine("");
            }
        }

        /// <summary>Where the item comes from in a run (RogueShop.Sample and SampleReward).</summary>
        public static string ItemSource(ItemDef def)
        {
            if (def == null) return "";
            if (!Obtainable(def)) return "Not offered in runs at the moment.";
            switch (def.Kind)
            {
                case ItemKind.Stat: return "The shop sells one stat upgrade on every visit, and one of the three free picks after each stage is a stat.";
                case ItemKind.Core: return "The shop offers a core on most visits, and always while you have none. It can also be one of the three free picks after a stage.";
                case ItemKind.Mod: return "The shop sells two mods on every visit, and at least one of the three free picks after each stage is a mod.";
                case ItemKind.Tactical: return "The shop sells it while you have no tactical, and now and then after that. It can also be one of the three free picks after a stage.";
                case ItemKind.Ultimate: return "The shop sells an ultimate from the second stage on while you have none, and now and then after that.";
                default: return "";
            }
        }

        /// <summary>What to do to open an entry that was never seen.</summary>
        /// <summary>An operative's melee weapon is one of the Bruiser's: its entry points there instead of repeating the table.</summary>
        public static string OperativeMeleeNote { get { return "Its melee weapon works as described in the Bruiser entry."; } }
        /// <summary>"{0} entries": the count line of a section without discovery tracking.</summary>
        public static TextLine EntryCount(int count) { return new TextLine("{0} entries", count.ToString(Inv)); }

        public static string UnseenItemHint { get { return "Own it once in a run and its full entry opens here."; } }
        public static string UnseenEnemyHint { get { return "Meet it once in a run and its full entry opens here."; } }

        // ------------------------------------------------------------------ enemies
        static readonly Dictionary<string, string[]> roles = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "role.rifleman", new[] { "The standard soldier, and the measure for every other enemy.", "Keeps its distance and shoots steadily." } },
            { "role.rusher", new[] { "Little health, but very fast.", "Comes in fast along a side lane and shoots from right in front of you." } },
            { "role.marksman", new[] { "Stays far away and hits very hard with a long-range rifle.", "Fragile once you get close to it." } },
            { "role.shieldbearer", new[] { "Slow and tough, behind a riot shield.", "The shield stops most of what hits it from the front." } },
            { "role.flanker", new[] { "Quicker than a rifleman.", "It does not come straight at you: it circles round to hit you from the side or from behind." } },
            { "role.jammer", new[] { "Carries a jamming device. While you stand inside its ring, your ultimate does not charge.", "Weak in a firefight." } },
            { "role.brawler", new[] { "Never shoots.", "Runs at you with one of the armory's melee weapons, picked at random, and uses that weapon's own move." } },
            { "role.operative", new[] { "An enemy built like a player: it has cores, a gun from the armory, a melee weapon, stat upgrades and a tactical.", "It is the most dangerous regular enemy, and pays the most." } },
        };

        public static string[] RoleAbout(string roleId) { string[] g; return roleId != null && roles.TryGetValue(roleId, out g) ? g : new string[0]; }

        static EnemyRoleDef Baseline { get { return RogueCatalog.Role("role.rifleman") ?? RogueCatalog.EnemyRoles[0]; } }

        /// <summary>The role's numbers, relative to the rifleman, as translatable lines (from EnemyRoleDef only).</summary>
        public static List<TextLine> RoleFacts(EnemyRoleDef r)
        {
            var list = new List<TextLine>();
            if (r == null) return list;
            var b = Baseline;
            bool baseline = r == b;
            if (baseline) list.Add(new TextLine("The measure for the other enemies: their numbers are compared with this one."));
            else list.Add(new TextLine("Health x{0}, speed x{1}, damage x{2} of a Rifleman.", N(r.HealthMul / b.HealthMul), N(r.SpeedMul / b.SpeedMul), N(r.DamageMul / b.DamageMul)));
            list.Add(new TextLine("Likes to fight from about {0} m.", N(r.PreferredRange)));
            if (r.FrontReduction > 0) list.Add(new TextLine("Takes {0}% less damage from the front.", P(r.FrontReduction)));
            list.Add(new TextLine("First appears at stage {0}-{1}.", RogueDepth.ChapterOf(r.MinDepth).ToString(Inv), RogueDepth.StageInChapter(r.MinDepth).ToString(Inv)));
            if (!baseline) list.Add(new TextLine("Bounty x{0} of a Rifleman.", N(r.Weight / (double)b.Weight)));
            return list;
        }

        /// <summary>English names of the guns the role fires (WeaponCatalog); empty for a role that never fires or carries an armory gun.</summary>
        public static List<string> RoleGuns(EnemyRoleDef r)
        {
            var list = new List<string>();
            if (r == null || r.Melee || r.Operative) return list;
            foreach (int w in r.Weapons) { string name = WeaponCatalog.GetDefault(w).gunName; if (!list.Contains(name)) list.Add(name); }
            return list;
        }

        /// <summary>The eight armory melee weapons as an enemy uses them (EnemyMeleeRules).</summary>
        public static List<CodexEnemyMelee> EnemyMelee()
        {
            var list = new List<CodexEnemyMelee>();
            foreach (var m in RogueArmory.Melee)
            {
                var row = new CodexEnemyMelee { Id = m.Id, Name = m.Name };
                row.Hit = new TextLine("{0}% of a player's base health per hit, {1} s wind-up.", P(EnemyMeleeRules.HitShare(m, false, false, false)), N(EnemyMeleeRules.Telegraph(m, 0, 0)));
                switch (m.Special)
                {
                    case MeleeSpecial.Backstab: row.Special = new TextLine("From behind it hits x{0} as hard.", N(Math.Max(1, Math.Min(m.S1, EnemyMeleeRules.BackstabCap)))); break;
                    case MeleeSpecial.Knockback: row.Special = new TextLine("Knocks you back {0} m.", N(EnemyMeleeRules.KnockbackMeters(m))); break;
                    case MeleeSpecial.GroundSlam: row.Special = new TextLine("Slams the ground: hits everyone within {0} m and slows them by {1}% for {2} s.", N(EnemyMeleeRules.SlamRadius(m)), P(EnemyMeleeRules.SlowFraction(m)), N(EnemyMeleeRules.SlowSeconds(m))); break;
                    case MeleeSpecial.Combo: row.Special = new TextLine("{0} hits in a row. During each wind-up it takes {1}% less damage from the front.", EnemyMeleeRules.ComboHits(m).ToString(Inv), P(EnemyMeleeRules.DeflectReduction(m))); break;
                    case MeleeSpecial.Throw: row.Special = new TextLine("Throws it at you from up to {0} m away.", N(EnemyMeleeRules.ThrowRange(m))); break;
                    case MeleeSpecial.Guard: row.Special = new TextLine("Its shield takes {0}% off damage from the front.", P(EnemyMeleeRules.GuardReduction(m))); break;
                    case MeleeSpecial.Shock: row.Special = new TextLine("A hit slows you by {0}% for {1} s.", P(EnemyMeleeRules.SlowFraction(m)), N(EnemyMeleeRules.SlowSeconds(m))); break;
                    case MeleeSpecial.Flurry: row.Special = new TextLine("Each hit in a row comes faster, up to {0} times.", EnemyMeleeRules.FlurryStacks(m).ToString(Inv)); break;
                    default: row.Special = new TextLine(""); break;
                }
                list.Add(row);
            }
            return list;
        }

        static int FirstChapter(Func<int, bool> test)
        {
            for (int chapter = 1; chapter <= 40; chapter++) if (test((chapter - 1) * RogueDepth.StagesPerChapter + 1)) return chapter;
            return 0;
        }

        /// <summary>What a Rogue Operative carries (EnemyKits, EnemyOperativeRules).</summary>
        public static List<TextLine> OperativeKit()
        {
            var list = new List<TextLine>();
            var def = RogueCatalog.Role(EnemyKits.OperativeRole);
            int first = def != null ? def.MinDepth : 1;
            int twoCores = FirstChapter(d => EnemyKits.CoreCount(d) >= 2);
            list.Add(twoCores > 0
                ? new TextLine("Cores: {0}, and {1} from chapter {2}. The icons over its head show which.", EnemyKits.CoreCount(first).ToString(Inv), "2", twoCores.ToString(Inv))
                : new TextLine("Cores: {0}. The icons over its head show which.", EnemyKits.CoreCount(first).ToString(Inv)));
            list.Add(new TextLine("Gun: one of {0} armory guns, picked at random, with that gun's trait.", EnemyKits.OperativeGuns().Count.ToString(Inv)));
            list.Add(new TextLine("Melee: one of the {0} armory melee weapons. It draws it when you get close.", RogueArmory.Melee.Length.ToString(Inv)));
            list.Add(new TextLine("Stat upgrades: {0} when it first appears, one more each chapter, up to {1}. Each one adds health, damage or speed.", EnemyKits.StatPoints(first).ToString(Inv), EnemyKits.MaxStatPoints.ToString(Inv)));
            list.Add(new TextLine("Tactical: a dash to get out of your aim, or a shield that absorbs {0} damage for {1} s.", N(EnemyOperativeRules.ShieldCapacity), N(EnemyOperativeRules.ShieldSeconds)));
            list.Add(new TextLine("One round of its gun never takes more than {0}% of a player's base health.", P(EnemyOperativeRules.ShotCapShare)));
            return list;
        }

        // ------------------------------------------------------------------ elites and affixes
        static readonly Dictionary<string, string[]> affixes = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "af.berserker", new[] { "When an enemy near it dies, it gets angry: faster and stronger for a few seconds.", "Kill it first, or leave it for last and finish it from a distance." } },
            { "af.suppressor", new[] { "Every hit it lands slows you down for a moment.", "Fight it from cover. Do not try to run across open ground while it is shooting at you." } },
            { "af.opening", new[] { "Its hits hurt more while you are at full health.", "The first hit is the dangerous one. Do not step out carelessly just because your health is full." } },
            { "af.shield_aura", new[] { "The other enemies near it take less damage. It does not protect itself.", "Kill it first, or pull the fight away from it." } },
            { "af.marksman", new[] { "Its hits hurt more from far away.", "Close the distance, or break its line of sight." } },
            { "af.guardian", new[] { "The first hit that should kill it does not: it cannot be hurt for a moment.", "Stop shooting, wait for it to end, then finish it." } },
        };

        public static string[] AffixAbout(string id) { string[] g; return id != null && affixes.TryGetValue(id, out g) ? g : new string[0]; }

        public static TextLine AffixFrom(AffixDef a) { return new TextLine("Elites can carry it from Heat {0}.", (a != null ? a.MinHeat : 0).ToString(Inv)); }

        /// <summary>How many affixes an elite carries at which Heat (the part of EliteRules an affix entry repeats).</summary>
        public static List<TextLine> AffixCounts()
        {
            var list = new List<TextLine>();
            int one = FirstHeat(h => EliteAffixes.Count(h, true, false) >= 1), two = FirstHeat(h => EliteAffixes.Count(h, true, false) >= 2);
            if (one >= 0) list.Add(new TextLine("From Heat {0}, every elite carries one affix: a skill borrowed from the players' skill tree.", one.ToString(Inv)));
            if (two >= 0) list.Add(new TextLine("From Heat {0}, every elite carries two affixes.", two.ToString(Inv)));
            return list;
        }

        /// <summary>The card's status line: the Heat from which elites can carry the affix.</summary>
        public static TextLine AffixHeat(AffixDef a) { return new TextLine("From Heat {0}", (a != null ? a.MinHeat : 0).ToString(Inv)); }

        static int FirstHeat(Func<int, bool> test) { for (int h = 0; h <= RogueHeat.MaxHeat; h++) if (test(h)) return h; return -1; }

        /// <summary>What an elite is and when elites and finale targets carry affixes (EliteAffixes.Count).</summary>
        public static List<TextLine> EliteRules()
        {
            var list = new List<TextLine>();
            list.Add(new TextLine("An elite is a stronger copy of a normal enemy with much more health. Killing one pays x{0} bounty.", RogueCatalog.EliteWeightMultiplier.ToString(Inv)));
            list.AddRange(AffixCounts());
            int boss = FirstHeat(h => EliteAffixes.Count(h, false, true) >= 1);
            if (boss >= 0) list.Add(new TextLine("From Heat {0}, finale targets carry affixes too.", boss.ToString(Inv)));
            list.Add(new TextLine("The icons over an elite's head show its affixes."));
            return list;
        }

        // ------------------------------------------------------------------ missions and events
        public static readonly GuideKind[] EncounterKinds = { GuideKind.Objective, GuideKind.Event, GuideKind.Emergency, GuideKind.Finale };

        public static EncounterDef[] Encounters(GuideKind kind)
        {
            switch (kind) { case GuideKind.Event: return RogueCatalog.Events; case GuideKind.Emergency: return RogueCatalog.Emergencies; case GuideKind.Finale: return RogueCatalog.Finales; default: return RogueCatalog.Objectives; }
        }

        public static string EncounterSection(GuideKind kind)
        {
            switch (kind) { case GuideKind.Event: return "Events"; case GuideKind.Emergency: return "Emergencies"; case GuideKind.Finale: return "Finales"; default: return "Stage objectives"; }
        }

        /// <summary>Id of the kind's section header card ("missions" is the first one, where the jump bar lands).</summary>
        public static string EncounterSectionId(GuideKind kind)
        {
            switch (kind) { case GuideKind.Event: return "events"; case GuideKind.Emergency: return "emergencies"; case GuideKind.Finale: return "finales"; default: return "missions"; }
        }

        /// <summary>What this kind of encounter is, for the section header.</summary>
        public static string[] EncounterKindAbout(GuideKind kind)
        {
            switch (kind)
            {
                case GuideKind.Event: return new[] { "Side tasks that show up in some stages. They pay extra bounty.", "You may ignore an event. The stage is still cleared by its objective." };
                case GuideKind.Emergency: return new[] { "Problems on a timer that show up in some stages.", "Deal with one in time for a bounty. Ignore it and something bad happens to the squad." };
                case GuideKind.Finale: return new[] { "The last stage of every chapter is a finale, with a target that has far more health than any enemy.", "After a finale you choose: evacuate with what you earned, or go on to the next chapter." };
                default: return new[] { "Every stage has one objective. Finish it to clear the stage and take the reward.", "The first four stages of a chapter each draw one of these." };
            }
        }

        public static TextLine EncounterReward(EncounterDef e)
        {
            if (e == null) return new TextLine("");
            // an event or an emergency is paid from the stage's event budget, which may already be partly spent (RogueEncounterGuide.RewardMinorEach)
            var kind = RogueEncounterGuide.KindOf(e.Id);
            return new TextLine(kind == GuideKind.Event || kind == GuideKind.Emergency ? "Reward: up to {0}% of the stage bounty for each player." : "Reward: {0}% of the stage bounty for each player.", P(e.RewardFraction));
        }

        public static TextLine EncounterFrom(EncounterDef e)
        {
            // a finale is always the last stage of a chapter: "from stage" would say nothing
            return e == null || RogueEncounterGuide.KindOf(e.Id) == GuideKind.Finale ? new TextLine("") : new TextLine("Can appear from stage {0}-{1}.", RogueDepth.ChapterOf(e.MinDepth).ToString(Inv), RogueDepth.StageInChapter(e.MinDepth).ToString(Inv));
        }

        // ------------------------------------------------------------------ lifetime statistics
        /// <summary>Statistic cards of the records page, in display order (the ids are the cards' English titles).</summary>
        public static readonly string[] RecordIds = { "Total runs", "Evacuations", "Squad wipes", "Deepest stage", "Total kills", "Headshots", "Melee kills", "Rescues", "Play time" };

        /// <summary>How each lifetime statistic is counted, written from MetaProfiles.NoteRunStarted, RecordRun and RecordTotals.</summary>
        public static List<TextLine> RecordAbout(string id)
        {
            var list = new List<TextLine>();
            switch (id)
            {
                case "Total runs":
                    list.Add(new TextLine("Every run you started. It counts the moment the run begins, whether or not you finish it."));
                    list.Add(new TextLine("Continuing a saved run does not count again."));
                    break;
                case "Evacuations":
                    list.Add(new TextLine("Runs you ended by your own choice: after clearing a chapter's finale you chose to evacuate and left with your rewards."));
                    break;
                case "Squad wipes":
                    list.Add(new TextLine("Runs that ended because the whole squad was down."));
                    list.Add(new TextLine("In a solo run, \"Abandon run\" in the pause menu ends the run as a defeat, so it counts here too."));
                    list.Add(new TextLine("Leaving a co-op squad early, or starting a new run over a saved one, is neither a wipe nor an evacuation."));
                    break;
                case "Deepest stage":
                    list.Add(new TextLine("The furthest stage you reached in any run, counted in stages from the start of the run."));
                    list.Add(new TextLine("A chapter has {0} stages, so stage {1} is the first stage of chapter 2.", RogueDepth.StagesPerChapter.ToString(Inv), (RogueDepth.StagesPerChapter + 1).ToString(Inv)));
                    break;
                case "Total kills":
                    list.Add(new TextLine("Enemies you killed yourself in all runs. Kills by teammates are not included."));
                    list.Add(new TextLine("Added when a run ends."));
                    break;
                case "Headshots":
                    list.Add(new TextLine("How many of your kills were made with a shot to the head."));
                    list.Add(new TextLine("Added when a run ends."));
                    break;
                case "Melee kills":
                    list.Add(new TextLine("How many of your kills were made with a melee weapon, swung or thrown."));
                    list.Add(new TextLine("Added when a run ends."));
                    break;
                case "Rescues":
                    list.Add(new TextLine("How many times you got a downed teammate back up."));
                    list.Add(new TextLine("Added when a run ends."));
                    break;
                case "Play time":
                    list.Add(new TextLine("Time spent inside runs, in hours, minutes and seconds. Headquarters and menus do not count."));
                    list.Add(new TextLine("Added when a run ends."));
                    break;
            }
            return list;
        }

        /// <summary>A stage number as "chapter-stage" arguments for the "Stage {0}-{1}" key.</summary>
        public static TextLine StageName(int depth)
        {
            return new TextLine("Stage {0}-{1}", RogueDepth.ChapterOf(depth).ToString(Inv), RogueDepth.StageInChapter(depth).ToString(Inv));
        }

        // ------------------------------------------------------------------ sections
        /// <summary>Section ids of the codex page, in display order; also the jump bar's entries.</summary>
        public static readonly string[] Sections = { "records", "stats", "cores", "mods", "tacticals", "ultimates", "enemies", "elites", "missions" };

        public static string SectionTitle(string section)
        {
            switch (section)
            {
                case "records": return "Lifetime records";
                case "stats": return "Stat upgrades";
                case "cores": return "Cores";
                case "mods": return "Mods";
                case "tacticals": return "Tacticals";
                case "ultimates": return "Ultimates";
                case "enemies": return "Enemies";
                case "elites": return "Elites and affixes";
                case "missions": return "Missions and events";
                default: return "";
            }
        }

        /// <summary>The section's short name on the jump bar.</summary>
        public static string SectionShort(string section)
        {
            switch (section) { case "records": return "Records"; case "stats": return "Stat upgrades"; case "elites": return "Elites"; case "missions": return "Missions"; default: return SectionTitle(section); }
        }

        /// <summary>What the section holds, shown when its header is selected.</summary>
        public static List<TextLine> SectionAbout(string section)
        {
            var list = new List<TextLine>();
            switch (section)
            {
                case "records":
                    list.Add(new TextLine("Your totals over every run on this profile. Select a number to see exactly how it is counted."));
                    break;
                case "stats":
                    list.Add(new TextLine("Simple upgrades with many tiers. Each tier adds the same amount. Each stat has its own top tier."));
                    list.Add(ItemSlots(RogueCatalog.Stats[0]));
                    break;
                case "cores":
                    list.Add(new TextLine("A core decides how your build fights. It is the biggest choice of a run."));
                    list.Add(ItemSlots(RogueCatalog.Cores[0]));
                    list.Add(new TextLine("A core has {0} tiers.", RogueCatalog.CoreTiers.ToString(Inv)));
                    break;
                case "mods":
                    list.Add(new TextLine("Small upgrades that sharpen a build. Many of them need a certain core, tactical or ultimate to do anything."));
                    list.Add(ItemSlots(RogueCatalog.Mods[0]));
                    break;
                case "tacticals":
                    list.Add(new TextLine("A movement or defence skill you can use again and again during a fight."));
                    list.Add(new TextLine("You carry one tactical. Taking another one replaces it."));
                    break;
                case "ultimates":
                    list.Add(new TextLine("A strong skill that needs a full charge. Kills by the squad charge it."));
                    list.Add(new TextLine("You carry one ultimate. Taking another one replaces it."));
                    break;
                case "enemies":
                    list.Add(new TextLine("Every kind of enemy, with its numbers compared to a Rifleman and how to deal with it."));
                    list.Add(new TextLine("The icon on each card is the marker the enemy wears over its head in a run."));
                    break;
                case "elites":
                    list.AddRange(EliteRules());
                    break;
                case "missions":
                    list.Add(new TextLine("What each stage can ask of you: how to finish it, and what goes wrong if you do not."));
                    break;
            }
            return list;
        }

        // ------------------------------------------------------------------ authoring guard
        static void CheckLine(List<string> errors, string where, TextLine line)
        {
            string template = line.Template ?? "";
            if (template.Length == 0) { errors.Add("empty codex line: " + where); return; }
            int highest = -1;
            for (int i = 0; i < template.Length - 2; i++)
                if (template[i] == '{' && char.IsDigit(template[i + 1]) && template[i + 2] == '}') highest = Math.Max(highest, template[i + 1] - '0');
            if (highest + 1 != line.Args.Length) errors.Add("codex template arguments do not match (" + line.Args.Length + " given): " + where + ": " + template);
            for (int a = 0; a < line.Args.Length; a++) if (template.IndexOf("{" + a + "}", StringComparison.Ordinal) < 0) errors.Add("codex template argument unused {" + a + "}: " + where);
            string text;
            try { text = line.ToString(); } catch (FormatException) { text = null; }
            if (text == null || text.IndexOf('{') >= 0 || text.IndexOf('}') >= 0) errors.Add("codex line does not render: " + where + ": " + template);
            foreach (var arg in line.Args) if (string.IsNullOrEmpty(arg) || arg == "NaN") errors.Add("codex line has an empty argument: " + where);
        }

        static void CheckText(List<string> errors, string where, string[] paragraphs)
        {
            if (paragraphs == null || paragraphs.Length == 0) { errors.Add("no codex text: " + where); return; }
            foreach (var p in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(p)) errors.Add("empty codex paragraph: " + where);
                else if (p.IndexOf('{') >= 0 || p.IndexOf('\t') >= 0 || p.IndexOf('\n') >= 0) errors.Add("codex paragraph holds a placeholder, tab or line break: " + where);
                else for (int i = 0; i < p.Length; i++) if (char.IsDigit(p[i])) { errors.Add("codex paragraph holds a number (use a template): " + where + ": " + p); break; }
            }
        }

        /// <summary>
        /// Every item, enemy role, affix, encounter, statistic and section has its explanation; every template renders with exactly
        /// its arguments; every number that changes with an item's tier has a column in its tier table. Empty when all is well.
        /// </summary>
        public static List<string> Validate()
        {
            var errors = new List<string>();
            foreach (var def in Items())
            {
                CheckText(errors, def.Id, ItemGuide(def.Id));
                if (RoguePitches.Of(def.Id).Length == 0) errors.Add("codex item without a pitch: " + def.Id);
                if (ItemSource(def).Length == 0) errors.Add("codex item without a source: " + def.Id);
                if (def.Kind == ItemKind.Stat) CheckLine(errors, def.Id + " total", StatTotal(def));
                if (def.Kind == ItemKind.Core || def.Kind == ItemKind.Mod || def.Kind == ItemKind.Stat) CheckLine(errors, def.Id + " slots", ItemSlots(def));
                var varying = VaryingArgs(def);
                CodexTierParam[] columns; tierParams.TryGetValue(def.Id, out columns);
                if (def.Kind == ItemKind.Core || def.Kind == ItemKind.Mod)
                {
                    foreach (int a in varying) if (columns == null || Array.FindIndex(columns, c => c.Arg == a) < 0) errors.Add("codex tier table misses argument {" + a + "}: " + def.Id);
                    if (columns != null)
                    {
                        foreach (var c in columns)
                        {
                            if (varying.IndexOf(c.Arg) < 0) errors.Add("codex tier table lists an argument that never changes {" + c.Arg + "}: " + def.Id);
                            if (c.Label.Length == 0 || c.Format.IndexOf("{0}", StringComparison.Ordinal) < 0) errors.Add("codex tier column without label or format: " + def.Id);
                        }
                        for (int t = 1; t <= RogueCatalog.MaxTier(def.Id); t++)
                            foreach (var cell in TierCells(def, t)) if (string.IsNullOrEmpty(cell.Value)) errors.Add("codex tier cell is empty: " + def.Id + " tier " + t);
                    }
                }
                else if (columns != null) errors.Add("codex tier table on an item without tiers: " + def.Id);
            }
            foreach (var id in guides.Keys) { var d = RogueCatalog.Item(id); if (d == null || !IsCodexKind(d.Kind)) errors.Add("codex text for an unknown item: " + id); }
            foreach (var id in tierParams.Keys) if (RogueCatalog.Item(id) == null) errors.Add("codex tier table for an unknown item: " + id);

            foreach (var r in RogueCatalog.EnemyRoles)
            {
                CheckText(errors, r.Id, RoleAbout(r.Id));
                int n = 0; foreach (var line in RoleFacts(r)) CheckLine(errors, r.Id + " fact " + n++, line);
                if (!r.Melee && !r.Operative && RoleGuns(r).Count == 0) errors.Add("codex role without a gun: " + r.Id);
            }
            foreach (var id in roles.Keys) if (RogueCatalog.Role(id) == null) errors.Add("codex text for an unknown role: " + id);
            var melee = EnemyMelee();
            if (melee.Count != RogueArmory.Melee.Length) errors.Add("codex enemy melee table is incomplete");
            foreach (var m in melee) { CheckLine(errors, m.Id + " hit", m.Hit); CheckLine(errors, m.Id + " special", m.Special); }
            { int n = 0; foreach (var line in OperativeKit()) CheckLine(errors, "operative kit " + n++, line); }

            foreach (var a in EliteAffixes.All)
            {
                CheckText(errors, a.Id, AffixAbout(a.Id));
                CheckLine(errors, a.Id + " effect", MetaText.Affix(a));
                CheckLine(errors, a.Id + " heat", AffixFrom(a)); CheckLine(errors, a.Id + " status", AffixHeat(a));
            }
            foreach (var id in affixes.Keys) if (EliteAffixes.Def(id) == null) errors.Add("codex text for an unknown affix: " + id);
            { int n = 0; foreach (var line in EliteRules()) CheckLine(errors, "elite rule " + n++, line); }

            int encounters = 0;
            foreach (var kind in EncounterKinds)
            {
                if (EncounterSection(kind).Length == 0) errors.Add("codex encounter kind without a title: " + kind);
                CheckText(errors, "kind " + kind, EncounterKindAbout(kind));
                foreach (var e in Encounters(kind))
                {
                    encounters++;
                    if (RogueEncounterGuide.KindOf(e.Id) != kind) errors.Add("codex encounter in the wrong section: " + e.Id);
                    var g = RogueEncounterGuide.For(e.Id);
                    if (g == null || g.Goal == null || g.Watch == null || g.Tip == null || g.Steps.Length == 0) errors.Add("codex encounter without a guide: " + e.Id);
                    if (string.IsNullOrEmpty(e.Name) || string.IsNullOrEmpty(e.Brief)) errors.Add("codex encounter without name or brief: " + e.Id);
                    CheckLine(errors, e.Id + " reward", EncounterReward(e));
                    if (kind != GuideKind.Finale) CheckLine(errors, e.Id + " from", EncounterFrom(e));
                }
            }
            int catalog = 0; foreach (var id in RogueEncounterGuide.CatalogIds()) catalog++;
            if (encounters != catalog) errors.Add("codex encounter sections do not cover the catalog: " + encounters + " of " + catalog);

            foreach (var id in RecordIds)
            {
                var lines = RecordAbout(id);
                if (lines.Count == 0) errors.Add("codex statistic without an explanation: " + id);
                int n = 0; foreach (var line in lines) CheckLine(errors, id + " line " + n++, line);
            }
            foreach (var s in Sections)
            {
                if (SectionTitle(s).Length == 0) errors.Add("codex section without a title: " + s);
                var lines = SectionAbout(s);
                if (lines.Count == 0) errors.Add("codex section without an explanation: " + s);
                int n = 0; foreach (var line in lines) CheckLine(errors, "section " + s + " line " + n++, line);
            }
            CheckLine(errors, "stage name", StageName(1));
            return errors;
        }

        static bool HasLetter(string text) { foreach (char c in text) if (char.IsLetter(c)) return true; return false; }

        /// <summary>Every English key the codex can show (templates unformatted), so a test can require a translation row for each.</summary>
        public static List<string> Keys()
        {
            var keys = new List<string>();
            Action<string> add = k => { if (!string.IsNullOrEmpty(k) && !keys.Contains(k)) keys.Add(k); };
            foreach (var g in guides.Values) foreach (var p in g) add(p);
            // a format of symbols only ("+{0}%", "x{0}") reads the same in every language and has no row; "{0} s" and "{0} m" do
            foreach (var list in tierParams.Values) foreach (var c in list) { add(c.Label); if (HasLetter(c.Format.Replace("{0}", "").Replace("x", ""))) add(c.Format); }
            foreach (var t in statTotals.Values) add(t);
            foreach (var def in Items()) { add(ItemSource(def)); add(ItemSlots(def).Template); }
            add(UnseenItemHint); add(UnseenEnemyHint); add(OperativeMeleeNote); add(EntryCount(1).Template);
            foreach (var g in roles.Values) foreach (var p in g) add(p);
            foreach (var r in RogueCatalog.EnemyRoles) foreach (var line in RoleFacts(r)) add(line.Template);
            foreach (var m in EnemyMelee()) { add(m.Hit.Template); add(m.Special.Template); }
            foreach (var line in OperativeKit()) add(line.Template);
            foreach (var g in affixes.Values) foreach (var p in g) add(p);
            foreach (var a in EliteAffixes.All) { add(AffixFrom(a).Template); add(AffixHeat(a).Template); }
            foreach (var line in EliteRules()) add(line.Template);
            foreach (var kind in EncounterKinds)
            {
                add(EncounterSection(kind));
                foreach (var p in EncounterKindAbout(kind)) add(p);
                foreach (var e in Encounters(kind)) { add(EncounterReward(e).Template); add(EncounterFrom(e).Template); }
            }
            foreach (var id in RecordIds) foreach (var line in RecordAbout(id)) add(line.Template);
            foreach (var s in Sections) { add(SectionTitle(s)); add(SectionShort(s)); foreach (var line in SectionAbout(s)) add(line.Template); }
            return keys;
        }
    }
}
