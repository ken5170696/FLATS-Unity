using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using Flats.Modules;

public partial class Menu
{
    bool enteringLegacyMode;
    Vector2[] originalTilePositions;
    internal bool HandleModNavigation(int button)
    {
        if(gameState!="Main" || enteringLegacyMode)return false;
        if(current=="Modules") { if(button==-1)GetComponentInParent<ModulePageBinding>().page.Close();return true; }
        if(current=="Main" && (button==0 || button==1))
        {
            if(fliping)return true;
            if(button==0)StartCoroutine(ShowPlay());
            else { PlayMenuSound(pressSE);GetComponentInParent<ModulePageBinding>().page.Open(); }
            return true;
        }
        if(current!="Play")return false;
        if(fliping)return true;
        if(button==-1)StartCoroutine(LeavePlay(-1));
        else if(button==0 || button==1)StartCoroutine(LeavePlay(button==0?1:0));
        return true;
    }
    IEnumerator ShowPlay()
    {
        fliping=true;PlayMenuSound(pressSE);anim.SetBool("Fade",true);
        yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
        current="Play";quitButton.SetActive(false);backButton.SetActive(true);anim.SetBool("Fade",false);fliping=false;
        RefreshPlayTiles();EventSystem.current.SetSelectedGameObject(buttons[0].transform.parent.gameObject);
    }
    IEnumerator LeavePlay(int legacyButton)
    {
        fliping=true;PlayMenuSound(legacyButton<0?cancelSE:pressSE);anim.SetBool("Fade",true);
        yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
        RestorePlayTiles();BackToMainMenu();backButton.SetActive(false);fliping=false;
        if(legacyButton<0) { quitButton.SetActive(true);EventSystem.current.SetSelectedGameObject(buttons[0].transform.parent.gameObject);yield break; }
        enteringLegacyMode=true;Fade(legacyButton);enteringLegacyMode=false;
    }
    internal void RefreshPlayTiles()
    {
        if(originalTilePositions==null)originalTilePositions=System.Array.ConvertAll(buttons,b=>((RectTransform)b.transform.parent).anchoredPosition);
        bt[0].text="Singleplayer";bt[1].text="Multiplayer";buttons[0].sprite=images[1];buttons[1].sprite=images[0];
        for(int i=0;i<buttons.Length;i++)
        {
            if(i>1)buttons[i].transform.parent.gameObject.SetActive(false);
            else ((RectTransform)buttons[i].transform.parent).anchoredPosition=new Vector2(i==0?-110:110,0);
        }
    }
    void RestorePlayTiles()
    {
        if(originalTilePositions==null)return;
        for(int i=0;i<buttons.Length;i++) { ((RectTransform)buttons[i].transform.parent).anchoredPosition=originalTilePositions[i];buttons[i].transform.parent.gameObject.SetActive(true); }
    }
    internal void RefreshMainModLabels()
    {
        if(gameState!="Main")return;
        if(current=="Main") { bt[0].text=waitBackground?"Play / Matchmaking":"Play";bt[1].text="Mod"; }
        if(current=="Play")RefreshPlayTiles();
    }
    void PublishRoomModules(ExitGames.Client.Photon.Hashtable properties)
    {
        properties[SessionModules.Property]=BuiltinModules.Instance.Center.Agreement();
    }
    static string pendingModuleRejection;
    readonly RoomModuleCoordinator roomModules=new RoomModuleCoordinator();
    bool CheckPeerModules(PhotonPlayer player)
    {
        return roomModules.AdmitPeer(PhotonNetwork.room.CustomProperties["R"],
            PhotonNetwork.room.CustomProperties[SessionModules.Property],player.CustomProperties[SessionModules.Property],()=>
            { PhotonNetwork.CloseConnection(player);Debug.LogWarning("MOD_SESSION_PEER_REJECTED id="+player.ID); });
    }
    void OnPhotonCustomRoomPropertiesChanged(ExitGames.Client.Photon.Hashtable changed)
    {
        if(PhotonNetwork.inRoom && changed.ContainsKey(SessionModules.Property))CheckRoomModules();
    }
    bool CheckRoomModules()
    {
        var center=BuiltinModules.Instance.Center;
        return roomModules.AdmitLocal(PhotonNetwork.room.CustomProperties["R"],
            PhotonNetwork.room.CustomProperties[SessionModules.Property],center.Ready,center.Agreement,RejectRoomModules);
    }
    void RejectRoomModules(string error)
    {
        if(gameState=="Multiplayer" && UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex!=0)
        {
            // Use the normal scene-exit cleanup. A room rejection during a round
            // must not strand a multiplayer menu over the still-loaded game scene.
            pendingModuleRejection=error;Reset(true);return;
        }
        PhotonNetwork.LeaveRoom();pleaseWait.SetActive(false);pendingRoomDeadline=0;fliping=false;anim.SetBool("Fade",false);anim.SetBool("Matching",false);
        current="Multiplayer";backButton.SetActive(true);
        ShowModuleRejection(error);
    }
    void ShowModuleRejection(string error)
    {
        ShowConfirm("Room modules differ",error+"\nUse the official Mod service to install or change these modules, then restart FLATS. No files were downloaded.",open=>
        {
            if(open && gameState=="Main")StartCoroutine(OpenModsAfterRoomExit());
        },"Open Mod","Back");
    }
    IEnumerator ShowPendingModuleRejection()
    {
        if(string.IsNullOrEmpty(pendingModuleRejection)||UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex!=0)yield break;
        yield return null; // Let the new menu finish its normal Start setup.
        while(current!="Main"||fliping||PhotonNetwork.inRoom)yield return null;
        var error=pendingModuleRejection;pendingModuleRejection=null;
        ShowModuleRejection(error);
    }
    IEnumerator OpenModsAfterRoomExit()
    {
        while(PhotonNetwork.inRoom)yield return null;
        if(currentDetail!=null){currentDetail.SetActive(false);currentDetail=null;}
        anim.SetBool("Detail",false);SetRoomCreationVisible(false);BackToMainMenu();backButton.SetActive(false);
        GetComponentInParent<ModulePageBinding>().page.Open();
    }
}
