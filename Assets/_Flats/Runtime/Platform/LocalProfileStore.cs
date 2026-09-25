using System;

namespace Flats.Profiles
{
    // Retains the existing atomic record, recovery, browser persistence and
    // legacy migration policies; this adapter does not introduce another copy.
    public sealed class LocalProfileStore : IProfileStore
    {
        public ProfilePayload Read()
        {
            if(!FlatsLocalProfile.Prepare())throw new InvalidOperationException("Local profile recovery is required before loading.");
            var authoritative=FlatsLocalProfile.ReadAuthoritative();
            return new ProfilePayload(authoritative==null?FlatsPreferences.GetString("characterData"):authoritative.character,
                authoritative==null?FlatsPreferences.GetString("settingsData"):authoritative.settings,
                authoritative==null?FlatsPreferences.GetString("currentData"):authoritative.current,
                FlatsPreferences.GetString("character"),FlatsPreferences.GetString("settings"),FlatsPreferences.GetString("current"));
        }
        public bool Write(ProfilePayload payload)
        {
            if(payload==null)throw new ArgumentNullException(nameof(payload));
            return FlatsLocalProfile.Commit(payload.Character,payload.Settings,payload.Current);
        }
    }
}
