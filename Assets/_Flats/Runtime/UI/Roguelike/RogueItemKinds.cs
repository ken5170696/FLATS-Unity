using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// One colour and one label per item category (stat, core, mod, tactical, ultimate, weapon, supply), shared by shop rows, reward
/// cards and the overview so a player can tell at a glance what kind of thing an item is. Rarity is a suffix of the label, never the
/// colour. Views receive the category through the existing rarity parameter as "kind|label" (see Parse).
/// </summary>
public static class RogueItemKinds
{
    public static readonly Color StatTint = new Color(0.8f, 0.1f, 0.4f), CoreTint = new Color(0.5f, 0.27f, 0.85f), ModTint = new Color(0.18f, 0.52f, 0.92f),
        TacticalTint = new Color(0.08f, 0.6f, 0.48f), UltimateTint = new Color(0.9f, 0.58f, 0.06f), WeaponTint = new Color(0.28f, 0.28f, 0.34f), SupplyTint = new Color(0.5f, 0.5f, 0.56f);

    public static Color Tint(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.Stat: return StatTint;
            case ItemKind.Core: return CoreTint;
            case ItemKind.Mod: return ModTint;
            case ItemKind.Tactical: return TacticalTint;
            case ItemKind.Ultimate: return UltimateTint;
            case ItemKind.Weapon: return WeaponTint;
            default: return SupplyTint;
        }
    }

    /// <summary>English source label (translated by the caller's table).</summary>
    public static string Label(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.Stat: return "Stat";
            case ItemKind.Core: return "Core";
            case ItemKind.Mod: return "Mod";
            case ItemKind.Tactical: return "Tactical";
            case ItemKind.Ultimate: return "Ultimate";
            case ItemKind.Weapon: return "Weapon";
            default: return "Supply";
        }
    }

    /// <summary>"kind|Label · Rarity" for an item; the label part is already translated.</summary>
    public static string Tag(ItemDef def, string rarityText)
    {
        if (def == null) return rarityText ?? "";
        // "Item kind X" keys: plain "Mod" and "Supply" already mean the Mod Center and the Moving Supply event in the table
        string key = "Item kind " + Label(def.Kind), translated = FlatsLocalization.Translate(key);
        string label = translated == key ? Label(def.Kind) : translated;
        return (int)def.Kind + "|" + (string.IsNullOrEmpty(rarityText) ? label : label + " · " + rarityText);
    }

    /// <summary>Splits a tag made by Tag. False for plain rarity or route strings, which keep their old look.</summary>
    public static bool Parse(string tag, out Color tint, out string label)
    {
        tint = SupplyTint; label = tag ?? "";
        if (string.IsNullOrEmpty(tag)) return false;
        int bar = tag.IndexOf('|');
        int kind;
        if (bar <= 0 || !int.TryParse(tag.Substring(0, bar), out kind)) return false;
        tint = Tint((ItemKind)kind); label = tag.Substring(bar + 1);
        return true;
    }

    /// <summary>The name the rest of FLATS shows for a catalog gun ("Assault Rifle 1" is "Assault Rifle typeA" on the character and
    /// weapon screens, "Light Machine Gun" is "Light Machinegun"), translated.</summary>
    public static string WeaponDisplayName(string catalogName)
    {
        if (string.IsNullOrEmpty(catalogName)) return "";
        string name = catalogName == "Light Machine Gun" ? "Light Machinegun" : catalogName;
        int space = name.LastIndexOf(' ');
        int number;
        if (space > 0 && int.TryParse(name.Substring(space + 1), out number) && number >= 1 && number <= 26)
            name = name.Substring(0, space) + " type" + (char)('A' + number - 1);
        return FlatsLocalization.Translate(name);
    }

    /// <summary>Darker variant for text on a pale background (the gold and teal are too light as text).</summary>
    public static Color TextTint(Color tint) { return new Color(tint.r * 0.72f, tint.g * 0.72f, tint.b * 0.72f, 1f); }
}
