using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Domain-reload-safe, opt-in editor lifecycle regression. Never uses a normal profile.
[InitializeOnLoad]
public static class FlatsPlayModeValidation
{
    const string Active="Flats.PlayValidation.Active", Count="Flats.PlayValidation.Count", Errors="Flats.PlayValidation.Errors";
    static double started;
    static double readyAt;
    static FlatsPlayModeValidation()
    {
        if(!SessionState.GetBool(Active,false))return;
        started=EditorApplication.timeSinceStartup;
        Application.logMessageReceived+=OnLog;
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=Changed;
    }
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLATS_MOD_TEST_ROOT")))
            throw new InvalidOperationException("Set FLATS_MOD_TEST_ROOT to an isolated test directory before launching Unity.");
        Directory.CreateDirectory(Environment.GetEnvironmentVariable("FLATS_MOD_TEST_ROOT"));
        Directory.CreateDirectory("Logs");
        SessionState.SetBool(Active,true);SessionState.SetInt(Count,0);SessionState.SetInt(Errors,0);
        EditorSceneManager.OpenScene("Assets/_Flats/Scenes/MainMenu.unity");
        var menu=UnityEngine.Object.FindFirstObjectByType<Menu>();
        foreach(var material in new[]{menu.mainUI,menu.selected})
        {
            var path=AssetDatabase.GetAssetPath(material);
            SessionState.SetString("Flats.PlayValidation.Material."+material.name,path+"|"+Hash(path));
        }
        // Let deferred editor startup/indexing finish before triggering a domain reload.
        readyAt=EditorApplication.timeSinceStartup+10;
        EditorApplication.update+=StartWhenReady;
    }
    static void StartWhenReady()
    {
        if(EditorApplication.timeSinceStartup<readyAt || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
        EditorApplication.update-=StartWhenReady;
        EditorApplication.isPlaying=true;
    }
    static void OnLog(string message,string stack,LogType type)
    {
        if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)
            SessionState.SetInt(Errors,SessionState.GetInt(Errors,0)+1);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Active,false))return;
        var elapsed=EditorApplication.timeSinceStartup-started;
        if(elapsed>90){Finish(false,"Timed out entering or leaving Play Mode");return;}
        if(!EditorApplication.isPlaying || elapsed<18)return;
        bool valid=Menu.current=="Main" && Flats.Modules.BuiltinModules.Instance!=null
            && UnityEngine.Object.FindObjectsByType<Flats.Modules.BuiltinModules>(FindObjectsSortMode.None).Length==1
            && SessionState.GetInt(Errors,0)==0;
        if(!valid){Finish(false,"MainMenu/module initialization or console error regression");return;}
        SessionState.SetInt(Count,SessionState.GetInt(Count,0)+1);
        EditorApplication.isPlaying=false;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredEditMode)return;
        if(SessionState.GetInt(Count,0)>=2)Finish(true,"Two Play Mode entries and exits completed");
        else EditorApplication.delayCall+=()=>EditorApplication.isPlaying=true;
    }
    static void Finish(bool pass,string detail)
    {
        AssetDatabase.SaveAssets();
        var menu=UnityEngine.Object.FindFirstObjectByType<Menu>();
        if(menu!=null && !EditorApplication.isPlaying)
            foreach(var material in new[]{menu.mainUI,menu.selected})
            {
                var record=SessionState.GetString("Flats.PlayValidation.Material."+material.name,"").Split('|');
                if(record.Length!=2 || Hash(record[0])!=record[1]){pass=false;detail="Play Mode modified a shared UI material asset";}
            }
        SessionState.SetBool(Active,false);
        File.WriteAllText("Logs/playmode-validation.txt",(pass?"PASS":"FAIL")+" "+detail+"; errors="+SessionState.GetInt(Errors,0));
        EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;EditorApplication.playModeStateChanged-=Changed;
        if(Application.isBatchMode)EditorApplication.Exit(pass?0:1);
        else if(!pass)Debug.LogError(detail);else Debug.Log("FLATS_PLAYMODE_VALIDATION_PASS");
    }
    static string Hash(string path)
    {
        using(var sha=System.Security.Cryptography.SHA256.Create())
            return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
    }
}
