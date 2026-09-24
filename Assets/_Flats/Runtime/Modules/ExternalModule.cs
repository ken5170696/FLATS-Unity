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
        public ExternalModule(InstalledPackage p, string directory)
        {
            package=p;path=directory;
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
            }
            implementation.Initialize(lifetime);
#endif
        }
        public void Enable(ModuleLifetime lifetime)
        {
            if(package.manifest.kind=="managed") { implementation.Enable(lifetime);return; }
            if(package.manifest.kind=="data" && package.manifest.adapter==Flats.Core.EnemyTuning.Adapter) { EnableEnemyTuning(lifetime);return; }
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
            if(string.IsNullOrEmpty(package.manifest.payload))throw new InvalidDataException("Enemy tuning packages require a payload");
            string file=Path.GetFullPath(Path.Combine(path,package.manifest.payload));
            if(!file.StartsWith(Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Payload path escapes the package");
            var info=new FileInfo(file);if(!info.Exists || info.Length>16*1024)throw new InvalidDataException("Enemy tuning payload is missing or too large");
            var payload=UnityEngine.JsonUtility.FromJson<Flats.Core.EnemyTuningPayload>(File.ReadAllText(file));
            if(payload==null)throw new InvalidDataException("Invalid enemy tuning payload");
            Flats.Core.EnemyTuning.Apply(payload);
            lifetime.Own(Flats.Core.EnemyTuning.Reset);
        }
        public void Disable() { implementation?.Disable(); }
    }
}
