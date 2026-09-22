using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Flats.Modules;
using Flats.UI;

public sealed partial class ModCenterVerification
{
    async Task Official()
    {
        Check("release player ignores legacy source",Application.isEditor || Debug.isDebugBuild || Service.SourceUrl==OfficialModEndpoint.Url);
        if(!Application.isEditor&&!Debug.isDebugBuild)
        {
            bool rejected=false;try{await Service.ConfigureSource("http://127.0.0.1:8766/");}catch(InvalidOperationException){rejected=true;}
            Check("verification flag cannot replace release endpoint",rejected);
        }
        if(phase=="OfficialPolicy")return;
        await MainClick(1,"Modules");
        Check("source UI removed",!page.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Source"||t.name=="SourceSettings"||t.name=="SourceUrl"));
        await Capture("official-installed");await Click("Explore");await Task.Delay(500);
        Check("no player configuration instructions",!TextHas("Set up a source")&&!TextHas("Catalogue URL"));
        await Capture("official-explore");await Click("Installed");await Click("Import");
        await Capture("local-import-preserved");
    }
    async Task MainClick(int index,string state)
    {
        var b=menu.buttons[index].transform.parent.GetComponent<Button>();await Until(()=>b.interactable&&b.gameObject.activeInHierarchy,30);Check("normal menu button "+index,b.interactable&&b.gameObject.activeInHierarchy);b.onClick.Invoke();await Until(()=>Menu.current==state);await Task.Delay(1100);
    }
    async Task BackMain(string state)
    {
        menu.backButton.GetComponent<Button>().onClick.Invoke();await Until(()=>Menu.current==state);await Task.Delay(900);
    }
    async Task SettingsEntry()
    {
        if(Screen.width<1450){await Click("Details-"+CrosshairModule.Id);await Click("Secondary");}
        else await Click("Configure");
    }
    async Task SetInput(string name,string value)
    {
        var field=page.GetComponentsInChildren<InputField>().Single(f=>f.name==name);
        field.text=value;await Task.Delay(800);
    }
    bool TextHas(string value)=>page.GetComponentsInChildren<Text>().Any(t=>t.text.Contains(value));
    async Task Approved()
    {
        await Until(()=>menu.buttons[1].transform.parent.gameObject.activeInHierarchy&&menu.buttons[1].transform.parent.GetComponent<Button>().interactable,30);await Task.Delay(1000);
        await Capture("01-main");
        if(phase=="ApprovedRestart")
        {
            Check("crosshair persisted after process restart",BuiltinModules.Instance.Crosshair.Settings.style==CrosshairStyle.Ring&&BuiltinModules.Instance.Crosshair.Settings.size==26);
            Check("enabled persisted after process restart",BuiltinModules.Instance.Manager.Installed.Single(r=>r.Manifest.Id==CrosshairModule.Id).Active);
            await MainClick(1,"Modules");await Capture("03-restored");return;
        }
        await MainClick(1,"Modules");await Capture("03-installed");
        Check("default installed and real one module",Button("Mod-"+CrosshairModule.Id).gameObject.activeInHierarchy);
        Check("selection does not enable",!BuiltinModules.Instance.Manager.Installed.Single(r=>r.Manifest.Id==CrosshairModule.Id).Requested);
        if(phase=="ApprovedTransport"){await ApprovedTransport();return;}
        if(phase=="ApprovedIssues"){await ApprovedIssues();return;}
        if(phase=="ApprovedStress"){await ApprovedStress();return;}
        if(phase=="ApprovedUpdate"){await ApprovedUpdate();return;}
        if(phase=="ApprovedNetwork"){await ApprovedNetwork();return;}
        if(phase=="ApprovedLayout"){await SettingsEntry();await Capture("05-settings");await Click("CancelDraft");await Click("Explore");await Capture("17-no-source");return;}
        await SettingsEntry();await Click("StyleRing");await Click("CrosshairLarger");await Capture("05-draft");
        Check("draft does not change saved appearance",BuiltinModules.Instance.Crosshair.Settings.style==CrosshairStyle.Cross&&BuiltinModules.Instance.Crosshair.Settings.size==24);
        Check("preview is three times HUD setting",page.preview.Diameter==78);
        await Click("CancelDraft");await Capture("24-unsaved");await Click("CancelAction");Check("stay retains draft",page.preview.Style==CrosshairStyle.Ring);
        await Click("CancelDraft");await Click("ExtraAction");
        Check("discard leaves saved settings intact",BuiltinModules.Instance.Crosshair.Settings.style==CrosshairStyle.Cross);
        await SettingsEntry();await Click("StyleRing");await Click("CrosshairLarger");await Click("SaveDraft");
        Check("saved draft applies real values",BuiltinModules.Instance.Crosshair.Settings.style==CrosshairStyle.Ring&&BuiltinModules.Instance.Crosshair.Settings.size==26);
        Check("save does not enable disabled mod",!BuiltinModules.Instance.Manager.Installed.Single(r=>r.Manifest.Id==CrosshairModule.Id).Requested);
        await SettingsEntry();await Click("CrosshairDefaults");await Capture("23-defaults");await Click("ConfirmAction");
        Check("defaults affect draft only",page.preview.Style==CrosshairStyle.Cross&&BuiltinModules.Instance.Crosshair.Settings.style==CrosshairStyle.Ring);
        await Click("CancelDraft");await Click("ExtraAction");
        await SettingsEntry();await Click("StyleDot");
        string settings=Path.Combine(Arg("-flats-module-settings-dir"),"mod-profiles-v1.json");
        using(var locked=new FileStream(settings,FileMode.Open,FileAccess.Read,FileShare.None)){await Click("SaveDraft");Check("failed save keeps draft open",Button("SaveDraft").gameObject.activeInHierarchy&&TextHas("Could not save"));await Capture("save-failure");}
        await Click("CancelDraft");await Click("ExtraAction");Check("failed save retained prior setting",BuiltinModules.Instance.Crosshair.Settings.style==CrosshairStyle.Ring);
        await SetInput("Search","nothing-matches-xyz");await Capture("21-no-results");await Click("EmptySecondary");Check("clear search restored real row",Button("Mod-"+CrosshairModule.Id).gameObject.activeInHierarchy);
        await Click("Explore");await Capture("17-no-source");await Click("Source");await SetInput("SourceUrl","not-a-url");await Click("SaveSource");Check("invalid source remains editable",TextHas("Could not save"));await Capture("source-invalid");await Click("CancelSource");
        await Click("Downloads");await Capture("12-empty-downloads");await Click("Installed");
        await Click("Toggle-"+CrosshairModule.Id);Check("enabled runtime confirmed",BuiltinModules.Instance.Manager.Installed.Single(r=>r.Manifest.Id==CrosshairModule.Id).Active);
        await Click("ModulesBack");await Until(()=>Menu.current=="Main");
        await MainClick(0,"Play");await Capture("02-play");await MainClick(0,"Singleplayer");await Capture("singleplayer");await BackMain("Play");await MainClick(1,"Multiplayer");await Capture("multiplayer");await BackMain("Play");await BackMain("Main");
        foreach(var entry in new[]{Tuple.Create(2,"Character"),Tuple.Create(3,"Settings"),Tuple.Create(4,"Leaderboard"),Tuple.Create(5,"Information")})
        {
            await MainClick(entry.Item1,entry.Item2);await Capture(entry.Item2.ToLowerInvariant());await BackMain("Main");
        }
        await MainClick(0,"Play");await MainClick(0,"Singleplayer");
        var training=menu.buttons[3].transform.parent.GetComponent<Button>();training.onClick.Invoke();
        await Until(()=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="MainMenu",30);
        await Until(()=>FindObjectsByType<LocalCrosshairPresenter>(FindObjectsSortMode.None).Any(p=>p.IsCustom),30);
        await Task.Delay(2200);await Capture("game-hud");
        Check("real gameplay crosshair",FindObjectsByType<LocalCrosshairPresenter>(FindObjectsSortMode.None).Any(p=>p.IsCustom));
        var graphic=FindObjectsByType<CrosshairGraphic>(FindObjectsSortMode.None).First(g=>g.name=="ModuleCrosshair");
        Check("HUD uses saved size not preview magnification",graphic.Diameter==26&&graphic.Style==CrosshairStyle.Ring);
    }
    async Task ApprovedNetwork()
    {
        await Click("Source");await SetInput("SourceUrl","http://127.0.0.1:18866/");await Click("CheckSource");await Until(()=>TextHas("Connection successful"));await Capture("18-source");await Click("SaveSource");
        await Until(()=>page.GetComponentsInChildren<Button>().Any(b=>b.name=="Mod-example.precision-dot"));await Capture("06-catalogue");
        await Click("Category");await Click("Category-HUD");await Capture("07-filters");await Click("ClearFilters");await Click("CloseFilters");
        await SetInput("Search","Precision");await Click("Mod-example.precision-dot");await Capture("08-details");await Click("Versions");await Capture("09-versions");await Click("ModulesBack");
        Check("search preserved on return",page.GetComponentsInChildren<InputField>().Single(f=>f.name=="Search").text=="Precision");
        await Click("Mod-example.precision-dot");await Click("Primary");await Capture("11-install-review");await Click("ConfirmAction");
        await Click("ModulesBack");await Click("Downloads");await Until(()=>Service.Downloads.Snapshot().Any(j=>j.Id=="example.precision-dot"&&!j.Busy));await Task.Delay(700);await Capture("12-download-installed");
        Check("real HTTP install",Service.Installed.Any(p=>p.manifest.id=="example.precision-dot"));
        await Click("Installed");await Click("Toggle-example.precision-dot");Check("enabled intent requires restart",Service.Installed.Single(p=>p.manifest.id=="example.precision-dot").requested&&!BuiltinModules.Instance.Manager.Installed.Any(r=>r.Manifest.Id=="example.precision-dot"&&r.Active));await Capture("restart-required");
        await Click("Import");await SetInput("PackagePath",Path.Combine(Arg("-flats-fixture-dir"),"packages/example.wide-ring-1.0.0.zip"));await Click("ReviewImport");await Capture("19-import-review");await Click("ConfirmAction");await Until(()=>Service.Installed.Length==2);await Capture("04-multiple-installed");
        await Click("Import");await SetInput("PackagePath",Path.Combine(Arg("-flats-fixture-dir"),"invalid.zip"));await Click("ReviewImport");Check("invalid import reports error",TextHas("Could not review"));await Capture("import-failure");await Click("CancelImport");
        await Click("Details-example.wide-ring");await Click("Remove");await Capture("22-uninstall");await Click("CancelAction");Check("cancel uninstall retains package",Service.Installed.Length==2);
        await Click("Remove");await Click("ConfirmAction");await Until(()=>Service.Installed.Length==1);await Click("ModulesBack");
        await Click("Source");await SetInput("SourceUrl","http://127.0.0.1:18867/");await Click("SaveSource");await Until(()=>TextHas("Source unavailable"));await Capture("26-unavailable");
        await Click("Source");await SetInput("SourceUrl","http://127.0.0.1:18866/");await Click("SaveSource");await Until(()=>page.GetComponentsInChildren<Button>().Any(b=>b.name=="Mod-example.precision-dot"));await Capture("source-recovered");
    }
    async Task ApprovedUpdate()
    {
        await Click("Category");await Task.Delay(900);await Click("Details-example.precision-dot");await Click("Secondary");await Capture("14-update-review");await Click("ConfirmAction");await Click("ModulesBack");await Click("Downloads");
        await Until(()=>Service.Downloads.Snapshot().Any(j=>j.Id=="example.precision-dot"&&!j.Busy));
        await Service.RefreshInstalled();
        Check("update installed new version",Service.Installed.Single(p=>p.manifest.id=="example.precision-dot").manifest.version=="1.0.1");
        Check("update retained runtime old version",Service.Running.Single(p=>p.manifest.id=="example.precision-dot").manifest.version=="1.0.0");
        Check("update retained requested state",Service.Installed.Single(p=>p.manifest.id=="example.precision-dot").requested);
        await Click("Installed");await Capture("13-updated-library");
    }
    async Task ApprovedStress()
    {
        for(int i=0;i<9;i++)
        {
            await Click("Import");await SetInput("PackagePath",Path.Combine(Arg("-flats-fixture-dir"),"packages/verification.library-"+i.ToString("00")+".zip"));
            await Click("ReviewImport");await Click("ConfirmAction");await Until(()=>Service.Installed.Length==i+1);
        }
        await Capture("04-long-library");
        var scroll=page.GetComponentsInChildren<ScrollRect>().Single(s=>s.name=="ModList");
        scroll.verticalNormalizedPosition=0;await Task.Delay(300);await Capture("library-scrolled");
        await Click("Details-verification.library-08");await Click("ModulesBack");
        Check("scroll retained after details",scroll.verticalNormalizedPosition<.1f);
        await SetInput("Search","deliberately");await Click("Details-verification.library-03");await Capture("long-name-details");await Click("ModulesBack");
        Check("query retained after details",page.GetComponentsInChildren<InputField>().Single(f=>f.name=="Search").text=="deliberately");
        await SetInput("Search","");await Click("Source");await SetInput("SourceUrl","http://127.0.0.1:18866/");await Click("SaveSource");
        await Until(()=>page.GetComponentsInChildren<Button>().Any(b=>b.name=="Mod-verification.library-09"));
        await Click("Mod-verification.library-09");await Click("Primary");await Click("ConfirmAction");await Click("ModulesBack");await Click("Downloads");
        await Until(()=>Service.Downloads.Snapshot().Any(j=>j.Id=="verification.library-09"&&j.State==DownloadState.Failed));await Capture("download-failure");
        Check("failed package not installed",!Service.Installed.Any(p=>p.manifest.id=="verification.library-09"));
        File.Copy(Path.Combine(Arg("-flats-fixture-dir"),"retry-good.zip"),Path.Combine(Arg("-flats-fixture-dir"),"packages/verification.library-09.zip"),true);
        await Click("Mod-verification.library-09");await Click("Primary");
        await Until(()=>Service.Downloads.Snapshot().Any(j=>j.Id=="verification.library-09"&&j.State==DownloadState.Installed));await Capture("download-retry-success");
    }

    async Task ApprovedIssues()
    {
        await Click("Import");await SetInput("PackagePath",Path.Combine(Arg("-flats-fixture-dir"),"packages/verification.dependent.zip"));await Click("ReviewImport");await Click("ConfirmAction");await Until(()=>Service.Installed.Length==1);
        await Click("StatusProblems");await Capture("15-needs-attention");await Click("Details-verification.dependent");await Click("Dependencies");await Capture("10-dependencies");
        Check("missing dependency blocks enabling",!Button("Primary").interactable&&TextHas("Not installed"));await Click("ModulesBack");await Click("StatusAll");
        await Click("Import");await SetInput("PackagePath",Path.Combine(Arg("-flats-fixture-dir"),"packages/verification.required.zip"));await Click("ReviewImport");await Click("ConfirmAction");await Until(()=>Service.Installed.Length==2);
        await Click("Toggle-verification.lifecycle");await Click("Details-verification.dependent");Check("resolved requirements allow enable",Button("Primary").interactable);await Click("Primary");await Click("ModulesBack");await Capture("15-resolved");
    }

    async Task ApprovedTransport()
    {
        await Click("Source");await SetInput("SourceUrl","http://127.0.0.1:18868/");await Click("SaveSource");await Capture("25-loading");
        await Until(()=>page.GetComponentsInChildren<Button>().Any(b=>b.name=="Mod-verification.transport"),30);
        await Click("Mod-verification.transport");await Click("Primary");await Click("ConfirmAction");await Click("ModulesBack");await Click("Downloads");
        await Until(()=>Service.Downloads.Snapshot().Any(j=>j.Id=="verification.transport"&&j.Received>0));await Capture("12-progress");
        Check("progress uses received HTTP bytes",Service.Downloads.Snapshot().Any(j=>j.Received>0&&j.Received<j.Total));
        await Click("QueueAction");await Until(()=>Service.Downloads.Snapshot().Any(j=>j.State==DownloadState.Cancelled));await Capture("12-cancelled");
        Check("cancel did not install",!Service.Installed.Any(p=>p.manifest.id=="verification.transport"));
        await Click("QueueAction");await Until(()=>Service.Downloads.Snapshot().Any(j=>j.State==DownloadState.Installed),120);await Task.Delay(700);await Capture("12-retry-installed");
        Check("retry installed verified ZIP",Service.Installed.Any(p=>p.manifest.id=="verification.transport"&&!p.requested));
    }

}
