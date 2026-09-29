using System.Collections;
using Flats.Core.Roguelike;
using InControl;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

// Roguelike Survival co-op room (rule RoguelikeMode.CoopRule only). Classic rooms never enter this file's paths.
//  - Ready: every player owns PhotonPlayer.CustomProperties["RDY"] (bool). The master starts the run when everyone in the
//    room is ready (after a short grace so a late cancel still counts) or when the host forces the start. No auto-start on a
//    full room and no master hand-off.
//  - Loadout & Armory: the meta hub opens from the room (and from the co-op result screen) without leaving the room.
//  - Return to room: on the co-op result screen the host sends RogueReturnToRoom; every client loads the menu scene while
//    staying in the Photon room and lands on the room screen. The master clears the room cache (buffered Sync, DecideMap,
//    LoadMap, VoteMap, StartNow and the run's cached instantiations) before it reopens the room.
public partial class Menu
{
    public const string RoomReadyKey = "RDY";
    const float RoomAllReadyGrace = 3f;

    // Set on the result screen right before the menu scene loads; the next Menu.Start consumes it.
    static bool roomReturnPending;

    RogueMetaHub roomHub;
    RogueRoomPanel roomPanel;
    RogueCoopResultPanel coopResultPanel;
    float roomAllReadySince = -1f;
    bool roomStartSent, roomReturnStarted;
    int menuInputReleaseOperation;
    string rogueResultBaseText;

    // ---------------------------------------------------------------- state
    static int RoomRule()
    {
        object r;
        return PhotonNetwork.room != null && PhotonNetwork.room.CustomProperties != null &&
               PhotonNetwork.room.CustomProperties.TryGetValue("R", out r) && r is int ? (int)r : -1;
    }

    /// <summary>An online Roguelike co-op room (the offline bot room and Classic rules are excluded).</summary>
    static bool RogueRoomActive
    {
        get { return PhotonNetwork.inRoom && !PhotonNetwork.offlineMode && RoomRule() == RoguelikeMode.CoopRule; }
    }

    /// <summary>The co-op run's result screen in an online room.</summary>
    static bool RogueCoopOnline
    {
        get { return RoguelikeMode.Coop && network == 2 && RogueRoomActive; }
    }

    public static bool IsRoomReady(PhotonPlayer player)
    {
        object v;
        return player != null && player.CustomProperties != null && player.CustomProperties.TryGetValue(RoomReadyKey, out v) && v is bool && (bool)v;
    }

    static void SetLocalRoomReady(bool ready)
    {
        if (!PhotonNetwork.inRoom || PhotonNetwork.offlineMode || PhotonNetwork.player == null) return;
        if (PhotonNetwork.player.CustomProperties != null && PhotonNetwork.player.CustomProperties.ContainsKey(RoomReadyKey) && IsRoomReady(PhotonNetwork.player) == ready) return;
        PhotonNetwork.player.SetCustomProperties(new PhotonHashtable { { RoomReadyKey, ready } });
    }

    static int RoomReadyCount(out int total)
    {
        var players = PhotonNetwork.playerList ?? new PhotonPlayer[0];
        total = players.Length;
        int ready = 0;
        foreach (var p in players) if (IsRoomReady(p)) ready++;
        return ready;
    }

    RogueRoomPanel RoomPanel()
    {
        if (roomPanel == null && matchingScreen != null) roomPanel = matchingScreen.GetComponentInChildren<RogueRoomPanel>(true);
        return roomPanel;
    }

    RogueCoopResultPanel CoopResultPanel()
    {
        if (coopResultPanel == null && resultsScreen != null) coopResultPanel = resultsScreen.GetComponentInChildren<RogueCoopResultPanel>(true);
        return coopResultPanel;
    }

    static string RoomText(string key) { return FlatsLocalization.Translate(key); }

    // ---------------------------------------------------------------- room screen
    /// <summary>Room labels, the local ready button, player badges and the optional authored panel. Safe to call any time.</summary>
    internal void RefreshRogueRoom()
    {
        var panel = RoomPanel();
        bool active = RogueRoomActive && current == "Matching" && !readyStarted && gameState != "Multiplayer";
        if (panel != null && panel.gameObject.activeSelf != active) panel.gameObject.SetActive(active);
        RefreshRoomCards();
        if (!active) { roomAllReadySince = -1f; return; }
        int total, ready = RoomReadyCount(out total);
        bool mine = IsRoomReady(PhotonNetwork.player), host = PhotonNetwork.isMasterClient;
        bool everyone = total > 0 && ready == total;
        if (startNow != null)
        {
            startNow.transform.GetChild(0).GetComponent<Text>().text = mine ? "Ready · press to cancel" : "Not ready · press when ready";
            startNow.interactable = true;
        }
        roomTexts[4].text = everyone ? string.Format("Everyone is ready ({0}/{1}) · starting...", ready, total)
            : host ? string.Format("Ready {0}/{1} · you can start at any time", ready, total)
            : string.Format("Ready {0}/{1} · the run starts when everyone is ready", ready, total);
        if (panel != null)
        {
            RogueMetaUI.Bind(panel.armory, "Loadout & Armory", OpenRoomHub, roomHub == null);
            if (panel.hostStart != null)
            {
                panel.hostStart.gameObject.SetActive(host);
                RogueMetaUI.Bind(panel.hostStart, "", ConfirmHostStart, host && !roomStartSent);
                var label = panel.hostStartLabel != null ? panel.hostStartLabel : panel.hostStart.GetComponentInChildren<Text>(true);
                if (label != null) label.text = RoomText(string.Format("Start now ({0}/{1} ready)", ready, total));
            }
            if (panel.status != null)
                panel.status.text = RoomText(host ? "You are the host: start now, or wait until everyone is ready." : "The host can start before everyone is ready.");
            if (panel.loadout != null) panel.loadout.text = LoadoutSummary();
        }
    }

    /// <summary>Level and armory weapons of the active preset, already translated (the run uses this preset).</summary>
    static string LoadoutSummary()
    {
        var profile = RogueMetaStore.Current;
        var preset = profile != null ? profile.Active : null;
        if (preset == null) return "";
        var parts = new System.Collections.Generic.List<string> { RogueMetaUI.L(MetaText.Level(profile.Level)) };
        foreach (var id in new[] { preset.primary, preset.secondary, preset.melee })
            if (!string.IsNullOrEmpty(id)) parts.Add(RogueMetaUI.T(MetaProfiles.ArmoryName(id)));
        return string.Join("  ·  ", parts.ToArray());
    }

    /// <summary>Player cards: an authored "ReadyBadge"/"HostBadge" child when present, otherwise a second name line.</summary>
    void RefreshRoomCards()
    {
        bool rogue = RogueRoomActive && gameState != "Multiplayer";
        if (multiplayerList == null || !rogue) return;   // Classic rooms keep their plain name cards
        foreach (Transform card in multiplayerList)
        {
            var info = card.GetComponent<DetailInformation>();
            PhotonPlayer player = card.gameObject == myButton ? PhotonNetwork.player : info != null ? PhotonPlayer.Find(info.id) : null;
            if (player == null && info != null && PhotonNetwork.player != null && info.id == PhotonNetwork.player.ID) player = PhotonNetwork.player;
            var readyBadge = card.Find("ReadyBadge");
            var hostBadge = card.Find("HostBadge");
            bool ready = rogue && IsRoomReady(player), host = rogue && player != null && player.IsMasterClient;
            if (readyBadge != null) readyBadge.gameObject.SetActive(ready);
            if (hostBadge != null) hostBadge.gameObject.SetActive(host);
            if (card.childCount < 2 || player == null) continue;
            var nameLabel = card.GetChild(1).GetComponent<Text>();
            if (nameLabel == null) continue;
            string text = player.NickName;
            // No authored badge: the state is a translated second line (the name label itself is never translated).
            if (rogue && readyBadge == null)
                text += "\n" + RoomText(ready ? "Ready" : "Not ready yet") + (host && hostBadge == null ? " · " + RoomText("Host") : "");
            if (nameLabel.text != text) nameLabel.text = text;
        }
    }

    /// <summary>Matching button 10 (the authored Start Now button) in the co-op room: the local player's ready toggle.</summary>
    internal void ToggleRoomReady()
    {
        if (!RogueRoomActive || readyStarted || roomHub != null) return;
        bool mine = IsRoomReady(PhotonNetwork.player);
        var panel = RoomPanel();
        if (mine && PhotonNetwork.isMasterClient && (panel == null || panel.hostStart == null))
        {
            // No authored host-start button: the host's second press offers the forced start.
            int total, ready = RoomReadyCount(out total);
            if (ready < total)
            {
                ShowConfirm("Start the run", string.Format("{0} of {1} players are ready. Start the run now without waiting?", ready, total),
                    ok => { if (ok) SendRoomStart(); else { SetLocalRoomReady(false); RefreshRogueRoom(); } }, "Start now", "Not ready");
                return;
            }
        }
        if (!mine && (panel == null || panel.armory == null))
        {
            // No authored armory button: readying up is where the loadout can still be changed.
            ShowConfirm("Ready for the run?", "The run uses your Roguelike loadout.\nChange it in Loadout & Armory before you ready up.",
                ok => { if (ok) { SetLocalRoomReady(true); RefreshRogueRoom(); } else OpenRoomHub(); }, "Ready", "Loadout & Armory");
            return;
        }
        SetLocalRoomReady(!mine);
        RefreshRogueRoom();
    }

    void ConfirmHostStart()
    {
        if (!RogueRoomActive || !PhotonNetwork.isMasterClient || readyStarted || roomStartSent) return;
        int total, ready = RoomReadyCount(out total);
        if (ready >= total) { SendRoomStart(); return; }
        ShowConfirm("Start the run", string.Format("{0} of {1} players are ready. Start the run now without waiting?", ready, total),
            ok => { if (ok) SendRoomStart(); }, "Start now", "Keep waiting");
    }

    void SendRoomStart()
    {
        if (!RogueRoomActive || !PhotonNetwork.isMasterClient || readyStarted || roomStartSent) return;
        roomStartSent = true;
        Debug.Log("FLATS_ROGUE_ROOM_START players=" + PhotonNetwork.room.PlayerCount);
        // Not buffered: the room closes in Ready() on every client, and a later joiner must not replay a start.
        base.gameObject.GetPhotonView().RPC("RogueRoomStart", PhotonTargets.All);
    }

    [PunRPC]
    private void RogueRoomStart(PhotonMessageInfo info)
    {
        if (info.sender != null && !info.sender.IsMasterClient) return;
        if (!RogueRoomActive || readyStarted) return;
        botCount = 0;
        playerCount = PhotonNetwork.room.PlayerCount;
        StartCoroutine("Ready");
    }

    /// <summary>Master, every frame in the room: everyone ready for the whole grace period starts the run.</summary>
    void TickRogueRoom()
    {
        if (!PhotonNetwork.isMasterClient || current != "Matching" || gameState == "Multiplayer" || readyStarted || roomStartSent || !RogueRoomActive)
        { roomAllReadySince = -1f; return; }
        int total, ready = RoomReadyCount(out total);
        if (total == 0 || ready < total) { roomAllReadySince = -1f; return; }
        if (roomAllReadySince < 0f) { roomAllReadySince = Time.realtimeSinceStartup; return; }
        if (Time.realtimeSinceStartup - roomAllReadySince >= RoomAllReadyGrace) SendRoomStart();
    }

    void OnPhotonPlayerPropertiesChanged(object[] playerAndUpdatedProps)
    {
        var changed = playerAndUpdatedProps != null && playerAndUpdatedProps.Length > 1 ? playerAndUpdatedProps[1] as PhotonHashtable : null;
        if (changed != null && !changed.ContainsKey(RoomReadyKey)) return;
        RefreshRogueRoom();
    }

    void OnMasterClientSwitched(PhotonPlayer newMasterClient)
    {
        // A host who left the returned room before reopening it: the new master finishes the reopening.
        if (PhotonNetwork.isMasterClient && RogueRoomActive && gameState != "Multiplayer" && current == "Matching" && !readyStarted && !PhotonNetwork.room.IsOpen)
            ReopenReturnedRoom();
        RefreshRogueRoom();
        RefreshRogueCoopResult();
    }

    // ---------------------------------------------------------------- Loadout & Armory from the room and the result screen
    internal void OpenRoomHub()
    {
        bool fromRoom = RogueRoomActive && current == "Matching" && !readyStarted;
        bool fromResult = RogueCoopOnline && current == "Result" && !roomReturnStarted;
        if ((!fromRoom && !fromResult) || roomHub != null || fliping) return;
        if (fromRoom) SetLocalRoomReady(false);   // a player editing the loadout is not ready
        RogueMetaHub.RunHowToPlay = RoguelikeHowToPlay;
        var canvas = startNow != null ? startNow.GetComponentInParent<Canvas>() : null;
        fliping = true;   // Menu.Update must not treat the hub's Esc/B as a room or result Back
        roomHub = RogueMetaHub.Open(canvas != null ? canvas.rootCanvas.transform : transform, RogueMetaStore.Copy(RogueMetaStore.Current),
            p => RogueMetaStore.Commit(RogueMetaStore.Copy(p)),
            () =>
            {
                roomHub = null;
                if (this == null) return;
                StartCoroutine(ReleaseMenuInput(() => { RefreshRogueRoom(); RefreshRogueCoopResult(); FocusRogueScreen(); }));
            });
        RefreshRogueRoom();
    }

    /// <summary>Closes a hub opened from the room or the result screen (a run start, a return or a disconnect).</summary>
    void CloseRoomHub()
    {
        if (roomHub == null) return;
        var hub = roomHub; roomHub = null;
        hub.Close();   // restores Menu.current to the screen it opened from
    }

    /// <summary>Keeps the menu's Back from firing on the same Esc/B press (or its key-up) that closed an overlay.</summary>
    IEnumerator ReleaseMenuInput(System.Action then)
    {
        int operation = ++menuInputReleaseOperation;
        fliping = true;
        yield return null;
        float until = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < until && (Input.GetKey(KeyCode.Escape) || InputManager.ActiveDevice.Action2.IsPressed || InputManager.ActiveDevice.CommandIsPressed))
            yield return null;
        yield return null;
        if (operation != menuInputReleaseOperation) yield break;
        fliping = false;
        if (then != null) then();
    }

    void FocusRogueScreen()
    {
        if (Input.GetJoystickNames().Length == 0 || EventSystem.current == null) return;
        GameObject target = null;
        if (current == "Matching")
        {
            var panel = RoomPanel();
            target = panel != null && panel.armory != null && panel.armory.gameObject.activeInHierarchy ? panel.armory.gameObject : startNow != null ? startNow.gameObject : null;
        }
        else if (current == "Result")
        {
            var panel = CoopResultPanel();
            target = panel != null && panel.gameObject.activeInHierarchy ? panel.FirstSelectable() : backButton;
        }
        if (target != null && target.activeInHierarchy) EventSystem.current.SetSelectedGameObject(target);
    }

    // ---------------------------------------------------------------- co-op result screen
    /// <summary>GameOver, co-op online only: host gets Back to room, everyone gets Loadout & Armory and Leave.</summary>
    internal void SetupRogueCoopResult()
    {
        if (!RogueCoopOnline) return;
        rogueResultBaseText = singleplayerResult != null ? singleplayerResult.GetChild(0).GetComponent<Text>().text : null;
        RefreshRogueCoopResult();
        FocusRogueScreen();
    }

    /// <summary>The meta result overlay closed: focus the co-op actions.</summary>
    public void FocusRogueCoopResult()
    {
        if (current == "Result" && RogueCoopOnline) { RefreshRogueCoopResult(); FocusRogueScreen(); }
    }

    internal void RefreshRogueCoopResult()
    {
        var panel = CoopResultPanel();
        bool active = current == "Result" && RogueCoopOnline && !roomReturnStarted;
        if (panel != null && panel.gameObject.activeSelf != active) panel.gameObject.SetActive(active);
        if (!active) return;
        bool host = PhotonNetwork.isMasterClient;
        string status = host ? "Take the squad back to the room for another run, or leave the room."
                             : "Waiting for the host to take the squad back to the room...";
        if (panel != null)
        {
            if (panel.backToRoom != null) { panel.backToRoom.gameObject.SetActive(host); RogueMetaUI.Bind(panel.backToRoom, "Back to room", ReturnSquadToRoom, host); }
            RogueMetaUI.Bind(panel.armory, "Loadout & Armory", OpenRoomHub, roomHub == null);
            RogueMetaUI.Bind(panel.leave, "Leave room", ConfirmLeaveResultRoom);
            if (panel.status != null) panel.status.text = RoomText(status);
        }
        else if (singleplayerResult != null && rogueResultBaseText != null)
        {
            // No authored panel: the next step is a line under the run summary; Back opens the choice.
            // one short line: the summary box is small and best-fits its text
            singleplayerResult.GetChild(0).GetComponent<Text>().text = rogueResultBaseText + "\n" +
                RoomText(host ? "Back: return the squad to the room, or leave." : "Waiting for the host · Back: leave the room.");
        }
    }

    /// <summary>Back (Esc/B or the back button) on the co-op result screen: a choice instead of leaving at once.</summary>
    internal void ShowRogueCoopResultChoice()
    {
        if (roomReturnStarted) return;
        if (PhotonNetwork.isMasterClient)
            // the cancel side (Esc/B) only opens the leave confirmation, so two quick presses never leave the room
            ShowConfirm("Run over", "Take the squad back to the room for another run?",
                ok => { if (ok) ReturnSquadToRoom(); else ConfirmLeaveResultRoom(); }, "Back to room", "Leave room...");
        else
            ShowConfirm("Waiting for the host", "The host decides when the squad returns to the room.\nLeave the room now?",
                ok => { if (ok) LeaveResultRoom(); }, "Leave room", "Keep waiting");
    }

    void ConfirmLeaveResultRoom()
    {
        ShowConfirm("Leave room", "Leave the room and return to the main menu?", ok => { if (ok) LeaveResultRoom(); }, "Leave room", "Cancel");
    }

    void LeaveResultRoom()
    {
        if (roomReturnStarted || current != "Result" || fliping) return;
        CloseRoomHub();
        StartCoroutine(ResultBackToMenu());
    }

    void ReturnSquadToRoom()
    {
        if (!RogueCoopOnline || !PhotonNetwork.isMasterClient || roomReturnStarted) return;
        Debug.Log("FLATS_ROGUE_RETURN_TO_ROOM players=" + PhotonNetwork.room.PlayerCount);
        // Not buffered: only the players in the room now return; nobody can join a closed room meanwhile.
        base.gameObject.GetPhotonView().RPC("RogueReturnToRoom", PhotonTargets.All);
    }

    [PunRPC]
    private void RogueReturnToRoom(PhotonMessageInfo info)
    {
        if (info.sender != null && !info.sender.IsMasterClient) return;
        if (roomReturnStarted || gameState != "Multiplayer" || !RogueRoomActive) return;
        StartCoroutine(ReturnToRoomRoutine());
    }

    IEnumerator ReturnToRoomRoutine()
    {
        roomReturnStarted = true;
        CloseRoomHub();
        if (confirm.activeSelf) OnClickedConfirm();
        RefreshRogueCoopResult();
        backButton.SetActive(false);
        startNowPlayer = 0; startNowPressed = false;
        // A slow client may still be in the Game Over delay: the scene loads from the result page (see BackgroundColor FadeIn).
        float until = Time.realtimeSinceStartup + 8f;
        while (current != "Result" && Time.realtimeSinceStartup < until) yield return null;
        CloseRoomHub();
        if (confirm.activeSelf) OnClickedConfirm();
        current = "Result";
        Time.timeScale = 1f;   // after Game Over paused the game: the fade below runs on scaled time
        roomReturnPending = true;
        if (adForWin != null && adForWin.Visible) adForWin.Visible = false;
        yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
        backButton.SetActive(false);
        anim.SetBool("Fade", false);
        resultsScreen.gameObject.SetActive(false);
        StartCoroutine("BackgroundColor", "FadeIn");   // loads the menu scene without disconnecting
    }

    // ---------------------------------------------------------------- arrival in the menu scene
    /// <summary>Menu.Start, menu scene, still in the room after a co-op run: reset the pre-match state and show the room.</summary>
    void ConsumeRoomReturn()
    {
        if (!roomReturnPending) return;
        roomReturnPending = false;
        if (!PhotonNetwork.inRoom || !RogueRoomActive) return;
        network = 0; waitBackground = false; wasInRoom = true; botCount = 0;
        startNowPlayer = 0; startNowPressed = false;
        Multiplayer.end = false;
        RoguelikeMode.PendingResume = null;
        RoguelikeMode.Reset();
        Time.timeScale = 1f; canOpen = true;
        playerCount = PhotonNetwork.room.MaxPlayers;
        SetLocalRoomReady(false);
        if (PhotonNetwork.isMasterClient) ReopenReturnedRoom();
        Debug.Log("FLATS_ROGUE_ROOM_RETURNED master=" + PhotonNetwork.isMasterClient + " players=" + PhotonNetwork.room.PlayerCount);
        StartCoroutine(EnterReturnedRoom());
    }

    /// <summary>Master only: no replay of the finished run for new joiners, then the room accepts players again.</summary>
    void ReopenReturnedRoom()
    {
        if (!PhotonNetwork.isMasterClient || !RogueRoomActive || readyStarted) return;
        // The whole room cache: buffered RPCs (Sync, DecideMap, LoadMap, VoteMap, StartNow, Log) and cached instantiations
        // (players, scene enemies, dropped weapons). Nobody can be joining yet: the room is still closed.
        PhotonNetwork.networkingPeer.OpRemoveCompleteCache();
        foreach (var p in PhotonNetwork.playerList) PhotonNetwork.RemoveRPCs(p);
        PhotonNetwork.room.IsOpen = true;
        PhotonNetwork.room.IsVisible = PhotonNetwork.room.Name != null && PhotonNetwork.room.Name.StartsWith("pub-");   // Open Match rooms only
    }

    IEnumerator EnterReturnedRoom()
    {
        // wait until the skipped title has opened the main page, exactly as a player tapping Multiplayer would
        float until = Time.realtimeSinceStartup + 5f;
        while (Time.realtimeSinceStartup < until && (!mainMenuInitialized || (backgroundRenderer != null && backgroundRenderer.sharedMaterial.color.a > 0.21f)))
            yield return null;
        yield return null;
        while (fliping && Time.realtimeSinceStartup < until + 3f) yield return null;
        if (!PhotonNetwork.inRoom || gameState != "Main" || current != "Main") yield break;
        fliping = true;
        anim.SetBool("Fade", true);
        yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
        if (!PhotonNetwork.inRoom || current != "Main") { anim.SetBool("Fade", false); fliping = false; yield break; }
        quitButton.SetActive(false);
        anim.SetBool("Matching", true);
        anim.SetBool("Detail", false);
        backButton.SetActive(true);
        anim.SetTrigger("SkipToMatching");
        current = "Matching";
        stayRoom.isOn = false;
        stayRoom.interactable = true;
        startNow.interactable = true;
        fliping = false;
        // became the master while arriving (the old host left before reopening): finish the reopening here
        if (PhotonNetwork.isMasterClient && !PhotonNetwork.room.IsOpen) ReopenReturnedRoom();
        RefreshRogueRoom();
        FocusRogueScreen();
    }

    /// <summary>Leaving the room or losing the connection: nothing of the co-op room state survives.</summary>
    void ResetRogueRoomState()
    {
        CloseRoomHub();
        roomStartSent = false;
        roomAllReadySince = -1f;
        roomReturnPending = false;
        var panel = RoomPanel();
        if (panel != null) panel.gameObject.SetActive(false);
    }
}
