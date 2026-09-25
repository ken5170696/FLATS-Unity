using System;

namespace Flats.Profiles
{
    public sealed class ProfileSnapshot
    {
        public Character Character { get; }
        public Settings Settings { get; }
        public Current Current { get; }
        public string LegacyCharacter { get; }
        public string LegacySettings { get; }
        public string LegacyCurrent { get; }
        public ProfileSnapshot(Character character,Settings settings,Current current,string legacyCharacter=null,string legacySettings=null,string legacyCurrent=null)
        {
            Character=character ?? throw new ArgumentNullException(nameof(character));
            Settings=settings ?? throw new ArgumentNullException(nameof(settings));
            Current=current ?? throw new ArgumentNullException(nameof(current));
            LegacyCharacter=legacyCharacter;LegacySettings=legacySettings;LegacyCurrent=legacyCurrent;
        }
    }
    public sealed class ProfilePayload
    {
        public string Character { get; }
        public string Settings { get; }
        public string Current { get; }
        public string LegacyCharacter { get; }
        public string LegacySettings { get; }
        public string LegacyCurrent { get; }
        public ProfilePayload(string character,string settings,string current,string legacyCharacter=null,string legacySettings=null,string legacyCurrent=null)
        {
            Character=character;Settings=settings;Current=current;
            LegacyCharacter=legacyCharacter;LegacySettings=legacySettings;LegacyCurrent=legacyCurrent;
        }
    }
    public interface IProfileStore
    {
        ProfilePayload Read();
        bool Write(ProfilePayload payload);
    }
}
