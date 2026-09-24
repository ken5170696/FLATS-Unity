using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Flats.Modules;

public sealed partial class ModuleManagementPage
{
    // What the package changes, from its kind and the game adapter it targets.
    static string Effect(PackageManifest m)
    {
        if(m.kind=="managed")return "This package runs code with FLATS privileges.";
        if(ModRules.IsCrosshairProvider(m))return "Changes the local crosshair appearance.";
        if(m.kind=="data" && m.adapter==Flats.Core.EnemyTuning.Adapter)return "Changes enemy health, damage and speed for everyone in the room.";
        return "Data package for the game feature "+m.adapter+". It contains no code.";
    }
    string detailSection="Overview";
    [SerializeField] Button overviewTab,versionsTab,dependenciesTab;
    [SerializeField] Text atGlance;
    [SerializeField] ScrollRect glanceScroll;
    [SerializeField] RectTransform glanceContent;
    
    void DetailSections(PackageManifest m,InstalledPackage local)
    {
        overviewTab.gameObject.SetActive(true);versionsTab.gameObject.SetActive(m!=null);dependenciesTab.gameObject.SetActive(m!=null&&(m.dependencies?.Length??0)>0);
        foreach(var b in new[]{overviewTab,versionsTab,dependenciesTab}){b.image.color=b.name==detailSection?ModCenterWidgets.Tint:ModCenterWidgets.Paper;b.GetComponentInChildren<Text>().color=b.name==detailSection?ModCenterWidgets.Accent:ModCenterWidgets.Muted;}
        atGlance.gameObject.SetActive(m!=null&&Wide);glanceScroll.gameObject.SetActive(m!=null&&Wide);
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
        else text=(m.description??"No description provided.")+"\n\n"+PlayerProblem(m)+"\n\n"+(m.scope=="ClientOnly"?"Affects your screen only.":"This mod participates in multiplayer session requirements.")+"\n\n"+Effect(m)+"\n\n"+(local==null?"Not installed. Installation does not enable a new mod.":InstalledStatus(local));
        atGlance.text="At a glance\n\nVersion "+m.version+"\n\n"+(string.IsNullOrWhiteSpace(m.author)?"":"By "+m.author+"\n\n")+(m.scope=="ClientOnly"?"Your screen only":"Multiplayer session mod")+"\n\n"+(local==null?"Not installed":InstalledStatus(local));
        if(!Wide)text="Version "+m.version+(string.IsNullOrWhiteSpace(m.author)?"":" · "+m.author)+"\n\n"+text;
        Canvas.ForceUpdateCanvases();
        float bodyH=detailScroll.viewport.rect.height;
        description.text=text;description.rectTransform.anchoredPosition=Vector2.zero;
        description.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(bodyH,description.preferredHeight+20));
        detailContent.sizeDelta=new Vector2(0,description.rectTransform.rect.height);
        if(detailSection=="Overview"&&!string.IsNullOrEmpty(item?.imageUrl))
        {
            detailArtwork.gameObject.SetActive(true);detailArtwork.rectTransform.anchoredPosition=new Vector2(0,-94);
            description.rectTransform.anchoredPosition=new Vector2(0,-204);detailContent.sizeDelta+=new Vector2(0,204);
        }
        else detailArtwork.gameObject.SetActive(false);
        float infoH=Mathf.Max(glanceScroll.viewport.rect.height,atGlance.preferredHeight+12);
        atGlance.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,infoH);glanceContent.sizeDelta=new Vector2(0,infoH);
    }
}
