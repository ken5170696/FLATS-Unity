using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Flats.Modules
{
    public enum DownloadState { Queued, Downloading, Verifying, Installed, Cancelled, Failed, Paused }
    public sealed class DownloadSnapshot
    {
        public string Id, Name, Version, Error;
        public long Received, Total;
        public DownloadState State;
        public bool Busy { get { return State == DownloadState.Queued || State == DownloadState.Downloading || State == DownloadState.Verifying; } }
    }
    // One bounded worker; ID reservation spans download, validation and atomic commit.
    public sealed class DownloadQueue : IDisposable
    {
        sealed class Job
        {
            public CatalogItem Item; public CatalogItem[] Items; public IModSource Source;
            public CancellationTokenSource Cancel = new CancellationTokenSource();
            public DownloadSnapshot Status;
            public IDisposable Reservation;
            public bool PauseRequested;
        }
        [Serializable] public sealed class SavedJob { public int schema=1; public CatalogItem item; public CatalogItem[] items; public string source; public DownloadState state; }
        readonly object gate = new object();
        readonly List<Job> jobs = new List<Job>();
        readonly SemaphoreSlim worker = new SemaphoreSlim(1);
        readonly PackageStore store;
        readonly string directory;
        bool disposed;
        public int Revision { get; private set; }
        public DownloadQueue(PackageStore storage, IModSource source=null)
        {
            store=storage;directory=Path.Combine(store.Root,"downloads");
            Directory.CreateDirectory(directory);PackageStore.RejectLinks(directory);
            if(source==null)return;
            foreach(var file in Directory.GetFiles(directory,"*.json"))
            try
            {
                if(new FileInfo(file).Length>8*1024*1024)throw new InvalidDataException("Queue record too large");
                var saved=store.Codec.Read<SavedJob>(File.ReadAllText(file));
                if(saved==null||saved.schema!=1||saved.source!=source.Identity)continue;
                saved.item.Validate(new Uri(source.Identity).IsLoopback);
                if(source is HttpModSource http){http.OfficialUri(saved.item.downloadUrl);}
                if(Path.GetFileName(file)!=saved.item.manifest.id+".json")throw new InvalidDataException("Queue identity differs");
                if(saved.state==DownloadState.Cancelled||saved.state==DownloadState.Installed)continue;
                if(jobs.Count>=64){store.Notices.Add("Download recovery limit reached; remaining records retained.");break;}
                var job=Create(saved.item,source,saved.items);job.Status.State=DownloadState.Paused;
                job.Status.Received=job.Items.Sum(i=>File.Exists(Archive(job,i))?new FileInfo(Archive(job,i)).Length:0);
                job.Status.Error="Recovered after restart. Continue when ready.";jobs.Add(job);
            }
            catch(Exception e){store.Notices.Add("Could not recover download: "+e.Message);}
        }
        Job Create(CatalogItem item,IModSource source,CatalogItem[] items=null)
        {
            items=items??new[]{item};
            if(items.Length==0||items.Length>64||items.Select(i=>i.manifest.id).Distinct().Count()!=items.Length)throw new InvalidDataException("Invalid download plan");
            foreach(var i in items){i.Validate(new Uri(source.Identity).IsLoopback);if(source is HttpModSource http)http.OfficialUri(i.downloadUrl);}
            return new Job{Item=item,Items=items,Source=source,Status=new DownloadSnapshot{Id=item.manifest.id,Name=item.manifest.name+(items.Length>1?" + dependencies":""),Version=item.manifest.version,Total=items.Sum(i=>i.bytes),State=DownloadState.Queued,Error=""}};
        }
        sealed class Reservations : IDisposable
        {
            public readonly List<IDisposable> Values=new List<IDisposable>();
            public void Dispose(){foreach(var value in Values)value.Dispose();}
        }
        string SafeFile(string name)
        {
            PackageStore.RejectLinks(directory);var path=Path.Combine(directory,name);
            if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked download files are not supported");
            return path;
        }
        string Archive(Job j) { return Archive(j,j.Items[0]); }
        string Archive(Job j,CatalogItem item)
        {
            if(j.Items.Length==1)return SafeFile(j.Status.Id+".partial");
            var folder=SafeFile("plan-"+j.Status.Id);Directory.CreateDirectory(folder);PackageStore.RejectLinks(folder);
            return SafeFile("plan-"+j.Status.Id+"/"+item.manifest.id+".partial");
        }
        void Save(Job j)
        {
            var path=SafeFile(j.Status.Id+".json");var temp=SafeFile(j.Status.Id+".json.new");
            if(j.Status.State==DownloadState.Cancelled||j.Status.State==DownloadState.Installed){if(File.Exists(path))File.Delete(path);return;}
            var bytes=System.Text.Encoding.UTF8.GetBytes(store.Codec.Write(new SavedJob{item=j.Item,items=j.Items,source=j.Source.Identity,state=j.Status.State}));
            if(bytes.Length>8*1024*1024)throw new InvalidDataException("Download plan is too large to persist");
            using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
        }
        void Cleanup(Job j)
        {
            foreach(var item in j.Items)foreach(var suffix in new[]{"",".etag",".identity"}){var file=Archive(j,item)+suffix;if(File.Exists(file))File.Delete(file);}
        }
        public DownloadSnapshot[] Snapshot()
        {
            lock (gate) return jobs.Select(j => new DownloadSnapshot { Id=j.Status.Id, Name=j.Status.Name, Version=j.Status.Version,
                Received=j.Status.Received, Total=j.Status.Total, State=j.Status.State, Error=j.Status.Error }).ToArray();
        }
        public bool IsBusy(string id) { lock (gate) return jobs.Any(j => (j.Status.Id==id || j.Items.Any(i=>i.manifest.id==id)) && j.Status.Busy); }
        public void Enqueue(CatalogItem item, IModSource source) { Enqueue(item,source,new[]{item}); }
        public void Enqueue(CatalogItem item, IModSource source,CatalogItem[] items)
        {
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException("DownloadQueue");
                ModRules.Id(item.manifest.id);
                if (IsBusy(item.manifest.id)) throw new InvalidOperationException("This module already has an operation in progress");
                if (jobs.Count(j => j.Status.Busy) >= 16) throw new InvalidOperationException("Queue is full (16 downloads). Wait for a download to finish.");
                jobs.RemoveAll(j=>j.Status.State==DownloadState.Installed||j.Status.State==DownloadState.Cancelled);
                if(jobs.Count>=64&&!jobs.Any(j=>j.Status.Id==item.manifest.id))throw new InvalidOperationException("Cancel or finish an existing download before adding more (64 saved jobs).");
                var job=Create(item,source,items);
                var reserved=new Reservations();job.Reservation=reserved;
                try{foreach(var id in items.Select(i=>i.manifest.id).Concat(new[]{item.manifest.id}).Distinct())reserved.Values.Add(store.Reserve(id));}
                catch{reserved.Dispose();throw;}
                try{Save(job);}catch{job.Reservation.Dispose();throw;}
                jobs.RemoveAll(j=>j.Status.Id==item.manifest.id);
                jobs.Add(job); Revision++;
                _ = Task.Run(() => Run(job));
            }
        }
        public void Pause(string id) { lock(gate){var j=jobs.First(x=>x.Status.Id==id);if(j.Status.State==DownloadState.Queued||j.Status.State==DownloadState.Downloading){j.PauseRequested=true;j.Cancel.Cancel();}} }
        public void Cancel(string id)
        {
            lock(gate)
            {
                var j=jobs.FirstOrDefault(x=>x.Status.Id==id);if(j==null)return;
                if(j.Status.Busy){j.PauseRequested=false;j.Cancel.Cancel();}
                else if(j.Status.State!=DownloadState.Installed){Cleanup(j);Set(j,DownloadState.Cancelled,"Cancelled; partial removed.");}
            }
        }
        public void Retry(string id)
        {
            lock (gate)
            {
                var j = jobs.First(x => x.Status.Id == id);
                if (j.Status.State != DownloadState.Failed && j.Status.State != DownloadState.Cancelled && j.Status.State!=DownloadState.Paused) throw new InvalidOperationException("This job cannot be retried");
                Enqueue(j.Item, j.Source,j.Items);
            }
        }
        void Set(Job j, DownloadState state, string error = "") { lock (gate) { j.Status.State=state; j.Status.Error=error;Save(j); Revision++; } }
        async Task Run(Job j)
        {
            string staging = null; bool entered = false;var final=DownloadState.Failed;string error="";
            try
            {
                await worker.WaitAsync(j.Cancel.Token).ConfigureAwait(false); entered=true;
                long completed=0;var archives=new List<string>();
                foreach(var item in j.Items)
                {
                    string archive=Archive(j,item);archives.Add(archive);
                    for(int attempt=0;;attempt++)
                    {
                        Set(j,DownloadState.Downloading);
                        try{await j.Source.Download(item,archive,n=>{lock(gate)j.Status.Received=completed+n;},j.Cancel.Token).ConfigureAwait(false);break;}
                        catch(HttpRequestException)when(attempt<2){await Task.Delay(500*(attempt+1),j.Cancel.Token).ConfigureAwait(false);}
                    }
                    completed+=item.bytes;
                }
                Set(j,DownloadState.Verifying);
                store.InstallBatch(j.Items,archives.ToArray(),j.Source.Identity,j.Cancel.Token);
                final=DownloadState.Installed;
            }
            catch (OperationCanceledException) { final=j.PauseRequested?DownloadState.Paused:DownloadState.Cancelled;error=j.PauseRequested?"Paused. Continue to resume the saved download.":"Cancelled. Previous installation is unchanged."; }
            catch (Exception e) { error=e.Message; }
            finally
            {
                if(staging!=null)try{store.EndStaging(staging);}catch(Exception e){lock(gate)j.Status.Error += " Cleanup deferred: "+e.Message;}
                if(entered)worker.Release();
                lock(gate)
                {
                    if(final==DownloadState.Paused&&!j.PauseRequested)final=DownloadState.Cancelled;
                    j.Reservation.Dispose();
                    if(final==DownloadState.Installed||final==DownloadState.Cancelled)try{Cleanup(j);}catch(Exception e){error+=" Cleanup deferred: "+e.Message;}
                    try{Set(j,final,error);}
                    catch(Exception e){j.Status.State=final;j.Status.Error=error+" Queue record could not be saved: "+e.Message;Revision++;}
                }
            }
        }
        public void Dispose() { lock(gate) { disposed=true; foreach(var j in jobs) if(j.Status.Busy){j.PauseRequested=true;j.Cancel.Cancel();} } }
    }
}
