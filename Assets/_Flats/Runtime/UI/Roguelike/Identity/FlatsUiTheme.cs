using System;
using UnityEngine;

/// <summary>
/// Colour and type tokens of a FLATS screen family (UI design spec §3.1, §5.1). The Roguelike Survival mode has its own asset,
/// Resources/UI/Roguelike/RogueTheme: the original FLATS vocabulary (pale surfaces, the brand magenta, ink bars and text, white
/// silhouettes on flat colour blocks, the teal of merits as the one secondary colour; owner decision D-05). Designers tune the asset in the Inspector; the Roguelike views
/// read their state colours from it at runtime, and the private authoring tools bake its colours into the Roguelike prefabs
/// (a FlatsThemeTag on each baked graphic records which token it follows). The original menus never read this asset.
/// </summary>
[CreateAssetMenu(menuName = "FLATS/UI Theme", fileName = "FlatsUiTheme")]
public sealed class FlatsUiTheme : ScriptableObject
{
    public enum Token
    {
        None = 0,
        BrandPrimary = 1, BrandHover = 2, BrandPressed = 3, BrandHot = 4, OnBrand = 5,
        Backdrop = 10, Scrim = 11, SurfaceBase = 12, SurfaceRaised = 13, SurfaceRaisedHover = 14, SurfaceSunken = 15, LineSubtle = 16,
        Ink = 17, OnInk = 18, MeritOnInk = 19,
        TextPrimary = 20, TextSecondary = 21, TextMuted = 22,
        Positive = 30, Negative = 31, Warning = 32, Info = 33, Merit = 34,
        Stat = 40, Core = 41, Mod = 42, Tactical = 43, Ultimate = 44, Weapon = 45, Supply = 46,
        RarityCommon = 50, RarityUncommon = 51, RarityRare = 52,
    }

    [Serializable]
    public struct TypeStyle
    {
        [Tooltip("Size in the screen's own canvas units.")] public int size;
        [Tooltip("Line spacing for Latin text.")] public float lineSpacingEn;
        [Tooltip("Line spacing for Chinese text.")] public float lineSpacingZh;
        public TypeStyle(int size, float en, float zh) { this.size = size; lineSpacingEn = en; lineSpacingZh = zh; }
    }

    [Header("Brand: the mode's signature accent (primary buttons, selected tabs, focus, progress)")]
    public Color brandPrimary = Hex(0xCC1964);
    public Color brandHover = Hex(0xE0246F), brandPressed = Hex(0x9C0F4A), brandHot = Hex(0xFF2079);
    [Tooltip("Text and silhouettes drawn on a brand-coloured block.")] public Color onBrand = Color.white;

    [Header("Surfaces")]
    [Tooltip("Full-screen background of a mode screen (behind panels).")] public Color backdrop = Hex(0x17181E);
    public Color scrim = Hex(0x0B0C10, 0.72f);
    public Color surfaceBase = Hex(0x1C1D24, 0.96f), surfaceRaised = Hex(0x262833), surfaceRaisedHover = Hex(0x30323F), surfaceSunken = Hex(0x121318), lineSubtle = Hex(0x3A3C48);
    [Tooltip("The FLATS ink: header bars, tab and secondary-button blocks, the result summary; and text on it.")]
    public Color ink = Hex(0x1F2129);
    public Color onInk = Color.white;
    [Tooltip("Merits shown on an ink bar (the lighter mint of the original header).")] public Color meritOnInk = Hex(0x66F2DB);

    [Header("Text (contrast measured on surfaceRaised)")]
    public Color textPrimary = Color.white;
    public Color textSecondary = Hex(0xC3C6D2), textMuted = Hex(0x8E92A3);

    [Header("Meaning: buff green and debuff red keep their meaning in every theme")]
    public Color positive = Hex(0x3DDC84);
    public Color negative = Hex(0xFF5A6A), warning = Hex(0xFFB020), info = Hex(0x3FA9F5), merit = Hex(0x45E0C2);

    [Header("Item categories (the category colour blocks; unchanged from the original values)")]
    public Color stat = new Color(0.8f, 0.1f, 0.4f);
    public Color core = new Color(0.5f, 0.27f, 0.85f), mod = new Color(0.18f, 0.52f, 0.92f), tactical = new Color(0.08f, 0.6f, 0.48f),
        ultimate = new Color(0.9f, 0.58f, 0.06f), weapon = new Color(0.28f, 0.28f, 0.34f), supply = new Color(0.5f, 0.5f, 0.56f);

    [Header("Rarity (frames, corner marks and dots; unchanged from the original values)")]
    public Color rarityCommon = new Color(0.8f, 0.098f, 0.4f, 1f);
    public Color rarityUncommon = new Color(0.25f, 0.6f, 1f, 1f), rarityRare = new Color(1f, 0.7f, 0.1f, 1f);

    [Header("Type (canvas units of the screen that uses them)")]
    public TypeStyle display = new TypeStyle(56, 1f, 1f);
    public TypeStyle h1 = new TypeStyle(40, 1.05f, 1.05f), h2 = new TypeStyle(30, 1.1f, 1.1f), h3 = new TypeStyle(24, 1.15f, 1.15f),
        body = new TypeStyle(22, 1.15f, 1.25f), small = new TypeStyle(18, 1.15f, 1.25f), micro = new TypeStyle(16, 1.1f, 1.1f);

    [Header("Menu integration")]
    [Tooltip("While the mode's page or room is open, recolour the menu's shared tile materials (off: the page keeps the original FLATS tiles).")]
    public bool recolorMenuTiles;
    [Tooltip("While the mode's page or room is open, hide the menu's floating squares.")] public bool hideMenuCubes;
    [Tooltip("Recolour the shared confirmation dialog while it opens over the mode's page (off: the original dialog colours).")] public bool themeMenuDialogs;

    [Header("Motion (seconds, unscaled time)")]
    public float openDuration = 0.18f;
    public float closeDuration = 0.12f, staggerStep = 0.04f, countUpDuration = 0.4f;

    public const string RoguePath = "UI/Roguelike/RogueTheme";
    static FlatsUiTheme rogue;

    /// <summary>The Roguelike Survival theme (Resources/UI/Roguelike/RogueTheme). When the asset is missing the mode still gets its
    /// look from the same default values the authoring tool writes into a new asset.</summary>
    public static FlatsUiTheme Rogue
    {
        get
        {
            if (rogue != null) return rogue;
            rogue = Resources.Load<FlatsUiTheme>(RoguePath);
            if (rogue == null)
            {
                rogue = CreateInstance<FlatsUiTheme>();
                rogue.name = "RogueTheme (defaults)";
                rogue.hideFlags = HideFlags.DontSave;
                ApplyRogueDefaults(rogue);
            }
            return rogue;
        }
    }

    /// <summary>
    /// The Roguelike Survival values: the original FLATS vocabulary, made consistent (owner decision D-05, QA-54). Brand magenta
    /// #CC1964 for primary actions, selection and progress, with white text (5.4:1); the pale warm grey of the original headquarters
    /// as the backdrop, white cards and rows, ink #1F2129 for header bars, tab and secondary blocks and for text (15:1 on white); the
    /// original merit teal as the only secondary colour. Category and rarity colours are unchanged; buff green and debuff red keep
    /// their meaning, in shades readable on white.
    /// </summary>
    public static void ApplyRogueDefaults(FlatsUiTheme t)
    {
        t.brandPrimary = Hex(0xCC1964);
        t.brandHover = Hex(0xE0246F);
        t.brandPressed = Hex(0x9C0F4A);
        t.brandHot = Hex(0xFF2079);
        t.onBrand = Color.white;
        t.backdrop = Hex(0xF0E6E8);
        t.scrim = Hex(0x0B0C10, 0.45f);
        t.surfaceBase = Hex(0xF7F2F4, 0.98f);
        t.surfaceRaised = Color.white;
        t.surfaceRaisedHover = Hex(0xF6E7EE);
        t.surfaceSunken = Hex(0xE2DCDF);
        t.lineSubtle = Hex(0xD8D0D4);
        t.ink = Hex(0x1F2129);
        t.onInk = Color.white;
        t.meritOnInk = Hex(0x66F2DB);
        t.textPrimary = Hex(0x1F2129);
        t.textSecondary = Hex(0x5E6070);
        t.textMuted = Hex(0x8A8C99);
        t.positive = Hex(0x1E9E5A);
        t.negative = Hex(0xAD2B3D);
        t.warning = Hex(0xB7791F);
        t.info = Hex(0x2E85EB);
        t.merit = Hex(0x0D7A7A);
        t.recolorMenuTiles = false;
        t.hideMenuCubes = false;
        t.themeMenuDialogs = false;
    }

    public Color Get(Token token)
    {
        switch (token)
        {
            case Token.BrandPrimary: return brandPrimary;
            case Token.BrandHover: return brandHover;
            case Token.BrandPressed: return brandPressed;
            case Token.BrandHot: return brandHot;
            case Token.OnBrand: return onBrand;
            case Token.Backdrop: return backdrop;
            case Token.Scrim: return scrim;
            case Token.SurfaceBase: return surfaceBase;
            case Token.SurfaceRaised: return surfaceRaised;
            case Token.SurfaceRaisedHover: return surfaceRaisedHover;
            case Token.SurfaceSunken: return surfaceSunken;
            case Token.LineSubtle: return lineSubtle;
            case Token.Ink: return ink;
            case Token.OnInk: return onInk;
            case Token.MeritOnInk: return meritOnInk;
            case Token.TextPrimary: return textPrimary;
            case Token.TextSecondary: return textSecondary;
            case Token.TextMuted: return textMuted;
            case Token.Positive: return positive;
            case Token.Negative: return negative;
            case Token.Warning: return warning;
            case Token.Info: return info;
            case Token.Merit: return merit;
            case Token.Stat: return stat;
            case Token.Core: return core;
            case Token.Mod: return mod;
            case Token.Tactical: return tactical;
            case Token.Ultimate: return ultimate;
            case Token.Weapon: return weapon;
            case Token.Supply: return supply;
            case Token.RarityCommon: return rarityCommon;
            case Token.RarityUncommon: return rarityUncommon;
            case Token.RarityRare: return rarityRare;
            default: return Color.white;
        }
    }

    /// <summary>A category colour lightened for text or a chip label on a dark surface (the category block itself keeps the pure colour).</summary>
    public Color ChipText(Color category) { var c = Color.Lerp(category, textPrimary, 0.45f); c.a = 1f; return c; }

    /// <summary>A category colour at low opacity: the chip plate behind ChipText.</summary>
    public static Color ChipPlate(Color category) { return new Color(category.r, category.g, category.b, 0.22f); }

    /// <summary>The colour with another alpha.</summary>
    public static Color WithAlpha(Color c, float a) { c.a = a; return c; }

    /// <summary>Sets a button's interactable state and shows it at once. A freshly instantiated row starts in the template's enabled
    /// colour and the Color Tint fade then flashed an unaffordable Buy button bright for a moment before it greyed out.</summary>
    public static void SetInteractableNow(UnityEngine.UI.Selectable selectable, bool on)
    {
        if (selectable == null) return;
        bool changed = selectable.interactable != on;
        selectable.interactable = on;
        // OnEnable applies the current state instantly (no fade)
        if (changed && selectable.isActiveAndEnabled) { selectable.enabled = false; selectable.enabled = true; }
    }

    static Color Hex(int rgb, float a = 1f) { return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a); }
}
