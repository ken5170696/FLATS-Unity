using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Flats.Modules
{
    public interface IModJson
    {
        T Read<T>(string text);
        string Write<T>(T value);
    }

    [Serializable] public sealed class DependencySpec
    {
        public string id, minimum, maximum;
    }

    // Wire format is deliberately separate from the immutable runtime manifest.
    [Serializable] public sealed class PackageManifest
    {
        public int schema;
        public string id, version, name, author, description, category, changelog;
        public string gameMinimum, gameMaximum, apiMinimum, apiMaximum, scope;
        public string kind, assembly, entryType;
        public int crosshairStyle;
        public float crosshairSize;
        public DependencySpec[] dependencies;
        public string[] conflicts;
        // Schema 2: data packages target a game adapter; any kind may declare settings.
        public string adapter, payload;
        public ModuleSettingSpec[] settings;
        public ModulePreset[] presets;

        public const string OpenGameMinimum = "0.0.0", OpenGameMaximum = "1000.0.0";

        public ModuleManifest Validate()
        {
            if (schema != 1 && schema != 2) throw new InvalidDataException("Unsupported manifest schema (expected 1 or 2)");
            bool usesV2 = kind == "data" || !string.IsNullOrEmpty(adapter) || !string.IsNullOrEmpty(payload) ||
                (settings?.Length ?? 0) > 0 || (presets?.Length ?? 0) > 0;
            if (schema == 1 && usesV2) throw new InvalidDataException("Data packages, settings and presets require manifest schema 2");
            ModRules.Id(id);
            if(id=="staging" || id=="downloads" || id=="install-batch.json")throw new InvalidDataException("Module ID is reserved for internal storage");
            if (id.StartsWith("flats.", StringComparison.Ordinal)) throw new InvalidDataException("The flats.* namespace is reserved for built-in modules");
            ModRules.Version(version);
            ModRules.Text(name, 120, "name", true); ModRules.Text(author, 120, "author", true);
            ModRules.Text(description, 16000, "description"); ModRules.Text(changelog, 16000, "changelog");
            ModRules.Text(category, 40, "category", true);
            if (scope != "ClientOnly" && scope != "RequiredForSession") throw new InvalidDataException("Unknown module scope");
            if (kind != "crosshair" && kind != "managed" && kind != "data") throw new InvalidDataException("Unsupported package kind");
            if (kind == "data")
            {
                ModAdapters.Validate(adapter);
                if (!string.IsNullOrEmpty(payload))
                {
                    ModRules.RelativePath(payload);
                    if (!payload.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Data payloads must be JSON files");
                }
            }
            else if (!string.IsNullOrEmpty(adapter) || !string.IsNullOrEmpty(payload))
                throw new InvalidDataException("Only data packages declare an adapter or payload");
            // crosshair@2 presets are applied to the game's own crosshair settings, so they
            // are validated against that contract rather than against declared settings.
            if (kind == "data" && adapter == CrosshairSettingsSpec.Adapter)
            {
                ModuleSettingsSchema.Validate(settings, null);
                ModuleSettingsSchema.Validate(CrosshairSettingsSpec.Specs(), presets);
            }
            else ModuleSettingsSchema.Validate(settings, presets);
            if (kind == "crosshair" && (crosshairStyle < 0 || crosshairStyle > 2 || float.IsNaN(crosshairSize) || float.IsInfinity(crosshairSize) || crosshairSize < 6 || crosshairSize > 64 || scope != "ClientOnly"))
                throw new InvalidDataException("Crosshair packages require ClientOnly scope, style 0-2 and size 6-64");
            if (kind == "managed")
            {
                ModRules.RelativePath(assembly);
                if (!assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(entryType) || entryType.Length > 240)
                    throw new InvalidDataException("Managed packages require an assembly and entryType");
            }
            var deps = dependencies ?? new DependencySpec[0];
            var clashes = conflicts ?? new string[0];
            if (deps.Length > 64 || clashes.Length > 64) throw new InvalidDataException("Too many dependencies or conflicts");
            foreach (var d in deps) { if (d == null) throw new InvalidDataException("Null dependency"); ModRules.Id(d.id); }
            foreach (var c in clashes) ModRules.Id(c);
            if (deps.Select(d => d.id).Distinct().Count() != deps.Length || clashes.Distinct().Count() != clashes.Length)
                throw new InvalidDataException("Duplicate dependency or conflict");
            // Data packages depend on their adapter version, not on a game release, so they
            // may leave the game range open instead of being republished for every build.
            bool openGame = kind == "data" && string.IsNullOrEmpty(gameMinimum) && string.IsNullOrEmpty(gameMaximum);
            return new ModuleManifest(id, version, name, description ?? "", scope == "ClientOnly" ? ModuleScope.ClientOnly : ModuleScope.RequiredForSession,
                ModRules.Range(apiMinimum, apiMaximum),
                openGame ? ModRules.Range(OpenGameMinimum, OpenGameMaximum) : ModRules.Range(gameMinimum, gameMaximum),
                deps.Select(d => new ModuleDependency(d.id, ModRules.Range(d.minimum, d.maximum))).ToArray(), clashes);
        }
    }

    public static class ModRules
    {
        // API 1.1.0 adds manifest schema 2 (data packages, settings, presets); 1.x packages still load.
        public const string GameVersion = "5.4.5", ApiVersion = "1.1.0";
        public const long MaxArchive = 64L * 1024 * 1024, MaxExpanded = 256L * 1024 * 1024;
        public static void Id(string id)
        {
            if (id == null || !Regex.IsMatch(id, @"\A[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*\z") || id.Length > 80)
                throw new InvalidDataException("Invalid stable module ID (lowercase ASCII, digits, dots and hyphens)");
            RelativePath(id);
        }
        public static Version Version(string value)
        {
            if (value == null || !Regex.IsMatch(value, @"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z") || !System.Version.TryParse(value, out var parsed))
                throw new InvalidDataException("Versions must contain three numeric components: major.minor.patch");
            return parsed;
        }
        public static VersionRange Range(string minimum, string maximum)
        {
            if (Version(minimum) >= Version(maximum)) throw new InvalidDataException("Version range is empty or reversed");
            return new VersionRange(minimum, maximum);
        }
        public static void Text(string value, int limit, string name, bool required = false)
        {
            if ((required && string.IsNullOrWhiteSpace(value)) || (value != null && (value.Length > limit || value.Any(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t'))))
                throw new InvalidDataException("Invalid " + name);
        }
        public static void RelativePath(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 180 || value.StartsWith("/") || value.Contains("\\") || value.Any(c => c < 32 || ":*?\"<>|".Contains(c)))
                throw new InvalidDataException("Unsafe package path");
            foreach (var part in value.Split('/'))
            {
                var stem = part.Split('.')[0].ToUpperInvariant();
                if (part.Length == 0 || part == "." || part == ".." || part.EndsWith(".") || part.EndsWith(" ") || Regex.IsMatch(stem, @"\A(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])\z"))
                    throw new InvalidDataException("Unsafe or reserved package path");
            }
        }
        public static string Compatibility(PackageManifest value)
        {
            try
            {
                var m = value.Validate();
                if (!m.GameVersions.Contains(Version(GameVersion))) return "Requires game " + m.GameVersions;
                if (!m.ApiVersions.Contains(Version(ApiVersion))) return "Requires Mod API " + m.ApiVersions;
                if (value.kind == "data") return ModAdapters.Problem(value.adapter, m.Scope);
                return "";
            }
            catch (Exception e) { return e.Message; }
        }
        // official.* modules come only from the official source. A local ZIP must not be able to
        // take over an official identity such as the built-in crosshair binding.
        public static void RequireLocalImportable(PackageManifest m)
        {
            if (m?.id != null && m.id.StartsWith("official.", StringComparison.Ordinal))
                throw new InvalidDataException("official.* mods can only be installed from the official source");
        }
        // Legacy single-preset packages and crosshair@2 data packages both draw the HUD crosshair.
        public static bool IsCrosshairProvider(PackageManifest m) =>
            m != null && (m.kind == "crosshair" || (m.kind == "data" && m.adapter == CrosshairSettingsSpec.Adapter));
        public static Uri Url(string value, bool allowLoopback)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.UserInfo.Length > 0 || uri.Fragment.Length > 0 ||
                (uri.Scheme != "https" && !(allowLoopback && uri.Scheme == "http" && uri.IsLoopback)))
                throw new InvalidDataException("Source requires a public HTTPS URL (HTTP loopback is development-only)");
            return uri;
        }
        public static void Hash(string hash)
        {
            if (hash == null || !Regex.IsMatch(hash, @"\A[a-f0-9]{64}\z")) throw new InvalidDataException("A SHA-256 package digest is required");
        }
    }

    [Serializable] public sealed class CatalogItem
    {
        public PackageManifest manifest;
        public string downloadUrl, sha256, imageUrl, publishedUtc;
        public long bytes;
        public void Validate(bool loopback)
        {
            if (manifest == null) throw new InvalidDataException("Missing manifest");
            manifest.Validate(); ModRules.Url(downloadUrl, loopback); ModRules.Hash(sha256);
            if (bytes <= 0 || bytes > ModRules.MaxArchive) throw new InvalidDataException("Invalid archive size");
            if (!string.IsNullOrEmpty(imageUrl)) ModRules.Url(imageUrl, loopback);
        }
    }
    [Serializable] public sealed class CatalogPage
    {
        public int schema, total, offset;
        public CatalogItem[] items;
        public string[] categories;
    }
    public sealed class CatalogQuery
    {
        public string Search = "", Category = "", Sort = "name", Id = "";
        public bool AllVersions;
        public bool Compatible = true;
        public int Offset, Limit = 20;
    }
    [Serializable] public sealed class InstalledPackage
    {
        public int schema;
        public PackageManifest manifest;
        public string directory, sha256, source, imageUrl;
        public bool requested;
        public string Problem;
    }
}
