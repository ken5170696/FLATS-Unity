using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Flats.Modules;
using Flats.UI;

public sealed partial class ModuleManagementPage
{
    Transform brandIcon,footerRule;
    Button[] statusTabs;
    Text versionHeading,stateHeading,enabledHeading;
    Text modalTitle;
    ScrollRect modalScroll;
    RectTransform modalContent;
    Text subtitle,listHeading,columnHeading,quickTitle,quickInfo,draftNotice;
    GameObject quickPanel;
    Button quickConfigure,quickDetails,extraConfirm,importButton,saveDraft,cancelDraft;
    CrosshairGraphic quickPreview;
    CrosshairSettings draft;
    bool settingsOpen;
    Image previewBackdrop;
    RectTransform draftControls;
    Slider sizeSlider;
    Button[] styles;
    bool Dirty=>draft!=null&&(draft.style!=BuiltinModules.Instance.ConfiguredCrosshair.style||!Mathf.Approximately(draft.size,BuiltinModules.Instance.ConfiguredCrosshair.size));
    void BuildApproved()
    {
        BuildDetailSections();
        modalTitle=ui.Text("ModalTitle",confirmPanel.transform,"",0,156,830,54,28);
        ui.Panel("AccentRule",confirmPanel.transform,0,208,890,5,ModCenterWidgets.Accent);
        confirmText.alignment=TextAnchor.UpperLeft;
        modalScroll=ui.Scroll("ConfirmationBody",confirmPanel.transform,0,-4,830,244,out modalContent);
        confirmText.transform.SetParent(modalContent,false);
        confirmText.rectTransform.anchorMin=confirmText.rectTransform.anchorMax=new Vector2(.5f,1);confirmText.rectTransform.pivot=new Vector2(.5f,1);confirmText.rectTransform.anchoredPosition=Vector2.zero;
        var primary=confirmPanel.transform.Find("ConfirmAction").GetComponent<Button>();primary.image.color=ModCenterWidgets.Accent;primary.GetComponentInChildren<Text>().color=Color.white;
        brandIcon=ui.Rect("Brand",root,0,0,34,34);var icon=brandIcon.gameObject.AddComponent<ModTileGraphic>();icon.color=ModCenterWidgets.Accent;icon.raycastTarget=false;
        subtitle=ui.Text("Subtitle",root,"Enable, configure and manage your mods.",0,0,600,26,16,ModCenterWidgets.Muted);
        footerRule=ui.Panel("FooterRule",root,0,0,100,1,ModCenterWidgets.Line).transform;
        listHeading=ui.Text("LibraryFilter",root,"All installed mods",0,0,600,46,18,ModCenterWidgets.Accent);
        columnHeading=ui.Text("Columns",root,"MOD",0,0,600,28,14,ModCenterWidgets.Muted);
        statusTabs=new Button[4];var names=new[]{"All","Enabled","Updates","Problems"};var labels=new[]{"All","Enabled","Updates","Needs attention"};
        for(int i=0;i<4;i++){string value=names[i];statusTabs[i]=ui.Button("Status"+value,root,labels[i],0,0,i==3?174:120,44,()=>{localFilter=value;offset=0;Reload();},ModCenterWidgets.Paper);}
        versionHeading=ui.Text("VersionHeading",root,"VERSION",0,0,100,28,14,ModCenterWidgets.Muted);
        stateHeading=ui.Text("StateHeading",root,"STATUS",0,0,160,28,14,ModCenterWidgets.Muted);
        enabledHeading=ui.Text("EnabledHeading",root,"ENABLED",0,0,100,28,14,ModCenterWidgets.Muted);
        quickPanel=ui.Panel("SelectedSummary",root,0,0,344,500,Color.white).gameObject;
        quickPreview=ui.Rect("Preview",quickPanel.transform,0,170,300,84).gameObject.AddComponent<CrosshairGraphic>();quickPreview.color=ModCenterWidgets.Accent;quickPreview.raycastTarget=false;
        quickTitle=ui.Text("Name",quickPanel.transform,"",0,100,300,76,25);
        quickInfo=ui.Text("Info",quickPanel.transform,"",0,0,300,160,16,ModCenterWidgets.Muted);quickInfo.alignment=TextAnchor.UpperLeft;
        quickConfigure=ui.Button("Configure",quickPanel.transform,"Configure",0,-158,302,46,OpenSettings,ModCenterWidgets.Accent);
        quickDetails=ui.Button("ViewDetails",quickPanel.transform,"View details",0,-212,302,44,OpenDetail);
        extraConfirm=ui.Button("ExtraAction",confirmPanel.transform,"",0,0,230,46,null);
        extraConfirm.gameObject.SetActive(false);
        importButton=ui.Button("Import",root,"Import",0,0,128,46,ShowImport);
        BuildProfiles();
    }
    void LayoutQuick()
    {
        float h=((RectTransform)quickPanel.transform).rect.height;
        Place(quickPreview.transform,0,h/2-66,300,94);
        Place(quickTitle.transform,0,h/2-160,300,84);
        Place(quickInfo.transform,0,h/2-290,300,152);
        Place(quickConfigure.transform,0,-h/2+92,302,46);
        Place(quickDetails.transform,0,-h/2+38,302,44);
    }
    void RefreshQuick()
    {
        var record=BuiltinModules.Instance.Manager.Installed.FirstOrDefault(r=>r.Manifest.Id==selectedId);
        var package=Service.Installed.FirstOrDefault(p=>p.manifest.id==selectedId);
        bool builtin=selectedId==CrosshairModule.Id;
        quickTitle.text=builtin?"Custom Crosshair":package?.manifest.name??record?.Manifest.DisplayName??"Select a mod";
        quickInfo.text=builtin?"Client-only  /  "+(BuiltinModules.Instance.Requested(CrosshairModule.Id)?"Enabled in selected profile":"Disabled in selected profile")+" / "+(record.Active?"Active now":"Not active now")+"\n\nInstalled version: 1.0.0\n\nAffects: Your screen only":
            package!=null?package.manifest.scope+"\n\nVersion "+package.manifest.version+"\n\n"+InstalledStatus(package):"Select a row to view its status and actions.";
        quickConfigure.gameObject.SetActive(builtin);quickDetails.interactable=!string.IsNullOrEmpty(selectedId);
        quickPreview.gameObject.SetActive(builtin);if(builtin)quickPreview.Set(BuiltinModules.Instance.ConfiguredCrosshair.style,54);
        foreach(Transform row in listContent){var b=row.GetComponent<Button>();if(b!=null)b.image.color=row.name=="Mod-"+selectedId?ModCenterWidgets.Tint:ModCenterWidgets.Paper;}
        listHeading.text="";foreach(var b in statusTabs){b.GetComponentInChildren<Text>().color=b.name=="Status"+localFilter?ModCenterWidgets.Accent:ModCenterWidgets.Muted;}
    }
    void SelectRow(string id)
    {
        selectedId=id;RefreshQuick();
        if(tab!="Installed"||!Wide)OpenDetail();
    }
    void OpenDetail() { detailSection="Overview";SaveView();detailOpen=true;detailScroll.verticalNormalizedPosition=1;ShowDetail();Focus(enable); }
    void ToggleRow(string id)
    {
        if(id==CrosshairModule.Id){var r=BuiltinModules.Instance.Manager.Installed.First(x=>x.Manifest.Id==id);BuiltinModules.Instance.Request(id,!BuiltinModules.Instance.Requested(id));notice.text=BuiltinModules.Instance.Notice;RenderLocal();}
        else {var p=Service.Installed.FirstOrDefault(x=>x.manifest.id==id);if(p!=null){if(p.requested)Run(()=>Service.Request(id,false));else ReviewPlan(p.manifest,null,true);}}
    }
    void BuildDraftSettings()
    {
        crosshairPanel=ui.Rect("CrosshairSettings",root,0,0,100,100).gameObject;
        ui.Text("PreviewTitle",crosshairPanel.transform,"Preview",0,0,300,30,22);
        previewBackdrop=ui.Panel("PreviewBackdrop",crosshairPanel.transform,0,0,500,340,new Color(.68f,.64f,.68f));
        preview=ui.Rect("CrosshairPreview",previewBackdrop.transform,0,0,220,220).gameObject.AddComponent<CrosshairGraphic>();preview.color=Color.white;preview.raycastTarget=false;
        ui.Text("Magnification",previewBackdrop.transform,"3× preview · actual size uses HUD units",0,-130,440,30,16,Color.white);
        ui.Text("PreviewHint",crosshairPanel.transform,"Your changes affect the preview only.\nSave changes to keep this configuration.",0,0,500,76,18,ModCenterWidgets.Muted);
        ui.Button("LightPreview",crosshairPanel.transform,"Light",0,0,160,44,()=>previewBackdrop.color=new Color(.75f,.72f,.75f));
        ui.Button("DarkPreview",crosshairPanel.transform,"Dark",0,0,160,44,()=>previewBackdrop.color=new Color(.25f,.23f,.26f));
        draftControls=ui.Panel("SettingsControls",crosshairPanel.transform,0,0,500,400,Color.white).rectTransform;
        ui.Text("StyleTitle",draftControls,"Style",0,0,440,32,22);
        styles=new Button[3];
        for(int i=0;i<3;i++){var value=(CrosshairStyle)i;styles[i]=ui.Button("Style"+value,draftControls,value.ToString(),0,0,140,48,()=>Configure(value,draft.size));}
        style=styles[0];
        settingsLabel=ui.Text("SettingsLabel",draftControls,"",0,0,440,42,22);
        var sliderRoot=ui.Rect("SizeSlider",draftControls,0,0,380,46);sizeSlider=sliderRoot.gameObject.AddComponent<Slider>();sizeSlider.minValue=6;sizeSlider.maxValue=64;sizeSlider.wholeNumbers=true;
        var track=ui.Panel("Track",sliderRoot,0,0,380,6,ModCenterWidgets.ControlColor);
        var area=ui.Rect("HandleArea",sliderRoot,0,0,360,46);
        var handle=ui.Panel("Handle",area,0,0,18,24,ModCenterWidgets.Accent);handle.rectTransform.sizeDelta=new Vector2(18,-22);sizeSlider.handleRect=handle.rectTransform;sizeSlider.targetGraphic=handle;
        sizeSlider.onValueChanged.AddListener(value=>Configure(draft.style,value));
        smaller=ui.Button("CrosshairSmaller",draftControls,"−",0,0,48,46,()=>Configure(draft.style,draft.size-2));
        larger=ui.Button("CrosshairLarger",draftControls,"+",0,0,48,46,()=>Configure(draft.style,draft.size+2));
        ui.Text("CapabilityHint",draftControls,"Changes apply when you save.\nSaving does not enable a disabled mod.",0,0,440,80,18,ModCenterWidgets.Muted);
        reset=ui.Button("CrosshairDefaults",crosshairPanel.transform,"Restore defaults",0,0,210,46,()=>Ask("Restore Crosshair defaults?\n\nThis changes only this mod's draft. Save changes to apply it. Other mods and game settings are not affected.",()=>Configure(CrosshairStyle.Cross,24)));
        saveDraft=ui.Button("SaveDraft",crosshairPanel.transform,"Save changes",0,0,188,46,()=>SaveDraft(true),ModCenterWidgets.Accent);
        cancelDraft=ui.Button("CancelDraft",crosshairPanel.transform,"Cancel",0,0,110,46,LeaveSettings);
        draftNotice=ui.Text("DraftNotice",crosshairPanel.transform,"",0,0,500,42,16,ModCenterWidgets.Muted);
        crosshairPanel.SetActive(false);
    }
    void LayoutDraft()
    {
        Place(crosshairPanel.transform,0,0,layoutWidth,layoutHeight);
        float left=(ContentWidth-32)*.47f,right=ContentWidth-left-32,top=148;
        Box(crosshairPanel.transform.Find("PreviewTitle"),30,106,left,32);
        Box(previewBackdrop.transform,30,top,left,Mathf.Min(360,layoutHeight-370));
        Place(previewBackdrop.transform.Find("Magnification"),0,-previewBackdrop.rectTransform.rect.height/2+24,left-32,32);
        float after=top+previewBackdrop.rectTransform.rect.height+16;
        Box(crosshairPanel.transform.Find("LightPreview"),30,after,(left-12)/2,44);
        Box(crosshairPanel.transform.Find("DarkPreview"),42+(left-12)/2,after,(left-12)/2,44);
        Box(crosshairPanel.transform.Find("PreviewHint"),30,after+60,left,78);
        Box(draftControls,62+left,108,right,layoutHeight-214);
        float h=draftControls.rect.height;
        Place(draftControls.Find("StyleTitle"),0,h/2-42,right-48,32);
        for(int i=0;i<3;i++)Place(styles[i].transform,-(right-48)/2+(right-64)/6+i*((right-64)/3+8),h/2-98,(right-64)/3,48);
        Place(settingsLabel.transform,0,h/2-168,right-48,42);
        Place(sizeSlider.transform,0,h/2-220,right-170,46);
        Place(sizeSlider.transform.Find("Track"),0,0,right-170,6);Place(sizeSlider.transform.Find("HandleArea"),0,0,right-190,46);
        Place(smaller.transform,-right/2+48,h/2-220,48,46);Place(larger.transform,right/2-48,h/2-220,48,46);
        Place(draftControls.Find("CapabilityHint"),0,-h/2+70,right-48,90);
        Box(reset.transform,30,layoutHeight-62,210,46);Box(saveDraft.transform,layoutWidth-218,layoutHeight-62,188,46);Box(cancelDraft.transform,layoutWidth-340,layoutHeight-62,110,46);
        Box(draftNotice.transform,260,layoutHeight-62,layoutWidth-620,46);
    }
    void OpenSettings()
    {
        var saved=BuiltinModules.Instance.ConfiguredCrosshair;draft=new CrosshairSettings{style=saved.style,size=saved.size};
        SaveView();settingsOpen=true;detailOpen=false;draftNotice.text="";ApplyView();RefreshDraft();Focus(styles[(int)draft.style]);
    }
    void RefreshDraft()
    {
        preview.Set(draft.style,draft.size*3);settingsLabel.text="Size: "+draft.size+" HUD units";
        sizeSlider.SetValueWithoutNotify(draft.size);
        for(int i=0;i<styles.Length;i++){styles[i].image.color=i==(int)draft.style?ModCenterWidgets.Tint:ModCenterWidgets.Paper;}
        draftNotice.text=Dirty?"Unsaved changes":"Saved settings";saveDraft.interactable=!BuiltinModules.Instance.ReadOnly;
    }
    bool SaveDraft(bool leave)
    {
        if(!BuiltinModules.Instance.Configure(draft.style,draft.size)){draftNotice.text=BuiltinModules.Instance.Notice;return false;}
        if(leave)FinishSettings();else RefreshDraft();return true;
    }
    void FinishSettings() { settingsOpen=false;detailOpen=false;Reload();FocusSelected(); }
    void LeaveSettings()
    {
        if(!Dirty){FinishSettings();return;}
        Ask("Save your Crosshair changes?\n\nStyle: "+BuiltinModules.Instance.ConfiguredCrosshair.style+" → "+draft.style+"\nSize: "+BuiltinModules.Instance.ConfiguredCrosshair.size+" → "+draft.size+" HUD units\n\nThe draft has not been applied. Saving keeps the mod's enabled state.",()=>SaveDraft(true));
        confirmPanel.transform.Find("ConfirmAction").GetComponentInChildren<Text>().text="Save & leave";
        confirmPanel.transform.Find("CancelAction").GetComponentInChildren<Text>().text="Stay here";
        extraConfirm.gameObject.SetActive(true);extraConfirm.GetComponentInChildren<Text>().text="Discard & leave";extraConfirm.onClick.RemoveAllListeners();extraConfirm.onClick.AddListener(()=>{HideModal();FinishSettings();});
    }
    void ReviewRemoval()
    {
        Ask("Uninstall this mod?\n\nThe package will be removed. Its personal settings are retained. Any running version stays active until restart. Use Disable to keep the package.",()=>Run(()=>Service.Remove(selectedId)));
        extraConfirm.gameObject.SetActive(true);extraConfirm.GetComponentInChildren<Text>().text="Disable instead";extraConfirm.onClick.RemoveAllListeners();extraConfirm.onClick.AddListener(()=>{HideModal();Run(()=>Service.Request(selectedId,false));});
    }
}
