using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ModuleManagementPage
{
    GameObject detailPanel;
    RectTransform modalShield;
    float layoutWidth=0,layoutHeight=0,savedScroll=1,rowY;
    bool detailOpen,readyRendered,restoreScroll,sourceFailed;
    readonly Dictionary<string,ViewState> views=new Dictionary<string,ViewState>();
    sealed class ViewState { public int offset;public string query,selected;public float scroll; }
    float ContentWidth=>layoutWidth-60;
    bool Wide=>layoutWidth>=1350;
    float ListWidth=>tab=="Installed"&&Wide&&!detailOpen?ContentWidth-368:ContentWidth-FilterWidth;
    void SaveView() { views[tab]=new ViewState { offset=offset,query=search.text,selected=selectedId,scroll=listScroll.verticalNormalizedPosition }; }
    void FocusSelected()
    {
        var item=listContent.Find("Mod-"+selectedId);
        if(item!=null)Focus(item.GetComponent<Button>());else Focus(tab=="Explore"?explore:tab=="Installed"?installed:downloads);
    }
    static void Place(Transform t,float x,float y,float w,float h)
    {
        var r=(RectTransform)t;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);
        var b=t.GetComponent<Button>();if(b!=null){var label=b.transform.Find("Label")?.GetComponent<Text>();if(label!=null)label.rectTransform.sizeDelta=new Vector2(w-20,h-4);}
    }
    void Box(Transform t,float x,float top,float w,float h) { Place(t,-layoutWidth/2+x+w/2,layoutHeight/2-top-h/2,w,h); }
    void Layout()
    {
        root.sizeDelta=new Vector2(layoutWidth,layoutHeight);
        Box(root.Find("Title"),86,24,300,50);
        root.Find("Title").GetComponent<Text>().color=ModCenterWidgets.Ink;
        Box(brandIcon,30,45,34,34);Box(subtitle.transform,86,72,600,26);
        Box(back.transform,layoutWidth-136,36,106,46);
        back.GetComponentInChildren<Text>().text="Back";back.image.color=ModCenterWidgets.Paper;back.GetComponentInChildren<Text>().color=ModCenterWidgets.Ink;
        var arrow=back.transform.Find("Arrow");if(arrow!=null)arrow.gameObject.SetActive(false);
        Box(importButton.transform,layoutWidth-442,36,128,46);
        Box(explore.transform,30,Wide?113:99,180,52);Box(installed.transform,218,Wide?113:99,180,52);Box(downloads.transform,406,Wide?113:99,194,52);
        Box(summary.transform,layoutWidth-650,Wide?123:109,620,30);summary.alignment=TextAnchor.MiddleRight;
        float w=ContentWidth,sw=w-630;
        Box(search.transform,30,Wide?184:162,sw,46);search.textComponent.rectTransform.sizeDelta=new Vector2(sw-28,40);search.placeholder.rectTransform.sizeDelta=new Vector2(sw-28,40);
        Box(category.transform,42+sw,Wide?184:162,182,46);Box(filter.transform,236+sw,Wide?184:162,202,46);Box(sort.transform,450+sw,Wide?184:162,180,46);
        Box(listHeading.transform,30,Wide?257:235,ListWidth,46);
        Box(columnHeading.transform,44,Wide?304:282,ListWidth-28,28);
        for(int i=0;i<4;i++)Box(statusTabs[i].transform,30+i*128,Wide?257:235,i==3?174:120,44);
        Box(notice.transform,30,layoutHeight-48,w-70,38);
        Box(footerRule,0,layoutHeight-56,layoutWidth,1);
        Box(previous.transform,30,layoutHeight-100,44,36);Box(next.transform,130,layoutHeight-100,44,36);Box(pageLabel.transform,190,layoutHeight-100,600,36);
        Box(detailPanel.transform,30,245,w,layoutHeight-315);
        Place(detailTitle.transform,0,(layoutHeight-315)/2-40,w-48,70);
        Place(enable.transform,w/2-144,-(layoutHeight-315)/2+38,240,46);
        Place(secondary.transform,w/2-414,-(layoutHeight-315)/2+38,260,46);
        Place(remove.transform,-w/2+94,-(layoutHeight-315)/2+38,140,46);
        modalShield.sizeDelta=new Vector2(Screen.width,Screen.height);
        Place(confirmPanel.transform,0,0,Mathf.Min(890,w-80),420);confirmText.rectTransform.sizeDelta=new Vector2(Mathf.Min(830,w-140),270);confirmText.rectTransform.anchoredPosition=Vector2.zero;confirmText.rectTransform.sizeDelta=new Vector2(Mathf.Min(830,w-140),244);
        Place(confirmPanel.transform.Find("ConfirmAction"),260,-164,230,46);
        Place(confirmPanel.transform.Find("CancelAction"),-20,-164,210,46);
        Place(extraConfirm.transform,-270,-164,230,46);
        LayoutDraft();LayoutConfirmation();ApplyView();
    }
    void LayoutConfirmation()
    {
        float w=Mathf.Min(830,ContentWidth-140);Place(modalScroll.transform,0,-4,w,244);modalScroll.viewport.sizeDelta=new Vector2(w-12,244);confirmText.rectTransform.sizeDelta=new Vector2(w-24,244);float h=Mathf.Max(244,confirmText.preferredHeight);confirmText.rectTransform.sizeDelta=new Vector2(w-24,h);modalContent.sizeDelta=new Vector2(0,h);
    }
    void ApplyView()
    {
        bool normal=!detailOpen&&!settingsOpen;
        if(detailOpen){Box(detailPanel.transform,30,200,ContentWidth,layoutHeight-286);Place(enable.transform,ContentWidth/2-144,-(layoutHeight-286)/2+38,240,46);Place(secondary.transform,ContentWidth/2-414,-(layoutHeight-286)/2+38,260,46);Place(remove.transform,-ContentWidth/2+94,-(layoutHeight-286)/2+38,140,46); }
        bool tools=normal&&(tab=="Installed"||tab=="Explore"&&Service.Source!=null&&!sourceFailed||tab=="Downloads"&&(Service.Downloads?.Snapshot().Length??0)>0);
        search.gameObject.SetActive(tools);filter.gameObject.SetActive(tools&&tab!="Downloads");category.gameObject.SetActive(tools&&tab!="Downloads");sort.gameObject.SetActive(tools&&tab!="Downloads");
        ((Text)search.placeholder).text=tab=="Installed"?"Search installed mods...":tab=="Downloads"?"Search downloads...":"Search mods...";
        listScroll.gameObject.SetActive(normal);detailPanel.SetActive(detailOpen&&!settingsOpen);
        listHeading.gameObject.SetActive(normal&&tab=="Installed");
        foreach(var b in statusTabs)b.gameObject.SetActive(normal&&tab=="Installed");
        versionHeading.gameObject.SetActive(normal&&tab=="Installed");stateHeading.gameObject.SetActive(normal&&tab=="Installed");enabledHeading.gameObject.SetActive(normal&&tab=="Installed");
        float rowWidth=ListWidth-16;Box(versionHeading.transform,38+rowWidth*.44f-50,Wide?304:282,100,28);Box(stateHeading.transform,38+rowWidth*.64f-rowWidth*.135f,Wide?304:282,160,28);Box(enabledHeading.transform,38+rowWidth-162,Wide?304:282,100,28);columnHeading.gameObject.SetActive(normal&&tab=="Installed");
        quickPanel.SetActive(normal&&tab=="Installed"&&Wide);
        float top=tab=="Installed"?(Wide?340:312):tools?(Wide?252:230):200;
        float h=layoutHeight-top-((previous.interactable||next.interactable)?112:76);
        Box(listScroll.transform,30+FilterWidth,top,ListWidth,h);listScroll.viewport.sizeDelta=new Vector2(ListWidth-12,h);
        Box(quickPanel.transform,layoutWidth-374,252,344,layoutHeight-328);
        LayoutQuick();RefreshFilters();
        previous.gameObject.SetActive(normal&&previous.interactable);next.gameObject.SetActive(normal&&next.interactable);pageLabel.gameObject.SetActive(normal&&(previous.interactable||next.interactable));
        foreach(var button in new[]{explore,installed,downloads})
        {
            button.gameObject.SetActive(!settingsOpen&&!detailOpen);
            bool active=button.name==tab;
            button.image.color=active?ModCenterWidgets.Accent:ModCenterWidgets.ControlColor;
            button.GetComponentInChildren<Text>().color=active?Color.white:ModCenterWidgets.Muted;
        }
        importButton.gameObject.SetActive(!settingsOpen);
        summary.gameObject.SetActive(!settingsOpen&&!detailOpen);brandIcon.gameObject.SetActive(!settingsOpen);
        subtitle.gameObject.SetActive(!settingsOpen);
        root.Find("Title").GetComponent<Text>().text=settingsOpen?"Custom Crosshair":detailOpen?"MOD / "+tab.ToUpperInvariant():"MOD";
        Box(root.Find("Title"),86,24,settingsOpen?700:500,50);notice.gameObject.SetActive(!settingsOpen);
        crosshairPanel.SetActive(settingsOpen);
        Box(footerRule,0,layoutHeight-(settingsOpen?76:56),layoutWidth,1);
        foreach(var nav in new[]{explore,installed,downloads})nav.gameObject.SetActive(!settingsOpen);
        subtitle.text="Enable, configure and manage your mods.";
        LayoutProfiles();
    }
}
