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
        if (!p.build.meta.Empty && (state.depth > 1 || state.phase != RunPhase.Prep)) return;   // a loadout cannot change mid-run
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
        LastReward = RogueMetaStore.Reward(facts);
        if (LastReward != null) StartCoroutine(ShowMetaResult());
    }

    /// <summary>After the shared result screen appears, the meta result (experience, merits, level-up, contributions, next goals) opens on top of it.</summary>
    IEnumerator ShowMetaResult()
    {
        yield return new WaitForSecondsRealtime(1.6f);
        var menuObject = GameObject.Find("Menu");
        var canvas = menuObject != null ? menuObject.GetComponentInParent<Canvas>() : null;
        if (canvas == null) { var any = FindObjectOfType<Canvas>(); canvas = any != null ? any.rootCanvas : null; }
        if (canvas == null || LastReward == null) yield break;
        RogueResultView.Show(canvas.rootCanvas.transform, LastReward, LastContributions ?? new Contribution[0], RogueMetaStore.Current, () => { });
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
        overview.AddStat("Experience", RogueMetaUI.L(MetaText.Level(m.level)), gear,
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
        var facts = MetaRun.Facts(run, key, run.history.Length * 120.0, 0, new string[0], run.heat);
        RogueMetaStore.Reward(facts);
    }
}
