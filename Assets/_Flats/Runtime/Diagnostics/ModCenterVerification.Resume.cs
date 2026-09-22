using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flats.Modules;
using UnityEngine.UI;

public sealed partial class ModCenterVerification
{
    // Controlled slow transport exercises the real queue and UI. HTTP byte-range
    // correctness is independently covered by the core tests and production source.
    sealed class SlowQueueSource : IModSource
    {
        public string Identity { get; set; }
        public Task<CatalogPage> Browse(CatalogQuery query,CancellationToken token){throw new NotSupportedException();}
        public Task<byte[]> Image(string url,CancellationToken token){throw new NotSupportedException();}
        public async Task Download(CatalogItem item,string destination,Action<long> progress,CancellationToken token)
        {
            File.WriteAllBytes(destination,new byte[128]);progress(128);
            await Task.Delay(-1,token);
        }
    }
    async Task ResumeUI()
    {
        if(!UnityEngine.Application.isEditor)throw new InvalidOperationException("Controlled transport UI test requires Editor isolation");
        await Service.ConfigureSource("http://127.0.0.1:8877/");
        var catalogue=await Service.Source.Browse(new CatalogQuery(),CancellationToken.None);
        var item=catalogue.items[0];
        Service.Downloads.Enqueue(item,new SlowQueueSource{Identity=Service.Source.Identity});
        await Until(()=>Service.Downloads.Snapshot().Single().State==DownloadState.Downloading);
        await MainClick(1,"Modules");await Click("Downloads");await Until(()=>page.GetComponentsInChildren<Button>().Any(b=>b.name=="QueueAction"));
        Check("real pause label",Button("QueueAction").GetComponentInChildren<Text>().text=="Pause");
        await Click("QueueAction");await Until(()=>Service.Downloads.Snapshot().Single().State==DownloadState.Paused);
        await Task.Delay(400);Check("continue label",Button("QueueAction").GetComponentInChildren<Text>().text=="Continue");
        Check("partial preserved",File.Exists(Path.Combine(Service.Store.Root,"downloads",item.manifest.id+".partial")));
        await Capture("download-paused");
        await Click("QueueAction");await Until(()=>Service.Downloads.Snapshot().Single().State==DownloadState.Downloading);
        await Click("QueueCancel");await Until(()=>Service.Downloads.Snapshot().Single().State==DownloadState.Cancelled);
        Check("cancel removes partial",!File.Exists(Path.Combine(Service.Store.Root,"downloads",item.manifest.id+".partial")));
        await Capture("download-cancelled");
    }
}
