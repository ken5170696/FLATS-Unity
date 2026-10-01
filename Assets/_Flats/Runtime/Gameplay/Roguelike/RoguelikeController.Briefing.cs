using System;
using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

// QA-43 mission briefing: the stage intro card (full the first time a player meets an encounter, compact afterwards), the event and
// emergency toasts, the HUD tracker's current step and the guide rows of the TAB overview. The text comes from RogueEncounterGuide
// (Core); this partial only decides what is on screen. Presentation only: nothing here changes the run.
public partial class RoguelikeController
{
    // ---------------------------------------------------------------- "seen" per encounter id (a local preference, not run state)
    /// <summary>Encounter ids this player has already had the full card for, comma separated. A local tutorial flag: it is not part of
    /// the save export (a new device shows each full card once more, which is harmless).</summary>
    public const string BriefingSeenKey = "rogue.briefing.seen.v1";
    static HashSet<string> briefingSeen;

    static HashSet<string> BriefingSeen
    {
        get
        {
            if (briefingSeen != null) return briefingSeen;
            briefingSeen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in FlatsPreferences.GetString(BriefingSeenKey, "").Split(','))
                if (RogueCatalog.Encounter(id.Trim()) != null) briefingSeen.Add(id.Trim());   // unknown or edited entries are dropped
            return briefingSeen;
        }
    }

    public static bool BriefingWasSeen(string id) { return !string.IsNullOrEmpty(id) && BriefingSeen.Contains(id); }

    static void MarkBriefingSeen(string id)
    {
        if (string.IsNullOrEmpty(id) || RogueCatalog.Encounter(id) == null || !BriefingSeen.Add(id)) return;
        var list = new List<string>(BriefingSeen); list.Sort(StringComparer.Ordinal);
        FlatsPreferences.SetString(BriefingSeenKey, string.Join(",", list.ToArray()));
        FlatsPreferences.Save();
    }

    /// <summary>Every encounter shows its full card again (a settings or debug action).</summary>
    public static void ResetBriefingsSeen()
    {
        briefingSeen = new HashSet<string>(StringComparer.Ordinal);
        FlatsPreferences.DeleteKey(BriefingSeenKey);
        FlatsPreferences.Save();
    }

    // ---------------------------------------------------------------- guide text
    static string GuideText(GuideLine line) { return line == null ? "" : T(line.Text, line.Args); }

    EncounterGuide Guide(string id) { return string.IsNullOrEmpty(id) ? null : RogueEncounterGuide.For(id, state != null ? Mathf.Max(1, state.depth) : 1); }

    /// <summary>Icon of an encounter for the HUD lines and the card: the guide's, else the generic one.</summary>
    string GuideIcon(string id, string fallback)
    {
        var g = Guide(id);
        return g != null && !string.IsNullOrEmpty(g.Icon) ? g.Icon : fallback;
    }

    static readonly Color BriefObjectiveTint = new Color(0.8f, 0.098f, 0.392f, 1f), BriefFinaleTint = new Color(0.9f, 0.58f, 0.06f, 1f),
        BriefEventTint = new Color(0.25f, 0.6f, 1f, 1f), BriefEmergencyTint = new Color(0.95f, 0.3f, 0.35f, 1f);

    static Color GuideTint(GuideKind kind)
    {
        switch (kind) { case GuideKind.Finale: return BriefFinaleTint; case GuideKind.Event: return BriefEventTint; case GuideKind.Emergency: return BriefEmergencyTint; default: return BriefObjectiveTint; }
    }

    /// <summary>A control named by a step, as the player's own binding: "Control: [Q]", "Control: [X]", or the on-screen button on a phone.</summary>
    static string ActionHint(string action) { return string.IsNullOrEmpty(action) ? "" : T("Control: {0}", RogueInput.KeyText(action)); }

    string KindCaption(EncounterGuide g)
    {
        string stage = T("Stage {0}-{1}", state.Chapter, RogueDepth.StageInChapter(state.depth));
        switch (g.Kind)
        {
            case GuideKind.Event: return T("Event") + (g.Optional ? " · " + T("Optional") : "");
            case GuideKind.Emergency: return T("Emergency");
            case GuideKind.Finale: return T("Finale") + " · " + stage;
            default: return T("Objective") + " · " + stage;
        }
    }

    string RewardLine(string id, EncounterGuide g)
    {
        long each = state != null ? RogueEncounterGuide.RewardMinorEach(id, state.ledger) : 0;
        if (each <= 0) return "";
        string money = RogueMoney.Format(each);
        return g.RewardIsMinimum ? T("Reward: ${0}+ each", money) : T("Reward: ${0} each", money);
    }

    RogueBriefingView.Content BriefingContent(string id, RogueBriefingView.Mode mode)
    {
        var g = Guide(id); var def = RogueCatalog.Encounter(id);
        var c = new RogueBriefingView.Content();
        if (g == null || def == null) return c;
        c.icon = g.Icon; c.tint = GuideTint(g.Kind);
        c.kind = KindCaption(g);
        c.title = T(def.Name);
        c.goal = GuideText(g.Goal);
        c.steps = new RogueBriefingView.Step[g.Steps.Length];
        for (int i = 0; i < g.Steps.Length; i++)
            c.steps[i] = new RogueBriefingView.Step { icon = g.Steps[i].Icon, text = GuideText(g.Steps[i]), key = string.IsNullOrEmpty(g.Steps[i].Action) ? "" : RogueInput.KeyCap(g.Steps[i].Action) };
        c.currentStep = id == MainEncounterId ? trackerIndex : -1;
        c.watch = GuideText(g.Watch);
        c.tip = mode == RogueBriefingView.Mode.Toast ? "" : GuideText(g.Tip);
        c.reward = RewardLine(id, g);
        // "details" names the Overview binding (keyboard or pad); a phone taps the card itself
        if (RogueInput.IsTouch) { c.detailsKey = ""; c.detailsLabel = T("Tap for details"); }
        else { c.detailsKey = RogueInput.KeyCap("Overview"); c.detailsLabel = T("Details"); }
        return c;
    }

    string MainEncounterId { get { var enc = state != null ? state.encounter : null; return enc == null ? "" : enc.IsFinale ? enc.finaleId : enc.objectiveId; } }

    // ---------------------------------------------------------------- card and toast sequencing (every frame from TickHudFrame)
    string briefStage = "", cardId = "", toastId = "";
    RogueBriefingView.Mode cardMode;
    bool cardActive;
    float cardElapsed, cardSinceIntro, toastElapsed, briefingRefresh;
    readonly List<string> toastQueue = new List<string>();
    readonly HashSet<string> toastsQueued = new HashSet<string>(StringComparer.Ordinal);

    RogueBriefingView BriefingView { get { return hudView != null ? hudView.briefing : null; } }

    /// <summary>The intro card shows the countdown itself; the HUD's stage clock stays hidden meanwhile.</summary>
    bool BriefingDrawsIntro { get { var v = BriefingView; return cardActive && v != null && v.Visible; } }

    /// <summary>The HUD draws the stage's mission (card, tracker): the authority's start-of-stage Brief banner is then redundant on this
    /// client. The "brief" event handler in RoguelikeController reads this.</summary>
    public bool HudDrawsBriefing { get { return BriefingView != null && hudView.HudVisible; } }

    /// <summary>The card or a toast is up: TAB and a tap open the overview on the Run tab, where the full guide is.</summary>
    bool BriefingWantsDetails { get { var v = BriefingView; return v != null && v.Visible; } }

    bool CentreBannerShowing { get { return phaseText != null && phaseText.enabled && !string.IsNullOrEmpty(phaseText.text); } }

    void TickBriefing()
    {
        var view = BriefingView;
        if (view == null) return;
        var enc = state.encounter;
        if (state.phase != RunPhase.Combat || enc == null)
        {
            if (view.Visible) view.HideNow();
            hudView.SetObjectiveSuppressed(false);
            briefStage = ""; cardActive = false; toastId = ""; toastQueue.Clear(); toastsQueued.Clear();
            return;
        }
        string stage = state.runId + "#" + state.encounterCounter;
        if (stage != briefStage)
        {
            briefStage = stage; toastQueue.Clear(); toastsQueued.Clear(); toastId = "";
            cardId = MainEncounterId;
            cardMode = BriefingWasSeen(cardId) ? RogueBriefingView.Mode.Compact : RogueBriefingView.Mode.Full;
            cardElapsed = cardSinceIntro = 0f; briefingRefresh = 1f;
            cardActive = Guide(cardId) != null;
            if (cardActive) view.Show(BriefingContent(cardId, cardMode), cardMode);
        }
        // an event or emergency gets its toast when its status first appears (the moment it is running on every client)
        QueueToast(enc.emergencyId, RawObjectivePart(2));
        QueueToast(enc.eventId, RawObjectivePart(1));

        // time only runs while the HUD is on screen: a card is never used up behind the TAB overview or the pause menu
        bool visible = hudView.HudVisible;
        float dt = visible ? Time.unscaledDeltaTime : 0f;
        briefingRefresh -= dt;
        bool refresh = briefingRefresh <= 0f;
        if (refresh) briefingRefresh = 1f;   // the language, the bindings and the current step can change while it is up

        if (cardActive)
        {
            float intro = IntroRemaining;
            hudView.SetObjectiveSuppressed(true);
            view.SetCountdown(intro > 0f ? T("Enemies arrive in") : "", intro > 0f ? Mathf.CeilToInt(intro).ToString() : "");
            cardElapsed += dt;
            if (intro <= 0f) cardSinceIntro += dt;
            // the full card's lower half and a centre banner share the space above the crosshair: the details step aside for it
            view.ShowDetails(intro > 0f || !CentreBannerShowing);
            if (refresh) view.Refresh(BriefingContent(cardId, cardMode));
            bool full = cardMode == RogueBriefingView.Mode.Full;
            // firing puts the card away at once (playtest 2026-10-01: it covered the view and could not be closed); the objective card keeps the current step
            if (BriefingDismissed(cardElapsed) || (cardElapsed >= (full ? view.fullSeconds : view.compactSeconds) && cardSinceIntro >= (full ? view.fullAfterIntro : view.compactAfterIntro)))
            {
                if (full) MarkBriefingSeen(cardId);
                cardActive = false;
                hudView.SetObjectiveSuppressed(false);
                view.SetCountdown("", "");
                view.Hide(hudView.objectiveRect);   // folds into the objective card, which now shows the current step
            }
            return;
        }
        if (toastId == "" && toastQueue.Count > 0 && !view.Visible)
        {
            toastId = toastQueue[0]; toastQueue.RemoveAt(0); toastElapsed = 0f;
            view.Show(BriefingContent(toastId, RogueBriefingView.Mode.Toast), RogueBriefingView.Mode.Toast);
        }
        if (toastId == "") return;
        toastElapsed += dt;
        if (refresh) view.Refresh(BriefingContent(toastId, RogueBriefingView.Mode.Toast));
        if (toastElapsed >= view.toastSeconds || BriefingDismissed(toastElapsed))
        {
            view.Hide(hudView.EventLineRect(RogueEncounterGuide.KindOf(toastId) == GuideKind.Emergency));
            toastId = "";
        }
    }

    /// <summary>Seconds a card or toast is up before Fire can dismiss it (a shot already in flight must not skip it unread).</summary>
    public const float BriefingDismissAfter = 0.6f;

    /// <summary>The player fired while the card or toast was on screen: put it away.</summary>
    bool BriefingDismissed(float shownFor)
    {
        if (shownFor < BriefingDismissAfter || Menu.current != "Playing" || !FlatsCursor.GameplayInput) return false;
        return FlatsControls.Down("Fire") || FlatsControls.PadState("Fire", 1);
    }

    void QueueToast(string id, string rawStatus)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(rawStatus) || toastsQueued.Contains(id) || Guide(id) == null) return;
        toastsQueued.Add(id);
        // an emergency goes first: it has a clock
        if (RogueEncounterGuide.KindOf(id) == GuideKind.Emergency) toastQueue.Insert(0, id); else toastQueue.Add(id);
    }

    /// <summary>The card, the objective panel (a tap on a phone) or TAB while the card is up: the overview on its Run tab, which holds
    /// the whole guide of the stage's objective, event and emergency.</summary>
    public void OpenBriefingDetails()
    {
        if (state == null || leaving || ConfirmDialogOpen()) return;
        if (Menu.current != "Playing" && Menu.current != "RogueScreen") return;
        if (overview == null) OpenOverview();
        if (overview != null) { FocusObjectiveGuide(); overview.Select(RunTabIndex()); }
    }

    int RunTabIndex()
    {
        if (overview == null || overview.tabs == null || overview.tabs.Length == 0) return 0;
        for (int i = 0; i < overview.tabs.Length; i++) if (overview.tabs[i] != null && overview.tabs[i].key == "Run") return i;
        return overview.tabs.Length - 1;
    }

    // ---------------------------------------------------------------- HUD tracker: the objective card's current step
    int trackerIndex = -1;

    /// <summary>The authority's objective status as sent (English, not yet translated), for reading which state a runner is in.</summary>
    string RawObjectivePart(int i) { var parts = objectiveText.Split('|'); return i < parts.Length ? parts[i] : ""; }

    /// <summary>The objective status for the card's header: the number part (a "Go to the zone" segment moves to the step line).</summary>
    string HeaderProgress(string localized, string raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.IndexOf("  Go to the zone", StringComparison.Ordinal) < 0) return localized;
        return Localize(raw.Replace("  Go to the zone", ""));
    }

    Transform TrackerOrigin()
    {
        var rp = CachedLocalRoguePlayer();
        return rp != null ? rp.transform : null;
    }

    /// <summary>Metres from the local player to the first visible waypoint with this label; below 0 when there is none.</summary>
    float WaypointDistance(string label, out RogueWaypoint found)
    {
        found = null;
        var me = TrackerOrigin();
        if (me == null) return -1f;
        foreach (var wp in RogueWaypoint.All)
            if (wp != null && !wp.Hidden && wp.isActiveAndEnabled && wp.Label == label) { found = wp; return Vector3.Distance(me.position, wp.transform.position); }
        return -1f;
    }

    float WaypointDistance(string label) { RogueWaypoint wp; return WaypointDistance(label, out wp); }

    float DistanceTo(Component target)
    {
        var me = TrackerOrigin();
        return me != null && target != null ? Vector3.Distance(me.position, target.transform.position) : -1f;
    }

    static string WithDistance(string line, float metres) { return metres > 4f ? line + " · " + Mathf.RoundToInt(metres) + " m" : line; }

    string StepLine(EncounterGuide g, int index) { return g != null && index >= 0 && index < g.Steps.Length ? GuideText(g.Steps[index]) : ""; }

    /// <summary>
    /// The current step of the stage's objective as one instruction, with the distance to where it happens when a waypoint marks it.
    /// Read from what every client has: the authority's replicated status line (one English template per runner state), the
    /// replicated ledger (objective paid), the waypoints and the carried crate. index is the guide step it matches, or -1.
    /// </summary>
    string TrackerStep(string id, out string icon, out int index)
    {
        var g = Guide(id);
        index = -1; icon = g != null ? g.Icon : "Objective";
        if (g == null) return "";
        if (state.ledger != null && state.ledger.objectivePaid)
        {
            int left = AliveEnemies;
            icon = "Check";
            return left > 0 ? T("Objective complete: clear the last enemies ({0})", left) : T("Objective complete");
        }
        string raw = RawObjectivePart(0);
        string line = "";
        float metres = -1f;
        switch (id)
        {
            case "obj.clear":
                index = stragglers.Count > 0 ? 1 : 0;
                break;
            case "obj.capture":
                if (raw == "") break;
                metres = WaypointDistance("Capture zone");
                if (raw.IndexOf("Go to the zone", StringComparison.Ordinal) >= 0) index = 0;
                else if (metres > CaptureRunner.Radius) { line = T("Join the others in the zone"); icon = "Arrow"; }
                else { index = 1; metres = -1f; }
                break;
            case "obj.carry":
            {
                if (raw == "") break;
                RogueWaypoint crate;
                float toCrate = WaypointDistance("Supply crate", out crate);
                var carry = crate != null ? crate.GetComponent<RogueCarryable>() : null;
                string holder = carry != null ? carry.HolderKey : "";
                if (string.IsNullOrEmpty(holder)) { index = 0; metres = toCrate; }
                else if (holder == localKey) { index = 1; metres = WaypointDistance("Drop zone"); }
                else
                {
                    var p = state.Player(holder);
                    line = T("Cover {0} to the drop zone", p != null ? p.name : ""); icon = "Squad"; metres = toCrate;
                }
                break;
            }
            case "obj.protect":
                if (raw == "") break;
                if (raw.StartsWith("Device lost", StringComparison.Ordinal)) { line = Localize(raw); icon = "Enemy"; break; }
                metres = WaypointDistance("Protect the device");
                if (metres > 5f) index = 0;
                else { line = T("Hold {0} near it to repair", RogueInput.KeyText("Interact")); icon = "Tap"; index = 1; metres = -1f; }
                break;
            case "obj.breakout":
                if (raw == "") break;
                metres = WaypointDistance("Exit");
                if (raw.StartsWith("Reach the extraction point", StringComparison.Ordinal))
                {
                    if (metres >= 0f && metres <= BreakoutRunner.Radius) { line = T("Wait in the exit zone for the others"); icon = "Exit"; metres = -1f; }
                    index = 0;
                }
                else if (raw.StartsWith("Paused, teammate down", StringComparison.Ordinal)) { index = 2; metres = -1f; }
                else if (raw.StartsWith("Paused", StringComparison.Ordinal)) { line = T("Everyone back into the exit zone"); icon = "Exit"; index = 0; }
                else { line = T("Stay in the zone until the countdown ends"); icon = "Timer"; index = 1; metres = -1f; }
                break;
            case "fin.commander":
                if (raw == "") break;
                if (raw.StartsWith("Commander exposed", StringComparison.Ordinal)) { index = 2; metres = DistanceTo(FindFinaleEnemy()); }
                else index = 0;
                break;
            case "fin.vault":
                if (raw == "") break;
                if (raw.StartsWith("Vault core exposed", StringComparison.Ordinal)) { index = 2; metres = DistanceTo(FindFinaleEnemy()); }
                else
                {
                    index = 0;
                    RogueWaypoint cell = null;
                    foreach (var wp in RogueWaypoint.All)
                        if (wp != null && !wp.Hidden && wp.isActiveAndEnabled && wp.Label != null && wp.Label.StartsWith("Power cell", StringComparison.Ordinal) && (cell == null || wp.Priority > cell.Priority)) cell = wp;
                    string n = cell != null && cell.Label.IndexOf('|') >= 0 ? cell.Label.Substring(cell.Label.IndexOf('|') + 1) : "";
                    if (n != "") { line = T("Hold {0} at power cell {1}", RogueInput.KeyText("Interact"), n); icon = "Battery"; metres = DistanceTo(cell); }
                }
                break;
            case "fin.convoy":
                if (raw == "") break;
                if (raw.StartsWith("Carrier escaped", StringComparison.Ordinal)) { line = Localize(raw); icon = "Enemy"; break; }
                metres = WaypointDistance("Carrier");
                if (raw.StartsWith("Carrier stopped", StringComparison.Ordinal)) index = 2;
                else if (raw.StartsWith("Carrier moving again", StringComparison.Ordinal)) { line = T("Kill escorts before its next stop"); icon = "Enemy"; index = 0; }
                else index = 0;
                break;
        }
        if (line == "" && index >= 0) { line = StepLine(g, index); icon = g.Steps[index].Icon; }
        if (line == "") { line = GuideText(g.Goal); icon = g.Goal.Icon; }
        return WithDistance(line, metres);
    }

    // ---------------------------------------------------------------- TAB overview: the whole guide
    /// <summary>Rows of one encounter's guide on the overview's Run tab: heading with the live status and the goal, each step (the
    /// current one marked, a control step with the player's binding), what to watch out for, a tip and the reward.</summary>
    void AddGuideRows(string id, string heading, string status, Color tint, bool main)
    {
        var def = RogueCatalog.Encounter(id);
        if (def == null) return;
        var g = Guide(id);
        if (g == null) { overview.AddStat(RogueIcons.ForEncounter(id), heading, status, T(def.Brief), -1, tint); return; }
        overview.AddStat(g.Icon, heading, status, GuideText(g.Goal), -1, tint);
        int current = main && state.phase == RunPhase.Combat ? trackerIndex : -1;
        for (int i = 0; i < g.Steps.Length; i++)
        {
            var s = g.Steps[i];
            overview.AddStat(s.Icon, (i + 1) + ".  " + GuideText(s), i == current ? T("Now") : "", ActionHint(s.Action), -1, i == current ? tint : TintInk);
        }
        overview.AddStat("Warning", T("Watch out"), "", GuideText(g.Watch), -1, TintRed);
        overview.AddStat("Star", T("Tip"), "", GuideText(g.Tip), -1, TintGold);
        string reward = RewardLine(id, g);
        if (reward != "" || g.Optional) overview.AddStat("Coin", T("Reward"), reward, g.Optional ? T("Optional: you can skip it") : "", -1, TintGold);
    }
}
