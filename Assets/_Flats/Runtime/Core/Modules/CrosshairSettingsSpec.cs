using System;
using System.Globalization;

namespace Flats.Modules
{
    // The crosshair@2 adapter contract: the settings the game can draw and the
    // built-in presets that replace the retired single-preset crosshair packages.
    public static class CrosshairSettingsSpec
    {
        public const string Adapter = "crosshair@2";
        public static readonly string[] Styles = { "cross", "dot", "ring", "crossdot", "t" };

        public static ModuleSettingSpec[] Specs() => new[]
        {
            new ModuleSettingSpec { id = "style", type = "choice", label = "Style", defaultValue = "cross", choices = (string[])Styles.Clone() },
            new ModuleSettingSpec { id = "size", type = "float", label = "Size", defaultValue = "24", min = 6, max = 64, step = 2 },
            new ModuleSettingSpec { id = "thickness", type = "float", label = "Thickness", defaultValue = "2", min = 1, max = 8, step = 0.5f },
            new ModuleSettingSpec { id = "gap", type = "float", label = "Gap", defaultValue = "3", min = 0, max = 16, step = 1 },
            new ModuleSettingSpec { id = "color", type = "color", label = "Color", defaultValue = "#ffffff" },
            new ModuleSettingSpec { id = "opacity", type = "float", label = "Opacity", defaultValue = "1", min = 0.2f, max = 1, step = 0.05f },
            new ModuleSettingSpec { id = "outline", type = "bool", label = "Outline", defaultValue = "false" },
            new ModuleSettingSpec { id = "outlineColor", type = "color", label = "Outline color", defaultValue = "#000000" },
        };

        public static ModulePreset[] Presets() => new[]
        {
            Preset("classic", "Classic", "cross", "24", "2", "3"),
            // Former example.precision-dot and example.wide-ring packages.
            Preset("precisionDot", "Precision Dot", "dot", "8", "1.5", "0"),
            Preset("wideRing", "Wide Ring", "ring", "36", "3", "0"),
        };

        static ModulePreset Preset(string id, string name, string style, string size, string thickness, string gap) => new ModulePreset
        {
            id = id, name = name, values = new[]
            {
                new SettingValue { id = "style", value = style }, new SettingValue { id = "size", value = size },
                new SettingValue { id = "thickness", value = thickness }, new SettingValue { id = "gap", value = gap },
            }
        };

        // Crosshair settings schema 1 stored only a style index (Cross, Dot, Ring) and a
        // size. The old renderer derived thickness as max(1.5, size/12) and the cross
        // gap as size/7; keep those so a migrated crosshair looks the same.
        public static SettingValue[] FromLegacy(int style, float size)
        {
            if (float.IsNaN(size) || float.IsInfinity(size)) size = 24;
            size = Math.Min(64, Math.Max(6, size));
            string name = style >= 0 && style < 3 ? Styles[style] : "cross";
            float thickness = Math.Max(1.5f, size / 12f);
            float gap = name == "cross" ? size / 7f : 0f;
            return ModuleSettingsSchema.Normalize(Specs(), new[]
            {
                new SettingValue { id = "style", value = name },
                new SettingValue { id = "size", value = size.ToString("R", CultureInfo.InvariantCulture) },
                new SettingValue { id = "thickness", value = thickness.ToString("R", CultureInfo.InvariantCulture) },
                new SettingValue { id = "gap", value = gap.ToString("R", CultureInfo.InvariantCulture) },
            });
        }

        // Map a retired preset package to the official module's equivalent preset.
        public static string PresetForRetiredPackage(string id)
        {
            switch (id)
            {
                case "example.precision-dot": return "precisionDot";
                case "example.wide-ring": return "wideRing";
                default: return null;
            }
        }
    }
}
