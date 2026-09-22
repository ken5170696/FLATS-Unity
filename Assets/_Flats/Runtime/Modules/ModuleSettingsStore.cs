using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Flats.Modules
{
    [Serializable] public sealed class ModuleSetting
    {
        public string id, version, json;
        public bool requested;
    }
    [Serializable] public sealed class ModuleSettingsDocument
    {
        public int schema = 1;
        public ModuleSetting[] modules = new ModuleSetting[0];
    }
    // Separate from profile/FlatsPreferences. Unknown installed-module entries are preserved.
    public sealed class ModuleSettingsStore
    {
        public readonly string Path;
        public string Notice { get; private set; }
        public bool ReadOnly { get; private set; }
        public ModuleSettingsStore(string path) { Path=path; }
        static void Validate(string json)
        {
            var d=JsonUtility.FromJson<ModuleSettingsDocument>(json);
            if(d!=null && d.schema>1)throw new NotSupportedException("Newer module settings schema; file retained, changes blocked");
            if(d==null || d.schema!=1 || d.modules==null || d.modules.Any(m=>m==null || string.IsNullOrEmpty(m.id)) || d.modules.Select(m=>m.id).Distinct().Count()!=d.modules.Length)
                throw new InvalidDataException("Invalid module settings");
        }
        public ModuleSettingsDocument Load()
        {
            try
            {
                string recovery; string json=FlatsAtomicRecord.Read(Path,Validate,out recovery); Notice=recovery ?? "";
                return json==null ? new ModuleSettingsDocument() : JsonUtility.FromJson<ModuleSettingsDocument>(json);
            }
            catch(NotSupportedException e) { ReadOnly=true; Notice=e.Message; return new ModuleSettingsDocument(); }
            catch(Exception e)
            {
                // Preserve every damaged generation and reset only this independent module store.
                try
                {
                    var directory=System.IO.Path.GetDirectoryName(Path);
                    if(Directory.Exists(directory))foreach(var file in Directory.GetFiles(directory,System.IO.Path.GetFileName(Path)+"*"))
                        File.Move(file,file+".retained-"+Guid.NewGuid().ToString("N"));
                    Notice="Module settings recovered to defaults; originals retained. " + e.Message;
                }
                catch(Exception recovery) { ReadOnly=true; Notice="Module settings unavailable; writes blocked. " + recovery.Message; }
                return new ModuleSettingsDocument();
            }
        }
        public void Save(ModuleSettingsDocument document)
        {
            if(ReadOnly)throw new IOException(Notice);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            FlatsAtomicRecord.Write(Path,JsonUtility.ToJson(document,true),Validate);
        }
    }
}
