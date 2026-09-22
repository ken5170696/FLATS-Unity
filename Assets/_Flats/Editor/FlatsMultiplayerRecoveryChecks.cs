using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class FlatsMultiplayerRecoveryChecks
{
    public static void Run()
    {
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/storage-check-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")));
        Directory.CreateDirectory(root);var lines=new System.Collections.Generic.List<string>();
        Action<string,Action> test=(name,action)=>{try{action();lines.Add("PASS "+name);}catch(Exception e){lines.Add("FAIL "+name+" "+e);}};
        Action<bool> require=ok=>{if(!ok)throw new Exception("Assertion failed");};
        string path=Path.Combine(root,"profile.json");string recovery;
        string profile=JsonUtility.ToJson(new FlatsLocalProfile.Profile {character="test-id$Test$Comment$0$1$2$3$4$5$0$1$0$0$0,1",settings=string.Join("$",Enumerable.Repeat("0",17).ToArray()),current="1$2$3$4$5$6"});
        test("legacy payload validation",()=>FlatsLocalProfile.Validate(profile));
        test("first atomic write and backup",()=>{FlatsAtomicRecord.Write(path,profile,FlatsLocalProfile.Validate);require(File.Exists(path+".bak"));require(FlatsAtomicRecord.Read(path,FlatsLocalProfile.Validate,out recovery)==profile);});
        string next=profile.Replace("Comment","Changed");
        test("second write survives fresh read",()=>{FlatsAtomicRecord.Write(path,next,FlatsLocalProfile.Validate);require(FlatsAtomicRecord.Read(path,FlatsLocalProfile.Validate,out recovery)==next);});
        test("corrupt primary preserved and backup recovered",()=>{File.WriteAllText(path,"broken original");require(FlatsAtomicRecord.Read(path,FlatsLocalProfile.Validate,out recovery)==profile);require(recovery!=null);require(Directory.GetFiles(root,"profile.json.corrupt-*").Any(p=>File.ReadAllText(p)=="broken original"));});
        test("invalid payload refused without overwriting",()=>{string before=File.ReadAllText(path);try{FlatsAtomicRecord.Write(path,"{}",FlatsLocalProfile.Validate);throw new Exception("accepted invalid payload");}catch(InvalidDataException){}require(File.ReadAllText(path)==before);});
        test("newer schema not silently downgraded",()=>{File.WriteAllText(path,"{\"schema\":2}");try{FlatsAtomicRecord.Read(path,FlatsLocalProfile.Validate,out recovery);throw new Exception("downgraded");}catch(NotSupportedException){}require(File.ReadAllText(path)=="{\"schema\":2}");});
        test("both corrupt files retained",()=>{File.WriteAllText(path,"bad primary");File.WriteAllText(path+".bak","bad backup");try{FlatsAtomicRecord.Read(path,FlatsLocalProfile.Validate,out recovery);throw new Exception("accepted corruption");}catch(InvalidDataException){}require(File.ReadAllText(path)=="bad primary"&&File.ReadAllText(path+".bak")=="bad backup");});
        test("write failure keeps existing data",()=>{string before=File.ReadAllText(path);try{FlatsAtomicRecord.Write(path,profile,FlatsLocalProfile.Validate);throw new Exception("overwrote corrupt record");}catch(InvalidDataException){}require(File.ReadAllText(path)==before);});
        test("immutable generations and corrupt latest recovery",()=>{
            string g=Path.Combine(root,"generations.json");FlatsAtomicRecord.Write(g,profile,FlatsLocalProfile.Validate);
            string old=g+".generation-0639000000000000000-fixture.json";File.Copy(g,old);
            FlatsAtomicRecord.Write(g,next,FlatsLocalProfile.Validate);require(FlatsAtomicRecord.Read(g,FlatsLocalProfile.Validate,out recovery)==next);
            string latest=Directory.GetFiles(root,"generations.json.generation-*.json").OrderByDescending(x=>x,StringComparer.Ordinal).First();File.WriteAllText(latest,"broken latest");
            require(FlatsAtomicRecord.Read(g,FlatsLocalProfile.Validate,out recovery)==profile);require(recovery!=null&&File.ReadAllText(latest)=="broken latest");
        });
        test("actual user-data filesystem isolated save and fresh read",()=>{
            string isolated=Path.Combine(root,"isolated-profile","profile.json");
            FlatsAtomicRecord.Write(isolated,profile,FlatsLocalProfile.Validate);FlatsAtomicRecord.Write(isolated,next,FlatsLocalProfile.Validate);
            require(FlatsAtomicRecord.Read(isolated,FlatsLocalProfile.Validate,out recovery)==next);
        });
        test("real DH exchange and AES offset roundtrip",()=>{
            var type=typeof(Menu).Assembly.GetType("Photon.SocketServer.Security.DiffieHellmanCryptoProvider",true);
            using(var a=(IDisposable)Activator.CreateInstance(type))using(var b=(IDisposable)Activator.CreateInstance(type))
            {
                byte[] ak=(byte[])type.GetProperty("PublicKey").GetValue(a,null),bk=(byte[])type.GetProperty("PublicKey").GetValue(b,null);
                type.GetMethod("DeriveSharedKey").Invoke(a,new object[]{bk});type.GetMethod("DeriveSharedKey").Invoke(b,new object[]{ak});
                byte[] input=Encoding.UTF8.GetBytes("prefix-PHOTON wire test-suffix");
                byte[] encrypted=(byte[])type.GetMethod("Encrypt",new[]{typeof(byte[]),typeof(int),typeof(int)}).Invoke(a,new object[]{input,7,16});
                byte[] decrypted=(byte[])type.GetMethod("Decrypt",new[]{typeof(byte[])}).Invoke(b,new object[]{encrypted});require(decrypted.SequenceEqual(input.Skip(7).Take(16)));
            }
        });
        File.WriteAllLines(Path.Combine(root,"results.txt"),lines);Debug.Log("FLATS_RECOVERY_CHECKS "+root+" "+string.Join("; ",lines.ToArray()));
        if(lines.Any(x=>x.StartsWith("FAIL")))throw new Exception("Recovery checks failed; see "+root);
    }
}
