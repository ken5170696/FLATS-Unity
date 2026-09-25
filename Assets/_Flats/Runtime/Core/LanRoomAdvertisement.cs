using System;
namespace Flats.Core
{
    // A discovery hint is never authority to join or trust a peer.
    public sealed class LanRoomAdvertisement
    {
        public const int MaximumBytes = 128;
        public string Room { get; private set; }
        public string Region { get; private set; }
        public string Version { get; private set; }
        public static bool IsRoomCode(string value)
        {
            if (value == null || value.Length != 20 || !value.StartsWith("lan-", StringComparison.Ordinal)) return false;
            for (int i = 4; i < value.Length; i++)
                if (!(value[i] >= '0' && value[i] <= '9') && !(value[i] >= 'a' && value[i] <= 'f')) return false;
            return true;
        }
        public static bool TryParse(string payload, out LanRoomAdvertisement result)
        {
            result = null;
            if (payload == null || payload.Length > MaximumBytes) return false;
            foreach (char c in payload) if (c < 32 || c > 126) return false;
            var parts = payload.Split('|');
            if (parts.Length != 4 || parts[0] != "FLATS-LAN1" || !IsRoomCode(parts[1])) return false;
            if (parts[2].Length < 2 || parts[2].Length > 16 || parts[3].Length < 1 || parts[3].Length > 16) return false;
            foreach (char c in parts[2]) if (!(c >= 'a' && c <= 'z')) return false;
            foreach (char c in parts[3]) if (!(c >= '0' && c <= '9') && c != '.') return false;
            result = new LanRoomAdvertisement { Room = parts[1], Region = parts[2], Version = parts[3] };
            return true;
        }
        public static string Encode(string room, string region, string version)
        {
            string payload = "FLATS-LAN1|" + room + "|" + region + "|" + version;
            LanRoomAdvertisement parsed;
            if (!TryParse(payload, out parsed)) throw new ArgumentException("Invalid LAN advertisement");
            return payload;
        }
    }
}
