using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace Flats.Modules
{
    public sealed partial class PackageStore
    {
        [Serializable] public sealed class PointerBackup { public string id,current,previous; }
        [Serializable] public sealed class BatchJournal { public int schema=1; public PointerBackup[] pointers; }
        bool recoveryRequired;
        void WriteDurable(string path,string value)
        {
            var temp=path+".new";
            if(File.Exists(temp)&&(File.GetAttributes(temp)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked journal files are not supported");
            var bytes=System.Text.Encoding.UTF8.GetBytes(value);
            using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
        }
        void RestorePointer(string path,string value)
        {
            if(value==null){if(File.Exists(path))File.Delete(path);}
            else {Directory.CreateDirectory(Path.GetDirectoryName(path));WriteDurable(path,value);}
        }
        void RecoverBatch()
        {
            string path=Under("install-batch.json");
            if(!File.Exists(path)){recoveryRequired=false;return;}
            recoveryRequired=true;
            if(new FileInfo(path).Length>20*1024*1024)throw new InvalidDataException("Install journal is too large; storage retained");
            var journal=json.Read<BatchJournal>(File.ReadAllText(path));
            if(journal==null||journal.schema!=1||journal.pointers==null||journal.pointers.Length>64)throw new InvalidDataException("Invalid install journal; storage retained");
            foreach(var p in journal.pointers){ModRules.Id(p.id);if(p.current!=null&&p.current.Length>128*1024||p.previous!=null&&p.previous.Length>128*1024)throw new InvalidDataException("Invalid pointer backup");}
            foreach(var p in journal.pointers)
            {
                RestorePointer(Under(p.id+"/current.json"),p.current);
                RestorePointer(Under(p.id+"/current.json.previous"),p.previous);
            }
            File.Delete(path);recoveryRequired=false;
            Notices.Add("An interrupted install plan was rolled back. Previous versions and settings were retained.");
        }
        // The durable journal is the transaction boundary. Scan cannot observe a
        // partial set under this gate; startup rolls back before loading any DLL.
        // Retained immutable content is a cache only, never an installed pointer.
        public void InstallBatch(CatalogItem[] items,string[] archives,string source,CancellationToken cancel)
        {
            if(items.Length==0)return;
            if(items.Length>64||items.Length!=archives.Length||items.Select(i=>i.manifest.id).Distinct().Count()!=items.Length)throw new InvalidDataException("Invalid install plan");
            lock(gate)
            {
                RecoverBatch();cancel.ThrowIfCancellationRequested();
                var journal=new BatchJournal{pointers=items.Select(i=>
                {
                    ModRules.Id(i.manifest.id);string path=Under(i.manifest.id+"/current.json");
                    return new PointerBackup{id=i.manifest.id,current=File.Exists(path)?File.ReadAllText(path):null,previous=File.Exists(path+".previous")?File.ReadAllText(path+".previous"):null};
                }).ToArray()};
                string journalPath=Under("install-batch.json");WriteDurable(journalPath,json.Write(journal));
                try
                {
                    for(int i=0;i<items.Length;i++)
                    {
                        var stage=BeginStaging();
                        try{Install(archives[i],stage,items[i],source,cancel);}finally{EndStaging(stage);}
                    }
                    cancel.ThrowIfCancellationRequested();
                    File.Delete(journalPath); // A crash before this point restores the whole old set.
                }
                catch
                {
                    recoveryRequired=true;RecoverBatch();throw;
                }
            }
        }
    }
}
