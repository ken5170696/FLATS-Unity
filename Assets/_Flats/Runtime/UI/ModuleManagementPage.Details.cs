using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Flats.Modules;

public sealed partial class ModuleManagementPage
{
    string detailSection="Overview";
    Button overviewTab,versionsTab,dependenciesTab;
    Text atGlance;
    ScrollRect glanceScroll;
    RectTransform glanceContent;
    void BuildDetailSections()
    {
        overviewTab=ui.Button("Overview",detailPanel.transform,"Overview",0,0,160,44,()=>{detailSection="Overview";ShowDetail();});
        versionsTab=ui.Button("Versions",detailPanel.transform,"Versions",0,0,160,44,()=>{detailSection="Versions";ShowDetail();});
        dependenciesTab=ui.Button("Dependencies",detailPanel.transform,"Dependencies",0,0,170,44,()=>{detailSection="Dependencies";ShowDetail();});
        glanceScroll=ui.Scroll("Requirements",detailPanel.transform,0,0,300,300,out glanceContent);
        atGlance=ui.Text("AtGlance",glanceContent,"",0,0,290,300,17,ModCenterWidgets.Muted);atGlance.alignment=TextAnchor.UpperLeft;atGlance.rectTransform.anchorMin=atGlance.rectTransform.anchorMax=new Vector2(.5f,1);atGlance.rectTransform.pivot=new Vector2(.5f,1);
    }
    void DetailSections(PackageManifest m,InstalledPackage local)
    {
        float w=ContentWidth,h=layoutHeight-200;
        Box(detailPanel.transform,30,114,w,h);
        Place(detailTitle.transform,-180,h/2-45,w-410,80);
        Place(enable.transform,w/2-124,h/2-42,200,46);
        Place(overviewTab.transform,-w/2+84,h/2-116,160,44);
        Place(versionsTab.transform,-w/2+250,h/2-116,160,44);
        Place(dependenciesTab.transform,-w/2+426,h/2-116,180,44);
        overviewTab.gameObject.SetActive(true);versionsTab.gameObject.SetActive(m!=null);dependenciesTab.gameObject.SetActive(m!=null&&(m.dependencies?.Length??0)>0);
        foreach(var b in new[]{overviewTab,versionsTab,dependenciesTab}){b.image.color=b.name==detailSection?ModCenterWidgets.Tint:ModCenterWidgets.Paper;b.GetComponentInChildren<Text>().color=b.name==detailSection?ModCenterWidgets.Accent:ModCenterWidgets.Muted;}
        atGlance.gameObject.SetActive(m!=null);glanceScroll.gameObject.SetActive(m!=null);
        if(m==null)return;
        known.TryGetValue(m.id,out var item);
        string compatibility=ModRules.Compatibility(m);
        string deps=string.Join("\n\n",(m.dependencies??new DependencySpec[0]).Select(d=>
        {
            var installed=Service.Installed.FirstOrDefault(p=>p.manifest.id==d.id);
            return d.id+"\nRequired: >= "+d.minimum+" < "+d.maximum+"\n"+(installed==null?"Not installed":"Installed "+installed.manifest.version);
        }));
        string text;
        if(detailSection=="Versions")text="Available version: "+m.version+"\n"+(compatibility.Length==0?"Compatible with this FLATS build":compatibility)+"\n\nInstalled: "+(local?.manifest.version??"None")+"\n\nChanges\n"+(string.IsNullOrWhiteSpace(m.changelog)?"No changelog provided.":m.changelog);
        else if(detailSection=="Dependencies")text="Required dependencies\n\n"+deps+"\n\nDependencies are installed individually after your review.";
        else text=(m.description??"No description provided.")+"\n\n"+PlayerProblem(m)+"\n\n"+(m.scope=="ClientOnly"?"Affects your screen only.":"This mod participates in multiplayer session requirements.")+"\n\n"+(m.kind=="managed"?"This package runs code with FLATS privileges.":"Changes the local crosshair appearance.")+"\n\n"+(local==null?"Not installed. Installation does not enable a new mod.":InstalledStatus(local));
        atGlance.text="At a glance\n\nVersion: "+m.version+"\n\nPackage: "+(item==null?"Local":(item.bytes/1024f).ToString("0.0")+" KB")+"\n\nGame: >= "+m.gameMinimum+" < "+m.gameMaximum+"\n\nMod API: >= "+m.apiMinimum+" < "+m.apiMaximum+"\n\nScope: "+m.scope+"\n\nSource: "+(local?.source??Service.SourceUrl);
        float bodyW=w-368,bodyH=h-276;
        Place(detailScroll.transform,-184,-24,bodyW,bodyH);detailScroll.viewport.sizeDelta=new Vector2(bodyW-12,bodyH);
        description.text=text;description.rectTransform.anchoredPosition=Vector2.zero;description.rectTransform.sizeDelta=new Vector2(bodyW-36,bodyH);
        description.rectTransform.sizeDelta=new Vector2(bodyW-36,Mathf.Max(bodyH,description.preferredHeight+20));detailContent.sizeDelta=new Vector2(0,description.rectTransform.sizeDelta.y);
        if(detailSection=="Overview"&&!string.IsNullOrEmpty(item?.imageUrl))
        {
            detailArtwork.gameObject.SetActive(true);detailArtwork.rectTransform.anchoredPosition=new Vector2(0,-94);
            description.rectTransform.anchoredPosition=new Vector2(0,-204);detailContent.sizeDelta+=new Vector2(0,204);
        }
        else detailArtwork.gameObject.SetActive(false);
        Place(glanceScroll.transform,w/2-160,-24,300,bodyH);glanceScroll.viewport.sizeDelta=new Vector2(288,bodyH);Place(atGlance.transform,0,0,276,bodyH);float infoH=Mathf.Max(bodyH,atGlance.preferredHeight+12);atGlance.rectTransform.sizeDelta=new Vector2(276,infoH);glanceContent.sizeDelta=new Vector2(0,infoH);
        Place(secondary.transform,w/2-160,-h/2+36,288,46);Place(remove.transform,-w/2+84,-h/2+36,160,46);
    }
}
