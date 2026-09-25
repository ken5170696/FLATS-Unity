using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http.Headers;

namespace Flats.Modules
{
    // Every asset must remain under the configured official origin and base path.
    public sealed class HttpModSource : IModSource, IDisposable
    {
        readonly HttpClient client;
        readonly IModJson json;
        readonly bool loopback;
        public string Identity { get; private set; }
        public HttpModSource(string url, IModJson codec, bool allowLoopback = false) : this(url,codec,allowLoopback,null) { }
        internal HttpModSource(string url, IModJson codec, bool allowLoopback, HttpMessageHandler handler)
        {
            Identity = ModRules.Url(url.TrimEnd('/') + "/", allowLoopback).AbsoluteUri; json = codec; loopback = allowLoopback;
            if(new Uri(Identity).Query.Length>0)throw new InvalidDataException("Use a public catalogue base URL without query parameters or credentials");
            client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.None });
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FLATS-ModCenter/1.0");
        }
        async Task Transfer(string url, Stream destination, long limit, long? exact, Action<long> progress, CancellationToken cancel)
        {
            var uri = OfficialUri(url);
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(90));
                try
                {
                    using (var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                    {
                        if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400) throw new InvalidDataException("Source redirected; configure the final HTTPS URL");
                        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Source returned HTTP " + (int)response.StatusCode);
                        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Response exceeds the size limit");
                        using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        {
                            long total = 0; var buffer = new byte[65536]; int read;
                            while ((read = await input.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) > 0)
                            {
                                total += read;
                                if (total > limit || (exact.HasValue && total > exact.Value)) throw new InvalidDataException("Response exceeds declared size");
                                await destination.WriteAsync(buffer, 0, read, timeout.Token).ConfigureAwait(false);
                                progress?.Invoke(total);
                            }
                            if (exact.HasValue && total != exact.Value) throw new InvalidDataException("Download was interrupted or incomplete");
                        }
                    }
                }
                catch (OperationCanceledException) when (!cancel.IsCancellationRequested) { throw new IOException("Source timed out after 90 seconds. Check connection and retry."); }
            }
        }
        public async Task<CatalogPage> Browse(CatalogQuery q, CancellationToken cancel)
        {
            int limit = Math.Max(1, Math.Min(24, q.Limit));
            if(q.AllVersions && string.IsNullOrEmpty(q.Id))throw new InvalidDataException("History requires an exact ID");
            if(!string.IsNullOrEmpty(q.Id))ModRules.Id(q.Id);
            string url = Identity + "v1/mods?q=" + Uri.EscapeDataString(q.Search ?? "") + "&category=" + Uri.EscapeDataString(q.Category ?? "") +
                "&sort=" + Uri.EscapeDataString(q.Sort) + "&offset=" + Math.Max(0, q.Offset) + "&limit=" + limit +
                "&id="+Uri.EscapeDataString(q.Id)+(q.AllVersions?"&versions=all":"")+
                (q.Compatible ? "&game=" + ModRules.GameVersion + "&api=" + ModRules.ApiVersion : "");
            using (var buffer = new MemoryStream())
            {
                await Transfer(url, buffer, 2 * 1024 * 1024, null, null, cancel).ConfigureAwait(false);
                var page = json.Read<CatalogPage>(Encoding.UTF8.GetString(buffer.ToArray()));
                if (page == null || page.schema != 1 || page.items == null || page.items.Length > limit || page.total < 0 || page.offset != q.Offset || page.categories == null || page.categories.Length > 128)
                    throw new InvalidDataException("Invalid catalogue response");
                var ids = new System.Collections.Generic.HashSet<string>();
                foreach (var item in page.items) { item.Validate(loopback); OfficialUri(item.downloadUrl); if(!string.IsNullOrEmpty(item.imageUrl))OfficialUri(item.imageUrl); if(!string.IsNullOrEmpty(q.Id)&&item.manifest.id!=q.Id)throw new InvalidDataException("Catalogue ID differs"); if (!ids.Add(item.manifest.id+(q.AllVersions?"@"+item.manifest.version:""))) throw new InvalidDataException("Duplicate catalogue ID"); }
                foreach (var category in page.categories) ModRules.Text(category, 40, "category", true);
                return page;
            }
        }
        public async Task Download(CatalogItem item, string destination, Action<long> progress, CancellationToken cancel)
        {
            item.Validate(loopback);
            var uri=OfficialUri(item.downloadUrl);
            // A partial belongs to exactly one immutable catalogue identity. Never append
            // without a strong validator supplied by the server that wrote those bytes.
            string metadata=destination+".identity", validator=destination+".etag";
            PackageStore.RejectLinks(Path.GetDirectoryName(Path.GetFullPath(destination)));
            foreach(var file in new[]{destination,metadata,validator})
                if(File.Exists(file)&&(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked download files are not supported");
            string identity=Identity+"\n"+item.downloadUrl+"\n"+item.sha256+"\n"+item.bytes;
            long offset=File.Exists(destination)?new FileInfo(destination).Length:0;
            string tag=File.Exists(validator)?File.ReadAllText(validator):"";
            if(!File.Exists(metadata)||File.ReadAllText(metadata)!=identity||offset>item.bytes||
               !EntityTagHeaderValue.TryParse(tag,out var etag)||etag.IsWeak)
            {
                // Invalidate before recording a new identity: a failed request must
                // never leave old bytes looking like a partial for a different URL.
                File.WriteAllText(validator,"");
                using(var empty=new FileStream(destination,FileMode.Create,FileAccess.Write)){}
                offset=0;tag="";
            }
            File.WriteAllText(metadata,identity);
            using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(90));
                try
                {
                    for(int attempt=0;attempt<2;attempt++)
                    using(var request=new HttpRequestMessage(HttpMethod.Get,uri))
                    {
                        if(offset>0){request.Headers.Range=new RangeHeaderValue(offset,null);request.Headers.TryAddWithoutValidation("If-Range",tag);}
                        using(var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false))
                        {
                            if(response.StatusCode==HttpStatusCode.RequestedRangeNotSatisfiable && offset>0)
                            { offset=0;tag="";continue; }
                            if(response.StatusCode!=HttpStatusCode.OK && response.StatusCode!=HttpStatusCode.PartialContent)
                                throw new HttpRequestException("Source returned HTTP "+(int)response.StatusCode);
                            bool append=response.StatusCode==HttpStatusCode.PartialContent;
                            if(append)
                            {
                                var range=response.Content.Headers.ContentRange;
                                if(offset==0||range==null||range.Unit!="bytes"||range.From!=offset||range.To!=item.bytes-1||range.Length!=item.bytes||
                                   response.Headers.ETag==null||response.Headers.ETag.IsWeak||response.Headers.ETag.ToString()!=tag)
                                {
                                    File.WriteAllText(validator,"");
                                    throw new InvalidDataException("Invalid resume response; retry starts a fresh download");
                                }
                            }
                            else offset=0; // Range ignored or representation changed: replace, never concatenate.
                            if(response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength!=item.bytes-offset)
                                throw new InvalidDataException("Download size differs from the catalogue");
                            using(var output=new FileStream(destination,append?FileMode.Append:FileMode.Create,FileAccess.Write,FileShare.None,65536,true))
                            {
                                // Persist the validator only after the old partial has been truncated.
                                File.WriteAllText(validator,response.Headers.ETag!=null&&!response.Headers.ETag.IsWeak?response.Headers.ETag.ToString():"");
                                progress?.Invoke(offset);
                                using(var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                                {
                                    var buffer=new byte[65536];int read;
                                    while((read=await input.ReadAsync(buffer,0,buffer.Length,timeout.Token).ConfigureAwait(false))>0)
                                    {
                                        if(offset+read>item.bytes)throw new InvalidDataException("Response exceeds declared size");
                                        await output.WriteAsync(buffer,0,read,timeout.Token).ConfigureAwait(false);
                                        offset+=read;progress?.Invoke(offset);
                                    }
                                }
                                if(offset!=item.bytes)throw new IOException("Download interrupted; retry to resume");
                            }
                            return;
                        }
                    }
                    throw new IOException("Source could not resume the download");
                }
                catch(OperationCanceledException) when(!cancel.IsCancellationRequested){throw new IOException("Source timed out; retry to resume");}
            }
        }
        public async Task<byte[]> Image(string url, CancellationToken cancel)
        {
            using (var data = new MemoryStream())
            {
                await Transfer(url, data, 2 * 1024 * 1024, null, null, cancel).ConfigureAwait(false);
                return data.ToArray();
            }
        }
        public void Dispose() { client.Dispose(); }
        public Uri OfficialUri(string url)
        {
            var uri=ModRules.Url(url,loopback);var origin=new Uri(Identity);
            if(uri.Scheme!=origin.Scheme || uri.Host!=origin.Host || uri.Port!=origin.Port ||
               !uri.AbsolutePath.StartsWith(origin.AbsolutePath,StringComparison.Ordinal) || uri.Fragment.Length>0)
                throw new InvalidDataException("Asset URL is outside the official mod service");
            return uri;
        }
    }
}
