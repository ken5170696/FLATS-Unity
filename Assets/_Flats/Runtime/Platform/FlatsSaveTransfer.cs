using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// Portable exports use the validated profile format. Older Unity playerprefs.dat
// files can be read explicitly, without changing the normal PlayerPrefs store.
public static class FlatsSaveTransfer
{
    public const int MaximumBytes = 1024 * 1024;
    public static FlatsLocalProfile.Profile Read(string path)
    {
        if (string.IsNullOrEmpty(path)) throw new InvalidDataException("Choose a save file.");
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > MaximumBytes) throw new InvalidDataException("Save file is missing or too large.");
        return ReadBytes(File.ReadAllBytes(path), info.Name);
    }

    public static FlatsLocalProfile.Profile ReadBytes(byte[] data, string filename)
    {
        if (data == null || data.Length == 0 || data.Length > MaximumBytes)
            throw new InvalidDataException("Save file is empty or too large (maximum 1 MiB).");
        FlatsLocalProfile.Profile profile;
        if (string.Equals(Path.GetExtension(filename), ".dat", StringComparison.OrdinalIgnoreCase))
        {
            if (data.Length < 8 || BitConverter.ToUInt32(data, 0) != 0xabfa22b1 || BitConverter.ToUInt32(data, 4) != data.Length)
                throw new InvalidDataException("Unsupported legacy PlayerPrefs file.");
            var fields = new Dictionary<string, string>();
            using (var reader = new BinaryReader(new MemoryStream(data), Encoding.UTF8))
            {
                reader.BaseStream.Position = 8;
                while (reader.BaseStream.Position < data.Length)
                {
                    if (reader.ReadByte() != (byte)'s') throw new InvalidDataException("Unsupported legacy field type.");
                    string key = ReadString(reader, data.Length);
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
        }
        else return ReadJson(new UTF8Encoding(false, true).GetString(data).TrimStart('\uFEFF'));
        FlatsLocalProfile.Validate(JsonUtility.ToJson(profile));
        return profile;
    }

    public static FlatsLocalProfile.Profile ReadJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaximumBytes)
            throw new InvalidDataException("Save JSON is empty or too large (maximum 1 MiB).");
        FlatsLocalProfile.Validate(json);
        return JsonUtility.FromJson<FlatsLocalProfile.Profile>(json);
    }

    public static string ExportJson()
    {
        string recovery;
        string json = FlatsAtomicRecord.Read(FlatsLocalProfile.FilePath, FlatsLocalProfile.Validate, out recovery);
        if (json == null) throw new InvalidDataException("No saved profile exists yet.");
        return JsonUtility.ToJson(ReadJson(json), true);
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

    public static void Import(FlatsLocalProfile.Profile profile)
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
    }
}
