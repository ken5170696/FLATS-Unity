using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ModuleManagementPage
{
    [SerializeField] GameObject detailPanel;
    [SerializeField] RectTransform modalShield;
    [SerializeField, Min(0)] float listBottomPadding=12;
    float layoutWidth=0,layoutHeight=0,savedScroll=1,rowY;
    bool detailOpen,readyRendered,restoreScroll,sourceFailed;
    readonly Dictionary<string,ViewState> views=new Dictionary<string,ViewState>();
    sealed class ViewState { public int offset;public string query,selected;public float scroll; }
    float ContentWidth=>layoutWidth-60;
    bool Wide=>layoutWidth>=splitViewWidth;
    bool HasQuickPanel=>tab=="Installed"&&Wide&&Service.Installed.Length>0;
    float ListWidth=>HasQuickPanel&&!detailOpen?ContentWidth-368:ContentWidth-FilterWidth;
    void SaveView() { views[tab]=new ViewState { offset=offset,query=search.text,selected=selectedId,scroll=listScroll.verticalNormalizedPosition }; }
    void FocusSelected()
    {
        var item=listContent.Find("Mod-"+selectedId);
        if(item!=null)Focus(item.GetComponent<Button>());else Focus(tab=="Explore"?explore:tab=="Installed"?installed:downloads);
    }
    static void Place(Transform t,float x,float y,float w,float h)
    {
        var r=(RectTransform)t;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);
        var b=t.GetComponent<Button>();if(b!=null){var label=b.transform.Find("Label")?.GetComponent<Text>();if(label!=null && label.rectTransform.anchorMin==label.rectTransform.anchorMax)label.rectTransform.sizeDelta=new Vector2(w-20,h-4);}
    }
    void Box(Transform t,float x,float top,float w,float h) { Place(t,-layoutWidth/2+x+w/2,layoutHeight/2-top-h/2,w,h); }
    void Layout()
    {
        // Fixed controls use authored anchors. Resizing must not overwrite the
        // positions, typography or sizes edited in Prefab Mode.
        root.sizeDelta=new Vector2(layoutWidth,layoutHeight);
        modalShield.sizeDelta=new Vector2(layoutWidth+edgePadding.x*2,layoutHeight+edgePadding.y*2);
        LayoutConfirmation();
        ApplyView();
    }
    void LayoutConfirmation()
    {
        float w=Mathf.Min(830,ContentWidth-140);Place(modalScroll.transform,0,-4,w,244);modalScroll.viewport.sizeDelta=new Vector2(w-12,244);confirmText.rectTransform.sizeDelta=new Vector2(w-24,244);float h=Mathf.Max(244,confirmText.preferredHeight);confirmText.rectTransform.sizeDelta=new Vector2(w-24,h);modalContent.sizeDelta=new Vector2(0,h);
    }
    void ApplyView()
    {
        bool normal=!detailOpen&&!settingsOpen;
        bool tools=normal&&(tab=="Installed"||tab=="Explore"&&Service.Source!=null&&!sourceFailed||tab=="Downloads"&&(Service.Downloads?.Snapshot().Length??0)>0);
        search.gameObject.SetActive(tools);filter.gameObject.SetActive(tools&&tab!="Downloads");category.gameObject.SetActive(tools&&tab!="Downloads");sort.gameObject.SetActive(tools&&tab!="Downloads");
        ((Text)search.placeholder).text=tab=="Installed"?"Search installed mods...":tab=="Downloads"?"Search downloads...":"Search mods...";
        listScroll.gameObject.SetActive(normal);detailPanel.SetActive(detailOpen&&!settingsOpen);
        listHeading.gameObject.SetActive(normal&&tab=="Installed");
        foreach(var b in statusTabs)b.gameObject.SetActive(normal&&tab=="Installed");
        versionHeading.gameObject.SetActive(normal&&tab=="Installed");stateHeading.gameObject.SetActive(normal&&tab=="Installed");enabledHeading.gameObject.SetActive(normal&&tab=="Installed");
        float rowWidth=ListWidth-16;Box(versionHeading.transform,38+rowWidth*.44f-50,Wide?304:282,100,28);Box(stateHeading.transform,38+rowWidth*.64f-rowWidth*.135f,Wide?304:282,160,28);Box(enabledHeading.transform,38+rowWidth-162,Wide?304:282,100,28);columnHeading.gameObject.SetActive(normal&&tab=="Installed");
        Box(columnHeading.transform,38,Wide?304:282,200,28);
        quickPanel.SetActive(normal&&HasQuickPanel);
        float top=tab=="Installed"?(Wide?340:312):tools?(Wide?252:230):200;
        float h=layoutHeight-top-((previous.interactable||next.interactable)?112:76);
        Box(listScroll.transform,30+FilterWidth,top,ListWidth,h);listScroll.viewport.sizeDelta=new Vector2(ListWidth-12,h);
        Box(quickPanel.transform,layoutWidth-374,252,344,layoutHeight-328);
        RefreshFilters();
        previous.gameObject.SetActive(normal&&previous.interactable);next.gameObject.SetActive(normal&&next.interactable);pageLabel.gameObject.SetActive(normal&&(previous.interactable||next.interactable));
        foreach(var button in new[]{explore,installed,downloads})
        {
            button.gameObject.SetActive(!settingsOpen&&!detailOpen);
            bool active=button.name==tab;
            button.image.color=active?ModCenterWidgets.Accent:ModCenterWidgets.ControlColor;
            button.GetComponentInChildren<Text>().color=active?Color.white:ModCenterWidgets.Muted;
        }
        importButton.gameObject.SetActive(!settingsOpen&&!detailOpen);
        summary.gameObject.SetActive(!settingsOpen&&!detailOpen);brandIcon.gameObject.SetActive(false);
        subtitle.gameObject.SetActive(!settingsOpen);
        pageTitle.text=settingsOpen?"Custom Crosshair":detailOpen?"MOD / "+tab.ToUpperInvariant():"MOD";
        notice.gameObject.SetActive(!settingsOpen);
        crosshairPanel.SetActive(settingsOpen);
        ((RectTransform)footerRule).anchoredPosition=new Vector2(0,settingsOpen?76:56);
        subtitle.text="Enable, configure and manage your mods.";
        LayoutProfiles();
    }
}
