using System.Collections;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.EventSystems;

public partial class Menu
{
    FlatsPauseView roguePause;
    string roguePauseUnderlying;
    GameObject roguePauseSelection;
    bool roguePauseRotate;
    RogueScreenView roguePauseScreen;
    RogueOverviewView roguePauseOverview;
    /// <summary>
    /// Pause from a run screen (reward, route, shop, TAB overview). Esc is those screens' own key there (it dismisses the shop and
    /// closes the overview; a pause opened on its key-down closed again on its key-up), so only the pad's Start pauses, and not
    /// while a dialog, a page change or the result screen owns the input. The Overview binding (View/Back by default) is excluded.
    /// </summary>
    bool TryRogueOverlayPause()
    {
        if(!RoguelikeMode.Active||current!="RogueScreen"||!canOpen||fliping||backWithCancel)return false;
        if(FlatsMenuDialog.BlocksMenuInput||RogueResultView.BlocksMenuInput||RogueOverviewView.BlocksMenuInput)return false;
        if((confirm!=null&&confirm.activeSelf)||(update!=null&&update.activeSelf))return false;
        var pad=InControl.InputManager.ActiveDevice;
        if(pad==null||!pad.CommandWasPressed||RogueOverviewButtonPressed())return false;
        OpenMenu();
        if(pauseNavigation.IsOpen)FlatsMenuDialog.Guard();   // the press that opened the pause does not also close it or activate a tile
        return true;
    }
    void PrepareRoguePause()
    {
        if(!RoguelikeMode.Active||pauseNavigation.IsOpen)return;
        roguePauseUnderlying=current;roguePauseRotate=FPSController.enableCamRotate;
        roguePauseSelection=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
        roguePauseScreen=FindObjectOfType<RogueScreenView>();roguePauseOverview=FindObjectOfType<RogueOverviewView>();
        if(current=="RogueScreen")current="Playing";
    }
    void ShowRoguePause()
    {
        if(!RoguelikeMode.Active||!pauseNavigation.IsOpen)return;
        SetTilesHidden(true);backButton.SetActive(false);
        if(roguePause==null)roguePause=FlatsPauseView.Open(this,ResumeRoguePause,OpenRoguePauseSettings,ShowRoguePauseHelp,()=>ConfirmRogueLeave(true),()=>ConfirmRogueLeave(false));
        else {roguePause.gameObject.SetActive(true);roguePause.FocusResume();}
    }
    void HideRoguePause()
    {
        if(roguePause==null)return;Destroy(roguePause.gameObject);roguePause=null;FlatsMenuDialog.Guard();
        // The original pause closes through the page's fade-out, which ends with the six main tiles inactive, and its Back handler
        // then hides the quit buttons. This pause closes at once, so both are put away here: the tiles stay transparent until the
        // page animator has left its main state, are deactivated, and only then get their alpha back for the next page that uses them.
        if(quitButton!=null)quitButton.SetActive(false);
        StartCoroutine(RetireMainTilesAfterRoguePause());
    }
    IEnumerator RetireMainTilesAfterRoguePause()
    {
        float until=Time.realtimeSinceStartup+2f;
        yield return null;
        while(Time.realtimeSinceStartup<until&&anim!=null&&(anim.IsInTransition(0)||!anim.GetCurrentAnimatorStateInfo(0).IsName("None")))
        {
            if(pauseNavigation.IsOpen)yield break;   // paused again: the pause keeps the tiles hidden and the next close retires them
            yield return null;
        }
        if(pauseNavigation.IsOpen||gameState=="Main"||buttons==null)yield break;
        // The run ended meanwhile (abandon, squad wiped): the animator was stopped or has moved to the result page, so nothing here
        // proves the tiles are inactive. They stay transparent; this Menu is replaced when the scene is left.
        if(current=="Result"||anim==null||anim.IsInTransition(0)||!anim.GetCurrentAnimatorStateInfo(0).IsName("None"))yield break;
        for(int i=0;i<buttons.Length;i++)
            if(buttons[i]!=null&&buttons[i].transform.parent!=null)buttons[i].transform.parent.gameObject.SetActive(false);
        SetTilesHidden(false);
        if(quitButton!=null&&current!="Main")quitButton.SetActive(false);
    }
    void RestoreRoguePauseUnderlying()
    {
        if(!RoguelikeMode.Active)return;
        // Only the screens that were up when the pause opened take the state back. In co-op the squad plays on: a screen opened
        // while this player was paused recorded the pause's state as "what was below" and corrects itself from "Playing".
        bool screen=roguePauseScreen!=null&&!roguePauseScreen.Closed,overview=roguePauseOverview!=null;
        if(roguePauseUnderlying=="RogueScreen"&&(screen||overview))
        {
            current="RogueScreen";FPSController.enableCamRotate=roguePauseRotate;
            if(EventSystem.current!=null&&roguePauseSelection!=null&&roguePauseSelection.activeInHierarchy)EventSystem.current.SetSelectedGameObject(roguePauseSelection);
            // the press or click that resumed must not also close the overview, buy the tile under the pointer or take a reward
            if(screen)roguePauseScreen.GuardInput();
            if(overview)roguePauseOverview.GuardInput();
        }
        roguePauseUnderlying=null;roguePauseSelection=null;roguePauseScreen=null;roguePauseOverview=null;
    }
    void ResumeRoguePause(){if(!pauseNavigation.IsOpen)return;CloseMenu();StartCoroutine(ReleaseMenuInput(null));}
    void OpenRoguePauseSettings(){roguePause.gameObject.SetActive(false);SetTilesHidden(false);Fade(3);}
    void ShowRoguePauseHelp(){FlatsMenuDialog.Show("Controls and how to play",RoguelikeHowToPlay(),null,null);}
    bool HandleRoguePauseNavigation(int button)
    {
        if(!RoguelikeMode.Active||!pauseNavigation.IsOpen||roguePause==null)return false;
        if(FlatsMenuDialog.BlocksMenuInput)return true;
        if(current=="Main"&&roguePause.gameObject.activeSelf){if(button==-1)ResumeRoguePause();return true;}
        if(current=="Settings"&&currentDetail==null&&button==-1){StartCoroutine(ReturnRoguePause());return true;}
        return false;
    }
    IEnumerator ReturnRoguePause(){fliping=true;anim.SetBool("Fade",true);yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));BackToMainMenu();ShowRoguePause();yield return StartCoroutine(ReleaseMenuInput(null));}
    void ConfirmRogueLeave(bool abandon)
    {
        string consequence;
        if(RoguelikeMode.Coop)consequence=PhotonNetwork.isMasterClient?"You leave with a reduced reward. The squad continues with a new host.":"You leave with a reduced reward. The squad continues without you.";
        else if(abandon)consequence="This run ends as a defeat and shows results. Its checkpoint is cleared.";
        else consequence=RogueSaveStore.HasCheckpoint()?"Progress returns to the last chapter checkpoint. Unsaved progress is lost.":"There is no checkpoint. Leaving loses this run's progress.";
        FlatsMenuDialog.Show(abandon?"Abandon run":"Back to main menu",consequence,abandon?"Abandon run":"Leave",()=>StartCoroutine(LeaveRoguePause(abandon)));
    }
    IEnumerator LeaveRoguePause(bool abandon)
    {
        bool solo=RoguelikeMode.Solo;ResumeRoguePause();yield return null;
        if(abandon&&solo){var c=RoguelikeController.Instance;if(c!=null)c.Command(new RogueCommandMessage{kind="died"});}
        else {Time.timeScale=1;Reset(true);}
    }
}
