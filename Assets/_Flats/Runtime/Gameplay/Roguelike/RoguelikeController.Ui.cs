using System;
using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.UI;

// HUD binding and the run screens (shop, reward pick, route, chapter end). The HUD and the screen are
// authored prefabs (Resources/UI/Roguelike/RogueHud, RogueScreen) instantiated under the shared canvases;
// rows instantiate the authored RogueOfferRow template. Presentation only: every click becomes a command
// to the authority.
public partial class RoguelikeController
{
    int interactPromptFrame = -1;
    /// <summary>Called every frame an interaction is possible; the touch Interact button shows while this is fresh.</summary>
    public void NoteInteractPrompt() { interactPromptFrame = Time.frameCount; }
    public bool InteractPromptActive { get { return Time.frameCount - interactPromptFrame <= 2; } }

    RogueScreenView screen;
    RogueHudView hudView;
    string screenMode = "";              // "", shop, reward, route, chapterend
    int txCounter;
    readonly Dictionary<string, string> pendingTx = new Dictionary<string, string>();   // txId -> itemId while awaiting the authority
    float bossMaxHp;

    /// <summary>Rescuer-side revive ring (the victim gets its ring from the authority's progress events).</summary>
    public void ShowReviveRing(float fraction, string label) { if (hudView != null) hudView.SetRevive(fraction, label); }

    // ---------------------------------------------------------------- QA-22 interaction feedback (called by the world interaction code)
    /// <summary>A hold interaction the local player is doing (vent, repair device, supply crate, revive): the ring around the
    /// crosshair shows the icon, what is being done and the progress 0..1; below 0 while the authority has not reported progress
    /// yet (the ring turns instead of filling). Call it every frame the hold lasts; the ring hides half a second after the last call.</summary>
    public void ShowInteractionProgress(string iconName, string label, float fraction) { if (hudView != null) hudView.SetInteraction(iconName, label, fraction, ""); }
    /// <summary>The hold stopped without finishing: the ring turns amber and says why (a translated reason), then hides.</summary>
    public void ShowInteractionCancelled(string iconName, string label, float fraction, string reason) { if (hudView != null) hudView.ShowInteractionCancelled(iconName, label, fraction, string.IsNullOrEmpty(reason) ? T("Cancelled") : reason); }
    /// <summary>An interaction finished (hold or instant): a green tick or a red cross with a short text and a cue.</summary>
    public void ShowInteractionResult(bool success, string text) { if (hudView != null) hudView.ShowInteractionResult(success, text); }

    // ---------------------------------------------------------------- QA-27 kill money (called with the local player's exact credit)
    /// <summary>Money credited to the LOCAL player (its own share of a bounty, a marked-kill bonus, a streak): the popup under the
    /// crosshair adds up credits that arrive within a moment of each other, so it always equals what the wallet gained.</summary>
    public void ShowPayout(long minor, bool headshot) { if (hudView != null) hudView.AddPayout(minor, headshot); }

    /// <summary>Per frame from the HUD view: the parts that must move smoothly (ability charge pips, the stage clock).</summary>
    public void TickHudFrame(RogueHudView view)
    {
        if (view == null || view != hudView || state == null || leaving) return;
        RefreshAbilities(LocalPlayer, true);
        TickStageClockUi();
        TickInteractionUi();
        TickBriefing();   // QA-43 intro card and event toasts (RoguelikeController.Briefing)
    }

    // ---------------------------------------------------------------- QA-22 hold interactions (state from RogueInteractionFeedback)
    int interactionSerial = -1;

    /// <summary>
    /// The ring around the crosshair follows the local player's hold interaction: progress while holding (the authority's progress,
    /// turning while none is reported yet), then amber with the reason when the hold stops, a green tick when the target completes,
    /// a red cross when it became unavailable. The prompt on offer and the refusals (RogueInteractionFeedback.CurrentPrompt and
    /// Refusal) show on the HUD's prompt plate under the crosshair while the HUD is visible; HudDrawsInteraction then tells the
    /// banner fallback to stay quiet. Without the authored plate, or behind a screen, the banner keeps them.
    /// </summary>
    void TickInteractionUi()
    {
        bool drawsPrompt = hudView.promptRoot != null && hudView.HudVisible;
        RogueInteractionFeedback.HudDrawsInteraction = drawsPrompt;
        if (drawsPrompt)
        {
            string refusal = RogueInteractionFeedback.Refusal;
            hudView.SetPrompt(RogueInteractionFeedback.Holding ? "" : RogueInteractionFeedback.CurrentPrompt, string.IsNullOrEmpty(refusal) ? "" : Decode(refusal));
        }
        if (interactionSerial < 0) interactionSerial = RogueInteractionFeedback.Serial;   // results from before this HUD existed are old news
        if (RogueInteractionFeedback.Serial != interactionSerial)
        {
            interactionSerial = RogueInteractionFeedback.Serial;
            var last = RogueInteractionFeedback.LastTarget;
            string icon = last != null ? RogueIcons.ForInteraction(last.Action) : "Tap";
            string label = last != null && !string.IsNullOrEmpty(last.Prompt) ? T(last.Prompt) : "";
            string reason = RogueInteractionFeedback.LastReason;
            switch (RogueInteractionFeedback.LastResult)
            {
                case RogueInteractionResult.Succeeded: hudView.ShowInteractionResult(true, label == "" ? T("Done") : T("Done: {0}", label)); break;
                case RogueInteractionResult.Failed: hudView.ShowInteractionResult(false, T(string.IsNullOrEmpty(reason) ? "Unavailable" : reason)); break;
                case RogueInteractionResult.Cancelled: hudView.ShowInteractionCancelled(icon, label, last != null ? last.Progress : 0f, T(string.IsNullOrEmpty(reason) ? "Cancelled" : reason)); break;
            }
        }
        var target = RogueInteractionFeedback.Target;
        if (target != null && RogueInteractionFeedback.Holding)
        {
            float progress = RogueInteractionFeedback.Progress;
            bool measured = RogueInteractionFeedback.HoldSeconds > 0f || progress > 0f;
            hudView.SetInteraction(RogueIcons.ForInteraction(target.Action), string.IsNullOrEmpty(target.Prompt) ? "" : T(target.Prompt), measured ? progress : -1f, "");
        }
        // QA-44: a carried body shield shows what it has left on the same ring (a hold interaction, a revive or a result keeps the
        // ring: SetInteraction never replaces a success or failure on screen, and a carrier cannot start a hold); a hit flashes amber
        float bodyShield = RogueBodyShield.LocalShieldFraction;
        if (bodyShield >= 0f && !RogueInteractionFeedback.Holding)
            hudView.SetInteraction("Shield", T("Body shield"), bodyShield, Time.time - RogueBodyShield.LocalLastHitTime < 0.15f ? T("Hit") : "");
    }

    RoguePlayer abilityPlayer; float abilityPlayerCheck = -10f;
    /// <summary>The local RoguePlayer for per-frame readouts, looked up at most twice a second (the lookup walks every tagged player).</summary>
    RoguePlayer CachedLocalRoguePlayer()
    {
        if (abilityPlayer == null || !abilityPlayer.isActiveAndEnabled || Time.unscaledTime >= abilityPlayerCheck)
        {
            abilityPlayer = LocalRoguePlayer();
            abilityPlayerCheck = Time.unscaledTime + 0.5f;
        }
        return abilityPlayer;
    }

    public delegate RogueOfferRowView OfferSink(string icon, string name, string effect, string price, string rarity, string action, bool interactable, string status, Action onAction);

    // ---------------------------------------------------------------- HUD
    void RefreshHud()
    {
        if (state == null || leaving) return;
        ExpirePendingTx();
        var me = LocalPlayer;
        string money = me != null ? RogueMoney.Format(me.walletMinor) : "0";
        string stage = state.Chapter + "-" + RogueDepth.StageInChapter(state.depth);
        if (hudView == null)
        {
            if (scoreText != null) scoreText.text = "$" + money + "  " + T("Stage {0}-{1}", state.Chapter, RogueDepth.StageInChapter(state.depth)) + "  " + ObjectiveHudText();
            return;
        }
        var enc = state.encounter;
        hudView.SetTop(stage, "$" + money, state.phase == RunPhase.Combat ? AliveEnemies.ToString() : "");
        switch (state.phase)
        {
            case RunPhase.Prep:
                if (CountdownRemaining > 0f) hudView.SetObjective("Timer", T("Everyone is ready"), T("The stage starts in {0} s.", Mathf.CeilToInt(CountdownRemaining)));
                else hudView.SetObjective("Coin", T("Prep"), screenDismissed ? T("Press {0} to reopen the shop", RogueInput.KeyText("Shop")) : T("Shop is open. Ready up when done."));
                break;
            case RunPhase.Combat:
                {
                    string id = enc.IsFinale ? enc.finaleId : enc.objectiveId;
                    var def = RogueCatalog.Encounter(id);
                    string progress = ObjectivePart(0);
                    if (progress == "" && id == "obj.clear")
                    {
                        // Clear Out: the authority counts kills itself; other clients read the replicated plan and ledger (paid or
                        // voided slots). Reinforcements count toward the goal on both sides, as the authority's clear condition does.
                        int done = objectiveKills, needed = objectiveKillsNeeded;
                        if (!IsAuthority && state.ledger != null)
                        {
                            needed = RogueDirector.CountEnemies(enc); done = 0;
                            foreach (var slot in state.ledger.slots) { if (slot.isExtra) needed++; if (slot.paid || slot.cancelled) done++; }
                        }
                        progress = objectiveDone ? T("Cleared") : Mathf.Min(done, needed) + " / " + needed;
                    }
                    // QA-43: the header keeps the number, the line under it says what to do now (and how far it is)
                    string stepIcon; int stepIndex;
                    string step = TrackerStep(id, out stepIcon, out stepIndex);
                    trackerIndex = stepIndex;
                    hudView.SetObjective(GuideIcon(id, RogueIcons.ForEncounter(id)), def != null ? T(def.Name) : T("Objective"), HeaderProgress(progress, RawObjectivePart(0)));
                    hudView.SetObjectiveStep(stepIcon, step);
                    hudView.SetObjectiveProgress(ProgressFraction(progress));
                    break;
                }
            case RunPhase.Cleared: hudView.SetObjective("Check", T("Cleared"), ""); break;
            case RunPhase.Reward: hudView.SetObjective("Coin", T("Pick a reward"), ""); break;
            case RunPhase.Route: hudView.SetObjective("Stage", T("Chapter {0} complete", state.Chapter), T("Choose a route")); break;
            case RunPhase.ChapterEnd: hudView.SetObjective("Coin", T("Chapter shop"), T("Continue or evacuate")); break;
            default: hudView.SetObjective("", "", ""); break;
        }
        bool combat = state.phase == RunPhase.Combat;
        if (!combat) { hudView.SetObjectiveProgress(-1f); hudView.SetObjectiveStep("", ""); trackerIndex = -1; }
        var ev = combat ? RogueCatalog.Encounter(enc.eventId) : null; var em = combat ? RogueCatalog.Encounter(enc.emergencyId) : null;
        hudView.SetEvent(ev != null ? GuideIcon(ev.Id, "Settings5") : "Settings5", EncounterLine(ev, ObjectivePart(1)), false);
        hudView.SetEvent(em != null ? GuideIcon(em.Id, "Warning") : "Warning", EncounterLine(em, ObjectivePart(2)), true);
        // the number in an event's status (a drone's health, a repair's progress) also fills a bar under its line (QA-37)
        hudView.SetEventProgress(false, ev != null ? ProgressFraction(ObjectivePart(1)) : -1f);
        hudView.SetEventProgress(true, em != null ? ProgressFraction(ObjectivePart(2)) : -1f);
        RefreshBoss(combat);
        RefreshStragglers(combat);
        RefreshAbilities(me);   // also every frame from TickHudFrame; here so a state change shows at once
        RefreshSquad(me);
        var rpLocal = LocalRoguePlayer();
        // downed (still rescuable, F49): how long is left and who can help, kept on screen for the whole bleed-out
        if (rpLocal != null && rpLocal.Downed)
        {
            string helper; float metres;
            string line = T("You are down: {0} s left. Crawl to cover.", Mathf.CeilToInt(rpLocal.BleedOutRemaining));
            line += rpLocal.NearestRescuer(out helper, out metres) ? "  " + T("{0} can revive you ({1} m).", helper, Mathf.RoundToInt(metres)) : "  " + T("Nobody is up to revive you.");
            hudView.SetHint("Medkit", line);
        }
        else if (RogueBodyShield.LocalCarried != null)   // QA-44: an enemy body carried as a shield (checked before the generic carry)
            hudView.SetHint("Shield", T("Body shield {0}%: press {1} to put it down. You cannot fire.", Mathf.CeilToInt(RogueBodyShield.LocalCarried.ShieldLeft * 100f), RogueInput.KeyText("Interact")));
        else if (rpLocal != null && rpLocal.Carrying) hudView.SetHint("Crate", T("Carrying: press {0} to put it down. You cannot fire.", RogueInput.KeyText("Interact")));
        // the first stages open with enemies already on the way: say so, or a new player sprints for the marker through them
        else if (combat && state.depth <= 2 && combatSince >= 0 && Time.time - combatSince < 12f) hudView.SetHint("Enemy", T("Enemies are coming: deal with them, then head for the objective."));
        else hudView.SetHint("", "");
    }

    /// <summary>"Name  status", unless the status already opens with the name: an event whose status reads "Alarm cache: optional"
    /// printed "Alarm Cache  Alarm cache: optional" (the name twice) on the objective panel.</summary>
    static string EncounterLine(EncounterDef def, string status)
    {
        if (def == null) return "";
        string name = T(def.Name);
        if (string.IsNullOrEmpty(status)) return name;
        if (status.StartsWith(name, StringComparison.OrdinalIgnoreCase) || status.StartsWith(def.Name, StringComparison.OrdinalIgnoreCase)) return status;
        return name + "  " + status;
    }

    // Clear Out has no prop to walk to: once the waves are thin, the last few enemies get markers so nobody hunts a hidden straggler.
    const string StragglerLabel = "Last enemies";
    const int StragglerCount = 3;
    const float StragglerDelay = 20f;
    readonly List<RogueEnemyRole> stragglers = new List<RogueEnemyRole>();
    float combatSince = -1;

    void RefreshStragglers(bool combat)
    {
        if (!combat) combatSince = -1; else if (combatSince < 0) combatSince = Time.time;
        int alive = AliveEnemies;
        // every objective ends only when the field is clear, so stragglers are marked for all of them (not the finale guard)
        bool show = combat && state.encounter != null && !state.encounter.IsFinale
            && Time.time - combatSince >= StragglerDelay && alive > 0 && alive <= StragglerCount;
        // a straggler that died, despawned or was pooled leaves the list at once, not when the count finally reaches zero (F14)
        for (int i = stragglers.Count - 1; i >= 0; i--)
        {
            var e = stragglers[i];
            var dr = e != null ? e.GetComponent<DamageReceiver>() : null;
            if (e != null && dr != null && !dr.Dead && liveEnemies.ContainsKey(e.InstanceId) && liveEnemies[e.InstanceId] == e) continue;
            if (e != null) { var wp = e.GetComponent<RogueWaypoint>(); if (wp != null && wp.Label == StragglerLabel) RogueWaypoint.Detach(e.gameObject); }
            stragglers.RemoveAt(i);
        }
        if (show)
        {
            foreach (var e in liveEnemies.Values)
                if (e != null && e.GetComponent<RogueWaypoint>() == null && e.GetComponent<DamageReceiver>() != null && !e.GetComponent<DamageReceiver>().Dead && !RogueKillPrediction.IsPredictedDead(e.gameObject)) { RogueWaypoint.Attach(e.gameObject, "Enemy", StragglerLabel, new Color(0.95f, 0.3f, 0.35f), 2.6f, 1); stragglers.Add(e); }
            return;
        }
        foreach (var e in stragglers) { var wp = e != null ? e.GetComponent<RogueWaypoint>() : null; if (wp != null && wp.Label == StragglerLabel) RogueWaypoint.Detach(e.gameObject); }
        stragglers.Clear();
    }

    static readonly System.Text.RegularExpressions.Regex PercentPattern = new System.Text.RegularExpressions.Regex(@"(\d+)\s*%"), CountPattern = new System.Text.RegularExpressions.Regex(@"(\d+)\s*/\s*(\d+)");

    /// <summary>Objective progress for the bar, read from the replicated progress text ("Captured 40%", "3 / 8"); -1 when it has no number.</summary>
    static float ProgressFraction(string progress)
    {
        if (string.IsNullOrEmpty(progress)) return -1f;
        var m = PercentPattern.Match(progress);
        if (m.Success) return Mathf.Clamp01(int.Parse(m.Groups[1].Value) / 100f);
        m = CountPattern.Match(progress);
        if (m.Success) { int a = int.Parse(m.Groups[1].Value), b = int.Parse(m.Groups[2].Value); return b > 0 ? Mathf.Clamp01((float)a / b) : -1f; }
        return -1f;
    }

    void RefreshBoss(bool combat)
    {
        var boss = combat ? FindFinaleEnemy() : null;
        var dr = boss != null ? boss.GetComponent<DamageReceiver>() : null;
        if (dr == null || dr.hitPoints <= 0) { hudView.HideBoss(); bossMaxHp = 0; return; }
        if (bossMaxHp <= 0 || dr.hitPoints > bossMaxHp) bossMaxHp = dr.hitPoints;
        var def = RogueCatalog.Encounter(state.encounter.finaleId);
        hudView.SetBoss("Enemy", (def != null ? T(def.Name) : T("Target")) + (boss.Invulnerable ? "  " + T("Shielded") : ""), bossMaxHp > 0 ? dr.hitPoints / bossMaxHp : 0);
    }

    bool ultReadyShown;
    void RefreshAbilities(RunPlayer me, bool perFrame = false)
    {
        var rp = perFrame ? CachedLocalRoguePlayer() : LocalRoguePlayer();
        if (me == null || rp == null) { hudView.SetAbility(true, "", "", 0, "", false, false); hudView.SetAbility(false, "", "", 0, "", false, false); return; }
        // a downed player can use nothing but a charged Emergency Revive: every other slot reads as not ready (just under full)
        bool downed = rp.Downed;
        bool hasUlt = !string.IsNullOrEmpty(me.build.ultimate);
        float ultFill = rp.UltimateActive ? rp.UltimateRemaining : me.ultimateCharge / 100f;
        bool ultReady = hasUlt && !rp.UltimateActive && me.ultimateCharge >= 100;
        if (ultReady && !ultReadyShown) RogueAudio.Play("ult_ready");   // once per charge: the slot flips to ready
        ultReadyShown = ultReady;
        if (downed && me.build.ultimate != "ult.emergency_revive") ultFill = Mathf.Min(ultFill, 0.99f);
        // each ultimate and tactical has its own icon (QA-08); the slot follows the equipped id through purchases, respawns,
        // reconnects and the next run because it is read from the replicated build on every refresh
        hudView.SetAbility(true, RogueIcons.ForAbility(me.build.ultimate), RogueIcons.KeyHint("Ultimate"), ultFill, rp.UltimateActive ? "" : me.ultimateCharge + "%", rp.UltimateActive, hasUlt);
        bool hasTac = !string.IsNullOrEmpty(me.build.tactical);
        int charges = 0, maxCharges = 0; float recharge = -1f;
        // a multi-charge tactical (Double Dash) shows one pip per charge instead of an "n/m" text (QA-15)
        bool segmented = hasTac && TacticalCharges(rp, out charges, out maxCharges, out recharge) && maxCharges > 1;
        if (!segmented) { charges = maxCharges = 0; recharge = -1f; }
        hudView.SetAbility(false, hasTac ? RogueIcons.ForAbility(me.build.tactical) : "", RogueIcons.KeyHint("Tactical"), downed ? Mathf.Min(rp.TacticalReadiness, 0.99f) : rp.TacticalReadiness, segmented ? "" : rp.TacticalValue, rp.TacticalActive, hasTac);
        hudView.SetTacticalCharges(charges, maxCharges, recharge);
    }

    // ---------------------------------------------------------------- QA-15 tactical charges (RoguePlayer over Core TacticalRuntime)
    /// <summary>
    /// Charges of the equipped tactical for the HUD pips: usable now, the maximum, and the progress 0..1 of the one segment that is
    /// recharging. Core TacticalRuntime recharges one segment at a time (TacticalRuntime), so only the segment at RechargingIndex fills; it is
    /// -1 when every charge is ready. Reads RoguePlayer.TacticalCharges, TacticalMaxCharges, TacticalRechargingIndex and
    /// TacticalRechargeProgress (the shield maps to one charge).
    /// </summary>
    static bool TacticalCharges(RoguePlayer rp, out int available, out int max, out float recharge)
    {
        available = 0; max = 0; recharge = -1f;
        if (rp == null) return false;
        max = rp.TacticalMaxCharges;
        if (max <= 0) { max = 0; return false; }
        available = Mathf.Clamp(rp.TacticalCharges, 0, max);
        int refilling = rp.TacticalRechargingIndex;
        // the HUD fills the pip right after the usable ones; the core's index is that same segment
        if (refilling >= 0 && available < max) recharge = Mathf.Clamp01(rp.TacticalRechargeProgress);
        return true;
    }

    void RefreshSquad(RunPlayer me)
    {
        var entries = new List<RogueHudView.SquadEntry>();
        foreach (var p in state.players)
        {
            if (!p.connected || (me != null && p.key == me.key)) continue;
            var go = RogueWorld.PlayerByKey(p.key);
            var rp = go != null ? go.GetComponent<RoguePlayer>() : null;
            bool body = rp != null && RogueBodyShield.CarriedBy(rp.gameObject) != null;   // QA-44: carrying an enemy body as a shield
            entries.Add(new RogueHudView.SquadEntry
            {
                // downed: the bar drains with the bleed-out and the state names the seconds left
                name = p.name, icon = body && p.life == PlayerLife.Alive ? "Shield" : rp != null && rp.Carrying && p.life == PlayerLife.Alive ? "Crate" : RogueIcons.ForLife(p.life),
                hp = p.life == PlayerLife.Alive ? (rp != null ? rp.HealthFraction() : 1f) : p.life == PlayerLife.Downed && rp != null && rp.Downed ? rp.BleedOutFraction : 0,
                shield = p.life == PlayerLife.Alive && rp != null ? rp.ShieldFraction : 0f,
                state = p.life == PlayerLife.Downed ? (rp != null && rp.Downed ? T("Downed {0} s", rp.HasBleedOutClock ? Mathf.CeilToInt(rp.BleedOutRemaining).ToString() : "…") : T("Downed")) : p.life == PlayerLife.Dead ? T("Dead") : body ? T("Body shield") : rp != null && rp.Carrying ? T("Carrying") : (state.phase == RunPhase.Prep || state.phase == RunPhase.ChapterEnd) && p.ready ? T("Ready") : "",
                tint = p.life == PlayerLife.Alive ? new Color(0.3f, 0.75f, 0.4f) : p.life == PlayerLife.Downed ? new Color(1f, 0.7f, 0.1f) : new Color(0.95f, 0.3f, 0.35f)
            });
        }
        hudView.SetSquad(entries);
    }

    // ---------------------------------------------------------------- screens
    void RefreshScreens()
    {
        if (state == null || leaving) return;
        if (screen != null && screen.RewardFeedbackPlaying) return;
        var me = LocalPlayer;
        if (me == null) { CloseScreens(); return; }
        string wanted = "";
        switch (state.phase)
        {
            case RunPhase.Prep: wanted = "shop"; break;
            case RunPhase.Reward: wanted = Array.Exists(me.rewardOffers, o => o.sold) || me.rewardOffers.Length == 0 ? "" : "reward"; break;
            case RunPhase.Route: wanted = "route"; break;
            case RunPhase.ChapterEnd: wanted = "chapterend"; break;
        }
        if (wanted == "" || (screenDismissed && wanted == "shop")) { CloseScreens(); return; }
        if (screen == null) { screen = RogueScreenView.Open(this); if (screen != null && overview != null) screen.SetCovered(true); }
        if (screen == null) return;
        screenMode = wanted;
        switch (wanted)
        {
            case "shop": ShowShop(me, false); break;
            case "reward": ShowReward(me); break;
            case "route": ShowRoute(me); break;
            case "chapterend": ShowShop(me, true); break;
        }
        EnsureLocalPlayerAlive();
    }

    void CloseScreens()
    {
        if (screen != null) { screen.Close(); screen = null; }
        screenMode = "";
    }

    /// <summary>Shop rows (offers + reroll) for the prep screen and the overview's shop tab.</summary>
    void AddShopRows(RunPlayer me, OfferSink add)
    {
        for (int i = 0; i < me.offers.Length; i++)
        {
            var offer = me.offers[i]; var def = RogueCatalog.Item(offer.itemId);
            if (def == null) continue;
            int index = i;
            bool staleTier = (def.Kind == ItemKind.Core || def.Kind == ItemKind.Mod) && !offer.sold && offer.tierAtSample != me.build.Tier(def.Id);
            string status = offer.sold ? T("Bought") : staleTier ? T("Tier changed: reroll for a new price") : me.build.RejectReason(def) != null ? T(me.build.RejectReason(def)) : me.walletMinor < offer.priceMinor ? T("Not enough money") : "";
            bool pending = pendingTx.ContainsValue(offer.itemId);
            var row = add(RogueIcons.ForItem(def), DisplayName(def), EffectLine(def, me.build, offer), "$" + RogueMoney.Format(offer.priceMinor), RogueItemKinds.Tag(def, RarityText(def)), offer.sold ? "" : T("Buy"),
                !offer.sold && status == "" && !pending, pending ? T("Buying...") : status,
                () => Buy(me, index, offer));
            if (row != null) row.SetPitch(T(RoguePitches.Of(def.Id)));
        }
        // what the player owns: every core and mod can be removed here (F40). The refund is half of what was paid for it (free
        // rewards refund nothing), so buying it back always costs more than the refund.
        foreach (var id in OwnedCoresAndMods(me.build))
        {
            var def = RogueCatalog.Item(id); if (def == null) continue;
            string itemId = id;
            long refund = RogueShop.RefundMinor(me.build, id);
            bool pendingRemove = pendingTx.ContainsValue("remove:" + id);
            // the tier goes on the effect line: on the title it pushed long names under the rarity tag (QA-30)
            string tier = TierText(me.build, id);
            var row = add(RogueIcons.ForItem(def), DisplayName(def), (tier == "" ? "" : tier + "   ") + T("Owned") + (FlatsLocalization.IsChinese ? "：" : ": ") + EffectText(def, me.build.Tier(id)), refund > 0 ? "+$" + RogueMoney.Format(refund) : T("No refund"), RogueItemKinds.Tag(def, RarityText(def)), T("Remove"),
                !pendingRemove, pendingRemove ? T("Removing...") : "", () => ConfirmRemove(me, itemId));
            if (row != null) row.SetPitch(T(RoguePitches.Of(def.Id)));
        }
        bool rerollPending = pendingTx.ContainsValue(PendingReroll) || pendingTx.ContainsValue(PendingTicket);
        int tickets = RerollTickets(me);
        if (tickets > 0)
        {
            // QA-24: a ticket from a skipped reward rerolls for free and does not use this visit's rerolls
            add("Reload", T("Reroll with a ticket"), T("Free: uses 1 reroll ticket from a skipped reward. Does not use this visit's rerolls."), T("Free"), "", T("Use ticket"),
                !rerollPending, rerollPending ? T("Rerolling...") : T("Reroll tickets: {0}", tickets), () => UseRerollTicket(me));
        }
        long reroll = RogueShop.RerollPriceMinor(state.Chapter);
        add("Reload", T("Reroll offers"), T("New offers for this visit. Limited per visit; the same offers return if you leave and come back."), "$" + RogueMoney.Format(reroll), "", T("Reroll"),
            me.rerollsLeft > 0 && me.walletMinor >= reroll && !rerollPending, rerollPending ? T("Rerolling...") : me.rerollsLeft > 0 ? T("{0} left", me.rerollsLeft) : T("No rerolls left"),
            () => PaidReroll(me, reroll));
    }

    // pendingTx values for requests that are not an item purchase: one request of a kind at a time, so a double click sends one
    const string PendingReroll = "#reroll", PendingTicket = "#ticket", PendingSkip = "#skip", PendingRewardPrefix = "#reward:";

    void PaidReroll(RunPlayer me, long price)
    {
        if (pendingTx.ContainsValue(PendingReroll) || pendingTx.ContainsValue(PendingTicket)) return;
        string txId = NextTx();
        pendingTx[txId] = PendingReroll;
        Command(new RogueCommandMessage { kind = "buy", tx = new ShopTransaction { txId = txId, runId = state.runId, shopVersion = me.shopVersion, reroll = true, expectedPriceMinor = price } });
        RefreshScreens();
        if (overview != null) FillOverview(true);
    }

    void UseRerollTicket(RunPlayer me)
    {
        if (RerollTickets(me) <= 0 || pendingTx.ContainsValue(PendingReroll) || pendingTx.ContainsValue(PendingTicket)) return;
        string txId = NextTx();
        pendingTx[txId] = PendingTicket;
        Command(new RogueCommandMessage { kind = "buy", tx = TicketRerollTx(me, txId) });
        RefreshScreens();
        if (overview != null) FillOverview(true);
    }

    // ---------------------------------------------------------------- QA-24 reroll tickets and skipped rewards (Core rules)
    // RunPlayer.rerollTickets (run-local, saved with the run), ShopTransaction.skipReward (Reward -> +1 ticket, every reward offer
    // closed) and ShopTransaction.useRerollTicket (with reroll, price 0, outside the visit's reroll limit); RunMachine.Buy routes both.
    static int RerollTickets(RunPlayer me) { return me != null ? Mathf.Max(0, me.rerollTickets) : 0; }
    ShopTransaction SkipRewardTx(RunPlayer me, string txId) { return new ShopTransaction { txId = txId, runId = state.runId, shopVersion = me.shopVersion, skipReward = true, expectedPriceMinor = 0 }; }
    ShopTransaction TicketRerollTx(RunPlayer me, string txId) { return new ShopTransaction { txId = txId, runId = state.runId, shopVersion = me.shopVersion, reroll = true, useRerollTicket = true, expectedPriceMinor = 0 }; }

    void ShowShop(RunPlayer me, bool chapterEnd)
    {
        screen.UseCards(false);
        screen.SetTitle("Coin", chapterEnd ? T("Chapter {0} Shop", state.Chapter) : T("Shop  Stage {0}-{1}", state.Chapter, RogueDepth.StageInChapter(state.depth)),
            T("Rerolls {0}   {1}", me.rerollsLeft, SlotSummary(me.build)) + (RerollTickets(me) > 0 ? "   " + T("Reroll tickets: {0}", RerollTickets(me)) : ""), "$" + RogueMoney.Format(me.walletMinor));
        screen.ClearRows();
        AddShopRows(me, (icon, name, effect, price, rarity, action, interactable, status, onAction) => screen.AddRow(icon, name, effect, price, rarity, action, interactable, status, onAction));
        if (chapterEnd)
        {
            // only the host decides where the squad goes (the authority ignores anyone else): other players see who they wait for,
            // and the host's buttons lock once a decision is on its way so a second click cannot look ignored
            bool decides = Menu.network == 0 || IsAuthority;
            string note = !decides ? T("Waiting for the host ({0}) to continue or evacuate.", HostName())
                : chapterDecisionSent ? T("Travelling...")
                : T("Continue travels to {0}. Evacuate banks this run's record and ends it.", RouteText(state.mapId, state.routeTag));
            screen.SetFooter(T("Continue"), "Arrow", () => { if (!decides || chapterDecisionSent) return; chapterDecisionSent = true; Command(new RogueCommandMessage { kind = "continue" }); RefreshScreens(); },
                T("Evacuate"), "Quit", () => { if (decides && !chapterDecisionSent) ConfirmEvacuate(); }, note);
            screen.SetFooterInteractable(decides && !chapterDecisionSent, decides && !chapterDecisionSent);
            screen.SetPrimaryHighlight(false);
            screen.SetOverview(RogueInput.OverviewLabel, () => OpenOverview());
        }
        else
        {
            bool ready = me.ready;
            string label, hint;
            ShopReadyTexts(me, out label, out hint);
            screen.SetFooter(label, "Check", () => Command(new RogueCommandMessage { kind = "ready", flag = !ready }), null, null, null,
                hint);   // the Overview button beside it already names its key
            screen.SetFooterInteractable(true, true);
            screen.SetPrimaryHighlight(ready);
            screen.SetOverview(RogueInput.OverviewLabel, () => OpenOverview());
        }
    }

    /// <summary>The prep screen's Ready button and the line under it: who is ready, and once everyone is, the seconds left (QA-20).</summary>
    void ShopReadyTexts(RunPlayer me, out string label, out string hint)
    {
        bool ready = me.ready;
        int countdown = Mathf.CeilToInt(CountdownRemaining);
        // an empty wallet in front of a full shop reads as "the shop is broken": say where money comes from, on its own line,
        // so the ready count (who we are waiting for) is always visible
        bool affordable = false; foreach (var o in me.offers) if (!o.sold && o.priceMinor <= me.walletMinor) affordable = true;
        hint = countdown > 0 ? T("Everyone is ready. The stage starts in {0} s. Cancel Ready to wait.", countdown) : ReadyText();
        if (!affordable && me.walletMinor < RogueMoney.Coins(10)) hint += "\n" + T("No money yet: kills pay bounty.");
        // the button says what pressing it does, short enough for one line beside its icon; the note under it carries the countdown
        // sentence ("Starting in 2 (tap to cancel)" wrapped onto the check icon in Chinese)
        label = !ready ? T("Ready") : countdown > 0 ? T("Cancel Ready ({0})", countdown) : T("Cancel Ready");
    }

    void ShowReward(RunPlayer me)
    {
        screen.SetTitle("Check", T("Cleared: {0}", T("Stage {0}-{1}", state.Chapter, RogueDepth.StageInChapter(state.depth))), T("Pick one. It is free."), "$" + RogueMoney.Format(me.walletMinor));
        screen.UseCards(true);
        screen.ClearRows();
        // one answer per reward: once a Take or a Skip is sent, every button waits for the authority (no double pick, no pick after a skip)
        bool decided = RewardDecisionPending;
        for (int i = 0; i < me.rewardOffers.Length; i++)
        {
            var offer = me.rewardOffers[i]; var def = RogueCatalog.Item(offer.itemId);
            if (def == null) continue;
            int index = i; string itemId = offer.itemId;
            string status = decided ? T("Waiting for the host...") : me.build.RejectReason(def) != null ? T(me.build.RejectReason(def)) : "";
            bool canTake = !decided && me.build.RejectReason(def) == null;
            Action take = () => TakeReward(index, itemId);
            string tag = RogueItemKinds.Tag(def, RarityText(def));
            // a card stacks what a row prints side by side: the effect, the tier and the pairing each get their own line
            var card = screen.AddCard(RogueIcons.ForItem(def), DisplayName(def), tag, EffectLine(def, me.build).Replace("   ", "\n").Replace("  ", "\n"), T("Take"), canTake, status, take);
            if (card != null) card.SetPitch(T(RoguePitches.Of(def.Id)));
            else
            {
                var row = screen.AddRow(RogueIcons.ForItem(def), DisplayName(def), EffectLine(def, me.build), T("Free"), tag, T("Take"), canTake, status, take);
                if (row != null) row.SetPitch(T(RoguePitches.Of(def.Id)));
            }
        }
        // QA-24: the alternative to every card is a reroll ticket; the note says what is given up, what is gained and what it does
        int tickets = RerollTickets(me);
        string note = T("Skip: give up these rewards and get 1 reroll ticket.") + "\n" + T("A ticket is one free shop reroll, now or at any later stage of this run.")
            + (tickets > 0 ? "  " + T("Reroll tickets: {0}", tickets) : "");
        screen.SetFooter(null, null, null, T("Skip for 1 reroll ticket"), "Reload", ConfirmSkipReward, note);
        screen.SetFooterInteractable(false, !decided && me.rewardOffers.Length > 0);
        screen.SetOverview(RogueInput.OverviewLabel, () => OpenOverview());
    }

    bool RewardDecisionPending
    {
        get { foreach (var v in pendingTx.Values) if (v == PendingSkip || (v != null && v.StartsWith(PendingRewardPrefix))) return true; return false; }
    }

    void TakeReward(int index, string itemId)
    {
        var me = LocalPlayer;
        if (me == null || state == null || state.phase != RunPhase.Reward || RewardDecisionPending) return;
        string txId = NextTx();
        pendingTx[txId] = PendingRewardPrefix + itemId;
        Command(new RogueCommandMessage { kind = "buy", tx = new ShopTransaction { txId = txId, runId = state.runId, shopVersion = me.shopVersion, offerIndex = index, expectedPriceMinor = 0, rewardPick = true } });
        RefreshScreens();
    }

    void ConfirmSkipReward()
    {
        var me = LocalPlayer;
        if (me == null || state == null || state.phase != RunPhase.Reward || RewardDecisionPending || me.rewardOffers.Length == 0) return;
        if (menu == null) { SkipReward(); return; }
        var names = new List<string>();
        foreach (var o in me.rewardOffers) names.Add(ItemName(o.itemId));
        menu.ShowConfirm(T("Skip this reward?"), T("You give up: {0}", string.Join(ListSeparator, names.ToArray())) + "\n" + T("You get: 1 reroll ticket.") + "\n"
            + T("A ticket rerolls the shop once for free, at this stage or any later one in this run. It does not use the shop's reroll limit."),
            ok => { if (ok) SkipReward(); }, T("Skip and take the ticket"), T("Back"));
    }

    void SkipReward()
    {
        var me = LocalPlayer;
        if (me == null || state == null || state.phase != RunPhase.Reward || RewardDecisionPending || me.rewardOffers.Length == 0) return;
        foreach (var o in me.rewardOffers) if (o.sold) return;   // already answered
        string txId = NextTx();
        pendingTx[txId] = PendingSkip;
        Command(new RogueCommandMessage { kind = "buy", tx = SkipRewardTx(me, txId) });
        RefreshScreens();
    }

    void ShowRoute(RunPlayer me)
    {
        screen.UseCards(false);
        screen.SetTitle("Stage", T("Chapter {0} complete", state.Chapter), T(IsAuthority ? "Choose the next chapter's route." : "The host chooses the route."), "$" + RogueMoney.Format(me.walletMinor));
        screen.ClearRows();
        for (int i = 0; i < state.routeOptions.Length; i++)
        {
            var parts = state.routeOptions[i].Split('|');
            var map = RogueCatalog.Map(parts[0]); var route = RogueCatalog.Route(parts.Length > 1 ? parts[1] : "");
            int index = i;
            screen.AddRow(RogueIcons.ForRoute(route.Tag), T(route.Name) + ": " + (map != null ? T(map.Name) : parts[0]), T(route.Brief), RouteRewardText(route), route.Tag == "danger" ? T("Risky") : route.Tag == "safe" ? T("Safer") : "", T("Go"),
                IsAuthority, IsAuthority ? "" : T("Host decides"), () => Command(new RogueCommandMessage { kind = "route", index = index }));
        }
        screen.SetFooter(null, null, null, null, "");
        screen.SetOverview(RogueInput.OverviewLabel, () => OpenOverview());
    }

    void Buy(RunPlayer me, int index, ShopOffer offer)
    {
        string txId = NextTx();
        pendingTx[txId] = offer.itemId;
        Command(new RogueCommandMessage { kind = "buy", tx = new ShopTransaction { txId = txId, runId = state.runId, shopVersion = me.shopVersion, offerIndex = index, expectedPriceMinor = offer.priceMinor } });
        RefreshScreens();
        if (overview != null) FillOverview();
    }

    string NextTx() { return localKey + ":" + (++txCounter) + ":" + DateTime.UtcNow.Ticks; }

    /// <summary>A request the authority never answered (lost in a host change, sent in a phase that just ended) kept its buttons locked
    /// for the rest of the stage: a guest could neither take nor skip the reward. After a few seconds the request is dropped and the
    /// screen is live again; a late answer is still applied by the snapshot.</summary>
    void ExpirePendingTx()
    {
        if (pendingTx.Count == 0) return;
        long now = DateTime.UtcNow.Ticks; List<string> stale = null;
        foreach (var id in pendingTx.Keys)
        {
            long sent; int cut = id.LastIndexOf(':');
            if (cut >= 0 && long.TryParse(id.Substring(cut + 1), out sent) && now - sent > TimeSpan.TicksPerSecond * 6) { if (stale == null) stale = new List<string>(); stale.Add(id); }
        }
        if (stale == null) return;
        foreach (var id in stale) pendingTx.Remove(id);
        if (screen != null) RefreshScreens();
        if (overview != null) FillOverview();
    }

    void OnTransactionResult(RogueEventMessage e)
    {
        if (e.playerKey != localKey) return;
        var parts = e.text.Split('|');
        string status = parts.Length > 0 ? parts[0] : "", reason = parts.Length > 1 ? parts[1] : "", item = parts.Length > 2 ? parts[2] : "", txId = parts.Length > 3 ? parts[3] : "";
        bool removal = parts.Length > 4 && parts[4] == "remove";
        string request; pendingTx.TryGetValue(txId, out request);
        pendingTx.Remove(txId);
        if (e.flag && !removal) ApplyTransactionEffects(txId, item);
        // Only a fresh acknowledgement of this client's own request earns a receipt; replays/removals/rerolls do not.
        if (e.flag && !removal && status != "Duplicate" && request != null && !string.IsNullOrEmpty(item))
        {
            var acquired = RogueCatalog.Item(item);
            if (acquired != null) RogueAcquisitionView.Show(RogueIcons.ForItem(acquired), DisplayName(acquired), T(RoguePitches.Of(item)), RogueItemKinds.Tint(acquired.Kind));
        }
        if (e.flag) RogueAudio.Play(item == "" ? "ui_click" : e.minor > 0 ? "ui_buy" : "ui_reward", 0.9f); else if (status != "Duplicate") RogueAudio.Play("ui_deny", 0.7f);
        if (e.flag && removal) Log(T("Removed {0} (refund ${1})", ItemName(item), RogueMoney.Format((long)e.value)));
        else if (e.flag && request == PendingSkip) { Log(T("Reward skipped: +1 reroll ticket")); Banner(T("+1 reroll ticket"), 2f); }
        else if (e.flag && request == PendingTicket) Log(T("Rerolled with a ticket"));
        else if (e.flag) Log(item == "" ? T("Rerolled") : e.minor > 0 ? T("Bought {0} for ${1}", ItemName(item), RogueMoney.Format(e.minor)) : T("Bought {0}", ItemName(item)));
        else if (status != "Duplicate") Log(T("Purchase failed: {0}", T(reason == "" ? status : reason)));
        if (screen != null) RefreshScreens();
        if (overview != null) FillOverview();
    }

    static List<string> OwnedCoresAndMods(PlayerBuild b)
    {
        var list = new List<string>(b.cores);
        list.AddRange(b.mods);
        return list;
    }

    /// <summary>"Tier 2/5" for cores and mods that have tiers ("Tier 5/5 (max)" at the top); empty for single-tier items.</summary>
    static string TierText(PlayerBuild b, string id)
    {
        int max = RogueCatalog.MaxTier(id), tier = b.Tier(id);
        return max > 1 ? TierLabel(tier, max) : "";
    }

    static string TierLabel(int tier, int max) { return tier >= max ? T("Tier {0}/{1} (max)", tier, max) : T("Tier {0}/{1}", tier, max); }

    /// <summary>An item's effect sentence with the numbers of one tier, in the local language. The catalog's template is the
    /// translation key and the numbers come from RogueTiers through ItemDef.EffectArgs, so every tier reads its own values.</summary>
    static string EffectText(ItemDef def, int tier) { return T(def.Effect, (object[])def.EffectArgs(tier)); }

    /// <summary>The same sentence as an upgrade preview: each number that changes reads "now -> next".</summary>
    static string EffectText(ItemDef def, int fromTier, int toTier) { return T(def.Effect, (object[])def.EffectArgs(fromTier, toTier)); }

    void ConfirmRemove(RunPlayer me, string itemId)
    {
        if (menu == null || me == null) return;
        long refund = RogueShop.RefundMinor(me.build, itemId);
        var breaks = RogueShop.RemovalBreaks(me.build, itemId);
        string message = T("Remove {0}? Its effect ends now.", ItemName(itemId)) + "\n" + (refund > 0 ? T("Refund: ${0} (half of what you paid).", RogueMoney.Format(refund)) : T("No refund: it was free."));
        if (breaks.Length > 0) { var names = new List<string>(); foreach (var b in breaks) names.Add(ItemName(b)); message += "\n" + T("These mods stop working without it: {0}", string.Join(ListSeparator, names.ToArray())); }
        menu.ShowConfirm(T("Remove"), message, ok =>
        {
            if (!ok || state == null) return;
            var current = LocalPlayer; if (current == null) return;
            string txId = NextTx();
            pendingTx[txId] = "remove:" + itemId;
            Command(new RogueCommandMessage { kind = "buy", tx = new ShopTransaction { txId = txId, runId = state.runId, shopVersion = current.shopVersion, remove = true, removeItemId = itemId, expectedRefundMinor = RogueShop.RefundMinor(current.build, itemId) } });
            RefreshScreens();
            if (overview != null) FillOverview();
        }, T("Remove"), T("Cancel"));
    }

    void ConfirmEvacuate()
    {
        if (menu == null) return;
        menu.ShowConfirm("Evacuate", "End the run here and bank your record? The checkpoint is removed.", ok => { if (ok && !chapterDecisionSent) { chapterDecisionSent = true; Command(new RogueCommandMessage { kind = "evacuate" }); RefreshScreens(); } }, T("Evacuate"), T("Cancel"));
    }

    string ReadyText()
    {
        int ready = 0, total = 0;
        var waiting = new List<string>();
        foreach (var p in state.players) if (p.connected) { total++; if (p.ready) ready++; else waiting.Add(p.key == localKey ? T("you") : p.name); }
        if (total <= 1) return T("Press Ready to start the stage.");
        string line = T("Ready {0}/{1}. The stage starts when everyone is ready.", ready, total);
        return waiting.Count > 0 ? line + "  " + T("Waiting for: {0}", string.Join(ListSeparator, waiting.ToArray())) : line;
    }

    /// <summary>Separator for a list of names in the player's language (Chinese lists use the enumeration comma).</summary>
    static string ListSeparator { get { return FlatsLocalization.IsChinese ? "、" : ", "; } }

    bool chapterDecisionSent;

    /// <summary>Display name of the player who decides (the Photon master).</summary>
    string HostName()
    {
        if (Menu.network == 0 || PhotonNetwork.masterClient == null) return "";
        var p = state != null ? state.Player(KeyOf(PhotonNetwork.masterClient)) : null;
        return p != null && !string.IsNullOrEmpty(p.name) ? p.name : PhotonNetwork.masterClient.NickName;
    }

    static string BuildSummary(PlayerBuild b)
    {
        var parts = new List<string>();
        foreach (var c in b.cores) parts.Add(ItemName(c));
        if (!string.IsNullOrEmpty(b.tactical)) parts.Add(ItemName(b.tactical));
        if (!string.IsNullOrEmpty(b.ultimate)) parts.Add(ItemName(b.ultimate));
        parts.Add(T("Mods {0}/{1}", b.mods.Length, RogueCatalog.MaxMods));
        if (b.healthTier + b.damageTier + b.magazineTier + b.speedTier > 0)
            parts.Add(T("Health T{0}  Damage T{1}  Magazine T{2}  Speed T{3}", b.healthTier, b.damageTier, b.magazineTier, b.speedTier));   // tiers, not percentages
        return string.Join("  ", parts.ToArray());
    }

    /// <summary>One short line of what the build holds, by category: the shop header uses it so a purchase can be judged against it.</summary>
    static string SlotSummary(PlayerBuild b)
    {
        return T("Cores {0}/{1}", b.cores.Length, RogueCatalog.MaxCores) + "   " + T("Mods {0}/{1}", b.mods.Length, RogueCatalog.MaxMods) + "   " +
               T("Tactical") + " " + (string.IsNullOrEmpty(b.tactical) ? "-" : ItemName(b.tactical)) + "   " + T("Ultimate") + " " + (string.IsNullOrEmpty(b.ultimate) ? "-" : ItemName(b.ultimate));
    }

    /// <summary>Cores a mod is built for (shared tags), for the "works with" hint; generic mods pair with nothing in particular.</summary>
    static List<ItemDef> PairedCores(ItemDef mod)
    {
        var list = new List<ItemDef>();
        foreach (var core in RogueCatalog.Cores)
            foreach (var t in mod.Tags)
                if (t != RogueCatalog.TagGeneric && core.HasTag(t)) { list.Add(core); break; }
        return list;
    }

    /// <summary>Slot and pairing context appended to a core or mod's effect: where it goes and what it works with.</summary>
    static string SlotLine(ItemDef def, PlayerBuild b)
    {
        if (def.Kind == ItemKind.Core) return T("Core slot {0}/{1}", Math.Min(RogueCatalog.MaxCores, b.cores.Length + (b.HasCore(def.Id) ? 0 : 1)), RogueCatalog.MaxCores);
        if (def.Kind != ItemKind.Mod) return "";
        string slot = T("Mod slot {0}/{1}", Math.Min(RogueCatalog.MaxMods, b.mods.Length + (b.HasMod(def.Id) ? 0 : 1)), RogueCatalog.MaxMods);
        var paired = PairedCores(def);
        foreach (var core in paired) if (b.HasCore(core.Id)) return T("Works with your {0}", ItemName(core.Id)) + "   " + slot;
        if (paired.Count == 0) return slot;
        var names = new List<string>(); foreach (var core in paired) names.Add(ItemName(core.Id));
        return T("Pairs with {0}", string.Join("/", names.ToArray())) + "   " + slot;
    }

    static string StatPct(string statId, BuildStats s)
    {
        switch (statId)
        {
            case "stat.health": return Pct(s.HealthMul);
            case "stat.damage": return Pct(s.DamageMul);
            case "stat.magazine": return Pct(s.MagazineMul);
            default: return Pct(s.SpeedMul);
        }
    }

    /// <summary>
    /// The description of a shop row or a reward card. <paramref name="offer"/> is the shop offer the row stands for (null for a
    /// reward card): the build is live, so a row that was just bought must describe what was bought, not the purchase after it.
    /// A sold row therefore shows the tier it sold and the current state only; an offer still on sale previews the next step.
    /// </summary>
    static string EffectLine(ItemDef def, PlayerBuild b, ShopOffer offer = null)
    {
        bool sold = offer != null && offer.sold;
        if (def.Kind == ItemKind.Stat)
        {
            int statTier = b.StatTier(def.Id);
            string now = StatPct(def.Id, BuildStats.Compute(b));
            // nothing to preview once it is bought, at the last tier, or when the total cap would swallow the next tier
            if (sold || b.RejectReason(def) != null) return EffectText(def, 1) + "  " + T("Tier {0}/{1}: {2}", statTier, def.MaxStacks, now);
            var after = b.Clone(); after.SetStatTier(def.Id, statTier + 1);
            return EffectText(def, 1) + "  " + T("Tier {0}/{1}: {2} -> {3}", statTier, def.MaxStacks, now, StatPct(def.Id, BuildStats.Compute(after)));
        }
        if (def.Kind == ItemKind.Tactical && !sold && !string.IsNullOrEmpty(b.tactical) && b.tactical != def.Id) return EffectText(def, 1) + "  " + T("Replaces {0}.", ItemName(b.tactical));
        if (def.Kind == ItemKind.Ultimate && !sold && !string.IsNullOrEmpty(b.ultimate) && b.ultimate != def.Id) return EffectText(def, 1) + "  " + T("Replaces {0} (charge is kept).", ItemName(b.ultimate));
        if (def.Kind == ItemKind.Weapon)
        {
            // after the purchase the primary IS this weapon: "Replaces <the gun just bought>" was nonsense
            bool replaces = !sold && b.primaryWeapon >= 0 && b.primaryWeapon != RogueCatalog.WeaponIndexOf(def.Id);
            return EffectText(def, 1) + (replaces ? "  " + T("Replaces {0}.", WeaponCatalogName(b.primaryWeapon)) : "") + WeaponRangeSuffix(def);
        }
        if (def.Kind != ItemKind.Core && def.Kind != ItemKind.Mod) return EffectText(def, 1);
        int maxTier = RogueCatalog.MaxTier(def.Id), tier = b.Tier(def.Id);
        if (sold)
        {
            // the tier this row sold (the offer remembers the tier it was sampled at), with that tier's numbers
            int bought = Mathf.Clamp(offer.tierAtSample + 1, 1, Mathf.Max(1, maxTier));
            return EffectText(def, bought) + (maxTier > 1 ? "\n" + TierLabel(bought, maxTier) : "");
        }
        if (tier > 0 && tier >= maxTier) return EffectText(def, tier) + (maxTier > 1 ? "\n" + TierLabel(tier, maxTier) : "");   // nothing left to upgrade to
        // an upgrade takes no new slot, so its row has no slot line: it shows what each number becomes
        if (tier > 0) return EffectText(def, tier, tier + 1) + "\n" + T("Upgrade to tier {0}/{1}", tier + 1, maxTier);
        string slotLine = SlotLine(def, b);
        if (maxTier > 1) slotLine = T("Tier {0}/{1}", 1, maxTier) + (slotLine == "" ? "" : "   " + slotLine);
        return slotLine == "" ? EffectText(def, 1) : EffectText(def, 1) + "\n" + slotLine;
    }

    // ---------------------------------------------------------------- QA-20 / QA-19 stage clock (the authority's replicated clock)
    int stageClockSecond = -1; StageClockKind stageClockShown;

    /// <summary>Draws the ready countdown and the stage intro from the replicated stage clock: the HUD's centre card while the HUD is
    /// up, and the Ready button and its line while the prep shop is open. The same numbers on every client, solo or co-op.</summary>
    void TickStageClockUi()
    {
        var kind = VisibleStageClock;
        float remaining = kind == StageClockKind.Countdown ? CountdownRemaining : kind == StageClockKind.Intro ? IntroRemaining : 0f;
        float duration = StageClockDuration;
        bool shopOpen = screen != null && screenMode == "shop";
        // the controller's banner fallback is only needed while neither the HUD nor the shop can show the clock (the TAB overview is up)
        HudDrawsStageClock = hudView.HudVisible || (shopOpen && kind == StageClockKind.Countdown);
        if (kind == StageClockKind.None || remaining <= 0f) hudView.SetStageClock("", "", "", 0f);
        else if (kind == StageClockKind.Intro && BriefingDrawsIntro) hudView.SetStageClock("", "", "", 0f);   // QA-43: the intro card counts down itself
        else if (kind == StageClockKind.Countdown) hudView.SetStageClock(T("Everyone is ready"), T("Stage starts in"), Mathf.CeilToInt(remaining).ToString(), duration > 0f ? remaining / duration : 0f);
        else hudView.SetStageClock(T("Stage {0}-{1}: {2}", state.Chapter, RogueDepth.StageInChapter(state.depth), StageIntroTitle), T("Enemies arrive in"), Mathf.CeilToInt(remaining).ToString(), duration > 0f ? remaining / duration : 0f);
        int second = remaining > 0f ? Mathf.CeilToInt(remaining) : -1;
        if (second == stageClockSecond && kind == stageClockShown) return;
        stageClockSecond = second; stageClockShown = kind;
        // once per second, and when the clock starts or stops: the prep shop's Ready button and note count down in place
        var me = LocalPlayer;
        if (shopOpen && me != null && state.phase == RunPhase.Prep)
        {
            string label, hint;
            ShopReadyTexts(me, out label, out hint);
            screen.SetPrimaryText(label, hint);
        }
        if (state.phase == RunPhase.Prep) RefreshHud();
    }

    // QA-49: a shotgun's close bonus and long-range falloff under its shop description ("" for other weapons)
    static string WeaponRangeSuffix(ItemDef def)
    {
        string range = def.Kind == ItemKind.Weapon ? RogueHooks.MetaWeaponRangeText(RogueCatalog.WeaponIndexOf(def.Id), "\n") : "";
        return range.Length > 0 ? "\n" + range : "";
    }

    static string WeaponCatalogName(int index) { return index >= 0 && index < Flats.Core.WeaponCatalog.Count ? RogueItemKinds.WeaponDisplayName(RogueHooks.MetaWeaponDisplay(index, Flats.Core.WeaponCatalog.GetDefault(index)).gunName) : "?"; }
    static string Pct(double mul) { return (mul >= 1 ? "+" : "") + Math.Round((mul - 1) * 100) + "%"; }
    static string RarityText(ItemDef def) { return def.Rarity == 2 ? T("Rare") : def.Rarity == 1 ? T("Uncommon") : ""; }
    static string RouteText(string mapId, string routeTag) { var m = RogueCatalog.Map(mapId); var r = RogueCatalog.Route(routeTag); return (m != null ? T(m.Name) : mapId) + " (" + T(r.Name) + ")"; }
    static string RouteRewardText(RouteDef r) { return T("Bounty {0}%", (r.BudgetMul >= 1 ? "+" : "") + Math.Round((r.BudgetMul - 1) * 100)); }
}
