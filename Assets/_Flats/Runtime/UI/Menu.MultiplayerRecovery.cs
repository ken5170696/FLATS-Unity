using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public partial class Menu
{
    private void SetRoomCreationVisible(bool visible)
    {
        roomCreation.SetActive(visible);
        anim.SetBool("RoomCreation",visible);
    }
    private string multiplayerFailure;
    private bool multiplayerConnecting;
    private bool multiplayerReady;
    private int multiplayerOperation;
    private float pendingRoomDeadline;

    private void CheckMultiplayerDeadline()
    {
        // Migrated animation clips may retain the old active flag after a reverse transition.
        // Keep the modal's actual visibility consistent with the original Animator parameter.
        if(roomCreation!=null && roomCreation.activeSelf!=anim.GetBool("RoomCreation"))
            roomCreation.SetActive(anim.GetBool("RoomCreation"));
        if (pendingRoomDeadline > 0 && Time.realtimeSinceStartup >= pendingRoomDeadline)
        {
            pendingRoomDeadline = 0; PhotonNetwork.Disconnect(); pleaseWait.SetActive(false);
            anim.SetBool("Matching",false); SetRoomCreationVisible(false); anim.SetBool("Fade",false);
            anim.SetBool("Detail",currentDetail!=null);
            if(currentDetail!=null)currentDetail.SetActive(true);
            current="Multiplayer";fliping=false;backButton.SetActive(true);
            ShowConfirm("Multiplayer request timed out", "The server did not complete the room request within 25 seconds. Please retry.", null,"OK",null);
        }
    }

    private IEnumerator EnsureMultiplayerConnection()
    {
        while (!Flats.Modules.BuiltinModules.Instance.Center.Ready) yield return null;
        PhotonNetwork.SetPlayerCustomProperties(new ExitGames.Client.Photon.Hashtable { { Flats.Modules.SessionModules.Property, Flats.Modules.BuiltinModules.Instance.Center.Agreement() } });
        multiplayerReady = false;
        if (multiplayerConnecting) yield break;
        if (PhotonNetwork.connectedAndReady && !PhotonNetwork.offlineMode && !PhotonNetwork.inRoom)
        { multiplayerReady = true; yield break; }
        multiplayerConnecting = true;
        int operation = multiplayerOperation;
        multiplayerFailure = null;
        pleaseWait.SetActive(true);
        backButton.SetActive(true);
        float deadline = Time.realtimeSinceStartup + 20f;
        if (PhotonNetwork.inRoom) PhotonNetwork.LeaveRoom();
        while (PhotonNetwork.inRoom && Time.realtimeSinceStartup < deadline) yield return null;
        if (PhotonNetwork.offlineMode) PhotonNetwork.offlineMode = false;
        if (!PhotonNetwork.connected) Connect();
        while (!PhotonNetwork.connectedAndReady && multiplayerFailure == null && Time.realtimeSinceStartup < deadline && operation == multiplayerOperation)
            yield return null;
        if (operation != multiplayerOperation)
        { multiplayerConnecting = false; multiplayerReady = false; pleaseWait.SetActive(false); yield break; }
        multiplayerReady = PhotonNetwork.connectedAndReady && !PhotonNetwork.offlineMode && !PhotonNetwork.inRoom && multiplayerFailure == null;
        multiplayerConnecting = false;
        pleaseWait.SetActive(false);
        if (!multiplayerReady)
        {
            if (multiplayerFailure == null) multiplayerFailure = "Photon connection timed out after 20 seconds. Please retry.";
            PhotonNetwork.Disconnect();
            ShowConfirm("Multiplayer connection", multiplayerFailure, null, "OK", null);
            Debug.LogWarning("FLATS_MULTIPLAYER_CONNECTION_FAILED " + multiplayerFailure);
        }
    }

    private void OnConnectionFail(DisconnectCause cause)
    {
        multiplayerFailure = "Photon connection lost: " + cause;
        pleaseWait.SetActive(false);
        fliping = false;
        anim.SetBool("Fade", false);
    }

    private void RefreshRegionLabel()
    {
        if(currentDetail!=null && currentDetail.name=="OnlineVersion")
        {
            foreach(var text in currentDetail.GetComponentsInChildren<Text>(true))
                if(text.text.StartsWith("Online Version:"))text.text="Online Version:"+version.Substring(0,3);
        }
        if (currentDetail == null || currentDetail.name != "ServerRegion") return;
        foreach (Transform t in currentDetail.GetComponentsInChildren<Transform>(true))
            if (t.name == "Region") t.GetChild(1).GetComponent<Text>().text=PhotonNetwork.PhotonServerSettings.PreferredRegion.ToString().ToUpper();
    }
}
