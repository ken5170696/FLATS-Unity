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
    bool objectiveDone, stageEnding;
    // QA-19: false from BeginCombat until the stage intro has finished on the authority clock. While false nothing spawns and no
    // stage timer runs (waves, pacing, objective, events, emergencies, extras), so every arrival comes after the intro.
    bool combatLive;
    int objectiveKillsNeeded, objectiveKills;

    public int AliveEnemies { get { int n = 0; foreach (var e in liveEnemies.Values) if (e != null) n++; return n; } }
    public bool WavesDone { get { return state != null && nextWave >= state.encounter.waves.Length && !spawning; } }
    /// <summary>Authority: planned arrivals already released but still waiting in the spawn queue.</summary>
    public int QueuedEnemies { get { return spawnQueue.Count; } }
    /// <summary>Authority: waves of the plan released so far (0 until the first wave leaves the queue).</summary>
    public int ReleasedWaves { get { return nextWave; } }
    /// <summary>Authority: the stage intro is over and the stage runs (spawns, objective and event timers).</summary>
    public bool CombatLive { get { return combatLive; } }
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
            else if (state.phase == RunPhase.Combat)
            {
                // clients own the local effects of replicated event state (gas damage on their own player); the authority does it in Tick
                try { if (eventRunner != null) eventRunner.ClientTick(Time.deltaTime); if (emergencyRunner != null) emergencyRunner.ClientTick(Time.deltaTime); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            yield return null;
        }
    }

    void AuthorityTick(float dt)
    {
        elapsedTimer -= dt;
        if (elapsedTimer <= 0f) { elapsedTimer = 5f; RecordElapsed(); }
        RogueBodyShield.AuthorityTick(this, dt);   // QA-44: carriers who go down, leave or leave combat drop their body
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

    // ---------------------------------------------------------------- cumulative play time (QA-18, Core RunMachine.RecordElapsed)
    RunMachine elapsedMachine;
    double elapsedBaseline;
    float elapsedSessionStart, elapsedTimer;

    /// <summary>Authority: the run's cumulative play seconds = the total saved when this authority took the run (a new run, a resumed
    /// checkpoint, a host change: every one builds a new RunMachine) plus this session's game time (a solo pause does not count). Sent
    /// every 5 s and before every checkpoint and the run end; the result breakdown reads it instead of estimating 120 s per stage.</summary>
    void RecordElapsed()
    {
        if (!IsAuthority || machine == null || state == null) return;
        if (elapsedMachine != machine) { elapsedMachine = machine; elapsedBaseline = state.elapsedSeconds; elapsedSessionStart = Time.time; }
        double total = elapsedBaseline + Mathf.Max(0f, Time.time - elapsedSessionStart);
        if (!double.IsNaN(total) && !double.IsInfinity(total)) machine.RecordElapsed(total);
    }

    // ---------------------------------------------------------------- prep and ready-up
    // QA-20/QA-19 as one authority sequence: everyone ready -> ready countdown -> stage intro -> spawns and stage timers.
    // Both waits are one replicated stage clock (stage clock region of RoguelikeController.cs); clients render the time
    // left from it and never run their own timers.
    void PrepTick(float dt)
    {
        if (clockKind == StageClockKind.Intro && clockEpoch == state.authorityEpoch) SetStageClock(StageClockKind.None, 0f);   // a stage that went back to Prep (host change) has no intro
        if (state.phase != RunPhase.Prep) { if (CountdownRunning) SetStageClock(StageClockKind.None, 0f); return; }
        bool all = machine.AllReady();
        if (all && !CountdownRunning) SetStageClock(StageClockKind.Countdown, Menu.network == 0 ? SoloReadyCountdownSeconds : CoopReadyCountdownSeconds);
        else if (!all && CountdownRunning)
        {
            // un-ready, a new member (joins unready) or a roster change: the countdown stops everywhere and starts over when all are ready
            SetStageClock(StageClockKind.None, 0f);
            Notify(new RogueEventMessage { kind = "banner", text = "Start cancelled.", value = 2 });
        }
        if (!CountdownRunning) return;
        ResendStageClock(dt);
        if (StageClockRemaining() > 0f) return;
        SetStageClock(StageClockKind.None, 0f);
        BeginCombat();
    }

    bool CountdownRunning { get { return clockKind == StageClockKind.Countdown && state != null && clockEpoch == state.authorityEpoch; } }

    void BeginCombat()
    {
        var map = RogueCatalog.Map(state.mapId);
        if (!machine.BeginCombat(map)) return;
        pacing = new RoguePacing();
        DebugOverrideEncounter();
        CloseScreens();
        nextWave = 0; objectiveDone = false; stageEnding = false; commanderDied = false; combatLive = false;
        spawnQueue.Clear(); spawning = false; spawnDelay = 0; lastSpawnPoint = -1; enemyCache.Clear(); SpawnAnchor = null; SpawnRouteTarget = null;
        objectiveKillsNeeded = RogueDirector.CountEnemies(state.encounter); objectiveKills = 0;
        RespawnDeadPlayers();
        ChoosePlanPoints();
        // the intro clock leaves before the Combat snapshot, so a client holds its objective and event world until the intro ends
        SetStageClock(StageClockKind.Intro, StageIntroSeconds);
        Broadcast();
    }

    /// <summary>Authority, first combat tick after the intro: the stage starts (brief, objective, events). Spawns follow in the same tick.</summary>
    void StartStageAfterIntro()
    {
        combatLive = true;
        if (clockKind == StageClockKind.Intro) SetStageClock(StageClockKind.None, 0f);
        state.stageSeconds = 0;   // stage time (pacing, wave release) starts when the players can act, not at the intro
        var enc = state.encounter;
        // QA-43: each client shows the stage brief its own way: the briefing card when its HUD draws one, else the old one-line banner
        Notify(new RogueEventMessage { kind = "brief", text = enc.IsFinale ? enc.finaleId : enc.objectiveId, value = 4 });
        if (!string.IsNullOrEmpty(enc.eventId)) Notify(new RogueEventMessage { kind = "log", text = "Event: {0}|@" + RogueCatalog.Encounter(enc.eventId).Name });
        if (!string.IsNullOrEmpty(enc.emergencyId)) Notify(new RogueEventMessage { kind = "log", text = "Warning: {0}|@" + RogueCatalog.Encounter(enc.emergencyId).Name });
        StartObjective();
        StartEvents();
    }

    // ---------------------------------------------------------------- combat
    void CombatTick(float dt)
    {
        if (!combatLive)
        {
            // the intro holds every spawn and stage timer; the clock is re-sent so a client that loaded late still counts down
            if (clockKind == StageClockKind.Intro && clockEpoch == state.authorityEpoch && StageClockRemaining() > 0f) { ResendStageClock(dt); return; }
            StartStageAfterIntro();
        }
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
    struct PendingSpawn { public int instanceId, wave; public string role; public bool elite, lead; }
    readonly Queue<PendingSpawn> spawnQueue = new Queue<PendingSpawn>();
    bool spawning;   // queue not empty; a validation skip may clear it to drop the rest
    float spawnDelay;
    int lastSpawnPoint = -1;

    void QueueWave(int waveIndex)
    {
        var wave = state.encounter.waves[waveIndex];
        if (waveIndex > 0) Notify(new RogueEventMessage { kind = "banner", text = "Reinforcements!", value = 1.5 });
        for (int i = 0; i < wave.roles.Length; i++)
            spawnQueue.Enqueue(new PendingSpawn { instanceId = machine.InstanceIdFor(waveIndex, i), wave = waveIndex, role = wave.roles[i], elite = wave.elite[i], lead = waveIndex > 0 && i == 0 });
        spawning = spawnQueue.Count > 0;
    }

    void TickSpawnQueue(float dt)
    {
        if (!spawning) spawnQueue.Clear();
        if (spawnQueue.Count == 0) { spawning = false; arrivalHold.Clear(); return; }   // nothing queued: nothing held
        spawnDelay -= dt;
        if (spawnDelay > 0 || AliveEnemies >= state.encounter.concurrentCap) return;
        var next = spawnQueue.Dequeue();
        bool heldBack = false;
        try
        {
            // an arrival held back longer than ArrivalMaxHoldSeconds is placed by the least bad position instead (ArrivalHold: keyed
            // by stage and slot, timed on Time.time, so a record left over from an earlier stage can only shorten a hold)
            bool mayHold = arrivalHold.MayHold(state.encounterCounter, next.instanceId, Time.time, ArrivalMaxHoldSeconds);
            heldBack = !SpawnEnemy(next.instanceId, next.role, next.elite, next.wave, mayHold, ref lastSpawnPoint);
        }
        catch (Exception ex) { Debug.LogException(ex); }
        if (!heldBack) arrivalHold.Released(state.encounterCounter, next.instanceId);
        if (heldBack)
        {
            // QA-52: every position left would break the solo rear window: the arrival keeps its place at the head of the queue and
            // retries shortly (the window lasts SoloEarlyRearWindowSeconds). It stays in the queue, so a finished mission still
            // cancels it and the stage cannot end around it.
            arrivalHold.Held(state.encounterCounter, next.instanceId, Time.time);
            var rest = spawnQueue.ToArray(); spawnQueue.Clear(); spawnQueue.Enqueue(next); foreach (var r in rest) spawnQueue.Enqueue(r);
            spawnDelay = ArrivalHoldRetrySeconds; spawning = true;
            return;
        }
        // a slot that could not be placed never reaches the field: void it so the plan and the Clear count still close
        if (!liveEnemies.ContainsKey(next.instanceId) && machine.EnemyCancelled(next.instanceId)) objectiveKills++;
        spawnDelay = next.wave == 0 ? 0.25f : 0.45f;   // the opening wave arrives quickly
        spawning = spawnQueue.Count > 0;
        // QA-52: the first arrival of a reinforcement wave tells every client where it came from (value: world bearing from the
        // squad centre; text: "x;z" of the arrival, invariant culture) so a client can name the side relative to its own view.
        // Clients without a handler ignore the kind.
        if (next.lead && liveEnemies.ContainsKey(next.instanceId) && lastArrivalBearing >= 0f)
            Notify(new RogueEventMessage { kind = "arrivaldir", value = lastArrivalBearing, index = next.wave,
                text = lastArrivalPosition.x.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + ";" + lastArrivalPosition.z.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) });
    }

    /// <summary>Authority: a spawn point position for an extra enemy by the arrival rules (the anchor ring when one is set).</summary>
    public Vector3 PickSpawnPosition()
    {
        if (spawnPoints == null || spawnPoints.childCount == 0) return Vector3.zero;
        return ArrivalPosition(-1, ref lastSpawnPoint);
    }

    /// <summary>QA-23: search radius (metres) below or around an authored spawn point for the NavMesh an arrival stands on.</summary>
    public const float ArrivalNearGround = 6f, ArrivalGroundSearch = 30f;

    /// <summary>QA-23: an arrival appears standing on the NavMesh. An authored point floating above the ground or on an unwalkable roof
    /// made the agent snap down on its first frame, which read as a teleport from a height; a walkable roof keeps its point and the
    /// enemy comes down through the map's drop links (RogueEnemyLinkTraversal).</summary>
    public static Vector3 GroundedArrival(Vector3 point)
    {
        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(point, out hit, ArrivalNearGround, UnityEngine.AI.NavMesh.AllAreas)) return hit.position;
        if (UnityEngine.AI.NavMesh.SamplePosition(point, out hit, ArrivalGroundSearch, UnityEngine.AI.NavMesh.AllAreas)) return hit.position;
        return point;
    }

    /// <summary>False only when the arrival was held back (QA-52, mayHold); a placement failure returns true with nothing spawned.</summary>
    bool SpawnEnemy(int instanceId, string roleId, bool elite, int wave, bool mayHold, ref int lastPoint)
    {
        if (spawnPoints == null || spawnPoints.childCount == 0) return true;
        Vector3 pos;
        if (!TryArrivalPosition(wave, mayHold, ref lastPoint, out pos)) return false;
        GameObject go;
        if (Menu.network == 0) go = Instantiate(Resources.Load("Flatman_Enemy"), pos, Quaternion.identity) as GameObject;
        else go = PhotonNetwork.InstantiateSceneObject("Flatman_Enemy", pos, Quaternion.identity, 0, null);
        if (go == null) return true;
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
        return true;
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

    // ---------------------------------------------------------------- QA-52 arrival directions
    // PickSpawnPoint chose by distance only, so a stage's arrivals kept coming from one side. ArrivalDirector (Core) ranks the
    // sectors around the squad (novelty, a mild flank/rear bias, the opening-wave spread and the fairness caps); this side turns a
    // sector into a position: an authored point in the band when the sector has one, else a NavMesh arrival synthesized inside the
    // sector with NearerArrival's guarantees (reachable, out of every player's sight, SpawnMinDistance from every player, grounded).
    // When nothing qualifies the old rule places it (PickSpawnPoint + NearerArrival). Authority only; timing, counts, caps and money
    // are not touched: the director only chooses where.

    /// <summary>QA-52: NavMesh samples per synthesized sector; the last ArrivalSynthFarAttempts of them reach NearArrivalMax to
    /// NearArrivalFarMax (open ground: farther out a rise or a tree line hides the arrival more often). A rejected sample costs a
    /// NavMesh sample and a few linecasts; ArrivalMaxSamples bounds them per arrival and ArrivalMaxPathChecks the costly path test.</summary>
    public const int ArrivalSynthAttempts = 10, ArrivalSynthFarAttempts = 3, ArrivalMaxSamples = 40;
    /// <summary>QA-52: outer distance of the far synthesis draws (metres from the squad centre). The old rule's fallback used raw
    /// authored points 200-720 m away on the open maps.</summary>
    public const float NearArrivalFarMax = 150f;
    /// <summary>QA-52: NavMesh path checks (the costly test) allowed for one arrival; beyond it synthesis stops for this arrival
    /// without blaming the sector (a spawn frame stays bounded).</summary>
    public const int ArrivalMaxPathChecks = 8;
    /// <summary>QA-52: old-rule draws per arrival when its first position breaks the solo rear window.</summary>
    public const int ArrivalFallbackDraws = 3;
    /// <summary>QA-52: a held-back arrival retries after ArrivalHoldRetrySeconds and is placed anyway (least bad position) after
    /// ArrivalMaxHoldSeconds, a little longer than the solo rear window, so no arrival waits forever.</summary>
    public const float ArrivalHoldRetrySeconds = 0.5f, ArrivalMaxHoldSeconds = 14f;

    readonly ArrivalDirector arrivalDirector = new ArrivalDirector(unchecked((ulong)DateTime.UtcNow.Ticks));
    readonly List<GameObject> arrivalPresent = new List<GameObject>(), arrivalStanding = new List<GameObject>();
    readonly List<double> arrivalViews = new List<double>();   // view bearing of each present player (NaN unknown)
    readonly List<Vector3> arrivalEyes = new List<Vector3>();  // eye of each present player (its camera, else from its rig height)
    ArrivalCandidate[] arrivalCandidates = new ArrivalCandidate[0];
    int arrivalStage = int.MinValue, arrivalPathChecks, arrivalSamples;
    readonly ArrivalHold arrivalHold = new ArrivalHold();   // the held-back slot (stage and slot keyed, Time.time)
    Vector3 arrivalSquadCentre, lastArrivalPosition;
    float lastArrivalBearing = -1f;

    /// <summary>QA-52 authority diagnostics for measuring arrival directions: per-sector counts relative to the squad, sides, sources,
    /// synthesis rejections per reason, failed sectors, held-back arrivals and the last arrivals (ArrivalDirector.Describe, Records).</summary>
    public ArrivalDirector Arrivals { get { return arrivalDirector; } }

    /// <summary>Authority: where an extra arrival stands (never held back).</summary>
    Vector3 ArrivalPosition(int wave, ref int lastPoint)
    {
        Vector3 pos;
        TryArrivalPosition(wave, false, ref lastPoint, out pos);
        return pos;
    }

    /// <summary>Authority: where the next arrival stands. wave 0 is the opening wave, -1 an extra. False (mayHold only) when every
    /// position left would break the solo rear window: the caller holds the arrival back and retries.</summary>
    bool TryArrivalPosition(int wave, bool mayHold, ref int lastPoint, out Vector3 pos)
    {
        lastArrivalBearing = -1f;
        ArrivalContext ctx = null;
        Vector3 origin = Vector3.zero;
        try
        {
            ctx = BuildArrivalContext(wave, lastPoint, out origin);
            if (ctx != null && DirectedArrival(ctx, origin, ref lastPoint, out pos)) return true;
        }
        catch (Exception ex) { Debug.LogException(ex); ctx = null; }   // a director fault must never lose an arrival: the old rule places it
        // the old rule (PickSpawnPoint + NearerArrival), held to the solo rear window like every other path
        int firstPoint = -1, rearPoint = -1; Vector3 first = Vector3.zero, rearPos = Vector3.zero;
        bool rearReserved = ctx != null && ArrivalRearReserved(ctx);
        for (int draw = 0; draw < ArrivalFallbackDraws; draw++)
        {
            int point = PickSpawnPoint(lastPoint, wave == 0);
            pos = GroundedArrival(NearerArrival(spawnPoints.GetChild(point).position));
            if (firstPoint < 0) { firstPoint = point; first = pos; }
            if (ctx != null && !ArrivalAdmitted(ctx, pos)) { arrivalDirector.NoteFallbackRefused(); continue; }
            if (ctx != null && ArrivalSeen(pos)) { arrivalDirector.NoteSeenRefusal(); continue; }   // the old rule never checked sight; far raw points could appear in view
            // a solo opening keeps its one rear arrival for later: spent here, a map with few open sides could not open a third one.
            // A rear draw is kept in case no other draw turns up.
            if (rearReserved && ArrivalDirector.IsRear(ctx, pos.x, pos.z)) { if (rearPoint < 0) { rearPoint = point; rearPos = pos; } continue; }
            lastPoint = point; if (ctx != null) NoteArrival(ctx, pos, ArrivalSource.Fallback); return true;
        }
        if (rearPoint >= 0) { lastPoint = rearPoint; pos = rearPos; NoteArrival(ctx, pos, ArrivalSource.Fallback); return true; }
        // then any authored point clear of the players that the window admits, nearest the band first
        int admitted = AdmittedAuthoredPoint(ctx);
        if (admitted >= 0)
        {
            lastPoint = admitted;
            pos = GroundedArrival(NearerArrival(spawnPoints.GetChild(admitted).position));
            if (ArrivalAdmitted(ctx, pos) && !ArrivalSeen(pos)) { NoteArrival(ctx, pos, ArrivalSource.Fallback); return true; }
            pos = GroundedArrival(spawnPoints.GetChild(admitted).position);   // its relocation turned behind the player or into sight: the point itself
            NoteArrival(ctx, pos, ArrivalSource.Fallback);
            return true;
        }
        if (mayHold) { arrivalDirector.NoteDeferred(); pos = Vector3.zero; return false; }
        lastPoint = firstPoint; pos = first;   // held too long, or an extra that cannot wait: the least bad position
        if (ctx != null) NoteArrival(ctx, pos, ArrivalSource.Fallback);
        return true;
    }

    bool ArrivalRearReserved(ArrivalContext ctx)
    {
        try { return arrivalDirector.RearReserved(ctx); }
        catch (Exception ex) { Debug.LogException(ex); return false; }
    }

    bool ArrivalAdmitted(ArrivalContext ctx, Vector3 pos)
    {
        try { return arrivalDirector.Admits(ctx, pos.x, pos.z); }
        catch (Exception ex) { Debug.LogException(ex); return true; }
    }

    /// <summary>An authored point at least SpawnMinDistance from every player that the solo rear window admits and no player would see
    /// appear: the nearest one past OpeningMinDistance, else the nearest one. -1 when none (or no context).</summary>
    int AdmittedAuthoredPoint(ArrivalContext ctx)
    {
        if (ctx == null || arrivalCandidates.Length != spawnPoints.childCount) return -1;
        int best = -1, bestClose = -1; double bestD = double.MaxValue, bestCloseD = double.MaxValue;
        for (int i = 0; i < arrivalCandidates.Length; i++)
        {
            var c = arrivalCandidates[i];
            if (c.nearestPlayer < SpawnMinDistance || !ArrivalAdmitted(ctx, new Vector3((float)c.x, 0f, (float)c.z))) continue;
            if (ArrivalSeen(GroundedArrival(spawnPoints.GetChild(i).position))) continue;
            if (c.nearestPlayer >= OpeningMinDistance) { if (c.nearestPlayer < bestD) { bestD = c.nearestPlayer; best = i; } }
            else if (c.nearestPlayer < bestCloseD) { bestCloseD = c.nearestPlayer; bestClose = i; }
        }
        return best >= 0 ? best : bestClose;
    }

    /// <summary>Present players (downed included) count for the minimum distance and line of sight; the squad centre and facing use
    /// the standing ones (everyone downed: the downed squad). Null when no player object exists.</summary>
    ArrivalContext BuildArrivalContext(int wave, int lastPoint, out Vector3 origin)
    {
        origin = Vector3.zero;
        arrivalPresent.Clear(); arrivalStanding.Clear(); arrivalViews.Clear(); arrivalEyes.Clear();
        foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (go.GetComponent<FPSController>() == null) continue;
            arrivalPresent.Add(go);
            var rp = go.GetComponent<RoguePlayer>();
            if (rp == null || !rp.Downed) arrivalStanding.Add(go);
            // the view: the player's own rendering camera when it has one (the local player), else the body's yaw
            var cam = go.GetComponentInChildren<Camera>();
            Vector3 view = cam != null && cam.enabled && cam.gameObject.activeInHierarchy ? cam.transform.forward : go.transform.forward;
            view.y = 0f;
            arrivalViews.Add(view.sqrMagnitude > 1e-4f ? ArrivalDirector.Bearing(0, 0, view.x, view.z) : double.NaN);
            arrivalEyes.Add(ArrivalEye(go));
        }
        if (arrivalPresent.Count == 0 || state == null) return null;
        if (arrivalStage != state.encounterCounter) { arrivalStage = state.encounterCounter; arrivalDirector.Reset(); arrivalHold.Clear(); }
        var standing = arrivalStanding.Count > 0 ? arrivalStanding : arrivalPresent;
        Vector3 centre = Vector3.zero, look = Vector3.zero;
        foreach (var go in standing)
        {
            centre += go.transform.position;
            Vector3 f = go.transform.forward; f.y = 0f;
            if (f.sqrMagnitude > 1e-4f) look += f.normalized;
        }
        centre /= standing.Count;
        arrivalSquadCentre = centre;
        var ctx = new ArrivalContext
        {
            // players looking different ways have no common front: no side bias and no rear rules
            facing = look.magnitude >= 0.35f * standing.Count ? ArrivalDirector.Bearing(0, 0, look.x, look.z) : double.NaN,
            now = state.stageSeconds, wave = wave, solo = arrivalPresent.Count <= 1, chapter = state.Chapter,
            anchored = SpawnAnchor.HasValue, lastCandidate = lastPoint,
            minDistance = SpawnMinDistance, bandDistance = SpawnBandDistance, openingMinDistance = OpeningMinDistance,
        };
        origin = ctx.anchored ? SpawnAnchor.Value : centre;
        ctx.centreX = origin.x; ctx.centreZ = origin.z;
        bool route = ctx.Opening && SpawnRouteTarget.HasValue;
        Vector3 target = route ? SpawnRouteTarget.Value : Vector3.zero;
        if (route) ctx.routeBearing = ArrivalDirector.Bearing(centre.x, centre.z, target.x, target.z);
        int n = spawnPoints.childCount;
        if (arrivalCandidates.Length != n) arrivalCandidates = new ArrivalCandidate[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 p = spawnPoints.GetChild(i).position;
            float nearest = float.MaxValue;
            foreach (var go in arrivalPresent) nearest = Mathf.Min(nearest, Vector3.Distance(go.transform.position, p));
            float fromAnchor = ctx.anchored ? Vector3.Distance(p, origin) : 0f;
            arrivalCandidates[i] = new ArrivalCandidate
            {
                x = p.x, z = p.z, nearestPlayer = nearest,
                onRoute = route && Vector3.Distance(p, target) < nearest,   // Break Out: nearer the exit than the squad, as before
                inRing = ctx.anchored && fromAnchor >= AnchorMinDistance && fromAnchor <= AnchorMaxDistance,
            };
        }
        return ctx;
    }

    /// <summary>Walks the plan: an authored entry is taken at once; synthesized entries are tried up to
    /// ArrivalDirector.MaxStrictSynthesizedSectors while they keep every rule (level 0) and MaxSynthesizedSectors once relaxed, within
    /// ArrivalMaxPathChecks path checks. A sector whose synthesis failed is remembered where the squad stood.</summary>
    bool DirectedArrival(ArrivalContext ctx, Vector3 origin, ref int lastPoint, out Vector3 pos)
    {
        pos = Vector3.zero;
        var plan = arrivalDirector.Plan(ctx, arrivalCandidates);
        int synthesized = 0;
        arrivalPathChecks = 0; arrivalSamples = 0;
        for (int k = 0; k < plan.Count; k++)
        {
            int idx = plan.candidates[k];
            if (idx >= 0)
            {
                if (idx >= spawnPoints.childCount) continue;
                pos = GroundedArrival(spawnPoints.GetChild(idx).position);
                if (!ArrivalAdmitted(ctx, pos)) continue;   // a last-resort entry may not break the solo rear window either
                if (ArrivalSeen(pos)) { arrivalDirector.NoteSeenRefusal(); continue; }   // authored points were never sight-checked
                lastPoint = idx;
                NoteArrival(ctx, pos, ArrivalSource.Authored);
                return true;
            }
            int budget = plan.Level(k) == 0 ? ArrivalDirector.MaxStrictSynthesizedSectors : ArrivalDirector.MaxSynthesizedSectors;
            if (synthesized >= budget || arrivalPathChecks >= ArrivalMaxPathChecks || arrivalSamples >= ArrivalMaxSamples) continue;   // later authored entries are still taken
            synthesized++;
            bool geometric;
            int result = SynthesizeArrival(ctx, plan, k, origin, out pos, out geometric);
            if (result > 0 && ArrivalAdmitted(ctx, pos)) { NoteArrival(ctx, pos, ArrivalSource.Synthesized); return true; }
            if (result == 0) arrivalDirector.MarkUnavailable(plan.sectors[k], ctx.now, origin.x, origin.z, geometric);
        }
        return false;
    }

    /// <summary>A NavMesh arrival inside plan entry k's sector: NearArrivalMin-NearArrivalMax metres from the squad centre (the last
    /// draws out to NearArrivalFarMax; the anchor ring when anchored), at least SpawnMinDistance from every player, out of every
    /// player's sight (outside its view cone, or behind cover), reachable by the nearest standing player, grounded. Replaces
    /// NearerArrival's +-70 degree fan around one authored point. 1 found, 0 the sector failed, -1 the per-arrival budget ran out.
    /// Every rejected sample is counted by reason (ArrivalDirector.SynthFailureCounts); geometric is true when the ground (no NavMesh,
    /// off the sector, outside the ring, no path) rejected at least GroundFailRatio of the samples, so the sector rests.</summary>
    int SynthesizeArrival(ArrivalContext ctx, ArrivalPlan plan, int k, Vector3 origin, out Vector3 pos, out bool geometric)
    {
        pos = Vector3.zero;
        int sector = plan.sectors[k];
        int ground = 0, other = 0, noMesh = 0;
        geometric = true;
        var standing = arrivalStanding.Count > 0 ? arrivalStanding : arrivalPresent;
        for (int attempt = 0; attempt < ArrivalSynthAttempts; attempt++)
        {
            geometric = ArrivalDirector.IsGroundFailure(ground, other);
            if (noMesh >= ArrivalDirector.NoMeshGiveUpSamples && noMesh == attempt) return 0;   // off the map: keep the budget for real sectors
            if (arrivalSamples >= ArrivalMaxSamples) return -1;   // the frame budget ran out, not the sector
            arrivalSamples++;
            bool far = attempt >= ArrivalSynthAttempts - ArrivalSynthFarAttempts;
            float minR = ctx.anchored ? AnchorMinDistance : far ? NearArrivalMax : NearArrivalMin;
            float maxR = ctx.anchored ? AnchorMaxDistance : far ? NearArrivalFarMax : NearArrivalMax;
            double bearing = ArrivalDirector.SectorStart(sector) + UnityEngine.Random.value * ArrivalDirector.SectorDegrees;
            if (!plan.AllowsBearing(k, bearing)) { arrivalDirector.NoteSynthFailure(SynthFailure.BearingRefused); other++; continue; }
            float rad = (float)(bearing * Math.PI / 180.0);
            Vector3 guess = origin + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * UnityEngine.Random.Range(minR, maxR);
            UnityEngine.AI.NavMeshHit hit;
            if (!UnityEngine.AI.NavMesh.SamplePosition(guess + Vector3.up * 4f, out hit, 10f, UnityEngine.AI.NavMesh.AllAreas)) { arrivalDirector.NoteSynthFailure(SynthFailure.NoNavMesh); ground++; noMesh++; continue; }
            Vector3 p = hit.position;
            // the sample may slide out of the sector, out of the Break Out cone or into a capped rear arc
            if (!plan.AllowsBearing(k, ArrivalDirector.Bearing(origin.x, origin.z, p.x, p.z))) { arrivalDirector.NoteSynthFailure(SynthFailure.LeftSector); ground++; continue; }
            if (ctx.anchored) { float d = Vector3.Distance(p, origin); if (d < AnchorMinDistance || d > AnchorMaxDistance) { arrivalDirector.NoteSynthFailure(SynthFailure.OutsideRing); ground++; continue; } }
            SynthFailure? reject = null;
            for (int i = 0; i < arrivalPresent.Count && reject == null; i++)
                if (Vector3.Distance(arrivalPresent[i].transform.position, p) < SpawnMinDistance) reject = SynthFailure.TooClose;
            if (reject == null && ArrivalSeen(p)) reject = SynthFailure.InView;   // never materialise in plain view
            if (reject != null) { arrivalDirector.NoteSynthFailure(reject.Value); other++; continue; }
            GameObject nearest = null; float nearestD = float.MaxValue;
            foreach (var go in standing) { float d = Vector3.Distance(go.transform.position, p); if (d < nearestD) { nearestD = d; nearest = go; } }
            if (arrivalPathChecks >= ArrivalMaxPathChecks) return -1;
            arrivalPathChecks++;
            if (nearest == null || !RogueWorld.Reachable(p, nearest.transform.position)) { arrivalDirector.NoteSynthFailure(SynthFailure.Unreachable); ground++; continue; }
            pos = GroundedArrival(p);
            return 1;
        }
        geometric = ArrivalDirector.IsGroundFailure(ground, other);
        return 0;
    }

    /// <summary>QA-52: what hides an arrival from a player's eye. Default carries every piece of static world geometry on the shipped
    /// maps: the ground and hills (UrbanPark's Land_* mesh colliders), rocks, buildings and tree trunks (2x9x2 m boxes); the project
    /// has no Terrain layer and no Unity Terrain. Deliberately left out: Glass (14, see-through), ManOnly (15: FlatCity, Beachside
    /// and Troy's InvisibleWall, DepartmentStore's 48 Nets, and UrbanPark's bush proxies, all sharing the layer, so counting it would
    /// hide arrivals behind invisible walls and nets) and the team, bullet, grabbed and ignore layers (moving or non-scenery).</summary>
    static int ArrivalSightMask { get { return LayerMask.GetMask("Default"); } }

    /// <summary>QA-52: points of an arrival a player could see, as fractions of its standing height (chest and head). Checking the
    /// chest alone let a dune or a low wall hide the chest while the head stood in view.</summary>
    public const float ArrivalChestFraction = 0.6f, ArrivalHeadFraction = 0.9f;
    /// <summary>QA-52: a player's eye as a fraction of its rig height when it has no camera to read (a remote copy).</summary>
    public const float ArrivalEyeFraction = 0.85f;
    static float arrivalBodyHeight = -1f;

    /// <summary>QA-52: standing height of an arriving enemy in metres, read once from the Flatman_Enemy prefab (root scale times its
    /// NavMeshAgent, else CharacterController, height: 4 x 1.8 = 7.2 m today, head about 6.5 m). The rig's own number, not a constant.</summary>
    static float ArrivalBodyHeight
    {
        get
        {
            if (arrivalBodyHeight > 0f) return arrivalBodyHeight;
            float h = 0f;
            var prefab = Resources.Load<GameObject>("Flatman_Enemy");
            if (prefab != null)
            {
                var agent = prefab.GetComponent<UnityEngine.AI.NavMeshAgent>();
                var controller = prefab.GetComponent<CharacterController>();
                float local = agent != null ? agent.height : controller != null ? controller.height : 0f;
                h = local * Mathf.Abs(prefab.transform.localScale.y);
            }
            arrivalBodyHeight = h > 0.5f ? h : 7.2f;   // a missing prefab never disables the check
            return arrivalBodyHeight;
        }
    }

    /// <summary>QA-52: a player's eye: its camera (the local player's, or a remote copy's camera object even when disabled) when it
    /// sits on the rig, else the rig height times ArrivalEyeFraction above its feet.</summary>
    static Vector3 ArrivalEye(GameObject player)
    {
        var controller = player.GetComponent<CharacterController>();
        float rig = (controller != null ? controller.height : 1.8f) * Mathf.Abs(player.transform.lossyScale.y);
        var cam = player.GetComponentInChildren<Camera>(true);
        if (cam != null && Vector3.Distance(cam.transform.position, player.transform.position) <= rig * 1.5f) return cam.transform.position;
        return player.transform.position + Vector3.up * rig * ArrivalEyeFraction;
    }

    /// <summary>QA-52: a player would see an arrival standing at p appear: p lies inside that player's view cone and the line from its
    /// eye to the arrival's chest or to its head is clear (either suffices). Checked in the frame the enemy is created, with the
    /// players' current eyes and views (BuildArrivalContext runs in the same call), on every path: synthesized, authored and old-rule.</summary>
    bool ArrivalSeen(Vector3 p)
    {
        int mask = ArrivalSightMask;
        float h = ArrivalBodyHeight;
        Vector3 chest = p + Vector3.up * h * ArrivalChestFraction, head = p + Vector3.up * h * ArrivalHeadFraction;
        for (int i = 0; i < arrivalPresent.Count && i < arrivalEyes.Count; i++)
        {
            Vector3 eye = arrivalEyes[i];
            if (!ArrivalDirector.InViewCone(eye.x, eye.z, arrivalViews[i], p.x, p.z)) continue;
            if (!Physics.Linecast(eye, chest, mask, QueryTriggerInteraction.Ignore)) return true;
            if (!Physics.Linecast(eye, head, mask, QueryTriggerInteraction.Ignore)) return true;
        }
        return false;
    }

    void NoteArrival(ArrivalContext ctx, Vector3 pos, ArrivalSource source)
    {
        try { arrivalDirector.Record(ctx, pos.x, pos.z, source); }
        catch (Exception ex) { Debug.LogException(ex); }
        lastArrivalPosition = pos;
        lastArrivalBearing = (float)ArrivalDirector.Bearing(arrivalSquadCentre.x, arrivalSquadCentre.z, pos.x, pos.z);
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
            // QA-27: one "credit" per member with that member's own amount (payout.Minor is per player; the popup and the log show
            // exactly what this player's wallet gained, not the first member's share)
            foreach (var credit in payout.Minor)
                if (credit.Value > 0) Notify(new RogueEventMessage { kind = "credit", playerKey = credit.Key, minor = credit.Value, flag = headshot && credit.Key == killerKey, index = 0, text = who });
            var marker = role.MarkedBy;
            var bonus = marker != null ? machine.MarkedKillBonus(marker.Key, payout) : null;
            if (bonus != null)
                foreach (var credit in bonus.Minor)
                    if (credit.Value > 0) Notify(new RogueEventMessage { kind = "credit", playerKey = credit.Key, minor = credit.Value, index = 1 });
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
        if (cmd.text != null && cmd.text.StartsWith(RogueBodyShield.ActionPrefix)) { RogueBodyShield.AuthorityCommand(this, cmd); return; }   // QA-44
        if (cmd.text != null && cmd.text.StartsWith("fx:")) { RogueWorldFx.RelayKillBlast(this, cmd); return; }   // QA-45 kill explosions for everyone
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
        Notify(new RogueEventMessage { kind = "banner", text = "Travelling to {0}...|@" + RogueCatalog.Map(state.mapId).Name, value = 3 });
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
        RogueBodyShield.ReleaseAllLocal();   // QA-44: nobody leaves the run holding a body
        RecordElapsed();   // the result counts the whole run's play time
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
