using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    public enum ItemKind { Supply = 0, Weapon = 1, Stat = 2, Core = 3, Mod = 4, Tactical = 5, Ultimate = 6 }

    /// <summary>One purchasable or pickable entry. `name` and `effect` are English localization keys.</summary>
    public sealed class ItemDef
    {
        public readonly string Id;
        public readonly ItemKind Kind;
        public readonly string Name;
        public readonly string Effect;      // one sentence, the real mechanic
        public readonly int BasePrice;      // coins in chapter 1
        public readonly int MaxStacks;      // 1 for singletons; stat tiers use 5
        public readonly int Rarity;         // 0 common, 1 uncommon, 2 rare (also shown as text)
        public readonly string[] Tags;      // core affinity tags for shop sampling
        public ItemDef(string id, ItemKind kind, string name, string effect, int basePrice, int maxStacks, int rarity, params string[] tags)
        { Id = id; Kind = kind; Name = name; Effect = effect; BasePrice = basePrice; MaxStacks = maxStacks; Rarity = rarity; Tags = tags ?? new string[0]; }
        public bool HasTag(string tag) { return Array.IndexOf(Tags, tag) >= 0; }
    }

    /// <summary>Enemy battlefield role. Weight drives bounty normalisation; the adapter maps the rest onto the legacy AI.</summary>
    public sealed class EnemyRoleDef
    {
        public readonly string Id, Name, Marker, Brief;
        public readonly double AimNear, AimMid, AimFar;
        public readonly int Weight;                 // bounty weight in hundredths (100 = rifleman)
        public readonly double HealthMul, SpeedMul, DamageMul, FrontReduction, PreferredRange;
        public readonly int[] Weapons;              // allowed primary weapon indices (WeaponCatalog)
        public readonly int MinDepth;
        public EnemyRoleDef(string id, string name, string marker, int weight, double healthMul, double speedMul, double damageMul, double frontReduction, double preferredRange, int minDepth, string brief, double aimNear, double aimMid, double aimFar, params int[] weapons)
        { Id = id; Name = name; Marker = marker; Weight = weight; HealthMul = healthMul; SpeedMul = speedMul; DamageMul = damageMul; FrontReduction = frontReduction; PreferredRange = preferredRange; MinDepth = minDepth; Weapons = weapons; Brief = brief; AimNear = aimNear; AimMid = aimMid; AimFar = aimFar; }
    }

    /// <summary>Objective, general event, emergency or finale. Compatibility is by map tags; cooldown is in stages.</summary>
    public sealed class EncounterDef
    {
        public readonly string Id, Name, Brief;
        public readonly int MinDepth, Cooldown, Weight;
        public readonly double RewardFraction;      // of G, paid on success
        public readonly string[] RequiresMapTags, ExcludesMapTags, Exclusive;
        public EncounterDef(string id, string name, string brief, int minDepth, int cooldown, int weight, double rewardFraction, string[] requires, string[] excludes, string[] exclusive)
        { Id = id; Name = name; Brief = brief; MinDepth = minDepth; Cooldown = cooldown; Weight = weight; RewardFraction = rewardFraction; RequiresMapTags = requires ?? new string[0]; ExcludesMapTags = excludes ?? new string[0]; Exclusive = exclusive ?? new string[0]; }
        public bool CompatibleWith(IList<string> mapTags)
        {
            foreach (var t in RequiresMapTags) if (mapTags == null || mapTags.IndexOf(t) < 0) return false;
            foreach (var t in ExcludesMapTags) if (mapTags != null && mapTags.IndexOf(t) >= 0) return false;
            return true;
        }
    }

    public sealed class MapDef
    {
        public readonly string Id, SceneName;
        public readonly int BuildIndex;
        public readonly string[] Tags;
        public MapDef(string id, string sceneName, int buildIndex, params string[] tags) { Id = id; SceneName = sceneName; BuildIndex = buildIndex; Tags = tags; }
    }

    public sealed class RouteDef
    {
        public readonly string Tag, Name, Brief;
        public readonly double BudgetMul, EliteMul, EventChanceMul, ShopRarityBonus;
        public RouteDef(string tag, string name, string brief, double budgetMul, double eliteMul, double eventChanceMul, double shopRarityBonus)
        { Tag = tag; Name = name; Brief = brief; BudgetMul = budgetMul; EliteMul = eliteMul; EventChanceMul = eventChanceMul; ShopRarityBonus = shopRarityBonus; }
    }

    /// <summary>
    /// Static content of the mode. Everything an author tunes lives here (or in the depth
    /// curves). Ids are stable save keys; never rename one without a migration.
    /// </summary>
    public static class RogueCatalog
    {
        public const string RulesVersion = "1";
        public const int MaxCores = 2, MaxMods = 6, StatTiers = 5;
        public const double HeadshotMoneyMultiplier = 1.5;
        public const double DashDistance = 20, DashSpeed = 50, DashCooldownSeconds = 6, DashMinIntervalSeconds = .3;
        public const double TierPriceStep = .5;
        public const double ClearRewardFraction = .2, ObjectiveRewardFraction = .3, BreakoutRewardFraction = .35, FinaleRewardFraction = .6;
        public const double ConvoyFailureRewardMultiplier = .5;

        public static int MaxTier(string id)
        {
            var def = Item(id);
            if (def == null) return 0;
            if (def.Kind == ItemKind.Core) return 3;
            if (def.Kind != ItemKind.Mod) return def.MaxStacks;
            switch (id)
            {
                case "mod.piercing_rounds": case "mod.double_bounce": case "mod.double_dash":
                case "mod.angle_finder": case "mod.rubber_rounds": case "mod.choke": case "mod.team_radio": return 1;
                default: return 3;
            }
        }

        // ---- tags shared by cores and mods
        public const string TagPrecision = "precision", TagAssault = "assault", TagSuppression = "suppression", TagReload = "reload",
            TagRicochet = "ricochet", TagDemolition = "demolition", TagMarker = "marker", TagMobility = "mobility", TagGeneric = "generic";

        public static readonly ItemDef[] Supplies =
        {
            new ItemDef("supply.ammo", ItemKind.Supply, "Ammo Crate", "Refill the reserve ammunition of both weapons.", 10, 99, 0, TagGeneric),
            new ItemDef("supply.medkit", ItemKind.Supply, "Overshield", "Refill a non-regenerating shield equal to maximum health. Does not stack; remaining shield persists between stages.", 12, 99, 0, TagGeneric),
            new ItemDef("supply.repair", ItemKind.Supply, "Field Repair", "Restore full health and refill ammunition.", 20, 99, 0, TagGeneric),
        };

        public static readonly ItemDef[] Stats =
        {
            new ItemDef("stat.health", ItemKind.Stat, "Vitality", "+12% maximum health per tier (max 5 tiers). Heals the added amount.", 25, StatTiers, 0, TagGeneric),
            new ItemDef("stat.damage", ItemKind.Stat, "Firepower", "+8% weapon damage per tier (max 5 tiers).", 30, StatTiers, 0, TagGeneric),
            new ItemDef("stat.magazine", ItemKind.Stat, "Magazine", "+15% magazine capacity per tier (max 5 tiers), at least +1 round.", 25, StatTiers, 0, TagGeneric),
            new ItemDef("stat.speed", ItemKind.Stat, "Agility", "+6% movement speed per tier (max 5 tiers).", 25, StatTiers, 0, TagGeneric),
        };

        public static readonly ItemDef[] Cores =
        {
            new ItemDef("core.precision", ItemKind.Core, "Precision", "Headshots deal +25% damage and bullets pierce one enemy at 60% damage. Body shots deal -10%.", 60, 3, 1, TagPrecision),
            new ItemDef("core.assault", ItemKind.Core, "Assault", "+15% damage within 12 m, -10% beyond 30 m. A kill within 12 m grants 2 s of 30% damage reduction and +15% speed.", 60, 3, 1, TagAssault),
            new ItemDef("core.suppression", ItemKind.Core, "Suppression", "Each trigger hit adds +4% damage, up to +40%. After 2.5 s, lose one stack per 0.5 s. Reload keeps half. +20% magazine, +20% reload time.", 60, 3, 1, TagSuppression),
            new ItemDef("core.reloadburst", ItemKind.Core, "Reload Burst", "Reloading after firing at least 60% of the magazine grants +35% damage for 3 s.", 60, 3, 1, TagReload),
            new ItemDef("core.ricochet", ItemKind.Core, "Ricochet", "Bullets bounce once off walls at 80% damage. Ricochet hits deal +30%.", 60, 3, 1, TagRicochet),
            new ItemDef("core.demolition", ItemKind.Core, "Demolition", "Kills explode: 40% of the killing damage in a 6 m radius and a short knockback. Explosions never chain.", 60, 3, 1, TagDemolition),
            new ItemDef("core.marker", ItemKind.Core, "Marker", "Your hits mark enemies for 4 s. Marked enemies take +12% damage from everyone. Marked kills by anyone charge your ultimate.", 60, 3, 1, TagMarker),
            new ItemDef("core.mobility", ItemKind.Core, "Mobility", "+12% speed, revive 40% faster, carry objects at full speed. The first shot after a dash or a jump landing deals +20%.", 60, 3, 1, TagMobility),
        };

        public static readonly ItemDef[] Mods =
        {
            new ItemDef("mod.long_barrel", ItemKind.Mod, "Long Barrel", "Headshots deal +10% damage.", 30, 3, 0, TagPrecision),
            new ItemDef("mod.piercing_rounds", ItemKind.Mod, "Piercing Rounds", "Bullets pierce one more enemy (maximum two).", 35, 1, 1, TagPrecision),
            new ItemDef("mod.calm_hands", ItemKind.Mod, "Calm Hands", "Weapon spread reduced by 25% while aiming.", 30, 3, 0, TagPrecision),
            new ItemDef("mod.close_quarters", ItemKind.Mod, "Close Quarters", "+10% damage within 12 m.", 30, 3, 0, TagAssault),
            new ItemDef("mod.adrenaline", ItemKind.Mod, "Adrenaline", "Kills heal 5% of maximum health (at most 3 heals per second).", 35, 3, 1, TagAssault),
            new ItemDef("mod.choke", ItemKind.Mod, "Choke", "Shotguns fire one extra pellet.", 30, 1, 0, TagAssault),
            new ItemDef("mod.extended_mag", ItemKind.Mod, "Extended Magazine", "+25% magazine capacity.", 30, 3, 0, TagSuppression),
            new ItemDef("mod.heavy_rounds", ItemKind.Mod, "Heavy Rounds", "+6% damage, -3% movement speed.", 30, 3, 0, TagSuppression),
            new ItemDef("mod.sustained_fire", ItemKind.Mod, "Sustained Fire", "Suppression stacks up to +60% instead of +40%.", 35, 3, 1, TagSuppression),
            new ItemDef("mod.fast_hands", ItemKind.Mod, "Fast Hands", "Reload time -25%.", 30, 3, 0, TagReload),
            new ItemDef("mod.tactical_reload", ItemKind.Mod, "Tactical Reload", "Every reload returns 2 rounds to the reserve.", 30, 3, 0, TagReload),
            new ItemDef("mod.burst_extender", ItemKind.Mod, "Burst Extender", "Reload Burst lasts 5 s instead of 3 s.", 35, 3, 1, TagReload),
            new ItemDef("mod.rubber_rounds", ItemKind.Mod, "Rubber Rounds", "Ricochets keep 100% damage.", 30, 1, 0, TagRicochet),
            new ItemDef("mod.double_bounce", ItemKind.Mod, "Double Bounce", "Bullets bounce one more time (maximum two).", 35, 1, 1, TagRicochet),
            new ItemDef("mod.angle_finder", ItemKind.Mod, "Angle Finder", "Ricochet hits mark the enemy for 4 s.", 30, 1, 0, TagRicochet, TagMarker),
            new ItemDef("mod.bigger_boom", ItemKind.Mod, "Bigger Boom", "Explosion radius +50%.", 35, 3, 1, TagDemolition),
            new ItemDef("mod.frag_grenades", ItemKind.Mod, "Frag Grenades", "Grenade damage +30%.", 30, 3, 0, TagDemolition),
            new ItemDef("mod.shockwave", ItemKind.Mod, "Shockwave", "Explosions slow enemies by 40% for 2 s.", 30, 3, 0, TagDemolition),
            new ItemDef("mod.spotter", ItemKind.Mod, "Spotter", "Marks last 3 s longer.", 30, 3, 0, TagMarker),
            new ItemDef("mod.bounty_hunter", ItemKind.Mod, "Bounty Hunter", "Marked kills pay +10% bounty to the whole squad (bounded per stage).", 35, 3, 1, TagMarker),
            new ItemDef("mod.team_radio", ItemKind.Mod, "Team Radio", "A teammate hitting your marked enemy charges your ultimate.", 30, 1, 0, TagMarker),
            new ItemDef("mod.double_dash", ItemKind.Mod, "Double Dash", "Dash has two charges.", 35, 1, 1, TagMobility),
            new ItemDef("mod.spring_legs", ItemKind.Mod, "Spring Legs", "Jump 30% higher.", 30, 3, 0, TagMobility),
            new ItemDef("mod.quick_revive", ItemKind.Mod, "Quick Revive", "Revive teammates 40% faster.", 30, 3, 0, TagMobility, TagGeneric),
            new ItemDef("mod.ammo_belt", ItemKind.Mod, "Ammo Belt", "+30% reserve ammunition.", 30, 3, 0, TagGeneric),
            new ItemDef("mod.thick_skin", ItemKind.Mod, "Thick Skin", "Damage taken -8%.", 35, 3, 1, TagGeneric),
        };

        public static readonly ItemDef[] Tacticals =
        {
            new ItemDef("tactical.doublejump", ItemKind.Tactical, "Double Jump", "Passive: press Jump again in the air for a second jump. Resets on landing.", 45, 1, 1, TagMobility),
            new ItemDef("tactical.dash", ItemKind.Tactical, "Dash", "Active: dash " + DashDistance + " m forward at " + DashSpeed + " m/s. Each charge recharges in " + DashCooldownSeconds + " s. Stops at walls and edges.", 45, 1, 1, TagMobility, TagAssault),
            new ItemDef("tactical.shield", ItemKind.Tactical, "Shield", "Active: absorb 400 damage for 4 s. 12 s cooldown. Re-activating replaces the shield, it does not stack.", 45, 1, 1, TagGeneric),
        };

        public static readonly ItemDef[] Ultimates =
        {
            new ItemDef("ult.infinite_fire", ItemKind.Ultimate, "Infinite Fire", "8 s of unlimited ammunition with no reloads. Fire rate unchanged.", 80, 1, 2, TagSuppression, TagReload),
            new ItemDef("ult.lethal_shot", ItemKind.Ultimate, "Lethal Shot", "5 s: direct hits kill regular enemies outright. Finale targets take +200% instead.", 80, 1, 2, TagPrecision),
            new ItemDef("ult.invincible", ItemKind.Ultimate, "Invincible", "5 s of immunity to combat and gas damage. Only you.", 80, 1, 2, TagAssault),
            new ItemDef("ult.emergency_revive", ItemKind.Ultimate, "Emergency Revive", "Once per run: instantly revive downed or dead teammates with their build. Solo: survive one lethal hit.", 80, 1, 2, TagMarker, TagMobility),
            new ItemDef("ult.enemy_sight", ItemKind.Ultimate, "Enemy Sight", "8 s: outlines of every spawned enemy within 80 m.", 80, 1, 2, TagMarker),
            new ItemDef("ult.chain_bullets", ItemKind.Ultimate, "Chain Bullets", "8 s: hits arc to up to 3 enemies within 10 m at 50% damage. Chains do not count as headshots.", 80, 1, 2, TagRicochet, TagDemolition),
            new ItemDef("ult.homing_bullets", ItemKind.Ultimate, "Homing Bullets", "8 s: bullets steer toward the nearest visible enemy within 15 degrees.", 80, 1, 2, TagPrecision, TagMobility),
        };

        public static readonly EnemyRoleDef[] EnemyRoles =
        {
            // id, name, icon, weight, hp, speed, dmg, frontReduction, range, minDepth, brief, near/mid/far spread, weapons
            new EnemyRoleDef("role.rifleman", "Rifleman", "Fire", 100, 1.0, 1.0, 1.0, 0.0, 40, 1, "Keeps up steady fire. Move between cover and do not trade shots at close range.", 0.06, 0.35, 0.8, 4, 5, 6, 7),
            new EnemyRoleDef("role.rusher", "Rusher", "Jump", 120, 0.7, 1.6, 1.0, 0.0, 8, 1, "Closes in fast with a shotgun or pistol. Keep your distance and drop it first.", 0.1, 0.6, 1.0, 8, 9, 12, 13),
            new EnemyRoleDef("role.marksman", "Marksman", "Sight", 130, 0.8, 0.9, 1.4, 0.0, 90, 2, "Hits hard and accurately from far away. Break line of sight, then flank it.", 0.03, 0.15, 0.4, 10, 11),
            new EnemyRoleDef("role.shieldbearer", "Shield Bearer", "Shield", 160, 1.6, 0.75, 0.8, 0.6, 20, 3, "Its shield blocks most damage from the front. Get to its side or back.", 0.06, 0.4, 0.85, 0, 1, 2, 3),
            new EnemyRoleDef("role.flanker", "Flanker", "Dash", 120, 0.9, 1.25, 1.0, 0.0, 25, 2, "Runs around your flank. Watch your sides and cover each other.", 0.06, 0.4, 0.85, 0, 1, 2, 3),
            new EnemyRoleDef("role.jammer", "Jammer", "Settings5", 150, 1.1, 0.9, 0.7, 0.0, 30, 4, "Stops ultimate charge within 30 m. Take it out first or leave its range.", 0.08, 0.45, 0.9, 12, 13),
        };
        public const int EliteWeightMultiplier = 2, FinaleWeightMultiplier = 6;

        public static readonly EncounterDef[] Objectives =
        {
            new EncounterDef("obj.clear", "Clear Out", "Eliminate every enemy in the area.", 1, 0, 100, ClearRewardFraction, null, null, null),
            new EncounterDef("obj.capture", "Hold the Zone", "Stand inside the marked zone until it is secured. More players secure it faster.", 1, 1, 90, ObjectiveRewardFraction, null, null, new[] { "ev.low_gravity" }),
            new EncounterDef("obj.carry", "Deliver the Crate", "Carry the supply crate to the drop point. The carrier cannot shoot.", 2, 1, 80, ObjectiveRewardFraction, null, new[] { "droplinks" }, null),
            new EncounterDef("obj.protect", "Protect the Repair", "Keep the repair device alive until it reaches 100%.", 2, 1, 80, ObjectiveRewardFraction, null, null, new[] { "ev.repair_device" }),
            new EncounterDef("obj.breakout", "Break Out", "Fight through to the extraction marker. Everyone must arrive.", 3, 2, 70, BreakoutRewardFraction, null, null, new[] { "em.gas_leak" }),
        };

        public static readonly EncounterDef[] Events =
        {
            new EncounterDef("ev.moving_supply", "Moving Supply", "A supply drone crosses the map. Shoot it down before it leaves to claim the crate.", 1, 2, 90, 0.2, null, null, null),
            new EncounterDef("ev.alarm_cache", "Alarm Cache", "Open the cache for a high bounty. Opening it calls a reinforcement wave.", 2, 2, 80, 0.4, null, null, null),
            new EncounterDef("ev.low_gravity", "Low Gravity", "Gravity is halved in the marked area for this stage.", 2, 3, 60, 0.1, null, new[] { "droplinks" }, new[] { "obj.capture" }),
            new EncounterDef("ev.power_reroute", "Power Reroute", "Flip the breaker: for 60 s enemy shields and jammers lose power and enemies see half as far.", 3, 3, 60, 0.15, new[] { "indoor" }, null, null),
            new EncounterDef("ev.repair_device", "Repair Device", "An optional device can be repaired while enemies attack it. Reward on completion.", 1, 2, 80, 0.3, null, null, new[] { "obj.protect" }),
            new EncounterDef("ev.risk_contract", "Risk Contract", "Accept: enemies deal +25% damage this stage, bounty +40%. Decline at no cost.", 2, 2, 70, 0.4, null, null, null),
            new EncounterDef("ev.elite_hunt", "Elite Hunt", "A marked elite roams the map. Killing it pays a large bounty.", 3, 2, 80, 0.5, null, null, null),
            new EncounterDef("ev.lure_crate", "Lure Crate", "A beacon crate attracts enemies while carried. Plant it to pull the wave away from an objective.", 2, 2, 60, 0.2, null, new[] { "droplinks" }, null),
        };

        public static readonly EncounterDef[] Emergencies =
        {
            new EncounterDef("em.gas_leak", "Gas Leak", "Activate 2 of 3 vent switches before the countdown ends or gas spreads zone by zone.", 2, 3, 100, 0.4, null, null, new[] { "obj.breakout" }),
            new EncounterDef("em.power_outage", "Power Outage", "Lights out. Restart the generator: hold the switch for 8 s while enemies close in.", 3, 3, 80, 0.35, new[] { "indoor" }, null, null),
            new EncounterDef("em.mobile_bomb", "Mobile Bomb", "A bomb is armed. Carry it to the disposal point before it explodes.", 3, 3, 80, 0.4, null, new[] { "droplinks" }, new[] { "obj.carry" }),
            new EncounterDef("em.reinforcement_signal", "Reinforcement Signal", "A signal device is calling waves. Destroy it to stop the reinforcements.", 2, 2, 90, 0.35, null, null, null),
        };

        public static readonly EncounterDef[] Finales =
        {
            new EncounterDef("fin.commander", "Commander", "The commander is shielded while its guard lives. Kill the guard wave to expose it, then strike.", 5, 0, 100, FinaleRewardFraction, null, null, null),
            new EncounterDef("fin.vault", "Vault", "Three power cells must be charged in order; each charge opens a window to damage the vault core.", 5, 0, 100, FinaleRewardFraction, null, null, null),
            new EncounterDef("fin.convoy", "Convoy", "An escorted carrier moves along a route. Break the escort and destroy the carrier before it reaches the exit.", 5, 0, 100, FinaleRewardFraction, null, new[] { "droplinks" }, null),
        };

        public static readonly MapDef[] Maps =
        {
            new MapDef("map.flatcity", "FlatCity", 2, "outdoor", "urban"),
            new MapDef("map.urbanpark", "UrbanPark", 3, "outdoor", "open"),
            new MapDef("map.beachside", "BeachsideTown", 4, "outdoor", "water"),
            new MapDef("map.departmentstore", "DepartmentStore", 5, "indoor", "vertical"),
            new MapDef("map.warehouse", "Warehouse", 6, "indoor", "droplinks"),
            new MapDef("map.nightland", "NightLand", 7, "outdoor", "droplinks", "dark"),
            new MapDef("map.troy", "Troy", 8, "outdoor", "open", "water"),
        };

        public static readonly RouteDef[] Routes =
        {
            new RouteDef("safe", "Quiet Route", "Fewer elites, no emergencies. Bounty -10%.", 0.9, 0.5, 0.0, 0.0),
            new RouteDef("danger", "Hot Route", "More elites and emergencies likely. Bounty +30%.", 1.3, 1.6, 1.5, 0.0),
            new RouteDef("event", "Strange Route", "Events every stage. Shops offer rarer items.", 1.0, 1.0, 2.0, 0.25),
            new RouteDef("rich", "Rich Route", "Bounty +20%, but the shop charges +20%.", 1.2, 1.0, 1.0, 0.0),
        };

        // ---- lookups
        private static Dictionary<string, ItemDef> items;
        public static IEnumerable<ItemDef> AllItems()
        {
            foreach (var i in Supplies) yield return i;
            foreach (var i in Stats) yield return i;
            foreach (var i in Cores) yield return i;
            foreach (var i in Mods) yield return i;
            foreach (var i in Tacticals) yield return i;
            foreach (var i in Ultimates) yield return i;
            for (int w = 0; w < WeaponCatalog.Count; w++) yield return Weapon(w);
        }

        private static ItemDef[] weapons;
        public static ItemDef Weapon(int index)
        {
            if (weapons == null)
            {
                weapons = new ItemDef[WeaponCatalog.Count];
                for (int w = 0; w < weapons.Length; w++)
                {
                    var def = WeaponCatalog.GetDefault(w);
                    int price = def.handgun ? 15 : def.grenade ? 55 : def.oneShot ? 45 : def.limitAmmo >= 100 ? 50 : 35;
                    weapons[w] = new ItemDef("weapon." + w, ItemKind.Weapon, def.gunName, "Replace your primary weapon. Ammunition is refilled.", price, 1, def.oneShot || def.grenade ? 1 : 0, TagGeneric);
                }
            }
            return weapons[index];
        }

        public static ItemDef Item(string id)
        {
            if (items == null)
            {
                var map = new Dictionary<string, ItemDef>();
                foreach (var i in AllItems()) map[i.Id] = i;
                items = map;
            }
            ItemDef found;
            return id != null && items.TryGetValue(id, out found) ? found : null;
        }

        public static int WeaponIndexOf(string itemId)
        {
            int w;
            return itemId != null && itemId.StartsWith("weapon.") && int.TryParse(itemId.Substring(7), out w) && w >= 0 && w < WeaponCatalog.Count ? w : -1;
        }

        public static EnemyRoleDef Role(string id) { foreach (var r in EnemyRoles) if (r.Id == id) return r; return null; }
        public static MapDef Map(string id) { foreach (var m in Maps) if (m.Id == id) return m; return null; }
        /// <summary>Set by the adapter: maps whose scene is not in the build are never offered or sampled.</summary>
        public static Func<MapDef, bool> MapAvailable = m => true;
        public static bool IsAvailable(MapDef m) { return m != null && (MapAvailable == null || MapAvailable(m)); }
        public static MapDef MapByScene(string sceneName) { foreach (var m in Maps) if (m.SceneName == sceneName) return m; return null; }
        public static readonly RouteDef NeutralRoute = new RouteDef("", "Direct Route", "No modifiers.", 1.0, 1.0, 1.0, 0.0);
        public static RouteDef Route(string tag) { if (!string.IsNullOrEmpty(tag)) foreach (var r in Routes) if (r.Tag == tag) return r; return NeutralRoute; }
        public static EncounterDef Encounter(string id)
        {
            foreach (var e in Objectives) if (e.Id == id) return e;
            foreach (var e in Events) if (e.Id == id) return e;
            foreach (var e in Emergencies) if (e.Id == id) return e;
            foreach (var e in Finales) if (e.Id == id) return e;
            return null;
        }

        /// <summary>Price of an item for a chapter; the route may add a surcharge. Fixed and readable, never wallet-dependent.</summary>
        public static long PriceMinor(ItemDef item, int chapter, string routeTag, int ownedStacks)
        {
            double price = item.BasePrice * RogueDepth.PriceMultiplier(chapter);
            if (item.Kind == ItemKind.Stat) price *= 1.0 + 0.25 * ownedStacks;   // tiers get dearer: clear marginal cost
            if (item.Kind == ItemKind.Core || item.Kind == ItemKind.Mod) price *= 1 + TierPriceStep * Math.Max(0, ownedStacks);
            if (routeTag == "rich") price *= 1.2;
            return RogueMoney.Coins((long)Math.Round(price));
        }

        /// <summary>Deterministic content hash (FNV-1a over item data and role aim/icon data) used for room compatibility.</summary>
        public static string ContentHash()
        {
            ulong h = 14695981039346656037UL;
            Action<string> mix = s => { foreach (char c in s) { h ^= c; h = unchecked(h * 1099511628211UL); } };
            mix(RulesVersion);
            foreach (var i in AllItems()) { mix(i.Id); mix(i.BasePrice.ToString()); mix(i.Effect); mix(MaxTier(i.Id).ToString()); }
            RogueTiers.Hash(mix);
            var defaults = new BuildStats();
            foreach (double value in new[] { TierPriceStep, DashDistance, DashSpeed, DashCooldownSeconds, DashMinIntervalSeconds,
                ConvoyFailureRewardMultiplier, RogueEconomy.EventBudgetFraction, (double)RogueShop.RefundNumerator, RogueShop.RefundDenominator,
                defaults.AssaultCloseRange, defaults.MomentumShotWindowSeconds, defaults.SuppressionWindowSeconds,
                SuppressionTracker.DecayIntervalSeconds, BuildStats.MaxReviveSpeedMul })
                mix(value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            foreach (var e in Objectives) mix(e.RewardFraction.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            foreach (var e in Finales) mix(e.RewardFraction.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            foreach (var r in EnemyRoles)
            {
                mix(r.Id); mix(r.Weight.ToString()); mix(r.Marker); mix(r.Brief);
                mix(r.AimNear.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                mix(r.AimMid.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                mix(r.AimFar.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            }
            foreach (var e in Objectives) mix(e.Id);
            foreach (var e in Events) mix(e.Id);
            foreach (var e in Emergencies) mix(e.Id);
            foreach (var e in Finales) mix(e.Id);
            return h.ToString("x16");
        }

        /// <summary>Authoring guard: duplicate ids, empty text or bad prices fail loudly at startup and in tests.</summary>
        public static List<string> Validate()
        {
            var errors = new List<string>();
            var seen = new HashSet<string>();
            foreach (var i in AllItems())
            {
                if (string.IsNullOrEmpty(i.Id) || !seen.Add(i.Id)) errors.Add("duplicate or empty item id: " + i.Id);
                if (string.IsNullOrEmpty(i.Name) || string.IsNullOrEmpty(i.Effect)) errors.Add("item without name/effect: " + i.Id);
                if (i.BasePrice < 0 || i.BasePrice > 100000) errors.Add("item price out of range: " + i.Id);
                if (i.MaxStacks < 1) errors.Add("item max stacks < 1: " + i.Id);
            }
            if (Cores.Length < 8) errors.Add("fewer than 8 cores");
            if (Mods.Length < 24) errors.Add("fewer than 24 mods");
            if (Stats.Length != 4) errors.Add("stats must be 4");
            if (Tacticals.Length != 3) errors.Add("tacticals must be 3");
            if (Ultimates.Length != 7) errors.Add("ultimates must be 7");
            if (EnemyRoles.Length < 6) errors.Add("fewer than 6 enemy roles");
            if (Objectives.Length < 5) errors.Add("fewer than 5 objectives");
            if (Events.Length < 8) errors.Add("fewer than 8 events");
            if (Emergencies.Length < 4) errors.Add("fewer than 4 emergencies");
            if (Finales.Length < 3) errors.Add("fewer than 3 finales");
            var eids = new HashSet<string>();
            foreach (var group in new[] { Objectives, Events, Emergencies, Finales })
                foreach (var e in group)
                {
                    if (!eids.Add(e.Id)) errors.Add("duplicate encounter id: " + e.Id);
                    foreach (var x in e.Exclusive) if (Encounter(x) == null) errors.Add("unknown exclusive ref " + x + " in " + e.Id);
                }
            foreach (var r in EnemyRoles)
            {
                if (string.IsNullOrWhiteSpace(r.Brief) || string.IsNullOrWhiteSpace(r.Marker)) errors.Add("role without brief/icon: " + r.Id);
                if (!(r.AimNear >= 0.02 && r.AimNear <= r.AimMid && r.AimMid <= r.AimFar && r.AimFar <= 1)) errors.Add("role aim out of range: " + r.Id);
                if (r.Weight <= 0) errors.Add("role weight must be positive: " + r.Id);
                foreach (var w in r.Weapons) if (w < 0 || w >= WeaponCatalog.Count) errors.Add("role weapon out of range: " + r.Id);
            }
            return errors;
        }
    }
}
