using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flats.Modules
{
    public sealed class DependencyPlan
    {
        public string RootId;
        public CatalogItem[] Downloads;
        public PackageManifest[] Modules;
        public string[] Enable;
        public long Bytes { get { return Downloads.Sum(i=>i.bytes); } }
    }
    // Bounded backtracking: prefer an installed compatible version, then newest
    // official version. A later diamond constraint may undo an earlier choice.
    public sealed class DependencyPlanner
    {
        readonly IModSource source;
        readonly InstalledPackage[] installed;
        readonly HashSet<string> enabled;
        readonly Dictionary<string,CatalogItem[]> history=new Dictionary<string,CatalogItem[]>();
        int attempts;
        string lastError="No compatible dependency plan";
        sealed class Choice { public PackageManifest Manifest; public CatalogItem Download; }
        public DependencyPlanner(IModSource source,InstalledPackage[] installed,IEnumerable<string> enabled)
        { this.source=source;this.installed=installed;this.enabled=new HashSet<string>(enabled); }
        public async Task<DependencyPlan> Resolve(PackageManifest root,CatalogItem download,CancellationToken cancel)
        {
            root.Validate();attempts=0;
            var previous=installed.FirstOrDefault(p=>p.manifest.id==root.id);
            if(previous!=null && ModRules.Version(root.version)<ModRules.Version(previous.manifest.version))throw new InvalidOperationException("Downgrade refused");
            if(previous!=null && download!=null && root.version==previous.manifest.version && download.sha256!=previous.sha256)throw new InvalidOperationException("Same version has a different digest");
            var chosen=new Dictionary<string,Choice>();
            // Preserve already enabled modules: dependencies may update, but only
            // when the whole enabled set still satisfies its version constraints.
            var required=new HashSet<string>(enabled);required.Add(root.id);
            chosen[root.id]=new Choice{Manifest=root,Download=download};
            var result=await Search(chosen,required,cancel);
            if(result==null)throw new InvalidOperationException(lastError);
            var closure=new HashSet<string>();Action<string> visit=null;
            visit=id=>{if(!closure.Add(id))return;foreach(var d in result[id].Manifest.dependencies??new DependencySpec[0])visit(d.id);};visit(root.id);
            return new DependencyPlan{RootId=root.id,Modules=result.Values.Select(c=>c.Manifest).ToArray(),
                Downloads=result.Values.Where(c=>c.Download!=null && !installed.Any(p=>p.manifest.id==c.Manifest.id && p.sha256==c.Download.sha256)).Select(c=>c.Download).ToArray(),Enable=closure.ToArray()};
        }
        async Task<Dictionary<string,Choice>> Search(Dictionary<string,Choice> chosen,HashSet<string> required,CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            if(++attempts>4096 || required.Count>64)throw new InvalidOperationException("Dependency plan exceeds the complexity limit");
            var allRequired=new HashSet<string>(required);
            foreach(var choice in chosen.Values)
            {
                string issue=ModRules.Compatibility(choice.Manifest);
                if(issue.Length>0){lastError=choice.Manifest.id+": "+issue;return null;}
                foreach(var dep in choice.Manifest.dependencies??new DependencySpec[0])
                {
                    allRequired.Add(dep.id);
                    string version=chosen.TryGetValue(dep.id,out var target)?target.Manifest.version:null;
                    if(version!=null && !ModRules.Range(dep.minimum,dep.maximum).Contains(ModRules.Version(version)))
                    {lastError="Incompatible dependency: "+dep.id+" required by "+choice.Manifest.id;return null;}
                }
            }
            string next=allRequired.OrderBy(id=>id,StringComparer.Ordinal).FirstOrDefault(id=>!chosen.ContainsKey(id));
            if(next!=null)
            {
                var existing=installed.FirstOrDefault(p=>p.manifest.id==next);
                if(existing!=null)
                {
                    var branch=new Dictionary<string,Choice>(chosen){[next]=new Choice{Manifest=existing.manifest}};
                    var answer=await Search(branch,allRequired,cancel);if(answer!=null)return answer;
                }
                foreach(var candidate in await Candidates(next,cancel))
                {
                    var branch=new Dictionary<string,Choice>(chosen){[next]=candidate};
                    var answer=await Search(branch,allRequired,cancel);if(answer!=null)return answer;
                }
                lastError="Cannot resolve "+next+". "+lastError;return null;
            }
            // The diagnostic traversal rejects cycles and conflicts in both directions.
            var active=allRequired.Concat(enabled).Distinct().ToArray();
            foreach(var c in chosen.Values)
            {
                string error=ModuleDiagnostics.Inspect(c.Manifest,chosen.Values.Select(v=>v.Manifest),active);
                if(error.Length>0){lastError=error;return null;}
            }
            return chosen;
        }
        async Task<Choice[]> Candidates(string id,CancellationToken cancel)
        {
            var old=installed.FirstOrDefault(p=>p.manifest.id==id);
            var result=new List<Choice>();
            if(source==null)return result.ToArray();
            if(!history.TryGetValue(id,out var versions))
            {
                if(history.Count>=64)throw new InvalidOperationException("Dependency catalogue limit exceeded");
                var items=new List<CatalogItem>();int offset=0;
                do
                {
                    var page=await source.Browse(new CatalogQuery{Id=id,AllVersions=true,Compatible=true,Offset=offset,Limit=24},cancel);
                    if(page.total>128 || page.items.Any(i=>i.manifest.id!=id))throw new InvalidDataException("Invalid dependency history");
                    items.AddRange(page.items);offset+=page.items.Length;
                    if(offset>=page.total)break;
                    if(page.items.Length==0)throw new InvalidDataException("Incomplete dependency history");
                }while(true);
                if(items.Select(i=>i.manifest.version).Distinct().Count()!=items.Count)throw new InvalidDataException("Duplicate dependency version");
                versions=items.ToArray();history[id]=versions;
            }
            foreach(var item in versions.OrderByDescending(i=>ModRules.Version(i.manifest.version)))
            {
                if(old!=null && (ModRules.Version(item.manifest.version)<ModRules.Version(old.manifest.version) ||
                    (item.manifest.version==old.manifest.version)))continue;
                result.Add(new Choice{Manifest=item.manifest,Download=item});
            }
            return result.ToArray();
        }
    }
}
