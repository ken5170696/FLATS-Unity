#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.IO;
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
        if (webImportPanel == null)
        {
            webImportPanel = ui.Panel("ImportCrosshairData",root,0,0,900,490,ModCenterWidgets.Paper).gameObject;
            ui.Text("Title",webImportPanel.transform,"Import crosshair data",0,186,820,54,30);
            ui.Text("Help",webImportPanel.transform,"Paste a crosshair package's manifest.json. This copies its shape and size\ninto Custom Crosshair. DLLs and package dependencies are not imported.",0,112,820,68,18,ModCenterWidgets.Muted);
            webImportJson = ui.Input("PresetJson",webImportPanel.transform,"Paste manifest.json",0,20,820);
            webImportJson.characterLimit = 96 * 1024;
            webImportJson.lineType = InputField.LineType.MultiLineNewline;
            webImportNotice = ui.Text("ImportNotice",webImportPanel.transform,"Your current preset stays unchanged until you review and save.",0,-70,820,100,18);
            ui.Button("ReviewData",webImportPanel.transform,"Review data",220,-196,250,46,ReviewWebData,ModCenterWidgets.Accent);
            ui.Button("CancelImport",webImportPanel.transform,"Cancel",-220,-196,250,46,HideModal);
        }
        ShowModal(webImportPanel); Focus(webImportJson);
    }
    void ReviewWebData()
    {
        try
        {
            if (System.Text.Encoding.UTF8.GetByteCount(webImportJson.text) > 96 * 1024)
                throw new InvalidDataException("Manifest exceeds 96 KB");
            var manifest = WebCrosshairPreset.Validate(JsonUtility.FromJson<PackageManifest>(webImportJson.text));
            HideModal();
            ReviewWebPreset(manifest);
        }
        catch(Exception e) { webImportNotice.text = "Could not import: "+e.Message; }
    }
    void ReviewWebPreset(PackageManifest candidate)
    {
        try
        {
            var manifest=WebCrosshairPreset.Validate(candidate);
            Ask("Copy "+manifest.name+" preset?\n\nShape: "+((Flats.UI.CrosshairStyle)manifest.crosshairStyle)+"\nSize: "+manifest.crosshairSize+"\n\nThis replaces Custom Crosshair settings in the selected profile. Enable Custom Crosshair separately to display it. The package itself is not installed.",()=>
            {
                var owner = BuiltinModules.Instance;
                owner.Configure((Flats.UI.CrosshairStyle)manifest.crosshairStyle,manifest.crosshairSize);
                notice.text = owner.Notice;
                Switch("Installed");
            });
        }
        catch(Exception e) { notice.text = "Could not apply preset: "+e.Message; }
    }
}
#endif
