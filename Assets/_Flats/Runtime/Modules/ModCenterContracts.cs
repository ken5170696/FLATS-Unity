using System;
using System.Threading;
using System.Threading.Tasks;
using Flats.UI;

namespace Flats.Modules
{
    // A bound page uses its host for the lifetime of that page, not a scene lookup.
    public interface IModHost
    {
        ModuleManager Manager { get; }
        ModProfiles Profiles { get; }
        CrosshairModule Crosshair { get; }
        CrosshairSettings ConfiguredCrosshair { get; }
        string Notice { get; }
        bool ReadOnly { get; }
        bool Requested(string id);
        bool Configure(CrosshairStyle style, float size);
        bool Configure(SettingValue[] values);
        // Mod API 1.2.0: declared settings of any installed module, stored per profile.
        SettingValue[] ConfiguredSettings(string id);
        bool Configure(string id, SettingValue[] values);
        // Mod API 1.3.0: hands draft values to the running module without saving them, so the
        // settings page previews live; the page hands the saved values back when it closes.
        bool PreviewSettings(string id, SettingValue[] values);
        void SelectProfile(string id);
        void InitializeProfiles(string directory, InstalledPackage[] installed);
        void AttachExternal(IFirstPartyModule[] modules, string[] requested);
        void SaveEnablePlan(string[] ids);
        void SaveExternalIntent(string id, bool requested, string version);
    }

    // Commands and disposal run on the owning (Unity) thread. Awaited disk/network
    // work resumes there; callers must not use returned state as cross-thread data.
    public interface IModCenter
    {
        PackageStore Store { get; }
        DownloadQueue Downloads { get; }
        IModSource Source { get; }
        InstalledPackage[] Installed { get; }
        InstalledPackage[] Running { get; }
        // Mod API 1.3.0: the running, active module that declares the graphics override, or null.
        InstalledPackage GraphicsOverride { get; }
        bool Ready { get; }
        bool InitializationComplete { get; }
        bool CanRetryInitialization { get; }
        Task RetryInitialization();
        string Notice { get; }
        string SourceUrl { get; }
        string PlanState { get; }
        Task RefreshInstalled();
        Task<CatalogItem[]> CheckUpdates(CancellationToken cancel);
        Task<DependencyPlan> Plan(PackageManifest manifest, CatalogItem download, CancellationToken cancel);
        Task ApplyPlan(DependencyPlan plan, string state, bool enable);
        Task Request(string id, bool requested);
        Task Remove(string id);
        Task<byte[]> Artwork(string url, CancellationToken cancel);
        string Problem(PackageManifest manifest);
        bool NeedsRestart(string id);
        // Mod API 1.2.0: hands saved setting values to the running module's context.
        // Returns false when that module is not running in this process.
        bool ApplyLiveSettings(string id, SettingValue[] values);
    }

    public interface IModCenterPlatform
    {
        IModJson Json { get; }
        bool Development { get; }
        bool RunInline { get; }
        string OfficialUrl { get; }
        IModSource CreateSource(string url, bool development);
        Task Work(Action action);
        Task<T> Work<T>(Func<T> action);
        void ValidatePackage(PackageManifest manifest);
        void ReportInitializationFailure(Exception error);
    }
}
