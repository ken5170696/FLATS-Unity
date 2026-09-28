using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Flats.Account
{
    // The cloud copy of a player's save. `export` is exactly the document that
    // FlatsSaveTransfer.ExportJson produces, so cloud data and file import share one
    // validation, backup and preference-acceptance path; no second save format exists.
    [Serializable]
    public sealed class CloudProfileDocument
    {
        public int schema = 1;
        public string savedAtUtc;   // DateTime "o" round-trip format
        public string gameVersion;
        public string platform;
        public string export;

        public static CloudProfileDocument Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            CloudProfileDocument document;
            try { document = JsonUtility.FromJson<CloudProfileDocument>(json); }
            catch (ArgumentException) { return null; }
            if (document == null || document.schema != 1 || string.IsNullOrEmpty(document.export) || string.IsNullOrEmpty(document.savedAtUtc)) return null;
            return document;
        }

        public DateTime? SavedAt
        {
            get
            {
                DateTime value;
                return DateTime.TryParseExact(savedAtUtc, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value)
                    ? value.ToUniversalTime() : (DateTime?)null;
            }
        }
    }

    public enum SyncAction { None, Upload, Download, AskPlayer }

    // What the player sees in a conflict dialog. Only fields the legacy character
    // record already exposes; nothing is invented.
    public sealed class ProfileSummary
    {
        public string Name = "";
        public int Kills, Deaths, SurvivalScore;
        public DateTime? SavedAtUtc;
        public int PreferenceCount;
    }

    public static class FlatsCloudSyncPolicy
    {
        // Decides what to do once both sides are known. `lastSyncedCloudStamp` is the
        // `savedAtUtc` this device last uploaded or downloaded for this player, and
        // `localDirty` is whether any save happened locally since that moment.
        // `accountChanged` is true when this device last synced with a different player:
        // the local save may belong to someone else, so it is never uploaded silently.
        // `localFresh` means the local save is the untouched first-run default: it has nothing
        // worth keeping, so an existing cloud save always wins without asking.
        public static SyncAction Decide(bool hasLocal, bool hasCloud, bool contentEqual, string lastSyncedCloudStamp, string cloudStamp, bool localDirty, bool accountChanged = false, bool localFresh = false)
        {
            if (!hasLocal && !hasCloud) return SyncAction.None;
            if (hasLocal && hasCloud && localFresh && !contentEqual) return SyncAction.Download;
            if (hasLocal && accountChanged && !contentEqual) return SyncAction.AskPlayer;
            if (hasLocal && !hasCloud) return SyncAction.Upload;
            if (!hasLocal && hasCloud) return SyncAction.Download;
            if (contentEqual) return SyncAction.None;
            // Never synced with this account on this device: the player decides.
            if (string.IsNullOrEmpty(lastSyncedCloudStamp)) return SyncAction.AskPlayer;
            bool cloudUnchanged = lastSyncedCloudStamp == cloudStamp;
            if (cloudUnchanged && localDirty) return SyncAction.Upload;
            if (!cloudUnchanged && !localDirty) return SyncAction.Download;
            return SyncAction.AskPlayer;
        }

        // Canonical form of an export document for equality: profile strings plus the
        // accepted preferences in key order. Formatting differences do not count.
        public static string Canonical(string exportJson)
        {
            FlatsSaveTransfer.Preference[] preferences;
            var profile = FlatsSaveTransfer.ReadJson(exportJson, out preferences);
            var builder = new StringBuilder();
            builder.Append(profile.character).Append('\u001f').Append(profile.settings).Append('\u001f').Append(profile.current);
            foreach (var preference in (preferences ?? new FlatsSaveTransfer.Preference[0]).OrderBy(p => p.key, StringComparer.Ordinal))
                builder.Append('\u001e').Append(preference.key).Append('=').Append(preference.kind).Append(':').Append(preference.value);
            return builder.ToString();
        }

        public static bool ContentEqual(string localExport, string cloudExport)
        {
            try { return Canonical(localExport) == Canonical(cloudExport); }
            catch (Exception) { return false; }
        }

        public static ProfileSummary Summarize(string exportJson, DateTime? savedAtUtc)
        {
            FlatsSaveTransfer.Preference[] preferences;
            var profile = FlatsSaveTransfer.ReadJson(exportJson, out preferences);
            var fields = profile.character.Split('$');
            var summary = new ProfileSummary { SavedAtUtc = savedAtUtc, PreferenceCount = preferences == null ? 0 : preferences.Length };
            if (fields.Length >= 14)
            {
                summary.Name = fields[1];
                int.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture, out summary.Kills);
                int.TryParse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture, out summary.Deaths);
                int.TryParse(fields[6], NumberStyles.None, CultureInfo.InvariantCulture, out summary.SurvivalScore);
            }
            return summary;
        }

        public static string Stamp(DateTime utc) => utc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

        public static string BuildDocument(string exportJson, DateTime utcNow, string gameVersion, string platform)
        {
            if (string.IsNullOrEmpty(exportJson)) throw new ArgumentException("Export is empty", nameof(exportJson));
            return JsonUtility.ToJson(new CloudProfileDocument
            {
                savedAtUtc = Stamp(utcNow), gameVersion = gameVersion ?? "", platform = platform ?? "", export = exportJson
            });
        }
    }
}
