using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Flats.Core.Roguelike;

/// <summary>Presentation only. Room commands remain owned by Menu.RogueRoom.</summary>
public sealed class FlatsRoomView : MonoBehaviour
{
    [Serializable] public class Member { public string name,level,weapon;public bool ready,host,self; }
    public Text heading,subtitle,resources,status,primaryLabel;
    public FlatsTile[] members;
    public FlatsTile difficulty,map,heat,armory,ready;
    public Button primary,leave;
    public CanvasGroup group;
    public float activationDelay=.35f;
    Action primaryAction,readyAction,armoryAction,leaveAction;
    GameObject remembered;
    float acceptAfter;
    bool showing;
    public static FlatsRoomView Open(Action primary,Action ready,Action armory,Action leave)
    {
        var v=Instantiate(Resources.Load<FlatsRoomView>("UI/Roguelike/Tiles/FlatsRoomView"));
        v.primaryAction=primary;v.readyAction=ready;v.armoryAction=armory;v.leaveAction=leave;
        v.primary.onClick.AddListener(()=>v.Activate(v.primaryAction));v.leave.onClick.AddListener(()=>v.Activate(v.leaveAction));
        v.remembered=v.primary.gameObject;return v;
    }
    void Activate(Action a){if(FlatsMenuDialog.BlocksMenuInput||!showing||Time.unscaledTime<acceptAfter||a==null)return;acceptAfter=Time.unscaledTime+activationDelay;RogueAudio.Click();a();}
    public void Present(string room,int capacity,Member[] roster,int level,long merits,string difficultyName,int heatValue,bool host,bool mine,string summary)
    {
        RogueMetaUI.Put(heading,"Squad room");subtitle.text=room+" · "+roster.Length+" / "+capacity;
        resources.text=FlatsLocalization.Translate("Level")+"  "+level+"     "+FlatsLocalization.Translate("Merits")+"  "+merits;
        status.text=summary;
        for(int i=0;i<members.Length;i++)
        {
            var tile=members[i];var p=i<roster.Length?roster[i]:null;
            tile.Bind(p!=null?p.name:"Waiting for player",p!=null?(p.host?FlatsLocalization.Translate("Room host")+(p.self?" · "+FlatsLocalization.Translate("You"):""):p.self?FlatsLocalization.Translate("You"):""):"",
                p!=null?p.level:"",p!=null?(p.ready?"✓ ":"")+FlatsLocalization.Translate(p.ready?"Ready":"Not ready yet"):"",
                // the local player's primary weapon; other players publish no loadout (no placeholder icon pretending they do)
                p!=null&&!string.IsNullOrEmpty(p.weapon)?FlatsHqView.Weapon(p.weapon):null,
                p==null?FlatsUiTheme.Token.Supply:p.ready?FlatsUiTheme.Token.Tactical:FlatsUiTheme.Token.Ink);
            // Names are player data, never localization keys.
            var localized=tile.title as FlatsLocalizedText;if(localized!=null)localized.translate=p==null;
            tile.title.text=p!=null?p.name:FlatsLocalization.Translate("Waiting for player");
            tile.SetSelected(p!=null&&p.self);if(tile.check!=null)tile.check.gameObject.SetActive(false);
            tile.navigation=new Navigation{mode=Navigation.Mode.None};
        }
        difficulty.Bind("Difficulty","",FlatsLocalization.Translate("Set when creating the room"),difficultyName,FlatsHqView.Icon("Target"),FlatsUiTheme.Token.Ink);
        map.Bind("Map","",FlatsLocalization.Translate("Vote after the host starts"),"",FlatsHqView.Icon("Map"),FlatsUiTheme.Token.Ink);
        // the run takes the host's Heat when it starts; it is not published to the room, so a client cannot show a number
        heat.Bind("Heat","",FlatsLocalization.Translate(heatValue<0?"Set by the host":"Your Heat applies to the squad"),heatValue<0?"":heatValue.ToString(),FlatsHqView.Icon("Flame"),FlatsUiTheme.Token.Ultimate);
        foreach(var t in new[]{difficulty,map,heat})t.navigation=new Navigation{mode=Navigation.Mode.None};
        armory.Bind("Loadout and armory","","Prepare your next run","",FlatsHqView.Icon("Equipment"),FlatsUiTheme.Token.Core,()=>Activate(armoryAction));
        ready.Bind(mine?"Cancel ready":"Ready","",mine?"Ready · press to cancel":"Not ready · press when ready","",FlatsHqView.Icon("Check"),mine?FlatsUiTheme.Token.Tactical:FlatsUiTheme.Token.Ink,()=>Activate(readyAction));
        RogueMetaUI.Put(primaryLabel,host?"Start":mine?"Cancel ready":"Ready");RogueMetaUI.Put(leave.GetComponentInChildren<Text>(),"Leave room");
    }
    public void SetVisible(bool visible)
    {
        if(showing==visible)return;showing=visible;GetComponent<Canvas>().enabled=visible;group.interactable=group.blocksRaycasts=visible;
        if(!visible)FlatsMenuDialog.CloseAll();   // "Leave room" left open over a room that started or was lost
        if(visible){PointerFocusPolicy.Hold(this);acceptAfter=Time.unscaledTime+activationDelay;if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(remembered);}
        else PointerFocusPolicy.Release(this);
    }
    void Update(){if(!showing)return;bool modal=FlatsMenuDialog.BlocksMenuInput;group.interactable=group.blocksRaycasts=!modal;if(modal)return;var e=EventSystem.current;if(e==null)return;var s=e.currentSelectedGameObject;if(s!=null&&s.activeInHierarchy&&s.transform.IsChildOf(transform))remembered=s;else if(remembered!=null)e.SetSelectedGameObject(remembered);}
    void OnDestroy(){PointerFocusPolicy.Release(this);if(showing)FlatsMenuDialog.CloseAll();}
    /// <summary>While the run is starting the room stays up, with its actions off.</summary>
    public void SetStarting(bool starting){if(primary.interactable==starting){primary.interactable=!starting;ready.SetAvailable(!starting);armory.SetAvailable(!starting);}}
}
