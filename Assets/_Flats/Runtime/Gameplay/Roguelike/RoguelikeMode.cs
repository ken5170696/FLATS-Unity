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

    /// <summary>Heat chosen in the meta hub (solo) or carried by the room (co-op); 0..RogueHeat.MaxHeat.</summary>
    public static int Heat;

    /// <summary>Map chosen in the solo menu; -1 = random.</summary>
    public static int SoloMap = -1;

    /// <summary>A checkpoint to resume instead of starting a new run. Consumed by the controller on scene start.</summary>
    public static RunSaveDocument PendingResume;

    /// <summary>Set while the controller drives a run; cleared on every exit path.</summary>
    public static bool RunInProgress;

    // ---------------------------------------------------------------- shooting range
    /// <summary>Solo: set by the menu right before the range's scene loads. Like PendingResume it survives Reset (the game scene's
    /// Singleplayer.Awake calls it before the run controller starts): the controller consumes it when it creates the state, and the
    /// menu clears it on entering the Roguelike page and on every ordinary launch.</summary>
    public static bool SoloRange;
    /// <summary>Co-op: room property (bool) the host sets in the squad room; every client reads the same value.</summary>
    public const string RangeRoomKey = "RG";
    /// <summary>The room's next (or current) start is the shooting range.</summary>
    public static bool RoomRange
    {
        get
        {
            object v;
            return PhotonNetwork.room != null && PhotonNetwork.room.CustomProperties != null &&
                   PhotonNetwork.room.CustomProperties.TryGetValue(RangeRoomKey, out v) && v is bool && (bool)v;
        }
    }
    /// <summary>The scene that is starting is the shooting range. The run controller copies it into RunState.range when it creates
    /// the state; from then on the replicated state is what every rule reads.</summary>
    public static bool Range { get { return Solo ? SoloRange : Coop && RoomRange; } }

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
            return KeyOf(PhotonNetwork.player);
        }
    }

    /// <summary>The run's identity for a Photon player; every client must compute the same value for the same player.
    /// UserId is not usable: FLATS rooms do not publish it, so a client sees its own UserId (the nickname by default) while
    /// everyone else sees an empty one. Nickname plus actor number is known identically on every client.</summary>
    public static string KeyOf(PhotonPlayer player)
    {
        if (player == null) return "local";
        return player.NickName + "#" + player.ID;
    }

    /// <summary>Called with Singleplayer.ResetSharedMatchState and on every exit path: nothing of the mode survives a scene change by accident.</summary>
    /// <summary>Scenes present in this build (index below the count); the menu and the route sampler skip the rest.</summary>
    public static bool SceneAvailable(int buildIndex) { return buildIndex >= 0 && buildIndex < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; }
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    static void InstallMapFilter() { Flats.Core.Roguelike.RogueCatalog.MapAvailable = m => SceneAvailable(m.BuildIndex); }

    public static void Reset()
    {
        // PendingResume is intentionally kept: chapter travel sets it right before the scene load that calls this.
        // The controller consumes it on start and the menu clears it on entry.
        RunInProgress = false;
        RoguePlayer.ResetLocalStatics();
        RogueScreenView.Suspended = false;
    }
}
