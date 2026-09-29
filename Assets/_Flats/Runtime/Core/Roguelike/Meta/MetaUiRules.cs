using System;

namespace Flats.Core.Roguelike
{
    /// <summary>Validated headquarters commands. UI adapters never assign profile fields.</summary>
    public static class MetaUiRules
    {
        public const int MaxPresetNameLength = 24;
        public static MetaResult SelectPreset(MetaProfile p, int index)
        {
            if (p == null || p.presets == null || index < 0 || index >= p.presets.Length) return MetaResult.Fail("Unknown loadout");
            p.activePreset = index;
            return MetaResult.Success;
        }
        public static MetaResult RenamePreset(MetaProfile p, int index, string name)
        {
            if (p == null || p.presets == null || index < 0 || index >= p.presets.Length) return MetaResult.Fail("Unknown loadout");
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.Length > MaxPresetNameLength || Array.Exists(name.ToCharArray(), char.IsControl)) return MetaResult.Fail("Invalid loadout name");
            p.presets[index].name = name;
            return MetaResult.Success;
        }
        public static MetaResult SelectHeat(MetaProfile p, int heat)
        {
            if (p == null || heat < 0 || heat > RogueHeat.MaxHeat || heat > p.heatUnlocked) return MetaResult.Fail("Heat not unlocked");
            p.lastHeat = heat;
            return MetaResult.Success;
        }
        public static MetaResult RollChallenges(MetaProfile p, DateTime utc) { Challenges.Roll(p, utc); return MetaResult.Success; }
        public static MetaResult DismissTutorial(MetaProfile p, string key) { MetaProfiles.MarkTutorial(p, key); return MetaResult.Success; }
    }
}
