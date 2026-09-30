using System;
using System.Collections;
using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

// Stage flow, spawning, kills and the wipe check. Authority only, except where noted.
public partial class RoguelikeController
{
    /// <summary>Private validation overlay hook (no public implementation): may rewrite the planned encounter before it starts.</summary>
    partial void DebugOverrideEncounter();
    readonly Dictionary<int, RogueEnemyRole> liveEnemies = new Dictionary<int, RogueEnemyRole>();
    // pacing compares against state.stageSeconds, which restarts at 0 every stage: a pacing kept across stages remembered the last
    // stage's release time and held the next opening wave back until that time plus the wave gap (F22). One per stage.
    RoguePacing pacing = new RoguePacing();
    int nextWave;
    float readyCountdown = -1;
    bool objectiveDone, stageEnding;
    int objectiveKillsNeeded, objectiveKills;

    public int AliveEnemies { get { int n = 0; foreach (var e in liveEnemies.Values) if (e != null) n++; return n; } }
    public bool WavesDone { get { return state != null && nextWave >= state.encounter.waves.Length && !spawning; } }
    /// <summary>Authority: planned enemies not on the field yet (queued arrivals plus waves not released).</summary>
    public int UnreleasedEnemies { get { int n = spawnQueue.Count; if (state != null) for (int w = nextWave; w < state.encounter.waves.Length; w++) n += state.encounter.waves[w].roles.Length; return n; } }
    /// <summary>Authority: arrivals prefer a ring around this point (a called extraction under siege). Null for the normal rule.</summary>
    public Vector3? SpawnAnchor { get; set; }
    /// <summary>Authority: the opening wave prefers points nearer this point than the squad (the route to a Break Out exit).</summary>
    public Vector3? SpawnRouteTarget { get; set; }
    // a mission objective (not Clear, not a finale) that is still running: waves come early while the field is thin
    bool MissionPressure { get { var enc = state.encounter; return objectiveRunner != null && !objectiveDone && !enc.IsFinale && enc.objectiveId != "obj.clear"; } }
    public bool StageObjectiveDone { get { return objectiveDone; } }
    public bool CommanderDead { get { return commanderDied; } }
    bool commanderDied;
    public RogueEnemyRole FindFinaleEnemy() { foreach (var e in liveEnemies.Values) if (e != null && e.RoleId == "role.finale") return e; return null; }

    IEnumerator RunLoop()
    {
        while (!leaving)
        {
            if (state == null) { yield return null; continue; }
            if (IsAuthority && machine != null)
            {
                try { AuthorityTick(Time.deltaTime); }
                catch (Exception ex) { Debug.LogException(ex); }   // one bad frame must not stop the run for every player
            }
            yield return null;
        }
    }

    void AuthorityTick(float dt)
    {
        switch (state.phase)
        {
            case RunPhase.Prep:
            case RunPhase.ChapterEnd:
                PrepTick(dt);
                break;
            case RunPhase.Combat:
                CombatTick(dt);
                break;
        }
    }

    // ---------------------------------------------------------------- prep and ready-up
    void PrepTick(float dt)
    {
        if (state.phase != RunPhase.Prep) return;
        bool all = machine.AllReady();
        if (all && readyCountdown < 0) { readyCountdown = Menu.network == 0 ? 3f : 5f; Notify(new RogueEventMessage { kind = "banner", text = "Everyone is ready. Starting...", value = readyCountdown }); }
        if (!all && readyCountdown >= 0) { readyCountdown = -1; Notify(new RogueEventMessage { kind = "banner", text = "Start cancelled.", value = 2 }); }
        if (readyCountdown >= 0)
        {
            readyCountdown -= dt;
            if (readyCountdown <= 0) { readyCountdown = -1; StartCoroutine(BeginCombat()); }
        }
    }

    IEnumerator BeginCombat()
    {
        var map = RogueCatalog.Map(state.mapId);
        if (!machine.BeginCombat(map)) yield break;
        pacing = new RoguePacing();
        DebugOverrideEncounter();
        CloseScreens();
        nextWave = 0; objectiveDone = false; stageEnding = false; commanderDied = false;
        spawnQueue.Clear(); spawning = false; spawnDelay = 0; lastSpawnPoint = -1; enemyCache.Clear(); SpawnAnchor = null; SpawnRouteTarget = null;
        objectiveKillsNeeded = RogueDirector.CountEnemies(state.encounter); objectiveKills = 0;
        RespawnDeadPlayers();
        ChoosePlanPoints();
        Broadcast();
        var enc = state.encounter;
        string title = "@" + (enc.IsFinale ? RogueCatalog.Encounter(enc.finaleId).Name : RogueCatalog.Encounter(enc.objectiveId).Name);
        for (int i = 2; i > 0; i--)
        {
            Notify(new RogueEventMessage { kind = "banner", text = "Stage {0}-{1}: {2}\nstart in {3}...|" + state.Chapter + "|" + RogueDepth.StageInChapter(state.depth) + "|" + title + "|" + i, value = 1.1 });
            yield return new WaitForSeconds(1f);
        }
        Notify(new RogueEventMessage { kind = "banner", text = RogueCatalog.Encounter(enc.IsFinale ? enc.finaleId : enc.objectiveId).Brief, value = 4 });
        if (!string.IsNullOrEmpty(enc.eventId)) Notify(new RogueEventMessage { kind = "log", text = "Event: {0}|@" + RogueCatalog.Encounter(enc.eventId).Name });
        if (!string.IsNullOrEmpty(enc.emergencyId)) Notify(new RogueEventMessage { kind = "log", text = "Warning: {0}|@" + RogueCatalog.Encounter(enc.emergencyId).Name });
        StartObjective();
        StartEvents();
    }

    // ---------------------------------------------------------------- combat
    void CombatTick(float dt)
    {
        state.stageSeconds += dt;
        pacing.Tick(dt);
        var waves = state.encounter.waves;
        // queued arrivals count as present, so a wave stalled behind the cap never has the next one piled on top of it
        if (nextWave < waves.Length && pacing.ShouldRelease(state.stageSeconds, waves[nextWave], AliveEnemies + spawnQueue.Count, state.encounter.concurrentCap, !objectiveDone, false, MissionPressure))
        {
            QueueWave(nextWave);
            pacing.Released(state.stageSeconds);
            nextWave++;
        }
        TickSpawnQueue(dt);
        SweepVanishedEnemies(dt);
        TickObjective(dt);
        TickEvents(dt);
        if (!stageEnding && objectiveDone && nextWave >= waves.Length && AliveEnemies == 0 && !spawning)
            StartCoroutine(EndStage());
    }

    // One authority queue for every released wave, spawned one at a time and never past the concurrent cap. Waves released while
    // an earlier one still waited used to run parallel coroutines that could each pass the cap check in the same frame, and the
    // first to finish cleared the shared flag while the other was still spawning.
    struct PendingSpawn { public int instanceId, wave; public string role; public bool elite; }
    readonly Queue<PendingSpawn> spawnQueue = new Queue<PendingSpawn>();
    bool spawning;   // queue not empty; a validation skip may clear it to drop the rest
    float spawnDelay;
    int lastSpawnPoint = -1;

    void QueueWave(int waveIndex)
    {
        var wave = state.encounter.waves[waveIndex];
        if (waveIndex > 0) Notify(new RogueEventMessage { kind = "banner", text = "Reinforcements!", value = 1.5 });
        for (int i = 0; i < wave.roles.Length; i++)
            spawnQueue.Enqueue(new PendingSpawn { instanceId = machine.InstanceIdFor(waveIndex, i), wave = waveIndex, role = wave.roles[i], elite = wave.elite[i] });
        spawning = spawnQueue.Count > 0;
    }

    void TickSpawnQueue(float dt)
    {
        if (!spawning) spawnQueue.Clear();
        if (spawnQueue.Count == 0) { spawning = false; return; }
        spawnDelay -= dt;
        if (spawnDelay > 0 || AliveEnemies >= state.encounter.concurrentCap) return;
        var next = spawnQueue.Dequeue();
        try { SpawnEnemy(next.instanceId, next.role, next.elite, next.wave == 0, ref lastSpawnPoint); }
        catch (Exception ex) { Debug.LogException(ex); }
        // a slot that could not be placed never reaches the field: void it so the plan and the Clear count still close
        if (!liveEnemies.ContainsKey(next.instanceId) && machine.EnemyCancelled(next.instanceId)) objectiveKills++;
        spawnDelay = next.wave == 0 ? 0.25f : 0.45f;   // the opening wave arrives quickly
        spawning = spawnQueue.Count > 0;
    }

    /// <summary>Authority: a spawn point position for an extra enemy by the arrival rules (the anchor ring when one is set).</summary>
    public Vector3 PickSpawnPosition()
    {
        if (spawnPoints == null || spawnPoints.childCount == 0) return Vector3.zero;
        lastSpawnPoint = PickSpawnPoint(lastSpawnPoint, false);
        return NearerArrival(spawnPoints.GetChild(lastSpawnPoint).position);
    }

    void SpawnEnemy(int instanceId, string roleId, bool elite, bool openingWave, ref int lastPoint)
    {
        if (spawnPoints == null || spawnPoints.childCount == 0) return;
        int point = PickSpawnPoint(lastPoint, openingWave);
        lastPoint = point;
        Vector3 pos = NearerArrival(spawnPoints.GetChild(point).position);
        GameObject go;
        if (Menu.network == 0) go = Instantiate(Resources.Load("Flatman_Enemy"), pos, Quaternion.identity) as GameObject;
        else go = PhotonNetwork.InstantiateSceneObject("Flatman_Enemy", pos, Quaternion.identity, 0, null);
        if (go == null) return;
        var ai = go.GetComponent<AI>();
        int tier = state.encounter.enemyStatTier;
        ai.stats_Attack = tier; ai.stats_Defense = tier;
        ai.rogueRole = RoleIndex(roleId); ai.rogueInstance = instanceId; ai.rogueElite = elite ? 1 : 0;
        // the role component is attached on every client from AI.SyncTeam; the authority attaches early so the map is complete
        var role = RogueEnemyRole.Attach(go, roleId, instanceId, elite);
        liveEnemies[instanceId] = role;
        RememberCachedInstantiate(instanceId, go);
        Singleplayer.enemy++;
        BindEnemyMarkers(role);
    }

    /// <summary>Engagement band for enemy arrivals (metres to the nearest player). The old farthest-point rule put the opening wave
    /// 300-400 m away on most maps, so a stage opened with a minute of nothing and then everyone arrived at once.</summary>
    public const float SpawnMinDistance = 40f, SpawnBandDistance = 120f, OpeningMinDistance = 60f;
    /// <summary>Ring around SpawnAnchor (metres): near enough to reach the zone during a hold, never inside it.</summary>
    public const float AnchorMinDistance = 25f, AnchorMaxDistance = 70f;

    /// <summary>Maps whose authored spawn points all sit far from the squad (FlatCity: 200-360 m from the start) left a stage with
    /// nobody to fight for the first 40 s. An arrival farther than the band from every player is moved to a reachable NavMesh point
    /// NearArrivalMin-NearArrivalMax metres from a random player, out of that player's sight, on the side the enemy came from.</summary>
    public const float NearArrivalMin = 60f, NearArrivalMax = 110f;

    Vector3 NearerArrival(Vector3 authored)
    {
        var players = GameObject.FindGameObjectsWithTag("Player");
        if (players.Length == 0) return authored;
        float nearest = float.MaxValue; foreach (var p in players) nearest = Mathf.Min(nearest, Vector3.Distance(p.transform.position, authored));
        if (nearest <= SpawnBandDistance) return authored;
        var anchor = players[UnityEngine.Random.Range(0, players.Length)].transform;
        Vector3 toward = authored - anchor.position; toward.y = 0; if (toward.sqrMagnitude < 1f) toward = anchor.forward; toward.Normalize();
        for (int attempt = 0; attempt < 16; attempt++)
        {
            // fan out around the direction of the authored point so arrivals keep their lanes
            float angle = UnityEngine.Random.Range(-70f, 70f), distance = UnityEngine.Random.Range(NearArrivalMin, NearArrivalMax);
            Vector3 guess = anchor.position + Quaternion.Euler(0, angle, 0) * toward * distance;
            UnityEngine.AI.NavMeshHit hit;
            if (!UnityEngine.AI.NavMesh.SamplePosition(guess + Vector3.up * 4f, out hit, 10f, UnityEngine.AI.NavMesh.AllAreas)) continue;
            bool clear = true;
            foreach (var p in players) if (Vector3.Distance(p.transform.position, hit.position) < SpawnMinDistance) clear = false;
            if (!clear || !RogueWorld.Reachable(hit.position, anchor.position)) continue;
            // never materialise in plain view: the player's eye must not see the arrival's chest
            Vector3 eye = anchor.position + Vector3.up * 5f, chest = hit.position + Vector3.up * 3f;
            if (!Physics.Linecast(eye, chest, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore)) continue;
            return hit.position;
        }
        return authored;
    }

    int PickSpawnPoint(int lastPoint, bool openingWave)
    {
        // every point at least SpawnMinDistance from all players is a candidate; points inside the band weigh 1, farther ones fade
        // (180 m = 0.5, 300 m = 0.25). The opening wave has its own rule below.
        var players = GameObject.FindGameObjectsWithTag("Player");
        var candidates = new List<int>(); var distances = new List<float>();
        for (int idx = 0; idx < spawnPoints.childCount; idx++)
        {
            float nearest = float.MaxValue;
            foreach (var p in players) nearest = Mathf.Min(nearest, Vector3.Distance(p.transform.position, spawnPoints.GetChild(idx).position));
            if (nearest < SpawnMinDistance) continue;          // no face spawns
            if (idx == lastPoint && spawnPoints.childCount > 2) continue;
            candidates.Add(idx); distances.Add(nearest);
        }
        if (candidates.Count == 0)
        {
            // everything is close (tiny map or a squad spread over it): the farthest point is the least bad
            int far = 0; float farD = -1;
            for (int idx = 0; idx < spawnPoints.childCount; idx++)
            {
                float nearest = float.MaxValue;
                foreach (var p in players) nearest = Mathf.Min(nearest, Vector3.Distance(p.transform.position, spawnPoints.GetChild(idx).position));
                if (nearest > farD) { farD = nearest; far = idx; }
            }
            return far;
        }
        if (SpawnAnchor.HasValue)
        {
            // an objective under siege (the called extraction): arrivals come from a ring around it, still clear of every player
            Vector3 anchor = SpawnAnchor.Value; var ring = new List<int>();
            foreach (int idx in candidates) { float d = Vector3.Distance(spawnPoints.GetChild(idx).position, anchor); if (d >= AnchorMinDistance && d <= AnchorMaxDistance) ring.Add(idx); }
            if (ring.Count > 0) return ring[UnityEngine.Random.Range(0, ring.Count)];
            // no authored point in the ring (FlatCity's exit had none within 70 m): the three candidates nearest the anchor, so the
            // siege still comes from the zone's side instead of the far end of the map
            var byAnchor = new List<int>(candidates);
            byAnchor.Sort((a, b) => Vector3.Distance(spawnPoints.GetChild(a).position, anchor).CompareTo(Vector3.Distance(spawnPoints.GetChild(b).position, anchor)));
            return byAnchor[UnityEngine.Random.Range(0, Mathf.Min(3, byAnchor.Count))];
        }
        if (openingWave)
        {
            // the three nearest points at least OpeningMinDistance away (any distance if none): contact within seconds, but spread over
            // several lanes and far enough that the squad can see them coming instead of taking focused fire from one spot
            var order = new List<int>(); for (int k = 0; k < candidates.Count; k++) order.Add(k);
            order.Sort((a, b) => distances[a].CompareTo(distances[b]));
            if (SpawnRouteTarget.HasValue)
            {
                // Break Out: the opening wave stands on the way (points nearer the exit than the squad), so the walk there is a fight
                Vector3 target = SpawnRouteTarget.Value;
                var route = order.FindAll(k => Vector3.Distance(spawnPoints.GetChild(candidates[k]).position, target) < distances[k]);
                if (route.Count > 0) order = route;
            }
            var near = order.FindAll(k => distances[k] >= OpeningMinDistance);
            if (near.Count == 0) near = order;
            return candidates[near[UnityEngine.Random.Range(0, Mathf.Min(3, near.Count))]];
        }
        float total = 0; var weights = new float[candidates.Count];
        for (int k = 0; k < candidates.Count; k++) { weights[k] = 1f / (1f + Mathf.Max(0f, distances[k] - SpawnBandDistance) / 60f); total += weights[k]; }
        float roll = UnityEngine.Random.value * total;
        for (int k = 0; k < candidates.Count; k++) { roll -= weights[k]; if (roll <= 0) return candidates[k]; }
        return candidates[candidates.Count - 1];
    }

    public static int RoleIndex(string roleId)
    {
        for (int i = 0; i < RogueCatalog.EnemyRoles.Length; i++) if (RogueCatalog.EnemyRoles[i].Id == roleId) return i;
        return roleId == "role.finale" ? 100 : 0;
    }

    /// <summary>Called from DamageReceiver.Die on every client; only the authority pays.</summary>
    public void OnEnemyDied(RogueEnemyRole role, Transform killer, bool headshot)
    {
        if (role == null) return;
        liveEnemies.Remove(role.InstanceId);
        if (IsAuthority) ForgetCachedInstantiate(role.InstanceId, role.gameObject);
        if (!IsAuthority || machine == null || state.phase != RunPhase.Combat) return;
        string killerKey = KeyOfTransform(killer);
        if (role.RoleId == "role.finale") commanderDied = true;
        var payout = machine.EnemyKilled(role.InstanceId, killerKey, headshot, JammedKeys());
        objectiveKills++;
        if (payout.Total > 0)
        {
            string who = Menu.network == 0 ? "" : (state.Player(killerKey) != null ? state.Player(killerKey).name : "");
            long each = 0; foreach (var v in payout.Minor.Values) { each = v; break; }
            Notify(new RogueEventMessage { kind = "bounty", playerKey = killerKey, minor = each, flag = headshot, text = who });
            var marker = role.MarkedBy;
            var bonus = marker != null ? machine.MarkedKillBonus(marker.Key, payout) : null;
            if (bonus != null && bonus.Total > 0) Notify(new RogueEventMessage { kind = "log", text = "Marked kill bonus +{0}|" + RogueMoney.Format(FirstValue(bonus)) });
            ChargeMarkedKill(role);
            BroadcastSoon();
        }
        OnObjectiveEnemyKilled(role);
        if (eventRunner != null) eventRunner.OnEnemyKilled(role);
        if (emergencyRunner != null) emergencyRunner.OnEnemyKilled(role);
    }

    /// <summary>An enemy left the field without dying (recovered, despawned, destroyed): its bounty is void. Safe to call from
    /// OnDestroy after a normal death (Die already removed it, so nothing is voided or counted twice).</summary>
    public void OnEnemyRemoved(RogueEnemyRole role) { if (role != null) RemoveEnemy(role.InstanceId, role.gameObject); }

    void RemoveEnemy(int instanceId, GameObject go)
    {
        if (!liveEnemies.Remove(instanceId)) return;
        if (!IsAuthority) return;
        ForgetCachedInstantiate(instanceId, go);
        if (machine != null && state.phase == RunPhase.Combat) { machine.EnemyCancelled(instanceId); objectiveKills++; }
    }

    // X007: an enemy destroyed without Die (a despawn, a kill volume, a watchdog) left a dead entry in liveEnemies and its slot open,
    // so nothing ever voided it. The authority sweeps destroyed entries twice a second.
    float sweepTimer;
    void SweepVanishedEnemies(float dt)
    {
        sweepTimer -= dt;
        if (sweepTimer > 0) return;
        sweepTimer = 0.5f;
        List<int> gone = null;
        foreach (var kv in liveEnemies)
        {
            if (kv.Value == null) { if (gone == null) gone = new List<int>(); gone.Add(kv.Key); }
            else if (!enemyCache.ContainsKey(kv.Key)) RememberCachedInstantiate(kv.Key, kv.Value.gameObject);   // extras spawned elsewhere
        }
        if (gone != null) foreach (int id in gone) RemoveEnemy(id, null);
    }

    // X006: enemies are room-cached scene objects and every client destroys its own dead copy locally, so the cached instantiate
    // outlived the enemy and a late joiner received a ghost. The master removes the cached instantiate and the view's buffered
    // RPCs (the first half of PhotonNetwork.Destroy) without the network destroy, which would cut the death short on every screen.
    readonly Dictionary<int, int[]> enemyCache = new Dictionary<int, int[]>();   // instanceId -> { instantiationId, creator, viewID }

    void RememberCachedInstantiate(int instanceId, GameObject go)
    {
        if (Menu.network == 0 || go == null) return;
        var view = go.GetComponent<PhotonView>();
        if (view != null && view.instantiationId > 0) enemyCache[instanceId] = new[] { view.instantiationId, view.CreatorActorNr, view.viewID };
    }

    void ForgetCachedInstantiate(int instanceId, GameObject go)
    {
        if (go != null && !enemyCache.ContainsKey(instanceId)) RememberCachedInstantiate(instanceId, go);
        int[] cached;
        if (!enemyCache.TryGetValue(instanceId, out cached)) return;
        enemyCache.Remove(instanceId);
        if (Menu.network == 0 || !PhotonNetwork.inRoom || !PhotonNetwork.isMasterClient) return;
        var instantiate = new ExitGames.Client.Photon.Hashtable(); instantiate[(byte)7] = cached[0];
        PhotonNetwork.networkingPeer.OpRaiseEvent(202, instantiate, true, new RaiseEventOptions { CachingOption = EventCaching.RemoveFromRoomCache, TargetActors = new[] { cached[1] } });
        var rpcs = new ExitGames.Client.Photon.Hashtable(); rpcs[(byte)0] = cached[2];
        PhotonNetwork.networkingPeer.OpRaiseEvent(200, rpcs, true, new RaiseEventOptions { CachingOption = EventCaching.RemoveFromRoomCache });
    }

    /// <summary>Master or solo: removes an enemy from every client and the room cache at once (host-change reset, not a death).</summary>
    public void DespawnEnemyEverywhere(RogueEnemyRole role)
    {
        if (role == null) return;
        var go = role.gameObject;
        liveEnemies.Remove(role.InstanceId); enemyCache.Remove(role.InstanceId);
        var view = go.GetComponent<PhotonView>();
        if (Menu.network != 0 && PhotonNetwork.inRoom && PhotonNetwork.isMasterClient && view != null && view.instantiationId > 0) PhotonNetwork.Destroy(go);
        else Destroy(go);
    }

    string KeyOfTransform(Transform t)
    {
        if (t == null) return "";
        if (Menu.network == 0) return localKey;
        var view = t.GetComponent<PhotonView>();
        return view != null && view.owner != null ? KeyOf(view.owner) : "";
    }

    // ---------------------------------------------------------------- objective (stage 1 slice: Clear; others plug in via RogueObjectiveRunner)
    RogueObjectiveRunner objectiveRunner;

    void StartObjective()
    {
        objectiveRunner = RogueObjectiveRunner.Create(this, state.encounter);
        if (objectiveRunner == null) objectiveDone = false;
    }

    void TickObjective(float dt)
    {
        if (objectiveDone) return;
        if (objectiveRunner != null)
        {
            objectiveRunner.Tick(dt);
            if (objectiveRunner.Succeeded) CompleteObjective();
            return;
        }
        // Clear: the whole plan released and spawned, and no live registered enemy left. A kill counter could never reach its total
        // when an enemy vanished without Die or a slot failed to spawn (X007); the counter only feeds the HUD.
        if (nextWave >= state.encounter.waves.Length && !spawning && AliveEnemies == 0) CompleteObjective();
    }

    void OnObjectiveEnemyKilled(RogueEnemyRole role) { if (objectiveRunner != null) objectiveRunner.OnEnemyKilled(role); }

    void OnObjectiveInput(RogueCommandMessage cmd)
    {
        if (cmd.text != null && cmd.text.StartsWith("hit:")) { OnWorldHit(cmd.text.Substring(4), (float)cmd.value, cmd.playerKey); return; }
        if (cmd.text != null && cmd.text.StartsWith("equip:")) { int idx; if (int.TryParse(cmd.text.Substring(6), out idx)) { var pl = state.Player(cmd.playerKey); if (pl != null && pl.build.primaryWeapon == idx) OnEquipRequest(cmd.playerKey, idx); } return; }
        if (objectiveRunner != null) objectiveRunner.OnCommand(cmd);
        if (eventRunner != null) eventRunner.OnCommand(cmd);
        if (emergencyRunner != null) emergencyRunner.OnCommand(cmd);
    }

    void CompleteObjective()
    {
        if (objectiveDone) return;
        objectiveDone = true;
        // a fallback success (Convoy lost, Protect device lost) pays the reduced amount directly; the old pay-then-claw-back left the
        // team total and the banner at the full amount (F29)
        double fraction = objectiveRunner != null ? Math.Max(0.0, Math.Min(1.0, objectiveRunner.RewardFraction)) : 1.0;
        // a finished mission ends the plan: slots never released (later waves, the spawn queue) are cancelled BEFORE the payout, the
        // order RunMachine needs to count the mission compensation. Enemies already on the field stay, still pay, and must still
        // be cleared before the stage ends. Clear has nothing left by now; a finale keeps its waves.
        var enc = state.encounter;
        if (!enc.IsFinale && enc.objectiveId != "obj.clear") CancelUnreleasedPlan();
        SpawnAnchor = null; SpawnRouteTarget = null;
        var pay = machine.ObjectiveCompleted(fraction);
        Notify(new RogueEventMessage { kind = "banner", text = pay.Total > 0 ? "Objective complete!\n+{0} each|" + RogueMoney.Format(FirstValue(pay)) : "Objective complete!", value = 3 });
        if (objectiveRunner != null) objectiveRunner.Dispose();
        objectiveRunner = null;
        Broadcast();
    }

    void CancelUnreleasedPlan()
    {
        foreach (var pending in spawnQueue) machine.EnemyCancelled(pending.instanceId);   // a paid or already voided slot returns false
        spawnQueue.Clear(); spawning = false;
        var waves = state.encounter.waves;
        for (int w = nextWave; w < waves.Length; w++) for (int i = 0; i < waves[w].roles.Length; i++) machine.EnemyCancelled(machine.InstanceIdFor(w, i));
        nextWave = waves.Length;
    }

    static long FirstValue(Payout p) { foreach (var v in p.Minor.Values) return v; return 0; }

    // ---------------------------------------------------------------- stage end, chapter flow, run end
    IEnumerator EndStage()
    {
        stageEnding = true;
        if (eventRunner != null && !eventRunner.Finished) { eventRunner.Tick(0); }
        DisposeEvents();
        machine.StageCleared();
        Notify(new RogueEventMessage { kind = "banner", text = "Stage cleared!", value = 2.5 });
        Broadcast();
        yield return new WaitForSeconds(2.5f);
        machine.EnterReward();
        Broadcast();
        // wait for every connected player to pick, with a generous AFK timeout that auto-picks the first offer
        float timeout = 90f;
        while (!machine.RewardsDone() && timeout > 0) { timeout -= Time.deltaTime; yield return null; }
        if (!machine.RewardsDone())
            foreach (var p in state.players)
                if (p.connected && p.rewardOffers.Length > 0 && Array.TrueForAll(p.rewardOffers, o => !o.sold))
                    machine.Buy(new ShopTransaction { txId = p.key + ":auto:" + state.depth, playerKey = p.key, runId = state.runId, shopVersion = p.shopVersion, offerIndex = 0, rewardPick = true });
        machine.AdvanceAfterReward();
        if (state.phase == RunPhase.Prep) { WriteCheckpoint(); Notify(new RogueEventMessage { kind = "banner", text = "Stage {0}-{1}\nShop is open. Ready up when done.|" + state.Chapter + "|" + RogueDepth.StageInChapter(state.depth), value = 4 }); }
        else if (state.phase == RunPhase.Route) Notify(new RogueEventMessage { kind = "banner", text = "Chapter {0} complete! Choose your route.|" + state.Chapter, value = 4 });
        Broadcast();
    }

    IEnumerator ContinueChapter()
    {
        if (!machine.ContinueChapter()) yield break;
        WriteCheckpoint();
        // no Broadcast here: the new chapter's Prep would open the next shop on every screen for the seconds before the scene
        // changes (purchases there would be lost with the scene). Clients receive this state with the travel RPC instead.
        leaving = true; travelling = true;
        CloseScreens();
        Notify(new RogueEventMessage { kind = "banner", text = "Travelling to {0}...|" + RogueCatalog.Map(state.mapId).SceneName, value = 3 });
        yield return new WaitForSeconds(1f);
        var map = RogueCatalog.Map(state.mapId);
        // the same in-memory document a disk resume would produce; the next scene's controller picks it up
        RoguelikeMode.PendingResume = new RunSaveDocument { run = state, localPlayerKey = localKey };
        if (Menu.network == 0)
        {
            menu.StartCoroutine("BackgroundColor", "FadeIn");
            yield return new WaitForSeconds(1.5f);
            Menu.changedSettings = true;
            menu.LoadOfflineSceneForRun(map.BuildIndex);
        }
        else
        {
            // co-op travels through the existing map RPC so every client loads the same scene
            GetComponent<PhotonView>().RPC("RogueTravel", PhotonTargets.All, map.BuildIndex, RogueSaveStore.ToJson(state));
        }
    }

    [PunRPC]
    void RogueTravel(int buildIndex, string json, PhotonMessageInfo info)
    {
        if (info.sender != null && !info.sender.IsMasterClient) return;
        var incoming = RogueSaveStore.FromJson<RunState>(json);
        if (incoming != null && (state == null || incoming.runId == state.runId)) { if (PhotonNetwork.isMasterClient) RoguelikeMode.PendingResume = new RunSaveDocument { run = incoming, localPlayerKey = localKey }; }
        else if (incoming != null) return;
        leaving = true; travelling = true;
        StartCoroutine(TravelRoutine(buildIndex));
    }

    IEnumerator TravelRoutine(int buildIndex)
    {
        menu.StartCoroutine("BackgroundColor", "FadeIn");
        yield return new WaitForSeconds(1.5f);
        Menu.changedSettings = true;
        menu.LoadOfflineSceneForRun(buildIndex);
    }

    void CheckWipe()
    {
        if (!IsAuthority || machine == null || state.phase == RunPhase.Ended) return;
        if (machine.TeamWiped()) StartCoroutine(WipeRoutine());
    }

    IEnumerator WipeRoutine()
    {
        machine.Wipe();
        Notify(new RogueEventMessage { kind = "banner", text = "Squad wiped.", value = 3 });
        Broadcast();
        yield return StartCoroutine(EndRun());
    }

    IEnumerator EndRun()
    {
        leaving = true; endedHandled = true;
        Broadcast();
        CleanupSession();
        if (IsAuthority)
        {
            var meta = RogueSaveStore.ReadMeta();
            previousBestDepth = meta.deepestDepth;
            if (RogueSave.RecordRunEnd(meta, state, localKey)) RogueSaveStore.WriteMeta(meta);
            RogueSaveStore.ClearCheckpoint();
        }
        MetaRunEnded();
        CloseScreens();
        yield return new WaitForSeconds(1f);
        // the shared Game Over flow shows the singleplayer result screen; Menu.Results reads our summary text
        Multiplayer.end = Menu.network != 0;
        GameObject.Find("Menu").BroadcastMessage("GameOver", SendMessageOptions.DontRequireReceiver);
    }

    /// <summary>HUD visibility follows the play state, not the value captured when a screen opened.</summary>
    public void RestoreHud()
    {
        if (screen != null) return;
        var hudObject = GameObject.Find("UI");
        var canvas = hudObject != null ? hudObject.GetComponent<Canvas>() : null;
        if (canvas != null && FindLocalPlayer() != null) canvas.enabled = true;
    }

    int previousBestDepth = -1;

    /// <summary>Summary shown on the result screen (Menu.Results asks for it when the mode is active): outcome, how far, what got you
    /// and how to counter it, and whether this was a personal best.</summary>
    /// <summary>Large result line (replaces the Classic score rank, which means nothing here): outcome, how far, and a new record.</summary>
    public string ResultHeadline()
    {
        if (state == null) return "";
        bool solo = state.players.Length <= 1;
        string outcome = T(state.end == RunEnd.Evacuated ? "Evacuated" : state.end == RunEnd.Wiped ? (solo ? "You fell" : "Squad wiped") : "Run ended");
        string reached = T("Chapter {0}  Stage {1}  Depth {2}", state.Chapter, RogueDepth.StageInChapter(state.depth), state.deepestDepth);
        bool record = previousBestDepth > 0 && state.deepestDepth > previousBestDepth;
        return (record ? T("New personal best!") + "\n" : "") + outcome + "\n" + reached;
    }

    public string ResultText()
    {
        if (state == null) return "";
        var me = LocalPlayer;
        string text = me != null ? T("Earned {0}  Kills {1}  Headshots {2}", "$" + RogueMoney.Format(me.earnedMinor), me.kills, me.headshots) : "";
        if (state.end == RunEnd.Wiped && RoguePlayer.LastHitRole != "" && Time.time - RoguePlayer.LastHitTime < 20f)
        {
            EnemyRoleDef role = null; foreach (var r in RogueCatalog.EnemyRoles) if (r.Id == RoguePlayer.LastHitRole) role = r;
            string who = role != null ? T(role.Name) : RoguePlayer.LastHitRole == "role.finale" ? T("the chapter target") : T("an enemy");
            text += "\n" + T("Last hit: {0}, {1} m away", who, Mathf.RoundToInt(RoguePlayer.LastHitDistance));
            if (role != null) text += "\n" + T(role.Brief);
        }
        if (previousBestDepth > 0 && state.deepestDepth <= previousBestDepth)
            text += "\n" + T("Best: chapter {0} stage {1}", RogueDepth.ChapterOf(previousBestDepth), RogueDepth.StageInChapter(previousBestDepth));
        return text;
    }

    void RespawnDeadPlayers()
    {
        // Solo: the player object is destroyed on death; a fresh Flatman spawns at prep. Co-op: each owner respawns itself (see RoguePlayer).
        if (Menu.network != 0) return;
        if (FindLocalPlayer() == null && spawnPoints != null)
        {
            foreach (var cam in FindObjectsOfType<WatchCamera>()) Destroy(cam.gameObject);
            foreach (var pending in FindObjectsOfType<Respawn>()) Destroy(pending);   // an old ragdoll must not spawn a spectator over the new player
            Instantiate(Resources.Load("Flatman"), spawnPoints.GetChild(UnityEngine.Random.Range(0, spawnPoints.childCount)).position, Quaternion.identity);
            RestoreHud();
        }
    }

    /// <summary>Co-op owner: recreate the local Flatman when the authority says we are alive again at a safe node.</summary>
    public void EnsureLocalPlayerAlive()
    {
        var me = LocalPlayer;
        if (me == null || me.life != PlayerLife.Alive || FindLocalPlayer() != null || spawnPoints == null || Menu.network == 0) return;
        foreach (var cam in FindObjectsOfType<WatchCamera>()) Destroy(cam.gameObject);
        foreach (var pending in FindObjectsOfType<Respawn>()) Destroy(pending);
        PhotonNetwork.Instantiate("Flatman", spawnPoints.GetChild(UnityEngine.Random.Range(0, spawnPoints.childCount)).position, Quaternion.identity, 0, null);
        if (menu != null) menu.StartCoroutine("BackgroundColor", "FadeOut");
        RestoreHud();
    }
}
