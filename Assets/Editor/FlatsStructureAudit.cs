using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Read-only structural baseline. Never saves a scene or exposes serialized values.</summary>
public static class FlatsStructureAudit
{
    [Serializable] public class Index { public string unity; public List<Asset> assets = new List<Asset>(); public List<SceneInfo> scenes = new List<SceneInfo>(); public List<AnimationInfo> animations = new List<AnimationInfo>(); }
    [Serializable] public class AnimationInfo {public string path;public List<string> bindings=new List<string>();public List<string> events=new List<string>();}
    [Serializable] public class Asset { public string path, guid, type; public string[] dependencies; public List<Sub> objects = new List<Sub>(); public List<Node> hierarchy = new List<Node>(); }
    [Serializable] public class Sub { public string name, type; public long id; }
    [Serializable] public class SceneInfo { public string path; public List<Node> hierarchy = new List<Node>(); }
    [Serializable] public class Node { public string path, name, prefab; public bool active, ui, canvas; public int missing; public List<Comp> components = new List<Comp>(); }
    [Serializable] public class Comp { public string type, id; public List<Ref> references = new List<Ref>(); public List<string> methods = new List<string>(); }
    [Serializable] public class Ref { public string property, target, asset, id; public bool missing; }
    public static string Output { get { var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, "-flats-index"); return i >= 0 ? a[i+1] : "Logs/refactor/current-index.json"; } }
    public static void RequireClean()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("Wait for compilation/import to finish before structural work.");
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Close prefab stage and stop play mode before migration.");
        for (int i=0; i<SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene: " + SceneManager.GetSceneAt(i).path);
    }
    [MenuItem("Flats/Structure/Export index")]
    public static void Run()
    {
        RequireClean(); var setup = EditorSceneManager.GetSceneManagerSetup(); var result = new Index { unity = Application.unityVersion };
        try
        {
            foreach (var p in AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(p)).OrderBy(p=>p))
            {
                var entry = new Asset {path=p, guid=AssetDatabase.AssetPathToGUID(p), type=AssetDatabase.GetMainAssetTypeAtPath(p)?.FullName, dependencies=AssetDatabase.GetDependencies(p, false)};
                foreach (var obj in (p.EndsWith(".unity") ? new UnityEngine.Object[0] : AssetDatabase.LoadAllAssetsAtPath(p)).Where(o=>o))
                {
                    string guid; long id; AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out id);
                    entry.objects.Add(new Sub {name=obj.name,type=obj.GetType().FullName,id=id});
                }
                if (p.EndsWith(".prefab")) Walk(AssetDatabase.LoadAssetAtPath<GameObject>(p), entry.hierarchy);
                if(p.EndsWith(".anim"))
                {
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(p);var animation=new AnimationInfo{path=p};
                    foreach(var binding in AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip)))animation.bindings.Add(binding.path+"|"+binding.type.FullName+"|"+binding.propertyName);
                    foreach(var ev in AnimationUtility.GetAnimationEvents(clip))animation.events.Add(ev.time.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"|"+ev.functionName);
                    result.animations.Add(animation);
                }
                result.assets.Add(entry);
            }
            foreach (var p in result.assets.Where(a=>a.path.EndsWith(".unity")).Select(a=>a.path))
            {
                var s=EditorSceneManager.OpenScene(p,OpenSceneMode.Single); var info=new SceneInfo{path=p};
                foreach(var root in s.GetRootGameObjects()) Walk(root, info.hierarchy);
                result.scenes.Add(info);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Output)); File.WriteAllText(Output,JsonUtility.ToJson(result,true));
            Debug.Log("FLATS_STRUCTURE_INDEX assets="+result.assets.Count+" scenes="+result.scenes.Count+" output="+Output);
        }
        finally { if(setup.Any(s=>s.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(setup); else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); }
    }
    static string PathOf(Transform t) { return t.parent ? PathOf(t.parent)+"/"+t.name : t.name; }
    static void Walk(GameObject root,List<Node> nodes)
    {
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            var n=new Node{path=PathOf(t),name=t.name,active=t.gameObject.activeSelf,ui=t is RectTransform,canvas=t.GetComponent<Canvas>()!=null,prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject),missing=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)};
            foreach(var c in t.GetComponents<Component>().Where(c=>c))
            {
                var item=new Comp{type=c.GetType().FullName,id=GlobalObjectId.GetGlobalObjectIdSlow(c).ToString()};
                using(var so=new SerializedObject(c))
                {
                    var it=so.GetIterator();
                    while(it.Next(true))
                    {
                        if(it.propertyType==SerializedPropertyType.ObjectReference)
                        {
                            var target=it.objectReferenceValue;
                            if(target || it.objectReferenceInstanceIDValue!=0) item.references.Add(new Ref{property=it.propertyPath,target=target is Component ? PathOf(((Component)target).transform) : target is GameObject ? PathOf(((GameObject)target).transform) : target ? target.name : "", asset=target ? AssetDatabase.GetAssetPath(target) : "", id=target ? GlobalObjectId.GetGlobalObjectIdSlow(target).ToString() : "",missing=!target});
                        }
                        else if(it.propertyType==SerializedPropertyType.String && it.propertyPath.EndsWith("m_MethodName") && !string.IsNullOrEmpty(it.stringValue)) item.methods.Add(it.propertyPath+"="+it.stringValue);
                    }
                }
                n.components.Add(item);
            }
            nodes.Add(n);
        }
    }
}
