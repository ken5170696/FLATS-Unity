using System;
using System.IO;
using UnityEngine;

public static class FlatsLocalProfile
{
    [Serializable] public class Profile { public int schema=1; public string character,settings,current; }
    private static bool prepared,blocked;
    private static Profile pendingMirror;
    public static bool LastSaveSucceeded { get; private set; }
    public static string FilePath { get { return Path.Combine(FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath,"profile-v1.json"); } }
    public static void Validate(string json)
    {
        var p = JsonUtility.FromJson<Profile>(json);
        // Do not recover an older generation over a save written by a newer game.
        if (p != null && p.schema > 1) throw new NotSupportedException("Newer profile schema; refusing downgrade");
        if (p == null || p.schema != 1) throw new InvalidDataException("Unsupported profile format");
        if (p.character == null || p.settings == null || p.current == null) throw new InvalidDataException("Incomplete profile");
        var character = p.character.Split('$');
        if (character.Length != 14) throw new InvalidDataException("Invalid character field count");
        // GameInterface contains Color0..11 and one loadout entry per catalog weapon.
        RequireRange(character[3], 0, 11, "character color");
        for (int i = 4; i <= 8; i++) int.Parse(character[i]); // Preserve earned counters without an invented score cap.
        int weaponCount = Flats.Core.WeaponCatalog.Count;
        RequireRange(character[9], 0, weaponCount - 1, "primary weapon");
        RequireRange(character[10], 0, weaponCount - 1, "secondary weapon");
        int attack = RequireRange(character[11], 0, 10, "attack");
        int defense = RequireRange(character[12], 0, 10, "defense");
        if (attack + defense > 10) throw new InvalidDataException("Attack and defense exceed the 10 point allocation");
        var sights = character[13].Split(',');
        if (sights.Length < weaponCount) throw new InvalidDataException("Missing weapon sight selections");
        for (int i = 0; i < sights.Length; i++)
        {
            // Retain trailing legacy values, but prevent invalid sight dictionary indexes.
            int maximum = i < weaponCount ? Flats.Core.WeaponCatalog.GetDefault(i).zoom : 5;
            RequireRange(sights[i], 0, maximum, "weapon sight " + i);
        }
        var settings = p.settings.Split('$');
        if (settings.Length != 17) throw new InvalidDataException("Invalid settings field count");
        for (int i = 0; i < settings.Length; i++)
        {
            // Matches the menu's discrete option lists and PlusMinus limits.
            int maximum = i < 2 ? 10 : (i == 7 || i == 12 || i == 13 ? 2 : 1);
            RequireRange(settings[i], 0, maximum, "setting " + i);
        }
        var current = p.current.Split('$');
        if (current.Length != 6) throw new InvalidDataException("Invalid progress field count");
        foreach (var value in current) int.Parse(value);
    }

    static int RequireRange(string text, int minimum, int maximum, string field)
    {
        int value;
        if (!int.TryParse(text, out value) || value < minimum || value > maximum)
            throw new InvalidDataException("Invalid " + field + "; expected " + minimum + ".." + maximum);
        return value;
    }
    public static bool Prepare()
    {
        if (prepared)
        {
            if (!blocked) RetryPreferenceMirror();
            return !blocked;
        }
        prepared = true;
        try
        {
            string recovery;string json=FlatsAtomicRecord.Read(FilePath,Validate,out recovery);
            if(json==null)
            {
                bool any=FlatsPreferences.HasKey("characterData")||FlatsPreferences.HasKey("settingsData")||FlatsPreferences.HasKey("currentData");
                if(!any)return true; // The original first-run/version migration initializes new profiles.
                json=JsonUtility.ToJson(new Profile{character=FlatsPreferences.GetString("characterData"),settings=FlatsPreferences.GetString("settingsData"),current=FlatsPreferences.GetString("currentData")});
                // Preserve legacy values even when validation refuses migration.
                string legacy=Path.Combine(FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath,"legacy-profile-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+".json");
                using(var f=new FileStream(legacy,FileMode.CreateNew,FileAccess.Write))using(var w=new StreamWriter(f)){w.Write(json);w.Flush();f.Flush(true);}
                Validate(json);FlatsAtomicRecord.Write(FilePath,json,Validate);
            }
            pendingMirror = JsonUtility.FromJson<Profile>(json);
            RetryPreferenceMirror();
            if(recovery!=null)FlatsStorageNotice.Show(recovery,false);
            return true;
        }
        catch(Exception e) { blocked=true;FlatsStorageNotice.Show("Profile could not be loaded. No defaults were written.\n"+e.Message+"\n"+FilePath,true);return false; }
    }
    // Readers use the atomic record, not the best-effort legacy preference mirror.
    public static Profile ReadAuthoritative()
    {
        string recovery;
        string json = FlatsAtomicRecord.Read(FilePath, Validate, out recovery);
        return json == null ? null : JsonUtility.FromJson<Profile>(json);
    }

    static void RetryPreferenceMirror()
    {
        if (pendingMirror == null) return;
        try
        {
            FlatsPreferences.SetString("characterData", pendingMirror.character);
            FlatsPreferences.SetString("settingsData", pendingMirror.settings);
            FlatsPreferences.SetString("currentData", pendingMirror.current);
            FlatsPreferences.Save();
            pendingMirror = null;
        }
        catch (Exception error)
        {
            // The authoritative record remains valid; retry on the next load/save.
            FlatsStorageNotice.Show("Profile saved. Legacy preference copy could not be updated; it will be retried.\n" + error.Message, false);
        }
    }

    public static bool Commit(string character, string settings, string current)
    {
        LastSaveSucceeded = false;
        if (blocked) return false;
        var profile = new Profile { character = character, settings = settings, current = current };
        string json = JsonUtility.ToJson(profile);
        try { FlatsAtomicRecord.Write(FilePath, json, Validate); }
        catch (Exception error)
        {
            // A backup write can fail after the atomic replacement has committed.
            // Confirm the actual record before describing this as a failed save.
            bool committed = false;
            try
            {
                string recovery;
                committed = FlatsAtomicRecord.Read(FilePath, Validate, out recovery) == json;
            }
            catch (Exception) { }
            if (!committed)
            {
                FlatsStorageNotice.Show("Save could not be confirmed. Existing recovery records were retained.\n" + error.Message, false);
                return false;
            }
            FlatsStorageNotice.Show("Profile saved, but recovery-copy maintenance reported a problem.\n" + error.Message, false);
        }
        LastSaveSucceeded = true;
        pendingMirror = profile;
        RetryPreferenceMirror();
        return true;
    }

}

public class FlatsStorageNotice : MonoBehaviour
{
    private string message;private bool fatal;
    public static void Show(string text,bool stop)
    {
        Debug.LogWarning("FLATS_STORAGE "+text);
        var notice=UnityEngine.Object.FindObjectOfType<FlatsStorageNotice>();
        if(notice==null){var go=new GameObject("Storage recovery notice");UnityEngine.Object.DontDestroyOnLoad(go);notice=go.AddComponent<FlatsStorageNotice>();}
        notice.message=text;notice.fatal=stop;
    }
    private void OnGUI()
    {
        // Immediate-mode GUI bypasses FlatsLocalizedText. Translate here and use the
        // bundled Chinese font, which Web builds need because they have no OS fallback.
        // Exception messages and paths have no catalogue entry and stay as data.
        Font font = FlatsLocalization.IsChinese ? FlatsLocalization.ChineseFont : null;
        GUIStyle label = new GUIStyle(GUI.skin.label) { wordWrap = true }, button = new GUIStyle(GUI.skin.button);
        if (font != null) label.font = button.font = font;
        GUI.depth=-1000;GUILayout.BeginArea(new Rect(30,30,Mathf.Min(Screen.width-60,780),300),GUI.skin.box);
        GUILayout.Label(FlatsLocalization.Translate(message), label);
        if(fatal){if(GUILayout.Button(FlatsLocalization.Translate("Quit without overwriting data"), button))Application.Quit();}
        else if(GUILayout.Button(FlatsLocalization.Translate("Close"), button))Destroy(gameObject);
        GUILayout.EndArea();
    }
}
