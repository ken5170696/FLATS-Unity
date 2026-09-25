using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Flats.Modules;

public sealed partial class ModuleManagementPage
{
    [SerializeField] GameObject webImportPanel;
    [SerializeField] InputField webImportJson;
    [SerializeField] Text webImportNotice;
    void ShowWebDataImport()
    {
        
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

