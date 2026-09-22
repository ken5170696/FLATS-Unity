using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FlatsAudioVerification
{
    [Serializable] public class ClipCheck { public string owner,field,clip; public int samples,channels; public bool pass; }
    [Serializable] public class Report { public string scope="Serialized clip validity and decoder check; not speaker or spatial listening acceptance";public List<ClipCheck> checks=new List<ClipCheck>();public List<string> unused=new List<string>(); }
    public static void Run()
    {
        var report=new Report();
        var player=Resources.Load<GameObject>("Flatman");
        var prefabs=new[]{player,Resources.Load<GameObject>("Flatman_Enemy"),player.GetComponent<FPSController>().bullet.gameObject};
        foreach(var prefab in prefabs)
        {
            if(prefab==null)throw new Exception("Missing audio owner prefab");
            string name=prefab.name;
            foreach(var behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(!(behaviour is Gun || behaviour is FPSController || behaviour is AI || behaviour is Bullet || behaviour is DamageReceiver))continue;
                foreach(var field in behaviour.GetType().GetFields())
                {
                    if(field.FieldType!=typeof(AudioClip))continue;
                    if(behaviour is Bullet && field.Name=="hitWallSE"){report.unused.Add("Bullet.hitWallSE is unassigned and never read by the recovered controller; regular HitEffect has no AudioSource.");continue;}
                    var clip=field.GetValue(behaviour) as AudioClip;
                    bool loaded=clip!=null && clip.LoadAudioData();
                    report.checks.Add(new ClipCheck {owner=name+"/"+behaviour.name,field=field.Name,clip=clip==null?"MISSING":clip.name,samples=clip==null?0:clip.samples,channels=clip==null?0:clip.channels,pass=loaded && clip.samples>0 && clip.channels>0});
                }
            }
        }
        var explosion=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Flats/Prefabs/VFX/GrenadeHitEffect.prefab").GetComponent<AudioSource>();
        var explosionClip=explosion.clip;
        report.checks.Add(new ClipCheck {owner="GrenadeHitEffect",field="AudioSource.clip",clip=explosionClip==null?"MISSING":explosionClip.name,samples=explosionClip==null?0:explosionClip.samples,channels=explosionClip==null?0:explosionClip.channels,pass=explosionClip!=null && explosionClip.LoadAudioData() && explosionClip.samples>0 && explosion.playOnAwake});
        var glass=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Flats/Prefabs/VFX/BrokenGlass.prefab").GetComponent<AudioSource>();
        Add(report,"BrokenGlass","playOnAwake clip",glass==null?null:glass.clip);
        if(glass==null||!glass.playOnAwake)throw new Exception("Broken glass playback is not configured");
        EditorSceneManager.OpenScene("Assets/_Flats/Scenes/MainMenu.unity",OpenSceneMode.Single);
        foreach(var menu in UnityEngine.Object.FindObjectsByType<Menu>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
            Add(report,"Menu","pressSE",menu.pressSE);Add(report,"Menu","cancelSE",menu.cancelSE);
        }
        int locomotionEvents=0;
        foreach(var clip in player.GetComponent<Animator>().runtimeAnimatorController.animationClips)
            if(clip.name.IndexOf("walk",StringComparison.OrdinalIgnoreCase)>=0||clip.name.IndexOf("run",StringComparison.OrdinalIgnoreCase)>=0)
                locomotionEvents+=AnimationUtility.GetAnimationEvents(clip).Length;
        int namedFootClips=0;
        foreach(string guid in AssetDatabase.FindAssets("t:AudioClip")){
            string path=AssetDatabase.GUIDToAssetPath(guid);
            if(Path.GetFileName(path).IndexOf("foot",StringComparison.OrdinalIgnoreCase)>=0||Path.GetFileName(path).IndexOf("step",StringComparison.OrdinalIgnoreCase)>=0)namedFootClips++;
        }
        report.unused.Add("Footstep audit: named foot/step audio clips="+namedFootClips+", walk/run animation events="+locomotionEvents+". Runtime source and controller call-site audit is additionally required before NOT_APPLICABLE.");
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/parity-audio-assets.json")),JsonUtility.ToJson(report,true));
        bool pass=report.checks.Count>0 && report.checks.TrueForAll(c=>c.pass);
        Debug.Log("FLATS_AUDIO_ASSETS_"+(pass?"PASS":"FAIL")+" checks="+report.checks.Count);
        EditorApplication.Exit(pass?0:1);
    }
    static void Add(Report report,string owner,string field,AudioClip clip){
        report.checks.Add(new ClipCheck{owner=owner,field=field,clip=clip==null?"MISSING":clip.name,samples=clip==null?0:clip.samples,channels=clip==null?0:clip.channels,pass=clip!=null&&clip.LoadAudioData()&&clip.samples>0&&clip.channels>0});
    }
}
