using System;
using System.IO;
using UnityEngine;

public static class FlatsLocalProfile
{
    [Serializable] public class Profile { public int schema=1; public string character,settings,current; }
    private static bool prepared,blocked;
    public static bool LastSaveSucceeded { get; private set; }
    public static string FilePath { get { return Path.Combine(FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath,"profile-v1.json"); } }
    public static void Validate(string json)
    {
        var p=JsonUtility.FromJson<Profile>(json);
        if(p==null || p.schema!=1)throw new InvalidDataException("Unsupported profile format");
        if(p.character==null||p.settings==null||p.current==null)throw new InvalidDataException("Incomplete profile");
        var c=p.character.Split('$');if(c.Length!=14)throw new InvalidDataException("Invalid character field count");
        for(int i=3;i<13;i++)int.Parse(c[i]);
        foreach(var sight in c[13].Split(','))int.Parse(sight);
        var settings=p.settings.Split('$');if(settings.Length!=17)throw new InvalidDataException("Invalid settings field count");
        foreach(var value in settings)int.Parse(value);
        var current=p.current.Split('$');if(current.Length!=6)throw new InvalidDataException("Invalid progress field count");
        foreach(var value in current)int.Parse(value);
    }
    public static bool Prepare()
    {
        if(prepared)return !blocked;prepared=true;
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
            var profile=JsonUtility.FromJson<Profile>(json);
            FlatsPreferences.SetString("characterData",profile.character);FlatsPreferences.SetString("settingsData",profile.settings);FlatsPreferences.SetString("currentData",profile.current);
            if(recovery!=null)FlatsStorageNotice.Show(recovery,false);
            return true;
        }
        catch(Exception e) { blocked=true;FlatsStorageNotice.Show("Profile could not be loaded. No defaults were written.\n"+e.Message+"\n"+FilePath,true);return false; }
    }
    public static bool Commit(string character,string settings,string current)
    {
        LastSaveSucceeded=false;if(blocked)return false;
        try
        {
            string json=JsonUtility.ToJson(new Profile{character=character,settings=settings,current=current});
            FlatsAtomicRecord.Write(FilePath,json,Validate);
            FlatsPreferences.SetString("characterData",character);FlatsPreferences.SetString("settingsData",settings);FlatsPreferences.SetString("currentData",current);FlatsPreferences.Save();
            LastSaveSucceeded=true;return true;
        }
        catch(Exception e){FlatsStorageNotice.Show("Save failed; previous profile retained.\n"+e.Message,false);return false;}
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
        GUI.depth=-1000;GUILayout.BeginArea(new Rect(30,30,Mathf.Min(Screen.width-60,780),300),GUI.skin.box);
        GUILayout.Label(message);
        if(fatal){if(GUILayout.Button("Quit without overwriting data"))Application.Quit();}
        else if(GUILayout.Button("Close"))Destroy(gameObject);
        GUILayout.EndArea();
    }
}
