using System;
using System.Linq;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class FlatsPauseView : MonoBehaviour
{
    public Text heading, status, notice, resumeKey;
    public FlatsTile resume, settings, help, abandon, menu;
    public Button primary;
    public CanvasGroup group;
    public float activationDelay=.35f;
    bool bound;
    Menu owner; Action resumeAction,settingsAction,helpAction,abandonAction,menuAction;
    GameObject remembered;
    float acceptAfter;
    int depth=-1,heat=-1;string objective;bool chinese;
    public static FlatsPauseView Open(Menu owner,Action resume,Action settings,Action help,Action abandon,Action menu)
    {
        var view=Instantiate(Resources.Load<FlatsPauseView>("UI/Roguelike/Tiles/FlatsPauseView"));
        view.bound=true;view.owner=owner;view.resumeAction=resume;view.settingsAction=settings;view.helpAction=help;view.abandonAction=abandon;view.menuAction=menu;
        view.Bind();view.primary.onClick.AddListener(()=>view.Activate(view.resumeAction));view.FocusResume();return view;
    }
    public void FocusResume(){remembered=resume.gameObject;acceptAfter=Time.unscaledTime+activationDelay;if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(remembered);}
    void Bind()
    {
        RogueMetaUI.Put(heading,"Pause");
        resume.Bind("Resume","","Return to this run","",FlatsHqView.Icon("Dash"),FlatsUiTheme.Token.BrandPrimary,()=>Activate(resumeAction));
        settings.Bind("Settings","","Sound, display and controls","",FlatsHqView.Icon("Mod"),FlatsUiTheme.Token.Ink,()=>Activate(settingsAction));
        help.Bind("Controls and how to play","","Controls follow your bindings","",FlatsHqView.Icon("Objective"),FlatsUiTheme.Token.Mod,()=>Activate(helpAction));
        abandon.Bind("Abandon run","","Confirm before leaving","",FlatsHqView.Icon("Skull"),FlatsUiTheme.Token.Ink,()=>Activate(abandonAction));
        menu.Bind("Back to main menu","","Confirm before leaving","",FlatsHqView.Icon("Reload"),FlatsUiTheme.Token.Ink,()=>Activate(menuAction));
        RogueMetaUI.Put(primary.GetComponentInChildren<Text>(),"Resume");
        RogueMetaUI.Put(notice,RoguelikeMode.Coop?"Multiplayer does not pause":"");
        resumeKey.text=RogueInput.IsTouch?"":RogueInput.Current==RogueInput.Scheme.Gamepad?"B":"Esc";
        chinese=FlatsLocalization.IsChinese;depth=-1;RefreshStatus();
    }
    void Activate(Action action) { if(FlatsMenuDialog.BlocksMenuInput||Time.unscaledTime<acceptAfter||action==null)return;acceptAfter=Time.unscaledTime+activationDelay;RogueAudio.Click();action(); }
    void RefreshStatus()
    {
        var c=RoguelikeController.Instance;var s=c!=null?c.State:null;if(s==null)return;
        string id=s.encounter==null?"":s.encounter.IsFinale?s.encounter.finaleId:s.encounter.objectiveId;
        if(depth==s.depth&&heat==s.heat&&objective==id)return;depth=s.depth;heat=s.heat;objective=id;
        var def=RogueCatalog.Objectives.FirstOrDefault(x=>x.Id==id);
        status.text=string.Format(FlatsLocalization.Translate("Stage {0}-{1} \u00b7 {2} \u00b7 Heat {3}"),RogueDepth.ChapterOf(depth),RogueDepth.StageInChapter(depth),FlatsLocalization.Translate(def!=null?def.Name:"Preparation"),heat);
    }
    void OnEnable(){PointerFocusPolicy.Hold(this);FlatsCursor.Push(this);FlatsLocalization.Changed+=Bind;acceptAfter=Time.unscaledTime+activationDelay;}
    void OnDisable(){PointerFocusPolicy.Release(this);FlatsCursor.Pop(this);FlatsLocalization.Changed-=Bind;}
    void OnDestroy(){PointerFocusPolicy.Release(this);FlatsCursor.Pop(this);FlatsMenuDialog.CloseAll();}
    void Update()
    {
        if(!bound)return;
        if(owner==null||Menu.current=="Result"){Destroy(gameObject);return;}if(chinese!=FlatsLocalization.IsChinese)Bind();RefreshStatus();
        string cap=RogueInput.IsTouch?"":RogueInput.Current==RogueInput.Scheme.Gamepad?"B":"Esc";
        if(resumeKey.text!=cap){resumeKey.text=cap;resumeKey.transform.parent.gameObject.SetActive(cap.Length>0);}
        bool modal=FlatsMenuDialog.BlocksMenuInput;group.interactable=group.blocksRaycasts=!modal;
        if(modal)return;
        var es=EventSystem.current;if(es==null)return;var selected=es.currentSelectedGameObject;
        if(selected!=null&&selected.activeInHierarchy&&selected.transform.IsChildOf(transform))remembered=selected;
        else if(remembered!=null&&remembered.activeInHierarchy)es.SetSelectedGameObject(remembered);
    }
}
