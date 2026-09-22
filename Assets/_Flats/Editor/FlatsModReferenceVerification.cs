using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Read-only current-state gate. Does not rewrite historical refactor baselines.
public static class FlatsModReferenceVerification
{
    [Serializable] sealed class Report
    {
        public string utc, status;
        public int scenes, prefabs, components, missingScripts, missingReferences;
        public List<string> failures = new List<string>();
    }
    [MenuItem("Flats/Modules/Verify scene and prefab references")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before reference verification.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save or discard scene edits before verification.");
        var report = new Report { utc = DateTime.UtcNow.ToString("o") };
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Flats", "Assets/Resources" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try { Inspect(root, path, report); report.prefabs++; }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled) continue;
            var scene = SceneManager.GetSceneByPath(entry.path);
            bool owned = !scene.isLoaded;
            try
            {
                if (owned) scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Additive);
                foreach (var root in scene.GetRootGameObjects()) Inspect(root, entry.path, report);
                report.scenes++;
            }
            finally { if (owned && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
        }
        report.status = report.failures.Count == 0 ? "PASS" : "FAIL";
        string output = Path.GetFullPath("Logs/mod-center/reference-tests.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
        if (report.failures.Count > 0) throw new InvalidOperationException("Reference failures: " + report.failures.Count + "; " + output);
        Debug.Log("MOD_REFERENCE_PASS scenes=" + report.scenes + " prefabs=" + report.prefabs);
    }
    static void Inspect(GameObject root, string path, Report report)
    {
        foreach (var node in root.GetComponentsInChildren<Transform>(true))
        {
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject);
            report.missingScripts += missing;
            if (missing > 0) report.failures.Add(path + ": " + node.name + " missing scripts=" + missing);
            foreach (var component in node.GetComponents<Component>())
            {
                if (!component) continue;
                report.components++;
                using (var serialized = new SerializedObject(component))
                {
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0) continue;
                        report.missingReferences++;
                        report.failures.Add(path + ": " + node.name + "/" + component.GetType().Name + "/" + property.propertyPath);
                    }
                }
            }
        }
    }
}
