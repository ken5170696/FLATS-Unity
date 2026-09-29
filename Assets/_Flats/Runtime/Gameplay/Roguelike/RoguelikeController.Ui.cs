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

    public delegate void OfferSink(string icon, string name, string effect, string price, string rarity, string action, bool interactable, string status, Action onAction);

    // ---------------------------------------------------------------- HUD
    void RefreshHud()
    {
        if (state == null || leaving) return;
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
            case RunPhase.Prep: hudView.SetObjective("Coin", T("Prep"), screenDismissed ? T("Press {0} to reopen the shop", RogueInput.KeyText("Shop")) : T("Shop is open. Ready up when done.")); break;
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
                    hudView.SetObjective(RogueIcons.ForEncounter(id), def != null ? T(def.Name) : T("Objective"), progress);
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
        if (!combat) hudView.SetObjectiveProgress(-1f);
        var ev = combat ? RogueCatalog.Encounter(enc.eventId) : null; var em = combat ? RogueCatalog.Encounter(enc.emergencyId) : null;
        hudView.SetEvent("Settings5", ev != null ? T(ev.Name) + (ObjectivePart(1) != "" ? "  " + ObjectivePart(1) : "") : "", false);
        hudView.SetEvent("Warning", em != null ? T(em.Name) + (ObjectivePart(2) != "" ? "  " + ObjectivePart(2) : "") : "", true);
        RefreshBoss(combat);
        RefreshStragglers(combat);
        RefreshAbilities(me);
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
        else if (rpLocal != null && rpLocal.Carrying) hudView.SetHint("Crate", T("Carrying: press {0} to put it down. You cannot fire.", RogueInput.KeyText("Interact")));
        // the first stages open with enemies already on the way: say so, or a new player sprints for the marker through them
        else if (combat && state.depth <= 2 && combatSince >= 0 && Time.time - combatSince < 12f) hudView.SetHint("Enemy", T("Enemies are coming: deal with them, then head for the objective."));
        else hudView.SetHint("", "");
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
                if (e != null && e.GetComponent<RogueWaypoint>() == null && e.GetComponent<DamageReceiver>() != null && !e.GetComponent<DamageReceiver>().Dead) { RogueWaypoint.Attach(e.gameObject, "Enemy", StragglerLabel, new Color(0.95f, 0.3f, 0.35f), 2.6f, 1); stragglers.Add(e); }
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

    void RefreshAbilities(RunPlayer me)
    {
        var rp = LocalRoguePlayer();
        if (me == null || rp == null) { hudView.SetAbility(true, "", "", 0, "", false, false); hudView.SetAbility(false, "", "", 0, "", false, false); return; }
        // a downed player can use nothing but a charged Emergency Revive: every other slot reads as not ready (just under full)
        bool downed = rp.Downed;
        bool hasUlt = !string.IsNullOrEmpty(me.build.ultimate);
        float ultFill = rp.UltimateActive ? rp.UltimateRemaining : me.ultimateCharge / 100f;
        if (downed && me.build.ultimate != "ult.emergency_revive") ultFill = Mathf.Min(ultFill, 0.99f);
        hudView.SetAbility(true, "Ultimate", RogueIcons.KeyHint("Ultimate"), ultFill, rp.UltimateActive ? "" : me.ultimateCharge + "%", rp.UltimateActive, hasUlt);
        bool hasTac = !string.IsNullOrEmpty(me.build.tactical);
        hudView.SetAbility(false, hasTac ? RogueIcons.ForItem(RogueCatalog.Item(me.build.tactical)) : "", RogueIcons.KeyHint("Tactical"), downed ? Mathf.Min(rp.TacticalReadiness, 0.99f) : rp.TacticalReadiness, rp.TacticalValue, rp.TacticalActive, hasTac);
    }

    void RefreshSquad(RunPlayer me)
    {
        var entries = new List<RogueHudView.SquadEntry>();
        foreach (var p in state.players)
        {
            if (!p.connected || (me != null && p.key == me.key)) continue;
            var go = RogueWorld.PlayerByKey(p.key);
            var rp = go != null ? go.GetComponent<RoguePlayer>() : null;
            entries.Add(new RogueHudView.SquadEntry
            {
                name = p.name, icon = rp != null && rp.Carrying && p.life == PlayerLife.Alive ? "Crate" : RogueIcons.ForLife(p.life), hp = p.life == PlayerLife.Alive ? (rp != null ? rp.HealthFraction() : 1f) : 0,
                state = p.life == PlayerLife.Downed ? T("Downed") : p.life == PlayerLife.Dead ? T("Dead") : rp != null && rp.Carrying ? T("Carrying") : (state.phase == RunPhase.Prep || state.phase == RunPhase.ChapterEnd) && p.ready ? T("Ready") : "",
                tint = p.life == PlayerLife.Alive ? new Color(0.3f, 0.75f, 0.4f) : p.life == PlayerLife.Downed ? new Color(1f, 0.7f, 0.1f) : new Color(0.95f, 0.3f, 0.35f)
            });
        }
        hudView.SetSquad(entries);
    }

    // ---------------------------------------------------------------- screens
    void RefreshScreens()
    {
        if (state == null || leaving) return;
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
        if (screen == null) screen = RogueScreenView.Open(this);
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
            add(RogueIcons.ForItem(def), DisplayName(def), EffectLine(def, me.build), "$" + RogueMoney.Format(offer.priceMinor), RogueItemKinds.Tag(def, RarityText(def)), offer.sold ? "" : T("Buy"),
                !offer.sold && status == "" && !pending, pending ? T("Buying...") : status,
                () => Buy(me, index, offer));
        }
        // what the player owns: every core and mod can be removed here (F40). The refund is half of what was paid for it (free
        // rewards refund nothing), so buying it back always costs more than the refund.
        foreach (var id in OwnedCoresAndMods(me.build))
        {
            var def = RogueCatalog.Item(id); if (def == null) continue;
            string itemId = id;
            long refund = RogueShop.RefundMinor(me.build, id);
            bool pendingRemove = pendingTx.ContainsValue("remove:" + id);
            add(RogueIcons.ForItem(def), DisplayName(def) + "  " + TierText(me.build, id), T("Owned") + ": " + T(def.Effect), refund > 0 ? "+$" + RogueMoney.Format(refund) : T("No refund"), RogueItemKinds.Tag(def, RarityText(def)), T("Remove"),
                !pendingRemove, pendingRemove ? T("Removing...") : "", () => ConfirmRemove(me, itemId));
        }
        long reroll = RogueShop.RerollPriceMinor(state.Chapter);
        add("Reload", T("Reroll offers"), T("New offers for this visit. Limited per visit; the same offers return if you leave and come back."), "$" + RogueMoney.Format(reroll), "", T("Reroll"),
            me.rerollsLeft > 0 && me.walletMinor >= reroll, me.rerollsLeft > 0 ? T("{0} left", me.rerollsLeft) : T("No rerolls left"),
            () => Command(new RogueCommandMessage { kind = "buy", tx = new ShopTransaction { txId = NextTx(), runId = state.runId, shopVersion = me.shopVersion, reroll = true, expectedPriceMinor = reroll } }));
    }

    void ShowShop(RunPlayer me, bool chapterEnd)
    {
        screen.UseCards(false);
        screen.SetTitle("Coin", chapterEnd ? T("Chapter {0} Shop", state.Chapter) : T("Shop  Stage {0}-{1}", state.Chapter, RogueDepth.StageInChapter(state.depth)),
            T("Rerolls {0}   {1}", me.rerollsLeft, SlotSummary(me.build)), "$" + RogueMoney.Format(me.walletMinor));
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
            // an empty wallet in front of a full shop reads as "the shop is broken": say where money comes from, on its own line,
            // so the ready count (who we are waiting for) is always visible
            bool affordable = false; foreach (var o in me.offers) if (!o.sold && o.priceMinor <= me.walletMinor) affordable = true;
            string hint = ReadyText();
            if (!affordable && me.walletMinor < RogueMoney.Coins(10)) hint += "\n" + T("No money yet: kills pay bounty.");
            // the button states what you are and what pressing it does: "Ready ✓ (cancel)" rather than a bare "Not ready"
            screen.SetFooter(ready ? T("Ready (tap to cancel)") : T("Ready"), "Check", () => Command(new RogueCommandMessage { kind = "ready", flag = !ready }), null, null, null,
                hint);   // the Overview button beside it already names its key
            screen.SetFooterInteractable(true, true);
            screen.SetPrimaryHighlight(ready);
            screen.SetOverview(RogueInput.OverviewLabel, () => OpenOverview());
        }
    }

    void ShowReward(RunPlayer me)
    {
        screen.SetTitle("Check", T("Cleared: {0}", T("Stage {0}-{1}", state.Chapter, RogueDepth.StageInChapter(state.depth))), T("Pick one. It is free."), "$" + RogueMoney.Format(me.walletMinor));
        screen.UseCards(true);
        screen.ClearRows();
        for (int i = 0; i < me.rewardOffers.Length; i++)
        {
            var offer = me.rewardOffers[i]; var def = RogueCatalog.Item(offer.itemId);
            if (def == null) continue;
            int index = i;
            string status = me.build.RejectReason(def) != null ? T(me.build.RejectReason(def)) : "";
            Action take = () => Command(new RogueCommandMessage { kind = "buy", tx = new ShopTransaction { txId = NextTx(), runId = state.runId, shopVersion = me.shopVersion, offerIndex = index, expectedPriceMinor = 0, rewardPick = true } });
            string tag = RogueItemKinds.Tag(def, RarityText(def));
            if (screen.AddCard(RogueIcons.ForItem(def), DisplayName(def), tag, EffectLine(def, me.build), T("Take"), status == "" && pendingTx.Count == 0, status, take) == null)
                screen.AddRow(RogueIcons.ForItem(def), DisplayName(def), EffectLine(def, me.build), T("Free"), tag, T("Take"), status == "" && pendingTx.Count == 0, status, take);
        }
        screen.SetFooter(null, null, null, null, "");
        screen.SetOverview(RogueInput.OverviewLabel, () => OpenOverview());
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
            screen.AddRow(RogueIcons.ForRoute(route.Tag), T(route.Name) + ": " + (map != null ? T(map.SceneName) : parts[0]), T(route.Brief), RouteRewardText(route), route.Tag == "danger" ? T("Risky") : route.Tag == "safe" ? T("Safer") : "", T("Go"),
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

    void OnTransactionResult(RogueEventMessage e)
    {
        if (e.playerKey != localKey) return;
        var parts = e.text.Split('|');
        string status = parts.Length > 0 ? parts[0] : "", reason = parts.Length > 1 ? parts[1] : "", item = parts.Length > 2 ? parts[2] : "", txId = parts.Length > 3 ? parts[3] : "";
        bool removal = parts.Length > 4 && parts[4] == "remove";
        pendingTx.Remove(txId);
        if (e.flag && !removal) ApplyTransactionEffects(txId, item);
        if (e.flag && removal) Log(T("Removed {0} (refund ${1})", ItemName(item), RogueMoney.Format((long)e.value)));
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

    /// <summary>"Tier 2/3" for cores and mods that have tiers; empty for single-tier items.</summary>
    static string TierText(PlayerBuild b, string id)
    {
        int max = RogueCatalog.MaxTier(id), tier = b.Tier(id);
        return max > 1 ? T("Tier {0}/{1}", tier, max) : "";
    }

    void ConfirmRemove(RunPlayer me, string itemId)
    {
        if (menu == null || me == null) return;
        long refund = RogueShop.RefundMinor(me.build, itemId);
        var breaks = RogueShop.RemovalBreaks(me.build, itemId);
        string message = T("Remove {0}? Its effect ends now.", ItemName(itemId)) + "\n" + (refund > 0 ? T("Refund: ${0} (half of what you paid).", RogueMoney.Format(refund)) : T("No refund: it was free."));
        if (breaks.Length > 0) { var names = new List<string>(); foreach (var b in breaks) names.Add(ItemName(b)); message += "\n" + T("These mods stop working without it: {0}", string.Join(", ", names.ToArray())); }
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
        return waiting.Count > 0 ? line + "  " + T("Waiting for: {0}", string.Join(", ", waiting.ToArray())) : line;
    }

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
            parts.Add(T("Health +{0}  Damage +{1}  Magazine +{2}  Speed +{3}", b.healthTier, b.damageTier, b.magazineTier, b.speedTier));
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

    static string EffectLine(ItemDef def, PlayerBuild b)
    {
        if (def.Kind == ItemKind.Stat)
        {
            var before = BuildStats.Compute(b); var after = b.Clone(); after.SetStatTier(def.Id, b.StatTier(def.Id) + 1); var stats = BuildStats.Compute(after);
            string now, next;
            switch (def.Id)
            {
                case "stat.health": now = Pct(before.HealthMul); next = Pct(stats.HealthMul); break;
                case "stat.damage": now = Pct(before.DamageMul); next = Pct(stats.DamageMul); break;
                case "stat.magazine": now = Pct(before.MagazineMul); next = Pct(stats.MagazineMul); break;
                default: now = Pct(before.SpeedMul); next = Pct(stats.SpeedMul); break;
            }
            return T(def.Effect) + "  " + T("Tier {0}/{1}: {2} -> {3}", b.StatTier(def.Id), def.MaxStacks, now, next);
        }
        if (def.Kind == ItemKind.Tactical && !string.IsNullOrEmpty(b.tactical) && b.tactical != def.Id) return T(def.Effect) + "  " + T("Replaces {0}.", ItemName(b.tactical));
        if (def.Kind == ItemKind.Ultimate && !string.IsNullOrEmpty(b.ultimate) && b.ultimate != def.Id) return T(def.Effect) + "  " + T("Replaces {0} (charge is kept).", ItemName(b.ultimate));
        if (def.Kind == ItemKind.Weapon && b.primaryWeapon >= 0) return T(def.Effect) + "  " + T("Replaces {0}.", WeaponCatalogName(b.primaryWeapon));
        string slotLine = SlotLine(def, b);
        // cores and mods now have tiers (F44/F45): an owned one offers the next tier
        int maxTier = RogueCatalog.MaxTier(def.Id), tier = b.Tier(def.Id);
        if ((def.Kind == ItemKind.Core || def.Kind == ItemKind.Mod) && maxTier > 1) slotLine = (tier > 0 ? T("Upgrade to tier {0}/{1}", tier + 1, maxTier) : T("Tier {0}/{1}", 1, maxTier)) + (slotLine == "" ? "" : "   " + slotLine);
        return slotLine == "" ? T(def.Effect) : T(def.Effect) + "\n" + slotLine;
    }

    static string WeaponCatalogName(int index) { return index >= 0 && index < Flats.Core.WeaponCatalog.Count ? RogueItemKinds.WeaponDisplayName(RogueHooks.MetaWeaponDisplay(index, Flats.Core.WeaponCatalog.GetDefault(index)).gunName) : "?"; }
    static string Pct(double mul) { return (mul >= 1 ? "+" : "") + Math.Round((mul - 1) * 100) + "%"; }
    static string RarityText(ItemDef def) { return def.Rarity == 2 ? T("Rare") : def.Rarity == 1 ? T("Uncommon") : ""; }
    static string RouteText(string mapId, string routeTag) { var m = RogueCatalog.Map(mapId); var r = RogueCatalog.Route(routeTag); return (m != null ? T(m.SceneName) : mapId) + " (" + T(r.Name) + ")"; }
    static string RouteRewardText(RouteDef r) { return T("Bounty {0}%", (r.BudgetMul >= 1 ? "+" : "") + Math.Round((r.BudgetMul - 1) * 100)); }
}
