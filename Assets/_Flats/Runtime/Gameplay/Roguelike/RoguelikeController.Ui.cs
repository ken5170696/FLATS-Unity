using System;
using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.UI;

// HUD text and the run screens (shop, reward pick, route, chapter end). The screen is an
// authored prefab (Prefabs/UI/Roguelike/RogueScreen.prefab) instantiated under the shared Menu
// canvas; rows instantiate the authored RogueOfferRow template. Presentation only: every
// click becomes a command to the authority.
public partial class RoguelikeController
{
    RogueScreenView screen;
    string screenMode = "";              // "", shop, reward, route, chapterend
    int txCounter;
    readonly Dictionary<string, string> pendingTx = new Dictionary<string, string>();   // txId -> itemId while awaiting the authority

    // ---------------------------------------------------------------- HUD
    void RefreshHud()
    {
        if (scoreText == null || state == null) return;
        var me = LocalPlayer;
        string money = me != null ? RogueMoney.Format(me.walletMinor) : "0";
        string stage = T("Stage {0}-{1}", state.Chapter, RogueDepth.StageInChapter(state.depth));
        string line;
        switch (state.phase)
        {
            case RunPhase.Prep: line = "$" + money + "  " + stage + "  " + T("Prep: shop open, ready when done"); break;
            case RunPhase.Combat:
                {
                    var enc = state.encounter;
                    var def = RogueCatalog.Encounter(enc.IsFinale ? enc.finaleId : enc.objectiveId);
                    string progress = objectiveRunner != null ? objectiveRunner.ProgressText : (objectiveDone ? "done" : objectiveKills + "/" + objectiveKillsNeeded);
                    line = "$" + money + "  " + stage + "  " + (def != null ? T(def.Name) : "") + " " + progress + "  " + T("Enemies {0}", AliveEnemies);
                    if (me != null && !string.IsNullOrEmpty(me.build.ultimate)) line += "  " + T("Ult {0}%", me.ultimateCharge);
                    break;
                }
            case RunPhase.Cleared: line = "$" + money + "  " + stage + "  " + T("Cleared"); break;
            case RunPhase.Reward: line = "$" + money + "  " + stage + "  " + T("Pick a reward"); break;
            case RunPhase.Route: line = "$" + money + "  " + T("Chapter {0} complete: choose a route", state.Chapter); break;
            case RunPhase.ChapterEnd: line = "$" + money + "  " + T("Chapter shop: continue or evacuate"); break;
            default: line = "$" + money + "  " + stage; break;
        }
        scoreText.text = line;
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

    void ShowShop(RunPlayer me, bool chapterEnd)
    {
        screen.SetTitle(chapterEnd ? T("Chapter {0} Shop", state.Chapter) : T("Shop  Stage {0}-{1}", state.Chapter, RogueDepth.StageInChapter(state.depth)),
            T("Wallet ${0}   Rerolls {1}   {2}", RogueMoney.Format(me.walletMinor), me.rerollsLeft, BuildSummary(me.build)));
        screen.ClearRows();
        for (int i = 0; i < me.offers.Length; i++)
        {
            var offer = me.offers[i]; var def = RogueCatalog.Item(offer.itemId);
            if (def == null) continue;
            int index = i;
            string status = offer.sold ? T("Bought") : me.build.RejectReason(def) != null ? T(me.build.RejectReason(def)) : me.walletMinor < offer.priceMinor ? T("Not enough money") : "";
            bool pending = pendingTx.ContainsValue(offer.itemId);
            screen.AddRow(T(def.Name), EffectLine(def, me.build), "$" + RogueMoney.Format(offer.priceMinor), RarityText(def), offer.sold ? "" : T("Buy"),
                !offer.sold && status == "" && !pending, pending ? T("Buying...") : status,
                () => Buy(me, index, offer));
        }
        long reroll = RogueShop.RerollPriceMinor(state.Chapter);
        screen.AddRow(T("Reroll offers"), T("New offers for this visit. Limited per visit; the same offers return if you leave and come back."), "$" + RogueMoney.Format(reroll), "", T("Reroll"),
            me.rerollsLeft > 0 && me.walletMinor >= reroll, me.rerollsLeft > 0 ? "" : T("No rerolls left"),
            () => Command(new RogueCommandMessage { kind = "buy", tx = new ShopTransaction { txId = NextTx(), runId = state.runId, shopVersion = me.shopVersion, reroll = true, expectedPriceMinor = reroll } }));
        if (chapterEnd)
        {
            screen.SetFooter(T("Continue"), () => Command(new RogueCommandMessage { kind = "continue" }), T("Evacuate"), () => ConfirmEvacuate(),
                T("Continue travels to {0}. Evacuate banks this run's record and ends it.", RouteText(state.mapId, state.routeTag)));
        }
        else
        {
            bool ready = me.ready;
            screen.SetFooter(T(ready ? "Not ready" : "Ready"), () => Command(new RogueCommandMessage { kind = "ready", flag = !ready }), null, null,
                ReadyText());
        }
    }

    void ShowReward(RunPlayer me)
    {
        screen.SetTitle(T("Cleared: {0}", T("Stage {0}-{1}", state.Chapter, RogueDepth.StageInChapter(state.depth))), T("Pick one. It is free."));
        screen.ClearRows();
        for (int i = 0; i < me.rewardOffers.Length; i++)
        {
            var offer = me.rewardOffers[i]; var def = RogueCatalog.Item(offer.itemId);
            if (def == null) continue;
            int index = i;
            string status = me.build.RejectReason(def) != null ? T(me.build.RejectReason(def)) : "";
            screen.AddRow(T(def.Name), EffectLine(def, me.build), T("Free"), RarityText(def), T("Take"), status == "" && pendingTx.Count == 0, status,
                () => Command(new RogueCommandMessage { kind = "buy", tx = new ShopTransaction { txId = NextTx(), runId = state.runId, shopVersion = me.shopVersion, offerIndex = index, expectedPriceMinor = 0, rewardPick = true } }));
        }
        screen.SetFooter(null, null, null, null, "");
    }

    void ShowRoute(RunPlayer me)
    {
        screen.SetTitle(T("Chapter {0} complete", state.Chapter), T(IsAuthority ? "Choose the next chapter's route." : "The host chooses the route."));
        screen.ClearRows();
        for (int i = 0; i < state.routeOptions.Length; i++)
        {
            var parts = state.routeOptions[i].Split('|');
            var map = RogueCatalog.Map(parts[0]); var route = RogueCatalog.Route(parts.Length > 1 ? parts[1] : "");
            int index = i;
            screen.AddRow(T(route.Name) + ": " + (map != null ? T(map.SceneName) : parts[0]), T(route.Brief), RouteRewardText(route), route.Tag == "danger" ? T("Risky") : route.Tag == "safe" ? T("Safer") : "", T("Go"),
                IsAuthority, IsAuthority ? "" : T("Host decides"), () => Command(new RogueCommandMessage { kind = "route", index = index }));
        }
        screen.SetFooter(null, null, null, null, "");
    }

    void Buy(RunPlayer me, int index, ShopOffer offer)
    {
        string txId = NextTx();
        pendingTx[txId] = offer.itemId;
        Command(new RogueCommandMessage { kind = "buy", tx = new ShopTransaction { txId = txId, runId = state.runId, shopVersion = me.shopVersion, offerIndex = index, expectedPriceMinor = offer.priceMinor } });
        RefreshScreens();
    }

    string NextTx() { return localKey + ":" + (++txCounter) + ":" + DateTime.UtcNow.Ticks; }

    void OnTransactionResult(RogueEventMessage e)
    {
        if (e.playerKey != localKey) return;
        var parts = e.text.Split('|');
        string status = parts.Length > 0 ? parts[0] : "", reason = parts.Length > 1 ? parts[1] : "", item = parts.Length > 2 ? parts[2] : "", txId = parts.Length > 3 ? parts[3] : "";
        pendingTx.Remove(txId);
        if (e.flag) ApplyTransactionEffects(txId, item);
        if (e.flag) Log(item == "" ? T("Rerolled") : e.minor > 0 ? T("Bought {0} for ${1}", ItemName(item), RogueMoney.Format(e.minor)) : T("Bought {0}", ItemName(item)));
        else if (status != "Duplicate") Log(T("Purchase failed: {0}", T(reason == "" ? status : reason)));
        if (screen != null) RefreshScreens();
    }

    void ConfirmEvacuate()
    {
        if (menu == null) return;
        menu.ShowConfirm("Evacuate", "End the run here and bank your record? The checkpoint is removed.", ok => { if (ok) Command(new RogueCommandMessage { kind = "evacuate" }); }, T("Evacuate"), T("Cancel"));
    }

    string ReadyText()
    {
        int ready = 0, total = 0;
        foreach (var p in state.players) if (p.connected) { total++; if (p.ready) ready++; }
        return total <= 1 ? T("Press Ready to start the stage.") : T("Ready {0}/{1}. The stage starts when everyone is ready.", ready, total);
    }

    static string BuildSummary(PlayerBuild b)
    {
        var parts = new List<string>();
        foreach (var c in b.cores) parts.Add(ItemName(c));
        if (!string.IsNullOrEmpty(b.tactical)) parts.Add(ItemName(b.tactical));
        if (!string.IsNullOrEmpty(b.ultimate)) parts.Add(ItemName(b.ultimate));
        parts.Add(T("Mods {0}/{1}", b.mods.Length, RogueCatalog.MaxMods));
        parts.Add("HP+" + b.healthTier + " DMG+" + b.damageTier + " MAG+" + b.magazineTier + " SPD+" + b.speedTier);
        return string.Join("  ", parts.ToArray());
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
        return T(def.Effect);
    }

    static string WeaponCatalogName(int index) { return index >= 0 && index < Flats.Core.WeaponCatalog.Count ? Flats.Core.WeaponCatalog.GetDefault(index).gunName : "?"; }
    static string Pct(double mul) { return (mul >= 1 ? "+" : "") + Math.Round((mul - 1) * 100) + "%"; }
    static string RarityText(ItemDef def) { return def.Rarity == 2 ? T("Rare") : def.Rarity == 1 ? T("Uncommon") : ""; }
    static string RouteText(string mapId, string routeTag) { var m = RogueCatalog.Map(mapId); var r = RogueCatalog.Route(routeTag); return (m != null ? T(m.SceneName) : mapId) + " (" + T(r.Name) + ")"; }
    static string RouteRewardText(RouteDef r) { return T("Bounty {0}%", (r.BudgetMul >= 1 ? "+" : "") + Math.Round((r.BudgetMul - 1) * 100)); }
}
