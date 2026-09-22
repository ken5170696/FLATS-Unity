using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Flats.Modules;

// Opt-in integration driver. Uses an isolated mod directory, actual UI events, HTTP and disk.
public sealed partial class ModCenterVerification : MonoBehaviour
{
    [Serializable] sealed class CheckResult { public string name,status,error; }
    [Serializable] sealed class Report { public string phase,utc;public int width,height,runtimeErrors;public CheckResult[] checks; }
    readonly List<CheckResult> checks=new List<CheckResult>();
    string output,phase;
#if UNITY_EDITOR
    public string EditorPhase,EditorOutput;
#endif
    static bool verificationRunning;
    int errors;
    Menu menu;
    ModuleManagementPage page;
    ModCenterService Service=>BuiltinModules.Instance.Center;
    static string Arg(string key) { var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-flats-verify") || Arg("-flats-mod-center-verify")==null || Arg("-flats-module-settings-dir")==null)return;
        var go=new GameObject("Mod center verification");DontDestroyOnLoad(go);go.AddComponent<ModCenterVerification>();
    }
    async void Start()
    {
        bool optedIn=Environment.GetCommandLineArgs().Contains("-flats-verify")&&Arg("-flats-module-settings-dir")!=null;
        phase=optedIn?Arg("-flats-mod-center-verify"):null;
        output=Arg("-flats-mod-evidence");
#if UNITY_EDITOR
        if(!string.IsNullOrEmpty(EditorPhase)){phase=EditorPhase;output=EditorOutput;}
#endif
        if(string.IsNullOrEmpty(phase)||string.IsNullOrEmpty(output)){Destroy(gameObject);return;}
        if(verificationRunning){Debug.LogWarning("MOD_CENTER_VERIFICATION another driver is running");Destroy(gameObject);return;}
        output=Path.GetFullPath(output);
        if(File.Exists(Path.Combine(output,"runtime.json"))){Debug.LogWarning("MOD_CENTER_VERIFICATION skipped: use a new evidence directory.");Destroy(gameObject);return;}
        verificationRunning=true;
        gameObject.hideFlags=HideFlags.DontSave;Directory.CreateDirectory(output);Application.logMessageReceived+=Log;
        try
        {
            await Until(()=>BuiltinModules.Instance!=null&&Service.Ready&&Menu.current=="Main",25);
            menu=FindFirstObjectByType<Menu>();page=FindFirstObjectByType<ModuleManagementPage>(FindObjectsInactive.Include);await Task.Delay(1500);
            Check("local modules initialized",Service.Store!=null&&Service.Downloads!=null);
            if(phase=="Official"||phase=="OfficialPolicy")await Official();
            else if(phase=="GlassUI")await GlassUI();
            else if(phase=="DependenciesUI"||phase=="DependenciesRestart")await DependenciesUI();
            else if(phase=="ResumeUI")await ResumeUI();
            else if(phase=="ProfilesUI"||phase=="ProfilesRestart"||phase=="ProfilesMigration")await ProfilesUI();
            else if(phase.StartsWith("Approved"))await Approved();
            else if(phase=="Configure")await Configure();
            else if(phase=="Restart")await Restart();
            else if(phase=="Removed")await Removed();
            else if(phase=="Layout")await Layout();
            else if(phase=="Managed")await Managed();
            else if(phase=="ManagedRestart")await ManagedRestart();
            else if(phase=="RoomMismatch")await RoomMismatch();
            else throw new Exception("Unknown phase");
            Check("runtime error count",errors==0);
        }
        catch(Exception e){checks.Add(new CheckResult{name="scenario",status="FAIL",error=e.ToString()});}
        finally
        {
            verificationRunning=false;
            var report=new Report{phase=phase,utc=DateTime.UtcNow.ToString("o"),width=Screen.width,height=Screen.height,runtimeErrors=errors,checks=checks.ToArray()};
            File.WriteAllText(Path.Combine(output,"runtime.json"),JsonUtility.ToJson(report,true));Application.logMessageReceived-=Log;
            Debug.Log("MOD_CENTER_VERIFICATION "+phase+" "+(checks.All(c=>c.status=="PASS")?"PASS":"FAIL"));
            if(Application.isEditor)Destroy(gameObject);else Application.Quit(checks.All(c=>c.status=="PASS")?0:1);
        }
    }
    void Log(string message,string stack,LogType type) { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++; }
    void Check(string name,bool ok) { checks.Add(new CheckResult{name=name,status=ok?"PASS":"FAIL"});if(!ok)throw new Exception(name); }
    static async Task Until(Func<bool> condition,int seconds=15)
    {
        var deadline=DateTime.UtcNow.AddSeconds(seconds);while(!condition()){if(DateTime.UtcNow>deadline)throw new TimeoutException("Integration condition timed out");await Task.Delay(60);}
    }
    async Task MenuAction(int action,string state) { menu.Fade(action);await Until(()=>Menu.current==state);await Task.Delay(700); }
    Button Button(string name) { return page.GetComponentsInChildren<Button>(true).Single(b=>b.name==name); }
    async Task Click(string name) { var b=Button(name);Check("click "+name,b.gameObject.activeInHierarchy&&b.interactable);b.onClick.Invoke();await Task.Delay(450); }
    async Task Capture(string name)
    {
        await Task.Delay(350);string path=Path.Combine(output,name+".png");UnityEngine.ScreenCapture.CaptureScreenshot(path);await Until(()=>File.Exists(path));
        Check("capture "+name,new FileInfo(path).Length>5000);
        await Task.Delay(120);
        var image=new Texture2D(2,2);image.LoadImage(File.ReadAllBytes(path));
        var pixels=image.GetPixels32();bool nonblank=pixels.Where((p,i)=>i%97==0).Select(p=>(p.r<<16)|(p.g<<8)|p.b).Distinct().Take(16).Count()>=16;Destroy(image);
        Check("nonblank rendered pixels "+name,nonblank);
    }
    async Task Configure()
    {
        await Capture("main");await MenuAction(0,"Play");await Capture("play");
        await MenuAction(0,"Singleplayer");Check("singleplayer modes retained",menu.buttons[3].transform.parent.GetComponentInChildren<Text>().text=="Training");
        await MenuAction(-1,"Play");await MenuAction(1,"Multiplayer");Check("multiplayer modes retained",menu.buttons[0].transform.parent.GetComponentInChildren<Text>().text=="Open Match");
        await MenuAction(-1,"Play");await MenuAction(-1,"Main");await MenuAction(1,"Modules");
        await Capture("installed");await Click("Explore");await Until(()=>page.GetComponentsInChildren<Button>().Any(b=>b.name=="Mod-example.precision-dot"));await Task.Delay(600);await Capture("explore");
        var search=page.GetComponentsInChildren<InputField>().Single(f=>f.name=="Search");search.text="no-such-module-xy123";await Task.Delay(1200);
        Check("search no results",page.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("No results")));await Capture("search-empty");search.text="";await Task.Delay(1200);
        await Click("Mod-example.precision-dot");await Click("Primary");await Capture("install-confirmation");await Click("ConfirmAction");await Click("Downloads");
        await Until(()=>Service.Downloads.Snapshot().Any(j=>j.Id=="example.precision-dot"&&!j.Busy));await Service.RefreshInstalled();
        Check("HTTP download and verified installation",Service.Installed.Any(p=>p.manifest.id=="example.precision-dot"));await Capture("download-complete");
        await Click("Installed");await Click("Mod-example.precision-dot");await Click("Primary");await Until(()=>Service.Installed.Any(p=>p.manifest.id=="example.precision-dot"&&p.requested));
        Check("configured versus active before restart",!BuiltinModules.Instance.Manager.Installed.Any(r=>r.Manifest.Id=="example.precision-dot"&&r.Active));
        await Capture("restart-required");page.Close();Check("mod return focus",Menu.current=="Main"&&EventSystem.current.currentSelectedGameObject==menu.buttons[1].transform.parent.gameObject);
    }
    async Task Restart()
    {
        Check("external module active after startup reload",BuiltinModules.Instance.Manager.Installed.Any(r=>r.Manifest.Id=="example.precision-dot"&&r.Active));
        Check("real HUD appearance registered",Flats.UI.CrosshairPresentation.Appearance!=null&&Flats.UI.CrosshairPresentation.Appearance.Style==Flats.UI.CrosshairStyle.Dot);
        await MenuAction(1,"Modules");await Click("Mod-example.precision-dot");await Capture("active-after-restart");
        await Click("Category");await Task.Delay(600);await Click("Mod-example.precision-dot");await Click("Secondary");await Click("ConfirmAction");
        await Until(()=>Service.Downloads.Snapshot().Any(j=>j.Id=="example.precision-dot"&&!j.Busy));await Service.RefreshInstalled();
        Check("new version installed",Service.Installed.Single(p=>p.manifest.id=="example.precision-dot").manifest.version=="1.0.1");
        Check("running version unchanged until restart",Service.Running.Single(p=>p.manifest.id=="example.precision-dot").manifest.version=="1.0.0");
        await Click("Installed");await Click("Mod-example.precision-dot");await Capture("updated-restart-required");await Click("Primary");
        Check("disable persists intent while runtime retained",!Service.Installed.Single(p=>p.manifest.id=="example.precision-dot").requested&&BuiltinModules.Instance.Manager.Installed.Single(r=>r.Manifest.Id=="example.precision-dot").Active);
        await Click("Remove");await Click("ConfirmAction");await Until(()=>Service.Installed.Length==0);await Capture("removed-pending-restart");
    }
    async Task Removed()
    {
        Check("removal survives restart",Service.Installed.Length==0&&!BuiltinModules.Instance.Manager.Installed.Any(r=>r.Manifest.Id=="example.precision-dot"));
        Check("removed HUD appearance cleared",Flats.UI.CrosshairPresentation.Appearance==null);
        await MenuAction(1,"Modules");await Capture("removed-after-restart");
        await Service.ConfigureSource("http://127.0.0.1:8767/");await Click("Explore");await Until(()=>page.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("Unable to load mods")));await Capture("offline");
        await Service.ConfigureSource("http://127.0.0.1:8766/");await Click("RetryBrowse");await Until(()=>page.GetComponentsInChildren<Button>().Any(b=>b.name=="Mod-example.precision-dot"));Check("source failure recovered by retry",true);
        await Click("Downloads");await Capture("downloads-empty");
    }
    async Task Layout()
    {
        await MenuAction(1,"Modules");await Click("Explore");await Until(()=>page.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("Mod-")));
        var root=(RectTransform)page.transform.Find("ModCenter");var corners=new Vector3[4];root.GetWorldCorners(corners);var canvas=page.GetComponentInParent<Canvas>();
        foreach(var c in corners){var p=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,c);Check("panel within viewport",p.x>=-1&&p.x<=Screen.width+1&&p.y>=-1&&p.y<=Screen.height+1);}
        await Capture("layout");
        var stress=page.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="Mod-verification.list-000");
        if(stress!=null)
        {
            await Click(stress.name);Check("bounded visible page",page.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("Mod-"))==20);
            var last=page.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("Mod-")).Last();EventSystem.current.SetSelectedGameObject(last.gameObject);await Task.Delay(200);
            var scroll=page.GetComponentsInChildren<ScrollRect>().Single(s=>s.name=="ModList");Check("keyboard focus scrolls into view",scroll.content.anchoredPosition.y>0);
            await Capture("long-text-focus");await Click("Next");await Task.Delay(500);Check("pagination loads next page",!page.GetComponentsInChildren<Button>().Any(b=>b.name=="Mod-verification.list-000"));await Capture("page-two");
        }
    }
    async Task Managed()
    {
        await MenuAction(1,"Modules");await Click("Explore");await Until(()=>page.GetComponentsInChildren<Button>().Any(b=>b.name=="Mod-verification.lifecycle"));
        await Click("Mod-verification.lifecycle");await Click("Primary");await Click("ConfirmAction");
        await Until(()=>Service.Downloads.Snapshot().Any(j=>j.Id=="verification.lifecycle"&&!j.Busy));await Service.RefreshInstalled();
        Check("managed ZIP installed",Service.Installed.Any(p=>p.manifest.id=="verification.lifecycle"));
        await Click("Installed");await Click("Mod-verification.lifecycle");await Click("Primary");
        Check("managed enable requires explicit confirmation",page.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("executes managed code")));
        await Click("ConfirmAction");Check("managed intent persisted",Service.Installed.Single(p=>p.manifest.id=="verification.lifecycle").requested);
        Check("no managed code loaded by installation",!AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="ManagedProbe"));await Capture("managed-restart-required");
    }
    async Task ManagedRestart()
    {
        var r=BuiltinModules.Instance.Manager.Installed.Single(x=>x.Manifest.Id=="verification.lifecycle");Check("managed runtime adapter active",r.Active);
        var type=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="ManagedProbe").GetType("ManagedProbe");
        Check("managed lifecycle actually executed",(int)type.GetField("Active").GetValue(null)==1&&(int)type.GetField("Starts").GetValue(null)==1);
        var agreement=Service.Agreement();Check("required module published",agreement.StartsWith("verification.lifecycle|1.0.0|"));
        Check("room agreement accepts identical content",SessionModules.Compare(agreement,agreement)=="");Check("unmodded room rejects required module",SessionModules.Compare("",agreement).Contains("Disable"));
        await MenuAction(1,"Modules");await Click("Mod-verification.lifecycle");await Capture("managed-active");
        BuiltinModules.Instance.Manager.Dispose();Check("managed lifetime cleanup executed",(int)type.GetField("Active").GetValue(null)==0);
        await Service.Remove("verification.lifecycle");
    }
    async Task RoomMismatch()
    {
        PhotonNetwork.offlineMode=true;
        var properties=new ExitGames.Client.Photon.Hashtable{{"R",1},{"O",1},{SessionModules.Property,"verification.required|1.0.0|"+new string('a',64)}};
        PhotonNetwork.CreateRoom("FLATS Mod gate local verification",new RoomOptions{MaxPlayers=1,IsVisible=false,CustomRoomProperties=properties},null);
        await Until(()=>menu.GetComponentsInChildren<Text>().Any(t=>t.text=="Room modules differ"));
        Check("actual Photon join callback rejects missing module",!PhotonNetwork.inRoom);
        Check("room mismatch does not download",Service.Downloads.Snapshot().Length==0);await Capture("room-mismatch");
        var open=menu.GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<Text>()?.text=="Open Mod");open.onClick.Invoke();await Until(()=>Menu.current=="Modules");
        Check("room mismatch opens explicit management entry",page.gameObject.activeInHierarchy);PhotonNetwork.offlineMode=false;
    }
}
