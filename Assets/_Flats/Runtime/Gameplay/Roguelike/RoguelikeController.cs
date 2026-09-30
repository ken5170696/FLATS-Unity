using System;
using System.Collections;
using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives one Roguelike Survival run inside a map scene. Lives on the SingleplayerController
/// prefab (which already carries a scene PhotonView in every map) and destroys itself when the
/// mode is not active. The authority (offline, or the Photon master) owns RunMachine; other
/// clients mirror RunState from snapshots and send commands. See DESIGN.md sections 2 and 5.
/// </summary>
public partial class RoguelikeController : MonoBehaviour
{
    public static RoguelikeController Instance { get; private set; }

    RunMachine machine;
    RunState state;
    IRogueTransport transport;
    string localKey = "local";
    int lastAppliedSeq;
    bool sceneReady, runStarted, leaving;

    // scene composition (found by the same names legacy modes use)
    Transform spawnPoints;
    Text phaseText, scoreText;
    Transform hud, logs;
    Menu menu;
    AudioSource[] ambient;

    public RunState State { get { return state; } }
    public RunMachine Machine { get { return machine; } }
    public bool IsAuthority { get { return transport != null && transport.IsAuthority; } }
    public string LocalKey { get { return localKey; } }
    public RunPlayer LocalPlayer { get { return state != null ? state.Player(localKey) : null; } }
    public bool Ready { get { return runStarted && state != null; } }

    void Awake()
    {
        ExtraEnemyDamageMul = 1f;
    }

    float hudRefresh;
    void Update()
    {
        // live counters (enemies alive, ultimate charge) change without a state broadcast
        hudRefresh -= Time.deltaTime;
        if (hudRefresh <= 0 && runStarted) { hudRefresh = 0.5f; RefreshHud(); }
        TickOverview();
        TickPrompt();
    }

    void OnDestroy()
    {
        MetaLeftEarly();
        if (Instance == this) Instance = null;
        CleanupWorld();
        RoguelikeMode.RunInProgress = false;
        if (!travelling) RoguelikeMode.PendingResume = null;
    }

    IEnumerator Start()
    {
        yield return null;   // let Multiplayer.Awake/Start settle the rule first
        if (!RoguelikeMode.Active) { Destroy(this); yield break; }
        Instance = this;
        RoguelikeMode.RunInProgress = true;
        RoguePlayer.ResetLastHit();
        localKey = RoguelikeMode.LocalPlayerKey;
        transport = Menu.network == 0 ? (IRogueTransport)new OfflineRogueTransport(this) : new PhotonRogueTransport(GetComponent<PhotonView>());

        // Scene objects: wait for the shared interface exactly like Singleplayer.Start does.
        while (GameObject.Find("SpawnPoints") == null || GameObject.Find("Score") == null || GameObject.Find("Message") == null || GameObject.Find("Menu") == null)
            yield return null;
        spawnPoints = GameObject.Find("SpawnPoints").transform;
        scoreText = GameObject.Find("Score").GetComponent<Text>();
        hud = scoreText.transform.parent;
        logs = hud.GetChild(13);
        phaseText = GameObject.Find("Message").transform.GetChild(0).GetComponent<Text>();
        menu = GameObject.Find("Menu").GetComponent<Menu>();
        var ambientObject = GameObject.Find("Ambient");
        if (ambientObject != null) ambient = ambientObject.GetComponents<AudioSource>();
        sceneReady = true;
        SetupMusic();
        hudView = RogueHudView.Open(hud);
        if (hudView != null)
        {
            scoreText.enabled = false;   // the roguelike HUD replaces the legacy score line
            // the shared centre banner (Message/Text, best fit up to 30) and the top-right log feed (Arial 20) were sized for a HUD with
            // nothing else on screen; next to the roguelike panels they read as oversized, so both step down while this HUD is up
            phaseText.resizeTextMaxSize = Mathf.Min(phaseText.resizeTextMaxSize, 22);
            logFontSize = 15;
        }

        // Wait for the local player: legacy Singleplayer/Multiplayer Start spawns Flatman.
        float wait = 0;
        while (FindLocalPlayer() == null && wait < 30f) { wait += Time.deltaTime; yield return null; }

        if (IsAuthority)
        {
            if (Menu.network != 0)
            {
                // every human must have spawned before the run is created, so the roster is complete
                float roster = 0;
                while (roster < 60f && PhotonNetwork.room != null && GameObject.FindGameObjectsWithTag("Player").Length < PhotonNetwork.room.PlayerCount) { roster += Time.deltaTime; yield return null; }
            }
            CreateOrResumeRun();
            Broadcast();
        }
        else
        {
            phaseText.enabled = true; phaseText.text = T("Waiting for the host...");
            while (state == null) yield return null;
            metaRunStartedAt = Time.time; MetaSendLocalLoadout(); // client
        }
        runStarted = true;
        if (Menu.network != 0)
        {
            // one line per client: a local key missing from the run's roster means this client would read someone else's shop and wallet
            var roster = new List<string>(); foreach (var p in state.players) roster.Add(p.key);
            Debug.Log("FLATS_ROGUE_ROSTER local=" + localKey + " found=" + (state.Player(localKey) != null) + " roster=" + string.Join(",", roster.ToArray()));
        }
        StartCoroutine(RunLoop());
    }

    // ---------------------------------------------------------------- run creation
    void CreateOrResumeRun()
    {
        var resume = RoguelikeMode.PendingResume;
        RoguelikeMode.PendingResume = null;
        var keys = new List<string>(); var names = new List<string>(); var primaries = new List<int>(); var secondaries = new List<int>();
        if (Menu.network == 0)
        {
            keys.Add(localKey); names.Add(Menu.myCharacter != null ? Menu.myCharacter.name : "Flatman");
            primaries.Add(Menu.myCharacter != null ? Menu.myCharacter.primaryWeapon : -1); secondaries.Add(Menu.myCharacter != null ? Menu.myCharacter.secondaryWeapon : -1);
        }
        else
        {
            foreach (var p in PhotonNetwork.playerList)
            {
                keys.Add(KeyOf(p)); names.Add(p.NickName);
                primaries.Add(-1); secondaries.Add(-1);
            }
        }

        if (resume == null && Menu.network != 0) resume = CoopResumeCandidate(keys);
        if (resume != null && resume.run != null)
        {
            bool fromEarlierVersion = RogueSave.ContentChanged(resume);
            state = resume.run;
            state.authorityEpoch++;
            state.contentHash = RogueCatalog.ContentHash();   // an update that kept every id resumes; later checkpoints carry this build's hash
            if (Menu.network == 0 && !string.IsNullOrEmpty(resume.localPlayerKey) && resume.localPlayerKey != localKey)
            {
                // a co-op checkpoint continued alone: the saved local player becomes "local", everyone else is offline
                var mine = state.Player(resume.localPlayerKey) ?? (state.players.Length > 0 ? state.players[0] : null);
                if (mine != null) mine.key = localKey;
            }
            if (Menu.network != 0) AdoptKeys(MatchSavedPlayers(state, keys, false));   // a co-op run continued in a new room: actor numbers changed
            foreach (var p in state.players) { p.connected = keys.Contains(p.key); p.ready = false; if (p.life != PlayerLife.Alive) p.life = PlayerLife.Alive; }
            machine = new RunMachine(state);
            for (int i = 0; i < keys.Count; i++) if (state.Player(keys[i]) == null) machine.AddPlayer(keys[i], names[i], primaries[i], secondaries[i]);
            if (state.phase != RunPhase.Prep && state.phase != RunPhase.ChapterEnd) state.phase = RunPhase.Prep;
            Log(T("Run resumed at chapter {0} stage {1}", state.Chapter, RogueDepth.StageInChapter(state.depth)));
            if (fromEarlierVersion) Log(T("Saved by an earlier version: prices and descriptions follow this version."));
        }
        else
        {
            var mapDef = RogueCatalog.MapByScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            long seed = (long)(UnityEngine.Random.value * int.MaxValue) ^ DateTime.UtcNow.Ticks;
            string runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "-" + (seed & 0xffff).ToString("x4");
            state = RunMachine.Create(runId, seed, RoguelikeMode.Difficulty, mapDef != null ? mapDef.Id : "map.flatcity", keys, names, primaries, secondaries);
            machine = new RunMachine(state);
            var meta = RogueSaveStore.ReadMeta(); meta.runsStarted++; RogueSaveStore.WriteMeta(meta);
            // a new run replaces the saved one; an unreadable file is set aside first so this run can checkpoint (co-op hosts start here too)
            if (RogueSaveStore.HasCheckpoint() && RogueSaveStore.ReadCheckpoint() == null) RogueSaveStore.RetireCheckpoint();
            Log(T("Run started: {0}", T(RoguelikeMode.DifficultyNames[state.difficulty])));
        }
        state.mapId = RogueCatalog.MapByScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name) != null
            ? RogueCatalog.MapByScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name).Id : state.mapId;
        MetaOnRunCreated(resume != null && resume.run != null);
        WriteCheckpoint();
    }

    static string KeyOf(PhotonPlayer p)
    {
        return RoguelikeMode.KeyOf(p);
    }

    // ---------------------------------------------------------------- replication
    /// <summary>Authority: push the full state to everyone and refresh local presentation.</summary>
    public void Broadcast()
    {
        if (state == null) return;
        state.eventSeq++;
        if (transport != null && Menu.network != 0) transport.SendSnapshot(RogueSaveStore.ToJson(state), -1);
        OnStateChanged();
    }

    public void Notify(RogueEventMessage e)
    {
        if (state != null) { state.eventSeq++; e.runId = state.runId; e.epoch = state.authorityEpoch; }
        if (transport != null && Menu.network != 0) transport.SendEvent(state != null ? state.eventSeq : 0, RogueSaveStore.ToJson(e));
        ApplyEvent(e);
    }

    [PunRPC]
    void RogueSnapshot(string json, PhotonMessageInfo info)
    {
        if (info.sender != null && !info.sender.IsMasterClient) return;      // only the authority publishes state
        var incoming = RogueSaveStore.FromJson<RunState>(json);
        if (incoming == null) return;
        if (state != null && incoming.authorityEpoch < state.authorityEpoch) return;
        if (state != null && incoming.authorityEpoch == state.authorityEpoch && incoming.eventSeq < state.eventSeq) return;
        state = incoming;
        machine = null;       // clients never simulate
        lastAppliedSeq = Mathf.Max(lastAppliedSeq, state.eventSeq);
        OnStateChanged();
    }

    [PunRPC]
    void RogueEvent(int seq, string json, PhotonMessageInfo info)
    {
        if (info.sender != null && !info.sender.IsMasterClient) return;
        if (seq <= lastAppliedSeq) return;
        var e = RogueSaveStore.FromJson<RogueEventMessage>(json);
        if (e == null) return;
        if (state != null && (e.runId != state.runId || e.epoch < state.authorityEpoch)) return;   // stale authority or another run
        lastAppliedSeq = seq;
        ApplyEvent(e);
    }

    [PunRPC]
    void RogueCommand(string json, PhotonMessageInfo info)
    {
        if (!IsAuthority) return;
        ReceiveCommand(json, info.sender != null ? info.sender.ID : -1);
    }

    /// <summary>Local intent: applied directly when we are the authority, otherwise sent to it.</summary>
    public void Command(RogueCommandMessage cmd)
    {
        if (cmd == null) return;
        cmd.playerKey = localKey;
        if (state != null) { cmd.runId = state.runId; cmd.epoch = state.authorityEpoch; }
        if (IsAuthority) ReceiveCommand(RogueSaveStore.ToJson(cmd), -1);
        else transport.SendCommand(RogueSaveStore.ToJson(cmd));
    }

    /// <summary>Authority: validate and apply a client command; the sender's identity comes from Photon, not the payload.</summary>
    public void ReceiveCommand(string json, int senderId)
    {
        if (!IsAuthority || machine == null) return;
        var cmd = RogueSaveStore.FromJson<RogueCommandMessage>(json);
        if (cmd == null) return;
        if (senderId >= 0)
        {
            var sender = PhotonPlayer.Find(senderId);
            if (sender == null) return;
            cmd.playerKey = KeyOf(sender);          // never trust a self-declared key
        }
        else cmd.playerKey = localKey;
        if (!string.IsNullOrEmpty(cmd.runId) && (cmd.runId != state.runId || cmd.epoch < state.authorityEpoch)) return;   // a command from an older run/authority
        var player = state.Player(cmd.playerKey);
        if (player == null || !player.connected) return;
        bool hostOnly = Menu.network == 0 || cmd.playerKey == localKey;   // route/continue/evacuate belong to the host
        switch (cmd.kind)
        {
            case "ready":
                if (machine.SetReady(cmd.playerKey, cmd.flag)) Broadcast();
                break;
            case "buy":
                if (cmd.tx != null)
                {
                    cmd.tx.playerKey = cmd.playerKey;
                    var result = machine.Buy(cmd.tx);
                    Notify(new RogueEventMessage { kind = "tx", playerKey = cmd.playerKey, text = result.Status + "|" + result.Reason + "|" + result.ItemId + "|" + cmd.tx.txId, minor = result.PaidMinor, flag = result.Ok });
                    if (result.Ok) Broadcast();
                }
                break;
            case "route":
                if (hostOnly && machine.ChooseRoute(cmd.index)) { WriteCheckpoint(); Broadcast(); }
                break;
            case "continue":
                if (hostOnly && state.phase == RunPhase.ChapterEnd) StartCoroutine(ContinueChapter());
                break;
            case "evacuate":
                if (hostOnly && machine.Evacuate()) StartCoroutine(EndRun());
                break;
            case "downed":
                if (machine.PlayerDowned(cmd.playerKey)) { pacing.OnPlayerDowned(state.stageSeconds); Notify(new RogueEventMessage { kind = "downed", playerKey = cmd.playerKey, text = player.name, index = cmd.index }); Broadcast(); CheckWipe(); }
                else Notify(new RogueEventMessage { kind = "downrefused", playerKey = cmd.playerKey, index = cmd.index });
                break;
            case "died":
                if (machine.PlayerDied(cmd.playerKey)) { Notify(new RogueEventMessage { kind = "died", playerKey = cmd.playerKey, text = player.name }); Broadcast(); CheckWipe(); }
                break;
            case "revive":
                ReviveHold(cmd.playerKey, cmd.text, (float)cmd.value);
                break;
            case "ult":
                if ((player.life == PlayerLife.Alive || player.build.ultimate == "ult.emergency_revive") && machine.SpendUltimate(cmd.playerKey))
                {
                    if (player.build.ultimate == "ult.emergency_revive")
                    {
                        var revived = machine.ReviveAll(cmd.playerKey);
                        foreach (var key in revived) Notify(new RogueEventMessage { kind = "revived", playerKey = key, text = player.name, flag = true });
                    }
                    Notify(new RogueEventMessage { kind = "ult", playerKey = cmd.playerKey, text = player.build.ultimate });
                    Broadcast();
                }
                break;
            case "objective":
                OnObjectiveInput(cmd);
                break;
            case "meta": MetaLoadoutCommand(cmd); break;
        }
    }

    // ---------------------------------------------------------------- presentation refresh
    RunPhase soundPhase = RunPhase.Prep; bool soundPhaseKnown;
    /// <summary>Phase stings and the music's second layer (the combat layer of the authored BGM pair) follow the run phase.</summary>
    void OnPhaseSound(RunPhase phase)
    {
        if (soundPhaseKnown && phase == soundPhase) return;
        bool first = !soundPhaseKnown; soundPhaseKnown = true; soundPhase = phase;
        if (Menu.network == 0) Singleplayer.chance = phase == RunPhase.Combat;   // the second BGM source fades in during combat
        if (first) return;
        switch (phase)
        {
            case RunPhase.Combat: RogueAudio.Play("stage_start"); break;
            case RunPhase.Cleared: RogueAudio.Play("stage_clear"); break;
            case RunPhase.Reward: RogueAudio.Play("ui_reward", 0.8f); break;
            case RunPhase.Route: case RunPhase.ChapterEnd: RogueAudio.Play("chapter"); break;
        }
    }

    void OnStateChanged()
    {
        if (state != null) OnPhaseSound(state.phase);
        if (state != null && state.phase == RunPhase.Combat) BuildClientWorld();
        else if (state != null && !IsAuthority && state.phase != RunPhase.Combat) DisposeEvents();
        if (state != null && state.phase != RunPhase.Prep) screenDismissed = false;
        ApplyLives();
        HandleEndedOnClient();
        RefreshHud();
        RefreshScreens();
        var me = LocalPlayer;
        var player = FindLocalPlayer();
        if (me != null && player != null)
        {
            var rp = player.GetComponent<RoguePlayer>();
            if (rp != null) rp.ApplyBuild(me.build);
        }
    }

    void ApplyEvent(RogueEventMessage e)
    {
        switch (e.kind)
        {
            case "bounty":
                if (e.minor > 0) RogueAudio.Coin();
                if (e.minor > 0) Log(T(e.flag ? "Headshot bounty +{0}" : "Bounty +{0}", RogueMoney.Format(e.minor)) + (string.IsNullOrEmpty(e.text) ? "" : " (" + e.text + ")"));
                if (e.minor > 0 && hudView != null) hudView.ShowBounty("+$" + RogueMoney.Format(e.minor) + (e.flag && e.playerKey == localKey ? "  " + T("Headshot") : ""));   // every member is paid the same bounty
                break;
            case "banner": RogueAudio.OnBanner(e.text); Banner(Decode(e.text), (float)(e.value > 0 ? e.value : 3)); break;
            case "log": Log(Decode(e.text)); break;
            case "downed": MetaTeammateDowned(e.playerKey); if (e.playerKey != localKey) RogueAudio.Play("downed_ally"); Log(T("{0} is down!", e.text)); { var rp = RogueHooks.Local; if (rp != null && e.playerKey == localKey) rp.AcknowledgeDown(e.index); } break;
            case "downrefused": { var rp = RogueHooks.Local; if (rp != null && e.playerKey == localKey) rp.RefuseDown(e.index); } break;
            case "died": RogueAudio.Play("died", e.playerKey == localKey ? 1f : 0.6f); Log(T("{0} died.", e.text)); break;
            case "revived": RogueAudio.Play("revive"); Log(T(e.flag ? "Emergency revive: {0}" : "Revived: {0}", e.text)); break;
            case "tx": OnTransactionResult(e); break;
            case "rescueshield": MetaRescueShieldEvent(e); break;
            case "ult": RogueAudio.Play("ult_use", e.playerKey == localKey ? 1f : 0.55f); Log(e.text == "" ? T("Ultimate used") : T("Ultimate: {0}", ItemName(e.text))); OnUltimateConfirmed(e); break;
            case "objective": Log(e.text); break;
            case "objtext": ApplyObjectiveText(e.text); break;
            case "equip": OnEquipEvent(e); break;
            case "revprog":
                if (e.playerKey == localKey)
                {
                    if (hudView != null) hudView.SetRevive((float)e.value, T("{0} is reviving you", e.text));
                    var victim = RogueHooks.Local; if (victim != null) victim.NoteBeingRevived();   // the bleed-out clock pauses while the hold continues
                }
                break;
            case "inv":
                if (!IsAuthority)
                {
                    pendingInvulnerable[e.index] = e.flag;   // remembered until the enemy registers (events can beat the spawn)
                    RogueEnemyRole role; if (liveEnemies.TryGetValue(e.index, out role) && role != null) role.ApplyInvulnerable(e.flag);
                }
                break;
            default:
                if (objectiveRunner != null) objectiveRunner.OnClientEvent(e);
                if (eventRunner != null) eventRunner.OnClientEvent(e);
                if (emergencyRunner != null) emergencyRunner.OnClientEvent(e);
                break;
        }
    }

    // ---------------------------------------------------------------- helpers
    // Called from many per-frame sites (every interactable, carryable, the HUD, the player): the tag search runs once per frame at most.
    static GameObject localPlayerCache; static int localPlayerFrame = -1;
    public static GameObject FindLocalPlayer()
    {
        if (localPlayerFrame == Time.frameCount && localPlayerCache != null && localPlayerCache.activeInHierarchy && localPlayerCache.GetComponent<FPSController>() != null) return localPlayerCache;
        localPlayerFrame = Time.frameCount; localPlayerCache = null;
        foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
        {
            var fps = go.GetComponent<FPSController>();
            if (fps == null) continue;
            if (Menu.network == 0) { localPlayerCache = go; break; }
            var view = go.GetPhotonView();
            if (view != null && view.isMine) { localPlayerCache = go; break; }
        }
        return localPlayerCache;
    }

    public static string ItemName(string id)
    {
        var def = RogueCatalog.Item(id);
        return def != null ? DisplayName(def) : id;
    }

    /// <summary>Player-facing item name: guns use the names the rest of FLATS shows, everything else its translated catalog name.</summary>
    public static string DisplayName(ItemDef def)
    {
        if (def == null) return "";
        if (def.Kind == ItemKind.Weapon) { int model = RogueCatalog.WeaponIndexOf(def.Id); if (model >= 0) return RogueItemKinds.WeaponDisplayName(RogueHooks.MetaWeaponDisplay(model, Flats.Core.WeaponCatalog.GetDefault(model)).gunName); }
        return def.Kind == ItemKind.Weapon ? RogueItemKinds.WeaponDisplayName(def.Name) : T(def.Name);
    }

    /// <summary>Event text travels as "English template|arg|arg"; an arg starting with @ is itself a translatable key. Each client renders its own language.</summary>
    public static string Decode(string packed)
    {
        if (string.IsNullOrEmpty(packed)) return "";
        var parts = packed.Split('|');
        if (parts.Length == 1) return T(parts[0]);
        var args = new object[parts.Length - 1];
        for (int i = 1; i < parts.Length; i++) args[i - 1] = parts[i].StartsWith("@") ? T(parts[i].Substring(1)) : parts[i];
        return T(parts[0], args);
    }

    /// <summary>Format in English, then translate through the shared table (template keys carry the values).</summary>
    public static string T(string key, params object[] args)
    {
        return FlatsLocalization.Translate(args == null || args.Length == 0 ? key : string.Format(key, args));
    }

    void SetupMusic()
    {
        var sp = GetComponent<Singleplayer>();
        if (ambient == null || ambient.Length < 2) return;
        if (Menu.network == 0 && sp != null && sp.singleplayerBGM != null && sp.singleplayerBGM.Length >= 2)
        {
            ambient[0].clip = sp.singleplayerBGM[0]; ambient[1].clip = sp.singleplayerBGM[1];
            ambient[1].volume = 0f;
            foreach (var a in ambient) if (a.clip != null && !a.isPlaying) a.Play();
        }
    }

    public void Log(string text)
    {
        if (logs == null) return;
        var line = Instantiate(Resources.Load("Log")) as GameObject;
        if (line == null) return;
        line.transform.SetParent(logs, false);
        line.transform.SetAsFirstSibling();
        var lineText = line.GetComponent<Text>();
        lineText.text = text;
        if (logFontSize > 0) lineText.fontSize = logFontSize;
    }
    int logFontSize;
    /// <summary>The HUD view, for runners that drive screen-space effects (gas tint).</summary>
    public RogueHudView Hud { get { return hudView; } }

    // Interaction prompts ("Hold E: Repair") are refreshed every frame by the prop the player stands at: they share the banner
    // text but use a timestamp instead of a coroutine, so a frame of prompting costs no allocation. A banner in flight wins.
    string promptText; float promptUntil = -1f;
    public void Prompt(string text)
    {
        if (phaseText == null || string.IsNullOrEmpty(text) || bannerRoutine != null) return;
        if (phaseText.text != text) { phaseText.text = text; }
        if (!phaseText.enabled) phaseText.enabled = true;
        promptText = text; promptUntil = Time.unscaledTime + 0.3f;
    }
    void TickPrompt()
    {
        if (promptText == null || Time.unscaledTime < promptUntil) return;
        if (bannerRoutine == null && phaseText != null && phaseText.text == promptText) { phaseText.text = ""; phaseText.enabled = false; }
        promptText = null;
    }

    Coroutine bannerRoutine;
    public void Banner(string text, float seconds)
    {
        if (phaseText == null) return;
        if (bannerRoutine != null) StopCoroutine(bannerRoutine);
        bannerRoutine = StartCoroutine(BannerRoutine(text, seconds));
    }

    IEnumerator BannerRoutine(string text, float seconds)
    {
        phaseText.enabled = true; phaseText.text = text;
        // firing dismisses a banner early: a long warning must never sit over the crosshair while the player is already acting on it
        float until = Time.time + seconds, minimum = Time.time + 0.6f;
        while (Time.time < until)
        {
            if (Time.time >= minimum && phaseText.text == text && Menu.current == "Playing" && (FlatsControls.Down("Fire") || FlatsControls.PadState("Fire", 1))) break;
            yield return null;
        }
        if (phaseText.text == text) { phaseText.text = ""; phaseText.enabled = false; }
        bannerRoutine = null;
    }

    void WriteCheckpoint()
    {
        if (!IsAuthority || machine == null || !machine.AtCheckpointBoundary()) return;
        machine.MarkCheckpoint();
        if (!RogueSaveStore.WriteCheckpoint(state, localKey)) Log(T("Checkpoint failed: {0}", RogueSaveStore.LastError));
    }

    /// <summary>Every exit path ends here: abilities, effects, timers and world objects are released.</summary>
    void CleanupWorld()
    {
        StopAllCoroutines();
        DisposeEvents();
        foreach (var e in liveEnemies.Values) if (e != null) { /* scene objects are destroyed with the scene */ }
        liveEnemies.Clear();
        CloseOverview();
        CloseScreens();
        RoguePlayer.ResetLocalStatics();
        RoguelikeMode.RunInProgress = false;
    }
}
