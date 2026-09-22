using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Flats.Modules;

public static class FlatsModuleTests
{
    sealed class Fake : IFirstPartyModule
    {
        public ModuleManifest Manifest { get; set; }
        public int Resources, Starts, Stops;
        public bool Fail, FailInit;
        public void Initialize(ModuleLifetime l) { l.Own(()=>Resources--);Resources++;if(FailInit)throw new Exception("initialize failed"); }
        public void Enable(ModuleLifetime l) { Starts++;l.Own(()=>Resources--);Resources++;if(Fail)throw new Exception("enable failed"); }
        public void Disable() { Stops++; }
    }
    static Fake Module(string id="test.a",ModuleDependency[] deps=null,string[] conflicts=null,ModuleScope scope=ModuleScope.ClientOnly,string api="1.0.0",string game="5.3.5")
    { return new Fake{Manifest=new ModuleManifest(id,"1.0.0",id,"test",scope,new VersionRange(api,"9.0.0"),new VersionRange(game,"9.0.0"),deps,conflicts)}; }
    static ModuleDependency Dep(string id,string min="1.0.0") { return new ModuleDependency(id,new VersionRange(min,"9.0.0")); }
    static ModuleManager Manager(params Fake[] m) { return new ModuleManager(m,"1.0.0","5.3.5"); }
    static void Check(bool value,string message="Assertion failed") { if(!value)throw new Exception(message); }
    [Serializable] public class Result { public string name,status,error; }
    [Serializable] public class Report { public string utc;public int passed,failed;public Result[] tests; }
    [MenuItem("Flats/Modules/Run focused tests")]
    public static void Run()
    {
        var results=new List<Result>();
        Action<string,Action> test=(name,body)=> {var r=new Result{name=name,status="PASS"};try{body();}catch(Exception e){r.status="FAIL";r.error=e.ToString();}results.Add(r);};
        test("valid + idempotent + repeated lifecycle",()=>{var a=Module();using(var m=Manager(a)){for(int i=0;i<10;i++){Check(m.Apply(new[]{"test.a"}));Check(m.Apply(new[]{"test.a"}));Check(a.Resources==2);Check(m.Installed[0].Active);Check(m.Apply(new string[0]));Check(a.Resources==0);}Check(a.Starts==10&&a.Stops==10);}});
        test("duplicate ID",()=>{using(var m=Manager(Module(),Module())){Check(!m.Apply(new[]{"test.a"}));Check(m.Installed.All(r=>!r.Active&&r.Reason.Contains("Duplicate")));}});
        test("missing dependency",()=>{using(var m=Manager(Module(deps:new[]{Dep("missing")}))){Check(!m.Apply(new[]{"test.a"}));Check(m.LastError.Contains("Missing"));}});
        test("cyclic dependency",()=>{using(var m=Manager(Module(deps:new[]{Dep("test.b")}),Module("test.b",new[]{Dep("test.a")}))){Check(!m.Apply(new[]{"test.a","test.b"}));Check(m.LastError.Contains("Cyclic"));}});
        test("dependency version",()=>{using(var m=Manager(Module(deps:new[]{Dep("test.b","2.0.0")}),Module("test.b"))){Check(!m.Apply(new[]{"test.a","test.b"}));Check(m.LastError.Contains("Incompatible dependency"));}});
        test("API version",()=>{using(var m=Manager(Module(api:"2.0.0"))){Check(!m.Apply(new[]{"test.a"}));Check(m.LastError.Contains("API"));}});
        test("game version",()=>{using(var m=Manager(Module(game:"6.0.0"))){Check(!m.Apply(new[]{"test.a"}));Check(m.LastError.Contains("game"));}});
        test("RequiredForSession fail closed",()=>{using(var m=Manager(Module(scope:ModuleScope.RequiredForSession))){Check(!m.Apply(new[]{"test.a"}));Check(m.LastError.Contains("room agreement"));}});
        test("one-sided conflict preserves active set",()=>{var a=Module();var b=Module("test.b",conflicts:new[]{"test.a"});using(var m=Manager(a,b)){Check(m.Apply(new[]{"test.a"}));Check(!m.Apply(new[]{"test.a","test.b"}));Check(a.Resources==2&&b.Resources==0);Check(m.Installed[1].Requested&&!m.Installed[1].Active);}});
        test("dependency request and reverse disable",()=>{var a=Module(deps:new[]{Dep("test.b")});var b=Module("test.b");using(var m=Manager(a,b)){Check(!m.Apply(new[]{"test.a"}));Check(m.Apply(new[]{"test.a","test.b"}));Check(!m.Apply(new[]{"test.a"}));Check(a.Resources==2&&b.Resources==2);Check(m.Apply(new string[0]));Check(a.Resources==0&&b.Resources==0);}});
        test("activation failure rolls back additions",()=>{var a=Module();var b=Module("test.b");var c=Module("test.c");c.Fail=true;using(var m=Manager(a,b,c)){Check(m.Apply(new[]{"test.a"}));Check(!m.Apply(new[]{"test.b","test.c"}));Check(a.Resources==2&&b.Resources==0&&c.Resources==0);Check(m.Installed[2].State==ModuleState.Error);Check(m.Installed[0].Active&&!m.Installed[0].Requested);}});
        test("initialization failure releases resources",()=>{var a=Module();a.FailInit=true;using(var m=Manager(a)){Check(!m.Apply(new[]{"test.a"}));Check(a.Resources==0);}});
        test("cleanup continues after exception",()=>{var l=new ModuleLifetime();int releases=0;l.Own(()=>releases++);l.Own(()=>{throw new Exception();});l.Own(()=>releases++);try{l.Dispose();}catch(AggregateException){}Check(releases==2);l.Dispose();Check(releases==2);});
        test("settings roundtrip + backup recovery + unknown module",()=>WithStore((s,p)=>{var d=new ModuleSettingsDocument{modules=new[]{new ModuleSetting{id="future.module",version="2.0.0",json="opaque",requested=true}}};s.Save(d);s.Save(d);File.WriteAllText(p,"broken");var loaded=s.Load();Check(loaded.modules[0].json=="opaque"&&loaded.modules[0].requested);Check(!string.IsNullOrEmpty(s.Notice));}));
        test("missing settings defaults",()=>WithStore((s,p)=>Check(s.Load().modules.Length==0&&!s.ReadOnly)));
        test("all damaged settings retained and recoverable",()=>WithStore((s,p)=>{File.WriteAllText(p,"broken");Check(s.Load().modules.Length==0);Check(Directory.GetFiles(Path.GetDirectoryName(p),"*.retained-*").Length==1);s.Save(new ModuleSettingsDocument());Check(s.Load().schema==1);}));
        test("old unsupported settings retained",()=>WithStore((s,p)=>{FlatsAtomicRecord.Write(p,"{\"schema\":0,\"modules\":[]}",j=>{});Check(s.Load().schema==1);Check(Directory.GetFiles(Path.GetDirectoryName(p),"*.retained-*").Length>0);}));
        test("future schema blocks writes",()=>WithStore((s,p)=>{FlatsAtomicRecord.Write(p,"{\"schema\":2,\"modules\":[]}",j=>{});s.Load();Check(s.ReadOnly);bool refused=false;try{s.Save(new ModuleSettingsDocument());}catch(IOException){refused=true;}Check(refused&&File.Exists(p));}));
        test("crosshair bounds and invalid values",()=>{var s=new CrosshairSettings{size=100};s.Validate();Check(s.size==64);s.size=0;s.Validate();Check(s.size==6);s.size=float.NaN;bool refused=false;try{s.Validate();}catch(InvalidDataException){refused=true;}Check(refused);});
        test("crosshair lease restores original presentation",()=>{var a=new CrosshairModule();using(var m=new ModuleManager(new[]{a},"1.0.0","5.3.5")){Check(m.Apply(new[]{CrosshairModule.Id}));Check(Flats.UI.CrosshairPresentation.Appearance==a.Settings);Check(m.Apply(new string[0]));Check(Flats.UI.CrosshairPresentation.Appearance==null);}});
        var report=new Report{utc=DateTime.UtcNow.ToString("o"),passed=results.Count(r=>r.status=="PASS"),failed=results.Count(r=>r.status=="FAIL"),tests=results.ToArray()};
        Directory.CreateDirectory("Logs/modules");File.WriteAllText("Logs/modules/core-tests.json",JsonUtility.ToJson(report,true));
        Debug.Log("MODULE_TESTS passed="+report.passed+" failed="+report.failed);
        if(report.failed>0)throw new Exception("Module tests failed; see logs/modules/core-tests.json");
    }
    static void WithStore(Action<ModuleSettingsStore,string> body)
    {
        string directory=Path.Combine(Path.GetTempPath(),"FlatsModuleTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try{string path=Path.Combine(directory,"modules-v1.json");body(new ModuleSettingsStore(path),path);}
        finally{Directory.Delete(directory,true);}
    }
}
