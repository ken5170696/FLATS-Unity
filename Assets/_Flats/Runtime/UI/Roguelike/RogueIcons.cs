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
        image.sprite = s; image.enabled = s != null; image.preserveAspect = true;
    }

    /// <summary>Icon for a catalog item or a tag. Falls back through kind and the first core tag.</summary>
    public static string ForItem(Flats.Core.Roguelike.ItemDef def)
    {
        if (def == null) return "";
        switch (def.Id)
        {
            case "supply.ammo": return "Ammo";
            case "supply.medkit": return "Medkit";
            case "supply.repair": return "Load";
            case "stat.health": return "Heart";
            case "stat.damage": return "Fire";
            case "stat.magazine": return "Ammo";
            case "stat.speed": return "Jump";
            case "tactical.doublejump": return "Jump";
            case "tactical.dash": return "Dash";
            case "tactical.shield": return "Shield";
        }
        if (def.Kind == Flats.Core.Roguelike.ItemKind.Ultimate) return "Ultimate";
        if (def.Kind == Flats.Core.Roguelike.ItemKind.Weapon) return "Fire";
        if (def.Kind == Flats.Core.Roguelike.ItemKind.Core) return "Core";
        if (def.Tags.Length > 0) return ForTag(def.Tags[0]);
        return def.Kind == Flats.Core.Roguelike.ItemKind.Mod ? "Mod" : "Square";
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
