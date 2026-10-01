using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Flats.Core.Roguelike
{
    public sealed class SimulationRow
    {
        public int Depth, EffectiveUpgrades, Cores, StatTiers;
        public string PlayerKey;
        public long IncomeMinor, SpendMinor, WalletMinor, BaseBountyMinor, BountyMinor, HeadshotBonusMinor, ConsumableSpendMinor;
        public double HeadshotIncomeFraction { get { return IncomeMinor == 0 ? 0 : (double)HeadshotBonusMinor / IncomeMinor; } }
    }
    public sealed class SimulationResult
    {
        public readonly List<SimulationRow> Rows = new List<SimulationRow>();
        public RunState FinalState;
        public string ToMarkdown()
        {
            var b = new StringBuilder("| 關卡 | 玩家 | 收入 | 支出 | 錢包 | 核心 | 屬性階 | 有效升級 | 爆頭加成占收入 |\n|---:|---|---:|---:|---:|---:|---:|---:|---:|\n");
            foreach (var r in Rows) b.AppendFormat(CultureInfo.InvariantCulture, "| {0} | {1} | {2:F2} | {3:F2} | {4:F2} | {5} | {6} | {7} | {8:P2} |\n", r.Depth, r.PlayerKey, r.IncomeMinor / 100.0, r.SpendMinor / 100.0, r.WalletMinor / 100.0, r.Cores, r.StatTiers, r.EffectiveUpgrades, r.HeadshotIncomeFraction);
            return b.ToString();
        }
    }
    public static class RogueSimulation
    {
        /// <summary>Controlled estimate, not runtime telemetry. releasedKillFraction is the regular
        /// bounty weight actually killed; cancellation applies to the remainder, not to extra enemies.</summary>
        public static double EstimatedIncomeCoins(int depth, int difficulty, string objectiveId,
            double releasedKillFraction, double headshotRate = .5, double eventFraction = .1, bool compensate = true)
        {
            RogueStateBag.Unit(releasedKillFraction); RogueStateBag.Unit(headshotRate); RogueStateBag.Unit(eventFraction);
            var def = RogueCatalog.Encounter(objectiveId);
            if (def == null) throw new ArgumentException("objectiveId");
            double g = RogueDepth.BudgetCoins(depth, difficulty);
            double compensation = compensate && objectiveId.StartsWith("obj.", StringComparison.Ordinal) && objectiveId != "obj.clear"
                ? Math.Min(releasedKillFraction, .8 * (1 - releasedKillFraction)) : 0;
            return g * (releasedKillFraction * (1 + headshotRate * .5) + def.RewardFraction + eventFraction + compensation);
        }
        public static SimulationResult Run(long seed, int players, int difficulty, int stages = 20, double headshotRate = .5, double eventSuccessRate = .75, int rescuePerStage = 0, double consumableSpendFraction = .2, string buildStrategy = "balanced", int startDepth = 1)
        {
            if (players < 1 || players > 4 || stages < 1 || stages > 10000 || startDepth < 1 || startDepth > 1000000 || rescuePerStage < 0) throw new ArgumentOutOfRangeException();
            RogueStateBag.Unit(headshotRate); RogueStateBag.Unit(eventSuccessRate); RogueStateBag.Unit(consumableSpendFraction);
            if (!new[] { "precision", "support", "balanced", "consumables" }.Contains(buildStrategy)) throw new ArgumentException("buildStrategy");
            var keys = Enumerable.Range(1, players).Select(i => "p" + i).ToArray();
            var state = RunMachine.Create("simulation-" + seed.ToString(CultureInfo.InvariantCulture), seed, difficulty, RogueCatalog.Maps[0].Id, keys, keys, null, null);
            // 深度壓力測試由合法 Prep 狀態開始；起始錢包仍是 0，第一關不會購買舊深度庫存。
            state.depth = startDepth;
            var run = new RunMachine(state); var random = new RogueRng(unchecked((ulong)seed)).Derive("simulation-outcomes", 0);
            var result = new SimulationResult { FinalState = state }; int tx = 0;
            for (int stage = 0; stage < stages; stage++)
            {
                var earned = state.players.Select(p => p.earnedMinor).ToArray(); var spent = state.players.Select(p => p.spentMinor).ToArray();
                var supplies = new long[players];
                for (int i = 0; i < players; i++)
                {
                    var p = state.players[i];
                    // 消耗預算以進 Prep 時可用錢包為基礎；所有策略先留一包彈藥（10 coins）。
                    long allowance = (long)Math.Floor(p.walletMinor * consumableSpendFraction);
                    long ammoCost = RogueCatalog.PriceMinor(RogueCatalog.Item("supply.ammo"), 1, "", 0);
                    while (allowance >= ammoCost && p.walletMinor >= ammoCost + RogueMoney.Coins(10))
                    {
                        var purchase = run.Buy(new ShopTransaction { txId = "sim-" + (++tx), runId = state.runId, playerKey = p.key, shopVersion = p.shopVersion, offerIndex = -1, itemId = "supply.ammo", expectedPriceMinor = ammoCost });
                        if (!purchase.Ok) throw new InvalidOperationException(purchase.Reason); allowance -= purchase.PaidMinor; supplies[i] += purchase.PaidMinor;
                    }
                    BuyUpgrades(run, p, buildStrategy, false, ref tx);
                    string preferred = buildStrategy == "precision" ? "core.precision" : buildStrategy == "support" ? "core.marker" : "";
                    // 專精策略保留一個核心槽，利用合法付費重抽尋找主核心。
                    while (preferred.Length > 0 && !p.build.HasCore(preferred) && p.rerollsLeft > 0)
                    {
                        long rerollCost = RogueShop.RerollPriceMinor(state.Chapter);
                        long coreCost = RogueCatalog.PriceMinor(RogueCatalog.Item(preferred), state.Chapter, state.routeTag, 0);
                        if (p.walletMinor < rerollCost + coreCost + RogueMoney.Coins(10)) break;
                        var reroll = run.Buy(new ShopTransaction { txId = "sim-" + (++tx), runId = state.runId, playerKey = p.key, shopVersion = p.shopVersion, reroll = true, expectedPriceMinor = rerollCost });
                        if (!reroll.Ok) throw new InvalidOperationException(reroll.Reason);
                        BuyUpgrades(run, p, buildStrategy, false, ref tx);
                    }
                }
                if (!run.BeginCombat(RogueCatalog.Map(state.mapId))) throw new InvalidOperationException("BeginCombat");
                var bounty = new long[players]; var headBonus = new long[players]; long baseBounty = 0;
                foreach (var slot in state.ledger.slots)
                {
                    bool head = random.Chance(headshotRate); var payout = run.EnemyKilled(slot.instanceId, keys[(slot.instanceId - 1) % players], head); baseBounty += slot.minor;
                    for (int i = 0; i < players; i++) { long paid; if (payout.Minor.TryGetValue(keys[i], out paid)) { bounty[i] += paid; headBonus[i] += paid - slot.minor; } }
                }
                run.ObjectiveCompleted();
                if (!string.IsNullOrEmpty(state.encounter.eventId)) run.EventResolved(state.encounter.eventId, random.Chance(eventSuccessRate));
                if (!string.IsNullOrEmpty(state.encounter.emergencyId)) run.EventResolved(state.encounter.emergencyId, random.Chance(eventSuccessRate));
                // 每關每位受救者最多一次；單人不合成自救收入。
                if (players > 1) for (int i = 0; i < Math.Min(rescuePerStage, players); i++) { run.PlayerDowned(keys[i]); run.Rescued(keys[(i + 1) % players], keys[i]); }
                int depth = state.depth;
                if (!run.StageCleared() || !run.EnterReward()) throw new InvalidOperationException("reward transition");
                foreach (var p in state.players) BuyUpgrades(run, p, buildStrategy, true, ref tx);
                if (!run.AdvanceAfterReward()) throw new InvalidOperationException("AdvanceAfterReward");
                if (state.phase == RunPhase.Route) { if (!run.ChooseRoute(0) || !run.ContinueChapter()) throw new InvalidOperationException("chapter transition"); }
                for (int i = 0; i < players; i++)
                {
                    var p = state.players[i]; var build = p.build;
                    int tiers = build.healthTier + build.damageTier + build.magazineTier + build.speedTier;
                    result.Rows.Add(new SimulationRow { Depth = depth, PlayerKey = p.key, IncomeMinor = p.earnedMinor - earned[i], SpendMinor = p.spentMinor - spent[i], WalletMinor = p.walletMinor, BaseBountyMinor = baseBounty, BountyMinor = bounty[i], HeadshotBonusMinor = headBonus[i], ConsumableSpendMinor = supplies[i], Cores = build.cores.Length, StatTiers = tiers, EffectiveUpgrades = build.cores.Sum(id => build.Tier(id)) + build.mods.Sum(id => build.Tier(id)) + tiers });
                }
            }
            return result;
        }
        private static int Score(ItemDef item, string strategy, PlayerBuild build)
        {
            if (item == null || build.RejectReason(item) != null) return -1;
            if (item.Id == (strategy == "precision" ? "core.precision" : strategy == "support" ? "core.marker" : "")) return 100;
            if (item.Id == (strategy == "precision" ? "stat.damage" : strategy == "support" ? "stat.health" : "")) return 90;
            if (item.Kind == ItemKind.Core)
            {
                string preferred = strategy == "precision" ? "core.precision" : strategy == "support" ? "core.marker" : "";
                if (preferred.Length > 0 && !build.HasCore(preferred) && build.cores.Length >= 1) return -1;
                return 80;
            }
            if (item.Kind == ItemKind.Stat) return 70 - build.StatTier(item.Id);
            if (item.Kind == ItemKind.Mod) return 60;
            return -1;
        }
        private static void BuyUpgrades(RunMachine run, RunPlayer p, string strategy, bool reward, ref int tx)
        {
            var offers = reward ? p.rewardOffers : p.offers;
            string preferredCore = strategy == "precision" ? "core.precision" : strategy == "support" ? "core.marker" : "";
            var indices = Enumerable.Range(0, offers.Length).OrderByDescending(i => Score(RogueCatalog.Item(offers[i].itemId), strategy, p.build)).ThenBy(i => i).ToArray();
            foreach (int i in indices)
            {
                var offer = offers[i]; var def = RogueCatalog.Item(offer.itemId);
                // a build that still lacks its main core keeps a reroll and that core's price in hand (five tiers made upgrades eat the money first)
                long reserve = !reward && preferredCore.Length > 0 && !p.build.HasCore(preferredCore) && def.Id != preferredCore
                    ? RogueShop.RerollPriceMinor(run.State.Chapter) + RogueCatalog.PriceMinor(RogueCatalog.Item(preferredCore), run.State.Chapter, run.State.routeTag, 0) : 0;
                if (offer.sold || Score(def, strategy, p.build) < 0 || (!reward && p.walletMinor - offer.priceMinor < RogueMoney.Coins(10) + reserve)) continue;
                var purchase = run.Buy(new ShopTransaction { txId = "sim-" + (++tx), runId = run.State.runId, playerKey = p.key, shopVersion = p.shopVersion, offerIndex = i, expectedPriceMinor = offer.free ? 0 : offer.priceMinor, rewardPick = reward });
                if (!purchase.Ok) throw new InvalidOperationException("simulation purchase: " + purchase.Reason);
                if (reward) break;
            }
            // 當有效升級已滿，仍須從合法獎勵中挑選戰術／大招／武器以完成實際流程。
            if (reward && !offers.Any(o => o.sold)) for (int i = 0; i < offers.Length; i++)
            {
                var fallback = RogueCatalog.Item(offers[i].itemId);
                if (p.build.RejectReason(fallback) != null) continue;
                // the fallback must not fill the last core slot with a core the strategy does not want
                string wanted = strategy == "precision" ? "core.precision" : strategy == "support" ? "core.marker" : "";
                if (fallback.Kind == ItemKind.Core && wanted.Length > 0 && fallback.Id != wanted && !p.build.HasCore(wanted) && p.build.cores.Length >= RogueCatalog.MaxCores - 1) continue;
                var purchase = run.Buy(new ShopTransaction { txId = "sim-" + (++tx), runId = run.State.runId, playerKey = p.key, shopVersion = p.shopVersion, offerIndex = i, expectedPriceMinor = 0, rewardPick = true });
                if (!purchase.Ok) throw new InvalidOperationException(purchase.Reason); break;
            }
        }
    }
}
