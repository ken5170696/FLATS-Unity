using System;
using System.Collections.Generic;
using System.Linq;

namespace Flats.Modules
{
    public static class ModuleDiagnostics
    {
        public static string Inspect(PackageManifest selected, IEnumerable<PackageManifest> installed, IEnumerable<string> requested)
        {
            var all=installed.ToArray();var enabled=new HashSet<string>(requested);var visiting=new HashSet<string>();
            Func<PackageManifest,string> visit=null;
            visit=m=>
            {
                var error=ModRules.Compatibility(m);if(error.Length>0)return error;
                if(!visiting.Add(m.id))return "Cyclic dependency: "+m.id;
                foreach(var d in m.dependencies ?? new DependencySpec[0])
                {
                    var matches=all.Where(p=>p.id==d.id).ToArray();
                    if(matches.Length!=1)return "Missing or duplicate dependency: "+d.id;
                    if(!ModRules.Range(d.minimum,d.maximum).Contains(ModRules.Version(matches[0].version)))return "Incompatible dependency: "+d.id;
                    var problem=visit(matches[0]);if(problem.Length>0)return problem;
                    if(!enabled.Contains(d.id))return "Enable dependency first: "+d.id;
                }
                visiting.Remove(m.id);return "";
            };
            try
            {
                string issue=visit(selected);if(issue.Length>0)return issue;
                foreach(var other in all.Where(m=>m.id!=selected.id && enabled.Contains(m.id)))
                    if((selected.conflicts ?? new string[0]).Contains(other.id) || (other.conflicts ?? new string[0]).Contains(selected.id) || (ModRules.IsCrosshairProvider(selected) && ModRules.IsCrosshairProvider(other)))
                        return "Conflict: "+other.id;
                return "";
            }
            catch(Exception e){return e.Message;}
        }
    }
}
