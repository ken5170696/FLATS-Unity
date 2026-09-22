using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;
using Flats.Modules;
using UnityEngine;

public sealed partial class ModCenterVerification
{
    sealed class DependencyFixtureSource : IModSource
    {
        public string Identity=>"https://dependency-tests.invalid/";
        public readonly List<CatalogItem> Items=new List<CatalogItem>();
        public readonly Dictionary<string,string> Files=new Dictionary<string,string>();
        public Task<CatalogPage> Browse(CatalogQuery q,CancellationToken c)
        {
            var items=Items.Where(i=>string.IsNullOrEmpty(q.Id)||i.manifest.id==q.Id).ToArray();
            return Task.FromResult(new CatalogPage{schema=1,total=items.Length,offset=q.Offset,categories=new[]{"Verification"},items=items.Skip(q.Offset).Take(q.Limit).ToArray()});
        }
        public Task<byte[]> Image(string u,CancellationToken c){throw new NotSupportedException();}
        public async Task Download(CatalogItem item,string destination,Action<long> progress,CancellationToken cancel)
        {await Task.Delay(400,cancel);File.Copy(Files[item.manifest.id],destination,true);progress(item.bytes);}
    }
    async Task DependenciesUI()
    {
        if(!Application.isEditor)throw new InvalidOperationException("Fixture transport requires isolated Editor");
        if(phase=="DependenciesRestart")
        {
            Check("both managed modules actually active after restart",BuiltinModules.Instance.Manager.Installed.Count(r=>r.Active&&r.Manifest.Id.StartsWith("verification."))==2);
            Check("agreement includes dependency closure",Service.Agreement().Contains("verification.base|1.0.0|")&&Service.Agreement().Contains("verification.session|1.0.0|"));return;
        }
        var source=new DependencyFixtureSource();
        string fixtures=Environment.GetEnvironmentVariable("FLATS_TEST_FIXTURES") ?? throw new InvalidOperationException("Set FLATS_TEST_FIXTURES to the generated dependency fixture directory");
        foreach(var slug in new[]{"base","session"})
        {
            string archive=Path.Combine(fixtures,slug+"-1.0.0-normal.zip");PackageManifest manifest;
            using(var zip=ZipFile.OpenRead(archive))using(var reader=new StreamReader(zip.GetEntry("manifest.json").Open()))manifest=JsonUtility.FromJson<PackageManifest>(reader.ReadToEnd());
            string hash;using(var sha=SHA256.Create())using(var file=File.OpenRead(archive))hash=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();
            source.Items.Add(new CatalogItem{manifest=manifest,sha256=hash,bytes=new FileInfo(archive).Length,downloadUrl=source.Identity+slug+".zip"});source.Files[manifest.id]=archive;
        }
        typeof(ModCenterService).GetProperty("Source").SetValue(Service,source);
        await MainClick(1,"Modules");await Click("Explore");await Until(()=>page.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name=="Mod-verification.session"));
        await Click("Mod-verification.session");await Click("Primary");await Capture("dependency-plan");
        Check("plan lists main and required versions",page.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text.Contains("verification.base  1.0.0")&&t.text.Contains("verification.session  1.0.0")));
        await Click("CancelAction");Check("cancel leaves library and queue unchanged",Service.Installed.Length==0&&Service.Downloads.Snapshot().Length==0);
        await Click("Primary");await Click("ConfirmAction");
        await Until(()=>Service.Downloads.Snapshot().Any(j=>j.State==DownloadState.Installed));await Service.RefreshInstalled();
        Check("whole dependency set installed disabled",Service.Installed.Length==2&&Service.Installed.All(p=>!p.requested));
        await Click("Installed");await Click("Details-verification.session");await Click("Primary");await Capture("dependency-enable-plan");await Click("ConfirmAction");
        await Until(()=>Service.Installed.All(p=>p.requested));
        Check("both enable intents saved atomically",BuiltinModules.Instance.Profiles.Requested("verification.base")&&BuiltinModules.Instance.Profiles.Requested("verification.session"));
        Check("current runtime unchanged pending restart",Service.Running.Length==0&&Service.NeedsRestart("verification.session"));
        await Capture("dependency-restart-required");
    }
}
