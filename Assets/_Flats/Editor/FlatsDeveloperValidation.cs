using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FlatsDeveloperValidation
{
    [MenuItem("Flats/Validation/Validate project and run regression tests")]
    public static void Run()
    {
        Directory.CreateDirectory("Logs");
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        if(setup.Any(s=>s.isLoaded && EditorSceneManager.GetSceneByPath(s.path).isDirty))
            throw new InvalidOperationException("Save open scenes before validation.");
        var issues=new List<string>();
        try
        {
            foreach(var entry in EditorBuildSettings.scenes.Where(s=>s.enabled))
            {
                var scene=EditorSceneManager.OpenScene(entry.path,OpenSceneMode.Single);
                foreach(var root in scene.GetRootGameObjects()) Inspect(root,entry.path,issues);
            }
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab!=null) Inspect(prefab,path,issues);
            }
        }
        finally
        {
            if(setup.Any(s=>s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
        File.WriteAllLines("Logs/asset-validation.txt",issues);
        if(issues.Count>0)throw new Exception("Asset validation failed: "+issues.Count+" issues; see Logs/asset-validation.txt");
        FlatsModuleTests.Run();
        FlatsMultiplayerRecoveryChecks.Run();
        VerifyCatalogueConfiguration();
        Debug.Log("FLATS_DEVELOPER_VALIDATION_PASS");
    }
    static void Inspect(GameObject root,string path,List<string> issues)
    {
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)
                issues.Add(path+" :: "+t.name+" missing script");
            foreach(var component in t.GetComponents<Component>())
            {
                if(component==null)continue;
                var serialized=new SerializedObject(component);var property=serialized.GetIterator();
                while(property.Next(true))
                    if(property.propertyType==SerializedPropertyType.ObjectReference && property.objectReferenceValue==null && property.objectReferenceInstanceIDValue!=0)
                        issues.Add(path+" :: "+t.name+" :: "+property.propertyPath+" broken reference");
            }
        }
    }
    static void VerifyCatalogueConfiguration()
    {
        const string key="FLATS_MOD_CATALOGUE_URL"; var old=Environment.GetEnvironmentVariable(key);
        try {
            Environment.SetEnvironmentVariable(key,null);
            if(Flats.Modules.OfficialModEndpoint.Url!="")throw new Exception("Unconfigured catalogue must not use private infrastructure");
            Environment.SetEnvironmentVariable(key,"https://catalogue.example.org");
            if(Flats.Modules.OfficialModEndpoint.Url!="https://catalogue.example.org")throw new Exception("Catalogue configuration ignored");
        } finally {Environment.SetEnvironmentVariable(key,old);}
    }
}
