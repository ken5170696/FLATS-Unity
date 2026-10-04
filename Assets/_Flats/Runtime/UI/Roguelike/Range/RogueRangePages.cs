using System;
using System.Collections.Generic;
using System.Globalization;
using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// What the shooting-range menu shows: one list of tiles per tab, each with the actions of its detail panel. Names and effect
/// sentences are the catalogue's own (RogueCatalog, RogueArmory, SkillTree, MetaText), so the range never describes an item
/// differently from the shop or the headquarters. Every action is a "range" command to the authority (RoguelikeController.RangeSend)
/// or a local switch of RogueRangeRuntime; nothing here changes a rule.
/// </summary>
public static class RogueRangePages
{
    public const int TabCount = 5;
    static readonly string[] tabNames = { "Enemies", "Weapons", "Build", "Skills", "Scene" };
    public static string TabName(int index) { return T(tabNames[Mathf.Clamp(index, 0, tabNames.Length - 1)]); }

    // spawn settings: local to this player, kept while the scene lives
    static int variant, countIndex, depthIndex, place, behaviour, affixIndex, meleeIndex;

    static string T(string key, params object[] args) { return RoguelikeController.T(key, args); }
    static string Name(string key) { return RogueMetaUI.T(key); }
    static string L(TextLine line) { return string.IsNullOrEmpty(line.Template) ? "" : RogueMetaUI.L(line); }
    static Sprite Icon(string key) { return FlatsHqView.Icon(key); }
    static string Inv(double v) { return Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture); }
    static string Kind(ItemKind kind)
    {
        string key = "Item kind " + RogueItemKinds.Label(kind), translated = FlatsLocalization.Translate(key);
        return translated == key ? RogueItemKinds.Label(kind) : translated;
    }
    static string Join(params string[] parts)
    {
        var list = new List<string>();
        foreach (var p in parts) if (!string.IsNullOrEmpty(p)) list.Add(p);
        return string.Join("\n\n", list.ToArray());
    }

    /// <summary>The line under the heading: how full the field is, the depth new enemies use, and this player's switches.</summary>
    public static string Status(RoguelikeController c, RogueRangeRuntime runtime)
    {
        var state = c.State; var me = c.LocalPlayer;
        var parts = new List<string> { T("Enemies {0} / {1}", c.AliveEnemies, RogueRange.EnemyCap), T("Stage {0}-{1}", RogueDepth.ChapterOf(state.depth), RogueDepth.StageInChapter(state.depth)) };
        if (c.RangeFrozen) parts.Add(T("Enemies frozen"));
        if (RogueRange.HasFlag(me, RogueRange.GodFlag)) parts.Add(T("No damage"));
        if (RogueRange.HasFlag(me, RogueRange.InfiniteAmmoFlag)) parts.Add(T("Infinite ammo"));
        return string.Join("  ·  ", parts.ToArray()) + "\n" + T("Free: nothing here is saved or counted.");
    }

    // The open menu asks for its page twice a second (RogueRangeView.Update). Composing every tile's text each time was the
    // menu's whole idle cost, so the last page is kept while nothing it shows has changed: the run state's sequence number (every
    // range command and every broadcast advances it), the field's counters, this file's own choices, the language and the input
    // scheme. It is composed again after CacheSeconds at the latest.
    const float CacheSeconds = 5f;
    static readonly List<RogueRangeEntry> cached = new List<RogueRangeEntry>();
    static string cachedStamp; static float cachedAt;

    static string Stamp(RoguelikeController c, RogueRangeRuntime runtime, RogueRangeView view, int tab)
    {
        var s = c.State;
        return string.Concat(new[] {
            c.GetInstanceID().ToString(), ".", view != null ? view.GetInstanceID().ToString() : "-", ".", tab.ToString(), ".", s.eventSeq.ToString(), ".", s.depth.ToString(), ".", s.rangeFlags.ToString(), ".",
            c.AliveEnemies.ToString(), ".", c.RangeQueued.ToString(), ".", variant.ToString(), countIndex.ToString(), depthIndex.ToString(), place.ToString(), behaviour.ToString(), ".", affixIndex.ToString(), ".", meleeIndex.ToString(), ".",
            FlatsLocalization.IsChinese ? "z" : "e", ((int)RogueInput.Current).ToString(), ".",
            runtime != null ? runtime.TimeScale.ToString(CultureInfo.InvariantCulture) + (runtime.StatsShown ? "s" : "h") + (runtime.Equipping ? "b" : "r") : "-" });
    }

    public static void Build(RoguelikeController c, RogueRangeRuntime runtime, RogueRangeView view, int tab, List<RogueRangeEntry> into)
    {
        var me = c.LocalPlayer;
        if (me == null || me.build == null) return;
        string stamp = Stamp(c, runtime, view, tab);
        if (stamp == cachedStamp && Time.unscaledTime - cachedAt < CacheSeconds) { into.AddRange(cached); return; }
        me.build.Normalize();
        int first = into.Count;
        switch (tab)
        {
            case 0: Enemies(c, into); break;
            case 1: Weapons(c, me, runtime, into); break;
            case 2: BuildItems(c, me, runtime, into); break;
            case 3: Skills(c, me, into); break;
            default: Scene(c, me, runtime, into); break;
        }
        cached.Clear();
        for (int i = first; i < into.Count; i++) cached.Add(into[i]);
        cachedStamp = stamp; cachedAt = Time.unscaledTime;
    }

    // ---------------------------------------------------------------- enemies
    static readonly string[] variantNames = { "Normal", "Elite", "Chapter target" };
    static readonly string[] placeNames = { "At the crosshair", "In front of me", "Random spawn point" };
    static readonly string[] behaviourNames = { "Fights", "Stands still" };

    static void Enemies(RoguelikeController c, List<RogueRangeEntry> into)
    {
        int alive = c.AliveEnemies; bool full = alive >= RogueRange.EnemyCap;
        var clear = new RogueRangeEntry
        {
            key = "enemies.clear", title = T("Clear all enemies"), tag = T("Field"), number = alive.ToString(CultureInfo.InvariantCulture), icon = Icon("Skull"), token = FlatsUiTheme.Token.Ink,
            body = T("Removes every enemy on the field and everything waiting to spawn. No kill is counted."),
        };
        clear.Add(T("Clear now"), "", () => c.RangeSend("clearenemies"), alive > 0 || c.RangeQueued > 0, true);
        into.Add(clear);

        bool frozen = c.RangeFrozen;
        var freeze = new RogueRangeEntry
        {
            key = "enemies.freeze", title = T(frozen ? "Unfreeze enemies" : "Freeze enemies"), tag = T("Field"), icon = Icon("Snow"), token = frozen ? FlatsUiTheme.Token.Mod : FlatsUiTheme.Token.Ink, selected = frozen, subtitle = frozen ? T("On") : "",
            body = T("Frozen enemies stand where they are and do not attack (the stun icon shows over them). They still take damage. After unfreezing, a running stun lasts up to {0} s more.", Inv(RoguelikeController.RangeHoldStunSeconds)),
        };
        freeze.Add(T(frozen ? "Unfreeze" : "Freeze"), T(frozen ? "On" : "Off"), () => c.RangeSend("freeze|" + (frozen ? 0 : 1)), true, true);
        into.Add(freeze);

        foreach (var role in RogueCatalog.EnemyRoles)
        {
            var def = role;
            var entry = new RogueRangeEntry
            {
                key = "enemy." + def.Id, title = T(def.Name), tag = T("Enemy"), icon = Icon(def.Marker), token = FlatsUiTheme.Token.BrandPrimary,
                // with the "Chapter target" variant the tile spawns the chapter's target whatever enemy it shows: said first, where it is read
                body = Join(variant == 2 ? T("Variant \"Chapter target\": every enemy tile spawns the chapter's target, whichever enemy it shows.") : "",
                    T(def.Brief), T("Health x{0}  ·  Speed x{1}  ·  Damage x{2}", Inv(def.HealthMul), Inv(def.SpeedMul), Inv(def.DamageMul)),
                    T("In a run from stage {0}.", def.MinDepth),
                    T("Depth sets health when the enemy spawns. Enemy damage always follows the depth chosen last.")),
            };
            int count = RogueRange.Counts[countIndex % RogueRange.Counts.Length];
            int room = RogueRange.SpawnRoom(alive + c.RangeQueued, count);
            entry.Add(full ? T("The field is full ({0} enemies).", RogueRange.EnemyCap) : T(variant == 2 ? "Spawn chapter target x{0}" : "Spawn x{0}", Mathf.Max(1, room)), "", () => Spawn(c, def), !full, true);
            AddSpawnSettings(entry, def.Melee || def.Operative);
            into.Add(entry);
        }
    }

    static void AddSpawnSettings(RogueRangeEntry entry, bool carriesMelee)
    {
        entry.Add(T("Variant"), T(variantNames[variant]), () => { variant = (variant + 1) % variantNames.Length; });
        var affixes = EliteAffixes.All;
        string affixName = affixIndex == 0 ? T("None") : T(affixes[affixIndex - 1].Name);
        entry.Add(T("Elite affix"), variant == 0 ? T("Elites only") : affixName, () => { affixIndex = (affixIndex + 1) % (affixes.Length + 1); }, variant != 0);
        if (carriesMelee)
        {
            var weapons = RogueArmory.Melee;
            entry.Add(T("Melee weapon"), meleeIndex == 0 ? T("Random") : Name(weapons[meleeIndex - 1].Name), () => { meleeIndex = (meleeIndex + 1) % (weapons.Length + 1); }, variant != 2);
        }
        entry.Add(T("Count"), RogueRange.Counts[countIndex % RogueRange.Counts.Length].ToString(CultureInfo.InvariantCulture), () => { countIndex = (countIndex + 1) % RogueRange.Counts.Length; });
        int depth = RogueRange.Depths[depthIndex % RogueRange.Depths.Length];
        entry.Add(T("Depth"), T("Stage {0}-{1}", RogueDepth.ChapterOf(depth), RogueDepth.StageInChapter(depth)), () => { depthIndex = (depthIndex + 1) % RogueRange.Depths.Length; });
        entry.Add(T("Where"), T(placeNames[place]), () => { place = (place + 1) % placeNames.Length; });
        entry.Add(T("Behaviour"), T(behaviourNames[behaviour]), () => { behaviour = (behaviour + 1) % behaviourNames.Length; });
    }

    static void Spawn(RoguelikeController c, EnemyRoleDef def)
    {
        var player = RoguelikeController.FindLocalPlayer();
        if (player == null) return;
        Vector3 point = player.transform.position + player.transform.forward * 20f;
        var cam = Camera.main;
        if (place == 0 && cam != null)
        {
            RaycastHit hit;
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            // the ground or a wall the crosshair rests on; open sky falls back to a point ahead
            if (Physics.Raycast(ray, out hit, 300f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore)) point = hit.point;
            else point = cam.transform.position + cam.transform.forward * 40f;
        }
        // spawned enemies face the player who asked for them
        Vector3 toPlayer = player.transform.position - point; toPlayer.y = 0f;
        float yaw = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer).eulerAngles.y : 0f;
        string affix = variant != 0 && affixIndex > 0 ? EliteAffixes.All[affixIndex - 1].Id : "";
        string melee = (def.Melee || def.Operative) && variant != 2 && meleeIndex > 0 ? RogueArmory.Melee[meleeIndex - 1].Id : "";
        c.RangeSend(string.Join("|", new[]
        {
            "spawn", def.Id, variant.ToString(CultureInfo.InvariantCulture), affix, melee,
            RogueRange.Counts[countIndex % RogueRange.Counts.Length].ToString(CultureInfo.InvariantCulture),
            RogueRange.Depths[depthIndex % RogueRange.Depths.Length].ToString(CultureInfo.InvariantCulture),
            place.ToString(CultureInfo.InvariantCulture), behaviour.ToString(CultureInfo.InvariantCulture),
            RoguelikeController.RangePointText(point), yaw.ToString("0.#", CultureInfo.InvariantCulture),
        }));
    }

    // ---------------------------------------------------------------- weapons
    static void Weapons(RoguelikeController c, RunPlayer me, RogueRangeRuntime runtime, List<RogueRangeEntry> into)
    {
        var b = me.build; var meta = b.meta ?? new MetaLoadout();
        bool busy = runtime != null && runtime.Equipping;
        var refill = new RogueRangeEntry
        {
            key = "weapons.refill", title = T("Refill ammo"), tag = T("Ammunition"), icon = Icon("Ammo"), token = FlatsUiTheme.Token.Ink,
            body = T("Fills the magazine and the reserve of both weapons."),
        };
        refill.Add(T("Refill now"), "", () => { if (runtime != null) runtime.RefillLocal(); }, true, true);
        into.Add(refill);

        bool infinite = RogueRange.HasFlag(me, RogueRange.InfiniteAmmoFlag);
        var endless = new RogueRangeEntry
        {
            key = "weapons.infinite", title = T("Infinite ammo"), tag = T("Ammunition"), icon = Icon("Infinity"), token = infinite ? FlatsUiTheme.Token.Ultimate : FlatsUiTheme.Token.Ink, selected = infinite, subtitle = infinite ? T("On") : "",
            body = T("Firing uses no ammunition and never needs a reload, like the Infinite Fire ultimate without a time limit."),
        };
        endless.Add(T(infinite ? "Turn off" : "Turn on"), T(infinite ? "On" : "Off"), () => c.RangeSend("flag|" + RogueRange.InfiniteAmmoFlag + "|" + (infinite ? 0 : 1)), true, true);
        into.Add(endless);

        var listed = new HashSet<int>();
        foreach (var weapon in RogueArmory.Ranged)
        {
            listed.Add(weapon.BaseModel);
            into.Add(WeaponEntry(c, b, meta, weapon.Id, weapon.BaseModel, weapon, busy));
        }
        for (int model = 0; model < Flats.Core.WeaponCatalog.Count; model++)
            if (!listed.Contains(model)) into.Add(WeaponEntry(c, b, meta, "model:" + model, model, null, busy));

        foreach (var sight in RogueArmory.Sights)
        {
            var def = sight;
            bool onPrimary = meta.primarySight == def.Id, onSecondary = meta.secondarySight == def.Id;
            var entry = new RogueRangeEntry
            {
                key = "sight." + def.Id, title = Name(def.Name), tag = T("Weapon sight"), icon = FlatsHqView.Weapon(def.Id), token = FlatsUiTheme.Token.Tactical, selected = onPrimary || onSecondary,
                subtitle = onPrimary && onSecondary ? T("On both weapons") : onPrimary ? T("On the primary") : onSecondary ? T("On the secondary") : "",
                body = Join(L(MetaText.Sight(def)), T("The weapon is taken out again with the new sight: the menu closes while the character does it.")),
            };
            bool fitsPrimary = RunMachine.RangeSightFits(meta.primary, def.Id), fitsSecondary = RunMachine.RangeSightFits(meta.secondary, def.Id);
            entry.Add(T("Mount on the primary"), onPrimary ? T("Mounted") : fitsPrimary ? "" : T("Does not fit"), () => c.RangeSend("sight|0|" + def.Id), fitsPrimary && !onPrimary && !busy, true, true);
            entry.Add(T("Mount on the secondary"), onSecondary ? T("Mounted") : fitsSecondary ? "" : T("Does not fit"), () => c.RangeSend("sight|1|" + def.Id), fitsSecondary && !onSecondary && !busy, false, true);
            into.Add(entry);
        }

        foreach (var melee in RogueArmory.Melee)
        {
            var def = melee;
            bool equipped = meta.melee == def.Id;
            var entry = new RogueRangeEntry
            {
                key = "melee." + def.Id, title = Name(def.Name), tag = T("Melee weapon"), icon = FlatsHqView.Weapon(def.Id), token = FlatsUiTheme.Token.Weapon, selected = equipped,
                subtitle = equipped ? T("Equipped") : "",
                body = Join(T(def.Flavor), T("Damage {0}  ·  Reach {1} m", Inv(def.Damage), Inv(def.Range)), L(MetaText.MeleeSpecialText(def)), L(MetaText.MeleeDrawback(def))),
            };
            entry.Add(T("Equip"), equipped ? T("Equipped") : "", () => c.RangeSend("melee|" + def.Id), !equipped, true);
            into.Add(entry);
        }
    }

    static RogueRangeEntry WeaponEntry(RoguelikeController c, PlayerBuild b, MetaLoadout meta, string id, int model, RangedWeaponDef def, bool busy)
    {
        bool isPrimary = def != null ? meta.primary == def.Id : b.primaryWeapon == model && string.IsNullOrEmpty(meta.primary);
        bool isSecondary = def != null ? meta.secondary == def.Id : b.secondaryWeapon == model && string.IsNullOrEmpty(meta.secondary);
        var legacy = Flats.Core.WeaponCatalog.GetDefault(model);
        string body;
        if (def != null)
        {
            var r = RogueArmory.Resolve(def);
            // a handgun is semi-automatic: its rate is the trigger's (WeaponRules.HandgunShotsPerSecond), the catalogue rpm is not used
            body = Join(T(def.Flavor),
                legacy.handgun ? T("Damage {0}  ·  Semi-automatic, up to {1} shots a second  ·  Magazine {2}  ·  Reload {3} s", Inv(r.Damage), Inv(WeaponRules.HandgunShotsPerSecond), r.Magazine, Inv(r.Reload))
                               : T("Damage {0}  ·  {1} RPM  ·  Magazine {2}  ·  Reload {3} s", Inv(r.Damage), Inv(r.Rpm), r.Magazine, Inv(r.Reload)),
                L(MetaText.Trait(def)), L(MetaText.Drawback(def)));
        }
        else body = legacy.handgun ? T("Damage {0}  ·  Semi-automatic, up to {1} shots a second  ·  Magazine {2}  ·  Reload {3} s", Inv(legacy.damage), Inv(WeaponRules.HandgunShotsPerSecond), legacy.limitAmmo, Inv(legacy.reloadTime))
                                   : T("Damage {0}  ·  {1} RPM  ·  Magazine {2}  ·  Reload {3} s", Inv(legacy.damage), Inv(legacy.rpm), legacy.limitAmmo, Inv(legacy.reloadTime));
        var entry = new RogueRangeEntry
        {
            key = "weapon." + id, title = def != null ? Name(def.Name) : RogueItemKinds.WeaponDisplayName(legacy.gunName), tag = T("Weapon"),
            icon = def != null ? FlatsHqView.Weapon(def.Id) : Icon("Fire"), token = FlatsUiTheme.Token.Weapon, selected = isPrimary || isSecondary,
            subtitle = isPrimary ? T("Primary") : isSecondary ? T("Secondary") : "",
            body = Join(body, T("Equipping fills its ammunition. The menu closes while the character takes the weapon.")),
        };
        // one gun model cannot be two different weapons at once: the other slot must hold another model
        bool blocksPrimary = b.secondaryWeapon == model, blocksSecondary = b.primaryWeapon == model;
        entry.Add(T("Equip as primary"), isPrimary ? T("Equipped") : blocksPrimary ? T("In the other slot") : "", () => c.RangeSend("weapon|0|" + id), !isPrimary && !blocksPrimary && !busy, true, true);
        entry.Add(T("Equip as secondary"), isSecondary ? T("Equipped") : blocksSecondary ? T("In the other slot") : "", () => c.RangeSend("weapon|1|" + id), !isSecondary && !blocksSecondary && !busy, false, true);
        return entry;
    }

    // ---------------------------------------------------------------- build
    static void BuildItems(RoguelikeController c, RunPlayer me, RogueRangeRuntime runtime, List<RogueRangeEntry> into)
    {
        var b = me.build;
        var clear = new RogueRangeEntry
        {
            key = "build.clear", title = T("Clear everything"), tag = T("Build"), icon = Icon("Reload"), token = FlatsUiTheme.Token.Ink,
            body = T("Removes every stat tier, core, mod, the tactical, the ultimate and every learned skill. Weapons stay."),
        };
        clear.Add(T("Clear now"), "", () => c.RangeSend("clear"), true, true);
        into.Add(clear);

        var loadout = new RogueRangeEntry
        {
            key = "build.loadout", title = T("Use my headquarters loadout"), tag = T("Build"), icon = Icon("Equipment"), token = FlatsUiTheme.Token.Ink,
            body = T("Takes the weapons, sights, melee weapon and learned skills of your active headquarters loadout. Cores, mods and stat tiers stay as they are."),
        };
        loadout.Add(T("Apply"), "", () => c.RangeSend("loadout|" + JsonUtility.ToJson(MetaProfiles.ToLoadout(RogueMetaStore.Current))), runtime == null || !runtime.Equipping, true, true);
        into.Add(loadout);

        bool hasUltimate = !string.IsNullOrEmpty(b.ultimate);
        var charge = new RogueRangeEntry
        {
            key = "build.ultfill", title = T("Fill ultimate charge"), tag = T("Build"), number = hasUltimate ? me.ultimateCharge + "%" : "", icon = Icon("Ultimate"), token = FlatsUiTheme.Token.Ultimate,
            body = hasUltimate ? T("Sets the charge of {0} to 100%. Kills in the range charge it as well.", RoguelikeController.ItemName(b.ultimate)) : T("Equip an ultimate first."),
        };
        charge.Add(T("Fill now"), "", () => c.RangeSend("ultfill"), hasUltimate && me.ultimateCharge < 100, true);
        into.Add(charge);

        bool hasTactical = !string.IsNullOrEmpty(b.tactical);
        var cooldown = new RogueRangeEntry
        {
            key = "build.cooldown", title = T("Reset tactical cooldown"), tag = T("Build"), icon = Icon("Dash"), token = FlatsUiTheme.Token.Tactical,
            body = hasTactical ? T("{0} is ready again at once: every dash charge is back and the shield's cooldown is over.", RoguelikeController.ItemName(b.tactical)) : T("Equip a tactical first."),
        };
        cooldown.Add(T("Reset now"), "", () => { var rp = RogueHooks.Local; if (rp != null) rp.RangeResetCooldowns(); }, hasTactical, true);
        into.Add(cooldown);

        bool god = RogueRange.HasFlag(me, RogueRange.GodFlag);
        var noDamage = new RogueRangeEntry
        {
            key = "build.god", title = T("No damage"), tag = T("Build"), icon = Icon("Shield"), token = god ? FlatsUiTheme.Token.Tactical : FlatsUiTheme.Token.Ink, selected = god, subtitle = god ? T("On") : "",
            body = T("You take no damage at all. Without it a lethal hit puts you back on your feet at full health, protected for {0} s.", Inv(RoguePlayer.RangeRespawnGraceSeconds)),
        };
        noDamage.Add(T(god ? "Turn off" : "Turn on"), T(god ? "On" : "Off"), () => c.RangeSend("flag|" + RogueRange.GodFlag + "|" + (god ? 0 : 1)), true, true);
        into.Add(noDamage);

        foreach (var stat in RogueCatalog.Stats)
        {
            var def = stat; int tier = b.StatTier(def.Id), max = def.MaxStacks;
            var entry = new RogueRangeEntry
            {
                key = "item." + def.Id, title = T(def.Name), tag = Kind(def.Kind), number = tier.ToString(CultureInfo.InvariantCulture), icon = Icon(RogueIcons.ForItem(def)), token = FlatsUiTheme.Token.Stat, selected = tier > 0,
                body = Join(T(def.EffectText(Mathf.Max(1, tier))), T("Tier {0}/{1}", tier, max)),
            };
            entry.Add("+1", "", () => c.RangeSend("stat|" + def.Id + "|" + (tier + 1)), tier < max, true);
            entry.Add("+10", "", () => c.RangeSend("stat|" + def.Id + "|" + Mathf.Min(max, tier + 10)), tier < max);
            entry.Add(T("Set to the top tier"), max.ToString(CultureInfo.InvariantCulture), () => c.RangeSend("stat|" + def.Id + "|" + max), tier < max);
            entry.Add(T("Back to zero"), "", () => c.RangeSend("stat|" + def.Id + "|0"), tier > 0);
            into.Add(entry);
        }
        foreach (var core in RogueCatalog.Cores) into.Add(TieredItem(c, b, core, FlatsUiTheme.Token.Core));
        foreach (var mod in RogueCatalog.Mods) if (RogueShop.Obtainable(mod.Id)) into.Add(TieredItem(c, b, mod, FlatsUiTheme.Token.Mod));
        foreach (var tactical in RogueCatalog.Tacticals) into.Add(SingleItem(c, b, tactical, b.tactical == tactical.Id, FlatsUiTheme.Token.Tactical));
        foreach (var ultimate in RogueCatalog.Ultimates) into.Add(SingleItem(c, b, ultimate, b.ultimate == ultimate.Id, FlatsUiTheme.Token.Ultimate));
    }

    static RogueRangeEntry TieredItem(RoguelikeController c, PlayerBuild b, ItemDef def, FlatsUiTheme.Token token)
    {
        int tier = b.Tier(def.Id), max = RogueCatalog.MaxTier(def.Id);
        var entry = new RogueRangeEntry
        {
            key = "item." + def.Id, title = T(def.Name), tag = Kind(def.Kind), number = tier > 0 ? "T" + tier : "", icon = Icon(RogueIcons.ForItem(def)), token = token, selected = tier > 0,
            body = Join(T(def.EffectText(Mathf.Max(1, tier))),
                max > 1 ? T("Tier {0}/{1}", tier, max) : T(tier > 0 ? "Owned" : "Not owned"),
                T(def.Kind == ItemKind.Core ? "The range has no core limit: hold as many as you like." : "The range has no mod limit: hold as many as you like.")),
        };
        if (max > 1)
        {
            entry.Add(T(tier == 0 ? "Add" : "Tier up"), tier < max ? "T" + (tier + 1) : T("Top tier"), () => c.RangeSend("item|" + def.Id + "|" + (tier + 1)), tier < max, true);
            entry.Add(T("Tier down"), tier > 1 ? "T" + (tier - 1) : "", () => c.RangeSend("item|" + def.Id + "|" + (tier - 1)), tier > 1);
            entry.Add(T("Set to the top tier"), "T" + max, () => c.RangeSend("item|" + def.Id + "|" + max), tier < max);
        }
        else entry.Add(T("Add"), tier > 0 ? T("Owned") : "", () => c.RangeSend("item|" + def.Id + "|1"), tier == 0, true);
        entry.Add(T("Remove"), "", () => c.RangeSend("item|" + def.Id + "|0"), tier > 0);
        return entry;
    }

    static RogueRangeEntry SingleItem(RoguelikeController c, PlayerBuild b, ItemDef def, bool equipped, FlatsUiTheme.Token token)
    {
        var entry = new RogueRangeEntry
        {
            key = "item." + def.Id, title = T(def.Name), tag = Kind(def.Kind), icon = Icon(RogueIcons.ForItem(def)), token = token, selected = equipped,
            subtitle = equipped ? T("Equipped") : "",
            body = T(def.EffectText(1)),
        };
        entry.Add(T("Equip"), equipped ? T("Equipped") : "", () => c.RangeSend("item|" + def.Id + "|1"), !equipped, true);
        entry.Add(T("Take off"), "", () => c.RangeSend("item|" + def.Id + "|0"), equipped);
        return entry;
    }

    // ---------------------------------------------------------------- skills
    static void Skills(RoguelikeController c, RunPlayer me, List<RogueRangeEntry> into)
    {
        var learned = new HashSet<string>(me.build.meta != null && me.build.meta.skills != null ? me.build.meta.skills : new string[0]);
        foreach (var node in SkillTree.Nodes)
        {
            var def = node; bool has = learned.Contains(def.Id);
            var lines = new List<string>();
            foreach (var line in MetaText.Skill(def)) lines.Add(L(line));
            var entry = new RogueRangeEntry
            {
                key = "skill." + def.Id, title = T(def.Name), tag = T(def.Branch.ToString()), icon = RogueMetaUI.SkillIcon(def), selected = has,
                token = has ? FlatsUiTheme.Token.Core : FlatsUiTheme.Token.Ink,
                subtitle = has ? T("Learned") : "",
                body = Join(string.Join("\n", lines.ToArray()), T(RogueMetaUI.SkillFeel(def)), T("The range needs no skill points and no earlier row.")),
            };
            entry.Add(T(has ? "Forget" : "Learn"), has ? T("Learned") : "", () => c.RangeSend("skill|" + def.Id + "|" + (has ? 0 : 1)), true, true);
            into.Add(entry);
        }
    }

    // ---------------------------------------------------------------- scene
    static readonly string[] sceneEvents = { "em.reinforcement_signal", "ev.moving_supply", "em.mobile_bomb" };

    static void Scene(RoguelikeController c, RunPlayer me, RogueRangeRuntime runtime, List<RogueRangeEntry> into)
    {
        var reset = new RogueRangeEntry
        {
            key = "scene.reset", title = T("Reset the range"), tag = T("Scene"), icon = Icon("Reload"), token = FlatsUiTheme.Token.Ink,
            body = T("Removes every enemy and running event, sets the depth back to stage 1-1, unfreezes the field and clears the statistics and ultimate charge of everyone. Builds and weapons stay."),
        };
        // it clears every player's statistics and ultimate charge: in a squad only the host may (the authority refuses the others too)
        bool mayReset = Menu.network == 0 || c.IsAuthority;
        if (!mayReset) reset.body = Join(reset.body, T("Only the host can reset the range."));
        reset.Add(T("Reset the range now"), mayReset ? "" : T("Host"), () => c.RangeSend("reset"), mayReset, true);
        into.Add(reset);

        bool solo = Menu.network == 0;
        float scale = runtime != null ? runtime.TimeScale : 1f;
        var time = new RogueRangeEntry
        {
            key = "scene.time", title = T("Time scale"), tag = T("Scene"), number = "x" + Inv(scale), icon = Icon("Timer"), token = scale != 1f ? FlatsUiTheme.Token.Mod : FlatsUiTheme.Token.Ink, selected = scale != 1f,
            body = solo ? T("How fast the game runs while you play. It goes back to x1 when you leave the range.") : T("Solo only: a squad shares one clock."),
        };
        time.Add(T("Next speed"), "x" + Inv(scale), () =>
        {
            if (runtime == null) return;
            int index = Array.IndexOf(RogueRangeRuntime.TimeScales, runtime.TimeScale);
            runtime.SetTimeScale(RogueRangeRuntime.TimeScales[(index + 1 + RogueRangeRuntime.TimeScales.Length) % RogueRangeRuntime.TimeScales.Length]);
        }, solo, true);
        time.Add(T("Back to x1"), "", () => { if (runtime != null) runtime.SetTimeScale(1f); }, solo && scale != 1f);
        into.Add(time);

        var mode = FlatsControls.DamageNumberStyle;
        var numbers = new RogueRangeEntry
        {
            key = "scene.numbers", title = T("Damage numbers"), tag = T("Scene"), icon = Icon("Target"), token = FlatsUiTheme.Token.Ink,
            subtitle = T(FlatsControls.DamageNumberStyleNames[(int)mode]),
            body = T("The same setting as in Controls: off, one floating number per hit, or one stacked number per target. It stays as you leave it."),
        };
        numbers.Add(T("Next style"), T(FlatsControls.DamageNumberStyleNames[(int)mode]), () => { FlatsControls.DamageNumberStyle = DamageNumberRules.Cycle(FlatsControls.DamageNumberStyle, 1); }, true, true);
        into.Add(numbers);

        bool statsOn = runtime != null && runtime.StatsShown;
        var stats = new RogueRangeEntry
        {
            key = "scene.stats", title = T("Range statistics"), tag = T("Scene"), icon = Icon("List"), token = statsOn ? FlatsUiTheme.Token.Mod : FlatsUiTheme.Token.Ink, selected = statsOn, subtitle = statsOn ? T("On") : "",
            body = Join(T("A panel on the HUD: your damage per second over the last {0} s, total damage, kills, hits and headshots, and your best single hit.", Inv(RogueRangeMeter.WindowSeconds)),
                T("Total damage {0}  ·  Kills {1}  ·  Hits {2}", Math.Round(RogueRangeMeter.TotalDamage).ToString("N0", CultureInfo.InvariantCulture), RogueRangeMeter.Kills, RogueRangeMeter.Hits)),
        };
        stats.Add(T(statsOn ? "Hide the panel" : "Show the panel"), T(statsOn ? "On" : "Off"), () => { if (runtime != null) runtime.SetStatsShown(!statsOn); }, true, true);
        stats.Add(T("Clear my statistics"), "", () => RogueRangeMeter.Reset(), RogueRangeMeter.Hits > 0);
        into.Add(stats);

        foreach (var id in sceneEvents)
        {
            var def = RogueCatalog.Encounter(id);
            if (def == null) continue;
            string eventId = id;
            bool running = c.State.encounter.eventId == id || c.State.encounter.emergencyId == id;
            var entry = new RogueRangeEntry
            {
                key = "scene." + id, title = T(def.Name), tag = T("Event"), icon = Icon(RogueIcons.ForEncounter(id)), token = running ? FlatsUiTheme.Token.Ultimate : FlatsUiTheme.Token.Ink, selected = running,
                subtitle = running ? T("Running") : "",
                body = Join(T(def.Brief), T("It runs as in a stage, at the range's depth, with its markers and the enemies it calls. It pays nothing here.")),
            };
            entry.Add(T("Start"), running ? T("Running") : "", () => c.RangeSend("event|" + eventId), !running, true);
            into.Add(entry);
        }

        bool coop = Menu.network != 0, host = c.IsAuthority;
        var leave = new RogueRangeEntry
        {
            key = "scene.leave", title = T("Leave the range"), tag = T("Scene"), icon = Icon("Exit"), token = FlatsUiTheme.Token.Ink,
            body = !coop ? T("Back to headquarters. Nothing from the range is kept; your saved run is untouched.")
                : host ? T("Takes the whole squad back to the room. Nothing from the range is kept.")
                : T("You leave the squad and go back to the main menu. Only the host can take the squad back to the room."),
        };
        leave.Add(!coop ? T("Back to headquarters") : host ? T("Squad back to the room") : T("Leave the squad"), "", () => { var menu = Menu.Current; if (menu != null) menu.ConfirmRangeLeave(); }, true, true, true);
        into.Add(leave);
    }
}
