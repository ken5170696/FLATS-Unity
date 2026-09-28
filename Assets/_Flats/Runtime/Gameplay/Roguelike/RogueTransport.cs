using System;
using UnityEngine;

/// <summary>
/// Authority/client message plumbing for the run. Offline runs call straight through; Photon
/// runs use the three RPCs on RoguelikeController's scene PhotonView. No AllBuffered: late
/// joiners receive a fresh snapshot from the authority instead of replaying history.
/// </summary>
public interface IRogueTransport
{
    bool IsAuthority { get; }
    /// <summary>Authority -> everyone else (or one player when targetId >= 0).</summary>
    void SendSnapshot(string json, int targetId);
    /// <summary>Authority -> everyone else: small ordered event.</summary>
    void SendEvent(int seq, string json);
    /// <summary>Client -> authority.</summary>
    void SendCommand(string json);
}

public sealed class OfflineRogueTransport : IRogueTransport
{
    readonly RoguelikeController controller;
    public OfflineRogueTransport(RoguelikeController controller) { this.controller = controller; }
    public bool IsAuthority { get { return true; } }
    public void SendSnapshot(string json, int targetId) { }
    public void SendEvent(int seq, string json) { }
    public void SendCommand(string json) { controller.ReceiveCommand(json, -1); }
}

public sealed class PhotonRogueTransport : IRogueTransport
{
    readonly PhotonView view;
    public PhotonRogueTransport(PhotonView view) { this.view = view; }
    public bool IsAuthority { get { return PhotonNetwork.isMasterClient; } }

    public void SendSnapshot(string json, int targetId)
    {
        if (!PhotonNetwork.inRoom) return;
        if (targetId >= 0)
        {
            var player = PhotonPlayer.Find(targetId);
            if (player != null) view.RPC("RogueSnapshot", player, json);
        }
        else view.RPC("RogueSnapshot", PhotonTargets.Others, json);
    }

    public void SendEvent(int seq, string json)
    {
        if (PhotonNetwork.inRoom) view.RPC("RogueEvent", PhotonTargets.Others, seq, json);
    }

    public void SendCommand(string json)
    {
        if (PhotonNetwork.inRoom) view.RPC("RogueCommand", PhotonTargets.MasterClient, json);
    }
}

/// <summary>Command envelope from a client to the authority. Flat for JsonUtility.</summary>
[Serializable]
public sealed class RogueCommandMessage
{
    public string kind = "";          // ready, buy, route, continue, evacuate, ult, downed, died, revive, interact, switch, pickup, drop, delivered, exit
    public string playerKey = "";
    public bool flag;
    public int index = -1;
    public string text = "";
    public double value;
    public Flats.Core.Roguelike.ShopTransaction tx;
}

/// <summary>Small authority -> client notification (popups, banners, effects). State itself travels as snapshots.</summary>
[Serializable]
public sealed class RogueEventMessage
{
    public string kind = "";          // bounty, banner, phase, downed, revived, died, wipe, ult, objective, event, emergency, log
    public string playerKey = "";
    public string text = "";
    public long minor;
    public bool flag;
    public int index;
    public double value;
}
