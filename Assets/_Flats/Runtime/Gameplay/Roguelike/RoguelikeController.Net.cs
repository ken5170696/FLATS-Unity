using System.Collections;
using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

// Co-op hardening: roster changes, authority hand-over, life replication, revive holds,
// transaction effects, run end on every client, and session cleanup.
public partial class RoguelikeController
{
    bool endedHandled, travelling, screenDismissed;
    readonly Dictionary<string, float> reviveHold = new Dictionary<string, float>();      // "rescuer|victim" -> seconds held
    readonly Dictionary<string, float> reviveLastReport = new Dictionary<string, float>();
    readonly HashSet<string> appliedTx = new HashSet<string>();
    readonly RogueHoldLedger reviveLedger = new RogueHoldLedger();

    // ---------------------------------------------------------------- roster and authority (Photon callbacks on the controller's object)
    void OnPhotonPlayerDisconnected(PhotonPlayer other)
    {
        if (!RoguelikeMode.Coop || state == null) return;
        string key = KeyOf(other);
        if (IsAuthority && machine != null)
        {
            machine.SetConnected(key, false);
            var p = state.Player(key);
            Notify(new RogueEventMessage { kind = "log", text = "{0} left the squad.|" + (p != null ? p.name : key) });
            Broadcast();
            CheckWipe();
        }
    }

    void OnPhotonPlayerConnected(PhotonPlayer other)
    {
        if (!RoguelikeMode.Coop || state == null || !IsAuthority || machine == null) return;
        // a returning or new player joins at the next safe node; the run adds them only in Prep (RunMachine.AddPlayer rule)
        string key = KeyOf(other);
        // a player back after a disconnect has a new actor number: they take back their own entry, build and wallet
        AdoptKeys(MatchSavedPlayers(state, new List<string> { key }, true));
        var existing = state.Player(key);
        if (existing != null) { machine.SetConnected(key, true); Notify(new RogueEventMessage { kind = "log", text = "{0} rejoined.|" + existing.name }); }
        else if (state.phase == RunPhase.Prep || state.phase == RunPhase.ChapterEnd) { var p = machine.AddPlayer(key, other.NickName, -1, -1); if (p != null) Notify(new RogueEventMessage { kind = "log", text = "{0} joined the squad.|" + p.name }); }
        StartCoroutine(SendSnapshotWhenReady(other.ID));
        Broadcast();
        ResendInvulnerable();
    }

    IEnumerator SendSnapshotWhenReady(int playerId)
    {
        yield return new WaitForSeconds(2f);   // the joiner's scene and controller need a moment
        if (transport != null && state != null) transport.SendSnapshot(RogueSaveStore.ToJson(state), playerId);
    }

    void OnMasterClientSwitched(PhotonPlayer newMaster)
    {
        if (!RoguelikeMode.Coop || state == null) return;
        // a run that already ended has nothing left to decide; the result screen must not turn into "you are the host now" (X002)
        if (state.phase == RunPhase.Ended || endedHandled) return;
        RogueBodyShield.ReleaseAllLocal();   // QA-44: the new authority has no claims; every copy puts its bodies down
        if (!PhotonNetwork.isMasterClient) { Banner(T("Host changed: waiting for the new host..."), 3f); return; }
        // We are the new authority: rebuild the machine from the last replicated state. A stage in progress cannot be
        // continued faithfully (spawn ownership and slot payments moved), so the squad returns to the safe node.
        state.authorityEpoch++;
        machine = new RunMachine(state);
        foreach (var p in state.players) p.connected = PhotonPlayerByKey(p.key) != null;
        if (state.phase == RunPhase.Combat || state.phase == RunPhase.Cleared || state.phase == RunPhase.Reward)
        {
            foreach (var role in new List<RogueEnemyRole>(liveEnemies.Values)) if (role != null) { machine.EnemyCancelled(role.InstanceId); DespawnEnemyEverywhere(role); }
            liveEnemies.Clear();
            DisposeEvents();
            stageEnding = false; objectiveDone = false; nextWave = 0; pacing = new RoguePacing();
            machine.RestartPrepAfterHostChange();
            Notify(new RogueEventMessage { kind = "banner", text = "The host left. Back to the safe node; the stage restarts.", value = 4 });
        }
        else Notify(new RogueEventMessage { kind = "banner", text = "You are the host now.", value = 3 });
        WriteCheckpoint();
        Broadcast();
    }

    static PhotonPlayer PhotonPlayerByKey(string key)
    {
        if (PhotonNetwork.playerList == null) return null;
        foreach (var p in PhotonNetwork.playerList) if (KeyOf(p) == key) return p;
        return null;
    }

    /// <summary>Co-op: the host's checkpoint is resumed only when every player in the room is part of it.</summary>
    RunSaveDocument CoopResumeCandidate(List<string> roomKeys)
    {
        if (!RogueSaveStore.HasCheckpoint()) return null;
        var doc = RogueSaveStore.ReadCheckpoint();
        if (doc == null || doc.run == null) return null;
        return MatchSavedPlayers(doc.run, roomKeys, false).Count == roomKeys.Count ? doc : null;
    }

    /// <summary>
    /// Pairs room keys with saved roster entries. Keys are nickname#actor number, and the actor number changes when a player
    /// joins a new room or rejoins after a disconnect, so a key that is not in the roster takes an unclaimed entry with the same
    /// nickname; players sharing a nickname are paired in actor-number order. onlyDisconnected limits candidates to entries
    /// whose player is away (a rejoin during a run).
    /// </summary>
    static Dictionary<string, RunPlayer> MatchSavedPlayers(RunState run, List<string> roomKeys, bool onlyDisconnected)
    {
        var map = new Dictionary<string, RunPlayer>();
        var taken = new HashSet<RunPlayer>();
        foreach (var k in roomKeys) { var p = run.Player(k); if (p != null) { map[k] = p; taken.Add(p); } }
        var byNick = new Dictionary<string, List<string>>();
        foreach (var k in roomKeys) if (!map.ContainsKey(k)) { var n = NickOf(k); if (!byNick.ContainsKey(n)) byNick[n] = new List<string>(); byNick[n].Add(k); }
        foreach (var pair in byNick)
        {
            var saved = new List<RunPlayer>();
            foreach (var p in run.players) if (!taken.Contains(p) && NickOf(p.key) == pair.Key && (!onlyDisconnected || !p.connected)) saved.Add(p);
            pair.Value.Sort((a, b) => ActorOf(a).CompareTo(ActorOf(b)));
            saved.Sort((a, b) => ActorOf(a.key).CompareTo(ActorOf(b.key)));
            for (int i = 0; i < pair.Value.Count && i < saved.Count; i++) { map[pair.Value[i]] = saved[i]; taken.Add(saved[i]); }
        }
        return map;
    }

    /// <summary>Moves matched roster entries onto the players' current keys.</summary>
    static void AdoptKeys(Dictionary<string, RunPlayer> map) { foreach (var pair in map) pair.Value.key = pair.Key; }

    static string NickOf(string key) { int i = key.LastIndexOf('#'); return i < 0 ? key : key.Substring(0, i); }
    static int ActorOf(string key) { int i = key.LastIndexOf('#'); int n; return i >= 0 && int.TryParse(key.Substring(i + 1), out n) ? n : int.MaxValue; }

    // ---------------------------------------------------------------- life replication (every client)
    void ApplyLives()
    {
        if (state == null) return;
        foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
        {
            var rp = go.GetComponent<RoguePlayer>();
            if (rp == null) continue;
            var p = state.Player(RogueWorld.KeyOf(go));
            if (p != null) rp.ApplyLife(p.life, state.authorityEpoch);
        }
        EnsureLocalPlayerAlive();
    }

    /// <summary>
    /// Authority: a rescuer reports held seconds; three seconds of holding in reach revives the victim. Revive progress lives here
    /// and reaches every client as "revprog" events (the victim's HUD, and the victim's RoguePlayer.NoteReviveProgress, which
    /// stops the crawl while it is being revived, QA-33). A revive that has started keeps its progress through a break of up to
    /// RoguePlayer.ReviveResetSeconds and is measured with the same extra reach as the rescuer's prompt, so the authority never
    /// refuses a hold that the rescuer still sees as valid.
    /// </summary>
    void ReviveHold(string rescuer, string victim, float seconds)
    {
        var r = state.Player(rescuer); var v = state.Player(victim);
        if (r == null || v == null || rescuer == victim || !r.connected || r.life != PlayerLife.Alive || v.life != PlayerLife.Downed) return;
        var ro = RogueWorld.PlayerByKey(rescuer); var vo = RogueWorld.PlayerByKey(victim);
        string key = rescuer + "|" + victim;
        float last; bool reported = reviveLastReport.TryGetValue(key, out last);
        float progress; reviveHold.TryGetValue(key, out progress);
        bool ongoing = reported && progress > 0f && Time.time - last <= RoguePlayer.ReviveResetSeconds;
        // the same reach rule as the rescuer's prompt (to the capsule, with the authority's tolerance, plus the extra reach of a
        // revive in progress), and one rescuer per victim
        if (ro == null || vo == null || !RogueInteraction.AuthorityCanAct(ro) ||
            !RogueInteraction.AuthorityInReach(ro, vo.GetComponent<CharacterController>(), RoguePlayer.ReviveRange, ongoing ? RoguePlayer.ReviveContinueReach : 0f)) return;
        bool inUse; float granted = reviveLedger.Credit(rescuer, "revive:" + victim, seconds, true, out inUse);
        if (inUse) { if (reviveLedger.NoticeDue(rescuer)) Notify(new RogueEventMessage { kind = "denied", playerKey = rescuer, text = "Someone else is using it" }); return; }
        if (!ongoing) reviveHold[key] = 0;   // a new hold, or one interrupted for longer than ReviveResetSeconds
        reviveLastReport[key] = Time.time;
        float held; reviveHold.TryGetValue(key, out held);
        var rr = ro.GetComponent<RoguePlayer>();
        // the authority's own record of the rescuer's build (a teammate's local copy only applied it at spawn)
        var rb = r.build != null ? BuildStats.Compute(r.build) : null;
        // held seconds against the rescuer's own time: the base time divided by its revive speed (BuildStats.ReviveSeconds, QA-32)
        held += granted;
        reviveHold[key] = held;
        var rs = rb != null ? rb : rr != null ? rr.Stats : null;
        float needed = Mathf.Max(0.1f, rs != null ? (float)rs.ReviveSeconds(RoguePlayer.ReviveHoldSeconds) : RoguePlayer.ReviveHoldSeconds);
        float progress01 = Mathf.Clamp01(held / needed);
        Notify(new RogueEventMessage { kind = "revprog", playerKey = victim, text = r.name, value = progress01 });
        // A victim who is the authority itself stops crawling here as well (every other victim through the "revprog" event).
        { var vp = vo.GetComponent<RoguePlayer>(); if (vp != null && vp.IsMine) vp.NoteReviveProgress(progress01); }
        if (held < needed) return;
        reviveHold.Remove(key);
        reviveLedger.Release("revive:" + victim);
        var pay = machine.Rescued(rescuer, victim);
        Notify(new RogueEventMessage { kind = "revived", playerKey = victim, text = r.name, minor = pay.Total });
        MetaRevived(rescuer, victim);
        Broadcast();
    }

    // ---------------------------------------------------------------- transaction effects on the owner (once per txId)
    void ApplyTransactionEffects(string txId, string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || appliedTx.Contains(txId)) return;
        appliedTx.Add(txId);
        var player = FindLocalPlayer();
        var fps = player != null ? player.GetComponent<FPSController>() : null;
        var dr = player != null ? player.GetComponent<DamageReceiver>() : null;
        var rp = player != null ? player.GetComponent<RoguePlayer>() : null;
        if (fps == null || rp == null) return;
        var def = RogueCatalog.Item(itemId);
        if (def == null) return;
        switch (def.Id)
        {
            case "supply.ammo": RogueHooks.RefillAmmo(fps); break;
            case "supply.medkit": rp.RestoreOvershield(1.0); break;   // the shop's shield worth the maximum health (F46); health is untouched
            case "supply.repair": RogueHooks.RefillAmmo(fps); if (dr != null) dr.hitPoints = RogueHooks.PlayerMaxHealth(dr, 1000f * (1f + Menu.myCharacter.defense * 0.1f)); break;
            case "stat.health": if (dr != null) dr.hitPoints += 1000f * (1f + Menu.myCharacter.defense * 0.1f) * 0.12f; break;   // heals the added amount, once
        }
        if (def.Kind == ItemKind.Weapon) StartCoroutine(RogueHooks.EquipWeapon(this, fps, RogueCatalog.WeaponIndexOf(def.Id)));
    }

    // ---------------------------------------------------------------- run end on every client
    void HandleEndedOnClient()
    {
        if (state == null || state.phase != RunPhase.Ended || endedHandled) return;
        endedHandled = true;
        if (IsAuthority) return;   // the authority runs EndRun itself
        StartCoroutine(EndRunPresentation());
    }

    IEnumerator EndRunPresentation()
    {
        leaving = true;
        RogueAudio.Loop("gas_loop", false);
        RogueAudio.Play(state != null && state.end == RunEnd.Evacuated ? "run_evac" : "run_end");
        var meta = RogueSaveStore.ReadMeta();
        if (RogueSave.RecordRunEnd(meta, state, localKey)) RogueSaveStore.WriteMeta(meta);
        MetaRunEnded();
        CleanupSession();
        yield return new WaitForSeconds(1f);
        Multiplayer.end = Menu.network != 0;
        var menuObject = GameObject.Find("Menu");
        if (menuObject != null) menuObject.BroadcastMessage("GameOver", SendMessageOptions.DontRequireReceiver);
    }

    /// <summary>Re-entrant: cancels abilities, downed timers, world effects and screens for every player object; the run state stays for the result.</summary>
    void CleanupSession()
    {
        foreach (var go in GameObject.FindGameObjectsWithTag("Player")) { var rp = go.GetComponent<RoguePlayer>(); if (rp != null) rp.CancelAll(); }
        RogueEnemyRole.ClearOutlines();   // every player's Enemy Sight source, not only the legacy global one
        RogueBodyShield.ReleaseAllLocal();
        DisposeEvents();
        CloseOverview();
        CloseScreens();
        DamageReceiver.invincibility = false;
        RoguelikeMode.RunInProgress = false;
    }

    // ---------------------------------------------------------------- screen dismiss / reopen (Prep only)
    public void DismissScreen()
    {
        if (state == null || state.phase != RunPhase.Prep) return;
        screenDismissed = true;
        CloseScreens();
        Banner(T("Press {0} to reopen the shop", RogueInput.KeyText("Shop")), 2.5f);
    }

    public void ReopenScreen()
    {
        if (!screenDismissed) return;
        screenDismissed = false;
        RefreshScreens();
    }

    public bool ScreenDismissed { get { return screenDismissed; } }

    /// <summary>Client: register an enemy that the authority spawned so the local counts and the kill map agree.</summary>
    public void RegisterEnemy(RogueEnemyRole role)
    {
        if (role == null) return;
        BindEnemyMarkers(role);                       // idempotent; runs on the authority's own copy as well
        if (liveEnemies.ContainsKey(role.InstanceId)) return;
        liveEnemies[role.InstanceId] = role;
        if (role.Elite && role.RoleId != "role.finale") RogueAudio.Play("elite_spawn", 0.8f);
        else if (role.RoleId == "role.finale") RogueAudio.Play("elite_spawn");
        if (!IsAuthority) Singleplayer.enemy++;
    }

    int huntInstance = -1;
    readonly Dictionary<int, bool> pendingInvulnerable = new Dictionary<int, bool>();
    /// <summary>Finale enemies and the Elite Hunt target carry a waypoint on every client; the hunt id arrives by event and may precede the enemy.</summary>
    void BindEnemyMarkers(RogueEnemyRole role)
    {
        if (role.RoleId == "role.finale" && state != null && role.GetComponent<RogueWaypoint>() == null)
        {
            var def = RogueCatalog.Encounter(state.encounter.finaleId);
            RogueWaypoint.Attach(role.gameObject, "Enemy", def != null ? def.Name : "Target", new Color(1f, 0.12f, 0.5f), 2.6f, 4);
        }
        if (huntInstance >= 0 && role.InstanceId == huntInstance && !role.HuntMarked) role.HuntMarked = true;
        bool inv; if (!IsAuthority && pendingInvulnerable.TryGetValue(role.InstanceId, out inv)) role.ApplyInvulnerable(inv);
    }

    /// <summary>Late joiners get the current shield states (the setter only sends on change).</summary>
    void ResendInvulnerable()
    {
        foreach (var e in liveEnemies.Values) if (e != null && e.Invulnerable) Notify(new RogueEventMessage { kind = "inv", index = e.InstanceId, flag = true });
    }

    public void MarkHuntTarget(int instanceId)
    {
        huntInstance = instanceId;
        RogueEnemyRole role;
        if (instanceId >= 0 && liveEnemies.TryGetValue(instanceId, out role) && role != null && !role.HuntMarked) role.HuntMarked = true;
    }
}
