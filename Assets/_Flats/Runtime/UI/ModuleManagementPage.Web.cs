#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Flats.Modules;

public sealed partial class ModuleManagementPage
{
    GameObject webImportPanel;
    InputField webImportJson;
    Text webImportNotice;
    void ShowWebDataImport()
    {
        if(webImportPanel==null)
        {
            webImportPanel=ui.Panel("FindWebPackage",root,0,0,900,490,ModCenterWidgets.Paper).gameObject;
            ui.Text("Title",webImportPanel.transform,"Find an official package",0,186,820,54,30);
            ui.Text("Help",webImportPanel.transform,"Paste a package manifest to find its official download.\nWeb installs crosshair data packages; code packages require desktop FLATS.",0,112,820,68,18,ModCenterWidgets.Muted);
            webImportJson=ui.Input("PresetJson",webImportPanel.transform,"Paste manifest.json",0,20,820);
            webImportJson.characterLimit=96*1024;webImportJson.lineType=InputField.LineType.MultiLineNewline;
            webImportNotice=ui.Text("ImportNotice",webImportPanel.transform,"The official package is downloaded and verified after your review.",0,-70,820,100,18);
            ui.Button("ReviewData",webImportPanel.transform,"Find package",220,-196,250,46,ReviewWebData,ModCenterWidgets.Accent);
            ui.Button("CancelImport",webImportPanel.transform,"Cancel",-220,-196,250,46,HideModal);
        }
        ShowModal(webImportPanel);Focus(webImportJson);
    }
    async void ReviewWebData()
    {
        try
        {
            var manifest=JsonUtility.FromJson<PackageManifest>(webImportJson.text);WebModSource.ValidatePackage(manifest);
            if(Service.Source==null)throw new Exception("Official service unavailable");
            var page=await Service.Source.Browse(new CatalogQuery{Id=manifest.id,AllVersions=true},CancellationToken.None);
            var item=page.items.FirstOrDefault(i=>i.manifest.version==manifest.version);
            if(item==null)throw new Exception("Version not found. Use Explore to find available versions.");
            if(webImportPanel==null || !webImportPanel.activeInHierarchy)return;
            HideModal();ReviewPlan(item.manifest,item,false);
        }
        catch(Exception e){webImportNotice.text="Could not find package: "+e.Message;}
    }
}
#endif
