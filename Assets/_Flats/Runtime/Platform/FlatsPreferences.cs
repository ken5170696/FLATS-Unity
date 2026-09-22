using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// The normal save contract stays in PlayerPrefs. Explicit verification profiles use
// an independent durable key/value store and never import or modify the real player.
public static class FlatsPreferences
{
    [Serializable] sealed class Entry { public string key,value; }
    [Serializable] sealed class Document { public Entry[] entries; }
    static Dictionary<string,string> values;
    public static string IsolatedRoot
    {
        get
        {
#if UNITY_EDITOR
            var test=Environment.GetEnvironmentVariable("FLATS_MOD_TEST_ROOT");
            if(!string.IsNullOrEmpty(test))return Path.GetFullPath(test);
#endif
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-flats-module-settings-dir");
            return Array.IndexOf(args,"-flats-verify")>=0&&i>=0&&i+1<args.Length?Path.GetFullPath(args[i+1]):null;
        }
    }
    static string FilePath { get { return Path.Combine(IsolatedRoot,"verification-prefs.json"); } }
    static Dictionary<string,string> Values
    {
        get
        {
            if(values==null)
            {
                values=new Dictionary<string,string>();
                string recovery;
                var json=FlatsAtomicRecord.Read(FilePath,s=>{if(JsonUtility.FromJson<Document>(s)?.entries==null)throw new InvalidDataException("Invalid isolated preferences");},out recovery);
                if(json!=null)foreach(var e in JsonUtility.FromJson<Document>(json).entries)values.Add(e.key,e.value);
            }
            return values;
        }
    }
    public static bool HasKey(string key) { return IsolatedRoot==null?PlayerPrefs.HasKey(key):Values.ContainsKey(key); }
    public static string GetString(string key,string fallback="") { return IsolatedRoot==null?PlayerPrefs.GetString(key,fallback):Values.TryGetValue(key,out var value)?value:fallback; }
    public static int GetInt(string key,int fallback=0) { return IsolatedRoot==null?PlayerPrefs.GetInt(key,fallback):int.TryParse(GetString(key),out var value)?value:fallback; }
    public static void SetString(string key,string value) { if(IsolatedRoot==null)PlayerPrefs.SetString(key,value);else Values[key]=value; }
    public static void SetInt(string key,int value) { if(IsolatedRoot==null)PlayerPrefs.SetInt(key,value);else SetString(key,value.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
    public static void DeleteKey(string key) { if(IsolatedRoot==null)PlayerPrefs.DeleteKey(key);else Values.Remove(key); }
    public static void Save()
    {
        if(IsolatedRoot==null){PlayerPrefs.Save();return;}
        Directory.CreateDirectory(IsolatedRoot);
        var json=JsonUtility.ToJson(new Document{entries=Values.Select(p=>new Entry{key=p.Key,value=p.Value}).ToArray()});
        FlatsAtomicRecord.Write(FilePath,json,s=>{if(JsonUtility.FromJson<Document>(s)?.entries==null)throw new InvalidDataException("Invalid isolated preferences");});
    }
}
