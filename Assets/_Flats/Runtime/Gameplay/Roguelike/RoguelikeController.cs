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
        if (broadcastDue >= 0f && Time.time >= broadcastDue) Broadcast();
        hudRefresh -= Time.deltaTime;
        if (hudRefresh <= 0 && runStarted) { hudRefresh = 0.5f; RefreshHud(); }
        TickOverview();
        TickStageClockBanner();
        TickMusicLayer();
        // a client builds the stage's objective and event world once the intro is over, the same moment the authority starts them
        if (clientWorldPending && !SpawnsHeld) { clientWorldPending = false; if (state != null && state.phase == RunPhase.Combat && !IsAuthority) BuildClientWorld(); }
    }

    // ---------------------------------------------------------------- stage clock (QA-20 ready countdown, QA-19 stage intro)
    /// <summary>What the authority's stage clock is counting: the ready countdown in Prep, or the intro before any spawn in Combat.</summary>
    public enum StageClockKind { None = 0, Countdown = 1, Intro = 2 }
    /// <summary>Seconds of the ready countdown once every connected player is ready (solo, co-op), and of the stage intro before spawns.</summary>
    public const float SoloReadyCountdownSeconds = 3f, CoopReadyCountdownSeconds = 5f, StageIntroSeconds = 2f;
    /// <summary>A running clock is repeated this often, so a client that loaded slowly or rejoined still counts down.</summary>
    public const float StageClockResendSeconds = 1f;
    /// <summary>Set by the HUD when it draws the countdown and the intro itself; the controller then skips its banner fallback.</summary>
    public static bool HudDrawsStageClock;

    StageClockKind clockKind;
    double clockEndsAt;
    float clockDuration, clockResend;
    int clockEpoch, clockShownSecond = -1;
    StageClockKind clockShownKind;
    bool clientWorldPending;

    /// <summary>Shared time base: the Photon server clock in co-op (the same on every client), game time offline.</summary>
    static double ClockNow { get { return Menu.network != 0 && PhotonNetwork.inRoom ? PhotonNetwork.time : (double)Time.time; } }
    /// <summary>to - from, across the wrap of PhotonNetwork.time (2^32 ms).</summary>
    static double ClockDelta(double to, double from)
    {
        const double wrap = 4294967.296;
        double d = to - from;
        if (d > wrap * 0.5) d -= wrap; else if (d < -wrap * 0.5) d += wrap;
        return d;
    }

    float StageClockRemaining() { return clockKind == StageClockKind.None ? 0f : Mathf.Max(0f, (float)ClockDelta(clockEndsAt, ClockNow)); }

    /// <summary>Authority: start, replace or clear (None) the stage clock and tell every client.</summary>
    void SetStageClock(StageClockKind kind, float seconds)
    {
        clockKind = kind;
        clockDuration = kind == StageClockKind.None ? 0f : Mathf.Max(0f, seconds);
        clockEndsAt = ClockNow + clockDuration;
        clockEpoch = state != null ? state.authorityEpoch : 0;
        clockResend = StageClockResendSeconds;
        SendStageClock();
    }

    void SendStageClock()
    {
        // the end time travels as text: JsonUtility may round a double to float precision, which is half a second at server-clock magnitudes
        Notify(new RogueEventMessage { kind = "stageclock", index = (int)clockKind, text = clockEndsAt.ToString("R", System.Globalization.CultureInfo.InvariantCulture), minor = Mathf.RoundToInt(clockDuration * 1000f) });
    }

    void ResendStageClock(float dt)
    {
        clockResend -= dt;
        if (clockResend > 0f) return;
        clockResend = StageClockResendSeconds;
        SendStageClock();
    }

    void ApplyStageClock(RogueEventMessage e)
    {
        double endsAt;
        if (!double.TryParse(e.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out endsAt)) return;
        clockKind = e.index >= 0 && e.index <= 2 ? (StageClockKind)e.index : StageClockKind.None;
        clockEndsAt = endsAt; clockDuration = e.minor / 1000f; clockEpoch = e.epoch;
    }

    /// <summary>
    /// HUD: which stage clock is running for this client, from the authority's replicated clock. Countdown only while the run
    /// is in Prep, Intro only in Combat; None when nothing counts down, the clock is from an earlier host, or it already ran out.
    /// </summary>
    public StageClockKind VisibleStageClock
    {
        get
        {
            if (state == null || clockKind == StageClockKind.None || clockEpoch < state.authorityEpoch || StageClockRemaining() <= 0f) return StageClockKind.None;
            if (clockKind == StageClockKind.Countdown && state.phase != RunPhase.Prep) return StageClockKind.None;
            if (clockKind == StageClockKind.Intro && state.phase != RunPhase.Combat) return StageClockKind.None;
            return clockKind;
        }
    }

    /// <summary>HUD (QA-20): seconds left of the ready countdown (everyone ready, in Prep); 0 when none runs. Un-ready, a join or a
    /// host change clears it on every client.</summary>
    public float CountdownRemaining { get { return VisibleStageClock == StageClockKind.Countdown ? StageClockRemaining() : 0f; } }
    /// <summary>HUD (QA-19): seconds left of the stage intro; enemies, the objective and event timers start when it reaches 0.</summary>
    public float IntroRemaining { get { return VisibleStageClock == StageClockKind.Intro ? StageClockRemaining() : 0f; } }
    /// <summary>HUD: full length (seconds) of the visible clock, for a ring or bar (remaining / duration).</summary>
    public float StageClockDuration { get { return VisibleStageClock == StageClockKind.None ? 0f : clockDuration; } }
    /// <summary>Stage intro running: nothing spawns and a client keeps its objective/event world until it ends (every client, any phase).</summary>
    public bool SpawnsHeld { get { return state != null && clockKind == StageClockKind.Intro && clockEpoch >= state.authorityEpoch && StageClockRemaining() > 0f; } }
    /// <summary>HUD: the translated objective or finale name the intro announces ("" outside combat).</summary>
    public string StageIntroTitle
    {
        get
        {
            if (state == null || state.phase != RunPhase.Combat) return "";
            var def = RogueCatalog.Encounter(state.encounter.IsFinale ? state.encounter.finaleId : state.encounter.objectiveId);
            return def != null ? T(def.Name) : "";
        }
    }

    /// <summary>Every client: the countdown and intro as banners, one per second, unless the HUD draws them (HudDrawsStageClock).</summary>
    void TickStageClockBanner()
    {
        var kind = VisibleStageClock;
        if (kind == StageClockKind.None) { clockShownSecond = -1; clockShownKind = kind; return; }
        int second = Mathf.CeilToInt(StageClockRemaining());
        if (second == clockShownSecond && kind == clockShownKind) return;
        clockShownSecond = second; clockShownKind = kind;
        if (HudDrawsStageClock || second <= 0) return;
        if (kind == StageClockKind.Countdown) Banner(T("Everyone is ready. Starting in {0}...", second), 1.2f);
        else Banner(T("Stage {0}-{1}: {2}\nstart in {3}...", state.Chapter, RogueDepth.StageInChapter(state.depth), StageIntroTitle, second), 1.2f);
    }

    // Language or bindings changed while run UI is up. Rows, hints and prompts are built from already-translated strings (and key
    // names), so what is open is rebuilt now instead of at the next state broadcast.
    void OnPresentationSettingsChanged()
    {
        if (!runStarted || state == null || leaving) return;
        if (screen != null)
        {
            // only an open screen is rebuilt (never opened as a side effect); keep where the player was reading
            float scrollAt = screen.scroll != null ? screen.scroll.verticalNormalizedPosition : 1f;
            RefreshScreens();
            if (screen != null && screen.scroll != null) { Canvas.ForceUpdateCanvases(); screen.scroll.verticalNormalizedPosition = scrollAt; }
        }
        RefreshHud();
        if (overview != null) FillOverview(true);
        briefingRefresh = 0f;   // the mission card and toasts re-read on the next HUD frame
    }

    void OnDestroy()
    {
        FlatsLocalization.Changed -= OnPresentationSettingsChanged;
        FlatsControls.Changed -= OnPresentationSettingsChanged;
        MetaLeftEarly();
        if (Instance == this) Instance = null;
        CleanupWorld();
        RoguelikeMode.RunInProgress = false;
        if (!travelling) RoguelikeMode.PendingResume = null;
        RestoreSendRates();
    }

    // Co-op positions (enemies, teammates) were serialised 10 times a second, PUN's default. RogueEnemyNetSync plays them back
    // 0.15 s behind, so one late or dropped (unreliable) update emptied the buffer and an enemy stopped, then caught up: "stop and
    // go". Twenty updates a second leave three samples inside the same delay. PUN batches every view into one message per tick.
    const int CoopSendRate = 30, CoopSerializeRate = 20;
    static int savedSendRate = -1, savedSerializeRate = -1;

    static void RaiseSendRates()
    {
        if (!RoguelikeMode.Coop || savedSendRate >= 0) return;
        savedSendRate = PhotonNetwork.sendRate; savedSerializeRate = PhotonNetwork.sendRateOnSerialize;
        PhotonNetwork.sendRate = Mathf.Max(savedSendRate, CoopSendRate);
        PhotonNetwork.sendRateOnSerialize = Mathf.Max(savedSerializeRate, CoopSerializeRate);
    }

    static void RestoreSendRates()
    {
        if (savedSendRate < 0) return;
        PhotonNetwork.sendRate = savedSendRate; PhotonNetwork.sendRateOnSerialize = savedSerializeRate;
        savedSendRate = -1; savedSerializeRate = -1;
    }

    IEnumerator Start()
    {
        yield return null;   // let Multiplayer.Awake/Start settle the rule first
        if (!RoguelikeMode.Active) { Destroy(this); yield break; }
        Instance = this;
        FlatsLocalization.Changed += OnPresentationSettingsChanged;
        FlatsControls.Changed += OnPresentationSettingsChanged;
        RaiseSendRates();
        RoguelikeMode.RunInProgress = true;
        RoguePlayer.ResetLastHit();
        localKey = RoguelikeMode.LocalPlayerKey;
        transport = Menu.network == 0 ? (IRogueTransport)new OfflineRogueTransport(this) : new PhotonRogueTransport(GetComponent<PhotonView>());

        // A squad run has no menu-side title card. Without a cover the players watched the empty scene (sky colour, the HUD's waiting
        // state, leftover room buttons) until everyone had spawned and the host's first snapshot arrived; the card holds until then.
        if (Menu.network != 0 && FindObjectOfType<RogueRunTitleCard>() == null)
        {
            string mapName = "";
            foreach (var m in RogueCatalog.Maps) if (m.BuildIndex == UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex) mapName = T(m.Name);
            string difficulty = T(RoguelikeMode.DifficultyNames[Mathf.Clamp(RoguelikeMode.Difficulty, 1, 3)]);
            RogueRunTitleCard.Show(T("SQUAD RUN"), mapName == "" ? difficulty : mapName + "  ·  " + difficulty);
        }

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
            phaseText.resizeTextMaxSize = Mathf.Min(phaseText.resizeTextMaxSize, hudView.bannerMaxFontSize);
            logFontSize = hudView.logFontSize;
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
            if (Menu.network != 0) Menu.SetRunMapProperty(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);   // the room now says "run in progress on this map"
            Broadcast();
        }
        else
        {
            phaseText.enabled = true; phaseText.text = T("Waiting for the host...");
            // a client that joined after the host's first broadcast (a rejoin, a slow load) asks for the state instead of waiting for the next change
            float askAt = Time.realtimeSinceStartup + 1.5f;
            while (state == null)
            {
                if (Time.realtimeSinceStartup >= askAt) { askAt = Time.realtimeSinceStartup + 2f; Command(new RogueCommandMessage { kind = "resync" }); }
                yield return null;
            }
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
            MetaRun.MigrateProgress(state);   // an older checkpoint: progress totals once, and an estimated play time only when none was saved (QA-18)
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
        broadcastDue = -1f;
        if (state == null) return;
        state.eventSeq++;
        if (transport != null && Menu.network != 0) transport.SendSnapshot(RogueSaveStore.ToJson(state), -1);
        OnStateChanged();
    }

    float broadcastDue = -1f;
    int overshieldStage = -1;
    /// <summary>Authority: a state change that can wait a moment (a kill's bounty). Kills in quick succession share one snapshot
    /// instead of sending the whole run state once per kill, which queued up behind the Die and hit messages (F07).</summary>
    public void BroadcastSoon() { if (broadcastDue < 0f) broadcastDue = Time.time + 0.25f; }

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
                    Notify(new RogueEventMessage { kind = "tx", playerKey = cmd.playerKey, text = result.Status + "|" + result.Reason + "|" + result.ItemId + "|" + cmd.tx.txId + "|" + (cmd.tx.remove ? "remove" : ""), minor = result.PaidMinor, value = result.RefundMinor, flag = result.Ok });
                    if (result.Ok) Broadcast();
                }
                break;
            case "route":
                if (hostOnly && machine.ChooseRoute(cmd.index)) { WriteCheckpoint(); Broadcast(); }
                break;
            case "continue":
                if (hostOnly && state.phase == RunPhase.ChapterEnd && !leaving) StartCoroutine(ContinueChapter());
                else if (!hostOnly) Notify(new RogueEventMessage { kind = "denied", playerKey = cmd.playerKey, text = "Only the host can continue or evacuate." });
                break;
            case "evacuate":
                if (hostOnly && !leaving && machine.Evacuate()) StartCoroutine(EndRun());
                else if (!hostOnly) Notify(new RogueEventMessage { kind = "denied", playerKey = cmd.playerKey, text = "Only the host can continue or evacuate." });
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
            case "overshield": machine.ReportOvershield(cmd.playerKey, cmd.value); break;   // only ever lowers the carried fraction
            case "resync": if (transport != null && senderId >= 0) transport.SendSnapshot(RogueSaveStore.ToJson(state), senderId); return;   // a (re)joining client wants the current state now
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
            case RunPhase.Cleared: RogueAudio.Play("stage_clear"); FlatsFeel.StageCleared(); break;
            case RunPhase.Reward: RogueAudio.Play("ui_reward", 0.8f); break;
            case RunPhase.Route: case RunPhase.ChapterEnd: RogueAudio.Play("chapter"); break;
        }
    }

    void OnStateChanged()
    {
        if (state != null) OnPhaseSound(state.phase);
        if (state != null && sceneReady) SetupMusic();   // a new chapter or a finale changes the track
        if (state != null && state.phase == RunPhase.Combat) BuildClientWorld();
        else if (state != null && !IsAuthority && state.phase != RunPhase.Combat) DisposeEvents();
        if (state != null && state.phase != RunPhase.Prep) screenDismissed = false;
        if (state != null && state.phase != RunPhase.Combat) RogueBodyShield.ReleaseAllLocal();   // QA-44: bodies are only carried in combat
        if (state != null && state.phase != RunPhase.ChapterEnd) chapterDecisionSent = false;
        if (state != null && state.phase != RunPhase.Combat && !IsAuthority) ExtraEnemyDamageMul = 1f;   // the stage's contract ended
        ApplyLives();
        HandleEndedOnClient();
        RefreshHud();
        RefreshScreens();
        var me = LocalPlayer;
        var player = FindLocalPlayer();
        if (me != null && player != null)
        {
            var rp = player.GetComponent<RoguePlayer>();
            if (rp != null)
            {
                rp.ApplyBuild(me.build);
                // a new stage (or a resumed run) rebuilds the shop shield from what the authority carried over
                if (state.phase == RunPhase.Combat && overshieldStage != state.encounterCounter) { overshieldStage = state.encounterCounter; rp.RestoreOvershield(me.overshieldFraction); }
            }
        }
    }

    /// <summary>QA-52: the reinforcement banner names the side the wave arrives from, relative to where this player is looking.</summary>
    void ShowArrivalSide(RogueEventMessage e)
    {
        var cam = Camera.main; if (cam == null) return;
        double bearing = e.value; float x, z; var parts = (e.text ?? "").Split(';');
        if (parts.Length == 2 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x)
            && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z))
            bearing = ArrivalDirector.Bearing(cam.transform.position.x, cam.transform.position.z, x, z);
        Vector3 f = cam.transform.forward; f.y = 0f; if (f.sqrMagnitude < 1e-4f) return;
        double rel = ArrivalDirector.AngleDiff(bearing, ArrivalDirector.Bearing(0, 0, f.x, f.z));   // + = right (clockwise)
        string key = Math.Abs(rel) <= 45 ? "Reinforcements ahead!" : Math.Abs(rel) >= 135 ? "Reinforcements behind you!"
                   : rel < 0 ? "Reinforcements on your left!" : "Reinforcements on your right!";
        Banner(T(key), 2f);
    }

    void ApplyEvent(RogueEventMessage e)
    {
        switch (e.kind)
        {
            case "credit":
                // money credited to one member (index 0 kill bounty, 1 marked-kill bonus): only that member's screen shows it (QA-27)
                if (e.playerKey != localKey || e.minor <= 0) break;
                RogueAudio.Coin();
                if (e.index == 1) Log(T("Marked kill bonus +{0}", RogueMoney.Format(e.minor)));
                else Log(T(e.flag ? "Headshot bounty +{0}" : "Bounty +{0}", RogueMoney.Format(e.minor)) + (string.IsNullOrEmpty(e.text) ? "" : " (" + e.text + ")"));
                ShowPayout(e.minor, e.flag && e.index == 0);
                break;
            case "bounty":   // older authority build: one shared amount
                if (e.minor > 0) Log(T(e.flag ? "Headshot bounty +{0}" : "Bounty +{0}", RogueMoney.Format(e.minor)) + (string.IsNullOrEmpty(e.text) ? "" : " (" + e.text + ")"));
                if (e.minor > 0 && hudView != null) hudView.ShowBounty("+$" + RogueMoney.Format(e.minor) + (e.flag && e.playerKey == localKey ? "  " + T("Headshot") : ""));   // every member is paid the same bounty
                break;
            case "banner": RogueAudio.OnBanner(e.text); Banner(Decode(e.text), (float)(e.value > 0 ? e.value : 3)); break;
            case "arrivaldir": ShowArrivalSide(e); break;   // QA-52: which side a reinforcement wave comes from, relative to this player's view
            case "brief":
                if (!HudDrawsBriefing) { var brief = RogueCatalog.Encounter(e.text); if (brief != null) Banner(T(brief.Brief), (float)(e.value > 0 ? e.value : 4)); }
                break;
            case "log": Log(Decode(e.text)); break;
            case "downed": MetaTeammateDowned(e.playerKey); if (e.playerKey != localKey) RogueAudio.Play("downed_ally"); Log(T("{0} is down!", e.text)); { var rp = RogueHooks.Local; if (rp != null && e.playerKey == localKey) rp.AcknowledgeDown(e.index); } break;
            case "downrefused": { var rp = RogueHooks.Local; if (rp != null && e.playerKey == localKey) rp.RefuseDown(e.index); } break;
            case "died": RogueAudio.Play("died", e.playerKey == localKey ? 1f : 0.6f); Log(T("{0} died.", e.text)); break;
            case "carrydrop": RogueCarryable.ApplyDropEvent(e); break;   // the authority's drop point, the same on every client (F36)
            case "enemymul": if (!IsAuthority) ExtraEnemyDamageMul = e.value > 0 ? (float)e.value : 1f; break;
            case "denied": if (e.playerKey == localKey) { Log(Decode(e.text)); Banner(Decode(e.text), 2f); } break;
            case "revived": RogueAudio.Play("revive"); Log(T(e.flag ? "Emergency revive: {0}" : "Revived: {0}", e.text)); break;
            case "tx": OnTransactionResult(e); break;
            case "rescueshield": MetaRescueShieldEvent(e); break;
            case "ult": RogueAudio.Play("ult_use", e.playerKey == localKey ? 1f : 0.55f); Log(e.text == "" ? T("Ultimate used") : T("Ultimate: {0}", ItemName(e.text))); OnUltimateConfirmed(e); break;
            case "objective": Log(e.text); break;
            case "objtext": ApplyObjectiveText(e.text); break;
            case "stageclock": ApplyStageClock(e); break;
            case "body": RogueBodyShield.ApplyEvent(e); break;   // QA-44 body shield
            case "fx": RogueWorldFx.ApplyFxEvent(e, localKey); break;   // QA-45 a teammate's kill explosion
            case "iprog": RogueInteractable.ApplyProgressEvent(e); break;
            case "dmghp": RogueDamageable.ApplyHealthEvent(e); break;
            case "equip": OnEquipEvent(e); break;
            case "revprog":
                if (e.playerKey == localKey && hudView != null) hudView.SetRevive((float)e.value, T("{0} is reviving you", e.text));
                // every copy of the victim hears it (F17): the owner stops crawling and holds its bleed-out clock (QA-33); other copies mirror "being revived"
                { var revived = RogueWorld.PlayerByKey(e.playerKey); var rp = revived != null ? revived.GetComponent<RoguePlayer>() : null; if (rp != null) rp.NoteReviveProgress((float)e.value); }
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

    /// <summary>Format in English without translating: text the authority replicates, translated by each reader.</summary>
    public static string F(string key, params object[] args) { return args == null || args.Length == 0 ? key : string.Format(key, args); }

    /// <summary>Replicated status text in the local language: the whole line when it is one template, otherwise each
    /// double-space separated segment ("Hold 40%  Go to the zone").</summary>
    public static string Localize(string english)
    {
        if (string.IsNullOrEmpty(english) || !FlatsLocalization.IsChinese) return english ?? "";
        string whole = FlatsLocalization.Translate(english);
        if (whole != english) return whole;
        var parts = english.Split(new[] { "  " }, StringSplitOptions.None);
        for (int i = 0; i < parts.Length; i++) parts[i] = FlatsLocalization.Translate(parts[i]);
        return string.Join("  ", parts);
    }

    /// <summary>Format in English, then translate through the shared table (template keys carry the values).</summary>
    public static string T(string key, params object[] args)
    {
        return FlatsLocalization.Translate(args == null || args.Length == 0 ? key : string.Format(key, args));
    }

    /// <summary>
    /// Solo: the second source of the BGM pair (the combat layer) fades in while a stage is fought and out between stages. The
    /// original modes fade it in Singleplayer's own rule loops; the roguelike rule has none, so the layer stayed silent.
    /// </summary>
    void TickMusicLayer()
    {
        if (Menu.network != 0 || !sceneReady || ambient == null || ambient.Length < 2 || ambient[1] == null || !ambient[1].isPlaying) return;
        // the run phase decides, not Singleplayer.chance: the original modes' own code clears that flag for their own reasons
        float target = state != null && state.phase == RunPhase.Combat ? Menu.mySettings.sound_bgm / 10f : 0f;
        if (!Mathf.Approximately(ambient[1].volume, target)) ambient[1].volume = Mathf.MoveTowards(ambient[1].volume, target, Time.unscaledDeltaTime * 0.8f);
    }

    int musicPair = -1;
    void SetupMusic()
    {
        if (state != null && state.phase == RunPhase.Ended) return;   // the result screen stopped the music: it stays stopped
        var sp = GetComponent<Singleplayer>();
        if (ambient == null || ambient.Length < 2) return;
        if (Menu.network == 0 && sp != null && sp.singleplayerBGM != null && sp.singleplayerBGM.Length >= 2)
        {
            // The authored BGM pairs (a base track and its combat layer) rotate by chapter, and a finale plays the next pair, so a
            // run does not loop one track. Both sources restart together to stay in step.
            int pairs = sp.singleplayerBGM.Length / 2;
            int pair = state != null ? (Mathf.Max(1, state.Chapter) - 1 + (state.IsFinaleStage ? 1 : 0)) % pairs : 0;
            if (pair == musicPair && ambient[0].isPlaying) return;
            musicPair = pair;
            float layer = ambient[1].isPlaying ? ambient[1].volume : 0f;
            ambient[0].clip = sp.singleplayerBGM[pair * 2]; ambient[1].clip = sp.singleplayerBGM[pair * 2 + 1];
            ambient[1].volume = layer;
            foreach (var a in ambient) if (a.clip != null) a.Play();
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
        RecordElapsed();   // a resumed run continues from the play time saved here
        machine.MarkCheckpoint();
        if (!RogueSaveStore.WriteCheckpoint(state, localKey)) Log(T("Checkpoint failed: {0}", RogueSaveStore.LastError));
    }

    /// <summary>Every exit path ends here: abilities, effects, timers and world objects are released.</summary>
    void CleanupWorld()
    {
        StopAllCoroutines();
        DisposeEvents();
        RogueBodyShield.ReleaseAllLocal();
        foreach (var e in liveEnemies.Values) if (e != null) { /* scene objects are destroyed with the scene */ }
        liveEnemies.Clear();
        CloseOverview();
        CloseScreens();
        RoguePlayer.ResetLocalStatics();
        RoguelikeMode.RunInProgress = false;
    }
}
