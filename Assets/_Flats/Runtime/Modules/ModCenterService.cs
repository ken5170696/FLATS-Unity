using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flats.Modules
{
    [Serializable] public sealed class ModSourceSettings { public int schema=1; public string url=""; }

    public sealed class ModCenterService : IModCenter, IDisposable
    {
        public PackageStore Store { get; private set; }
        public DownloadQueue Downloads { get; private set; }
        public IModSource Source { get; private set; }
        public InstalledPackage[] Installed { get; private set; } = new InstalledPackage[0];
        public InstalledPackage[] Running { get; private set; } = new InstalledPackage[0];
        public bool Ready { get; private set; }
        public bool InitializationComplete { get; private set; }
        public bool CanRetryInitialization { get { return !disposed && InitializationComplete && !Ready && !owner.ReadOnly && !activationStarted; } }
        bool activationStarted;
        public string Notice { get; private set; } = "Loading local modules...";
        public string SourceUrl { get { return Source?.Identity ?? ""; } }
        readonly string root;
        readonly IModJson json;
        readonly IModCenterPlatform platform;
        readonly bool development;
        readonly IModHost owner;
        bool disposed;
        readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        readonly CancellationToken lifetimeToken;
        readonly System.Collections.Generic.List<IDisposable> retiredSources=new System.Collections.Generic.List<IDisposable>();
        public ModCenterService(BuiltinModules modules,string directory)
            : this(modules,directory,new UnityModCenterPlatform()) { }
        public ModCenterService(IModHost modules,string directory,IModCenterPlatform platform)
        {
            owner=modules ?? throw new ArgumentNullException(nameof(modules));
            root=directory ?? throw new ArgumentNullException(nameof(directory));
            this.platform=platform ?? throw new ArgumentNullException(nameof(platform));
            json=platform.Json;development=platform.Development;lifetimeToken=lifetime.Token;
        }
        Task initialization;
        void EnsureAlive() { if(disposed)throw new ObjectDisposedException(nameof(ModCenterService)); }
        public Task Initialize()
        {
            EnsureAlive();return initialization ?? (initialization=InitializeCore());
        }
        public Task RetryInitialization()
        {
            EnsureAlive();
            if(!CanRetryInitialization)throw new InvalidOperationException("Restart FLATS after restoring compatible, writable module settings.");
            initialization=null;return Initialize();
        }
        void EnsureReady()
        {
            EnsureAlive();if(!Ready)throw new InvalidOperationException(InitializationComplete?Notice:"Local modules are still loading.");
        }
        async Task InitializeCore()
        {
            InitializationComplete=false;Notice="Loading local modules...";
            PackageStore preparedStore=null;
            InstalledPackage[] preparedInstalled=null;
            IModSource preparedSource=null;
            try
            {
                await platform.Work(()=>
                {
                    preparedStore=new PackageStore(Path.Combine(root,"mods"),json);preparedStore.Recover();preparedInstalled=preparedStore.Scan();
                    var config=Path.Combine(root,"mod-source.json");
                    try
                    {
                        if(development && File.Exists(config))
                        {
                            if(new FileInfo(config).Length>4096)throw new InvalidDataException("Source configuration is too large");
                            var s=json.Read<ModSourceSettings>(File.ReadAllText(config));
                            if(s==null || s.schema!=1)throw new InvalidDataException("Unsupported source configuration");
                            if(!string.IsNullOrWhiteSpace(s.url))preparedSource=platform.CreateSource(s.url,true);
                        }
                        else if(!string.IsNullOrWhiteSpace(platform.OfficialUrl))preparedSource=platform.CreateSource(platform.OfficialUrl,false);
                        if(!development && File.Exists(config))preparedStore.Notices.Add("Legacy source preferences are retained for recovery and ignored by this version.");
                    }
                    catch(Exception) { preparedStore.Notices.Add("Official mod service unavailable. Installed mods remain available."); }
                });
                if(disposed){(preparedSource as IDisposable)?.Dispose();return;}
                Store=preparedStore;Installed=preparedInstalled;Source=preparedSource;
                owner.InitializeProfiles(root,Installed);ApplyProfileIntent(Installed);
                Downloads=new DownloadQueue(Store,Source,runInline:platform.RunInline);Running=Installed;
                activationStarted=true;
                owner.AttachExternal(Running.Select(p=>p.manifest.id==CrosshairModule.Id && p.manifest.kind=="crosshair" ? (IFirstPartyModule)owner.Crosshair.Bind(p.manifest) : new ExternalModule(p,Store.ContentPath(p))).ToArray(),Running.Where(p=>p.requested).Select(p=>p.manifest.id).ToArray());
                Notice=string.Join("\n",Store.Notices.Distinct());
                Ready=true;
                if(Source==null)Notice+="\nOfficial mod service is unavailable in this build. Installed mods remain available.";
            }
            catch(Exception e)
            {
                Downloads?.Dispose();Downloads=null;
                (preparedSource as IDisposable)?.Dispose();Source=null;
                if(!disposed)
                {
                    Notice="Local modules could not start. "+e.Message+"\nCheck storage access and restore compatible settings from a retained backup if needed. No module changes are available until recovery.";
                    platform.ReportInitializationFailure(e);
                }
            }
            finally { if(!disposed)InitializationComplete=true; }
        }
        public async Task RefreshInstalled()
        {
            EnsureReady();
            var scanned=await platform.Work(()=>Store.Scan());
            EnsureAlive();Installed=scanned;
            ApplyProfileIntent(Installed);
        }
        void ApplyProfileIntent(InstalledPackage[] packages) { if(owner.Profiles!=null)foreach(var p in packages)p.requested=owner.Profiles.Requested(p.manifest.id); }
        public async Task<CatalogItem[]> CheckUpdates(CancellationToken cancel)
        {
            EnsureReady();
            using var operation=CancellationTokenSource.CreateLinkedTokenSource(cancel,lifetimeToken);
            cancel=operation.Token;
            var source=Source;if(source==null)throw new InvalidOperationException("Official mod service is unavailable. Try again later.");
            var items=new System.Collections.Generic.List<CatalogItem>();
            foreach(var p in Installed)
            {
                cancel.ThrowIfCancellationRequested();
                var page=await source.Browse(new CatalogQuery{Search=p.manifest.id,Compatible=false},cancel);
                EnsureAlive();cancel.ThrowIfCancellationRequested();
                var item=page.items.FirstOrDefault(i=>i.manifest.id==p.manifest.id);
                if(item!=null)items.Add(item);
            }
            Notice="Update check complete: "+items.Count+" installed mods found at this source.";
            return items.ToArray();
        }
        public async Task ConfigureSource(string url)
        {
            EnsureReady();
            if(!development)throw new InvalidOperationException("The official source is managed by the developer");
            // Construct and validate before replacing the persisted configuration.
            var next=string.IsNullOrWhiteSpace(url)?null:platform.CreateSource(url,development);
            try
            {
                await platform.Work(()=>
                {
                    Directory.CreateDirectory(root);
                    var path=Path.Combine(root,"mod-source.json");var temp=path+".new";
                    File.WriteAllText(temp,json.Write(new ModSourceSettings{url=next?.Identity ?? ""}));
                    if(File.Exists(path))File.Replace(temp,path,path+".previous");else File.Move(temp,path);
                });
            }
            catch { (next as IDisposable)?.Dispose();throw; }
            if(disposed){(next as IDisposable)?.Dispose();EnsureAlive();}
            // Existing queued jobs retain the source they were created with.
            if(Source is IDisposable previous)retiredSources.Add(previous);
            Source=next;Notice=next==null?"Online source removed.":"Source saved.";
        }
        public async Task<DependencyPlan> Plan(PackageManifest manifest,CatalogItem download,CancellationToken cancel)
        {
            EnsureReady();
            using var operation=CancellationTokenSource.CreateLinkedTokenSource(cancel,lifetimeToken);
            cancel=operation.Token;
            var enabled=Installed.Where(p=>p.requested).Select(p=>p.manifest.id);
            var plan=await new DependencyPlanner(Source,Installed,enabled).Resolve(manifest,download,cancel);
            EnsureAlive();cancel.ThrowIfCancellationRequested();return plan;
        }
        public string PlanState { get { return owner.Profiles.SelectedId+"|"+string.Join(";",Installed.OrderBy(p=>p.manifest.id).Select(p=>p.manifest.id+":"+p.sha256+":"+p.requested))+"|"+owner.Requested(CrosshairModule.Id); } }
        public async Task ApplyPlan(DependencyPlan plan,string state,bool enable)
        {
            EnsureReady();
            foreach(var manifest in plan.Modules)platform.ValidatePackage(manifest);
            await RefreshInstalled();
            if(state!=PlanState)throw new InvalidOperationException("Mods or profile changed. Review a new plan.");
            if(plan.Downloads.Length>0)
            {
                // Installing never silently changes enable intent. Existing active
                // roots needing newly enabled dependencies must first be disabled.
                foreach(var p in Installed.Where(p=>p.requested))
                {
                    var next=plan.Modules.FirstOrDefault(m=>m.id==p.manifest.id);
                    if(next!=null && ModuleDiagnostics.Inspect(next,plan.Modules,Installed.Where(i=>i.requested).Select(i=>i.manifest.id)).Length>0)
                        throw new InvalidOperationException("Disable "+p.manifest.name+" before changing its requirements. Then install and review Enable again.");
                }
                Downloads.Enqueue(plan.Downloads.FirstOrDefault(i=>i.manifest.id==plan.RootId)??plan.Downloads[0],Source,plan.Downloads);
                Notice=enable?"Requirements added to Downloads. When complete, review Enable again.":"Install plan added to Downloads. Existing settings retained; new mods remain disabled.";
                return;
            }
            if(enable)
            {
                if(plan.Enable.Any(id=>Downloads.IsBusy(id)))throw new InvalidOperationException("Wait for downloads before enabling this plan");
                owner.SaveEnablePlan(plan.Enable);await RefreshInstalled();Notice="Enabled the complete dependency set in this profile. Restart FLATS to apply.";
            }
            else Notice="All planned versions are already installed.";
        }
        public async Task Request(string id,bool requested)
        {
            EnsureReady();
            if(Downloads.IsBusy(id))throw new InvalidOperationException("Wait for this module's download to finish");
            if(!requested)CheckDependents(id);
            if(requested)
            {
                var manifest=Installed.Single(p=>p.manifest.id==id).manifest;
                var problem=Problem(manifest);
                if(problem.Length>0)throw new InvalidOperationException(problem);
            }
            owner.SaveExternalIntent(id,requested,Installed.Single(p=>p.manifest.id==id).manifest.version);await RefreshInstalled();Notice="Saved. Restart FLATS to apply external module changes.";
        }
        public async Task Remove(string id)
        {
            EnsureReady();
            if(Downloads.IsBusy(id))throw new InvalidOperationException("Cancel the download before removing this module");
            var profiles=owner.Profiles.Snapshot().Where(p=>p.modules.Any(m=>m.id==id&&m.requested)||p.modules.Where(m=>m.requested).Any(m=>Installed.Any(i=>i.manifest.id==m.id&&(i.manifest.dependencies??new DependencySpec[0]).Any(d=>d.id==id)))).ToArray();
            if(profiles.Length>0)throw new InvalidOperationException("Disable this module and its dependents in these profiles before uninstalling: "+string.Join(", ",profiles.Select(p=>p.name)));
            CheckDependents(id);
            await platform.Work(()=>Store.Remove(id));await RefreshInstalled();Notice="Removed from next launch. Any running version remains active until FLATS restarts.";
        }
        void CheckDependents(string id)
        {
            var dependent=Installed.FirstOrDefault(p=>p.requested && p.manifest.id!=id && (p.manifest.dependencies ?? new DependencySpec[0]).Any(d=>d.id==id));
            if(dependent!=null)throw new InvalidOperationException("Disable dependent module first: "+dependent.manifest.id);
        }
        public string Agreement()
        {
            return SessionModules.Encode(Running.Where(p=>owner.Manager.Installed.Any(r=>r.Manifest.Id==p.manifest.id&&r.Active)));
        }
        public string Problem(PackageManifest manifest)
        {
            return ModuleDiagnostics.Inspect(manifest,Installed.Select(p=>p.manifest),Installed.Where(p=>p.requested).Select(p=>p.manifest.id)
                );
        }
        readonly SemaphoreSlim artworkSlots=new SemaphoreSlim(2);
        readonly System.Collections.Generic.Dictionary<string,byte[]> artwork=new System.Collections.Generic.Dictionary<string,byte[]>();
        public async Task<byte[]> Artwork(string url,CancellationToken cancel)
        {
            EnsureReady();
            using var operation=CancellationTokenSource.CreateLinkedTokenSource(cancel,lifetimeToken);
            cancel=operation.Token;
            if(string.IsNullOrEmpty(url) || Source==null)return null;
            if(artwork.TryGetValue(url,out var cached))return cached;
            await artworkSlots.WaitAsync(cancel);
            try
            {
                EnsureAlive();
                var data=await Source.Image(url,cancel);
                EnsureAlive();cancel.ThrowIfCancellationRequested();
                // Decode only bounded PNGs. Check dimensions before the Unity image decoder allocates.
                if(data.Length<24 || data[0]!=137 || data[1]!=80 || data[2]!=78 || data[3]!=71)throw new InvalidDataException("Artwork must be PNG");
                long width=((long)data[16]<<24)|((long)data[17]<<16)|((long)data[18]<<8)|data[19];
                long height=((long)data[20]<<24)|((long)data[21]<<16)|((long)data[22]<<8)|data[23];
                if(width<1 || width>1024 || height<1 || height>1024)throw new InvalidDataException("Artwork exceeds 1024 pixels");
                if(artwork.Count>=32)artwork.Clear();artwork[url]=data;return data;
            }
            finally{artworkSlots.Release();}
        }
        public bool NeedsRestart(string id)
        {
            var installed=Installed.FirstOrDefault(p=>p.manifest.id==id);var running=Running.FirstOrDefault(p=>p.manifest.id==id);
            return installed?.sha256!=running?.sha256 || (installed?.requested ?? false)!=(running?.requested ?? false);
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;Ready=false;
            try { lifetime.Cancel(); }
            finally
            {
                Downloads?.Dispose();artwork.Clear();
                // Http cancellation runs through queue tokens; no assembly unload is claimed.
                (Source as IDisposable)?.Dispose();foreach(var source in retiredSources)source.Dispose();
                retiredSources.Clear();lifetime.Dispose();
            }
        }
    }
}
