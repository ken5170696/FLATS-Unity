using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Flats.Modules;
using Flats.UI;

public sealed partial class ModuleManagementPage : MonoBehaviour
{
    // Keep existing prefab GUID and composition references.
    public GameObject mainScreen, crosshairPanel, mainOptions;
    public Button entry, back, rowTemplate, enable, style, smaller, larger, reset;
    public Transform rows;
    public Text description, status, settingsLabel, notice;
    public CrosshairGraphic preview;
    ModCenterWidgets ui;
    Menu menu;
    RectTransform root, listContent, detailContent;
    ScrollRect listScroll,detailScroll;
    Text summary,pageLabel,detailTitle;
    InputField search;
    Button explore,installed,downloads,filter,category,sort,previous,next,secondary,remove;
    GameObject confirmPanel;
    Text confirmText;
    Action confirmation;
    bool wired,optionsWereActive,busy;
    string tab="Installed",selectedId="",localFilter="All",categoryValue="",sortValue="name";
    int offset,revision=-1,viewGeneration;
    float refreshAt,debounce=-1;
    bool compatible=true;
    CatalogItem[] catalog=new CatalogItem[0];
    readonly Dictionary<string,CatalogItem> known=new Dictionary<string,CatalogItem>();
    string[] categories=new string[0];
    CancellationTokenSource browsing;
    readonly List<Texture2D> artworkTextures=new List<Texture2D>();
    RawImage detailArtwork;
    int artworkGeneration;
    string pendingFocusName;
    GameObject currentModal;
    readonly List<Selectable> blockedControls=new List<Selectable>();
    GameObject startupPanel;
    Text startupMessage;
    Button retryInitialization;
    readonly List<Selectable> startupBlocked=new List<Selectable>();
    IModHost Host;
    IModCenter Service;

    public void Initialize(IModHost host, IModCenter service, Menu navigation)
    {
        Bind(host,service,navigation);
        Initialize();
    }
    public void Bind(IModHost host, IModCenter service, Menu navigation)
    {
        if(wired)
        {
            if(!ReferenceEquals(Host,host) || !ReferenceEquals(Service,service) || menu!=navigation)
                throw new InvalidOperationException("Module page is already bound to another session");
            return;
        }
        Host=host ?? throw new ArgumentNullException(nameof(host));
        Service=service ?? throw new ArgumentNullException(nameof(service));
        menu=navigation ?? throw new ArgumentNullException(nameof(navigation));
    }

    public void Initialize()
    {
        if(wired)return;
        if(Host==null || Service==null || menu==null)throw new InvalidOperationException("ModulePageBinding must bind the page before use");
        wired=true;
        var font=menu.buttons[0].transform.parent.GetComponentInChildren<Text>(true).font;

        foreach(Transform child in transform) { child.gameObject.SetActive(false);Destroy(child.gameObject); }
        var bg=GetComponent<Image>();if(bg!=null)bg.enabled=false;
        ui=new ModCenterWidgets(font,()=>menu.PlayMenuSound(menu.pressSE));
        root=ui.Panel("ModCenter",transform,0,0,960,600,ModCenterWidgets.Paper).rectTransform;
        back=ui.Button("ModulesBack",root,"",-436,253,56,56,Close,ModCenterWidgets.Accent);
        var nativeBack=menu.backButton.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.sprite!=null);
        if(nativeBack!=null){var arrow=ui.Panel("Arrow",back.transform,0,0,40,40,Color.white);arrow.sprite=nativeBack.sprite;arrow.preserveAspect=true;arrow.raycastTarget=false;}
        else ui.Text("Arrow",back.transform,menu.backButton.GetComponentInChildren<Text>(true).text,0,0,38,48,32).alignment=TextAnchor.MiddleCenter;
        ui.Text("Title",root,"MOD",-290,257,220,54,40,ModCenterWidgets.Accent);
        summary=ui.Text("Summary",root,"Make FLATS your own",-180,216,440,30,18,ModCenterWidgets.Muted);
        explore=ui.Button("Explore",root,"Explore",-296,164,280,48,()=>Switch("Explore"));
        installed=ui.Button("Installed",root,"Installed",0,164,280,48,()=>Switch("Installed"));
        downloads=ui.Button("Downloads",root,"Downloads",296,164,280,48,()=>Switch("Downloads"));
        search=ui.Input("Search",root,"Search installed mods...",-275,100,322);search.characterLimit=120;
        search.onValueChanged.AddListener(_=>debounce=Time.unscaledTime+.35f);
        category=ui.Button("Category",root,"All categories",-15,100,180,38,CycleCategory);
        filter=ui.Button("Filter",root,"Compatible",183,100,190,38,()=>{if(tab=="Explore")compatible=!compatible;else CycleFilter();offset=0;Reload();});
        sort=ui.Button("Sort",root,"Name A-Z",365,100,142,38,()=>{sortValue=sortValue=="name"?(tab=="Installed"?"name-desc":"updated"):"name";offset=0;Reload();});
        listScroll=ui.Scroll("ModList",root,0,-67,872,288,out listContent);rows=listContent;
        detailPanel=ui.Panel("DetailPage",root,0,-47,872,410,ModCenterWidgets.PanelColor).gameObject;
        detailTitle=ui.Text("DetailTitle",detailPanel.transform,"",0,147,816,84,30);
        detailScroll=ui.Scroll("Details",detailPanel.transform,0,-10,816,202,out detailContent);
        description=ui.Text("Description",detailContent,"",0,0,790,202,21);description.alignment=TextAnchor.UpperLeft;
        description.rectTransform.anchorMin=description.rectTransform.anchorMax=new Vector2(.5f,1);description.rectTransform.pivot=new Vector2(.5f,1);
        detailArtwork=ui.Rect("Artwork",detailContent,-330,-46,96,80).gameObject.AddComponent<RawImage>();detailArtwork.raycastTarget=false;
        detailArtwork.rectTransform.anchorMin=detailArtwork.rectTransform.anchorMax=new Vector2(.5f,1);detailArtwork.gameObject.SetActive(false);
        enable=ui.Button("Primary",detailPanel.transform,"Enable",-275,-166,260,46,Primary,ModCenterWidgets.Accent);
        secondary=ui.Button("Secondary",detailPanel.transform,"Check updates",5,-166,240,46,UpdateSelected);
        remove=ui.Button("Remove",detailPanel.transform,"Remove",309,-166,170,46,ReviewRemoval);
        previous=ui.Button("Previous",root,"<",-410,-235,50,34,()=>{offset=Math.Max(0,offset-20);savedScroll=1;restoreScroll=true;Reload();});
        pageLabel=ui.Text("Page",root,"",0,-235,620,34,18,ModCenterWidgets.Muted);pageLabel.alignment=TextAnchor.MiddleCenter;
        next=ui.Button("Next",root,">",410,-235,50,34,()=>{offset+=20;savedScroll=1;restoreScroll=true;Reload();});
        notice=ui.Text("Notice",root,"",0,-280,872,42,17,ModCenterWidgets.Muted);
        BuildCrosshair();BuildConfirm();BuildApproved();entry.gameObject.SetActive(false);
        detailPanel.SetActive(false);
        startupPanel=ui.Panel("ModuleStartup",root,0,-30,860,390,ModCenterWidgets.Paper).gameObject;
        ui.Text("Title",startupPanel.transform,"Local module storage",0,150,780,44,28);
        startupMessage=ui.Text("Message",startupPanel.transform,"",0,15,780,206,19,ModCenterWidgets.Muted);
        retryInitialization=ui.Button("RetryInitialization",startupPanel.transform,"Try again",0,-145,270,46,RetryLocalModules,ModCenterWidgets.Accent);
        startupPanel.SetActive(false);
    }
    bool ShowStartupState()
    {
        if(Service.Ready)
        {
            if(startupPanel.activeSelf)
            {
                startupPanel.SetActive(false);
                foreach(var control in startupBlocked)if(control!=null)control.interactable=true;
                startupBlocked.Clear();
            }
            return false;
        }
        startupPanel.SetActive(true);startupPanel.transform.SetAsLastSibling();
        foreach(var control in root.GetComponentsInChildren<Selectable>(true))
            if(control!=back && !control.transform.IsChildOf(startupPanel.transform) && control.interactable)
            {startupBlocked.Add(control);control.interactable=false;}
        startupMessage.text=Service.InitializationComplete?Service.Notice+"\nYou can return to the game. Restore storage access or a compatible backup, then retry or restart FLATS.":"Loading local modules and saved profiles...";
        retryInitialization.gameObject.SetActive(Service.InitializationComplete);
        retryInitialization.interactable=Service.CanRetryInitialization;
        retryInitialization.GetComponentInChildren<Text>().text=Service.CanRetryInitialization?"Try again":"Restart after recovery";
        notice.text=Service.InitializationComplete?"Module changes are blocked until local storage recovers.":"Loading local modules...";
        return true;
    }
    async void RetryLocalModules()
    {
        if(!Service.CanRetryInitialization)return;
        var pending=Service.RetryInitialization();ShowStartupState();
        try{await pending;}
        catch(Exception e){if(this!=null)Debug.LogWarning("MOD_CENTER_RETRY "+e.GetType().Name);}
        if(this==null)return;
        readyRendered=false;ShowStartupState();if(isActiveAndEnabled)Reload();
    }
    void BuildCrosshair() { BuildDraftSettings(); }
    void BuildConfirm()
    {
        modalShield=ui.Panel("ModalShield",root,0,0,960,600,new Color(.18f,.12f,.16f,.38f)).rectTransform;modalShield.gameObject.SetActive(false);
        confirmPanel=ui.Panel("ModConfirmation",root,0,0,800,340,ModCenterWidgets.PanelColor).gameObject;
        confirmText=ui.Text("Message",confirmPanel.transform,"",0,48,760,175,21);
        ui.Button("ConfirmAction",confirmPanel.transform,"Continue",-150,-105,250,44,()=>{HideModal();var action=confirmation;confirmation=null;action?.Invoke();});
        ui.Button("CancelAction",confirmPanel.transform,"Cancel",150,-105,250,44,()=>{if(!busy)HideModal();});confirmPanel.SetActive(false);
    }
    GameObject modalReturnFocus;
    void ShowModal(GameObject modal)
    {
        modalReturnFocus=EventSystem.current?.currentSelectedGameObject;
        modalShield.gameObject.SetActive(true);modalShield.SetAsLastSibling();currentModal=modal;modal.SetActive(true);modal.transform.SetAsLastSibling();blockedControls.Clear();
        foreach(var c in root.GetComponentsInChildren<Selectable>())if(!c.transform.IsChildOf(modal.transform)&&c.interactable){blockedControls.Add(c);c.interactable=false;}
    }
    void HideModal() { modalShield.gameObject.SetActive(false); if(currentModal!=null)currentModal.SetActive(false);currentModal=null;foreach(var c in blockedControls)if(c!=null)c.interactable=true;blockedControls.Clear();if(modalReturnFocus!=null&&modalReturnFocus.activeInHierarchy)EventSystem.current?.SetSelectedGameObject(modalReturnFocus); }
    void Ask(string text,Action action) { extraConfirm.gameObject.SetActive(false);confirmPanel.transform.Find("ConfirmAction").GetComponentInChildren<Text>().text="Continue";confirmPanel.transform.Find("CancelAction").GetComponentInChildren<Text>().text="Cancel";confirmation=action;var lines=text.Split(new[]{'\n'},2);modalTitle.text=lines[0];confirmText.text=lines.Length>1?lines[1].Trim():"";LayoutConfirmation();modalScroll.verticalNormalizedPosition=1;ShowModal(confirmPanel);Focus(confirmPanel.GetComponentsInChildren<Button>()[1]); }
    static void Focus(Selectable control) { if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(control.gameObject); }
    void Configure(CrosshairStyle value,float size) { draft.style=value;draft.size=Mathf.Clamp(size,6,64);RefreshDraft(); }
    public void Open()
    {
        if(Menu.current!="Main" || Menu.gameState!="Main")return;
        Initialize();optionsWereActive=mainOptions.activeSelf;mainOptions.SetActive(false);mainScreen.SetActive(false);
        Menu.current="Modules";gameObject.SetActive(true);

        tab="Installed";detailOpen=false;settingsOpen=false;
        notice.text=Service.Installed.Length==0?"No mods installed. Open Explore to download your first mod.":"External mod changes apply after restarting FLATS.";
        if(!readyRendered){savedScroll=1;restoreScroll=true;}Resize();Reload();Focus(Service.Ready?(tab=="Installed"?installed:tab=="Explore"?explore:downloads):back);
    }
    public void Close()
    {
        if(currentModal!=null){if(!busy)HideModal();return;}
        if(settingsOpen){LeaveSettings();return;}

        if(detailOpen){detailOpen=false;Reload();FocusSelected();return;}
        if(tab=="Profiles"){Switch("Installed");return;}
        SaveView();
        browsing?.Cancel();gameObject.SetActive(false);mainScreen.SetActive(true);mainOptions.SetActive(optionsWereActive);Menu.current="Main";
        menu.PlayMenuSound(menu.cancelSE);Focus(menu.buttons[1].transform.parent.GetComponent<Button>());
    }
    void Resize()
    {
        var parent=transform.parent as RectTransform;
        if(parent==null)return;
        float width=Screen.width-(Screen.width<=1280?60:88),height=Screen.height-(Screen.height<=720?48:68);
        // Keep the controls' minimum layout area on short windows, then fit the
        // complete page uniformly. Pixel-height layout made the slider and its
        // bottom-anchored explanation occupy the same space at 576p.
        float scale=Mathf.Min(1f,Mathf.Min(width/960f,height/680f));
        transform.localScale=Vector3.one*(parent.rect.height/Screen.height)*scale;
        width/=scale;height/=scale;
        if(Mathf.Abs(width-layoutWidth)<1&&Mathf.Abs(height-layoutHeight)<1)return;
        layoutWidth=width;layoutHeight=height;Layout();
        if(Service.Ready)Reload();
    }
    void Update()
    {
        if(!wired)return;Resize();
        var device=InControl.InputManager.ActiveDevice;
        if(Input.GetKeyUp(KeyCode.Escape) || device.Action2.WasPressed || device.CommandWasPressed) { Close();return; }
        if(ShowStartupState())return;
        if(currentModal==null && !settingsOpen)
        {
            if(Input.GetKeyDown(KeyCode.PageDown))detailScroll.verticalNormalizedPosition-=.25f;
            if(Input.GetKeyDown(KeyCode.PageUp))detailScroll.verticalNormalizedPosition+=.25f;
        }
        if(debounce>=0 && Time.unscaledTime>=debounce && currentModal==null){debounce=-1;offset=0;savedScroll=1;restoreScroll=true;Reload();}
        if(Time.unscaledTime<refreshAt)return;refreshAt=Time.unscaledTime+.25f;
        if(!Service.Ready)return;
        if(!readyRendered){readyRendered=true;Reload();}
        var queue=Service.Downloads;
        if(queue!=null && revision!=queue.Revision && currentModal==null && !settingsOpen){revision=queue.Revision;Run(async()=>{await Service.RefreshInstalled();if(this==null || !isActiveAndEnabled)return;if(tab!="Explore")RenderLocal();else {RefreshCatalogState();ShowDetail();}},false);}
        var active=queue?.Snapshot().Count(j=>j.Busy) ?? 0;
        downloads.GetComponentInChildren<Text>().text="Downloads"+(active>0?" ("+active+")":"");
        summary.text=(Service.Installed.Length)+" installed  /  "+Host.Manager.Installed.Count(r=>r.Active)+" active";
        if(tab=="Downloads" && currentModal==null) { if(detailOpen)UpdateDownloadDetail();else RefreshDownloadRows(); }
    }
    void Switch(string value)
    {
        if(settingsOpen){LeaveSettings();return;}
        debounce=-1;
        notice.text=value=="Installed"?"Manage mods installed on this device.":value=="Downloads"?"Downloads continue while you browse.":"";
        if(tab==value){detailOpen=false;Reload();return;}
        SaveView();tab=value;detailOpen=false;restoreScroll=true;
        if(views.TryGetValue(value,out var saved)){offset=saved.offset;selectedId=saved.selected;search.SetTextWithoutNotify(saved.query);savedScroll=saved.scroll;}
        else {offset=0;selectedId="";search.SetTextWithoutNotify("");savedScroll=1;}
        Reload();Focus(value=="Explore"?explore:value=="Installed"?installed:downloads);
    }
    void CycleCategory()
    {
        if(tab=="Installed"){Run(async()=>{foreach(var item in await Service.CheckUpdates(CancellationToken.None))known[item.manifest.id]=item;});return;}
        if(tab!="Explore")return;filtersOpen=!filtersOpen;Reload();
    }
    void CycleFilter() { var options=new[]{"All","Enabled","Disabled","Problems","Updates"};localFilter=options[(Array.IndexOf(options,localFilter)+1)%options.Length]; }
    void Reload()
    {
        if(ShowStartupState())return;
        explore.image.color=tab=="Explore"?ModCenterWidgets.Accent:ModCenterWidgets.PanelColor;
        installed.image.color=tab=="Installed"?ModCenterWidgets.Accent:ModCenterWidgets.PanelColor;
        downloads.image.color=tab=="Downloads"?ModCenterWidgets.Accent:ModCenterWidgets.PanelColor;
        category.interactable=tab=="Explore" || (tab=="Installed" && Service.Source!=null && !busy);sort.interactable=tab!="Downloads";filter.interactable=tab!="Downloads";
        category.GetComponentInChildren<Text>().text=tab=="Installed"?"Check updates":categoryValue.Length==0?"All categories":categoryValue;
        filter.GetComponentInChildren<Text>().text=tab=="Explore"?(compatible?"Compatible only":"All versions"):localFilter;
        sort.GetComponentInChildren<Text>().text=sortValue=="name"?"Name A-Z":sortValue=="name-desc"?"Name Z-A":"Updated";
        if(!restoreScroll)savedScroll=listScroll.verticalNormalizedPosition;restoreScroll=true;
        ApplyView();
        browsing?.Cancel();browsing=new CancellationTokenSource();viewGeneration++;
        if(tab=="Explore")Browse();else RenderLocal();
    }
    public void Refresh() { Reload(); }
    void OnDestroy() { browsing?.Cancel();browsing?.Dispose();foreach(var t in artworkTextures)if(t!=null)Destroy(t); }
}
