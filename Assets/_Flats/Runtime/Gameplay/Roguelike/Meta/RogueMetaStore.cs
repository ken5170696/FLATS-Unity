using System;
using System.Collections.Generic;
using System.IO;
using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// Disk persistence of the out-of-run profile (schema 2, roguelike-profile-v2.json) through
/// FlatsAtomicRecord (temp file, checksum, backup, generations). The first load migrates the
/// schema-1 record file, which stays untouched on disk so an older build keeps working.
/// Every mutation the UI makes is saved immediately; a failed save is reported and the caller
/// rolls its screen back (the in-memory profile is only replaced after a successful write).
/// </summary>
public static class RogueMetaStore
{
    public static string ProfilePath { get { return Path.Combine(FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath, MetaProfiles.FileName); } }
    public static string LastError { get; private set; }
    public static List<string> LastRepairs { get; private set; }

    static MetaProfile cached;

    /// <summary>The current profile (loaded once per session, sanitized). Never null.</summary>
    public static MetaProfile Current
    {
        get { if (cached == null) cached = Load(); return cached; }
    }

    static void Validate(string json)
    {
        var p = JsonUtility.FromJson<MetaProfile>(json);
        if (p == null) throw new InvalidDataException("empty profile");
        if (p.schema > MetaProfile.CurrentSchema) throw new NotSupportedException("Profile schema " + p.schema + " is newer than this build");
        if (p.schema < 2) throw new InvalidDataException("profile schema " + p.schema);
    }

    public static MetaProfile Load()
    {
        LastError = null;
        MetaProfile p = null;
        try
        {
            string recovery;
            string json = FlatsAtomicRecord.Read(ProfilePath, Validate, out recovery);
            if (!string.IsNullOrEmpty(recovery)) Debug.LogWarning("FLATS_ROGUE_PROFILE_RECOVERED " + recovery);
            if (json != null) p = JsonUtility.FromJson<MetaProfile>(json);
        }
        catch (NotSupportedException e) { LastError = e.Message; Debug.LogWarning("FLATS_ROGUE_PROFILE_NEWER " + e.Message); }
        catch (Exception e) { LastError = e.Message; Debug.LogWarning("FLATS_ROGUE_PROFILE_LOAD_FAILED " + e.Message); }
        if (p == null && LastError == null)
        {
            // first run of this build: migrate the v1 records (they stay on disk as they are)
            p = MetaProfiles.Migrate(RogueSaveStore.ReadMeta());
            if (!Write(p)) Debug.LogWarning("FLATS_ROGUE_PROFILE_MIGRATION_UNSAVED " + LastError);
            else Debug.Log("FLATS_ROGUE_PROFILE_MIGRATED " + p.migratedFrom + " xp=" + p.xp);
        }
        if (p == null)
        {
            // unreadable or newer: play with a fresh in-memory profile but never overwrite the file
            p = MetaProfiles.CreateNew();
            readOnly = true;
        }
        LastRepairs = MetaProfiles.Sanitize(p);
        if (LastRepairs.Count > 0) Debug.LogWarning("FLATS_ROGUE_PROFILE_REPAIRED " + string.Join("; ", LastRepairs.ToArray()));
        return p;
    }

    static bool readOnly;
    public static bool ReadOnly { get { return readOnly; } }

    static bool Write(MetaProfile p)
    {
        LastError = null;
        if (readOnly) { LastError = "The profile on disk could not be read; progress is not saved this session."; return false; }
        try
        {
            FlatsAtomicRecord.Write(ProfilePath, JsonUtility.ToJson(p), Validate);
            FlatsAtomicRecord.PruneGenerations(ProfilePath);
            return true;
        }
        catch (Exception e) { LastError = e.Message; Debug.LogWarning("FLATS_ROGUE_PROFILE_SAVE_FAILED " + e.Message); return false; }
    }

    /// <summary>Saves a modified copy. On success it becomes the current profile; on failure the current one is unchanged.</summary>
    public static bool Commit(MetaProfile edited)
    {
        if (edited == null) return false;
        if (!Write(edited)) return false;
        cached = edited;
        return true;
    }

    /// <summary>A deep copy for screens that edit and then commit or discard.</summary>
    public static MetaProfile Copy(MetaProfile p) { return JsonUtility.FromJson<MetaProfile>(JsonUtility.ToJson(p)); }

    /// <summary>Run start: records the pending run id (crash detection) and the start counters, then saves.</summary>
    public static void NoteRunStarted(string runId)
    {
        var p = Copy(Current);
        MetaProfiles.NoteRunStarted(p, runId);
        Commit(p);
    }

    /// <summary>
    /// Run end: applies the reward exactly once and saves atomically. Returns the finished reward
    /// (with level-up, mastery and challenge lines), or null when this run was already rewarded.
    /// </summary>
    public static RunReward Reward(RunFacts facts)
    {
        var p = Copy(Current);
        var reward = MetaProgression.RunReward(facts);
        if (!MetaProfiles.ApplyRunReward(p, reward, facts, DateTime.UtcNow)) return null;
        if (!Commit(p)) { Debug.LogWarning("FLATS_ROGUE_REWARD_UNSAVED " + LastError); return reward; }
        Debug.Log("FLATS_ROGUE_REWARD run=" + reward.runId + " xp=" + reward.xp + " merits=" + reward.merits + " level=" + reward.levelBefore + "->" + reward.levelAfter);
        return reward;
    }

    /// <summary>Test and Editor hook: forget the cached profile so the next access reloads from disk.</summary>
    public static void ResetCache() { cached = null; readOnly = false; }
}
