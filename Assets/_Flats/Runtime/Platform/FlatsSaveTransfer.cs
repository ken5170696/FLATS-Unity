using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// Portable exports use the validated profile format. Older Unity playerprefs.dat
// files can be read explicitly, without changing the normal PlayerPrefs store.
public static class FlatsSaveTransfer
{
    public static FlatsLocalProfile.Profile Read(string path)
    {
        if (string.IsNullOrEmpty(path)) throw new InvalidDataException("Choose a save file.");
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > 1024 * 1024) throw new InvalidDataException("Save file is missing or too large.");
        FlatsLocalProfile.Profile profile;
        if (string.Equals(info.Extension, ".dat", StringComparison.OrdinalIgnoreCase))
        {
            var data = File.ReadAllBytes(path);
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
        else profile = JsonUtility.FromJson<FlatsLocalProfile.Profile>(File.ReadAllText(path, Encoding.UTF8));
        FlatsLocalProfile.Validate(JsonUtility.ToJson(profile));
        return profile;
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
        string source = FlatsLocalProfile.FilePath;
        var profile = Read(source);
        string folder = Path.Combine(FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath, "Exports");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "FLATS-save-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + ".json");
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        { writer.Write(JsonUtility.ToJson(profile, true)); writer.Flush(); stream.Flush(true); }
        return path;
    }

    public static void Import(FlatsLocalProfile.Profile profile)
    {
        string source = FlatsLocalProfile.FilePath;
        if (File.Exists(source))
        {
            string folder = Path.Combine(Path.GetDirectoryName(source), "Backups");
            Directory.CreateDirectory(folder);
            File.Copy(source, Path.Combine(folder, "before-import-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + ".json"));
        }
        if (!FlatsLocalProfile.Commit(profile.character, profile.settings, profile.current))
            throw new IOException("The save could not be imported. Current data was retained.");
    }
}
