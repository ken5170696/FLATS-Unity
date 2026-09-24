using System;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Flats.Modules;

public sealed partial class ModuleManagementPage
{
    [SerializeField] GameObject importPanel;
    [SerializeField] InputField importPath;
    [SerializeField] Text importNotice;
    CatalogItem importCandidate;
    string reviewedPath;
    void AddCard(string id,string name,string subtitle,int index)
    {
        int columns=ListWidth<760?1:ListWidth>=1300?3:2;float gap=20,width=(ListWidth-16-gap*(columns-1))/columns,height=((RectTransform)catalogueCardPrefab.transform).rect.height;
        float x=-(ListWidth-16)/2+width/2+(index%columns)*(width+gap),y=-(index/columns)*(height+20);
        var view=Instantiate(catalogueCardPrefab,listContent,false);view.name="Mod-"+id;
        var r=(RectTransform)view.transform;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(width,height);
        Listen(view.select,()=>{selectedId=id;OpenDetail();});
        var item=known[id];
        view.thumbnail.gameObject.SetActive(!string.IsNullOrEmpty(item.imageUrl));if(view.thumbnail.gameObject.activeSelf)LoadArtwork(view.thumbnail,item.imageUrl,-1);
        view.title.text=PlayerName(name);view.purpose.text=item.manifest.description??"";view.state.text=CatalogStatus(item);
        view.review.GetComponentInChildren<Text>().text=Service.Installed.Any(p=>p.manifest.id==id)?"Manage":"Review";
        Listen(view.review,()=>{selectedId=id;OpenDetail();});
        rowY=(index/columns+1)*(height+20);listContent.sizeDelta=new Vector2(0,Mathf.Max(listScroll.viewport.rect.height,rowY));
    }
    void ShowCatalogueLoading()
    {
        int columns=ListWidth<760?1:ListWidth>=1300?3:2;float gap=20,w=(ListWidth-16-gap*(columns-1))/columns;
        for(int i=0;i<columns*2;i++)
        {
            var card=Instantiate(loadingCardPrefab,listContent,false);card.anchoredPosition=new Vector2(-(ListWidth-16)/2+w/2+(i%columns)*(w+gap),-(i/columns)*258);card.sizeDelta=new Vector2(w,card.sizeDelta.y);
        }
        listContent.sizeDelta=new Vector2(0,516);
    }
    void RefreshDownloadRows()
    {
        foreach(var job in Service.Downloads?.Snapshot()??new DownloadSnapshot[0])
        {
            var row=listContent.Find("Mod-"+job.Id);if(row==null)continue;
            var view=row.GetComponent<ModuleListRowView>();
            view.state.text=job.State+"\n"+(job.Total>0&&job.State==DownloadState.Downloading?Mathf.RoundToInt((float)job.Received/job.Total*100)+"% · "+job.Received/1024+" / "+job.Total/1024+" KB":"");
            view.progress.SetActive((job.State==DownloadState.Downloading||job.State==DownloadState.Paused)&&job.Total>0);
            float fraction=Mathf.Clamp01((float)job.Received/Math.Max(1,job.Total));
            view.progressValue.anchorMax=new Vector2(fraction,1);
            view.queueAction.GetComponentInChildren<Text>().text=QueueLabel(job);
            view.queueAction.interactable=job.State!=DownloadState.Verifying;
            view.queueCancel.gameObject.SetActive(job.Busy||job.State==DownloadState.Paused||job.State==DownloadState.Failed);
        }
    }
    static string QueueLabel(DownloadSnapshot job) { return job.State==DownloadState.Verifying?"Verifying":job.Busy?"Pause":job.State==DownloadState.Paused?"Continue":job.State==DownloadState.Installed?"Manage":"Retry"; }
    void QueueAction(string id)
    {
        var j=Service.Downloads.Snapshot().First(x=>x.Id==id);
        if(j.Busy)Service.Downloads.Pause(id);
        else if(j.State==DownloadState.Installed){Switch("Installed");selectedId=id;OpenDetail();}
        else Service.Downloads.Retry(id);
    }
    void ShowImport()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        ShowWebDataImport();
        return;
#else
        
        ShowModal(importPanel);Focus(importPath);
#endif
    }
    async void ReviewImport()
    {
        if(busy)return;busy=true;importNotice.text="Checking package...";
        try
        {
            reviewedPath=Path.GetFullPath(importPath.text.Trim().Trim('"'));
            importCandidate=await Task.Run(()=>
            {
                var file=new FileInfo(reviewedPath);if(!file.Exists||file.Length>ModRules.MaxArchive)throw new InvalidDataException("Package not found or exceeds 64 MB.");
                PackageManifest manifest;
                using(var zip=ZipFile.OpenRead(reviewedPath))
                {
                    var entry=zip.GetEntry("manifest.json");if(entry==null||entry.Length>96*1024)throw new InvalidDataException("Missing or oversized manifest.json.");
                    using(var reader=new StreamReader(entry.Open()))manifest=JsonUtility.FromJson<PackageManifest>(reader.ReadToEnd());
                }
                manifest.Validate();string hash;using(var stream=File.OpenRead(reviewedPath))using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
                return new CatalogItem{manifest=manifest,sha256=hash,bytes=file.Length};
            });
            if(this==null || !isActiveAndEnabled)return;
            var problem=ModRules.Compatibility(importCandidate.manifest);if(problem.Length>0)throw new InvalidDataException(problem);
            HideModal();
            Ask("Import "+importCandidate.manifest.name+" v"+importCandidate.manifest.version+"?\n\n"+(importCandidate.bytes/1024f).ToString("0.0")+" KB · Local package\n"+importCandidate.manifest.scope+"\n"+Service.Problem(importCandidate.manifest)+"\n\nInstallation does not enable a new mod. Restart is required to load it. Packages may run code.",()=>Run(async()=>
            {
                string stage=Service.Store.BeginStaging();
                try{using(Service.Store.Reserve(importCandidate.manifest.id))await Task.Run(()=>Service.Store.Install(reviewedPath,stage,importCandidate,"Local import",CancellationToken.None));await Service.RefreshInstalled();if(this==null || !isActiveAndEnabled)return;Switch("Installed");notice.text="Package imported. Review its status before enabling.";}
                finally{Service.Store.EndStaging(stage);}
            }));
        }
        catch(Exception e){if(this!=null && isActiveAndEnabled)importNotice.text="Could not review package. "+e.Message;}
        finally{busy=false;}
    }
}
