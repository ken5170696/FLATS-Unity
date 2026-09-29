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
        if (nextWave < waves.Length && pacing.ShouldRelease(state.stageSeconds, waves[nextWave], AliveEnemies, state.encounter.concurrentCap, !objectiveDone, false))
        {
            StartCoroutine(SpawnWave(nextWave));
            pacing.Released(state.stageSeconds);
            nextWave++;
        }
        TickObjective(dt);
        TickEvents(dt);
        if (!stageEnding && objectiveDone && nextWave >= waves.Length && AliveEnemies == 0 && !spawning)
            StartCoroutine(EndStage());
    }

    bool spawning;
    IEnumerator SpawnWave(int waveIndex)
    {
        spawning = true;
        var wave = state.encounter.waves[waveIndex];
        if (waveIndex > 0) Notify(new RogueEventMessage { kind = "banner", text = "Reinforcements!", value = 1.5 });
        int lastPoint = -1;
        for (int i = 0; i < wave.roles.Length; i++)
        {
            while (AliveEnemies >= state.encounter.concurrentCap && state.phase == RunPhase.Combat) yield return new WaitForSeconds(0.5f);
            if (state.phase != RunPhase.Combat) break;
            int instanceId = machine.InstanceIdFor(waveIndex, i);
            try { SpawnEnemy(instanceId, wave.roles[i], wave.elite[i], waveIndex == 0, ref lastPoint); }
            catch (Exception ex) { Debug.LogException(ex); }
            yield return new WaitForSeconds(waveIndex == 0 ? 0.25f : 0.45f);   // the opening wave arrives quickly
        }
        spawning = false;
    }

    void SpawnEnemy(int instanceId, string roleId, bool elite, bool openingWave, ref int lastPoint)
    {
        if (spawnPoints == null || spawnPoints.childCount == 0) return;
        int point = PickSpawnPoint(lastPoint, openingWave);
        lastPoint = point;
        Vector3 pos = spawnPoints.GetChild(point).position;
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
        Singleplayer.enemy++;
        BindEnemyMarkers(role);
    }

    /// <summary>Engagement band for enemy arrivals (metres to the nearest player). The old farthest-point rule put the opening wave
    /// 300-400 m away on most maps, so a stage opened with a minute of nothing and then everyone arrived at once.</summary>
    public const float SpawnMinDistance = 40f, SpawnBandDistance = 120f, OpeningMinDistance = 60f;

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
        if (openingWave)
        {
            // the three nearest points at least OpeningMinDistance away (any distance if none): contact within seconds, but spread over
            // several lanes and far enough that the squad can see them coming instead of taking focused fire from one spot
            var order = new List<int>(); for (int k = 0; k < candidates.Count; k++) order.Add(k);
            order.Sort((a, b) => distances[a].CompareTo(distances[b]));
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
            BroadcastSoon();
        }
        OnObjectiveEnemyKilled(role);
        if (eventRunner != null) eventRunner.OnEnemyKilled(role);
        if (emergencyRunner != null) emergencyRunner.OnEnemyKilled(role);
    }

    /// <summary>An enemy left the field without dying (recovered, despawned): its bounty is void.</summary>
    public void OnEnemyRemoved(RogueEnemyRole role)
    {
        if (role == null) return;
        liveEnemies.Remove(role.InstanceId);
        if (IsAuthority && machine != null) { machine.EnemyCancelled(role.InstanceId); objectiveKills++; }
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
        // Clear: every planned enemy dead or voided
        if (nextWave >= state.encounter.waves.Length && objectiveKills >= objectiveKillsNeeded && AliveEnemies == 0 && !spawning) CompleteObjective();
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
        var pay = machine.ObjectiveCompleted(fraction);
        Notify(new RogueEventMessage { kind = "banner", text = pay.Total > 0 ? "Objective complete!\n+{0} each|" + RogueMoney.Format(FirstValue(pay)) : "Objective complete!", value = 3 });
        if (objectiveRunner != null) objectiveRunner.Dispose();
        objectiveRunner = null;
        Broadcast();
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
