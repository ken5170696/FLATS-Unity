using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Flats.UI;

namespace Flats.Modules
{
    // Managed extensions run with the game's privileges. This is a lifecycle adapter, not a sandbox.
    internal sealed class ExternalModule : IFirstPartyModule
    {
        readonly InstalledPackage package;
        readonly string path;
        IFirstPartyModule implementation;
        public ModuleManifest Manifest { get; private set; }
        // Mod API 1.2.0: the installed directory and live setting values a managed
        // module receives through IModuleContextReceiver. Created for every kind so the
        // host can push saved values uniformly; only managed entry types observe it.
        public ModuleContext Context { get; }
        // Only a managed entry type can observe Context (IModuleContextReceiver). The data adapters
        // and crosshair presets below read the package payload or first preset in Enable and never
        // the declared settings, so a settings change has nothing to re-apply for them.
        public bool ReadsSettings { get { return package.manifest.kind!="crosshair" && package.manifest.kind!="data"; } }
        public ExternalModule(InstalledPackage p, string directory, SettingValue[] storedSettings=null)
        {
            package=p;path=directory;
            Context=new ModuleContext(Path.GetFullPath(directory),p.manifest.settings,storedSettings);
            var m=p.manifest.Validate();
            var conflicts=m.Conflicts.ToList();
            if(ModRules.IsCrosshairProvider(p.manifest))conflicts.Add(CrosshairModule.Id);
            Manifest=new ModuleManifest(m.Id,m.Version.ToString(),m.DisplayName,m.Description,m.Scope,m.ApiVersions,m.GameVersions,m.Dependencies.ToArray(),conflicts.Distinct().ToArray());
        }
        public void Initialize(ModuleLifetime lifetime)
        {
            // Crosshair presets and data packages carry no code.
            if(package.manifest.kind=="crosshair" || package.manifest.kind=="data")return;
#if ENABLE_IL2CPP && !UNITY_EDITOR
            throw new PlatformNotSupportedException("Downloaded managed DLL modules require a Mono desktop build. This AOT player cannot load executable modules.");
#else
            if(implementation==null)
            {
                var assembly=Assembly.LoadFrom(Path.Combine(path,package.manifest.assembly));
                var type=assembly.GetType(package.manifest.entryType,true);
                if(!typeof(IFirstPartyModule).IsAssignableFrom(type) || type.IsAbstract)throw new InvalidDataException("Entry type must implement IFirstPartyModule");
                implementation=(IFirstPartyModule)Activator.CreateInstance(type);
                var actual=implementation.Manifest; var declared=Manifest;
                if(actual.Id!=declared.Id || actual.Version!=declared.Version || actual.Scope!=declared.Scope || actual.ApiVersions.ToString()!=declared.ApiVersions.ToString() || actual.GameVersions.ToString()!=declared.GameVersions.ToString() ||
                    !actual.Dependencies.Select(d=>d.Id+d.Versions).SequenceEqual(declared.Dependencies.Select(d=>d.Id+d.Versions)) || !actual.Conflicts.SequenceEqual(declared.Conflicts))
                    throw new InvalidDataException("Entry manifest differs from the validated package manifest");
                // Attach runs once, before the first Initialize, so the module can read its
                // directory and settings during Initialize and Enable.
                if(implementation is IModuleContextReceiver receiver)receiver.Attach(Context);
            }
            implementation.Initialize(lifetime);
#endif
        }
        public void Enable(ModuleLifetime lifetime)
        {
            if(package.manifest.kind=="managed") { implementation.Enable(lifetime);return; }
            if(package.manifest.kind=="data" && package.manifest.adapter==Flats.Core.EnemyTuning.Adapter) { EnableEnemyTuning(lifetime);return; }
            if(package.manifest.kind=="data" && package.manifest.adapter==Flats.Core.ScopeView.Adapter) { EnableScopeView(lifetime);return; }
            if(!ModRules.IsCrosshairProvider(package.manifest))
                throw new NotSupportedException("No game handler for "+package.manifest.adapter);
            if(CrosshairPresentation.Appearance!=null)throw new InvalidOperationException("Disable the other crosshair module first");
            lifetime.Own(()=>CrosshairPresentation.Appearance=null);
            // A legacy preset keeps the thickness and gap the original renderer derived from its size;
            // a crosshair@2 data package uses its first preset.
            var settings=new CrosshairSettings();
            if(package.manifest.kind=="crosshair")settings.Values=CrosshairSettingsSpec.FromLegacy(package.manifest.crosshairStyle,package.manifest.crosshairSize);
            else if(package.manifest.presets!=null && package.manifest.presets.Length>0)settings.Values=ModuleSettingsSchema.Apply(CrosshairSettingsSpec.Specs(),settings.Values,package.manifest.presets[0]);
            CrosshairPresentation.Appearance=settings;
        }
        void EnableEnemyTuning(ModuleLifetime lifetime)
        {
            // The payload is part of the hashed package, so every player in the room applies the same values.
            var payload=ReadPayload<Flats.Core.EnemyTuningPayload>("Enemy tuning");
            Flats.Core.EnemyTuning.Apply(payload);
            // Registered after Apply: the adapter holds one static state, so a second package
            // rejected by Apply must not reset the first one during failure cleanup.
            lifetime.Own(Flats.Core.EnemyTuning.Reset);
        }
        void EnableScopeView(ModuleLifetime lifetime)
        {
            // Presentation only: the lens image grows on this player's screen; the aim ray,
            // damage and what other players see are unchanged.
            var payload=ReadPayload<Flats.Core.ScopeViewPayload>("Scope view");
            Flats.Core.ScopeView.Apply(payload);
            lifetime.Own(Flats.Core.ScopeView.Reset);
        }
        // Reads the package's JSON payload (at most 16 KiB, inside the package directory).
        T ReadPayload<T>(string what) where T:class
        {
            if(string.IsNullOrEmpty(package.manifest.payload))throw new InvalidDataException(what+" packages require a payload");
            string file=Path.GetFullPath(Path.Combine(path,package.manifest.payload));
            if(!file.StartsWith(Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Payload path escapes the package");
            var info=new FileInfo(file);if(!info.Exists || info.Length>16*1024)throw new InvalidDataException(what+" payload is missing or too large");
            var payload=UnityEngine.JsonUtility.FromJson<T>(File.ReadAllText(file));
            if(payload==null)throw new InvalidDataException("Invalid "+what.ToLowerInvariant()+" payload");
            return payload;
        }
        public void Disable() { implementation?.Disable(); }
    }
}
