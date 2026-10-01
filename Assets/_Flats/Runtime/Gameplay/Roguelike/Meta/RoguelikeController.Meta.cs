using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Flats.Core.Roguelike;
using UnityEngine;

// Meta layer of the run controller: every player's out-of-run loadout enters the run through the
// authority (validated, then replicated in PlayerBuild.meta), Heat and Starter Kit are applied at
// creation, and the run end turns authoritative facts into the local profile's reward exactly once.
public partial class RoguelikeController
{
    float metaRunStartedAt;
    bool metaRewarded;
    public RunReward LastReward { get; private set; }
    public Contribution[] LastContributions { get; private set; }

    /// <summary>Authority, right after a new run was created: local loadout (solo/host), heat, starter kit, profile bookkeeping.</summary>
    void MetaOnRunCreated(bool resumed)
    {
        metaRunStartedAt = Time.time; metaRewarded = false;
        if (state == null) return;
        if (!resumed)
        {
            // solo: the Heat picked on the Roguelike page; co-op: the host's selected Heat (never above what the host unlocked)
            var host = RogueMetaStore.Current;
            state.heat = RogueHeat.Clamp(Menu.network == 0 ? RoguelikeMode.Heat : Math.Min(host.lastHeat, host.heatUnlocked));
            RoguelikeMode.Heat = state.heat;
            var mine = state.Player(localKey);
            if (mine != null) SetLoadout(mine, MetaProfiles.ToLoadout(RogueMetaStore.Current));
            RogueMetaStore.NoteRunStarted(state.runId);
        }
        MetaSendLocalLoadout();
    }

    /// <summary>Every client (the authority included) declares its own loadout once it knows the run.</summary>
    void MetaSendLocalLoadout()
    {
        if (Menu.network == 0 || state == null) return;
        var loadout = MetaProfiles.ToLoadout(RogueMetaStore.Current);
        Command(new RogueCommandMessage { kind = "meta", text = JsonUtility.ToJson(loadout) });
    }

    /// <summary>Authority: a player's declared loadout. Accepted before their first combat only; sanitized; replicated.</summary>
    void MetaLoadoutCommand(RogueCommandMessage cmd)
    {
        var p = state != null ? state.Player(cmd.playerKey) : null;
        if (p == null) return;
        // a roster entry without a build (incomplete or legacy state) cannot take a loadout; one without a meta loadout has none yet,
        // which is what "empty" means here (SetLoadout below already treats a missing meta as the first loadout)
        if (p.build == null) { Debug.LogWarning("FLATS_ROGUE_LOADOUT_REJECTED " + p.key + ": the player has no build"); return; }
        if (p.build.meta != null && !p.build.meta.Empty && (state.depth > 1 || state.phase != RunPhase.Prep)) return;   // a loadout cannot change mid-run
        MetaLoadout declared = null;
        try { declared = JsonUtility.FromJson<MetaLoadout>(cmd.text); } catch (System.Exception) { }
        var errors = new List<string>();
        var clean = MetaProfiles.SanitizeLoadout(declared, errors);
        if (errors.Count > 0) Debug.LogWarning("FLATS_ROGUE_LOADOUT_REPAIRED " + p.key + ": " + string.Join(", ", errors.ToArray()));
        SetLoadout(p, clean);
        Broadcast();
    }

    /// <summary>Applies a loadout to a run player: armory weapons become the build's weapon models; Starter Kit mods are granted once.</summary>
    void SetLoadout(RunPlayer p, MetaLoadout loadout)
    {
        bool first = p.build.meta == null || p.build.meta.Empty;
        p.build.meta = loadout;
        var pw = RogueArmory.Weapon(loadout.primary); var sw = RogueArmory.Weapon(loadout.secondary);
        if (pw != null) p.build.primaryWeapon = pw.BaseModel;
        if (sw != null) p.build.secondaryWeapon = sw.BaseModel;
        if (first && IsAuthority && state.depth == 1)
        {
            ulong seed = unchecked((ulong)state.seed ^ (ulong)p.key.GetHashCode());
            foreach (var id in MetaRun.ApplyStarterMods(p, new RogueRng(seed)))
                Notify(new RogueEventMessage { kind = "log", text = "Starter Kit: {0}|@" + RogueCatalog.Item(id).Name });
        }
    }

    /// <summary>Every client at the run end: facts from the replicated state plus the owner's own tallies, rewarded once.</summary>
    void MetaRunEnded()
    {
        if (metaRewarded || state == null || state.phase != RunPhase.Ended) return;
        metaRewarded = true;
        var runtime = LocalRuntime();
        var facts = MetaRun.Facts(state, localKey, Time.time - metaRunStartedAt, runtime != null ? runtime.MeleeKills : 0, runtime != null ? runtime.WeaponKillEntries() : new string[0], state.heat);
        LastContributions = runtime != null ? runtime.Ledger.Top(3) : new Contribution[0];
        // the reward is applied and saved here, once, before any result screen opens (RogueMetaStore.Reward is idempotent per run id);
        // everything after this line only displays it
        LastReward = RogueMetaStore.Reward(facts);
        string notice = "";
        if (LastReward == null)
        {
            // already rewarded (a replayed end): the statistics still open, with nothing added, so the flow reaches headquarters
            var profile = RogueMetaStore.Current;
            LastReward = new RunReward { runId = facts.RunId, levelBefore = profile.Level, levelAfter = profile.Level };
            notice = "This run's reward was already saved.";
        }
        else if (RogueMetaStore.LastError != null) notice = "Could not save this run's rewards. Your previous progress is unchanged.";
        StartCoroutine(ShowMetaResult(ResultSummary(facts, notice)));
    }

    /// <summary>What the statistics step shows about the finished run (outcome, reach, tallies, build).</summary>
    RogueResultView.RunSummary ResultSummary(RunFacts facts, string notice)
    {
        var me = LocalPlayer;
        return new RogueResultView.RunSummary
        {
            end = state.end, solo = state.players.Length <= 1,
            chapter = state.Chapter, stage = RogueDepth.StageInChapter(state.depth), deepestDepth = state.deepestDepth,
            difficulty = state.difficulty, heat = state.heat,
            kills = facts.Kills, headshots = facts.Headshots, rescues = facts.Rescues, objectives = facts.Objectives, stagesCleared = facts.StagesCleared,
            seconds = facts.Seconds, earnedMinor = me != null ? me.earnedMinor : 0,
            build = me != null && me.build != null ? BuildLine(me.build) : "",
            items = me != null && me.build != null ? BuildItems(me.build) : null,
            modsUsed = me != null && me.build != null ? (me.build.mods ?? new string[0]).Length : -1,
            modsCapacity = me != null && me.build != null ? RogueCatalog.MaxMods : -1,
            newBest = previousBestDepth > 0 && state.deepestDepth > previousBestDepth,
            notice = notice,
            continueLabel = Menu.RogueHeadquartersAfterRun ? "Continue to headquarters" : "Continue",
        };
    }

    /// <summary>The build as the statistics show it: cores, then mods, the tactical and the ultimate, each with its real tier
    /// (tacticals and ultimates have none: -1 hides the number). Names are catalogue keys; the view translates them.</summary>
    static RogueResultView.BuildItem[] BuildItems(PlayerBuild b)
    {
        var items = new List<RogueResultView.BuildItem>();
        System.Action<string, int> add = (id, rank) =>
        {
            if (string.IsNullOrEmpty(id)) return;
            var def = RogueCatalog.Item(id);
            if (def == null) return;
            items.Add(new RogueResultView.BuildItem { itemId = id, kind = def.Kind, name = def.Name, rank = rank });
        };
        foreach (var id in b.cores ?? new string[0]) add(id, b.Tier(id));
        foreach (var id in b.mods ?? new string[0]) add(id, RogueCatalog.MaxTier(id) > 1 ? b.Tier(id) : -1);
        add(b.tactical, -1);
        add(b.ultimate, -1);
        return items.ToArray();
    }

    static string BuildLine(PlayerBuild b)
    {
        var cores = b.cores ?? new string[0];
        var parts = new List<string> { cores.Length > 0 ? string.Join(" + ", cores.Select(ItemName).ToArray()) : T("No core") };
        parts.Add(T("Mods {0}/{1}", (b.mods ?? new string[0]).Length, RogueCatalog.MaxMods));
        if (!string.IsNullOrEmpty(b.tactical)) parts.Add(ItemName(b.tactical));
        if (!string.IsNullOrEmpty(b.ultimate)) parts.Add(ItemName(b.ultimate));
        return T("Build: {0}", string.Join("   ·   ", parts.ToArray()));
    }

    /// <summary>
    /// Result → Stats → Headquarters (QA-46). The shared result page (outcome and reach, Menu.GameOver) comes first and stays alone
    /// for RogueResultView.ResultPageSeconds, or until Back on it asks for the statistics. The statistics never open over a dialog,
    /// over Loadout &amp; Armory or during a transition: they wait until the result page is idle again. Their Continue goes to
    /// headquarters (Menu.ContinueFromRogueStats).
    /// </summary>
    IEnumerator ShowMetaResult(RogueResultView.RunSummary summary)
    {
        RogueResultView.BeginPending(this);
        float deadline = Time.realtimeSinceStartup + 15f, idleSince = -1f, beat = RogueResultView.ResultPageSeconds;
        while (!RogueResultView.ShowRequested)
        {
            float now = Time.realtimeSinceStartup;
            var menu = Menu.Current;
            if (menu != null && menu.RogueResultIdle) { if (idleSince < 0f) idleSince = now; if (now - idleSince >= beat) break; }
            else if (idleSince < 0f && now >= deadline && Menu.current != "RogueMetaHub") break;   // the result page never came: show them anyway
            yield return null;
        }
        var menuObject = GameObject.Find("Menu");
        var canvas = menuObject != null ? menuObject.GetComponentInParent<Canvas>() : null;
        if (canvas == null) { var any = FindObjectOfType<Canvas>(); canvas = any != null ? any.rootCanvas : null; }
        if (canvas == null || LastReward == null) { RogueResultView.EndPending(); yield break; }
        RogueResultView.Show(canvas.rootCanvas.transform, LastReward, LastContributions ?? new Contribution[0], RogueMetaStore.Current, summary,
            () => { var m = Menu.Current; if (m != null) m.ContinueFromRogueStats(); });
    }

    /// <summary>A co-op client leaving before the end keeps the reduced "left early" reward of what it played.</summary>
    void MetaLeftEarly()
    {
        if (metaRewarded || state == null || state.phase == RunPhase.Ended || travelling || Menu.network == 0) return;
        metaRewarded = true;
        var runtime = LocalRuntime();
        var copy = RogueSaveStore.FromJson<RunState>(RogueSaveStore.ToJson(state));
        copy.end = RunEnd.Abandoned; copy.phase = RunPhase.Ended;
        var facts = MetaRun.Facts(copy, localKey, Time.time - metaRunStartedAt, runtime != null ? runtime.MeleeKills : 0, runtime != null ? runtime.WeaponKillEntries() : new string[0], state.heat);
        RogueMetaStore.Reward(facts);
    }

    /// <summary>Authority, after a revive: a rescuer with Rescue Shield shields both players (event to every client; each owner applies its own).</summary>
    void MetaRevived(string rescuerKey, string victimKey)
    {
        var rescuer = state != null ? state.Player(rescuerKey) : null;
        if (rescuer == null) return;
        var stats = BuildStats.Compute(rescuer.build);
        if (stats.RescueShieldPoints <= 0) return;
        Notify(new RogueEventMessage { kind = "rescueshield", playerKey = rescuerKey, text = victimKey, value = stats.RescueShieldPoints, index = (int)Math.Round(stats.RescueShieldSeconds * 1000) });
    }

    void MetaRescueShieldEvent(RogueEventMessage e)
    {
        if (e.playerKey != localKey && e.text != localKey) return;
        var rt = LocalRuntime();
        if (rt != null) rt.GrantRescueShield((float)e.value, e.index / 1000f);
    }

    /// <summary>A teammate went down: Squad Link fires for everyone else who learned it.</summary>
    void MetaTeammateDowned(string downedKey)
    {
        if (downedKey == localKey) return;
        var rt = LocalRuntime();
        if (rt != null) rt.TriggerSquadLink();
    }

    /// <summary>Squad tab row per player: meta level, armory loadout, strongest branches and the fairness note
    /// (enemies scale with the squad average; the lowest levels get catch-up experience).</summary>
    void MetaSquadRow(RunPlayer p)
    {
        var m = p.build != null ? p.build.meta : null;
        if (m == null || overview == null) return;
        int highest = MetaRun.SquadHighestLevel(state);
        string gear = string.Join(" · ", new[] { m.primary, m.secondary, m.melee }.Where(id => !string.IsNullOrEmpty(id)).Select(id => RogueMetaUI.T(MetaProfiles.ArmoryName(id))).ToArray());
        string branches = string.Join("   ", ((SkillBranch[])Enum.GetValues(typeof(SkillBranch))).Where(b => SkillTree.SpentIn(m.skills, b) > 0)
            .OrderByDescending(b => SkillTree.SpentIn(m.skills, b)).Select(b => T(b.ToString()) + " " + SkillTree.SpentIn(m.skills, b)).ToArray());
        // the loadout names three weapons: in the value column it wrapped over the level bar, so it has its own row
        if (gear.Length > 0) overview.AddStat("Fire", T("Armory loadout"), "", gear, -1, TintInk);
        overview.AddStat("Experience", RogueMetaUI.L(MetaText.Level(m.level)), "",
            (branches.Length > 0 ? branches + "   " : "") + RogueMetaUI.L(MetaText.Fairness(m.level, highest)), highest > 0 ? m.level / (float)Math.Max(highest, 1) : -1, TintBlue);
    }

    static RogueMetaRuntime LocalRuntime()
    {
        var go = FindLocalPlayer();
        return go != null ? go.GetComponent<RogueMetaRuntime>() : null;
    }

    /// <summary>A solo checkpoint that is discarded for a new run was abandoned: reward what the checkpoint recorded (40%).</summary>
    public static void RewardDiscardedCheckpoint(RunSaveDocument doc)
    {
        if (doc == null || doc.run == null) return;
        var run = doc.run;
        run.end = RunEnd.Abandoned; run.phase = RunPhase.Ended;
        string key = run.Player(RoguelikeMode.LocalPlayerKey) != null ? RoguelikeMode.LocalPlayerKey : doc.localPlayerKey;
        // the saved play time (RecordElapsed); only a checkpoint from before it existed gets the per-stage estimate (MigrateProgress),
        // never the length of the 12-entry recency history, which capped a long run at 24 minutes (QA-18)
        MetaRun.MigrateProgress(run);
        var facts = MetaRun.Facts(run, key, run.elapsedSeconds, 0, new string[0], run.heat);
        RogueMetaStore.Reward(facts);
    }
}
