using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed partial class ModuleManagementPage
{
    bool filtersOpen;
    GameObject filtersPanel;
    ScrollRect filtersScroll;
    RectTransform filtersContent;
    float FilterWidth=>filtersOpen&&tab=="Explore"&&!detailOpen&&Service.Source!=null&&!sourceFailed?290:0;
    void RefreshFilters()
    {
        if(filtersPanel==null){filtersPanel=ui.Panel("ExploreFilters",root,0,0,270,400,Color.white).gameObject;filtersScroll=ui.Scroll("FilterOptions",filtersPanel.transform,0,0,264,390,out filtersContent);}
        bool show=FilterWidth>0&&!settingsOpen;filtersPanel.SetActive(show);if(!show)return;
        float h=layoutHeight-(Wide?252:230)-76;Box(filtersPanel.transform,30,Wide?252:230,270,h);Place(filtersScroll.transform,0,0,264,h-16);filtersScroll.viewport.sizeDelta=new Vector2(252,h-16);
        string selected=EventSystem.current?.currentSelectedGameObject!=null&&EventSystem.current.currentSelectedGameObject.transform.IsChildOf(filtersContent)?EventSystem.current.currentSelectedGameObject.name:null;
        foreach(Transform child in filtersContent){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        float y=12;
        System.Action<Transform,float> position=(t,height)=>{var r=(RectTransform)t;r.anchorMin=r.anchorMax=new Vector2(.5f,1);r.pivot=new Vector2(.5f,1);r.anchoredPosition=new Vector2(0,-y);y+=height+10;};
        var title=ui.Text("Heading",filtersContent,"Filters",0,0,218,32,22);position(title.transform,32);
        var close=ui.Button("CloseFilters",filtersContent,"Close filters",0,0,218,40,()=>{filtersOpen=false;Reload();});position(close.transform,40);
        var label=ui.Text("CategoryLabel",filtersContent,"CATEGORY",0,0,218,26,15,ModCenterWidgets.Accent);position(label.transform,26);
        var values=new System.Collections.Generic.List<string>{""};foreach(var value in categories)if(!string.IsNullOrEmpty(value)&&!values.Contains(value))values.Add(value);
        foreach(var value in values){string choice=value;var b=ui.Button("Category-"+choice,filtersContent,(categoryValue==choice?"✓  ":"    ")+(choice.Length==0?"All categories":choice),0,0,218,42,()=>{categoryValue=choice;offset=0;Reload();},categoryValue==choice?ModCenterWidgets.Tint:Color.white);position(b.transform,42);}
        var compatibleButton=ui.Button("CompatibleFilter",filtersContent,(compatible?"✓  ":"    ")+"Current game build",0,0,218,44,()=>{compatible=!compatible;offset=0;Reload();});position(compatibleButton.transform,44);
        var clear=ui.Button("ClearFilters",filtersContent,"Clear filters",0,0,218,44,()=>{categoryValue="";compatible=true;offset=0;Reload();});position(clear.transform,44);filtersContent.sizeDelta=new Vector2(0,y);
        if(selected!=null)foreach(Transform child in filtersContent)if(child.gameObject.activeSelf&&child.name==selected){EventSystem.current.SetSelectedGameObject(child.gameObject);break;}
    }
}
