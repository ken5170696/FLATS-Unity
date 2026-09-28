using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Flats.Modules;
using Flats.UI;

public sealed partial class ModuleManagementPage
{
    [SerializeField] Transform brandIcon,footerRule;
    [SerializeField] Button[] statusTabs;
    [SerializeField] Text versionHeading,stateHeading,enabledHeading;
    [SerializeField] Text modalTitle;
    [SerializeField] ScrollRect modalScroll;
    [SerializeField] RectTransform modalContent;
    [SerializeField] Text subtitle,listHeading,columnHeading,quickTitle,quickInfo,draftNotice;
    [SerializeField] GameObject quickPanel;
    [SerializeField] Button quickConfigure,quickDetails,extraConfirm,importButton,saveDraft,cancelDraft;
    [SerializeField] CrosshairGraphic quickPreview;
    CrosshairSettings draft;
    bool settingsOpen;
    [SerializeField] Image previewBackdrop;
    [SerializeField] RectTransform draftControls;
    [SerializeField] Slider sizeSlider;
    [SerializeField] Button[] styles;
    [SerializeField, Tooltip("Settings-driven editor. When assigned it replaces the style buttons and size slider.")]
    ModuleSettingsForm settingsForm;
    [SerializeField, Tooltip("Optional authored Configure button in the detail panel for installed mods that declare settings. When absent the quick panel offers Configure.")]
    Button configure;
    bool Generic=>settingsForm!=null;
    // Mod API 1.2.0: the settings-driven form edits any installed module that declares
    // settings, not only the crosshair. Values are saved per profile and pushed to the
    // running module through its IModuleContext.
    string settingsModuleId=CrosshairModule.Id;
    SettingValue[] genericDraft=new SettingValue[0];
    bool ModuleSettings=>settingsModuleId!=CrosshairModule.Id;
    static string Signature(CrosshairSettings s)=>Signature(s.Values);
    static string Signature(SettingValue[] values)=>string.Join(";",(values??new SettingValue[0]).Select(v=>v.id+"="+v.value));
    bool Dirty=>ModuleSettings?Signature(genericDraft)!=Signature(Host.ConfiguredSettings(settingsModuleId)):draft!=null&&Signature(draft)!=Signature(Host.ConfiguredCrosshair);
    static bool HasSettings(PackageManifest m)=>m?.settings!=null && m.settings.Length>0;
    bool Configurable(InstalledPackage package)=>package!=null && (package.manifest.id==CrosshairModule.Id || (Generic && HasSettings(package.manifest)));
    string SettingsTitle=>ModuleSettings?PlayerName(Service.Installed.FirstOrDefault(p=>p.manifest.id==settingsModuleId)?.manifest.name):"Custom Crosshair";


    void RefreshQuick()
    {
        var record=Host.Manager.Installed.FirstOrDefault(r=>r.Manifest.Id==selectedId);
        var package=Service.Installed.FirstOrDefault(p=>p.manifest.id==selectedId);
        bool configurable=Configurable(package);
        bool crosshair=selectedId==CrosshairModule.Id && package!=null;
        ((FlatsLocalizedText)quickTitle).translate=crosshair || (package==null && record==null);
        quickTitle.text=crosshair?"Custom Crosshair":package?.manifest.name??record?.Manifest.DisplayName??"Select a mod";
        quickInfo.text=crosshair?"Client-only  /  "+(Host.Requested(CrosshairModule.Id)?"Enabled in selected profile":"Disabled in selected profile")+" / "+(record?.Active==true?"Active now":"Not active now")+"\n\nInstalled version: "+package.manifest.version+"\n\nAffects: Your screen only":
            package!=null?(package.manifest.scope=="ClientOnly"?"Your screen only":"Multiplayer mod")+"\n\nVersion "+package.manifest.version+"\n\n"+InstalledStatus(package):"Select a row to view its status and actions.";
        quickConfigure.gameObject.SetActive(configurable);quickDetails.interactable=!string.IsNullOrEmpty(selectedId);
        quickPreview.gameObject.SetActive(crosshair);if(crosshair)quickPreview.Set(Host.ConfiguredCrosshair.style,54);
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
        var p=Service.Installed.FirstOrDefault(x=>x.manifest.id==id);if(p!=null){if(p.requested)Run(()=>Service.Request(id,false));else ReviewPlan(p.manifest,null,true);}
    }
    
    
    void OpenSettings() { OpenSettings(selectedId); }
    void OpenSettings(string id)
    {
        var package=Service.Installed.FirstOrDefault(p=>p.manifest.id==id);
        if(id!=CrosshairModule.Id && !(Generic && HasSettings(package?.manifest)))return;
        settingsModuleId=id==CrosshairModule.Id?CrosshairModule.Id:id;
        // Start from every saved value so editing style or size keeps colour, thickness and outline.
        draft=Host.ConfiguredCrosshair.Clone();
        genericDraft=ModuleSettings?Host.ConfiguredSettings(id):new SettingValue[0];
        SaveView();settingsOpen=true;detailOpen=false;draftNotice.text="";ApplyView();
        ShowPreview(!ModuleSettings);
        if(ModuleSettings)
        {
            settingsForm.Bind(package.manifest.settings,package.manifest.presets,genericDraft,values=>{genericDraft=values;RefreshDraft();Host.PreviewSettings(settingsModuleId,genericDraft);});
            RefreshDraft();var first=settingsForm.GetComponentInChildren<Selectable>();if(first!=null)Focus(first);
        }
        else if(Generic)
        {
            // Settings are in HUD units; the preview shows them three times larger, as before.
            preview.rectTransform.localScale=Vector3.one*3;
            settingsForm.Bind(CrosshairSettingsSpec.Specs(),CrosshairSettingsSpec.Presets(),draft.Values,values=>{draft.Values=values;RefreshDraft();});
            RefreshDraft();var first=settingsForm.GetComponentInChildren<Selectable>();if(first!=null)Focus(first);
        }
        else {RefreshDraft();Focus(styles[(int)draft.style]);}
    }
    // The crosshair preview and its backdrop buttons are authored for the crosshair only.
    void ShowPreview(bool visible)
    {
        if(preview!=null)preview.gameObject.SetActive(visible);
        if(previewBackdrop!=null)previewBackdrop.gameObject.SetActive(visible);
        if(settingsLabel!=null)settingsLabel.gameObject.SetActive(visible);
        foreach(var name in new[]{"LightPreview","DarkPreview"}){var t=crosshairPanel.transform.Find(name);if(t!=null)t.gameObject.SetActive(visible);}
    }
    void RefreshDraft()
    {
        if(ModuleSettings) { }
        else if(Generic)preview.Set(draft);
        else
        {
            preview.Set(draft.style,draft.size*3);settingsLabel.text="Size: "+draft.size+" HUD units";
            if(sizeSlider!=null)sizeSlider.SetValueWithoutNotify(draft.size);
            if(styles!=null)for(int i=0;i<styles.Length;i++){styles[i].image.color=i==(int)draft.style?ModCenterWidgets.Tint:ModCenterWidgets.Paper;}
        }
        draftNotice.text=Dirty?"Unsaved changes":"Saved settings";saveDraft.interactable=!Host.ReadOnly&&Dirty;
    }
    bool SaveDraft(bool leave)
    {
        bool saved=ModuleSettings?Host.Configure(settingsModuleId,genericDraft):Host.Configure(draft.Values);
        if(!saved){draftNotice.text=Host.Notice;return false;}
        if(leave)FinishSettings();else RefreshDraft();return true;
    }
    void FinishSettings()
    {
        // Whatever was previewed, the running module ends on the saved values.
        if(ModuleSettings)Host.PreviewSettings(settingsModuleId,Host.ConfiguredSettings(settingsModuleId));
        settingsOpen=false;detailOpen=false;settingsModuleId=CrosshairModule.Id;
        if(restricted){restricted=false;Close();return;}
        ShowPreview(true);Reload();FocusSelected();
    }
    // Settings-only visit opened from another screen (the graphics settings notice). From the main
    // menu this is the ordinary Mod Center; in a paused match only the settings page is shown and
    // leaving it returns to the pause menu.
    bool restricted;
    public void OpenModuleSettings(string id)
    {
        if(Menu.current=="Modules"){OpenSettings(id);return;}
        if(Menu.gameState=="Main"){Open();if(Menu.current=="Modules")OpenSettings(id);return;}
        if(Menu.current!="Main")return;
        Initialize();optionsWereActive=mainOptions.activeSelf;mainOptions.SetActive(false);mainScreen.SetActive(false);
        Menu.current="Modules";gameObject.SetActive(true);restricted=true;
        tab="Installed";detailOpen=false;settingsOpen=false;Resize();Reload();
        OpenSettings(id);
        if(!settingsOpen){restricted=false;Close();}
    }
    void LeaveSettings()
    {
        if(!Dirty){FinishSettings();return;}
        Ask(ModuleSettings?"Save your changes to "+SettingsTitle+"?\n\nThe draft has not been applied. Saving keeps the mod's enabled state.":
            Generic?"Save your Crosshair changes?\n\nThe draft has not been applied. Saving keeps the mod's enabled state.":
            "Save your Crosshair changes?\n\nStyle: "+Host.ConfiguredCrosshair.style+" → "+draft.style+"\nSize: "+Host.ConfiguredCrosshair.size+" → "+draft.size+" HUD units\n\nThe draft has not been applied. Saving keeps the mod's enabled state.",()=>SaveDraft(true));
        confirmPanel.transform.Find("ConfirmAction").GetComponentInChildren<Text>().text="Save & leave";
        confirmPanel.transform.Find("CancelAction").GetComponentInChildren<Text>().text="Stay here";
        extraConfirm.gameObject.SetActive(true);extraConfirm.GetComponentInChildren<Text>().text="Discard & leave";extraConfirm.onClick.RemoveAllListeners();extraConfirm.onClick.AddListener(()=>{HideModal();FinishSettings();});
    }
    void ReviewRemoval()
    {
        Ask("Uninstall this mod?\n\nThe package will be removed. Its personal settings are retained. Any running version stays active until restart. Use Disable to keep the package.",()=>Run(()=>Service.Remove(selectedId)));
        confirmPanel.transform.Find("ConfirmAction").GetComponentInChildren<Text>().text="Uninstall";
        extraConfirm.gameObject.SetActive(true);extraConfirm.GetComponentInChildren<Text>().text="Disable instead";extraConfirm.onClick.RemoveAllListeners();extraConfirm.onClick.AddListener(()=>{HideModal();Run(()=>Service.Request(selectedId,false));});
    }
}
