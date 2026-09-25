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
    bool Generic=>settingsForm!=null;
    static string Signature(CrosshairSettings s)=>string.Join(";",s.Values.Select(v=>v.id+"="+v.value));
    bool Dirty=>draft!=null&&Signature(draft)!=Signature(Host.ConfiguredCrosshair);
    
    
    void RefreshQuick()
    {
        var record=Host.Manager.Installed.FirstOrDefault(r=>r.Manifest.Id==selectedId);
        var package=Service.Installed.FirstOrDefault(p=>p.manifest.id==selectedId);
        bool configurable=selectedId==CrosshairModule.Id && package!=null;
        ((FlatsLocalizedText)quickTitle).translate=configurable || (package==null && record==null);
        quickTitle.text=configurable?"Custom Crosshair":package?.manifest.name??record?.Manifest.DisplayName??"Select a mod";
        quickInfo.text=configurable?"Client-only  /  "+(Host.Requested(CrosshairModule.Id)?"Enabled in selected profile":"Disabled in selected profile")+" / "+(record?.Active==true?"Active now":"Not active now")+"\n\nInstalled version: "+package.manifest.version+"\n\nAffects: Your screen only":
            package!=null?(package.manifest.scope=="ClientOnly"?"Your screen only":"Multiplayer mod")+"\n\nVersion "+package.manifest.version+"\n\n"+InstalledStatus(package):"Select a row to view its status and actions.";
        quickConfigure.gameObject.SetActive(configurable);quickDetails.interactable=!string.IsNullOrEmpty(selectedId);
        quickPreview.gameObject.SetActive(configurable);if(configurable)quickPreview.Set(Host.ConfiguredCrosshair.style,54);
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
    
    
    void OpenSettings()
    {
        // Start from every saved value so editing style or size keeps colour, thickness and outline.
        draft=Host.ConfiguredCrosshair.Clone();
        SaveView();settingsOpen=true;detailOpen=false;draftNotice.text="";ApplyView();
        if(Generic)
        {
            // Settings are in HUD units; the preview shows them three times larger, as before.
            preview.rectTransform.localScale=Vector3.one*3;
            settingsForm.Bind(CrosshairSettingsSpec.Specs(),CrosshairSettingsSpec.Presets(),draft.Values,values=>{draft.Values=values;RefreshDraft();});
            RefreshDraft();var first=settingsForm.GetComponentInChildren<Selectable>();if(first!=null)Focus(first);
        }
        else {RefreshDraft();Focus(styles[(int)draft.style]);}
    }
    void RefreshDraft()
    {
        if(Generic)preview.Set(draft);
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
        if(!Host.Configure(draft.Values)){draftNotice.text=Host.Notice;return false;}
        if(leave)FinishSettings();else RefreshDraft();return true;
    }
    void FinishSettings() { settingsOpen=false;detailOpen=false;Reload();FocusSelected(); }
    void LeaveSettings()
    {
        if(!Dirty){FinishSettings();return;}
        Ask(Generic?"Save your Crosshair changes?\n\nThe draft has not been applied. Saving keeps the mod's enabled state.":
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
