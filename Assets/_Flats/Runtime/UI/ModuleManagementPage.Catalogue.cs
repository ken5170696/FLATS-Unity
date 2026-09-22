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
    GameObject importPanel;
    InputField importPath;
    Text importNotice;
    CatalogItem importCandidate;
    string reviewedPath;
    void AddCard(string id,string name,string subtitle,int index)
    {
        int columns=ListWidth>=1300?3:2;float gap=20,width=(ListWidth-16-gap*(columns-1))/columns,height=238;
        float x=-(ListWidth-16)/2+width/2+(index%columns)*(width+gap),y=-(index/columns)*(height+20);
        var card=ui.Button("Mod-"+id,listContent,"",x,y,width,height,()=>{selectedId=id;OpenDetail();},Color.white);
        var r=(RectTransform)card.transform;r.anchorMin=r.anchorMax=new Vector2(.5f,1);r.pivot=new Vector2(.5f,1);
        var cover=ui.Panel("Cover",r,0,-48,width,96,ModCenterWidgets.ControlColor);
        var icon=ui.Rect("ModIcon",cover.transform,0,0,50,50).gameObject.AddComponent<ModTileGraphic>();icon.color=ModCenterWidgets.Accent;icon.raycastTarget=false;
        var item=known[id];
        if(!string.IsNullOrEmpty(item.imageUrl)){var img=ui.Rect("Thumbnail",cover.transform,0,0,80,80).gameObject.AddComponent<RawImage>();img.raycastTarget=false;LoadArtwork(img,item.imageUrl,-1);}
        ui.Text("Name",r,PlayerName(name),0,-123,width-36,46,22);
        var desc=ui.Text("Purpose",r,item.manifest.description??"",0,-164,width-36,38,16,ModCenterWidgets.Muted);
        ui.Text("State",r,CatalogStatus(item),-width/4,-209,width/2-24,34,14,ModCenterWidgets.Muted);
        ui.Button("Review-"+id,r,Service.Installed.Any(p=>p.manifest.id==id)?"Manage":"Review",width/2-76,-209,128,42,()=>{selectedId=id;OpenDetail();},ModCenterWidgets.Accent);
        foreach(Transform t in r)((RectTransform)t).anchorMin=((RectTransform)t).anchorMax=new Vector2(.5f,1);
        card.gameObject.AddComponent<ModScrollFocus>().Scroll=listScroll;
        rowY=(index/columns+1)*(height+20);listContent.sizeDelta=new Vector2(0,Mathf.Max(listScroll.viewport.rect.height,rowY));
    }
    void ShowCatalogueLoading()
    {
        int columns=ListWidth>=1300?3:2;float gap=20,w=(ListWidth-16-gap*(columns-1))/columns;
        for(int i=0;i<columns*2;i++)
        {
            var card=ui.Panel("LoadingCard",listContent,-(ListWidth-16)/2+w/2+(i%columns)*(w+gap),-(i/columns)*258,w,238,Color.white).rectTransform;card.anchorMin=card.anchorMax=new Vector2(.5f,1);card.pivot=new Vector2(.5f,1);
            var cover=ui.Panel("PendingArtwork",card,0,-48,w,96,ModCenterWidgets.ControlColor).rectTransform;cover.anchorMin=cover.anchorMax=new Vector2(.5f,1);
            for(int j=0;j<3;j++){var line=ui.Panel("PendingText",card,-w*.1f,-126-j*30,w*(j==0?.65f:.75f),12,ModCenterWidgets.ControlColor).rectTransform;line.anchorMin=line.anchorMax=new Vector2(.5f,1);}
        }
        listContent.sizeDelta=new Vector2(0,516);
    }
    void RefreshDownloadRows()
    {
        foreach(var job in Service.Downloads?.Snapshot()??new DownloadSnapshot[0])
        {
            var row=listContent.Find("Mod-"+job.Id);if(row==null)continue;
            row.Find("State").GetComponent<Text>().text=job.State+"\n"+(job.Total>0&&job.State==DownloadState.Downloading?Mathf.RoundToInt((float)job.Received/job.Total*100)+"% · "+job.Received/1024+" / "+job.Total/1024+" KB":"");
            float rowW=((RectTransform)row).rect.width;
            var progress=row.Find("Progress");
            if(progress==null){var track=ui.Panel("Progress",row,0,-94,rowW-180,4,ModCenterWidgets.ControlColor);track.rectTransform.anchorMin=track.rectTransform.anchorMax=new Vector2(.5f,1);progress=track.transform;ui.Panel("Value",progress,0,0,0,4,ModCenterWidgets.Accent);}
            progress.gameObject.SetActive((job.State==DownloadState.Downloading||job.State==DownloadState.Paused)&&job.Total>0);
            var value=(RectTransform)progress.Find("Value");value.pivot=new Vector2(0,.5f);value.anchoredPosition=new Vector2(-(rowW-180)/2,0);value.sizeDelta=new Vector2((rowW-180)*Mathf.Clamp01((float)job.Received/Math.Max(1,job.Total)),4);
            if(row.Find("QueueAction")==null)
            {
                float w=((RectTransform)row).rect.width;
                var b=ui.Button("QueueAction",row,QueueLabel(job),w/2-80,-54,130,44,()=>QueueAction(job.Id));
                ((RectTransform)b.transform).anchorMin=((RectTransform)b.transform).anchorMax=new Vector2(.5f,1);
                var cancel=ui.Button("QueueCancel",row,"Cancel",w/2-225,-54,130,44,()=>Service.Downloads.Cancel(job.Id));
                ((RectTransform)cancel.transform).anchorMin=((RectTransform)cancel.transform).anchorMax=new Vector2(.5f,1);
            }
            else row.Find("QueueAction").GetComponentInChildren<Text>().text=QueueLabel(job);
            row.Find("QueueAction").GetComponent<Button>().interactable=job.State!=DownloadState.Verifying;
            row.Find("QueueCancel").gameObject.SetActive(job.Busy||job.State==DownloadState.Paused||job.State==DownloadState.Failed);
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
        if(importPanel==null)
        {
            importPanel=ui.Panel("ImportPackage",root,0,0,900,490,ModCenterWidgets.Paper).gameObject;
            ui.Text("Title",importPanel.transform,"Import a local mod",0,186,820,54,30);
            ui.Text("Help",importPanel.transform,"Choose a FLATS ZIP package or enter its full file path.\nOnly import packages from a source you recognise.",0,112,820,68,18,ModCenterWidgets.Muted);
            importPath=ui.Input("PackagePath",importPanel.transform,"C:\\Mods\\package.zip",0,36,820);
            importNotice=ui.Text("ImportNotice",importPanel.transform,"The package will be checked before installation.",0,-62,820,120,18);
            ui.Button("ReviewImport",importPanel.transform,"Review package",245,-196,230,46,ReviewImport,ModCenterWidgets.Accent);
            ui.Button("ChooseImport",importPanel.transform,"Choose file",-245,-196,210,46,()=>{var path=LocalModFilePicker.Choose();if(!string.IsNullOrEmpty(path))importPath.text=path;});
            ui.Button("CancelImport",importPanel.transform,"Cancel",-20,-196,180,46,HideModal);
        }
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
            var problem=ModRules.Compatibility(importCandidate.manifest);if(problem.Length>0)throw new InvalidDataException(problem);
            HideModal();
            Ask("Import "+importCandidate.manifest.name+" v"+importCandidate.manifest.version+"?\n\n"+(importCandidate.bytes/1024f).ToString("0.0")+" KB · Local package\n"+importCandidate.manifest.scope+"\n"+Service.Problem(importCandidate.manifest)+"\n\nInstallation does not enable a new mod. Restart is required to load it. Packages may run code.",()=>Run(async()=>
            {
                string stage=Service.Store.BeginStaging();
                try{using(Service.Store.Reserve(importCandidate.manifest.id))await Task.Run(()=>Service.Store.Install(reviewedPath,stage,importCandidate,"Local import",CancellationToken.None));await Service.RefreshInstalled();Switch("Installed");notice.text="Package imported. Review its status before enabling.";}
                finally{Service.Store.EndStaging(stage);}
            }));
        }
        catch(Exception e){importNotice.text="Could not review package. "+e.Message;}
        finally{busy=false;}
    }
}
