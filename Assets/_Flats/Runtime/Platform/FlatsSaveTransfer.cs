using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

// Portable exports use the validated profile format. Older Unity playerprefs.dat
// files can be read explicitly, without changing the normal PlayerPrefs store.
public static class FlatsSaveTransfer
{
    public const int MaximumBytes = 1024 * 1024;
    public static FlatsLocalProfile.Profile Read(string path) => Read(path, out _);

    public static FlatsLocalProfile.Profile Read(string path, out Preference[] preferences)
    {
        if (string.IsNullOrEmpty(path)) throw new InvalidDataException("Choose a save file.");
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > MaximumBytes) throw new InvalidDataException("Save file is missing or too large.");
        return ReadBytes(File.ReadAllBytes(path), info.Name, out preferences);
    }

    public static FlatsLocalProfile.Profile ReadBytes(byte[] data, string filename) => ReadBytes(data, filename, out _);

    public static FlatsLocalProfile.Profile ReadBytes(byte[] data, string filename, out Preference[] preferences)
    {
        if (data == null || data.Length == 0 || data.Length > MaximumBytes)
            throw new InvalidDataException("Save file is empty or too large (maximum 1 MiB).");
        FlatsLocalProfile.Profile profile;
        if (string.Equals(Path.GetExtension(filename), ".dat", StringComparison.OrdinalIgnoreCase))
        {
            // The header is a CRC-32 of the rest of the file followed by the total
            // length; it is not a fixed magic number. Every save has its own value.
            if (data.Length < 8 || BitConverter.ToUInt32(data, 4) != data.Length || BitConverter.ToUInt32(data, 0) != Crc32(data, 4))
                throw new InvalidDataException("Unsupported legacy PlayerPrefs file.");
            var fields = new Dictionary<string, string>();
            using (var reader = new BinaryReader(new MemoryStream(data), Encoding.UTF8))
            {
                reader.BaseStream.Position = 8;
                while (reader.BaseStream.Position < data.Length)
                {
                    byte type = reader.ReadByte();
                    string key = ReadString(reader, data.Length);
                    if (type == (byte)'i' || type == (byte)'f')
                    {
                        // Older games also stored int/float preferences. Only the string
                        // profile records are imported; skip the 4-byte value.
                        if (data.Length - reader.BaseStream.Position < 4) throw new InvalidDataException("Truncated legacy save.");
                        reader.ReadBytes(4);
                        continue;
                    }
                    if (type != (byte)'s') throw new InvalidDataException("Unsupported legacy field type.");
                    string value = ReadString(reader, data.Length);
                    if (fields.ContainsKey(key)) throw new InvalidDataException("Duplicate legacy field.");
                    fields.Add(key, value);
                }
            }
            if (!fields.TryGetValue("characterData", out var character) ||
                !fields.TryGetValue("settingsData", out var settings) ||
                !fields.TryGetValue("currentData", out var current))
                throw new InvalidDataException("Legacy save has no complete game profile.");
            profile = new FlatsLocalProfile.Profile { character = character, settings = settings, current = current };
            // The original game kept its controller and touch layouts beside the profile.
            preferences = Accepted(fields.Where(f => f.Key == "controllermapping" || f.Key == "touchmapping")
                .Select(f => new Preference { key = f.Key, kind = "string", value = f.Value }));
        }
        else return ReadJson(new UTF8Encoding(false, true).GetString(data).TrimStart('\uFEFF'), out preferences);
        FlatsLocalProfile.Validate(JsonUtility.ToJson(profile));
        return profile;
    }

    public static FlatsLocalProfile.Profile ReadJson(string json) => ReadJson(json, out _);

    public static FlatsLocalProfile.Profile ReadJson(string json, out Preference[] preferences)
    {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaximumBytes)
            throw new InvalidDataException("Save JSON is empty or too large (maximum 1 MiB).");
        FlatsLocalProfile.Validate(json);
        // Exports older than preferences simply have none; unknown keys are ignored.
        preferences = Accepted(JsonUtility.FromJson<PreferenceDocument>(json)?.preferences);
        return JsonUtility.FromJson<FlatsLocalProfile.Profile>(json);
    }

    public static string ExportJson()
    {
        string recovery;
        string json = FlatsAtomicRecord.Read(FlatsLocalProfile.FilePath, FlatsLocalProfile.Validate, out recovery);
        if (json == null) throw new InvalidDataException("No saved profile exists yet.");
        var profile = ReadJson(json);
        return JsonUtility.ToJson(new ExportDocument
        {
            schema = profile.schema, character = profile.character, settings = profile.settings,
            current = profile.current, preferences = CapturePreferences()
        }, true);
    }

    // IEEE CRC-32 (the zlib polynomial) over data[offset..].
    static uint Crc32(byte[] data, int offset)
    {
        uint crc = 0xFFFFFFFF;
        for (int i = offset; i < data.Length; i++)
        {
            crc ^= data[i];
            for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ (0xEDB88320u & (uint)-(int)(crc & 1));
        }
        return ~crc;
    }

    static string ReadString(BinaryReader reader, int total)
    {
        if (total - reader.BaseStream.Position < 4) throw new InvalidDataException("Truncated legacy save.");
        uint length = reader.ReadUInt32();
        if (length > total - reader.BaseStream.Position) throw new InvalidDataException("Truncated legacy save.");
        return Encoding.UTF8.GetString(reader.ReadBytes((int)length));
    }

    public static string Export()
    {
        string json = ExportJson();
        string folder = Path.Combine(FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath, "Exports");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "FLATS-save-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + ".json");
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        { writer.Write(json); writer.Flush(); stream.Flush(true); }
        return path;
    }

    public static void Import(FlatsLocalProfile.Profile profile, Preference[] preferences = null)
    {
        if(profile==null)throw new ArgumentNullException(nameof(profile));
        FlatsLocalProfile.Validate(JsonUtility.ToJson(profile));
        string source = FlatsLocalProfile.FilePath;
        var previous=FlatsLocalProfile.ReadAuthoritative();
        if (previous!=null)
        {
            string folder = Path.Combine(Path.GetDirectoryName(source), "Backups");
            Directory.CreateDirectory(folder);
            string backup=Path.Combine(folder,"before-import-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+"-"+Guid.NewGuid().ToString("N")+".json");
            // Preserve the existing atomic-envelope backup format, but capture the
            // authoritative generation rather than an obsolete/missing base file.
            FlatsAtomicRecord.Write(backup,JsonUtility.ToJson(previous),FlatsLocalProfile.Validate);
        }
        if (!FlatsLocalProfile.Commit(profile.character, profile.settings, profile.current))
            throw new IOException("The save could not be imported. Current data was retained.");
        ApplyPreferences(preferences);
    }

    // Personal settings travel with the profile as an optional list, so exports stay
    // readable by builds that only know the profile fields. Device-specific display
    // settings (resolution, window mode, VSync) are deliberately left out.
    [Serializable] public sealed class Preference { public string key, kind, value; }
    [Serializable] sealed class PreferenceDocument { public Preference[] preferences; }
    [Serializable] sealed class ExportDocument
    {
        public int schema;
        public string character, settings, current;
        public Preference[] preferences;
    }

    const string LanguageKey = "ui.language", CrosshairColorKey = "Flats.Desktop.CrosshairColor",
        CrosshairSizeKey = "Flats.Desktop.CrosshairSize";

    static IEnumerable<string> StringKeys()
    {
        yield return LanguageKey;
        foreach (string action in FlatsControls.KeyboardActions) yield return "controls.v1.key." + action;
        foreach (string action in FlatsControls.PadActions) yield return "controls.v1.pad." + action;
        yield return FlatsControls.AimModeKey;
        yield return FlatsControls.AimSensitivityKey;
        yield return FlatsControls.KillCinematicKey;
        foreach (string key in FlatsGamepad.Keys) yield return key;
        yield return "controllermapping";
        yield return "touchmapping";
    }

    public static Preference[] CapturePreferences()
    {
        var list = new List<Preference>();
        // The language is exported as shown, even if it was never chosen explicitly.
        list.Add(new Preference { key = LanguageKey, kind = "string", value = FlatsLocalization.Language });
        foreach (string key in StringKeys())
            if (key != LanguageKey && FlatsPreferences.HasKey(key)) list.Add(new Preference { key = key, kind = "string", value = FlatsPreferences.GetString(key) });
        foreach (string key in new[] { CrosshairColorKey, CrosshairSizeKey })
            if (FlatsPreferences.HasKey(key))
                list.Add(new Preference { key = key, kind = "int", value = FlatsPreferences.GetInt(key).ToString(CultureInfo.InvariantCulture) });
        return Accepted(list);
    }

    static Preference[] Accepted(IEnumerable<Preference> source)
    {
        // Later entries win; the result is bounded by the number of known keys.
        var result = new Dictionary<string, Preference>();
        if (source != null) foreach (var entry in source) if (Accept(entry)) result[entry.key] = entry;
        return result.Values.ToArray();
    }

    static bool Accept(Preference entry)
    {
        if (entry == null || entry.key == null || entry.value == null || entry.value.Length > 512) return false;
        string key = entry.key, value = entry.value;
        if (key == CrosshairColorKey || key == CrosshairSizeKey)
            return entry.kind == "int" && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) &&
                   number <= (key == CrosshairColorKey ? 5 : 3);
        if (entry.kind != "string") return false;
        if (key == LanguageKey) return value == "en" || value == "zh-Hant";
        if (key == FlatsControls.AimModeKey) return value == "hold" || value == "toggle";
        if (key == FlatsControls.KillCinematicKey) return value == "on" || value == "off";
        if (Array.IndexOf(FlatsGamepad.Keys, key) >= 0) return FlatsGamepad.Valid(key, value);
        if (key == FlatsControls.AimSensitivityKey)
            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int level) && level < FlatsControls.AimSensitivities.Length;
        if (key.StartsWith("controls.v1.key.", StringComparison.Ordinal))
            return Array.IndexOf(FlatsControls.KeyboardActions, key.Substring(16)) >= 0 && IsEnumName<KeyCode>(value);
        if (key.StartsWith("controls.v1.pad.", StringComparison.Ordinal))
            return Array.IndexOf(FlatsControls.PadActions, key.Substring(16)) >= 0 && IsEnumName<InControl.InputControlType>(value);
        // Menu parses these at startup without guarding, so only well-formed values pass.
        if (key == "controllermapping") return value.Split('$').Length == 7;
        if (key == "touchmapping")
        {
            var parts = value.Split('$');
            // The menu stores and normalises layouts with invariant numbers.
            return (parts.Length == 8 || parts.Length == 12) && parts.All(part =>
                float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && !float.IsNaN(v) && !float.IsInfinity(v));
        }
        return false;
    }

    // Enum.TryParse also accepts numbers; only named values are valid bindings.
    static bool IsEnumName<T>(string value) where T : struct
        => value.Length > 0 && !char.IsDigit(value[0]) && value[0] != '-' && Enum.TryParse(value, false, out T _);

    static void ApplyPreferences(Preference[] preferences)
    {
        if (preferences == null) return;
        foreach (var entry in Accepted(preferences))
        {
            if (entry.key == LanguageKey) FlatsLocalization.SetLanguage(entry.value);
            else if (entry.kind == "int") FlatsPreferences.SetInt(entry.key, int.Parse(entry.value, CultureInfo.InvariantCulture));
            else FlatsPreferences.SetString(entry.key, entry.value);
        }
        FlatsPreferences.Save();
        FlatsControls.ReloadAimSensitivity();
        FlatsGamepad.Reload();
    }
}
