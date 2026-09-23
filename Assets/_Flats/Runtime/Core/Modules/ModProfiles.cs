using System;
using System.IO;
using System.Linq;

namespace Flats.Modules
{
    [Serializable] public sealed class ProfileModule { public string id,version,json;public bool requested; }
    [Serializable] public sealed class ModProfile { public string id,name;public ProfileModule[] modules=new ProfileModule[0]; }
    [Serializable] public sealed class ProfileDocument { public int schema=1;public string selected;public ModProfile[] profiles; }
    // A single atomic record owns selection, enabled intent and configuration together.
    // Package receipts remain a shared library and are never copied into a profile.
    public sealed class ModProfiles
    {
        readonly string path;readonly IModJson codec;ProfileDocument document;
        bool selectionChanged;
        public string RunningId { get; private set; }
        public string SelectedId { get { return document.selected; } }
        public bool RestartRequired { get { return selectionChanged; } }
        public ModProfile[] Snapshot() { return Copy(document).profiles; }
        public ModProfile Selected { get { return Snapshot().Single(p=>p.id==SelectedId); } }
        ProfileDocument Copy(ProfileDocument value) { return codec.Read<ProfileDocument>(codec.Write(value)); }
        public ModProfiles(string file,IModJson json,ProfileModule[] legacy)
        {
            path=file;codec=json;Directory.CreateDirectory(Path.GetDirectoryName(path));
            if(File.Exists(path))
            {
                try{document=Read(path);}
                catch(NotSupportedException){throw;}
                catch{document=Read(path+".previous");File.Copy(path,path+".retained-"+Guid.NewGuid().ToString("N"));Write(document);}
            }
            else
            {
                var first=new ModProfile{id=Guid.NewGuid().ToString("N"),name="Default",modules=legacy};
                document=new ProfileDocument{selected=first.id,profiles=new[]{first}};Write(document);
            }
            RunningId=document.selected;
        }
        ProfileDocument Read(string file)
        {
            if(new FileInfo(file).Length>4*1024*1024)throw new InvalidDataException("Profiles file is too large");
            var value=codec.Read<ProfileDocument>(File.ReadAllText(file));Validate(value);return value;
        }
        static void Name(string name){ModRules.Text(name,48,"profile name",true);if(name.Any(char.IsControl))throw new InvalidDataException("Invalid profile name");}
        static void Validate(ProfileDocument value)
        {
            if(value!=null&&value.schema>1)throw new NotSupportedException("Profiles use a newer schema; original retained");
            if(value==null||value.schema!=1||value.profiles==null||value.profiles.Length<1||value.profiles.Length>32)throw new InvalidDataException("Invalid profiles document");
            if(value.profiles.Select(p=>p.id).Distinct().Count()!=value.profiles.Length||!value.profiles.Any(p=>p.id==value.selected))throw new InvalidDataException("Invalid profile selection");
            foreach(var p in value.profiles)
            {
                if(!Guid.TryParseExact(p.id,"N",out _))throw new InvalidDataException("Invalid profile identity");Name(p.name);
                if(p.modules==null||p.modules.Length>256||p.modules.Select(m=>m.id).Distinct().Count()!=p.modules.Length)throw new InvalidDataException("Invalid profile modules");
                foreach(var m in p.modules){ModRules.Id(m.id);if(m.json!=null&&m.json.Length>65536)throw new InvalidDataException("Module settings too large");}
            }
        }
        void Write(ProfileDocument next)
        {
            Validate(next);PackageStore.RejectLinks(Path.GetDirectoryName(path));
            var temp=path+".new";var text=codec.Write(next);if(System.Text.Encoding.UTF8.GetByteCount(text)>4*1024*1024)throw new InvalidDataException("Profiles document too large");
            foreach(var file in new[]{path,temp,path+".previous"})if(File.Exists(file)&&(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked profile files are not supported");
            using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write)){using(var writer=new StreamWriter(f)){writer.Write(text);writer.Flush();f.Flush(true);}}
            if(File.Exists(path))File.Replace(temp,path,path+".previous");else File.Move(temp,path);
            document=next;
        }
        public string Create(string name,string copyId=null)
        {
            Name(name);var next=Copy(document);var p=new ModProfile{id=Guid.NewGuid().ToString("N"),name=name.Trim(),modules=copyId==null?new ProfileModule[0]:next.profiles.Single(x=>x.id==copyId).modules};
            next.profiles=next.profiles.Concat(new[]{p}).ToArray();Write(next);return p.id;
        }
        public void Rename(string id,string name){Name(name);var next=Copy(document);next.profiles.Single(p=>p.id==id).name=name.Trim();Write(next);}
        public void Delete(string id)
        {
            if(id==SelectedId||id==RunningId)throw new InvalidOperationException("Choose another profile and restart before deleting a selected or running profile");
            var next=Copy(document);next.profiles=next.profiles.Where(p=>p.id!=id).ToArray();Write(next);
        }
        public void Select(string id,Action<ModProfile> validate)
        {
            var next=Copy(document);var p=next.profiles.Single(x=>x.id==id);validate(p);bool changed=id!=SelectedId;next.selected=id;Write(next);selectionChanged|=changed;
        }
        public void Save(ProfileModule[] modules)
        {
            var next=Copy(document);next.profiles.Single(p=>p.id==next.selected).modules=codec.Read<ModProfile>(codec.Write(new ModProfile{modules=modules})).modules;Write(next);
        }
        public void RetireBuiltin(string oldId,string packageId)
        {
            var next=Copy(document);bool changed=false;
            foreach(var profile in next.profiles)
            {
                var old=profile.modules.FirstOrDefault(m=>m.id==oldId);if(old==null)continue;
                if(!profile.modules.Any(m=>m.id==packageId))
                {
                    profile.modules=profile.modules.Concat(new[]{new ProfileModule{id=packageId,version=old.version,json=old.json,requested=false}}).ToArray();changed=true;
                }
                if(old.requested){old.requested=false;changed=true;}
            }
            if(changed)Write(next);
        }
        public bool Requested(string id) { return document.profiles.Single(p=>p.id==SelectedId).modules.Any(m=>m.id==id&&m.requested); }
    }
}
