using System;
using System.Collections.Generic;
using System.Globalization;
using Flats.Core.Roguelike;
using UnityEngine;

// The shooting range (owner request 2026-10-04): a run whose state carries RunState.range. It stays in the Combat phase with an
// empty plan, so the HUD, builds, abilities, hits and co-op replication are the ordinary ones; what differs is here:
//  - creation never touches a save file, the profile or the statistics (CreateRangeRun), and nothing later does either: every
//    reward, codex, checkpoint and run-end path asks RangeActive first (or RunMachine refuses it for a range state);
//  - players edit their own build and spawn enemies through "range" commands on the existing RogueCommand channel. The authority
//    accepts them only while the state is a range (RangeCommand, and every RunMachine.Range* method checks again);
//  - enemies come from the authority's spawn queue (RangeTick), through the same instantiate path as a stage's waves.
public partial class RoguelikeController
{
    /// <summary>The running state is the shooting range (every client: the flag travels in the replicated state).</summary>
    public bool RangeActive { get { return state != null && state.range; } }

    /// <summary>Authority: instance ids of range enemies start here, clear of the ledger's own small ids (event squads).</summary>
    public const int RangeFirstInstanceId = 100000;
    /// <summary>Authority: seconds the depth and heat stay as they are after a spawn, so every copy configures the new enemies
    /// (AI.SyncTeam arrives a frame after the instantiate) with the plan they were spawned for.</summary>
    public const float RangeSettleSeconds = 0.75f;
    /// <summary>Authority: seconds one stun that holds frozen and passive enemies lasts (elites take half of it), and seconds between
    /// two refreshes. The refresh is shorter than the halved stun, so nobody wakes between two refreshes; an unfreeze lets the
    /// running stun run out, at most RangeHoldStunSeconds later.</summary>
    public const float RangeHoldStunSeconds = 3f, RangeHoldRefreshSeconds = 1f;
    /// <summary>Authority: dropped weapons older than this are removed (every weapon change drops the old one).</summary>
    public const float RangeDroppedGunSeconds = 6f;

    struct RangeSpawnRequest
    {
        public string role, affix, melee, requester;
        public bool elite, passive;
        public int count, depth, place;
        public Vector3 point; public float yaw;
    }

    readonly Queue<RangeSpawnRequest> rangeQueue = new Queue<RangeSpawnRequest>();
    readonly HashSet<int> rangeUsedIds = new HashSet<int>();
    readonly HashSet<int> rangePassive = new HashSet<int>();
    readonly Dictionary<int, float> rangeDropped = new Dictionary<int, float>();   // dropped gun instance id -> first seen
    readonly HashSet<int> rangeInherited = new HashSet<int>();   // enemies an earlier host spawned: their buffered SyncTeam left the room with that host
    HashSet<int> rangeMapGuns;
    int rangeNextId = RangeFirstInstanceId;
    float rangeSettleUntil = -1f, rangeHoldAt, rangeDropSweepAt;

    /// <summary>Authority: enemies waiting in the spawn queue (the menu shows it next to the count on the field).</summary>
    public int RangeQueued { get { int n = 0; foreach (var r in rangeQueue) n += r.count; return n; } }
    /// <summary>Every enemy is held in place (replicated in the state).</summary>
    public bool RangeFrozen { get { return RangeActive && (state.rangeFlags & RogueRange.FrozenFlag) != 0; } }

    // ---------------------------------------------------------------- creation (authority)
    /// <summary>CreateOrResumeRun, range: a fresh range state. No checkpoint is read, written, retired or cleared, the run counters and
    /// the profile's pending run id stay as they are, and nothing is ever rewarded for it.</summary>
    void CreateRangeRun(List<string> keys, List<string> names, List<int> primaries, List<int> secondaries)
    {
        // RoguelikeMode.SoloRange stays set while the range lasts: a scene that loads again in the middle of it is the range again,
        // never an ordinary new run (whose first checkpoint would replace the player's saved run). RangeLeaving clears it, and the
        // menu clears it on entering the Roguelike page and on every ordinary launch.
        var mapDef = RogueCatalog.MapByScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        long seed = (long)(UnityEngine.Random.value * int.MaxValue) ^ DateTime.UtcNow.Ticks;
        string runId = "range-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "-" + (seed & 0xffff).ToString("x4");
        state = RunMachine.CreateRange(runId, seed, RoguelikeMode.Difficulty, mapDef != null ? mapDef.Id : "map.flatcity", keys, names, primaries, secondaries);
        machine = new RunMachine(state);
        RangeResetStageFields();
        metaRunStartedAt = Time.time; metaRewarded = true;   // nothing to reward, now or at any exit
        var mine = state.Player(localKey);
        if (mine != null) SetLoadout(mine, MetaProfiles.ToLoadout(RogueMetaStore.Current));   // start with the headquarters loadout
        MetaSendLocalLoadout();
        Debug.Log("FLATS_ROGUE_RANGE created run=" + runId + " map=" + state.mapId + " players=" + state.players.Length);
        Notify(new RogueEventMessage { kind = "banner", text = "Shooting range\nNothing here is saved.", value = 3 });
    }

    void RangeResetStageFields()
    {
        pacing = new RoguePacing();
        nextWave = 0; objectiveDone = false; stageEnding = false; commanderDied = false; combatLive = true;
        spawnQueue.Clear(); spawning = false; SpawnAnchor = null; SpawnRouteTarget = null;
        objectiveKills = 0; objectiveKillsNeeded = 0;
    }

    /// <summary>The new master of a range keeps it running as it is: enemies stay (scene objects follow the master), nobody goes back
    /// to a safe node, nothing is saved.</summary>
    void RangeHostChanged()
    {
        RangeResetStageFields();
        rangeQueue.Clear();
        rangeUsedIds.Clear();
        rangeInherited.Clear();
        foreach (var kv in liveEnemies) { rangeUsedIds.Add(kv.Key); rangeInherited.Add(kv.Key); if (kv.Key >= rangeNextId) rangeNextId = kv.Key + 1; }
        // the passive targets of the old host come with the replicated state: this host goes on holding the ones still alive
        rangePassive.Clear();
        if (state.rangePassiveIds != null) foreach (int id in state.rangePassiveIds) if (liveEnemies.ContainsKey(id)) rangePassive.Add(id);
        RangeStorePassive();
        rangeHoldAt = 0f;   // held again on the next tick, before the old host's last stun runs out
        if (state.heat != 0) machine.RangeSetHeat(0);
        Notify(new RogueEventMessage { kind = "banner", text = "You are the host now.", value = 3 });
        Broadcast();
    }

    /// <summary>Authority: a player who joins after a host change gets the enemies of the earlier host from the room cache, but not
    /// their SyncTeam (buffered under the host who left): without it the copies have no role, no weapons and no melee component.
    /// This host sends each of those enemies' data to the newcomer once; enemies it spawned itself still come from its own buffer.</summary>
    void RangeSyncInheritedEnemies(PhotonPlayer joiner)
    {
        if (!RangeActive || Menu.network == 0 || joiner == null || rangeInherited.Count == 0) return;
        foreach (int id in rangeInherited)
        {
            RogueEnemyRole role;
            if (!liveEnemies.TryGetValue(id, out role) || role == null || role.Dead) continue;
            var ai = role.GetComponent<AI>(); var view = role.GetComponent<PhotonView>();
            if (ai == null || view == null) continue;
            view.RPC("SyncTeam", joiner, ai.RogueSyncData(RoleIndex(role.RoleId), role.InstanceId, role.Elite));
        }
    }

    /// <summary>Every copy: this enemy is held by the range itself (everything frozen, or a passive target), read from the
    /// replicated state. The authority's stun refresh (RangeTickHold) stops while a host that crashed is being replaced; the hold
    /// does not.</summary>
    public bool RangeHolds(int instanceId)
    {
        if (!RangeActive) return false;
        if ((state.rangeFlags & RogueRange.FrozenFlag) != 0) return true;
        var ids = state.rangePassiveIds;
        return ids != null && Array.IndexOf(ids, instanceId) >= 0;
    }

    /// <summary>Authority: the passive set into the replicated state (sent with the next broadcast).</summary>
    void RangeStorePassive()
    {
        var ids = new int[rangePassive.Count];
        rangePassive.CopyTo(ids);
        Array.Sort(ids);
        state.rangePassiveIds = ids;
    }

    // ---------------------------------------------------------------- authority tick
    void RangeTick(float dt)
    {
        if (!combatLive) combatLive = true;
        state.stageSeconds += dt;
        RangeTickQueue();
        RangeTickHold();
        RangeTickDroppedGuns();
        RangeTickRebroadcast();
        SweepVanishedEnemies(dt);
        TickEvents(dt);
        // a settled event leaves its slot in the plan, so a client that joins later does not build it again
        var enc = state.encounter; bool changed = false;
        if (eventRunner == null && !string.IsNullOrEmpty(enc.eventId)) { enc.eventId = ""; changed = true; }
        if (emergencyRunner == null && !string.IsNullOrEmpty(enc.emergencyId)) { enc.emergencyId = ""; changed = true; }
        if (changed) Broadcast();
    }

    void RangeTickQueue()
    {
        if (rangeQueue.Count == 0)
        {
            // elites with a chosen affix were spawned at a raised heat: back to 0 once every copy has configured them
            if (state.heat != 0 && Time.time >= rangeSettleUntil) { machine.RangeSetHeat(0); Broadcast(); }
            return;
        }
        var next = rangeQueue.Peek();
        int heat = next.affix != "" ? RogueRange.AffixHeat : 0;
        bool sameContext = state.depth == next.depth && state.heat == heat;
        if (!sameContext)
        {
            if (Time.time < rangeSettleUntil) return;   // the last batch is still being configured under its own depth
            if (state.depth != next.depth) machine.RangeSetDepth(next.depth);
            machine.RangeSetHeat(heat);
            Broadcast();   // sent before the instantiates below, so every copy reads this plan when it attaches the role
        }
        rangeQueue.Dequeue();
        int room = RogueRange.SpawnRoom(AliveEnemies, next.count);
        int spawned = 0;
        for (int i = 0; i < room; i++)
        {
            try { if (RangeSpawnOne(next, i) != null) spawned++; }
            catch (Exception ex) { Debug.LogException(ex); }
        }
        rangeSettleUntil = Time.time + RangeSettleSeconds;
        if (next.passive && spawned > 0) { RangeStorePassive(); BroadcastSoon(); }
        if (spawned < next.count)
            Notify(new RogueEventMessage { kind = "denied", playerKey = next.requester, text = spawned == 0 ? "The field is full ({0} enemies).|" + RogueRange.EnemyCap : "Only {0} spawned: the field holds {1} enemies.|" + spawned + "|" + RogueRange.EnemyCap });
        Debug.Log("FLATS_ROGUE_RANGE spawn role=" + next.role + " elite=" + next.elite + " affix=" + next.affix + " melee=" + next.melee + " depth=" + next.depth + " asked=" + next.count + " spawned=" + spawned + " alive=" + AliveEnemies);
    }

    RogueEnemyRole RangeSpawnOne(RangeSpawnRequest request, int index)
    {
        bool finale = request.role == RogueRange.FinaleRole;
        bool elite = request.elite || finale;
        int id = RogueRange.FindInstance(state.runId, rangeNextId, rangeUsedIds, request.role, state.encounter, state.heat, elite, request.melee, request.affix);
        if (id < 0) id = RogueRange.FindInstance(state.runId, rangeNextId, rangeUsedIds, request.role, state.encounter, state.heat, elite, "", "");   // no such combination: any free id
        if (id < 0) return null;
        rangeUsedIds.Add(id);
        while (rangeUsedIds.Contains(rangeNextId)) rangeNextId++;
        Vector3 pos = RangeSpawnPosition(request, index);
        var rotation = Quaternion.Euler(0f, request.yaw, 0f);
        GameObject go = Menu.network == 0 ? Instantiate(Resources.Load("Flatman_Enemy"), pos, rotation) as GameObject : PhotonNetwork.InstantiateSceneObject("Flatman_Enemy", pos, rotation, 0, null);
        if (go == null) return null;
        var ai = go.GetComponent<AI>();
        int tier = state.encounter.enemyStatTier;
        ai.stats_Attack = tier; ai.stats_Defense = tier;
        ai.rogueRole = RoleIndex(request.role); ai.rogueInstance = id; ai.rogueElite = elite ? 1 : 0;
        var role = RogueEnemyRole.Attach(go, request.role, id, elite);
        liveEnemies[id] = role;
        RememberCachedInstantiate(id, go);
        Singleplayer.enemy++;
        BindEnemyMarkers(role);
        if (request.passive) rangePassive.Add(id);
        // held from the next hold tick: the stun travels as an RPC to a component the other copies add when the enemy starts
        if (request.passive || RangeFrozen) rangeHoldAt = Mathf.Min(rangeHoldAt, Time.time + 0.4f);
        return role;
    }

    /// <summary>Where the index-th enemy of a request stands: the requested point (crosshair or ahead of the player) or a random
    /// authored spawn point, then a ring around it for the others, each on the NavMesh.</summary>
    Vector3 RangeSpawnPosition(RangeSpawnRequest request, int index)
    {
        Vector3 centre = request.point;
        if (request.place == 2 && spawnPoints != null && spawnPoints.childCount > 0)
            centre = spawnPoints.GetChild(UnityEngine.Random.Range(0, spawnPoints.childCount)).position;
        if (index > 0)
        {
            // characters are about 6 m tall and 3 m wide: 4 m apart on a growing ring
            float radius = 4f * Mathf.Ceil(index / 6f), angle = index * 60f + (index / 6) * 30f;
            centre += Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
        }
        return GroundedArrival(centre);
    }

    /// <summary>Frozen and passive enemies are held by the ordinary stun (RogueEnemyStatus), renewed before it runs out: every AI
    /// loop, the melee brain and the operative brain already stop while stunned, and every copy shows the stun icon.</summary>
    void RangeTickHold()
    {
        if (Time.time < rangeHoldAt) return;
        rangeHoldAt = Time.time + RangeHoldRefreshSeconds;
        bool frozen = RangeFrozen;
        if (!frozen && rangePassive.Count == 0) return;
        foreach (var kv in liveEnemies)
        {
            if (kv.Value == null || kv.Value.Dead) continue;
            if (frozen || rangePassive.Contains(kv.Key)) RogueEnemyStatus.Request(kv.Value.gameObject, RangeHoldStunSeconds, 0f, 0f, Vector3.zero, null);
        }
    }

    void RangeTickDroppedGuns()
    {
        if (Time.time < rangeDropSweepAt) return;
        rangeDropSweepAt = Time.time + 1f;
        var guns = FindObjectsOfType<DroppedGun>();
        // weapons that lay in the map before this authority's first sweep are the map's own: they stay
        if (rangeMapGuns == null) { rangeMapGuns = new HashSet<int>(); foreach (var gun in guns) rangeMapGuns.Add(gun.gameObject.GetInstanceID()); return; }
        var seen = new HashSet<int>();
        foreach (var gun in guns)
        {
            int id = gun.gameObject.GetInstanceID(); seen.Add(id);
            if (rangeMapGuns.Contains(id)) continue;
            float since;
            if (!rangeDropped.TryGetValue(id, out since)) { rangeDropped[id] = Time.time; continue; }
            if (Time.time - since < RangeDroppedGunSeconds) continue;
            var view = gun.GetComponent<PhotonView>();
            if (Menu.network != 0 && PhotonNetwork.inRoom && view != null && view.instantiationId > 0) { if (PhotonNetwork.isMasterClient) PhotonNetwork.Destroy(gun.gameObject); }
            else Destroy(gun.gameObject);
        }
        if (rangeDropped.Count > seen.Count)
        {
            var gone = new List<int>();
            foreach (var id in rangeDropped.Keys) if (!seen.Contains(id)) gone.Add(id);
            foreach (var id in gone) rangeDropped.Remove(id);
        }
    }

    // ---------------------------------------------------------------- kills (authority)
    /// <summary>OnEnemyDied in the range: the kill counts for the killer's tally and charges ultimates like a paid kill; no money,
    /// no objective. Event runners (a triggered signal device counts its squad) still hear it.</summary>
    void RangeEnemyDied(RogueEnemyRole role, Transform killer, bool headshot)
    {
        if (rangePassive.Remove(role.InstanceId)) RangeStorePassive();
        int weight = role.Def != null ? role.Def.Weight : 100 * RogueCatalog.FinaleWeightMultiplier;
        if (role.Elite && role.Def != null) weight *= RogueCatalog.EliteWeightMultiplier;
        machine.RangeEnemyKilled(KeyOfTransform(killer), weight, headshot, JammedKeys());
        ChargeMarkedKill(role);
        BroadcastSoon();
        if (eventRunner != null) eventRunner.OnEnemyKilled(role);
        if (emergencyRunner != null) emergencyRunner.OnEnemyKilled(role);
    }

    // ---------------------------------------------------------------- commands (authority)
    static int RangeInt(string[] parts, int index, int fallback) { int v; return index < parts.Length && int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : fallback; }
    static string RangeText(string[] parts, int index) { return index < parts.Length ? parts[index] ?? "" : ""; }
    /// <summary>"x;y;z" (invariant culture) to a finite point.</summary>
    static bool RangePoint(string text, out Vector3 point)
    {
        point = Vector3.zero;
        var xyz = (text ?? "").Split(';');
        float x, y, z;
        if (xyz.Length != 3) return false;
        if (!float.TryParse(xyz[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)) return false;
        if (!float.TryParse(xyz[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) return false;
        if (!float.TryParse(xyz[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) return false;
        if (float.IsNaN(x + y + z) || float.IsInfinity(x + y + z)) return false;
        point = new Vector3(x, y, z);
        return true;
    }
    /// <summary>A point as a command argument.</summary>
    public static string RangePointText(Vector3 p)
    {
        return p.x.ToString("0.##", CultureInfo.InvariantCulture) + ";" + p.y.ToString("0.##", CultureInfo.InvariantCulture) + ";" + p.z.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Authority: one "range" command. cmd.text is "op|arg|arg..."; the sender's key was already replaced by the one Photon reports
    /// (ReceiveCommand), so a player can only edit its own build. Refused outright unless the state is the shooting range.
    /// </summary>
    void RangeCommand(RogueCommandMessage cmd)
    {
        if (!RangeActive || machine == null || cmd == null || string.IsNullOrEmpty(cmd.text))
        {
            if (state != null && !state.range) Debug.LogWarning("FLATS_ROGUE_RANGE_REFUSED " + (cmd != null ? cmd.playerKey : "") + ": not a shooting range");
            return;
        }
        var parts = cmd.text.Split('|');
        string key = cmd.playerKey;
        bool changed = false;
        switch (parts[0])
        {
            case "stat": changed = machine.RangeSetStat(key, RangeText(parts, 1), RangeInt(parts, 2, 0)); break;
            case "item": changed = machine.RangeSetItem(key, RangeText(parts, 1), RangeInt(parts, 2, 0)); break;
            case "skill": changed = machine.RangeSetSkill(key, RangeText(parts, 1), RangeInt(parts, 2, 0) != 0); break;
            case "melee": changed = machine.RangeSetMelee(key, RangeText(parts, 1)); break;
            case "clear": changed = machine.RangeClearBuild(key); break;
            case "ultfill": changed = machine.RangeFillUltimate(key); break;
            case "flag": changed = machine.RangeSetFlag(key, RangeInt(parts, 1, 0), RangeInt(parts, 2, 0) != 0); break;
            case "weapon": RangeWeaponCommand(key, RangeInt(parts, 1, 0), RangeText(parts, 2), true); return;
            case "sight": RangeWeaponCommand(key, RangeInt(parts, 1, 0), RangeText(parts, 2), false); return;
            case "loadout": RangeLoadoutCommand(key, cmd.text.Length > "loadout|".Length ? cmd.text.Substring("loadout|".Length) : ""); return;
            case "spawn": RangeSpawnCommand(key, parts); return;
            case "clearenemies": RangeClearEnemies(); Broadcast(); return;
            case "freeze":
                changed = machine.RangeSetWorldFlag(RogueRange.FrozenFlag, RangeInt(parts, 1, 0) != 0);
                rangeHoldAt = 0f;   // applied on the next tick; an unfreeze lets the running stun (at most RangeHoldStunSeconds) run out
                break;
            case "reset":
                // it clears every player's tallies and ultimate charge: the host's decision (the menu shows the reason to the others)
                if (key != localKey) { Notify(new RogueEventMessage { kind = "denied", playerKey = key, text = "Only the host can reset the range." }); return; }
                RangeResetWorld(); return;
            case "event": RangeEventCommand(key, RangeText(parts, 1)); return;
            default: Debug.LogWarning("FLATS_ROGUE_RANGE unknown command " + parts[0]); return;
        }
        if (changed) Broadcast();
    }

    /// <summary>A weapon (or only its sight) for one of the player's two slots: the build changes first, then the owner swaps the gun
    /// in its hands through the ordinary exchange (in co-op the authority spawns the scene gun for it, as the shop does).</summary>
    void RangeWeaponCommand(string key, int slot, string id, bool weapon)
    {
        var p = state.Player(key);
        if (p == null || slot < 0 || slot > 1 || string.IsNullOrEmpty(id)) return;   // a command without its arguments changes nothing
        int oldModel = slot == 0 ? p.build.primaryWeapon : p.build.secondaryWeapon;
        bool ok = weapon ? machine.RangeSetWeapon(key, slot, id) : machine.RangeSetSight(key, slot, id);
        if (!ok) { Notify(new RogueEventMessage { kind = "denied", playerKey = key, text = weapon ? "That gun model is already in your other slot." : "This weapon cannot mount that sight." }); return; }
        Broadcast();
        int model = slot == 0 ? p.build.primaryWeapon : p.build.secondaryWeapon;
        var sight = RogueArmory.Sight(slot == 0 ? p.build.meta.primarySight : p.build.meta.secondarySight);
        int sightIndex = sight != null ? sight.Index : 0;
        int viewId = 0;
        if (Menu.network != 0)
        {
            var go = RogueWorld.PlayerByKey(key);
            if (go == null) return;
            var gun = PhotonNetwork.InstantiateSceneObject("Weapons/Weapon" + model, go.transform.position + Vector3.up * 2f + go.transform.forward, Quaternion.identity, 0, null);
            if (gun == null) return;
            gun.GetPhotonView().RPC("DropData", PhotonTargets.All, GunInfo.limitAmmo[model], GunInfo.limitMaxAmmo[model], sightIndex);
            viewId = gun.GetPhotonView().viewID;
            rangeDropped[gun.GetInstanceID()] = Time.time + 20f;   // the buyer's gun is not litter while it waits to be taken
        }
        Notify(new RogueEventMessage { kind = "range", playerKey = key, text = "equip|" + slot + "|" + model + "|" + sightIndex + "|" + oldModel, index = viewId });
        RangeRebroadcastAfterEquip();
    }

    float rangeRebroadcastA = -1f, rangeRebroadcastB = -1f;
    /// <summary>A gun a copy takes out for the first time starts with its catalogue numbers (Gun.Start), a second or so after the
    /// build that named its armory variant arrived: the state goes out again once the exchange is over, so every copy applies the
    /// build to that gun (magazine, damage, fire rate).</summary>
    void RangeRebroadcastAfterEquip() { rangeRebroadcastA = Time.time + 2.5f; rangeRebroadcastB = Time.time + 5f; }

    void RangeTickRebroadcast()
    {
        if (rangeRebroadcastA >= 0f && Time.time >= rangeRebroadcastA) { rangeRebroadcastA = -1f; Broadcast(); }
        if (rangeRebroadcastB >= 0f && Time.time >= rangeRebroadcastB) { rangeRebroadcastB = -1f; Broadcast(); }
    }

    void RangeLoadoutCommand(string key, string json)
    {
        MetaLoadout declared = null;
        try { declared = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<MetaLoadout>(json); } catch (Exception) { }
        if (declared == null) { Debug.LogWarning("FLATS_ROGUE_RANGE loadout command without a readable loadout from " + key); return; }
        var errors = new List<string>();
        var clean = MetaProfiles.SanitizeLoadout(declared, errors);
        var p = state.Player(key);
        if (p == null) return;
        int oldPrimary = p.build.primaryWeapon, oldSecondary = p.build.secondaryWeapon;
        if (!machine.RangeSetLoadout(key, clean)) return;
        Broadcast();
        // both weapons go back into the player's hands, one after the other (the owner queues them)
        RangeReequip(key, 0, oldPrimary);
        RangeReequip(key, 1, oldSecondary);
    }

    void RangeReequip(string key, int slot, int oldModel)
    {
        var p = state.Player(key);
        if (p == null) return;
        int model = slot == 0 ? p.build.primaryWeapon : p.build.secondaryWeapon;
        var sight = RogueArmory.Sight(slot == 0 ? p.build.meta.primarySight : p.build.meta.secondarySight);
        int sightIndex = sight != null ? sight.Index : 0;
        if (model < 0) return;
        int viewId = 0;
        if (Menu.network != 0)
        {
            var go = RogueWorld.PlayerByKey(key);
            if (go == null) return;
            var gun = PhotonNetwork.InstantiateSceneObject("Weapons/Weapon" + model, go.transform.position + Vector3.up * 2f + go.transform.forward * (1f + slot), Quaternion.identity, 0, null);
            if (gun == null) return;
            gun.GetPhotonView().RPC("DropData", PhotonTargets.All, GunInfo.limitAmmo[model], GunInfo.limitMaxAmmo[model], sightIndex);
            viewId = gun.GetPhotonView().viewID;
            rangeDropped[gun.GetInstanceID()] = Time.time + 20f;
        }
        Notify(new RogueEventMessage { kind = "range", playerKey = key, text = "equip|" + slot + "|" + model + "|" + sightIndex + "|" + oldModel, index = viewId });
        RangeRebroadcastAfterEquip();
    }

    /// <summary>"spawn|role|variant|affix|melee|count|depth|place|passive|x;y;z|yaw": variant 0 normal, 1 elite, 2 chapter target.</summary>
    void RangeSpawnCommand(string key, string[] parts)
    {
        string role = RangeText(parts, 1);
        int variant = RangeInt(parts, 2, 0);
        if (variant == 2) role = RogueRange.FinaleRole;
        if (!RogueRange.ValidRole(role)) return;
        string affix = RangeText(parts, 3), melee = RangeText(parts, 4);
        if (variant == 0 || EliteAffixes.Def(affix) == null) affix = "";
        if (RogueArmory.MeleeWeapon(melee) == null) melee = "";
        int count = Mathf.Clamp(RangeInt(parts, 5, 1), 1, RogueRange.MaxSpawnCount);
        int depth = RogueDepth.Clamp(RangeInt(parts, 6, 1));
        int place = Mathf.Clamp(RangeInt(parts, 7, 0), 0, 2);
        bool passive = RangeInt(parts, 8, 0) != 0;
        Vector3 point; float yaw;
        bool hasPoint = RangePoint(RangeText(parts, 9), out point);
        float.TryParse(RangeText(parts, 10), NumberStyles.Float, CultureInfo.InvariantCulture, out yaw);
        if (float.IsNaN(yaw) || float.IsInfinity(yaw)) yaw = 0f;
        var requester = RogueWorld.PlayerByKey(key);
        // the point must be near the requester (a client cannot place enemies across the map through a crafted command)
        if (place != 2 && (!hasPoint || requester == null || Vector3.Distance(point, requester.transform.position) > 400f))
        {
            if (requester == null) return;
            point = requester.transform.position + requester.transform.forward * 20f;
        }
        if (AliveEnemies + RangeQueued >= RogueRange.EnemyCap)
        {
            Notify(new RogueEventMessage { kind = "denied", playerKey = key, text = "The field is full ({0} enemies).|" + RogueRange.EnemyCap });
            return;
        }
        rangeQueue.Enqueue(new RangeSpawnRequest { role = role, elite = variant == 1, affix = affix, melee = melee, count = count, depth = depth, place = place, passive = passive, point = point, yaw = yaw, requester = key });
    }

    void RangeClearEnemies()
    {
        rangeQueue.Clear(); rangePassive.Clear(); RangeStorePassive();
        foreach (var role in new List<RogueEnemyRole>(liveEnemies.Values)) if (role != null) DespawnEnemyEverywhere(role);
        liveEnemies.Clear();
        Singleplayer.enemy = 0;
        huntInstance = -1;
    }

    /// <summary>Reset: no enemies, no running event, depth 1, nothing frozen, every tally and ultimate charge at zero. Builds stay.</summary>
    void RangeResetWorld()
    {
        RangeClearEnemies();
        DisposeEvents();
        state.encounter.eventId = ""; state.encounter.emergencyId = "";
        machine.RangeSetWorldFlag(RogueRange.FrozenFlag, false);
        machine.RangeSetHeat(0);
        machine.RangeSetDepth(1);
        machine.RangeResetTallies();
        state.ledger = new EncounterLedger();
        ExtraEnemyDamageMul = 1f;
        Notify(new RogueEventMessage { kind = "range", text = "reset" });
        Broadcast();
    }

    /// <summary>Scene tab: one of the stage events on demand (the signal device, the supply drone, the mobile bomb). They are the
    /// ordinary runners in the plan's event and emergency slots, so every client builds its copy from the replicated plan.</summary>
    void RangeEventCommand(string key, string id)
    {
        var def = RogueCatalog.Encounter(id);
        bool emergency = false; foreach (var e in RogueCatalog.Emergencies) if (e.Id == id) emergency = true;
        bool general = false; foreach (var e in RogueCatalog.Events) if (e.Id == id) general = true;
        if (def == null || (!emergency && !general)) return;
        if ((emergency ? emergencyRunner : eventRunner) != null)
        {
            Notify(new RogueEventMessage { kind = "denied", playerKey = key, text = "Another one is still running. Finish it or reset the range." });
            return;
        }
        var enc = state.encounter;
        // anchors are chosen once per pair of events: a running event keeps the points every client built its props on
        if (eventRunner == null && emergencyRunner == null) ChoosePlanPoints();
        state.ledger = new EncounterLedger();   // no "already finished" record of an earlier run of the same event
        if (emergency) enc.emergencyId = id; else enc.eventId = id;
        Notify(new RogueEventMessage { kind = "range", text = "event|" + id });
        var runner = RogueEventRunner.Create(this, id, emergency ? 6 : 3);
        if (runner == null) { if (emergency) enc.emergencyId = ""; else enc.eventId = ""; return; }
        if (emergency) emergencyRunner = runner; else eventRunner = runner;
        runner.Begin();
        Broadcast();
    }

    // ---------------------------------------------------------------- events (every client)
    /// <summary>A "range" event from the authority: the owner's weapon exchange, a reset, an event about to be built again.</summary>
    void RangeEvent(RogueEventMessage e)
    {
        var parts = (e.text ?? "").Split('|');
        switch (parts[0])
        {
            case "equip":
                rangeAppliedBuilds.Remove(e.playerKey);   // that player's copy takes a gun out: its build goes on again with the next states
                rangeEquipReapplyUntil = Time.time + 6f;
                if (e.playerKey != localKey) break;
                RogueRangeRuntime.Ensure(this).QueueEquip(RangeInt(parts, 1, 0), RangeInt(parts, 2, -1), RangeInt(parts, 3, 0), RangeInt(parts, 4, -1), e.index);
                break;
            case "reset":
                RogueRangeMeter.Reset();
                if (!IsAuthority) { DisposeEvents(); liveEnemies.Clear(); }
                break;
            case "event":
                clientFinishedEvents.Remove(RangeText(parts, 1));
                break;
        }
    }

    // ---------------------------------------------------------------- teammates' copies (every client)
    readonly Dictionary<string, string> rangeAppliedBuilds = new Dictionary<string, string>();
    float rangeEquipReapplyUntil = -1f;
    /// <summary>In a run a teammate's copy takes its build once, when it spawns. In the range builds change all the time, so every
    /// copy of every other player applies the replicated build whenever it changed (armory weapon numbers and skins, melee weapon,
    /// stats): what a teammate carries looks and counts the same on every screen. The local player's own copy is applied by
    /// OnStateChanged as always.</summary>
    void RangeApplyTeammateBuilds()
    {
        if (Menu.network == 0 || state == null) return;
        if (Time.time < rangeEquipReapplyUntil) rangeAppliedBuilds.Clear();   // a gun taken out meanwhile starts with catalogue numbers (Gun.Start)
        foreach (var p in state.players)
        {
            if (p.key == localKey || p.build == null) continue;
            var go = RogueWorld.PlayerByKey(p.key);
            var rp = go != null ? go.GetComponent<RoguePlayer>() : null;
            if (rp == null) continue;
            string json = JsonUtility.ToJson(p.build) + "#" + go.GetInstanceID();
            string applied;
            if (rangeAppliedBuilds.TryGetValue(p.key, out applied) && applied == json) continue;
            rangeAppliedBuilds[p.key] = json;
            rp.ApplyBuild(p.build);
        }
    }

    // ---------------------------------------------------------------- local player
    /// <summary>The local player sends one range command ("op|arg|arg...").</summary>
    public void RangeSend(string text) { if (RangeActive) Command(new RogueCommandMessage { kind = "range", text = text }); }

    /// <summary>HUD objective panel in the range: what this is and how to open the menu.</summary>
    void RangeHud()
    {
        // nothing costs money here: the wallet chip says so instead of "$0"
        hudView.SetTop(state.Chapter + "-" + RogueDepth.StageInChapter(state.depth), T("Free"), AliveEnemies.ToString());
        hudView.SetObjective("Sight", T("Shooting range"), "");
        hudView.SetObjectiveStep("Tap", RogueInput.IsTouch ? T("Shooting range: the Range menu button opens the menu") : T("Shooting range: {0} opens the menu", RogueInput.KeyText("Shop")));
        hudView.SetObjectiveProgress(-1f);
        trackerIndex = -1;
    }

    /// <summary>Leaving the range from the pause menu or the range menu: every local effect ends and nothing is written.</summary>
    public void RangeLeaving()
    {
        leaving = true;
        metaRewarded = true;
        RoguelikeMode.SoloRange = false;   // the range is over: the next solo scene is whatever the menu launches
        // the runtime goes now, not with the scene: its Update would write the chosen time scale back while the menu scene loads
        var runtime = GetComponent<RogueRangeRuntime>();
        if (runtime != null) runtime.Shutdown();
        CleanupSession();
    }
}
