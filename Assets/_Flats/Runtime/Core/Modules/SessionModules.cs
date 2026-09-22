using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Flats.Modules
{
    public static class SessionModules
    {
        public const string Property = "FM1";
        public static string Encode(IEnumerable<InstalledPackage> active)
        {
            var all=active.ToDictionary(x=>x.manifest.id,StringComparer.Ordinal);
            var required=new HashSet<string>(StringComparer.Ordinal);
            Action<string> visit=null;
            visit=id=> { if(!required.Add(id))return; if(!all.TryGetValue(id,out var p))throw new InvalidDataException("Active dependency is absent: "+id);
                foreach(var d in p.manifest.dependencies ?? new DependencySpec[0]) if(!d.id.StartsWith("flats.",StringComparison.Ordinal))visit(d.id); };
            foreach(var p in all.Values.Where(x=>x.manifest.scope=="RequiredForSession"))visit(p.manifest.id);
            var text=string.Join("\n",required.OrderBy(x=>x,StringComparer.Ordinal).Select(id=>id+"|"+all[id].manifest.version+"|"+all[id].sha256));
            if(required.Count>64 || text.Length>16000)throw new InvalidDataException("Too many required room modules");
            return text;
        }
        public static string Compare(string room, string local)
        {
            try
            {
                var wanted=Parse(room ?? ""); var have=Parse(local ?? "");
                var errors=new List<string>();
                foreach(var pair in wanted)
                {
                    if(!have.TryGetValue(pair.Key,out var value))errors.Add("Required: "+pair.Key+" "+pair.Value.Split('|')[0]);
                    else if(value!=pair.Value)errors.Add("Version/content differs: "+pair.Key+" (room "+pair.Value.Split('|')[0]+")");
                }
                foreach(var id in have.Keys.Except(wanted.Keys))errors.Add("Disable session module: "+id);
                return string.Join("\n",errors);
            }
            catch(Exception) { return "Room module requirements are invalid or use an unsupported protocol."; }
        }
        static Dictionary<string,string> Parse(string value)
        {
            if(value.Length>16000)throw new InvalidDataException();
            var result=new Dictionary<string,string>(StringComparer.Ordinal);
            if(value.Length==0)return result;
            var lines=value.Split('\n');if(lines.Length>64)throw new InvalidDataException();
            foreach(var line in lines)
            {
                var parts=line.Split('|');if(parts.Length!=3)throw new InvalidDataException();
                ModRules.Id(parts[0]);ModRules.Version(parts[1]);ModRules.Hash(parts[2]);result.Add(parts[0],parts[1]+"|"+parts[2]);
            }
            return result;
        }
    }
}
