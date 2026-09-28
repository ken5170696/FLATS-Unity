using System;
using System.Collections;
using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

// Stage flow, spawning, kills and the wipe check. Authority only, except where noted.
public partial class RoguelikeController
{
#if UNITY_EDITOR
    public static string DebugForceObjective, DebugForceEvent, DebugForceEmergency;
#endif
    readonly Dictionary<int, RogueEnemyRole> liveEnemies = new Dictionary<int, RogueEnemyRole>();
    readonly RoguePacing pacing = new RoguePacing();
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
            if (IsAuthority && machine != null) AuthorityTick(Time.deltaTime);
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
#if UNITY_EDITOR
        // Editor-only validation override (never compiled into a player): force a specific objective/event/emergency for the stage.
        if (!string.IsNullOrEmpty(DebugForceObjective)) { if (DebugForceObjective.StartsWith("fin.")) { state.encounter.finaleId = DebugForceObjective; state.encounter.objectiveId = ""; } else { state.encounter.objectiveId = DebugForceObjective; state.encounter.finaleId = ""; } }
        if (DebugForceEvent != null) state.encounter.eventId = DebugForceEvent;
        if (DebugForceEmergency != null) state.encounter.emergencyId = DebugForceEmergency;
        if (DebugForceObjective != null && DebugForceObjective.StartsWith("fin.") && !System.Array.Exists(state.encounter.waves[state.encounter.waves.Length - 1].roles, r => r == "role.finale"))
        {
            var last = state.encounter.waves[state.encounter.waves.Length - 1];
            var r = new System.Collections.Generic.List<string>(last.roles); var e = new System.Collections.Generic.List<bool>(last.elite); var w = new System.Collections.Generic.List<int>(last.weights);
            r.Add("role.finale"); e.Add(true); w.Add(600); last.roles = r.ToArray(); last.elite = e.ToArray(); last.weights = w.ToArray();   // appended so existing slot ids stay aligned with InstanceIdFor
            RogueEconomy.Reserve(state.ledger, new[] { "role.finale" }, new[] { 600 });
        }
#endif
        CloseScreens();
        nextWave = 0; objectiveDone = false; stageEnding = false; commanderDied = false;
        objectiveKillsNeeded = RogueDirector.CountEnemies(state.encounter); objectiveKills = 0;
        RespawnDeadPlayers();
        ChoosePlanPoints();
        Broadcast();
        var enc = state.encounter;
        string title = "@" + (enc.IsFinale ? RogueCatalog.Encounter(enc.finaleId).Name : RogueCatalog.Encounter(enc.objectiveId).Name);
        for (int i = 3; i > 0; i--)
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
            SpawnEnemy(instanceId, wave.roles[i], wave.elite[i], ref lastPoint);
            yield return new WaitForSeconds(0.6f);
        }
        spawning = false;
    }

    void SpawnEnemy(int instanceId, string roleId, bool elite, ref int lastPoint)
    {
        if (spawnPoints == null || spawnPoints.childCount == 0) return;
        int point = PickSpawnPoint(lastPoint);
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
    }

    int PickSpawnPoint(int lastPoint)
    {
        // farthest-from-players bias with a random tie-break; never the same point twice in a row
        var players = GameObject.FindGameObjectsWithTag("Player");
        int best = -1; float bestScore = -1;
        int tries = Mathf.Min(6, spawnPoints.childCount);
        for (int t = 0; t < tries; t++)
        {
            int idx = UnityEngine.Random.Range(0, spawnPoints.childCount);
            if (idx == lastPoint && spawnPoints.childCount > 1) continue;
            float nearest = float.MaxValue;
            foreach (var p in players) nearest = Mathf.Min(nearest, Vector3.Distance(p.transform.position, spawnPoints.GetChild(idx).position));
            if (nearest < 12f) nearest *= 0.1f;     // no face spawns
            if (nearest > bestScore) { bestScore = nearest; best = idx; }
        }
        return best < 0 ? UnityEngine.Random.Range(0, spawnPoints.childCount) : best;
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
            Broadcast();
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
        double fraction = objectiveRunner != null ? objectiveRunner.RewardFraction : 1.0;
        var pay = machine.ObjectiveCompleted();
        if (fraction < 1.0 && pay.Total > 0) { /* half reward: the ledger already paid; claw back the difference from each wallet */ foreach (var p in state.players) { var v = pay.Minor.ContainsKey(p.key) ? pay.Minor[p.key] : 0; long back = RogueMoney.MulFraction(v, 1.0 - fraction); p.walletMinor = RogueMoney.Clamp(p.walletMinor - back); p.earnedMinor -= back; } }
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
        Broadcast();
        leaving = true; travelling = true;
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
            if (RogueSave.RecordRunEnd(meta, state, localKey)) RogueSaveStore.WriteMeta(meta);
            RogueSaveStore.ClearCheckpoint();
        }
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

    /// <summary>Summary shown on the result screen (Menu.Results asks for it when the mode is active).</summary>
    public string ResultText()
    {
        if (state == null) return "";
        var me = LocalPlayer;
        string outcome = T(state.end == RunEnd.Evacuated ? "Evacuated" : state.end == RunEnd.Wiped ? "Squad wiped" : "Run ended");
        string mine = me != null ? "\n" + T("Earned {0}  Kills {1}  Headshots {2}", RogueMoney.Format(me.earnedMinor), me.kills, me.headshots) : "";
        return outcome + "\n" + T("Chapter {0}  Stage {1}  Depth {2}", state.Chapter, RogueDepth.StageInChapter(state.depth), state.deepestDepth) + mine;
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
