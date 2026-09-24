using System;
using System.Collections;
using Flats.Core;
using UnityEngine;
using UnityEngine.UI;

public partial class Menu
{
    [SerializeField] GameObject localMatchPanel;
    [SerializeField] Text localMatchStatus;
    [SerializeField] Text localRoomsEmpty;
    [SerializeField] InputField localRoomCode;
    [SerializeField] Button[] localRoomButtons;
    [SerializeField] Button localJoin, localHost, localRefresh, localClose;
    bool localMatchBound;
    FlatsLanDiscovery localDiscovery;
    string localRequestedRoom, localHostedRoom, localFeedback, cancelledLocalRoom;
    bool localRequestPending;
    float localListRefresh;

    string LocalRegion { get { return PhotonNetwork.connectedAndReady || PhotonNetwork.inRoom
        ? PhotonNetwork.CloudRegion.ToString().ToLowerInvariant()
        : PhotonNetwork.PhotonServerSettings.PreferredRegion.ToString().ToLowerInvariant(); } }

    void OpenLocalMatch()
    {
        if (!localMatchBound)
        {
            localDiscovery = gameObject.AddComponent<FlatsLanDiscovery>();
            localJoin.onClick.AddListener(() => { PlayMenuSound(pressSE); RequestLocalRoom(false, localRoomCode.text.Trim().ToLowerInvariant()); });
            localHost.onClick.AddListener(() => { PlayMenuSound(pressSE); RequestLocalRoom(true, "lan-" + Guid.NewGuid().ToString("N").Substring(0,16)); });
            localRefresh.onClick.AddListener(() => { PlayMenuSound(pressSE); if (!localRequestPending && !multiplayerConnecting) { localFeedback = null; localDiscovery.Listen(); } });
            localClose.onClick.AddListener(() => { PlayMenuSound(cancelSE); CloseLocalMatch(); });
            localMatchBound = true;
        }
        localMatchPanel.SetActive(true);
        localMatchPanel.transform.SetAsLastSibling();
        backButton.SetActive(false);
        localFeedback = null;
        localDiscovery.Listen();
        RefreshLocalMatch();
    }

    void CloseLocalMatch()
    {
        if (localRequestPending || multiplayerConnecting)
        {
            ++multiplayerOperation;
            multiplayerReady = false;
            localRequestPending = false;
            pendingRoomDeadline = 0;
            cancelledLocalRoom = localRequestedRoom;
            localRequestedRoom = null;
            localHostedRoom = null;
            PhotonNetwork.Disconnect();
            pleaseWait.SetActive(false);
        }
        if (localDiscovery != null) localDiscovery.Stop();
        if (localMatchPanel != null) localMatchPanel.SetActive(false);
        backButton.SetActive(true);
        fliping = false;
        anim.SetBool("Fade", false);
    }

    void RequestLocalRoom(bool host, string room)
    {
        if (localRequestPending || multiplayerConnecting || fliping) return;
        if (!LanRoomAdvertisement.IsRoomCode(room))
        { localMatchStatus.text = "Enter the host's complete lan- room code."; return; }
        localFeedback = null;
        cancelledLocalRoom = null;
        localRequestPending = true;
        localRequestedRoom = room;
        localHostedRoom = host ? room : null;
        localDiscovery.Stop();
        StartCoroutine(ConnectLocalRoom(host, room));
    }

    IEnumerator ConnectLocalRoom(bool host, string room)
    {
        int operation = multiplayerOperation;
        yield return StartCoroutine(EnsureMultiplayerConnection());
        if (operation != multiplayerOperation || !localRequestPending) yield break;
        if (!multiplayerReady) { LocalRoomFailed("Connection failed. Check the message, region and configuration, then retry."); yield break; }
        pendingRoomDeadline = Time.realtimeSinceStartup + 25f;
        pleaseWait.SetActive(true);
        stayRoom.gameObject.SetActive(true);
        ipButton.SetActive(false);
        bool accepted;
        if (host)
        {
            rule = 1; objective = 1; playerCount = 2; botCount = 0;
            var properties = new ExitGames.Client.Photon.Hashtable { { "R", rule }, { "O", objective } };
            PublishRoomModules(properties);
            accepted = PhotonNetwork.CreateRoom(room, new RoomOptions { MaxPlayers = 2, IsVisible = false,
                CustomRoomProperties = properties, CustomRoomPropertiesForLobby = new[] { "R", "O" } }, null);
        }
        else accepted = PhotonNetwork.JoinRoom(room);
        if (!accepted) LocalRoomFailed("Photon could not send the room request. Refresh and retry.");
    }

    bool LocalRoomFailed(string message)
    {
        if (!localRequestPending) return false;
        cancelledLocalRoom = localRequestedRoom;
        localRequestPending = false; localRequestedRoom = null; localHostedRoom = null;
        pendingRoomDeadline = 0; pleaseWait.SetActive(false); fliping = false;
        if (localDiscovery != null) localDiscovery.Stop();
        localFeedback = message + " No room was joined.";
        if (localMatchStatus != null) localMatchStatus.text = localFeedback;
        backButton.SetActive(true);
        return true;
    }

    bool RejectCancelledLocalRoom()
    {
        if (cancelledLocalRoom == null || PhotonNetwork.room == null || PhotonNetwork.room.Name != cancelledLocalRoom) return false;
        PhotonNetwork.LeaveRoom();
        return true;
    }

    void LocalRoomJoined()
    {
        if (!localRequestPending || PhotonNetwork.room == null || PhotonNetwork.room.Name != localRequestedRoom) return;
        localRequestPending = false;
        localMatchPanel.SetActive(false);
        anim.SetTrigger("SkipToMatching");
        if (localHostedRoom != null)
            localDiscovery.Advertise(LanRoomAdvertisement.Encode(localHostedRoom, LocalRegion, version.Substring(0,3)));
    }

    void TickLocalMatch()
    {
        if (localHostedRoom != null && !localRequestPending)
        {
            if (!PhotonNetwork.inRoom || PhotonNetwork.room.Name != localHostedRoom || current != "Matching" || !PhotonNetwork.isMasterClient || readyStarted || !PhotonNetwork.room.IsOpen)
            { localHostedRoom = null; if (localDiscovery != null) localDiscovery.Stop(); }
            else roomTexts[4].text = "Room: " + localHostedRoom + " / " + LocalRegion.ToUpperInvariant() +
                "\nInternet required. Waiting for second player." + (localDiscovery.Error == null ? "" : "\n" + localDiscovery.Error);
        }
        if (localRequestPending && pendingRoomDeadline > 0 && Time.realtimeSinceStartup >= pendingRoomDeadline)
        { LocalRoomFailed("Room request timed out. Retry or check the host's code."); PhotonNetwork.Disconnect(); }
        if (localMatchPanel != null && localMatchPanel.activeSelf && Time.realtimeSinceStartup >= localListRefresh)
        { localListRefresh = Time.realtimeSinceStartup + .3f; RefreshLocalMatch(); }
    }

    void RefreshLocalMatch()
    {
        if (localFeedback != null && !localRequestPending) localMatchStatus.text = localFeedback;
        else if (!localRequestPending && localDiscovery.Error == null)
            localMatchStatus.text = "Region: " + LocalRegion.ToUpperInvariant() + " / online " + version.Substring(0,3) +
                "\nSelect a room below, or enter the host's code. Discovery expires after 30 seconds.";
        else if (localRequestPending) localMatchStatus.text = "Connecting to " + localRequestedRoom + "... Back cancels.";
        else if (localDiscovery.Error != null) localMatchStatus.text = localDiscovery.Error + " Region: " + LocalRegion.ToUpperInvariant();
        int row = 0;
        foreach (var entry in localDiscovery.Rooms)
        {
            if (row == localRoomButtons.Length) break;
            if (entry.Room.Version != version.Substring(0,3) || entry.Room.Region != LocalRegion) continue;
            var record = entry.Room;
            var button = localRoomButtons[row++];
            button.gameObject.SetActive(true);
            button.interactable = !localRequestPending && !multiplayerConnecting;
            button.GetComponentInChildren<Text>().text = "Join " + record.Room + " / " + record.Region;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                // A stale discovery row must not join after its advertisement expires.
                var fresh = localDiscovery.Rooms.Find(item => item.Room.Room == record.Room && item.Room.Region == record.Region &&
                    Time.realtimeSinceStartup - item.LastSeen <= FlatsLanDiscovery.Lifetime);
                if (fresh != null) RequestLocalRoom(false, record.Room);
            });
        }
        localRoomsEmpty.gameObject.SetActive(row == 0 && !localRequestPending);
        while (row < localRoomButtons.Length) localRoomButtons[row++].gameObject.SetActive(false);
    }
}
