using System;
using System.Globalization;

namespace Flats.Core
{
    /// <summary>Legacy LAN sync carries identity and scores, never settings or executable content.</summary>
    public sealed class LanSyncRecord
    {
        public const int MaximumBytes = 160;
        public string Id { get; private set; }
        public int Kills { get; private set; }
        public int Deaths { get; private set; }
        public int Survival { get; private set; }
        public int Assortment { get; private set; }
        public int Headshot { get; private set; }

        public static bool TryParse(string payload, out LanSyncRecord record)
        {
            record = null;
            if (string.IsNullOrEmpty(payload) || payload.Length > MaximumBytes) return false;
            var fields = payload.Split('$');
            if (fields.Length != 6 || fields[0].Length < 1 || fields[0].Length > 64) return false;
            foreach (char c in fields[0])
                if (!(c >= '0' && c <= '9') && !(c >= 'A' && c <= 'Z') &&
                    !(c >= 'a' && c <= 'z') && c != '-' && c != '_') return false;
            var values = new int[5];
            for (int i = 0; i < values.Length; i++)
                if (!int.TryParse(fields[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out values[i]))
                    return false;
            record = new LanSyncRecord { Id = fields[0], Kills = values[0], Deaths = values[1],
                Survival = values[2], Assortment = values[3], Headshot = values[4] };
            return true;
        }

        public static string Encode(string id, int kills, int deaths, int survival, int assortment, int headshot)
        {
            string payload = string.Join("$", id, kills.ToString(CultureInfo.InvariantCulture),
                deaths.ToString(CultureInfo.InvariantCulture), survival.ToString(CultureInfo.InvariantCulture),
                assortment.ToString(CultureInfo.InvariantCulture), headshot.ToString(CultureInfo.InvariantCulture));
            LanSyncRecord parsed;
            if (!TryParse(payload, out parsed)) throw new ArgumentException("Identity or score data is invalid for LAN sync.");
            return payload;
        }
    }
}
