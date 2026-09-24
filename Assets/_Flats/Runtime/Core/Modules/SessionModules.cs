using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Flats.Modules
{
    public static class SessionModules
    {
        public const string Property = "FM1";
        // Short lobby-visible digest of the FM1 agreement. Random matchmaking filters on it so
        // players are only matched into rooms whose required modules they already have.
        public const string DigestProperty = "FMH";
        public static string Digest(string agreement)
        {
            if (string.IsNullOrEmpty(agreement)) return "none";
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(agreement));
                return string.Concat(hash.Take(8).Select(b => b.ToString("x2")));
            }
        }
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
        public sealed class Requirement { public string Id, Version, Sha256; }
        // The exact packages a room requires, from its FM1 agreement. Throws on malformed input.
        public static Requirement[] Requirements(string agreement)
        {
            return Parse(agreement ?? "").OrderBy(p => p.Key, StringComparer.Ordinal).Select(p =>
            {
                var parts = p.Value.Split('|');
                return new Requirement { Id = p.Key, Version = parts[0], Sha256 = parts[1] };
            }).ToArray();
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
