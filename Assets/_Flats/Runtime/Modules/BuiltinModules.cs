using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Flats.UI;

namespace Flats.Modules
{
    [Serializable] public sealed class CrosshairSettings : ICrosshairAppearance
    {
        public int schema=1;
        public CrosshairStyle style=CrosshairStyle.Cross;
        public float size=24;
        public CrosshairStyle Style { get { return style; } }
        public float Size { get { return size; } }
        public void Validate()
        {
            if(schema!=1)throw new NotSupportedException("Crosshair settings version is unsupported");
            if(!Enum.IsDefined(typeof(CrosshairStyle),style) || float.IsNaN(size) || float.IsInfinity(size))throw new InvalidDataException("Invalid crosshair settings");
            size=Mathf.Clamp(size,6,64);
        }
    }
    public sealed class CrosshairModule : IFirstPartyModule
    {
        public const string Id="official.custom-crosshair";
        public CrosshairSettings Settings = new CrosshairSettings();
        public ModuleManifest Manifest { get; private set; }
        public CrosshairModule()
        {
            Manifest=new ModuleManifest(Id,"1.0.0","Custom Crosshair","Cross, dot or ring. Local HUD appearance only.",
                ModuleScope.ClientOnly,new VersionRange("1.0.0","2.0.0"),new VersionRange("5.3.5","5.4.0"));
        }
        public void Initialize(ModuleLifetime lifetime) { Settings.Validate(); }
        internal CrosshairModule Bind(PackageManifest package) { Manifest=package.Validate();return this; }
        public void Enable(ModuleLifetime lifetime)
        {
            if(CrosshairPresentation.Appearance!=null)throw new InvalidOperationException("Crosshair appearance already registered");
            lifetime.Own(()=>CrosshairPresentation.Appearance=null);
            CrosshairPresentation.Appearance=Settings;
        }
        public void Disable() { }
    }
    public sealed class BuiltinModules : MonoBehaviour, IModHost
    {
        public static BuiltinModules Instance { get; private set; }
        public ModuleManager Manager { get; private set; }
        public CrosshairModule Crosshair { get; private set; }
        public string Notice { get; private set; }
        public bool ReadOnly { get { return store.ReadOnly; } }
        ModuleSettingsStore store;
        ModuleSettingsDocument document;
        public ModCenterService Center { get; private set; }
        public ModProfiles Profiles { get; private set; }
        public CrosshairSettings ConfiguredCrosshair
        {
            get { var entry=document.modules.FirstOrDefault(m=>m.id==CrosshairModule.Id);var value=entry!=null&&!string.IsNullOrEmpty(entry.json)?JsonUtility.FromJson<CrosshairSettings>(entry.json):new CrosshairSettings();value.Validate();return value; }
        }
        public bool Requested(string id) { return Profiles!=null?Profiles.Requested(id):document.modules.Any(m=>m.id==id&&m.requested); }
        public void InitializeProfiles(string directory,InstalledPackage[] installed)
        {
            if(store.ReadOnly)throw new InvalidOperationException(store.Notice);
            var legacy=document.modules.Select(m=>new ProfileModule{id=m.id=="flats.crosshair"?CrosshairModule.Id:m.id,version=m.version,json=m.json,requested=m.id!="flats.crosshair"&&m.requested}).ToList();
            var crosshair=legacy.FirstOrDefault(m=>m.id==CrosshairModule.Id);
            // Awake already validated legacy settings and recovered invalid data to
            // defaults. Migrate that validated result; retain the original legacy file.
            if(crosshair!=null){crosshair.json=JsonUtility.ToJson(Crosshair.Settings);crosshair.version="1.0.0";}
            foreach(var p in installed)if(!legacy.Any(m=>m.id==p.manifest.id))legacy.Add(new ProfileModule{id=p.manifest.id,version=p.manifest.version,requested=p.requested,json=""});
            Profiles=new ModProfiles(Path.Combine(directory,"mod-profiles-v1.json"),new UnityModJson(),legacy.ToArray());
            Profiles.RetireBuiltin("flats.crosshair",CrosshairModule.Id);
            if(!installed.Any(p=>p.manifest.id==CrosshairModule.Id))
            {
                var entries=Profiles.Selected.modules;
                foreach(var entry in entries.Where(m=>m.id==CrosshairModule.Id))entry.requested=false;
                Profiles.Save(entries);
            }
            LoadSelectedDocument();Crosshair.Settings=ConfiguredCrosshair;
        }
        void LoadSelectedDocument()
        {
            document=new ModuleSettingsDocument{modules=Profiles.Selected.modules.Select(m=>new ModuleSetting{id=m.id,version=m.version,json=m.json,requested=m.requested}).ToArray()};
        }
        void SaveDocument(ModuleSettingsDocument next)
        {
            if(Profiles==null)throw new InvalidOperationException("Profiles are still loading");
            var entries=Profiles.Selected.modules.ToList();
            foreach(var m in next.modules){entries.RemoveAll(p=>p.id==m.id);entries.Add(new ProfileModule{id=m.id,version=m.version,json=m.json,requested=m.requested});}
            Profiles.Save(entries.ToArray());document=next;
        }
        public void SelectProfile(string id)
        {
            Profiles.Select(id,profile=>
            {
                var enabled=profile.modules.Where(m=>m.requested).Select(m=>m.id).ToArray();
                foreach(var entry in profile.modules)
                {
                    if(entry.id==CrosshairModule.Id && !entry.requested){if(!string.IsNullOrEmpty(entry.json)){var settings=JsonUtility.FromJson<CrosshairSettings>(entry.json);settings.Validate();}continue;}
                    if(!entry.requested)continue;
                    var p=Center.Installed.FirstOrDefault(i=>i.manifest.id==entry.id);if(p==null)throw new InvalidOperationException("Missing module: "+entry.id);
                    var issue=ModuleDiagnostics.Inspect(p.manifest,Center.Installed.Select(i=>i.manifest),enabled);if(issue.Length>0)throw new InvalidOperationException(issue);
                }
            });
            LoadSelectedDocument();Notice=Profiles.RestartRequired?"Profile selected. Restart FLATS to apply its mods and settings.":"Running profile selected.";
        }
        public void SaveEnablePlan(string[] ids)
        {
            var entries=Profiles.Selected.modules.ToList();
            foreach(var id in ids)
            {
                var entry=entries.FirstOrDefault(m=>m.id==id);
                if(entry==null){entry=new ProfileModule{id=id,json=""};entries.Add(entry);}
                entry.requested=true;entry.version=Center.Installed.Single(p=>p.manifest.id==id).manifest.version;
            }
            Profiles.Save(entries.ToArray());LoadSelectedDocument();
        }
        public void SaveExternalIntent(string id,bool requested,string version)
        {
            var entries=Profiles.Selected.modules.ToList();var entry=entries.FirstOrDefault(m=>m.id==id);
            if(entry==null){entry=new ProfileModule{id=id,json=""};entries.Add(entry);}entry.requested=requested;entry.version=version;
            Profiles.Save(entries.ToArray());LoadSelectedDocument();
        }
        string[] externalRequested = new string[0];
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Boot()
        {
            if(Instance!=null)return;
            var go=new GameObject("First-party modules"); DontDestroyOnLoad(go); go.AddComponent<BuiltinModules>();
        }
        void Awake()
        {
            if(Instance!=null && Instance!=this){Destroy(gameObject);return;}
            Instance=this; CrosshairPresentation.Appearance=null;
            string directory=Application.persistentDataPath;
#if UNITY_EDITOR
            var testRoot=Environment.GetEnvironmentVariable("FLATS_MOD_TEST_ROOT");
            if(!string.IsNullOrEmpty(testRoot))directory=Path.GetFullPath(testRoot);
#endif
            // Existing verification opt-in allows isolated per-client settings, never affects normal profiles.
            var args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,"-flats-module-settings-dir");
            if(Array.IndexOf(args,"-flats-verify")>=0 && i>=0 && i+1<args.Length)directory=Path.GetFullPath(args[i+1]);
            store=new ModuleSettingsStore(Path.Combine(directory,"modules-v1.json")); document=store.Load(); Notice=store.Notice;
            Crosshair=new CrosshairModule();
            var entry=document.modules.FirstOrDefault(m=>m.id==CrosshairModule.Id || m.id=="flats.crosshair");
            if(entry!=null && !string.IsNullOrEmpty(entry.json))
                try
                {
                    // Settings schema, not the package release version, governs compatibility.
                    var settings=JsonUtility.FromJson<CrosshairSettings>(entry.json); settings.Validate(); Crosshair.Settings=settings;
                }
                catch(Exception) { Notice+="\nCrosshair settings reset to defaults; previous record remains in backup until saved."; }
            Manager=new ModuleManager(new IFirstPartyModule[0],ModRules.ApiVersion,ModRules.GameVersion);
            Manager.Apply(new string[0]);
            Center=new ModCenterService(this,directory,new UnityModCenterPlatform());
            _ = Center.Initialize();
        }
        public void AttachExternal(IFirstPartyModule[] modules,string[] requested)
        {
            Manager.Dispose();externalRequested=requested;
            Manager=new ModuleManager(modules,ModRules.ApiVersion,ModRules.GameVersion,true);
            Manager.Apply(externalRequested);
            if(!string.IsNullOrEmpty(Manager.LastError))Notice=Manager.LastError;
        }
        ModuleSettingsDocument Candidate(string id, bool? requested, CrosshairSettings settings)
        {
            var copy=JsonUtility.FromJson<ModuleSettingsDocument>(JsonUtility.ToJson(document));
            var entries=copy.modules.ToList(); var entry=entries.FirstOrDefault(m=>m.id==id);
            if(entry==null){entry=new ModuleSetting{id=id};entries.Add(entry);}
            entry.version=Center.Installed.Single(p=>p.manifest.id==id).manifest.version;
            if(requested.HasValue)entry.requested=requested.Value;
            if(settings!=null)entry.json=JsonUtility.ToJson(settings);
            copy.modules=entries.ToArray();return copy;
        }
        public bool Request(string id,bool enabled)
        {
            try
            {
                var next=Candidate(id,enabled,id==CrosshairModule.Id?ConfiguredCrosshair:null);
                SaveDocument(next);
                if(Profiles.RestartRequired){Notice="Saved to selected profile. Restart required.";return true;}
                bool ok=Manager.Apply(externalRequested);
                Notice=ok?"Saved":Manager.LastError; return ok;
            }
            catch(Exception e){Notice="Could not save; active state unchanged. "+e.Message;return false;}
        }
        public bool Configure(CrosshairStyle style,float size)
        {
            try
            {
                var nextSettings=new CrosshairSettings{style=style,size=size}; nextSettings.Validate();
                var next=Candidate(CrosshairModule.Id,null,nextSettings);SaveDocument(next);
                if(!Profiles.RestartRequired){Crosshair.Settings.style=nextSettings.style;Crosshair.Settings.size=nextSettings.size;}
                Notice=Profiles.RestartRequired?"Saved to selected profile. Restart required.":"Saved";return true;
            }
            catch(Exception e){Notice="Could not save; previous settings retained. "+e.Message;return false;}
        }
        void OnDestroy() { Center?.Dispose(); if(Manager!=null)Manager.Dispose(); if(Instance==this)Instance=null; }
    }
}
