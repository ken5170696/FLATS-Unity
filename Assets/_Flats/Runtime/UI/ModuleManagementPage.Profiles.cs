using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Flats.Modules;

public sealed partial class ModuleManagementPage
{
    [SerializeField] Button profilesButton,newProfileButton;
    [SerializeField] GameObject profileNamePanel;
    [SerializeField] InputField profileNameInput;
    Action<string> saveProfileName;
    ModProfiles Profiles=>Host.Profiles;
    
    void NameProfile(string initial,Action<string> save){saveProfileName=save;profileNameInput.text=initial;ShowModal(profileNamePanel);Focus(profileNameInput);}
    void RenderProfiles()
    {
        ClearRows();if(Profiles==null){Empty("Profiles unavailable\n"+Service.Notice);return;}
        int columns=Wide?3:2;float gap=20,width=(ListWidth-16-gap*(columns-1))/columns,height=300;int i=0;
        foreach(var profile in Profiles.Snapshot())
        {
            bool selected=profile.id==Profiles.SelectedId,running=profile.id==Profiles.RunningId;
            float x=-(ListWidth-16)/2+width/2+(i%columns)*(width+gap),y=-(i/columns)*(height+gap);
            var view=Instantiate(profileCardPrefab,listContent,false);view.name="Profile-"+profile.id;view.background.color=selected?ModCenterWidgets.Tint:Color.white;
            var card=(RectTransform)view.transform;card.anchoredPosition=new Vector2(x,y);card.sizeDelta=new Vector2(width,height);
            card.anchorMin=card.anchorMax=new Vector2(.5f,1);card.pivot=new Vector2(.5f,1);
            view.title.text=profile.name;
            view.modules.text=profile.modules.Count(m=>m.requested)+" mods enabled";
            string state=(selected?"Selected":"Saved profile")+(running?" / Running":"")+(selected&&Profiles.RestartRequired?" / Restart required":"");
            view.state.text=state;
            view.select.GetComponentInChildren<Text>().text=selected?"Manage selected":"Review & switch";
            view.select.image.color=selected?ModCenterWidgets.Accent:ModCenterWidgets.ControlColor;
            view.select.GetComponentInChildren<Text>().color=selected?Color.white:ModCenterWidgets.Ink;
            Listen(view.select,()=>
            {
                if(selected){Switch("Installed");return;}
                Ask("Select "+profile.name+"?\n\n"+profile.modules.Count(m=>m.requested)+" enabled mods. Dependencies and conflicts will be checked.\n\nRestart FLATS to run this configuration. Installed packages are shared.",()=>Run(async()=>{Host.SelectProfile(profile.id);await Service.RefreshInstalled();if(this==null || !isActiveAndEnabled)return;RenderProfiles();notice.text=Host.Notice;},false));
            });
            Listen(view.duplicate,()=>NameProfile(profile.name+" copy",name=>Profiles.Create(name,profile.id)));
            Listen(view.rename,()=>NameProfile(profile.name,name=>Profiles.Rename(profile.id,name)));
            Listen(view.remove,()=>Ask("Delete "+profile.name+"?\n\nOnly this saved configuration is removed. Shared packages remain installed.",()=>{try{Profiles.Delete(profile.id);RenderProfiles();}catch(Exception e){notice.text=e.Message;}}));
            view.remove.interactable=!selected&&!running;
            i++;
        }
        listContent.sizeDelta=new Vector2(0,Mathf.Max(listScroll.viewport.rect.height,((i+columns-1)/columns)*(height+gap)));
        previous.interactable=next.interactable=false;ApplyView();
        notice.text="Selected: "+Profiles.Selected.name+". "+(Profiles.RestartRequired?"Restart required; the running configuration is unchanged.":"Profile edits are saved locally; packages are shared.");
    }
    void LayoutProfiles()
    {
        bool show=tab=="Profiles";profilesButton.gameObject.SetActive(!settingsOpen&&!detailOpen&&!show);newProfileButton.gameObject.SetActive(show);
        if(!show)return;
        importButton.gameObject.SetActive(false);explore.gameObject.SetActive(false);installed.gameObject.SetActive(false);downloads.gameObject.SetActive(false);summary.gameObject.SetActive(false);
        pageTitle.text="PROFILES";subtitle.text="Keep your enabled mods and settings together.";
        Box(listScroll.transform,30,130,ContentWidth,layoutHeight-210);listScroll.viewport.sizeDelta=new Vector2(ContentWidth-12,layoutHeight-210);
    }
}
