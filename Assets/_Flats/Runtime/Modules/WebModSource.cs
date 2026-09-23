using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Flats.Modules
{
    // Browser catalogue transport. Presets use manifest data; no downloaded code is executed.
    public sealed class WebModSource : IModSource
    {
        readonly IModJson json;
        public string Identity { get; private set; }
        public WebModSource(string url, IModJson codec)
        {
            var uri=ModRules.Url(url.TrimEnd('/')+"/",false);
            if(uri.Query.Length>0 || uri.Fragment.Length>0)throw new InvalidDataException("Expected catalogue base URL");
            Identity=uri.AbsoluteUri;json=codec;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int FlatsModFetch(string url,int limit);
        [DllImport("__Internal")] static extern int FlatsModPoll(int id);
        [DllImport("__Internal")] static extern void FlatsModRead(int id,byte[] bytes);
        [DllImport("__Internal")] static extern void FlatsModRelease(int id);
#endif
        async Task<byte[]> Get(string url,CancellationToken cancel,int limit=2*1024*1024)
        {
            var uri=ModRules.Url(url,false);var origin=new Uri(Identity);
            if(uri.GetLeftPart(UriPartial.Authority)!=origin.GetLeftPart(UriPartial.Authority) ||
               !uri.AbsolutePath.StartsWith(origin.AbsolutePath,StringComparison.Ordinal) || uri.Fragment.Length>0)
                throw new InvalidDataException("Asset URL is outside the official mod service");
#if UNITY_WEBGL && !UNITY_EDITOR
            cancel.ThrowIfCancellationRequested();
            int id=FlatsModFetch(uri.AbsoluteUri,limit);
            try
            {
                int length;
                while((length=FlatsModPoll(id))==0) { cancel.ThrowIfCancellationRequested();await Task.Yield(); }
                if(length<0)throw new IOException("Official service request failed. Check connection and retry.");
                var data=new byte[length-1];FlatsModRead(id,data);return data;
            }
            finally { FlatsModRelease(id); }
#else
            await Task.CompletedTask;throw new PlatformNotSupportedException("Browser transport requires WebGL");
#endif
        }
        public async Task<CatalogPage> Browse(CatalogQuery q,CancellationToken cancel)
        {
            int limit=Math.Max(1,Math.Min(24,q.Limit));
            if(q.AllVersions && string.IsNullOrEmpty(q.Id))throw new InvalidDataException("History requires exact ID");
            string url=Identity+"v1/mods?q="+Uri.EscapeDataString(q.Search??"")+"&category="+Uri.EscapeDataString(q.Category??"")+
                "&sort="+Uri.EscapeDataString(q.Sort??"name")+"&offset="+Math.Max(0,q.Offset)+"&limit="+limit+
                "&id="+Uri.EscapeDataString(q.Id??"")+(q.AllVersions?"&versions=all":"")+
                (q.Compatible?"&game="+ModRules.GameVersion+"&api="+ModRules.ApiVersion:"");
            var page=json.Read<CatalogPage>(Encoding.UTF8.GetString(await Get(url,cancel)));
            if(page==null || page.schema!=1 || page.items==null || page.items.Length>limit || page.total<0 || page.offset!=q.Offset || page.categories==null || page.categories.Length>128)
                throw new InvalidDataException("Invalid catalogue response");
            var ids=new System.Collections.Generic.HashSet<string>();
            foreach(var item in page.items)
            {
                item.Validate(false);
                if(!string.IsNullOrEmpty(q.Id)&&item.manifest.id!=q.Id)throw new InvalidDataException("Catalogue ID differs");
                if(!ids.Add(item.manifest.id+(q.AllVersions?"@"+item.manifest.version:"")))throw new InvalidDataException("Duplicate catalogue ID");
            }
            foreach(var category in page.categories)ModRules.Text(category,40,"category",true);
            return page;
        }
        public Task<byte[]> Image(string url,CancellationToken cancel) { return Get(url,cancel); }
        public static void ValidatePackage(PackageManifest manifest)
        {
            string problem=ModRules.Compatibility(manifest);
            if(problem.Length>0)throw new InvalidDataException(problem);
            if(manifest.kind!="crosshair" || manifest.scope!="ClientOnly" || !string.IsNullOrEmpty(manifest.assembly) || !string.IsNullOrEmpty(manifest.entryType))
                throw new PlatformNotSupportedException("This package requires desktop FLATS. Web supports client-only crosshair data packages.");
        }
        public async Task Download(CatalogItem item,string destination,Action<long> progress,CancellationToken cancel)
        {
            item.Validate(false);ValidatePackage(item.manifest);
            // Browser downloads restart after pause; installation still uses the shared
            // archive size/hash/manifest checks and atomic package transaction.
            var bytes=await Get(item.downloadUrl,cancel,(int)Math.Min(item.bytes,ModRules.MaxArchive));
            cancel.ThrowIfCancellationRequested();
            if(bytes.LongLength!=item.bytes)throw new InvalidDataException("Download size differs from the catalogue");
            File.WriteAllBytes(destination,bytes);progress?.Invoke(bytes.LongLength);
        }
    }
}
