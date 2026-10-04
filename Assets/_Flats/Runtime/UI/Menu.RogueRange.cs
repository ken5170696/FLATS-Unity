using System.Collections;
using Flats.Core.Roguelike;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

// The Roguelike shooting range from the menu's side.
//  - Solo: headquarters shows a "Shooting range" tile (an authored prefab laid over the hub: RogueRangeEntryTile). It closes the
//    hub and launches the range on the map chosen in headquarters. No checkpoint is read, asked about, rewarded or cleared.
//  - Co-op: the squad room shows the mode of the next start (RogueRangeRoomTile); the host switches between an ordinary run and the
//    shooting range (room property RoguelikeMode.RangeRoomKey, the same on every client). The start itself is the room's own.
//  - Leaving: from the pause menu or the range menu. Solo returns to headquarters, the host takes the squad back to the room, a
//    client leaves the squad. Nothing of the range is written anywhere.
public partial class Menu
{
    FlatsRangeEntry rangeEntry;
    FlatsRangeRoomTile rangeRoomTile;

    // ---------------------------------------------------------------- solo: headquarters
    /// <summary>OpenRogueHeadquarters: the shooting-range entrance over the hub's home page.</summary>
    void AttachRangeEntry(RogueMetaHub hub)
    {
        if (hub == null) return;
        if (rangeEntry != null) Destroy(rangeEntry.gameObject);
        rangeEntry = FlatsRangeEntry.Attach(hub, () =>
        {
            if (hub == null || gameState != "Main") return;
            hub.Close();   // gives the singleplayer page back, exactly as Back does
            StartCoroutine(RangeFromHeadquarters());
        });
    }

    IEnumerator RangeFromHeadquarters()
    {
        int operation = ++menuInputReleaseOperation;   // the hub's own release must not hand the focus back to the page meanwhile
        fliping = true;
        yield return null;
        float until = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < until && ConfirmInputHeld()) yield return null;
        yield return null;
        if (operation != menuInputReleaseOperation) yield break;
        fliping = false;
        if (gameState != "Main" || current != "Singleplayer") yield break;
        yield return StartCoroutine(LaunchRogueRange());
    }

    /// <summary>The solo launch of the shooting range: LaunchRoguelike's fade, title card and scene load, with no checkpoint and
    /// RoguelikeMode.SoloRange set for the run controller of the scene that loads.</summary>
    IEnumerator LaunchRogueRange()
    {
        fliping = true;
        backButton.SetActive(false);
        if (!waitBackground && PhotonNetwork.connected) PhotonNetwork.Disconnect();
        anim.SetBool("Fade", true);
        yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
        HideModeTiles();
        StartCoroutine("BackgroundColor", "FadeIn");
        int buildIndex = roguelikeMap < 0 ? RandomAvailableStageIndex() : roguelikeMap + 2;
        if (!RoguelikeMode.SceneAvailable(buildIndex)) buildIndex = RandomAvailableStageIndex();
        int map = buildIndex - 2;
        string mapName = stageName != null && stageName.ContainsKey(map) ? FlatsLocalization.Translate(stageName[map]) : "";
        RogueRunTitleCard.Show(FlatsLocalization.Translate("SHOOTING RANGE"), mapName == "" ? FlatsLocalization.Translate("Nothing here is saved.") : mapName + "  ·  " + FlatsLocalization.Translate("Nothing here is saved."));
        yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(2f));
        RoguelikeMode.Difficulty = roguelikeDifficulty;
        RoguelikeMode.Heat = 0;
        Singleplayer.rule = RoguelikeMode.SoloRule;
        network = 0;
        Singleplayer.ResetSharedMatchState();
        RoguelikeMode.PendingResume = null;
        RoguelikeMode.SoloRange = true;   // consumed by the run controller when it creates the state
        RoguelikeMode.SoloMap = map;
        gameState = "Singleplayer";
        Debug.Log("FLATS_ROGUE_RANGE_LAUNCH map=" + buildIndex);
        LoadOfflineScene(buildIndex);
        fliping = false;
    }

    // ---------------------------------------------------------------- co-op: the squad room
    /// <summary>Every frame in the squad room: the mode tile exists while the room view does.</summary>
    void TickRangeRoom()
    {
        if (tileRoom == null) { if (rangeRoomTile != null) { Destroy(rangeRoomTile.gameObject); rangeRoomTile = null; } return; }
        if (rangeRoomTile == null) rangeRoomTile = FlatsRangeRoomTile.Attach(tileRoom, ToggleRoomRange);
        if (rangeRoomTile != null) rangeRoomTile.Present(RoguelikeMode.RoomRange, PhotonNetwork.isMasterClient, readyStarted);
    }

    /// <summary>Host only: the room's next start is the shooting range, or an ordinary run again.</summary>
    void ToggleRoomRange()
    {
        if (!RogueRoomActive || !PhotonNetwork.isMasterClient || readyStarted || roomStartSent || FlatsMenuDialog.BlocksMenuInput) return;
        bool next = !RoguelikeMode.RoomRange;
        PhotonNetwork.room.SetCustomProperties(new PhotonHashtable { { RoguelikeMode.RangeRoomKey, next } });
        Debug.Log("FLATS_ROGUE_ROOM_RANGE " + next);
    }

    // ---------------------------------------------------------------- leaving
    /// <summary>The pause menu's two leave tiles and the range menu's own, in the shooting range. toMenu: straight to the main menu.</summary>
    public void ConfirmRangeLeave(bool toMenu = false)
    {
        bool coop = RoguelikeMode.Coop, host = PhotonNetwork.isMasterClient;
        string heading = toMenu ? "Back to main menu" : "Leave the range", button, consequence;
        if (coop && !host) { button = "Leave the squad"; consequence = "You leave the squad and go back to the main menu. Nothing from the range is kept."; toMenu = true; }
        else if (coop && toMenu) { button = "Leave"; consequence = "You leave the squad and go back to the main menu. The squad goes on with a new host."; }
        else if (coop) { button = "Squad back to the room"; consequence = "Takes the whole squad back to the room. Nothing from the range is kept."; }
        else if (toMenu) { button = "Leave"; consequence = "Back to the main menu. Nothing from the range is kept; your saved run is untouched."; }
        else { button = "Back to headquarters"; consequence = "Back to headquarters. Nothing from the range is kept; your saved run is untouched."; }
        bool menuOnly = toMenu;
        FlatsMenuDialog.Show(heading, consequence, button, () => StartCoroutine(LeaveRange(menuOnly)));
    }

    IEnumerator LeaveRange(bool toMenu)
    {
        if (pauseNavigation.IsOpen) { ResumeRoguePause(); yield return null; }
        var controller = RoguelikeController.Instance;
        bool coop = RoguelikeMode.Coop;
        if (coop && !toMenu && PhotonNetwork.isMasterClient)
        {
            // every client ends its range when the return reaches it (ReturnToRoomRoutine)
            Debug.Log("FLATS_ROGUE_RANGE_RETURN_TO_ROOM players=" + PhotonNetwork.room.PlayerCount);
            PhotonNetwork.room.IsOpen = false;
            SetRunMapProperty(0);
            base.gameObject.GetPhotonView().RPC("RogueReturnToRoom", PhotonTargets.All);
            yield break;
        }
        if (controller != null && controller.RangeActive) controller.RangeLeaving();
        Time.timeScale = 1f;
        if (!coop && !toMenu)
        {
            // the menu scene opens headquarters again, as after a run's statistics
            rogueHeadquartersPending = true;
            rogueHeadquartersRequestedAt = Time.realtimeSinceStartup;
        }
        Debug.Log("FLATS_ROGUE_RANGE_LEFT coop=" + coop + " toMenu=" + toMenu);
        Reset(true);
    }

    /// <summary>ReturnToRoomRoutine: a shooting range has no result page to wait for; it ends here on every client.</summary>
    bool EndRangeForRoomReturn()
    {
        var controller = RoguelikeController.Instance;
        if (controller == null || !controller.RangeActive) return false;
        controller.RangeLeaving();
        if (pauseNavigation.IsOpen) CloseMenu();
        return true;
    }
}
