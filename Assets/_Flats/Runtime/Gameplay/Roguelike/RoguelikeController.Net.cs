using System.Collections;
using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

// Co-op hardening: roster changes, authority hand-over, life replication, revive holds,
// transaction effects, run end on every client, and session cleanup. See codex/REVIEW_ADAPTER_P1.md.
public partial class RoguelikeController
{
    bool endedHandled, travelling, screenDismissed;
    readonly Dictionary<string, float> reviveHold = new Dictionary<string, float>();      // "rescuer|victim" -> seconds held
    readonly Dictionary<string, float> reviveLastReport = new Dictionary<string, float>();
    readonly HashSet<string> appliedTx = new HashSet<string>();

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
        if (!PhotonNetwork.isMasterClient) { Banner(T("Host changed: waiting for the new host..."), 3f); return; }
        // We are the new authority: rebuild the machine from the last replicated state. A stage in progress cannot be
        // continued faithfully (spawn ownership and slot payments moved), so the squad returns to the safe node.
        state.authorityEpoch++;
        machine = new RunMachine(state);
        foreach (var p in state.players) p.connected = PhotonPlayerByKey(p.key) != null;
        if (state.phase == RunPhase.Combat || state.phase == RunPhase.Cleared || state.phase == RunPhase.Reward)
        {
            foreach (var role in new List<RogueEnemyRole>(liveEnemies.Values)) if (role != null) { machine.EnemyCancelled(role.InstanceId); Destroy(role.gameObject); }
            liveEnemies.Clear();
            DisposeEvents();
            stageEnding = false; objectiveDone = false; nextWave = 0;
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
        foreach (var k in roomKeys) if (doc.run.Player(k) == null) return null;
        return doc;
    }

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

    /// <summary>Authority: a rescuer reports held seconds; three seconds of continuous, in-range holding revives the victim.</summary>
    void ReviveHold(string rescuer, string victim, float seconds)
    {
        var r = state.Player(rescuer); var v = state.Player(victim);
        if (r == null || v == null || rescuer == victim || !r.connected || r.life != PlayerLife.Alive || v.life != PlayerLife.Downed) return;
        var ro = RogueWorld.PlayerByKey(rescuer); var vo = RogueWorld.PlayerByKey(victim);
        if (ro == null || vo == null || Vector3.Distance(ro.transform.position, vo.transform.position) > RoguePlayer.ReviveRange + 1f) return;
        string key = rescuer + "|" + victim;
        float last; reviveLastReport.TryGetValue(key, out last);
        if (Time.time - last > 1f) reviveHold[key] = 0;   // the hold was interrupted
        reviveLastReport[key] = Time.time;
        float held; reviveHold.TryGetValue(key, out held);
        var rr = ro.GetComponent<RoguePlayer>();
        held += Mathf.Clamp(seconds, 0f, 0.6f) * (rr != null ? (float)rr.Stats.ReviveSpeedMul : 1f);
        reviveHold[key] = held;
        Notify(new RogueEventMessage { kind = "revprog", playerKey = victim, text = r.name, value = Mathf.Clamp01(held / RoguePlayer.ReviveHoldSeconds) });
        if (held < RoguePlayer.ReviveHoldSeconds) return;
        reviveHold.Remove(key);
        var pay = machine.Rescued(rescuer, victim);
        Notify(new RogueEventMessage { kind = "revived", playerKey = victim, text = r.name, minor = pay.Total });
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
            case "supply.medkit": if (dr != null) dr.hitPoints = RogueHooks.PlayerMaxHealth(dr, 1000f * (1f + Menu.myCharacter.defense * 0.1f)); break;
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
        var meta = RogueSaveStore.ReadMeta();
        if (RogueSave.RecordRunEnd(meta, state, localKey)) RogueSaveStore.WriteMeta(meta);
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
        RogueEnemyRole.SetOutlines(false, Vector3.zero, 0);
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
        Banner(T("Press {0} to reopen the shop", FlatsControls.Label("Interact", FlatsControls.UsingGamepad)), 2.5f);
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
