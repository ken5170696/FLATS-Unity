using Flats.Profiles;
using UnityEngine;

// Compatibility facade for serialized/legacy callers. Codec and storage receive
// explicit profile values and do not depend on Menu or scene objects.
public class SaveDataController : MonoBehaviour
{
    public static void Save()
    {
        Save(new ProfileSnapshot(Menu.myCharacter,Menu.mySettings,Menu.myCurrent),new LocalProfileStore());
    }
    public static bool Save(ProfileSnapshot snapshot,IProfileStore store)
    {
        if(store==null)throw new System.ArgumentNullException(nameof(store));
        return store.Write(LegacyProfileCodec.Encode(snapshot));
    }
    public static ProfileSnapshot Load(IProfileStore store)
    {
        if(store==null)throw new System.ArgumentNullException(nameof(store));
        return LegacyProfileCodec.Decode(store.Read());
    }
    public static void Load()
    {
        // Nothing is assigned until every payload has parsed successfully.
        var snapshot=Load(new LocalProfileStore());
        Menu.myCharacter=snapshot.Character;
        Menu.mySettings=snapshot.Settings;
        Menu.myCurrent=snapshot.Current;
        if(snapshot.LegacyCharacter!=null)
        {
            Menu.oldCharacter=snapshot.LegacyCharacter;
            Menu.oldCurrent=snapshot.LegacyCurrent;
            Menu.oldSettings=snapshot.LegacySettings;
        }
    }
}
