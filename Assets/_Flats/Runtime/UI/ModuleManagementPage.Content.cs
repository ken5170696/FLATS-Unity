using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Flats.Modules;

public sealed partial class ModuleManagementPage
{
    async void Browse()
    {
        sourceFailed=false;ApplyView();var generation=viewGeneration;var source=Service.Source;
        if(source==null){ClearRows();
#if UNITY_WEBGL && !UNITY_EDITOR
            Empty("Official service unavailable\nInstalled data modules remain available. Please try again later.");
            notice.text=Service.Notice;
#else
            Empty("Official service unavailable\nYour installed mods still work. Please try again later.");notice.text="Official mod service is unavailable in this build.";
#endif
            restoreScroll=false;ShowDetail();return;}
        var cancel=browsing.Token;
        var query=new CatalogQuery{Search=search.text,Category=categoryValue,Sort=sortValue,Compatible=compatible,Offset=offset};
        string key=source.Identity+"|"+query.Search+"|"+query.Category+"|"+query.Sort+"|"+query.Compatible+"|"+query.Offset;
        // Show the last result for this query at once and refresh it in the background.
        bool cached=catalogueCache.TryGetValue(key,out var previous);
        if(cached)RenderCatalogue(previous,source,true);
        else
        {
            ClearRows();ShowCatalogueLoading();notice.text="Loading mods — connecting to "+new Uri(source.Identity).Host;
            detailTitle.text="Loading...";SetDescription("Retrieving catalogue details.");SetPrimary("Loading...",false);secondary.gameObject.SetActive(false);remove.gameObject.SetActive(false);crosshairPanel.SetActive(false);
        }
        var timer=System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var page=await source.Browse(query,cancel);
            Debug.Log("MOD_CATALOGUE_BROWSE ms="+timer.ElapsedMilliseconds+" items="+page.items.Length+" cached="+cached);
            if(this==null || cancel.IsCancellationRequested || generation!=viewGeneration || !isActiveAndEnabled)return;
            catalogueCache[key]=page;
            RenderCatalogue(page,source,false);
        }
        catch(OperationCanceledException) { }
        catch(Exception)
        {
            Debug.Log("MOD_CATALOGUE_BROWSE failed ms="+timer.ElapsedMilliseconds+" cached="+cached);
            if(this==null || !isActiveAndEnabled || generation!=viewGeneration)return;
            // A cached page stays usable when the refresh fails.
            if(cached){notice.text="Showing saved results. Check your connection, then retry.";return;}
            sourceFailed=true;ClearRows();Empty("Official service offline\nCheck your connection and retry. Your installed mods are still available.");
            detailTitle.text="Official service offline";SetDescription("Your installed mods remain available. Retry when your connection is available.");SetPrimary("Unavailable",false);

            notice.text="Check your connection, then retry.";Page(0);restoreScroll=false;
        }
    }
    // Catalogue pages from this session, keyed by source and query.
    static readonly Dictionary<string,CatalogPage> catalogueCache=new Dictionary<string,CatalogPage>();
    void RenderCatalogue(CatalogPage page,IModSource source,bool stale)
    {
        {
            // Precision Dot and Wide Ring are now presets of Custom Crosshair; the live
            // catalogue keeps them for older builds, so this build hides them from Explore.
            int retired=page.items.Count(i=>CrosshairSettingsSpec.PresetForRetiredPackage(i.manifest.id)!=null);
            catalog=page.items.Where(i=>CrosshairSettingsSpec.PresetForRetiredPackage(i.manifest.id)==null).ToArray();categories=page.categories;RefreshFilters();foreach(var item in catalog)known[item.manifest.id]=item;
            ClearRows();int i=0;
            foreach(var item in catalog)AddRow(item.manifest.id,item.manifest.name,(item.manifest.description ?? "No description provided.")+"\n"+CatalogStatus(item),i++);
            if(i==0)Empty(search.text.Length>0?"No results\nTry another search or filter.":"No mods in this category.");
            Page(page.total);restoreScroll=false;notice.text="Source: "+new Uri(source.Identity).Host+" / "+(page.total-retired)+" results"+(stale?" (updating)":"");
            if(!catalog.Any(c=>c.manifest.id==selectedId))selectedId="";
            ShowDetail();
        }
    }
    string CatalogStatus(CatalogItem item)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { WebModSource.ValidatePackage(item.manifest); }
        catch(Exception) { return "Requires desktop FLATS"; }
#endif
        string problem=ModRules.Compatibility(item.manifest);if(problem.Length>0)return "Incompatible: "+problem;
        var local=Service.Installed.FirstOrDefault(p=>p.manifest.id==item.manifest.id);
        if(Service.Downloads?.IsBusy(item.manifest.id)==true)return "Download in progress";
        if(local==null)return "v"+item.manifest.version;
        return ModRules.Version(item.manifest.version)>ModRules.Version(local.manifest.version)?"Update available":"Installed";

    }
    void RenderLocal()
    {
        if(!wired || !isActiveAndEnabled)return;
        if(tab=="Profiles"){RenderProfiles();return;}
        if(!restoreScroll)savedScroll=listScroll.verticalNormalizedPosition;
        ClearRows();var entries=new List<Tuple<string,string,string>>();
        if(tab=="Downloads")
        {
            foreach(var j in Service.Downloads?.Snapshot() ?? new DownloadSnapshot[0])entries.Add(Tuple.Create(j.Id,j.Name,j.State+" / v"+j.Version));
        }
        else
        {
            if(Service.Store!=null && Service.Store.Notices.Count>0)entries.Add(Tuple.Create("storage-report","Storage recovery report","Select to review local storage notices"));
            foreach(var p in Service.Installed)entries.Add(Tuple.Create(p.manifest.id,p.manifest.name,(p.manifest.description ?? "No description provided.")+"\n"+InstalledStatus(p)));
            foreach(var p in Service.Running.Where(p=>!Service.Installed.Any(i=>i.manifest.id==p.manifest.id)))entries.Add(Tuple.Create(p.manifest.id,p.manifest.name,"Removed / still loaded until restart"));
        }
        entries=entries.Where(e=>e.Item2.IndexOf(search.text,StringComparison.OrdinalIgnoreCase)>=0 || e.Item1.IndexOf(search.text,StringComparison.OrdinalIgnoreCase)>=0).Where(e=>tab=="Downloads" || MatchesFilter(e.Item1)).ToList();
        if(offset>=entries.Count)offset=Math.Max(0,((entries.Count-1)/20)*20);
        if(sortValue=="name")entries=entries.OrderBy(e=>e.Item2,StringComparer.OrdinalIgnoreCase).ToList();
        if(sortValue=="name-desc")entries=entries.OrderByDescending(e=>e.Item2,StringComparer.OrdinalIgnoreCase).ToList();
        int row=0;foreach(var e in entries.Skip(offset).Take(20))AddRow(e.Item1,e.Item2,e.Item3,row++);
        if(entries.Count==0)Empty(tab=="Installed"&&Service.Installed.Length==0&&search.text.Length==0&&localFilter=="All"?"No mods installed\nOpen Explore to download your first mod.":tab=="Downloads"?(search.text.Length>0?"No matching downloads\nClear your search to see the queue.":"No downloads\nYour queue will appear here."):"No matching mods\nTry another search or filter.");
        if(!entries.Any(e=>e.Item1==selectedId))selectedId=entries.FirstOrDefault()?.Item1??"";
        Page(entries.Count);restoreScroll=false;RefreshQuick();ShowDetail();
    }
    string InstalledStatus(InstalledPackage package)
    {
        string id=package.manifest.id;
        var record=Host.Manager.Installed.FirstOrDefault(r=>r.Manifest.Id==id);
        string state=Service.NeedsRestart(id)?(package.requested?"Restart required":record?.Active==true?"Disable on restart":"Disabled"):(record?.Active==true?"Active now":package.requested?"Enabled / not active":"Disabled");
        if(Service.Problem(package.manifest).Length>0||!string.IsNullOrEmpty(record?.Reason))state="Needs attention / "+FlatsLocalization.Translate(state);
        if(known.TryGetValue(id,out var item)&&ModRules.Version(item.manifest.version)>ModRules.Version(package.manifest.version))state="Update available / "+state;
        return state;
    }
    void RefreshCatalogState()
    {
        foreach(var item in catalog)
        {
            var row=listContent.Find("Mod-"+item.manifest.id);
            if(row!=null){var state=row.Find("State");if(state!=null)state.GetComponent<Text>().text=CatalogStatus(item);}
        }
    }
    bool MatchesFilter(string id)
    {
        if(id=="storage-report")return localFilter=="All"||localFilter=="Problems";
        var p=Service.Installed.FirstOrDefault(x=>x.manifest.id==id);var r=Host.Manager.Installed.FirstOrDefault(x=>x.Manifest.Id==id);
        bool requested=p?.requested ?? Host.Requested(id);
        return localFilter=="All" || (localFilter=="Enabled" && requested) || (localFilter=="Disabled" && !requested) ||
            (localFilter=="Problems" && (!string.IsNullOrEmpty(r?.Reason) || (p!=null && Service.Problem(p.manifest).Length>0))) || (localFilter=="Updates" && p!=null && known.TryGetValue(id,out var c) && ModRules.Version(c.manifest.version)>ModRules.Version(p.manifest.version));
    }
    void ClearRows()
    {
        var focused=UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        pendingFocusName=focused!=null&&focused.transform.IsChildOf(listContent)?focused.name:null;
        foreach(Transform child in listContent){foreach(var img in child.GetComponentsInChildren<RawImage>()){if(img.texture is Texture2D t){artworkTextures.Remove(t);Destroy(t);}}child.gameObject.SetActive(false);Destroy(child.gameObject);}
        rowY=0;listContent.sizeDelta=new Vector2(0,288);
    }
    void Empty(string message)
    {
        float h=Mathf.Max(180,listScroll.viewport.rect.height);bool compact=h<280;
        var view=Instantiate(emptyStatePrefab,listContent,false);view.name="EmptyPanel";
        var panel=(RectTransform)view.transform;panel.anchoredPosition=new Vector2(0,-h/2);panel.sizeDelta=new Vector2(ListWidth-16,h);
        view.icon.SetActive(!compact);
        var parts=message.Split(new[]{'\n'},2);
        view.title.text=parts[0];view.message.text=parts.Length>1?parts[1]:"";
        if(message.StartsWith("Loading")){view.primary.gameObject.SetActive(false);view.secondary.gameObject.SetActive(false);Page(0);return;}
        bool query=search.text.Length>0||localFilter!="All"||categoryValue.Length>0;
        string label;Action action;
        if(tab=="Explore"&&Service.Source==null){label="Try again";action=Reload;}
        else if(query){label="Clear filters";action=()=>{localFilter="All";categoryValue="";compatible=true;offset=0;Reload();};}
        else if(tab=="Explore"){label="Try again";action=Reload;}
        else {label="Explore mods";action=()=>Switch("Explore");}
        view.primary.GetComponentInChildren<Text>().text=label;Listen(view.primary,action);
        view.secondary.GetComponentInChildren<Text>().text=query?"Clear search":"Go to installed";
        view.secondary.gameObject.SetActive(query||tab!="Installed");
        Listen(view.secondary,()=>{if(query){search.text="";Reload();}else Switch("Installed");});
        Page(0);
    }
    static string PlayerName(string value) { return string.Join(" ",(value ?? "Unnamed mod").Split(new[]{' ' ,'\n','\r','\t'},StringSplitOptions.RemoveEmptyEntries)); }
    void AddRow(string id,string name,string subtitle,int index)
    {
        if(tab=="Explore"){AddCard(id,name,subtitle,index);return;}
        bool downloading=tab=="Downloads";
        var view=Instantiate(downloading?downloadRowPrefab:installedRowPrefab,listContent,false);
        view.name="Mod-"+id;view.scroll=listScroll;
        var rect=(RectTransform)view.transform;
        rect.sizeDelta=new Vector2(ListWidth-16,rect.sizeDelta.y);
        rect.anchoredPosition=new Vector2(0,-rowY);
        view.select.image.color=id==selectedId?ModCenterWidgets.Tint:ModCenterWidgets.Paper;
        Listen(view.select,()=>SelectRow(id));
        view.title.translate=id==CrosshairModule.Id;view.SetTitle(PlayerName(name));
        var record=Host.Manager.Installed.FirstOrDefault(x=>x.Manifest.Id==id);
        var package=Service.Installed.FirstOrDefault(x=>x.manifest.id==id);
        view.scope.text=package==null?"":package.manifest.scope=="ClientOnly"?"Your screen only":"Multiplayer mod";
        view.version.text=package?.manifest.version??record?.Manifest.Version.ToString()??"";
        view.state.text=subtitle.Split('\n').Last();
        bool installedRow=!downloading&&(package!=null||record!=null);
        view.toggle.gameObject.SetActive(installedRow);view.details.gameObject.SetActive(installedRow);
        if(installedRow)
        {
            view.toggle.name="Toggle-"+id;view.details.name="Details-"+id;
            view.SetRequested(package?.requested??Host.Requested(id));
            Listen(view.toggle,()=>ToggleRow(id));
            Listen(view.details,()=>{selectedId=id;OpenDetail();});
        }
        if(downloading)
        {
            Listen(view.queueAction,()=>QueueAction(id));
            Listen(view.queueCancel,()=>Service.Downloads.Cancel(id));
        }
        rowY+=rect.rect.height;
        listContent.sizeDelta=new Vector2(0,Mathf.Max(listScroll.viewport.rect.height,rowY+listBottomPadding));
        if(view.name==pendingFocusName&&!detailOpen)Focus(view.select);
    }
    void Page(int total)
    {
        previous.interactable=offset>0;next.interactable=offset+20<total;
        pageLabel.text=total==0?"":(offset+1)+" - "+Math.Min(offset+20,total)+" of "+total+(tab=="Downloads"?" downloads":" results");
        Canvas.ForceUpdateCanvases();listScroll.verticalNormalizedPosition=savedScroll;ApplyView();
    }
    void ShowDetail()
    {
        if(!wired || currentModal!=null)return;
        ApplyView();if(!detailOpen)return;
        overviewTab.gameObject.SetActive(false);versionsTab.gameObject.SetActive(false);dependenciesTab.gameObject.SetActive(false);atGlance.gameObject.SetActive(false);glanceScroll.gameObject.SetActive(false);
        artworkGeneration++;detailArtwork.gameObject.SetActive(false);
        ((FlatsLocalizedText)detailTitle).translate=selectedId==CrosshairModule.Id || selectedId=="storage-report";
        ((FlatsLocalizedText)description).translate=selectedId==CrosshairModule.Id || selectedId=="storage-report" || tab=="Downloads";
        crosshairPanel.SetActive(settingsOpen);secondary.gameObject.SetActive(false);remove.gameObject.SetActive(false);enable.interactable=false;
        foreach(Transform row in listContent){var b=row.GetComponent<Button>();if(b!=null)b.image.color=row.name=="Mod-"+selectedId?ModCenterWidgets.Accent:ModCenterWidgets.PanelColor;}
        if(tab=="Downloads"){UpdateDownloadDetail();return;}
        var record=Host.Manager.Installed.FirstOrDefault(r=>r.Manifest.Id==selectedId);
        if(selectedId=="storage-report"){detailTitle.text="Storage recovery report";SetDescription(string.Join("\n\n",Service.Store.Notices.Distinct()));SetPrimary("Read only",false);return;}
        if(tab!="Explore" && selectedId==CrosshairModule.Id && Service.Installed.Any(x=>x.manifest.id==selectedId))
        {
            detailTitle.text="Custom Crosshair";
            SetDescription("Choose the shape and size of your aiming reticle.\n\nClient-only · Your screen only\n"+InstalledStatus(Service.Installed.First(x=>x.manifest.id==selectedId))+"\n\nConfigure a draft and preview changes before saving.\n"+(record?.Reason??""));
            secondary.gameObject.SetActive(true);secondary.GetComponentInChildren<Text>().text="Configure";secondary.interactable=true;
            SetPrimary(Host.Requested(CrosshairModule.Id)?"Disable":"Enable",!Host.ReadOnly);remove.gameObject.SetActive(true);remove.interactable=!busy;return;
        }
        var p=Service.Installed.FirstOrDefault(x=>x.manifest.id==selectedId);
        var m=tab=="Explore"?catalog.FirstOrDefault(x=>x.manifest.id==selectedId)?.manifest:p?.manifest ?? Service.Running.FirstOrDefault(x=>x.manifest.id==selectedId)?.manifest;
        if(m==null){detailOpen=false;ApplyView();return;}
        detailTitle.text=PlayerName(m.name);
        string deps=string.Join("\n",(m.dependencies ?? new DependencySpec[0]).Select(d=>d.id+" >="+d.minimum+" <"+d.maximum));
        var running=Service.Running.FirstOrDefault(x=>x.manifest.id==selectedId);
        string state=p==null?"Not installed":((p.requested?"Enabled for next launch":"Disabled for next launch")+" / "+(record?.Active==true?"Active now":"Not active now"));
        string text=state+(Service.NeedsRestart(selectedId)?" — Restart FLATS to apply":"")+"\n"+PlayerProblem(m)+"\n"+(record?.Reason ?? "")+"\n"+(string.IsNullOrWhiteSpace(m.description)?"No description provided.":m.description)+"\n\nAbout this mod\n"+m.author+" / v"+m.version+" / "+m.category+
            "\nInstalled version: "+(p?.manifest.version ?? "none")+" / Running version: "+(record?.Active==true?running?.manifest.version:"none")+"\n\nTechnical details\nGame >="+m.gameMinimum+" <"+m.gameMaximum+"\nMod API >="+m.apiMinimum+" <"+m.apiMaximum+"\n"+m.scope+
            "\n\nDependencies\n"+(deps.Length>0?deps:"None")+"\n\nChanges\n"+(string.IsNullOrEmpty(m.changelog)?"No changelog provided.":m.changelog)+
            "\n\nSource: "+(p?.source ?? Service.SourceUrl)+"\n"+(m.kind=="managed"?"Managed code runs with FLATS privileges. Restart to apply changes.":ModRules.IsCrosshairProvider(m)&&m.scope=="ClientOnly"?"Crosshair module. Enabling and disabling apply immediately.":"Data module. Restart to apply changes.");
        SetDescription(text);
        bool downloading=Service.Downloads?.IsBusy(selectedId)==true;
        var item=known.TryGetValue(selectedId,out var found)?found:null;
        var imageUrl=item?.imageUrl ?? p?.imageUrl;
        if(!string.IsNullOrEmpty(imageUrl))
        {
            description.rectTransform.anchoredPosition=new Vector2(0,-92);detailContent.sizeDelta+=new Vector2(0,92);
            detailArtwork.rectTransform.anchoredPosition=new Vector2(-ContentWidth/2+84,-46);detailArtwork.gameObject.SetActive(true);LoadArtwork(detailArtwork,imageUrl,artworkGeneration);
        }
        bool incompatible=ModRules.Compatibility(m).Length>0;
        bool blocked=p!=null&&!p.requested&&Service.Problem(m).Length>0;
        SetPrimary(incompatible&&p==null?"Incompatible":blocked?"Resolve requirements":busy?"Saving...":downloading?"Downloading...":p==null?"Install":p.requested?"Disable":"Enable",!busy&&!downloading&&(p!=null || (item!=null && ModRules.Compatibility(m).Length==0)));
        if(p!=null)
        {
            secondary.gameObject.SetActive(true);bool update=!busy&&!downloading&&item!=null&&ModRules.Version(item.manifest.version)>ModRules.Version(p.manifest.version);
            secondary.GetComponentInChildren<Text>().text=update?"Update to "+item.manifest.version:"Check updates";
            secondary.interactable=!busy&&!downloading&&Service.Source!=null;
            remove.gameObject.SetActive(true);remove.interactable=!busy&&!downloading;
        }
        DetailSections(m,p);
#if UNITY_WEBGL && !UNITY_EDITOR
        try { WebModSource.ValidatePackage(m); }
        catch(Exception) { SetPrimary("Desktop required",false); }
#endif
    }
    void SetDescription(string text)
    {
        detailScroll.gameObject.SetActive(true);
        description.text=text;description.rectTransform.anchoredPosition=Vector2.zero;
        Canvas.ForceUpdateCanvases();
        float height=Mathf.Max(detailScroll.viewport.rect.height,description.preferredHeight+12);
        description.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
        detailContent.sizeDelta=new Vector2(0,height);
    }
    void SetPrimary(string label,bool allowed) { enable.GetComponentInChildren<Text>().text=label;enable.interactable=allowed; }
    string PlayerProblem(PackageManifest manifest)
    {
        string issue=Service.Problem(manifest);
        if(ModRules.Compatibility(manifest).Length>0)return issue+"\nFind a version compatible with FLATS "+ModRules.GameVersion+".";
        if(issue.StartsWith("Missing"))return issue+"\nFind and install the required mod from your catalogue.";
        if(issue.StartsWith("Conflict"))return issue+"\nDisable the conflicting mod in Installed, then try again.";
        return issue;
    }
    void UpdateDownloadDetail()
    {
        var job=Service.Downloads?.Snapshot().FirstOrDefault(j=>j.Id==selectedId);
        if(job==null){detailOpen=false;ApplyView();return;}
        detailTitle.text=PlayerName(job.Name);
        string text=job.State==DownloadState.Downloading?(job.Total>0?Mathf.RoundToInt((float)job.Received/job.Total*100)+"% / "+(job.Received/1024)+" of "+(job.Total/1024)+" KB":"Downloading... Size is not available."):
            job.State==DownloadState.Installed?"Installed. Manage this mod in Installed. External changes apply after restarting FLATS.":
            job.State==DownloadState.Queued?"Waiting for another download to finish.":
            job.State==DownloadState.Verifying?"Checking and installing the download...":
            job.State==DownloadState.Paused?"Paused at "+job.Received/1024+" / "+job.Total/1024+" KB. Continue to resume. If the service cannot resume safely, the download starts again.":
            job.State==DownloadState.Cancelled?"Download cancelled. Your existing mods were kept.":
            "The download could not be installed. Try again or check with your catalogue provider. Your existing mods were kept.\n\nTechnical details\n"+job.Error;
        SetDescription(job.State+"\n\n"+text);
        SetPrimary(QueueLabel(job),job.State!=DownloadState.Installed&&job.State!=DownloadState.Verifying);
        notice.text=job.State==DownloadState.Failed?"Download failed. Your existing mods were kept.":job.State==DownloadState.Installed?"Download complete. Open Installed to manage the mod.":job.State==DownloadState.Cancelled?"Download cancelled.":"Downloads continue while you browse.";
    }
    void Primary()
    {
        if(tab=="Downloads") { QueueAction(selectedId);return; }
        var p=Service.Installed.FirstOrDefault(x=>x.manifest.id==selectedId);
        if(p==null){InstallSelected();return;}
        if(p.requested)Run(()=>Service.Request(p.manifest.id,false));else ReviewPlan(p.manifest,null,true);
    }
    void InstallSelected()
    {
        if(!known.TryGetValue(selectedId,out var item))return;
        ReviewPlan(item.manifest,item,false);
    }
    void ReviewPlan(PackageManifest manifest,CatalogItem item,bool enablePlan)
    {
        Run(async()=>
        {
            string state=Service.PlanState;
            notice.text="Checking dependency versions...";
            var plan=await Service.Plan(manifest,item,CancellationToken.None);
            if(this==null||!isActiveAndEnabled)return;
            // Installed crosshairs only change this client's HUD; the service starts them immediately.
            bool live=enablePlan && plan.Downloads.Length==0 && plan.Modules.All(m=>ModRules.IsCrosshairProvider(m) && m.scope=="ClientOnly");
            var changes=string.Join("\n",plan.Modules.Select(m=>m.id+"  "+m.version+(plan.Downloads.Any(d=>d.manifest.id==m.id)?" - download":" - installed")));
            Ask((enablePlan?"Enable ":"Install ")+PlayerName(manifest.name)+" and requirements?\n"+ScopeLabel(manifest)+
                (manifest.scope=="ClientOnly"?"":"\nEveryone in the room needs the same version.")+"\n"+changes+"\nTotal download: "+(plan.Bytes/1024f).ToString("0.0")+" KB\nConflicts: none in this plan.\n"+
                (plan.Downloads.Length>0?"Packages install together. New mods stay disabled; review Enable after downloading.":"The complete dependency set will be enabled in this profile.")+(live?"\nApplies immediately.":"\nRestart FLATS to apply."),()=>Run(()=>Service.ApplyPlan(plan,state,enablePlan)));
        },false);
    }

    void UpdateSelected()
    {
        if(selectedId==CrosshairModule.Id && tab!="Explore"){OpenSettings();return;}
        var p=Service.Installed.FirstOrDefault(x=>x.manifest.id==selectedId);
        if(p!=null && known.TryGetValue(selectedId,out var item) && ModRules.Version(item.manifest.version)>ModRules.Version(p.manifest.version)){InstallSelected();return;}
        Run(async()=>{foreach(var update in await Service.CheckUpdates(CancellationToken.None))known[update.manifest.id]=update;});
    }
    async void Run(Func<Task> action,bool show=true)
    {
        if(show){if(busy)return;busy=true;category.interactable=false;ShowDetail();}
        try{await action();if(this!=null && isActiveAndEnabled){if(tab!="Explore")RenderLocal();else ShowDetail();if(show)notice.text=Service.Notice;}}
        catch(OperationCanceledException) { }
        catch(Exception e){if(this!=null && isActiveAndEnabled){notice.text="Could not complete the action. "+e.Message;}Debug.LogWarning("MOD_CENTER_ACTION "+e.GetType().Name);}
        finally{if(show){busy=false;if(this!=null && isActiveAndEnabled){category.interactable=tab=="Explore" || tab=="Installed"&&Service.Source!=null;ShowDetail();}}}
    }
    async void LoadArtwork(RawImage image,string url,int generation)
    {
        try
        {
            var bytes=await Service.Artwork(url,browsing?.Token ?? CancellationToken.None);
            if(this==null || image==null || bytes==null || (generation>=0 && generation!=artworkGeneration))return;
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!texture.LoadImage(bytes,true)){Destroy(texture);return;}
            if(image.texture is Texture2D old){artworkTextures.Remove(old);Destroy(old);}
            image.texture=texture;image.color=Color.white;artworkTextures.Add(texture);
            float box=generation<0?64:180;float ratio=(float)texture.width/texture.height;
            image.rectTransform.sizeDelta=ratio>=1?new Vector2(box,box/ratio):new Vector2(box*ratio,box);
            var fallback=image.transform.parent.Find("ModIcon");if(fallback!=null)fallback.gameObject.SetActive(false);
        }
        catch(Exception) { /* Optional artwork degrades to the readable text row. */ }
    }
}
