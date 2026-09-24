using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Batch entry points deliberately produce local candidates, never publish artifacts.
public static class FlatsPortalBuild
{
    [Serializable] sealed class Provenance
    {
        public string sourceCommit, unityVersion, target, builtUtc, result;
        public string sourceFingerprint;
        public bool sourceDirty, development;
        public ulong bytes;
        public int errors, warnings;
        public bool publicDistributionApproved = false;
    }

    [Serializable] sealed class SourceFile
    {
        public string path, sha256;
    }

    [Serializable] sealed class SourceSnapshot
    {
        public string commit, fingerprint;
        public bool dirty;
        public SourceFile[] files;
    }

    [Serializable] sealed class PackedItem
    {
        public string container, source;
        public ulong bytes;
    }

    [Serializable] sealed class SizeReport
    {
        public PackedItem[] assets;
    }

    [MenuItem("FLATS/Build/Portal Windows")]
    public static void Windows() { Build(BuildTarget.StandaloneWindows64, "Windows", "FLATS.exe"); }
    [MenuItem("FLATS/Build/Portal Web")]
    public static void Web() { Build(BuildTarget.WebGL, "Web", ""); }
    [MenuItem("FLATS/Build/Portal Linux")]
    public static void Linux() { Build(BuildTarget.StandaloneLinux64, "Linux", "FLATS.x86_64"); }
    [MenuItem("FLATS/Build/Portal macOS")]
    public static void Mac() { Build(BuildTarget.StandaloneOSX, "macOS", "FLATS.app"); }
    public static void Android()
    {
        RequireSavedAuthoringState();
        var previous = PlayerSettings.Android.targetArchitectures;
        var previousCode = PlayerSettings.Android.bundleVersionCode;
        var previousApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        var previousDefaultApis = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
        try
        {
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.bundleVersionCode = Math.Max(1, previousCode);
            // Recovered FxPro shaders fail Vulkan compilation; GLES3 preserves
            // the existing effects without shipping known broken variants.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            Build(BuildTarget.Android, "Android", "FLATS.apk");
        }
        finally
        {
            PlayerSettings.Android.targetArchitectures = previous;
            PlayerSettings.Android.bundleVersionCode = previousCode;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, previousApis);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, previousDefaultApis);
            AssetDatabase.SaveAssets();
        }
    }
    public static void IOS() { Build(BuildTarget.iOS, "iOS", ""); }

    static void Build(BuildTarget target, string name, string file)
    {
        RequireSavedAuthoringState();
        var group = BuildPipeline.GetBuildTargetGroup(target);
        if (!BuildPipeline.IsBuildTargetSupported(group, target))
            throw new BuildFailedException("Install Unity " + Application.unityVersion + " support module for " + target);
        var named = NamedBuildTarget.FromBuildTargetGroup(group);
        var oldBackend = PlayerSettings.GetScriptingBackend(named);
        var oldIdentifier = PlayerSettings.GetApplicationIdentifier(named);
        var oldVersion = PlayerSettings.bundleVersion;
        var oldCompression = PlayerSettings.WebGL.compressionFormat;
        var oldThreads = PlayerSettings.WebGL.threadsSupport;
        var oldCache = PlayerSettings.WebGL.dataCaching;
        var oldFallback = PlayerSettings.WebGL.decompressionFallback;
        var oldSymbols = PlayerSettings.WebGL.debugSymbolMode;
        string root = Path.GetFullPath("Builds/Portal/" + name);
        Directory.CreateDirectory(root);
        var source = CaptureSource();
        const string catalogueAsset = "Assets/Resources/FlatsModCatalogue.txt";
        const string photonAsset = "Assets/Resources/FlatsPhotonClient.txt";
        if (File.Exists(photonAsset) || File.Exists(photonAsset + ".meta")) throw new BuildFailedException("Reserved generated Photon client asset or metadata already exists.");
        string photonClient = Environment.GetEnvironmentVariable("FLATS_PHOTON_APP_ID");
        if (!string.IsNullOrWhiteSpace(photonClient) && (!Guid.TryParse(photonClient, out var clientId) || clientId == Guid.Empty))
            throw new BuildFailedException("Invalid release Photon client App ID.");
        if(File.Exists(catalogueAsset) || File.Exists(catalogueAsset + ".meta")) throw new BuildFailedException("Reserved generated catalogue asset or metadata already exists; preserve and move it before building.");
        string catalogue = Flats.Modules.OfficialModEndpoint.Url;
        if(!string.IsNullOrWhiteSpace(catalogue))
        {
            var uri=Flats.Modules.ModRules.Url(catalogue.TrimEnd('/')+"/",false);
            if(uri.Query.Length>0 || uri.Fragment.Length>0) throw new BuildFailedException("Catalogue must be an HTTPS base URL without query or fragment.");
            catalogue=uri.AbsoluteUri;
        }
        try
        {
            if (!string.IsNullOrWhiteSpace(photonClient))
            {
                Directory.CreateDirectory("Assets/Resources");
                File.WriteAllText(photonAsset, photonClient.Trim());
                AssetDatabase.ImportAsset(photonAsset, ImportAssetOptions.ForceSynchronousImport);
            }
            if(!string.IsNullOrWhiteSpace(catalogue))
            {
                Directory.CreateDirectory("Assets/Resources");
                File.WriteAllText(catalogueAsset,catalogue);
                AssetDatabase.ImportAsset(catalogueAsset,ImportAssetOptions.ForceSynchronousImport);
            }
            if (target == BuildTarget.Android || target == BuildTarget.iOS)
            {
                PlayerSettings.SetApplicationIdentifier(named, "io.github.ken5170696.flats.preview");
                PlayerSettings.bundleVersion = oldVersion;
            }
            PlayerSettings.SetScriptingBackend(named, group == BuildTargetGroup.Standalone ? ScriptingImplementation.Mono2x : ScriptingImplementation.IL2CPP);
            if (target == BuildTarget.WebGL)
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                PlayerSettings.WebGL.threadsSupport = false;
                PlayerSettings.WebGL.dataCaching = false;
                PlayerSettings.WebGL.decompressionFallback = false;
                PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            }
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 || !scenes[0].EndsWith("/MainMenu.unity"))
                throw new BuildFailedException("Expected tracked build scenes with MainMenu first.");
            foreach (string scene in scenes) if (!File.Exists(scene)) throw new FileNotFoundException(scene);
            File.WriteAllText(Path.Combine(root, "source-snapshot.json"), JsonUtility.ToJson(source, true));
            // FLATS_DEVELOPMENT_BUILD=1 produces a Development player for local verification.
            bool development = Environment.GetEnvironmentVariable("FLATS_DEVELOPMENT_BUILD") == "1";
            var options = BuildOptions.DetailedBuildReport | (development ? BuildOptions.Development : BuildOptions.None);
            var report = BuildPipeline.BuildPlayer(scenes, Path.Combine(root, file), target, options);
            // Some failed export postprocessors remove their destination folder.
            Directory.CreateDirectory(root);
            var evidence = new Provenance { sourceCommit=source.commit, sourceFingerprint=source.fingerprint,
                sourceDirty=source.dirty, development=development, unityVersion=Application.unityVersion,
                target=target.ToString(), builtUtc=DateTime.UtcNow.ToString("o"), result=report.summary.result.ToString(),
                bytes=report.summary.totalSize, errors=report.summary.totalErrors, warnings=report.summary.totalWarnings };
            File.WriteAllText(Path.Combine(root,"build-provenance.json"), JsonUtility.ToJson(evidence,true));
            var sizes = new SizeReport { assets = report.packedAssets.SelectMany(pack => pack.contents.Select(item =>
                new PackedItem { container = pack.shortPath, source = item.sourceAssetPath, bytes = item.packedSize }))
                .OrderByDescending(item => item.bytes).ToArray() };
            File.WriteAllText(Path.Combine(root, "packed-assets.json"), JsonUtility.ToJson(sizes, true));
            using (var writer = new StreamWriter("Builds/Portal/" + name + "-messages.txt"))
                foreach (var step in report.steps) foreach (var message in step.messages)
                    writer.WriteLine(message.type + ": " + message.content);
            if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0)
                throw new BuildFailedException("FLATS " + name + " build: " + report.summary.result);
            if(target==BuildTarget.WebGL)
            {
                var index=Path.Combine(root,"index.html");var html=File.ReadAllText(index);
                const string setting="// config.autoSyncPersistentDataPath = true;";
                if(!html.Contains(setting))throw new BuildFailedException("Web template must expose autoSyncPersistentDataPath before starting the player.");
                File.WriteAllText(index,html.Replace(setting,"config.autoSyncPersistentDataPath = true;"));
            }
            Debug.Log("FLATS_PORTAL_BUILD_SUCCEEDED " + root);
        }
        finally
        {
            if(File.Exists(photonAsset)) AssetDatabase.DeleteAsset(photonAsset);
            if(File.Exists(catalogueAsset)) AssetDatabase.DeleteAsset(catalogueAsset);
            PlayerSettings.SetApplicationIdentifier(named, oldIdentifier);
            PlayerSettings.bundleVersion = oldVersion;
            PlayerSettings.SetScriptingBackend(named, oldBackend);
            PlayerSettings.WebGL.compressionFormat = oldCompression;
            PlayerSettings.WebGL.threadsSupport = oldThreads;
            PlayerSettings.WebGL.dataCaching = oldCache;
            PlayerSettings.WebGL.decompressionFallback = oldFallback;
            PlayerSettings.WebGL.debugSymbolMode = oldSymbols;
            AssetDatabase.SaveAssets();
        }
    }

    static void RequireSavedAuthoringState()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new BuildFailedException("Stop Play Mode before building.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isDirty)
                throw new BuildFailedException("Save or discard scene changes explicitly before building: " + scene.name);
        }
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.scene.isDirty)
            throw new BuildFailedException("Save or discard Prefab Mode changes explicitly before building: " + stage.assetPath);
    }

    static string Hash(byte[] bytes)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    static SourceSnapshot CaptureSource()
    {
        var files = Git("ls-files -z --cached --others --exclude-standard").Split('\0')
            .Where(path => path.Length > 0).Distinct().OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new SourceFile { path = path,
                sha256 = File.Exists(path) ? Hash(File.ReadAllBytes(path)) : "deleted" }).ToArray();
        return new SourceSnapshot {
            commit = Git("rev-parse HEAD").Trim(), dirty = Git("status --porcelain").Length > 0, files = files,
            fingerprint = Hash(Encoding.UTF8.GetBytes(string.Concat(files.Select(item => item.path + "\0" + item.sha256 + "\n"))))
        };
    }

    static string Git(string arguments)
    {
        string git = Environment.GetEnvironmentVariable("FLATS_GIT_EXECUTABLE") ?? "git";
        string windowsGit = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git/cmd/git.exe");
        if (git == "git" && File.Exists(windowsGit)) git = windowsGit;
        var start = new System.Diagnostics.ProcessStartInfo(git, arguments) {
            WorkingDirectory=Path.GetFullPath("."), UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true };
        using (var process = System.Diagnostics.Process.Start(start))
        {
            string value=process.StandardOutput.ReadToEnd(); process.WaitForExit();
            if (process.ExitCode != 0) throw new BuildFailedException("Cannot capture Git source identity.");
            return value;
        }
    }
}
