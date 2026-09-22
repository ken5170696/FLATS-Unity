using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Flats.Modules;

// Explicitly invoked Play-mode checks. Requires the isolated FLATS_MOD_TEST_ROOT profile.
// The local catalogue contains labelled TEST content, never player or public catalogue data.
public static class FlatsModRedesignVerification
{
    static readonly List<string> checks=new List<string>();
    static string Output=>Path.GetFullPath("Logs/mod-redesign");
    static ModuleManagementPage Page=>UnityEngine.Object.FindFirstObjectByType<ModuleManagementPage>(FindObjectsInactive.Include);
    static ModCenterService Service=>BuiltinModules.Instance.Center;
    static Button Find(string name)=>Page.GetComponentsInChildren<Button>().First(b=>b.name==name);
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);checks.Add("PASS "+name);}
    static async Task Click(string name){var b=Find(name);Check(b.interactable,"interactive "+name);b.onClick.Invoke();await Task.Delay(450);}
    static async Task Until(Func<bool> predicate){for(int i=0;i<120;i++){if(predicate())return;await Task.Delay(100);}throw new Exception("Timed out");}
    static async Task Capture(string name)
    {
        await Task.Delay(300);UnityEngine.ScreenCapture.CaptureScreenshot(Path.Combine(Output,name+".png"));await Task.Delay(500);
        Check(File.Exists(Path.Combine(Output,name+".png")),"rendered "+name);
    }
    static async Task Size(int w,int h){FlatsModViewVerification.Size(w,h);await Task.Delay(700);Check(Screen.width==w&&Screen.height==h,"resolution "+w+"x"+h);}
    public static async void Run(string phase)
    {
        checks.Clear();
        try
        {
            Check(EditorApplication.isPlaying,"Play mode");
            Check((Environment.GetEnvironmentVariable("FLATS_MOD_TEST_ROOT")??"").Replace('\\','/').EndsWith("/logs/mod-redesign/profile"),"isolated profile");
            await Until(()=>Service.Ready);
            if(phase=="local")await Local();
            else if(phase=="catalog")await Catalog();
            else if(phase=="restart")await Restart();
            else if(phase=="removed")await Removed();
            else if(phase=="navigation")await Navigation();
            else if(phase=="edges")await Edges();
            else if(phase=="loading")await Loading();
            else throw new Exception("Unknown phase");
            checks.Add("COMPLETE "+phase);
        }
        catch(Exception e){checks.Add("FAIL "+e);Debug.LogError("MOD_REDESIGN_QA "+e.Message);}
        File.WriteAllLines(Path.Combine(Output,phase+"-checks.txt"),checks);
    }
    static async Task Local()
    {
        if(Menu.current=="Main")Page.Open();
        await Click("Installed");await Click("Mod-flats.crosshair");
        if(!BuiltinModules.Instance.Manager.Installed.First().Active)await Click("Primary");
        await Click("CrosshairStyle");await Click("CrosshairLarger");
        var style=BuiltinModules.Instance.Crosshair.Settings.style;var size=BuiltinModules.Instance.Crosshair.Settings.size;
        Check(Flats.UI.CrosshairPresentation.Appearance!=null,"built-in live HUD registered");
        Page.Close();Check(Menu.current=="Modules","back from detail stays in library");Page.Close();Check(Menu.current=="Main","back from library reaches main");
        Page.Open();await Click("Mod-flats.crosshair");Check(BuiltinModules.Instance.Crosshair.Settings.style==style&&BuiltinModules.Instance.Crosshair.Settings.size==size,"settings survive reopening");
        await Size(1920,1080);await Capture("builtin-detail-1080");
        await Size(1024,768);await Capture("builtin-detail-4x3");
        await Click("Primary");Check(!BuiltinModules.Instance.Manager.Installed.First().Active,"built-in disable applies immediately");
        Page.Close();await Click("Explore");await Capture("no-source-4x3");
        Check(!Page.GetComponentsInChildren<InputField>().Any(),"no useless search without source");
        await Click("EmptyAction");Check(!Page.back.interactable,"modal blocks background controls");
        Page.GetComponentsInChildren<InputField>().First(f=>f.name=="SourceUrl").text="not a valid address";await Click("SaveSource");
        Check(Page.GetComponentsInChildren<Text>().Any(t=>t.name=="SourceNotice"&&t.text.StartsWith("Could not")),"invalid source explains failure inside modal");
        await Capture("source-error-4x3");await Click("CancelSource");await Click("Downloads");await Capture("downloads-empty-4x3");
        await Click("Installed");Page.GetComponentsInChildren<InputField>().First(f=>f.name=="Search").text="no-such-mod";await Task.Delay(650);await Capture("search-empty-4x3");await Click("EmptyAction");
        await Size(1280,720);await Capture("installed-720");
    }
    static async Task Catalog()
    {
        if(Menu.current=="Main")Page.Open();
        await Click("Source");Page.GetComponentsInChildren<InputField>().First(f=>f.name=="SourceUrl").text="http://127.0.0.1:18766/";await Click("SaveSource");
        await Until(()=>Page.GetComponentsInChildren<Button>().Any(b=>b.name=="Mod-example.precision-dot"));
        await Size(1920,1080);await Capture("explore-1080");await Size(1024,768);await Capture("explore-long-list-4x3");
        await Click("Mod-verification.list-000");await Capture("long-detail-4x3");
        Check(Page.description.preferredHeight>200,"long description scrolls");
        Page.Close();var scroll=Page.GetComponentsInChildren<ScrollRect>().Single(s=>s.name=="ModList");
        var visible=scroll.content.GetComponentsInChildren<Button>()[8];scroll.StopMovement();Canvas.ForceUpdateCanvases();scroll.content.anchoredPosition=new Vector2(0,-((RectTransform)visible.transform).anchoredPosition.y);await Task.Delay(200);
        float position=scroll.verticalNormalizedPosition;await Click(visible.name);Page.Close();Check(Mathf.Abs(scroll.verticalNormalizedPosition-position)<.04f,"detail returns to list scroll position");
        await Click("Next");Check(!Page.GetComponentsInChildren<Button>().Any(b=>b.name=="Mod-example.precision-dot"),"pagination changes results");await Capture("page-two-4x3");await Click("Previous");
        await Click("Mod-example.precision-dot");await Click("Primary");await Capture("install-confirmation-4x3");await Click("ConfirmAction");
        await Until(()=>Service.Installed.Any(p=>p.manifest.id=="example.precision-dot"));
        await Click("Downloads");await Capture("download-complete-4x3");
        await Click("Installed");await Click("Mod-example.precision-dot");await Click("Primary");
        Check(Service.Installed.Single(p=>p.manifest.id=="example.precision-dot").requested,"external enable saved on disk");
        Check(!BuiltinModules.Instance.Manager.Installed.Any(r=>r.Manifest.Id=="example.precision-dot"&&r.Active),"external enable does not falsely claim active");
        await Capture("restart-required-4x3");
    }
    static async Task Restart()
    {
        Check(BuiltinModules.Instance.Manager.Installed.Any(r=>r.Manifest.Id=="example.precision-dot"&&r.Active),"external module loaded after restart");
        Check(Flats.UI.CrosshairPresentation.Appearance.Style==Flats.UI.CrosshairStyle.Dot,"external preset controls actual HUD");
        Page.Open();await Click("Mod-example.precision-dot");await Size(1280,720);await Capture("external-active-720");
        await Click("Secondary");Check(Find("Secondary").GetComponentInChildren<Text>().text.StartsWith("Update to"),"update discovered from source");
        await Click("Secondary");await Click("ConfirmAction");await Until(()=>Service.Installed.Single(p=>p.manifest.id=="example.precision-dot").manifest.version=="1.0.1");
        Check(Service.Running.Single(p=>p.manifest.id=="example.precision-dot").manifest.version=="1.0.0","update preserves running version until restart");await Capture("updated-restart-required-720");
        await Click("Primary");Check(!Service.Installed.Single(p=>p.manifest.id=="example.precision-dot").requested,"disable saved");
        Check(BuiltinModules.Instance.Manager.Installed.Single(r=>r.Manifest.Id=="example.precision-dot").Active,"running external remains active until restart");
        await Click("Remove");await Capture("remove-confirmation-720");await Click("CancelAction");Check(Service.Installed.Length==1,"cancel removal preserves installed mod");
        await Click("Remove");await Click("ConfirmAction");await Until(()=>Service.Installed.Length==0);await Capture("removed-pending-restart-720");
    }
    static async Task Removed()
    {
        Check(Service.Installed.Length==0,"removal survives restart");Check(!BuiltinModules.Instance.Manager.Installed.Any(r=>r.Manifest.Id=="example.precision-dot"),"removed module no longer loaded");
        Page.Open();await Click("Source");Page.GetComponentsInChildren<InputField>().First(f=>f.name=="SourceUrl").text="http://127.0.0.1:18767/";await Click("SaveSource");
        await Until(()=>Page.GetComponentsInChildren<Text>().Any(t=>t.text.StartsWith("Source unavailable")));await Capture("source-offline-720");
        await Click("Installed");Check(Page.GetComponentsInChildren<Button>().Any(b=>b.name=="Mod-flats.crosshair"),"local library available during source outage");
    }
    static async Task Navigation()
    {
        while(Menu.current=="Modules")Page.Close();var menu=UnityEngine.Object.FindFirstObjectByType<Menu>();
        menu.Fade(0);await Until(()=>Menu.current=="Play");await Task.Delay(700);await Capture("play-navigation");
        menu.Fade(0);await Until(()=>Menu.current=="Singleplayer");await Task.Delay(700);Check(menu.buttons[3].transform.parent.GetComponentInChildren<Text>().text=="Training","singleplayer entry preserved");
        menu.Fade(-1);await Until(()=>Menu.current=="Play");await Task.Delay(700);menu.Fade(1);await Until(()=>Menu.current=="Multiplayer");await Task.Delay(700);
        Check(menu.buttons[0].transform.parent.GetComponentInChildren<Text>().text=="Open Match","multiplayer entry preserved");
        menu.Fade(-1);await Until(()=>Menu.current=="Play");await Task.Delay(700);menu.Fade(-1);await Until(()=>Menu.current=="Main");await Task.Delay(700);await Capture("main-navigation");
    }
    static async Task Edges()
    {
        if(Menu.current=="Main")Page.Open();
        await Click("Source");Page.GetComponentsInChildren<InputField>().First(f=>f.name=="SourceUrl").text="http://127.0.0.1:18766/";await Click("SaveSource");
        var search=Page.GetComponentsInChildren<InputField>().First(f=>f.name=="Search");search.text="verification.list-000";await Task.Delay(900);
        await Click("Mod-verification.list-000");await Click("Primary");await Click("ConfirmAction");
        await Until(()=>Service.Downloads.Snapshot().Any(j=>j.Id=="verification.list-000"&&j.State==DownloadState.Failed));
        await Click("Downloads");await Click("Mod-verification.list-000");await Capture("failed-download-720");
        Check(Service.Installed.Length==0,"failed package not installed");await Click("Primary");await Task.Delay(900);
        Check(Service.Downloads.Snapshot().Any(j=>j.Id=="verification.list-000"&&j.State==DownloadState.Failed),"retry performs a real request and reports failure");
        await Click("Explore");search=Page.GetComponentsInChildren<InputField>().First(f=>f.name=="Search");search.text="verification.list-001";await Task.Delay(900);
        // Clear filters through the real empty-state action, then include incompatible versions.
        await Click("EmptyAction");await Task.Delay(500);await Click("Filter");search.text="verification.list-001";await Task.Delay(900);
        await Click("Mod-verification.list-001");Check(!Page.enable.interactable,"incompatible install disabled");await Capture("incompatible-detail-720");
        await Click("Explore");search.text="example.precision-dot";await Task.Delay(900);
        await Until(()=>Page.GetComponentsInChildren<RawImage>().Any(i=>i.texture!=null));
        var thumbnail=Page.GetComponentsInChildren<RawImage>().First(i=>i.texture!=null);
        Check(Mathf.Abs(thumbnail.rectTransform.rect.width/thumbnail.rectTransform.rect.height-(float)thumbnail.texture.width/thumbnail.texture.height)<.01f,"non-square thumbnail preserves aspect ratio");
        await Click("Mod-example.precision-dot");await Task.Delay(400);await Capture("artwork-detail-720");
        await Click("Explore");search.text="";await Task.Delay(900);await Size(1920,1080);await Task.Delay(1800);await Capture("explore-final-1080");
        await Size(1024,768);await Capture("explore-final-4x3");await Click("Mod-verification.list-000");await Capture("long-detail-final-4x3");
        await Click("Source");Page.GetComponentsInChildren<InputField>().First(f=>f.name=="SourceUrl").text="";await Click("SaveSource");
        await Click("Installed");await Size(1280,720);await Capture("installed-final-720");
        await Click("Mod-flats.crosshair");Check(Page.preview.GetComponent<CanvasRenderer>()!=null,"live preview renderer present");await Capture("builtin-final-720");
    }
    static async Task Loading()
    {
        if(Menu.current=="Main")Page.Open();
        await Click("Source");Page.GetComponentsInChildren<InputField>().First(f=>f.name=="SourceUrl").text="http://127.0.0.1:18768/";await Click("SaveSource");
        Check(Page.GetComponentsInChildren<Text>().Any(t=>t.text=="Loading mods..."),"real pending catalogue request displays loading");await Capture("loading-720");
        await Click("Installed");await Task.Delay(4000);Check(Page.GetComponentsInChildren<Button>().Any(b=>b.name=="Mod-flats.crosshair"),"cancelled browse cannot replace current tab");
        await Click("Source");Page.GetComponentsInChildren<InputField>().First(f=>f.name=="SourceUrl").text="";await Click("SaveSource");
    }
}
