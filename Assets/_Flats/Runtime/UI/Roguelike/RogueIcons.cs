using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Icon library for the mode's HUD and screens: authored sprite references on the HUD prefab
/// (white flat silhouettes from Art/UI, the FLATS style), looked up by name at runtime.
/// Item kinds, cores, stats, routes and waypoint kinds map to a small, consistent set.
/// </summary>
public class RogueIcons : MonoBehaviour
{
    public string[] names = new string[0];
    public Sprite[] sprites = new Sprite[0];
    static RogueIcons instance;
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    void Awake() { instance = this; Fill(this); }
    void OnDestroy() { if (instance == this) { instance = null; cache.Clear(); } }

    static void Fill(RogueIcons set)
    {
        cache.Clear();
        for (int i = 0; i < set.names.Length && i < set.sprites.Length; i++) if (!string.IsNullOrEmpty(set.names[i]) && set.sprites[i] != null) cache[set.names[i]] = set.sprites[i];
    }

    /// <summary>The authored icon set (Resources/UI/Roguelike/RogueIconSet) is read on first use; no scene instance is needed.</summary>
    static void EnsureLoaded()
    {
        if (cache.Count > 0 || instance != null) return;
        var prefab = Resources.Load<GameObject>("UI/Roguelike/RogueIconSet");
        var set = prefab != null ? prefab.GetComponent<RogueIcons>() : null;
        if (set != null) Fill(set);
    }

    public static Sprite Get(string name)
    {
        EnsureLoaded();
        Sprite s;
        return !string.IsNullOrEmpty(name) && cache.TryGetValue(name, out s) ? s : null;
    }

    /// <summary>Key hint for an action as the current input method would show it (keyboard letter or pad glyph text).</summary>
    public static string KeyHint(string action)
    {
        return RogueInput.KeyCap(action);   // empty on phones: they tap the slot itself, a keyboard key cap would mislead
    }

    public static void Apply(Image image, string name)
    {
        if (image == null) return;
        var s = Get(name);
        // an empty name hides the image on purpose; a name the set does not have is a wrong reference and says so once
        if (s == null && !string.IsNullOrEmpty(name)) WarnOnce("FLATS_ROGUE_ICON missing sprite '" + name + "' in Resources/UI/Roguelike/RogueIconSet");
        image.sprite = s; image.enabled = s != null; image.preserveAspect = true;
    }

    static readonly HashSet<string> warned = new HashSet<string>();
    static void WarnOnce(string message) { if (warned.Add(message)) Debug.LogWarning(message); }

    /// <summary>
    /// Every tactical and ultimate has its own icon (QA-08): the ability slots, the shop and reward cards, the TAB overview and
    /// the records all read this one table, so a slot never falls back to a shared picture. The ultimate's generic "Ultimate"
    /// bolt is only the fallback for an id this table does not know yet, and that fallback is logged once.
    /// </summary>
    static readonly Dictionary<string, string> abilityIcons = new Dictionary<string, string>
    {
        { "tactical.doublejump", "Wings" },       // a second jump in the air
        { "tactical.dash", "Dash" },              // chevrons: a burst forward
        { "tactical.shield", "Shield" },
        { "ult.infinite_fire", "Infinity" },      // unlimited ammunition
        { "ult.lethal_shot", "Skull" },           // hits kill outright
        { "ult.invincible", "Star" },             // the same star as the HUD's invulnerability badge
        { "ult.emergency_revive", "Plus" },       // medical cross: bring teammates back
        { "ult.enemy_sight", "Eye" },             // see enemies through walls
        { "ult.chain_bullets", "Link" },          // hits chain to nearby enemies
        { "ult.homing_bullets", "Target" },       // bullets seek a target
    };

    /// <summary>Icon for an equipped tactical or ultimate id (the HUD slots); "" for none.</summary>
    public static string ForAbility(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        return ForItem(Flats.Core.Roguelike.RogueCatalog.Item(id), id);
    }

    /// <summary>Icon for a catalog item or a tag. Falls back through kind and the first core tag.</summary>
    public static string ForItem(Flats.Core.Roguelike.ItemDef def) { return ForItem(def, null); }

    static string ForItem(Flats.Core.Roguelike.ItemDef def, string requestedId)
    {
        if (def == null)
        {
            if (!string.IsNullOrEmpty(requestedId)) WarnOnce("FLATS_ROGUE_ICON unknown item id '" + requestedId + "'");
            return "";
        }
        string icon;
        if (abilityIcons.TryGetValue(def.Id, out icon)) return icon;
        switch (def.Id)
        {
            case "supply.ammo": return "Ammo";
            case "supply.medkit": return "Medkit";
            case "supply.repair": return "Load";
            case "stat.health": return "Heart";
            case "stat.damage": return "Fire";
            case "stat.magazine": return "Ammo";
            case "stat.speed": return "Jump";
        }
        if (def.Kind == Flats.Core.Roguelike.ItemKind.Ultimate) { WarnOnce("FLATS_ROGUE_ICON no icon mapped for ultimate '" + def.Id + "'; using the generic Ultimate icon"); return "Ultimate"; }
        if (def.Kind == Flats.Core.Roguelike.ItemKind.Tactical) { WarnOnce("FLATS_ROGUE_ICON no icon mapped for tactical '" + def.Id + "'; using the generic Dash icon"); return "Dash"; }
        if (def.Kind == Flats.Core.Roguelike.ItemKind.Weapon) return "Fire";
        if (def.Kind == Flats.Core.Roguelike.ItemKind.Core) return "Core";
        if (def.Tags.Length > 0) return ForTag(def.Tags[0]);
        return def.Kind == Flats.Core.Roguelike.ItemKind.Mod ? "Mod" : "Square";
    }

    /// <summary>Icon for a hold interaction from its action (RogueInteractable.Action): what the ring around the crosshair shows.</summary>
    public static string ForInteraction(string action)
    {
        if (string.IsNullOrEmpty(action)) return "Tap";
        if (action.StartsWith("vent")) return "Wind";
        if (action.StartsWith("breaker")) return "Bolt";
        if (action.StartsWith("cell")) return "Battery";
        if (action.StartsWith("cache")) return "Crate";
        if (action.StartsWith("repair") || action.StartsWith("sidedevice") || action.StartsWith("generator")) return "Settings5";
        return "Tap";
    }

    public static string ForTag(string tag)
    {
        switch (tag)
        {
            case "precision": return "Sight";
            case "assault": return "Fire";
            case "suppression": return "Ammo";
            case "reload": return "Reload";
            case "ricochet": return "Share";
            case "demolition": return "Zoom";
            case "marker": return "Multiplayer4";
            case "mobility": return "Jump";
            default: return "Mod";
        }
    }

    public static string ForRoute(string tag)
    {
        switch (tag) { case "safe": return "Check"; case "danger": return "Warning"; case "event": return "Settings5"; case "rich": return "Coin"; default: return "Singleplayer5"; }
    }

    /// <summary>Icon for a player's life state on the squad list.</summary>
    public static string ForLife(Flats.Core.Roguelike.PlayerLife life)
    {
        switch (life) { case Flats.Core.Roguelike.PlayerLife.Downed: return "Medkit"; case Flats.Core.Roguelike.PlayerLife.Dead: return "Warning"; default: return "Heart"; }
    }

    public static string ForEncounter(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        if (id.StartsWith("em.")) return "Warning";
        if (id.StartsWith("fin.")) return "Main4";
        if (id.StartsWith("ev.")) return "Settings5";
        // Deliver the Crate uses the crate icon ("Square" is the plain white fill sprite, not an icon)
        switch (id) { case "obj.capture": return "Load"; case "obj.carry": return "Crate"; case "obj.protect": return "Shield"; case "obj.breakout": return "Check"; default: return "Objective"; }
    }
}
