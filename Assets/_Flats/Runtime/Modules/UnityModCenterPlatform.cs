using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Flats.Modules
{
    public sealed class UnityModJson : IModJson
    {
        public T Read<T>(string text) { return JsonUtility.FromJson<T>(text); }
        public string Write<T>(T value) { return JsonUtility.ToJson(value); }
    }

    // Resolve Unity resources and platform policy once at the composition boundary.
    public sealed class UnityModCenterPlatform : IModCenterPlatform
    {
        public IModJson Json { get; } = new UnityModJson();
        public bool Development { get; }
        public string OfficialUrl { get; }
        public bool RunInline { get; }
        public UnityModCenterPlatform()
        {
            var args=Environment.GetCommandLineArgs();
            Development=Application.isEditor || (Debug.isDebugBuild && args.Contains("-flats-verify") && args.Contains("-flats-module-settings-dir"));
            var bundled=Resources.Load<TextAsset>("FlatsModCatalogue");
            OfficialUrl=string.IsNullOrWhiteSpace(OfficialModEndpoint.Url)?bundled?.text.Trim():OfficialModEndpoint.Url;
#if UNITY_WEBGL && !UNITY_EDITOR
            RunInline=true;
#endif
        }
        public IModSource CreateSource(string url, bool development)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WebModSource(url,Json);
#else
            return new HttpModSource(url,Json,development);
#endif
        }
        public Task Work(Action action)
        {
            if(RunInline){action();return Task.CompletedTask;}
            return Task.Run(action);
        }
        public Task<T> Work<T>(Func<T> action)
        {
            return RunInline?Task.FromResult(action()):Task.Run(action);
        }
        public void ValidatePackage(PackageManifest manifest)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebModSource.ValidatePackage(manifest);
#endif
        }
        public void ReportInitializationFailure(Exception error) { Debug.LogWarning("MOD_CENTER_INIT "+error.GetType().Name); }
    }
}
