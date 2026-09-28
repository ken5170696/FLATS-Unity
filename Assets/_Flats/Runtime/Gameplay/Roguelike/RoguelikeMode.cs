using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// Mode identity for Roguelike Survival. It only reads the two legacy rule fields; the run's
/// real state lives in RoguelikeController / RunState, never in more static flags.
/// Solo: Singleplayer.rule == SoloRule (gameState "Singleplayer").
/// Co-op: Multiplayer.rule == CoopRule (gameState "Multiplayer"); Multiplayer.Awake mirrors it into Singleplayer.rule.
/// </summary>
public static class RoguelikeMode
{
    public const int SoloRule = 5;
    public const int CoopRule = 9;

    public static bool Solo { get { return Menu.gameState == "Singleplayer" && Singleplayer.rule == SoloRule; } }
    public static bool Coop { get { return Menu.gameState == "Multiplayer" && Multiplayer.rule == CoopRule; } }
    public static bool Active { get { return Solo || Coop; } }

    /// <summary>Difficulty chosen in the menu (solo) or carried by the room objective (co-op). 1 Normal, 2 Hard, 3 Chaos.</summary>
    public static int Difficulty = 1;

    /// <summary>Map chosen in the solo menu; -1 = random.</summary>
    public static int SoloMap = -1;

    /// <summary>A checkpoint to resume instead of starting a new run. Consumed by the controller on scene start.</summary>
    public static RunSaveDocument PendingResume;

    /// <summary>Set while the controller drives a run; cleared on every exit path.</summary>
    public static bool RunInProgress;

    public static readonly string[] DifficultyNames = { "", "Normal", "Hard", "Chaos" };
    public static readonly string[] DifficultyBriefs =
    {
        "",
        "Standard enemies and bounty. Emergencies from stage 3.",
        "Tougher enemies, more elites, +10% bounty.",
        "Elite squads early, frequent emergencies, +20% bounty.",
    };

    public static string LocalPlayerKey
    {
        get
        {
            if (Menu.network != 2 || PhotonNetwork.player == null) return "local";
            if (!string.IsNullOrEmpty(PhotonNetwork.player.UserId)) return PhotonNetwork.player.UserId;
            return PhotonNetwork.player.NickName + "#" + PhotonNetwork.player.ID;
        }
    }

    /// <summary>Called with Singleplayer.ResetSharedMatchState and on every exit path: nothing of the mode survives a scene change by accident.</summary>
    public static void Reset()
    {
        // PendingResume is intentionally kept: chapter travel sets it right before the scene load that calls this.
        // The controller consumes it on start and the menu clears it on entry.
        RunInProgress = false;
        RoguePlayer.ResetLocalStatics();
    }
}
