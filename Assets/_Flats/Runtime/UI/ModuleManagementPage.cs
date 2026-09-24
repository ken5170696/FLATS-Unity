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
    [SerializeField] RectTransform root, listContent, detailContent;
    [SerializeField] ScrollRect listScroll,detailScroll;
    [SerializeField] Text summary,pageLabel,detailTitle;
    [SerializeField] InputField search;
    [SerializeField] Button explore,installed,downloads,filter,category,sort,previous,next,secondary,remove;
    [SerializeField] GameObject confirmPanel;
    [SerializeField] Text confirmText;
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
    [SerializeField] RawImage detailArtwork;
    int artworkGeneration;
    string pendingFocusName;
    GameObject currentModal;
    readonly List<Selectable> blockedControls=new List<Selectable>();
    [SerializeField] GameObject startupPanel;
    [SerializeField] Text startupMessage;
    [SerializeField] Button retryInitialization;
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
        if(Host==null || Service==null || menu==null)
            throw new InvalidOperationException("ModulePageBinding must bind the page before use");
        if(root==null || search==null || sizeSlider==null)
            throw new InvalidOperationException("ModulesScreen prefab has missing authored view references");
        ui=new ModCenterWidgets(FlatsLocalizedText.GetSourceFont(search.textComponent),()=>menu.PlayMenuSound(menu.pressSE));
        BindViewActions();
        BindFilters();
        wired=true;
        if(entry!=null)entry.gameObject.SetActive(false);
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
        startupPanel.SetActive(true);startupPanel.transform.parent.SetAsLastSibling();startupPanel.transform.SetAsLastSibling();
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
    
    
    GameObject modalReturnFocus;
    void ShowModal(GameObject modal)
    {
        modalReturnFocus=EventSystem.current?.currentSelectedGameObject;
        modalShield.transform.parent.SetAsLastSibling();
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
        if(parent==null || parent.rect.height<=0)return;
        float designHeight=Mathf.Max(1,referenceHeight);
        float width=designHeight*parent.rect.width/parent.rect.height-2*edgePadding.x;
        float height=designHeight-2*edgePadding.y;
        // Keep the controls' minimum layout area on short windows, then fit the
        // complete page uniformly. Pixel-height layout made the slider and its
        // bottom-anchored explanation occupy the same space at 576p.
        float scale=Mathf.Max(.01f,Mathf.Min(1f,Mathf.Min(width/Mathf.Max(1,minimumLayoutSize.x),height/Mathf.Max(1,minimumLayoutSize.y))));
        transform.localScale=Vector3.one*(parent.rect.height/designHeight)*scale;
        width=Mathf.Min(width/scale,maximumLayoutWidth);height/=scale;
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
