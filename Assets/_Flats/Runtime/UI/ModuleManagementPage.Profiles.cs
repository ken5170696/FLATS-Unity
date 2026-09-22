using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Flats.Modules;

public sealed partial class ModuleManagementPage
{
    Button profilesButton,newProfileButton;
    GameObject profileNamePanel;
    InputField profileNameInput;
    Action<string> saveProfileName;
    ModProfiles Profiles=>BuiltinModules.Instance.Profiles;
    void BuildProfiles()
    {
        profilesButton=ui.Button("Profiles",root,"Profiles",0,0,150,46,()=>Switch("Profiles"));
        newProfileButton=ui.Button("NewProfile",root,"+ New profile",0,0,164,46,()=>NameProfile("New profile",name=>Profiles.Create(name)));
        newProfileButton.gameObject.SetActive(false);
        profileNamePanel=ui.Panel("ProfileNameDialog",root,0,0,660,250,ModCenterWidgets.Paper).gameObject;
        ui.Text("ProfileNameTitle",profileNamePanel.transform,"Profile name",0,80,600,40,28);
        profileNameInput=ui.Input("ProfileName",profileNamePanel.transform,"Name",0,15,570);profileNameInput.characterLimit=48;
        ui.Button("SaveProfileName",profileNamePanel.transform,"Save",150,-75,250,46,()=>
        {
            try{saveProfileName(profileNameInput.text);HideModal();RenderProfiles();notice.text="Profile saved.";}catch(Exception e){notice.text=e.Message;}
        },ModCenterWidgets.Accent);
        ui.Button("CancelProfileName",profileNamePanel.transform,"Cancel",-150,-75,250,46,HideModal);
        profileNamePanel.SetActive(false);
    }
    void NameProfile(string initial,Action<string> save){saveProfileName=save;profileNameInput.text=initial;ShowModal(profileNamePanel);Focus(profileNameInput);}
    void RenderProfiles()
    {
        ClearRows();if(Profiles==null){Empty("Profiles unavailable\n"+Service.Notice);return;}
        int columns=Wide?3:2;float gap=20,width=(ListWidth-16-gap*(columns-1))/columns,height=300;int i=0;
        foreach(var profile in Profiles.Snapshot())
        {
            bool selected=profile.id==Profiles.SelectedId,running=profile.id==Profiles.RunningId;
            float x=-(ListWidth-16)/2+width/2+(i%columns)*(width+gap),y=-(i/columns)*(height+gap);
            var card=ui.Panel("Profile-"+profile.id,listContent,x,y,width,height,selected?ModCenterWidgets.Tint:Color.white).rectTransform;
            card.anchorMin=card.anchorMax=new Vector2(.5f,1);card.pivot=new Vector2(.5f,1);
            ui.Text("ProfileTitle",card,profile.name,0,-36,width-36,52,26);
            ui.Text("ProfileModules",card,profile.modules.Count(m=>m.requested)+" mods enabled",0,-82,width-36,34,17,ModCenterWidgets.Muted);
            string state=(selected?"Selected":"Saved profile")+(running?" / Running":"")+(selected&&Profiles.RestartRequired?" / Restart required":"");
            ui.Text("ProfileState",card,state,0,-124,width-36,48,16,ModCenterWidgets.Accent);
            ui.Button("SelectProfile-"+profile.id,card,selected?"Manage selected":"Review & switch",0,-178,width-36,44,()=>
            {
                if(selected){Switch("Installed");return;}
                Ask("Select "+profile.name+"?\n\n"+profile.modules.Count(m=>m.requested)+" enabled mods. Dependencies and conflicts will be checked.\n\nRestart FLATS to run this configuration. Installed packages are shared.",()=>Run(async()=>{BuiltinModules.Instance.SelectProfile(profile.id);await Service.RefreshInstalled();RenderProfiles();notice.text=BuiltinModules.Instance.Notice;},false));
            },selected?ModCenterWidgets.Accent:ModCenterWidgets.ControlColor);
            ui.Button("DuplicateProfile-"+profile.id,card,"Duplicate",-width*.24f,-234,width*.43f,40,()=>NameProfile(profile.name+" copy",name=>Profiles.Create(name,profile.id)));
            ui.Button("RenameProfile-"+profile.id,card,"Rename",width*.24f,-234,width*.43f,40,()=>NameProfile(profile.name,name=>Profiles.Rename(profile.id,name)));
            var removeProfile=ui.Button("DeleteProfile-"+profile.id,card,"Delete",0,-278,width-36,34,()=>Ask("Delete "+profile.name+"?\n\nOnly this saved configuration is removed. Shared packages remain installed.",()=>{try{Profiles.Delete(profile.id);RenderProfiles();}catch(Exception e){notice.text=e.Message;}}));
            removeProfile.interactable=!selected&&!running;
            foreach(Transform child in card)((RectTransform)child).anchorMin=((RectTransform)child).anchorMax=new Vector2(.5f,1);
            i++;
        }
        listContent.sizeDelta=new Vector2(0,Mathf.Max(listScroll.viewport.rect.height,((i+columns-1)/columns)*(height+gap)));
        previous.interactable=next.interactable=false;ApplyView();
        notice.text="Selected: "+Profiles.Selected.name+". "+(Profiles.RestartRequired?"Restart required; the running configuration is unchanged.":"Profile edits are saved locally; packages are shared.");
    }
    void LayoutProfiles()
    {
        Box(profilesButton.transform,layoutWidth-300,36,154,46);Box(newProfileButton.transform,layoutWidth-486,36,176,46);
        bool show=tab=="Profiles";profilesButton.gameObject.SetActive(!settingsOpen&&!show);newProfileButton.gameObject.SetActive(show);
        if(!show)return;
        importButton.gameObject.SetActive(false);explore.gameObject.SetActive(false);installed.gameObject.SetActive(false);downloads.gameObject.SetActive(false);summary.gameObject.SetActive(false);
        root.Find("Title").GetComponent<Text>().text="PROFILES";subtitle.text="Keep your enabled mods and settings together.";
        Box(listScroll.transform,30,130,ContentWidth,layoutHeight-210);listScroll.viewport.sizeDelta=new Vector2(ContentWidth-12,layoutHeight-210);
    }
}
