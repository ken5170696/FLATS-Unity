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
            if(p.manifest.kind=="crosshair")conflicts.Add(CrosshairModule.Id);
            Manifest=new ModuleManifest(m.Id,m.Version.ToString(),m.DisplayName,m.Description,m.Scope,m.ApiVersions,m.GameVersions,m.Dependencies.ToArray(),conflicts.Distinct().ToArray());
        }
        public void Initialize(ModuleLifetime lifetime)
        {
            if(package.manifest.kind=="crosshair")return;
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
            if(CrosshairPresentation.Appearance!=null)throw new InvalidOperationException("Disable the other crosshair module first");
            lifetime.Own(()=>CrosshairPresentation.Appearance=null);
            CrosshairPresentation.Appearance=new CrosshairSettings { style=(CrosshairStyle)package.manifest.crosshairStyle,size=package.manifest.crosshairSize };
        }
        public void Disable() { implementation?.Disable(); }
    }
}
