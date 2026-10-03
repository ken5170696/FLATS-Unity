using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

// Player life cycle repairs (2026-10-04): respawn after a death, the room cache across deaths and chapter travel, a bleed-out
// that races a revive, and actors that leave the map.
public partial class RoguelikeController
{
    // ---------------------------------------------------------------- respawn
    /// <summary>The local player has been seen in this scene. Until then the first spawn belongs to Multiplayer.Start, and
    /// EnsureLocalPlayerAlive must not create a second Flatman (a rejoining player gets the run state before that spawn).</summary>
    bool localSpawnSeen;

    /// <summary>Seconds the controller waits for Multiplayer.Start to spawn the local Flatman before EnsureLocalPlayerAlive may do it.</summary>
    public const float LocalSpawnWaitSeconds = 10f;

    // The loaded scene in which the local Flatman of a co-op roguelike scene was created (by Multiplayer.Start or, after the wait
    // ran out, by EnsureLocalPlayerAlive). A scene handle is new for every load, so nothing has to reset it; whoever comes second
    // sees it and does not create another character.
    static int localSpawnSceneHandle = int.MinValue;

    /// <summary>The local Flatman of the active scene has been created (roguelike co-op only).</summary>
    public static bool LocalSpawnedThisScene
    {
        get { return localSpawnSceneHandle == UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle; }
    }

    /// <summary>Called right after the local Flatman of this scene was created.</summary>
    public static void NoteLocalSpawned()
    {
        localSpawnSceneHandle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        if (Instance != null) Instance.localSpawnSeen = true;
    }

    /// <summary>
    /// Removes what a death left on the local screen before a new Flatman spawns: the spectator camera, and the ragdoll whose
    /// Respawn was still waiting to create one. Respawn.Start removes the ragdoll's timed Destroy and removes the body itself only
    /// in its last line, so destroying the component alone (as before) left the body and its active respawn camera for good.
    /// True when a Respawn was still pending (its "Respawn" fade may be running).
    /// </summary>
    bool ClearDeathView()
    {
        bool pending = false;
        foreach (var cam in FindObjectsOfType<WatchCamera>()) Destroy(cam.gameObject);
        foreach (var respawn in FindObjectsOfType<Respawn>())
        {
            pending = true;
            Destroy(respawn.transform.root.gameObject);   // the ragdoll root: body, respawn camera and its blur
        }
        return pending;
    }

    // ---------------------------------------------------------------- bleed-out against revive (authority)
    // The owner counts the bleed-out; the authority rules revives. Both can happen in the same moment: the revive is ruled, and
    // the owner's "died" (sent before that ruling reached it) arrives afterwards. It used to turn the revived player Dead again
    // while the squad had already spent the ultimate (or the hold). The owner's down request number tells the two apart: a
    // bleed-out of the very down that the authority has since revived is ignored, and the owner respawns as revived.
    readonly Dictionary<string, int> downIndex = new Dictionary<string, int>();     // player key -> request number of the accepted down
    readonly Dictionary<string, int> downRevived = new Dictionary<string, int>();   // player key -> request number of a down that ended in a revive

    void NoteDown(string key, int index) { downIndex[key] = index; downRevived.Remove(key); }

    void NoteRevivedFromDown(string key)
    {
        int index;
        if (downIndex.TryGetValue(key, out index)) downRevived[key] = index;
    }

    bool BleedOutSuperseded(string key, int index)
    {
        int revived;
        var p = state != null ? state.Player(key) : null;
        return index > 0 && p != null && p.life == PlayerLife.Alive && downRevived.TryGetValue(key, out revived) && revived == index;
    }

    // ---------------------------------------------------------------- room cache (co-op)
    // A player's Flatman is a cached instantiation. Nothing removed that cache entry when the player died (every copy destroys
    // its own object locally, DamageReceiver.Stop) or when the squad travelled to the next map (the scene load destroys them).
    // A player who joined or rejoined later received every one of those entries and built a second, third... character for each
    // squad member. Deaths: the owner removes the entry (ForgetCachedPlayer). Travel: the master empties the room cache before
    // the travel RPC, and every client removes what it cached itself right before its scene load.

    /// <summary>Owner's copy of a dying player (every Die): its cached instantiation and its views' buffered RPCs leave the room
    /// cache, exactly what PhotonNetwork.Destroy removes, without the network destroy that would cut the death short.</summary>
    public static void ForgetCachedPlayer(GameObject player)
    {
        if (player == null || Menu.network == 0 || !PhotonNetwork.inRoom || PhotonNetwork.offlineMode) return;
        var views = player.GetComponentsInChildren<PhotonView>(true);
        if (views == null || views.Length == 0 || !views[0].isMine || views[0].instantiationId < 1) return;
        var instantiate = new ExitGames.Client.Photon.Hashtable(); instantiate[(byte)7] = views[0].instantiationId;
        PhotonNetwork.networkingPeer.OpRaiseEvent(202, instantiate, true, new RaiseEventOptions { CachingOption = EventCaching.RemoveFromRoomCache, TargetActors = new[] { views[0].CreatorActorNr } });
        foreach (var view in views)
        {
            if (view == null) continue;
            var rpcs = new ExitGames.Client.Photon.Hashtable(); rpcs[(byte)0] = view.viewID;
            PhotonNetwork.networkingPeer.OpRaiseEvent(200, rpcs, true, new RaiseEventOptions { CachingOption = EventCaching.RemoveFromRoomCache });
        }
    }

    /// <summary>Master, right before the travel RPC: the whole room cache of the map being left (players, dropped weapons,
    /// leftover scene objects, buffered RPCs). Nothing of it is valid in the next scene; the next scene caches its own.</summary>
    void ClearRoomCacheForTravel()
    {
        if (Menu.network == 0 || !PhotonNetwork.inRoom || PhotonNetwork.offlineMode || !PhotonNetwork.isMasterClient) return;
        PhotonNetwork.networkingPeer.OpRemoveCompleteCache();
        enemyCache.Clear();
        Debug.Log("FLATS_ROGUE_TRAVEL_CACHE_CLEARED players=" + (PhotonNetwork.room != null ? PhotonNetwork.room.PlayerCount : 0));
    }

    /// <summary>Every client, right before its travel scene load: whatever this client cached after the master's clear (a
    /// buffered weapon or team RPC during the fade) goes too. Sent before the load, so it is ordered before this client's new
    /// Sync and Flatman in the next scene.</summary>
    void ForgetOwnCacheBeforeTravel()
    {
        if (Menu.network == 0 || !PhotonNetwork.inRoom || PhotonNetwork.offlineMode || PhotonNetwork.player == null) return;
        PhotonNetwork.networkingPeer.OpRemoveCompleteCacheOfPlayer(PhotonNetwork.player.ID);
        PhotonNetwork.SendOutgoingCommands();
    }

    // ---------------------------------------------------------------- out of the map
    /// <summary>Metres below the lowest kill volume (or, without one, below the lowest spawn point) at which an actor counts as
    /// out of the map even though it never touched a kill volume.</summary>
    public const float KillFloorMargin = 10f, KillFloorWithoutVolume = 150f;
    float killFloor = float.NaN;

    /// <summary>World height below which nothing of the map exists (cached per scene; the controller is per scene).</summary>
    public float KillFloorY
    {
        get
        {
            if (!float.IsNaN(killFloor)) return killFloor;
            float lowest = float.MaxValue;
            foreach (var zone in FindObjectsOfType<DeadZone>())
                foreach (var c in zone.GetComponents<Collider>()) lowest = Mathf.Min(lowest, c.bounds.min.y);
            if (lowest < float.MaxValue) lowest -= KillFloorMargin;
            else
            {
                var points = spawnPoints != null ? spawnPoints : (GameObject.Find("SpawnPoints") != null ? GameObject.Find("SpawnPoints").transform : null);
                if (points == null || points.childCount == 0) return -1000f;   // not cached: the scene is not ready yet
                foreach (Transform t in points) lowest = Mathf.Min(lowest, t.position.y);
                lowest -= KillFloorWithoutVolume;
            }
            killFloor = lowest;
            return killFloor;
        }
    }

    /// <summary>Authority: an enemy touched a kill volume or fell below the map. It can never be reached again, so it dies at
    /// once (no shield, Guardian or invulnerability applies): the kill counts for the stage, the objective and the squad's
    /// bounty like any other, and the stage can end.</summary>
    public void EnemyOutOfBounds(RogueEnemyRole role)
    {
        if (role == null || !IsAuthority) return;
        var receiver = role.GetComponent<DamageReceiver>();
        if (receiver == null || receiver.Dead) return;
        Debug.Log("FLATS_ROGUE_ENEMY_OUT_OF_BOUNDS instance=" + role.InstanceId + " role=" + role.RoleId + " y=" + role.transform.position.y.ToString("0.#"));
        receiver.RogueKillOutOfBounds();
    }

    readonly List<RogueEnemyRole> fallenBuffer = new List<RogueEnemyRole>();
    /// <summary>Authority, twice a second (SweepVanishedEnemies): enemies below the map that no kill volume caught.</summary>
    void SweepFallenEnemies()
    {
        if (!IsAuthority || liveEnemies.Count == 0) return;
        float floor = KillFloorY;
        fallenBuffer.Clear();
        foreach (var kv in liveEnemies) if (kv.Value != null && kv.Value.transform.position.y < floor) fallenBuffer.Add(kv.Value);
        foreach (var role in fallenBuffer) EnemyOutOfBounds(role);
        fallenBuffer.Clear();
    }
}
