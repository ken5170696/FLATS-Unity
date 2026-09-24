using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Flats.Modules
{
    // Declarative player settings (manifest schema 2). The module page renders them
    // generically, so a module with settings needs no module-specific UI code.
    [Serializable] public sealed class ModuleSettingSpec
    {
        public string id, type, label, description, defaultValue;
        public float min, max, step;
        public string[] choices;
    }
    [Serializable] public sealed class SettingValue { public string id, value; }
    [Serializable] public sealed class ModulePreset { public string id, name; public SettingValue[] values; }
    // Stored in ProfileModule.json for modules that declare settings.
    [Serializable] public sealed class SettingValuesDocument { public int schema = 2; public SettingValue[] values; }

    public static class ModuleSettingsSchema
    {
        public const int MaxSettings = 32, MaxPresets = 16, MaxChoices = 32;
        static readonly Regex SettingId = new Regex(@"\A[a-z][a-zA-Z0-9]{0,39}\z");
        static readonly Regex Color = new Regex(@"\A#[0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?\z");

        public static void Validate(ModuleSettingSpec[] settings, ModulePreset[] presets)
        {
            settings = settings ?? new ModuleSettingSpec[0];
            presets = presets ?? new ModulePreset[0];
            if (settings.Length > MaxSettings || presets.Length > MaxPresets) throw new InvalidDataException("Too many settings or presets");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in settings)
            {
                if (s == null || s.id == null || !SettingId.IsMatch(s.id) || !ids.Add(s.id)) throw new InvalidDataException("Invalid or duplicate setting id");
                ModRules.Text(s.label, 80, "setting label", true);
                ModRules.Text(s.description, 400, "setting description");
                switch (s.type)
                {
                    case "bool": case "color": break;
                    case "int": case "float":
                        if (!Finite(s.min) || !Finite(s.max) || !Finite(s.step) || s.min >= s.max || s.step <= 0)
                            throw new InvalidDataException("Numeric setting " + s.id + " needs min < max and step > 0");
                        if (s.type == "int" && (s.min != Math.Floor(s.min) || s.max != Math.Floor(s.max) || s.step != Math.Floor(s.step)))
                            throw new InvalidDataException("Integer setting " + s.id + " needs whole-number bounds");
                        break;
                    case "choice":
                        var choices = s.choices ?? new string[0];
                        if (choices.Length == 0 || choices.Length > MaxChoices || choices.Distinct(StringComparer.Ordinal).Count() != choices.Length ||
                            choices.Any(c => c == null || !SettingId.IsMatch(c)))
                            throw new InvalidDataException("Choice setting " + s.id + " needs 1-32 unique lowercase choices");
                        break;
                    default: throw new InvalidDataException("Unknown setting type for " + s.id);
                }
                if (!IsValid(s, s.defaultValue)) throw new InvalidDataException("Invalid default for setting " + s.id);
            }
            var presetIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in presets)
            {
                if (p == null || p.id == null || !SettingId.IsMatch(p.id) || !presetIds.Add(p.id)) throw new InvalidDataException("Invalid or duplicate preset id");
                ModRules.Text(p.name, 80, "preset name", true);
                var values = p.values ?? new SettingValue[0];
                if (values.Select(v => v?.id).Distinct(StringComparer.Ordinal).Count() != values.Length) throw new InvalidDataException("Duplicate value in preset " + p.id);
                foreach (var v in values)
                {
                    var spec = settings.FirstOrDefault(s => s.id == v?.id);
                    if (spec == null || !IsValid(spec, v.value)) throw new InvalidDataException("Preset " + p.id + " has an invalid value");
                }
            }
        }

        static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }

        public static bool IsValid(ModuleSettingSpec spec, string value)
        {
            if (value == null) return false;
            switch (spec.type)
            {
                case "bool": return value == "true" || value == "false";
                case "color": return Color.IsMatch(value);
                case "choice": return spec.choices != null && Array.IndexOf(spec.choices, value) >= 0;
                case "int":
                    return long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long i) && i >= spec.min && i <= spec.max;
                case "float":
                    return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && Finite(f) && f >= spec.min && f <= spec.max;
                default: return false;
            }
        }

        // Every declared setting gets exactly one value. Unknown ids are dropped, invalid
        // values fall back to the default and numbers are clamped and snapped to step, so
        // stale or edited stored settings can never break a module.
        public static SettingValue[] Normalize(ModuleSettingSpec[] settings, IEnumerable<SettingValue> stored)
        {
            var given = new Dictionary<string, string>(StringComparer.Ordinal);
            if (stored != null) foreach (var v in stored) if (v?.id != null && v.value != null) given[v.id] = v.value;
            return (settings ?? new ModuleSettingSpec[0]).Select(s =>
                new SettingValue { id = s.id, value = given.TryGetValue(s.id, out var value) ? Coerce(s, value) : s.defaultValue }).ToArray();
        }

        static string Coerce(ModuleSettingSpec spec, string value)
        {
            if (spec.type == "int" || spec.type == "float")
            {
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || double.IsNaN(number) || double.IsInfinity(number))
                    return spec.defaultValue;
                number = Math.Min(spec.max, Math.Max(spec.min, number));
                number = spec.min + Math.Round((number - spec.min) / spec.step) * spec.step;
                number = Math.Min(spec.max, number);
                return spec.type == "int"
                    ? ((long)Math.Round(number)).ToString(CultureInfo.InvariantCulture)
                    : ((float)number).ToString("R", CultureInfo.InvariantCulture);
            }
            if (spec.type == "color" && Color.IsMatch(value)) return value.ToLowerInvariant();
            return IsValid(spec, value) ? value : spec.defaultValue;
        }

        public static ModulePreset Preset(ModulePreset[] presets, string id) => presets?.FirstOrDefault(p => p?.id == id);

        // Applying a preset changes only the values it names.
        public static SettingValue[] Apply(ModuleSettingSpec[] settings, SettingValue[] current, ModulePreset preset)
        {
            var merged = (current ?? new SettingValue[0]).ToDictionary(v => v.id, v => v.value, StringComparer.Ordinal);
            foreach (var v in preset?.values ?? new SettingValue[0]) merged[v.id] = v.value;
            return Normalize(settings, merged.Select(p => new SettingValue { id = p.Key, value = p.Value }));
        }

        public static string Get(SettingValue[] values, string id) => values?.FirstOrDefault(v => v.id == id)?.value;
        public static bool GetBool(SettingValue[] values, string id) => Get(values, id) == "true";
        public static float GetFloat(SettingValue[] values, string id) =>
            float.TryParse(Get(values, id), NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f;
        // RGBA bytes packed as 0xRRGGBBAA; alpha defaults to 0xFF.
        public static uint GetColor(SettingValue[] values, string id)
        {
            string v = Get(values, id);
            if (v == null || !Color.IsMatch(v)) return 0xFFFFFFFFu;
            uint rgb = uint.Parse(v.Substring(1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            uint alpha = v.Length == 9 ? uint.Parse(v.Substring(7, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) : 0xFFu;
            return (rgb << 8) | alpha;
        }
    }

    // Extension points a data package can target. The game registers what it
    // implements at startup; an unregistered adapter makes a package incompatible
    // instead of failing at runtime.
    public static class ModAdapters
    {
        static readonly Regex Name = new Regex(@"\A([a-z][a-z0-9]*(?:[.-][a-z0-9]+)*)@([1-9][0-9]{0,3})\z");
        static readonly Dictionary<string, ModuleScope[]> registered = new Dictionary<string, ModuleScope[]>(StringComparer.Ordinal);

        public static void Validate(string adapter)
        {
            if (adapter == null || !Name.IsMatch(adapter)) throw new InvalidDataException("Data packages need an adapter such as crosshair@2");
        }
        public static void Register(string adapter, params ModuleScope[] scopes)
        {
            Validate(adapter);
            if (scopes == null || scopes.Length == 0) throw new ArgumentException("An adapter must allow at least one scope");
            lock (registered) registered[adapter] = (ModuleScope[])scopes.Clone();
        }
        public static void Clear() { lock (registered) registered.Clear(); }
        // Empty when compatible, otherwise a player-facing reason.
        public static string Problem(string adapter, ModuleScope scope)
        {
            ModuleScope[] scopes;
            lock (registered) if (!registered.TryGetValue(adapter ?? "", out scopes)) return "Requires game feature " + adapter;
            return Array.IndexOf(scopes, scope) >= 0 ? "" : "Unsupported scope for " + adapter;
        }
    }
}
