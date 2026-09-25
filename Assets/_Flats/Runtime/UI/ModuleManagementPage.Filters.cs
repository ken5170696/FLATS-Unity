using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed partial class ModuleManagementPage
{
    bool filtersOpen;
    [SerializeField] GameObject filtersPanel;
    [SerializeField] ScrollRect filtersScroll;
    [SerializeField] RectTransform filtersContent;
    [SerializeField] RectTransform categoryChoices;
    [SerializeField] Button categoryChoicePrefab,closeFilters,compatibleFilter,clearFilters;
    void BindFilters()
    {
        Listen(closeFilters,()=>{filtersOpen=false;Reload();});
        Listen(compatibleFilter,()=>{compatible=!compatible;offset=0;Reload();});
        Listen(clearFilters,()=>{categoryValue="";compatible=true;offset=0;Reload();});
    }
    float FilterWidth=>filtersOpen&&tab=="Explore"&&!detailOpen&&Service.Source!=null&&!sourceFailed?290:0;
    void RefreshFilters()
    {
        
        bool show=FilterWidth>0&&!settingsOpen;filtersPanel.SetActive(show);if(!show)return;
        float h=layoutHeight-(Wide?252:230)-76;Box(filtersPanel.transform,30,Wide?252:230,270,h);Place(filtersScroll.transform,0,0,264,h-16);filtersScroll.viewport.sizeDelta=new Vector2(252,h-16);
        string selected=EventSystem.current?.currentSelectedGameObject!=null&&EventSystem.current.currentSelectedGameObject.transform.IsChildOf(filtersContent)?EventSystem.current.currentSelectedGameObject.name:null;
        foreach(Transform child in categoryChoices){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        var values=new System.Collections.Generic.List<string>{""};foreach(var value in categories)if(!string.IsNullOrEmpty(value)&&!values.Contains(value))values.Add(value);
        foreach(var value in values)
        {
            string choice=value;var b=Instantiate(categoryChoicePrefab,categoryChoices,false);b.name="Category-"+choice;
            b.GetComponentInChildren<Text>().text=(categoryValue==choice?"√  ":"    ")+(choice.Length==0?"All categories":choice);
            b.image.color=categoryValue==choice?ModCenterWidgets.Tint:Color.white;
            Listen(b,()=>{categoryValue=choice;offset=0;Reload();});
        }
        compatibleFilter.GetComponentInChildren<Text>().text=(compatible?"√  ":"    ")+"Current game build";
        if(selected!=null)foreach(var button in filtersContent.GetComponentsInChildren<Button>())if(button.name==selected){EventSystem.current.SetSelectedGameObject(button.gameObject);break;}
    }
}
