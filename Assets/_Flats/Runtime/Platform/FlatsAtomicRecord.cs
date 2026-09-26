using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class FlatsAtomicRecord
{
    [Serializable] private class Envelope { public int schema=1; public string payload,sha256; }
    private static string Hash(string text)
    { using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-",""); }
    private static string Decode(string path)
    {
        if(new FileInfo(path).Length>4*1024*1024)throw new InvalidDataException("Record exceeds size limit: "+path);
        Envelope e;
        try { e=JsonUtility.FromJson<Envelope>(File.ReadAllText(path,Encoding.UTF8)); }
        catch(ArgumentException ex) { throw new InvalidDataException("Malformed save JSON: "+path,ex); }
        if(e!=null && e.schema>1)throw new NotSupportedException("Newer save schema; refusing downgrade: "+path);
        if(e==null || e.schema!=1 || e.payload==null || e.sha256!=Hash(e.payload))throw new InvalidDataException("Invalid save checksum/schema: "+path);
        return e.payload;
    }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet=System.Runtime.InteropServices.CharSet.Unicode, SetLastError=true)]
    private static extern bool MoveFileEx(string source,string destination,int flags);
#endif
    private static void ReplaceAtomically(string source,string destination)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if(Environment.OSVersion.Platform==PlatformID.Win32NT)
        {
            if(!MoveFileEx(source,destination,1|8))throw new IOException("Atomic save rename failed: "+destination,new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error()));
        }
        else
#endif
        if(File.Exists(destination))File.Replace(source,destination,null);
        else File.Move(source,destination);
    }
    private static string[] Generations(string path)
    {
        var directory=Path.GetDirectoryName(path);
        if(!Directory.Exists(directory))return new string[0];
        var files=Directory.GetFiles(directory,Path.GetFileName(path)+".generation-*.json");
        Array.Sort(files,StringComparer.Ordinal);Array.Reverse(files);return files;
    }
    private static void PublishGeneration(string temp,string path)
    {
        long sequence=DateTime.UtcNow.Ticks;
        foreach(string file in Generations(path))
        {
            string tail=file.Substring((path+".generation-").Length);long previous;
            if(tail.Length>=19 && long.TryParse(tail.Substring(0,19),out previous))sequence=Math.Max(sequence,previous+1);
        }
        // Unique same-directory rename publishes only a completely written, flushed record.
        // Previous generations remain immutable, including damaged records for recovery.
        File.Move(temp,path+".generation-"+sequence.ToString("D19")+"-"+Guid.NewGuid().ToString("N")+".json");
        PruneGenerations(path);
    }
    // Generation mode is permanent once entered, so without a cap the folder (and on WebGL
    // the IndexedDB sync) grows with every save. Keep the newest records; the base file
    // and .bak are never touched here.
    public const int RetainedGenerations=8;
    public static int PruneGenerations(string path)
    {
        string[] generations=Generations(path);
        int removed=0;
        for(int i=RetainedGenerations;i<generations.Length;i++)
        {
            try { File.Delete(generations[i]);removed++; }
            catch(IOException) { }
            catch(UnauthorizedAccessException) { }
        }
        return removed;
    }
    public static string Read(string path,Action<string> validate,out string recovery)
    {
        recovery=null;
        string damaged=null;
        foreach(string generation in Generations(path))
        {
            try { string value=Decode(generation);validate(value);if(damaged!=null)recovery="Recovered previous committed generation. Damaged records retained: "+damaged;return value; }
            catch(NotSupportedException){throw;}
            catch(Exception){damaged=(damaged==null?"":damaged+", ")+generation;}
        }
        if(damaged!=null)recovery="Recovered base record. Damaged generations retained: "+damaged;
        if(!File.Exists(path) && !File.Exists(path+".bak")){if(damaged!=null)throw new InvalidDataException("No valid save generation; all records retained: "+damaged);return null;}
        try { string value=Decode(path);validate(value);return value; }
        catch(NotSupportedException) { throw; }
        catch(Exception original)
        {
            if(!File.Exists(path+".bak"))throw new InvalidDataException("Save is unreadable and no backup exists; original retained. "+path,original);
            string value=Decode(path+".bak");validate(value);
            string retained=path+".corrupt-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+"-"+Guid.NewGuid().ToString("N");
            if(File.Exists(path))File.Copy(path,retained,false);
            string temp=path+".recover-"+Guid.NewGuid().ToString("N");File.Copy(path+".bak",temp,false);
            try { ReplaceAtomically(temp,path); }
            catch(IOException) { PublishGeneration(temp,path); }
            catch(UnauthorizedAccessException) { PublishGeneration(temp,path); }
            recovery="Recovered the last valid backup. Original retained at: "+retained;
            return value;
        }
    }
    public static void Write(string path,string payload,Action<string> validate)
    {
        validate(payload);
        if(File.Exists(path) || Generations(path).Length>0){string recovery;string current=Read(path,validate,out recovery);validate(current);}
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp=path+".pending-"+Guid.NewGuid().ToString("N");
        byte[] bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Envelope{payload=payload,sha256=Hash(payload)}));
        using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) { f.Write(bytes,0,bytes.Length);f.Flush(true); }
        if(Generations(path).Length>0){PublishGeneration(temp,path);return;}
        try
        {
            if(File.Exists(path))
            {
                string backup=path+".backup-"+Guid.NewGuid().ToString("N");File.Copy(path,backup,false);
                ReplaceAtomically(backup,path+".bak");ReplaceAtomically(temp,path);
            }
            else { ReplaceAtomically(temp,path);File.Copy(path,path+".bak",false); }
        }
        catch(IOException) { if(!File.Exists(temp))throw;PublishGeneration(temp,path); }
        catch(UnauthorizedAccessException) { if(!File.Exists(temp))throw;PublishGeneration(temp,path); }
    }
}
