using System;
using System.IO;
using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// Disk persistence for the mode. Separate files from the Classic profile, written through
/// FlatsAtomicRecord (temp file, checksum, backup, generations). A checkpoint is written only
/// at a consistent boundary (RunMachine.AtCheckpointBoundary). Failures surface as messages;
/// nothing is silently reset.
/// </summary>
public static class RogueSaveStore
{
    public static string RunPath { get { return Path.Combine(FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath, RogueSave.RunFileName); } }
    public static string MetaPath { get { return Path.Combine(FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath, RogueSave.MetaFileName); } }

    public static string LastError { get; private set; }
    public static string LastRecovery { get; private set; }

    public static string ToJson<T>(T value) { return JsonUtility.ToJson(value); }
    public static T FromJson<T>(string json) { return JsonUtility.FromJson<T>(json); }

    static void ValidateRunJson(string json)
    {
        var doc = JsonUtility.FromJson<RunSaveDocument>(json);
        if (doc == null) throw new InvalidDataException("empty run document");
        if (doc.schema > RunSaveDocument.CurrentSchema) throw new NotSupportedException("Run save schema " + doc.schema + " is newer than this build");
        var errors = RogueSave.Validate(doc);
        if (errors.Count > 0) throw new InvalidDataException(string.Join("; ", errors.ToArray()));
    }

    static void ValidateMetaJson(string json)
    {
        var doc = JsonUtility.FromJson<RogueMetaDocument>(json);
        if (doc == null) throw new InvalidDataException("empty meta document");
        if (doc.schema > RogueMetaDocument.CurrentSchema) throw new NotSupportedException("Meta schema " + doc.schema + " is newer than this build");
    }

    /// <summary>Writes a checkpoint. Returns false with LastError set when the write failed; the run keeps going.</summary>
    public static bool WriteCheckpoint(RunState run, string localPlayerKey)
    {
        LastError = null;
        try
        {
            var doc = new RunSaveDocument { run = run, localPlayerKey = localPlayerKey ?? "", savedAtUtc = DateTime.UtcNow.ToString("o"), gameVersion = Application.version };
            FlatsAtomicRecord.Write(RunPath, JsonUtility.ToJson(doc), ValidateRunJson);
            FlatsAtomicRecord.PruneGenerations(RunPath);
            return true;
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogWarning("FLATS_ROGUE_SAVE_FAILED " + e.Message);
            return false;
        }
    }

    /// <summary>Reads the checkpoint. Null when none exists or it is unusable; LastError explains why.</summary>
    public static RunSaveDocument ReadCheckpoint()
    {
        LastError = null; LastRecovery = null;
        try
        {
            string recovery;
            string json = FlatsAtomicRecord.Read(RunPath, ValidateRunJson, out recovery);
            LastRecovery = recovery;
            if (json == null) return null;
            return JsonUtility.FromJson<RunSaveDocument>(json);
        }
        catch (NotSupportedException e) { LastError = e.Message; return null; }
        catch (Exception e) { LastError = e.Message; Debug.LogWarning("FLATS_ROGUE_LOAD_FAILED " + e.Message); return null; }
    }

    public static bool HasCheckpoint() { return File.Exists(RunPath) || File.Exists(RunPath + ".bak"); }

    /// <summary>Removes the checkpoint after a run ends or the player abandons it. Backups are kept by the record layer.</summary>
    public static void ClearCheckpoint()
    {
        try
        {
            if (File.Exists(RunPath)) File.Delete(RunPath);
            if (File.Exists(RunPath + ".bak")) File.Delete(RunPath + ".bak");
            foreach (var g in Directory.GetFiles(Path.GetDirectoryName(RunPath), Path.GetFileName(RunPath) + ".generation-*"))
                try { File.Delete(g); } catch (IOException) { }
        }
        catch (Exception e) { Debug.LogWarning("FLATS_ROGUE_CLEAR_FAILED " + e.Message); }
    }

    public static RogueMetaDocument ReadMeta()
    {
        try
        {
            string recovery;
            string json = FlatsAtomicRecord.Read(MetaPath, ValidateMetaJson, out recovery);
            return json == null ? new RogueMetaDocument() : JsonUtility.FromJson<RogueMetaDocument>(json);
        }
        catch (Exception e) { Debug.LogWarning("FLATS_ROGUE_META_LOAD_FAILED " + e.Message); return new RogueMetaDocument(); }
    }

    public static bool WriteMeta(RogueMetaDocument meta)
    {
        try { FlatsAtomicRecord.Write(MetaPath, JsonUtility.ToJson(meta), ValidateMetaJson); FlatsAtomicRecord.PruneGenerations(MetaPath); return true; }
        catch (Exception e) { Debug.LogWarning("FLATS_ROGUE_META_SAVE_FAILED " + e.Message); return false; }
    }
}
