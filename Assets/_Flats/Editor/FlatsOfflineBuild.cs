using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
#if UNITY_2018_1_OR_NEWER
using UnityEditor.Build.Reporting;
#endif

public static class FlatsOfflineBuild
{
    [MenuItem("Flats Recovery/Build Windows Offline")]
    public static void BuildWindows()
    {
        var previousCompany=PlayerSettings.companyName;
        var previousProduct=PlayerSettings.productName;
        var previousBackground=PlayerSettings.runInBackground;
        var previousWidth=PlayerSettings.defaultScreenWidth;
        var previousHeight=PlayerSettings.defaultScreenHeight;
        var previousScenes=EditorBuildSettings.scenes;
        var previousBackend=PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
        try
        {
            PlayerSettings.companyName = "FlatsRecovery";
            PlayerSettings.productName = "Flats Offline";
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            string[] scenes = {
                "Assets/_Flats/Scenes/MainMenu.unity", "Assets/_Flats/Scenes/Tutorial.unity",
                "Assets/_Flats/Scenes/FlatCity.unity", "Assets/_Flats/Scenes/UrbanPark.unity",
                "Assets/_Flats/Scenes/BeachsideTown.unity", "Assets/_Flats/Scenes/DepartmentStore.unity",
                "Assets/_Flats/Scenes/Warehouse.unity", "Assets/_Flats/Scenes/NightLand.unity"
            };
            EditorBuildSettingsScene[] entries = new EditorBuildSettingsScene[scenes.Length];
            for (int i=0;i<scenes.Length;i++)
            {
                if (!File.Exists(scenes[i])) throw new FileNotFoundException(scenes[i]);
                entries[i] = new EditorBuildSettingsScene(scenes[i], true);
            }
            EditorBuildSettings.scenes = entries;
            Directory.CreateDirectory("Builds/Windows");
            const string output = "Builds/Windows/FlatsOffline.exe";
#if UNITY_2018_1_OR_NEWER
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            BuildOptions options = BuildOptions.None;
#if UNITY_2021_2_OR_NEWER
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-flats-clean-build") >= 0)
                options |= BuildOptions.CleanBuildCache;
#endif
            BuildReport report = BuildPipeline.BuildPlayer(scenes, output, BuildTarget.StandaloneWindows64, options);
            File.WriteAllText("Builds/build-result.txt", report.summary.result + "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings);
            using (var writer = new StreamWriter("Builds/build-messages.txt"))
                foreach (var step in report.steps)
                    foreach (var message in step.messages) writer.WriteLine(message.type + ": " + message.content);
            if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0) throw new Exception("Standalone build failed: " + report.summary.result);
#else
            string error = BuildPipeline.BuildPlayer(scenes, output, BuildTarget.StandaloneWindows64, BuildOptions.None);
            File.WriteAllText("Builds/build-result.txt", String.IsNullOrEmpty(error) ? "Succeeded" : error);
            if (!String.IsNullOrEmpty(error)) throw new Exception(error);
#endif
            if (!File.Exists(output)) throw new FileNotFoundException(output);
            Debug.Log("FLATS_BUILD_SUCCEEDED: " + Path.GetFullPath(output));
        }
        finally
        {
            // The offline candidate's identity and output settings must not
            // become the designer's permanent ProjectSettings, even on failure.
            PlayerSettings.companyName=previousCompany;
            PlayerSettings.productName=previousProduct;
            PlayerSettings.runInBackground=previousBackground;
            PlayerSettings.defaultScreenWidth=previousWidth;
            PlayerSettings.defaultScreenHeight=previousHeight;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,previousBackend);
            EditorBuildSettings.scenes=previousScenes;
            AssetDatabase.SaveAssets();
        }
    }
}
