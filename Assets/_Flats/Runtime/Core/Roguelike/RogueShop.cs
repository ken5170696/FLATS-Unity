using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    [Serializable]
    public sealed class ShopOffer
    {
        public string itemId;
        public long priceMinor;
        public bool sold;
        public bool free;           // reward picks are free offers
        public int tierAtSample;
    }

    public enum TransactionStatus { Ok = 0, Duplicate = 1, WrongVersion = 2, UnknownItem = 3, PriceMismatch = 4, NotAllowed = 5, InsufficientFunds = 6, AlreadySold = 7, WrongPhase = 8, RerollsExhausted = 9, NotOwned = 10 }

    [Serializable]
    public sealed class ShopTransaction
    {
        public string txId;         // client-generated unique id (playerKey + counter); dedup key
        public string playerKey;
        public string runId;
        public int shopVersion;     // the version the client saw
        public int offerIndex;      // -1 with itemId for direct buys (supplies)
        public string itemId;
        public long expectedPriceMinor;
        public bool skipReward;     // Reward -> one run-local reroll ticket, mutually exclusive with a pick
        public bool useRerollTicket; // requires reroll, expected price zero; does not use the visit quota
        public bool reroll;         // reroll request instead of a purchase
        public bool rewardPick;     // picking from the free reward offers
        public bool remove;
        public string removeItemId;
        public long expectedRefundMinor;
    }

    public sealed class TransactionResult
    {
        public TransactionStatus Status;
        public string Reason = "";
        public string ItemId = "";
        public string ReplacedItemId;
        public long PaidMinor;
        public long RefundMinor;
        public int NewShopVersion;
        public bool Ok { get { return Status == TransactionStatus.Ok; } }
    }

    /// <summary>
    /// Personal shop: each player samples their own inventory from a seed derived from the run,
    /// the depth, the player and the shop version, so the same visit always shows the same
    /// offers (no free rerolls by re-opening, reloading or reconnecting). Purchases are atomic:
    /// validate everything, then deduct, then apply, then bump the version.
    /// </summary>
    public static class RogueShop
    {
        public const int MaxRerollsPerVisit = 2, MaxRerollsChapterEnd = 3, RerollBaseCoins = 10;
        public const int RewardChoices = 3;
        public const int RefundNumerator = 1, RefundDenominator = 2;

        public static long RefundMinor(PlayerBuild build, string itemId)
        {
            return build == null ? 0 : Math.Max(0, build.PaidMinor(itemId)) * RefundNumerator / RefundDenominator;
        }

        public static string[] RemovalBreaks(PlayerBuild build, string itemId)
        {
            if (build == null) return new string[0];
            var after = build.Clone();
            if (!after.Remove(itemId)) return new string[0];
            var broken = new List<string>();
            foreach (var id in after.mods)
                if (!MissingPrerequisite(RogueCatalog.Item(id), build) && MissingPrerequisite(RogueCatalog.Item(id), after)) broken.Add(id);
            return broken.ToArray();
        }

        public static long RerollPriceMinor(int chapter) { return RogueMoney.Coins((long)Math.Round(RerollBaseCoins * RogueDepth.PriceMultiplier(chapter))); }

        /// <summary>Samples a visit's offers. Chapter-end (finale cleared) shops are wider.</summary>
        public static ShopOffer[] Sample(RogueRng root, string runSalt, int depth, string playerKey, int shopVersion, PlayerBuild build, string routeTag, bool chapterEnd)
        {
            var rng = root.Derive("shop:" + runSalt + ":" + playerKey + ":" + depth, shopVersion);
            int chapter = RogueDepth.ChapterOf(depth);
            var route = RogueCatalog.Route(routeTag);
            var offers = new List<ShopOffer>();
            var taken = new HashSet<string>();
            Action<ItemDef> add = def =>
            {
                if (def == null || !taken.Add(def.Id)) return;
                offers.Add(new ShopOffer { itemId = def.Id, priceMinor = RogueCatalog.PriceMinor(def, chapter, routeTag, build.Owned(def.Id)), tierAtSample = build.Owned(def.Id) });
            };

            // 1. supplies: always one ammo and one persistent overshield
            add(RogueCatalog.Item("supply.ammo"));
            add(RogueCatalog.Item("supply.medkit"));

            // 2. one stat upgrade with a gap preference (lowest tier first)
            add(PickStat(rng, build));

            // 3. a core: guaranteed while the player has none (pity), otherwise 60% (always at chapter end)
            bool needsCore = build.cores.Length == 0;
            if (needsCore || chapterEnd || rng.Chance(0.6 + route.ShopRarityBonus)) add(PickByTag(rng, RogueCatalog.Cores, build, AffinityTags(build), 0.75, taken));
            if (chapterEnd) add(PickByTag(rng, RogueCatalog.Cores, build, AffinityTags(build), 0.3, taken));

            // 4. mods: two, one biased to the current affinity and one generic/transition option
            add(PickByTag(rng, RogueCatalog.Mods, build, AffinityTags(build), 0.85, taken));
            add(PickByTag(rng, RogueCatalog.Mods, build, new[] { RogueCatalog.TagGeneric }, 0.6, taken));
            if (chapterEnd) add(PickByTag(rng, RogueCatalog.Mods, build, AffinityTags(build), 0.5, taken));

            // 5. tactical or ultimate: ultimate guaranteed while none is owned from depth 2; otherwise alternate
            if (string.IsNullOrEmpty(build.ultimate) && depth >= 2) add(PickByTag(rng, RogueCatalog.Ultimates, build, AffinityTags(build), 0.7, taken));
            else if (string.IsNullOrEmpty(build.tactical)) add(PickByTag(rng, RogueCatalog.Tacticals, build, AffinityTags(build), 0.5, taken));
            else if (chapterEnd || rng.Chance(0.5)) add(PickByTag(rng, rng.Chance(0.5) ? RogueCatalog.Ultimates : RogueCatalog.Tacticals, build, AffinityTags(build), 0.5, taken));

            // 6. a weapon: only when not already the primary; chapter end offers a second
            add(PickWeapon(rng, build, taken));
            if (chapterEnd) add(PickWeapon(rng, build, taken));

            return offers.ToArray();
        }

        /// <summary>Three free choices after a stage clear: one stat, one mod and one core/tactical pick; a saturated build gets sideways swaps.</summary>
        public static ShopOffer[] SampleReward(RogueRng root, string runSalt, int depth, string playerKey, PlayerBuild build, string routeTag)
        {
            var rng = root.Derive("reward:" + runSalt + ":" + playerKey + ":" + depth);
            var taken = new HashSet<string>();
            var offers = new List<ShopOffer>();
            Action<ItemDef> add = def => { if (def != null && taken.Add(def.Id)) offers.Add(new ShopOffer { itemId = def.Id, priceMinor = 0, free = true, tierAtSample = build.Owned(def.Id) }); };
            add(PickStat(rng, build));
            add(PickByTag(rng, RogueCatalog.Mods, build, AffinityTags(build), 0.8, taken));
            ItemDef third = null;
            if (build.cores.Length == 0) third = PickByTag(rng, RogueCatalog.Cores, build, AffinityTags(build), 0.5, taken);
            else if (string.IsNullOrEmpty(build.tactical) && rng.Chance(0.5)) third = PickByTag(rng, RogueCatalog.Tacticals, build, AffinityTags(build), 0.5, taken);
            else if (rng.Chance(0.4)) third = PickByTag(rng, RogueCatalog.Cores, build, AffinityTags(build), 0.5, taken);
            if (third == null) third = PickByTag(rng, RogueCatalog.Mods, build, new[] { RogueCatalog.TagGeneric }, 0.5, taken);
            add(third);
            while (offers.Count < RewardChoices) { var extra = PickByTag(rng, RogueCatalog.Mods, build, null, 0, taken); if (extra == null) break; add(extra); }
            // a full build still gets three real choices: sideways swaps (tactical, ultimate, weapon) instead of an empty or shrinking pick
            if (offers.Count < RewardChoices) add(PickByTag(rng, RogueCatalog.Tacticals, build, AffinityTags(build), 0.5, taken));
            if (offers.Count < RewardChoices) add(PickByTag(rng, RogueCatalog.Ultimates, build, AffinityTags(build), 0.5, taken));
            while (offers.Count < RewardChoices) { var weapon = PickWeapon(rng, build, taken); if (weapon == null) break; add(weapon); }
            return offers.ToArray();
        }

        /// <summary>
        /// An item whose effect needs something the build does not have yet (a core, a tactical, a weapon family) and would do nothing
        /// on its own. Such items are not sampled, so a paid slot is never spent on a dead effect; owning the prerequisite brings them back.
        /// Only sampling uses this: RejectReason and old checkpoints are unchanged.
        /// </summary>
        public static bool MissingPrerequisite(ItemDef def, PlayerBuild build)
        {
            if (def == null || build == null) return false;
            bool ricochet = build.HasCore("core.ricochet") || build.HasMod("mod.double_bounce");
            bool marks = build.HasCore("core.marker") || (build.HasMod("mod.angle_finder") && ricochet);
            switch (def.Id)
            {
                case "mod.sustained_fire": return !build.HasCore("core.suppression");
                case "mod.burst_extender": return !build.HasCore("core.reloadburst");
                case "mod.double_dash": return build.tactical != "tactical.dash";
                case "mod.rubber_rounds": return !ricochet;
                case "mod.angle_finder": return !ricochet;
                case "mod.spotter":
                case "mod.bounty_hunter": return !marks;
                // marks are simulated on the shooter's client and not replicated, so "a teammate hits your mark" cannot be observed yet;
                // the mod is kept for old checkpoints but no longer offered
                case "mod.team_radio": return true;
                case "mod.bigger_boom":
                case "mod.shockwave": return !build.HasCore("core.demolition");
                case "mod.choke": return !CarriesWeapon(build, 8, 9);
                // one more pierce or bounce, up to two: nothing is left to add once the core's tier already gives two
                case "mod.piercing_rounds": return !Changes(def, build, s => s.PenetrateDepth);
                case "mod.double_bounce": return !Changes(def, build, s => s.RicochetBounces);
            }
            return false;
        }

        /// <summary>Whether owning the mod changes one derived number of this build (compared with the same build without it).</summary>
        static bool Changes(ItemDef mod, PlayerBuild build, Func<BuildStats, int> read)
        {
            var without = build.Clone(); without.Remove(mod.Id);
            var with = without.Clone();
            if (with.mods.Length >= RogueCatalog.MaxMods) return true;   // no free slot: RejectReason decides, not this rule
            with.Apply(mod);
            return read(BuildStats.Compute(with)) != read(BuildStats.Compute(without));
        }

        /// <summary>An unknown primary (-1: a co-op joiner's character default) counts as possibly matching, so nobody is starved; an unknown
        /// secondary is the character's sidearm, never a shotgun.</summary>
        static bool CarriesWeapon(PlayerBuild build, int first, int last)
        {
            if (build.primaryWeapon < 0) return true;
            return (build.primaryWeapon >= first && build.primaryWeapon <= last) || (build.secondaryWeapon >= first && build.secondaryWeapon <= last);
        }

        public static string[] AffinityTags(PlayerBuild build)
        {
            var tags = new List<string>();
            foreach (var c in build.cores) { var d = RogueCatalog.Item(c); if (d != null) foreach (var t in d.Tags) if (!tags.Contains(t)) tags.Add(t); }
            if (tags.Count == 0) tags.Add(RogueCatalog.TagGeneric);
            return tags.ToArray();
        }

        private static ItemDef PickStat(RogueRng rng, PlayerBuild build)
        {
            var candidates = new List<ItemDef>();
            var weights = new List<double>();
            foreach (var s in RogueCatalog.Stats)
            {
                int tier = build.StatTier(s.Id);
                if (tier >= s.MaxStacks || !build.StatTierHasEffect(s.Id)) continue;   // a tier that the total cap would swallow is not sold
                candidates.Add(s); weights.Add(1.0 + Math.Min(5, RogueCatalog.StatTiers - tier) * 0.5);   // capped at the five-tier weight: more tiers must not crowd cores and mods out of the offers
            }
            int i = rng.WeightedIndex(weights);
            return i < 0 ? null : candidates[i];
        }

        private static ItemDef PickWeapon(RogueRng rng, PlayerBuild build, HashSet<string> taken)
        {
            var candidates = new List<ItemDef>();
            for (int w = 0; w < WeaponCatalog.Count; w++)
            {
                var def = RogueCatalog.Weapon(w);
                if (w == build.primaryWeapon || w == build.secondaryWeapon || taken.Contains(def.Id)) continue;
                if (!MetaRun.ShopOffers(build, w)) continue;   // only models the player owns an armory variant of
                candidates.Add(def);
            }
            return candidates.Count == 0 ? null : rng.Pick(candidates);
        }

        /// <summary>Weighted pick: items sharing a preferred tag get `affinity` extra weight; rarer items are rarer.</summary>
        private static ItemDef PickByTag(RogueRng rng, ItemDef[] pool, PlayerBuild build, string[] preferredTags, double affinity, HashSet<string> taken)
        {
            var candidates = new List<ItemDef>();
            var weights = new List<double>();
            foreach (var def in pool)
            {
                if (taken.Contains(def.Id) || build.RejectReason(def) != null || MissingPrerequisite(def, build)) continue;
                double w = def.Rarity == 2 ? 0.5 : def.Rarity == 1 ? 0.8 : 1.0;
                if (preferredTags != null) foreach (var t in preferredTags) if (def.HasTag(t)) { w += affinity; break; }
                candidates.Add(def); weights.Add(w);
            }
            int i = rng.WeightedIndex(weights);
            return i < 0 ? null : candidates[i];
        }

        /// <summary>
        /// Atomic purchase. `walletMinor` and `build` are mutated only on success. The caller (authority) supplies the
        /// player's processed transaction ids for de-duplication and persists the returned result.
        /// </summary>
        public static TransactionResult Apply(ShopTransaction tx, ref long walletMinor, PlayerBuild build, ShopOffer[] offers, ref int shopVersion,
            ref int rerollsLeft, List<string> processedTx, bool shopOpen, string runId, RunPhase phase = RunPhase.Prep)
        {
            var r = new TransactionResult { NewShopVersion = shopVersion };
            if (tx == null || string.IsNullOrEmpty(tx.txId)) { r.Status = TransactionStatus.NotAllowed; r.Reason = "missing transaction id"; return r; }
            if (processedTx != null && processedTx.Contains(tx.txId)) { r.Status = TransactionStatus.Duplicate; r.Reason = "already processed"; return r; }
            if (tx.skipReward || tx.useRerollTicket) { r.Status = TransactionStatus.NotAllowed; r.Reason = "ticket transactions require RunMachine"; return r; }
            if (tx.remove && (tx.rewardPick || (phase != RunPhase.Prep && phase != RunPhase.ChapterEnd))) { r.Status = TransactionStatus.WrongPhase; r.Reason = "cannot remove outside a shop"; return r; }
            if (!shopOpen) { r.Status = TransactionStatus.WrongPhase; r.Reason = "shop closed"; return r; }
            if (tx.runId != runId) { r.Status = TransactionStatus.WrongVersion; r.Reason = "different run"; return r; }
            if (tx.shopVersion != shopVersion) { r.Status = TransactionStatus.WrongVersion; r.Reason = "shop changed"; return r; }

            if (tx.remove)
            {
                var item = RogueCatalog.Item(tx.removeItemId);
                if (item == null || (item.Kind != ItemKind.Core && item.Kind != ItemKind.Mod) || !build.Has(item.Id))
                { r.Status = TransactionStatus.NotOwned; r.Reason = "core or mod not owned"; return r; }
                long refund = RefundMinor(build, item.Id);
                if (refund != tx.expectedRefundMinor) { r.Status = TransactionStatus.PriceMismatch; r.Reason = "refund changed"; return r; }
                if (refund > RogueMoney.MaxWallet - walletMinor) { r.Status = TransactionStatus.NotAllowed; r.Reason = "wallet limit"; return r; }
                build.Remove(item.Id); walletMinor += refund; shopVersion++;
                r.Status = TransactionStatus.Ok; r.ItemId = item.Id; r.RefundMinor = refund; r.NewShopVersion = shopVersion;
                Remember(processedTx, tx.txId);
                return r;
            }

            if (tx.reroll)
            {
                if (rerollsLeft <= 0) { r.Status = TransactionStatus.RerollsExhausted; r.Reason = "no rerolls left"; return r; }
                long price = tx.expectedPriceMinor;
                if (price < 0) { r.Status = TransactionStatus.PriceMismatch; r.Reason = "invalid price"; return r; }
                if (walletMinor < price) { r.Status = TransactionStatus.InsufficientFunds; r.Reason = "not enough money"; return r; }
                walletMinor -= price; rerollsLeft--; shopVersion++;
                r.Status = TransactionStatus.Ok; r.PaidMinor = price; r.NewShopVersion = shopVersion;
                Remember(processedTx, tx.txId);
                return r;
            }

            ShopOffer offer = null;
            ItemDef def;
            if (tx.offerIndex >= 0)
            {
                if (offers == null || tx.offerIndex >= offers.Length) { r.Status = TransactionStatus.UnknownItem; r.Reason = "no such offer"; return r; }
                offer = offers[tx.offerIndex];
                def = RogueCatalog.Item(offer.itemId);
                if (offer.sold) { r.Status = TransactionStatus.AlreadySold; r.Reason = "already bought"; return r; }
            }
            else def = RogueCatalog.Item(tx.itemId);
            if (def == null) { r.Status = TransactionStatus.UnknownItem; r.Reason = "unknown item"; return r; }
            if (offer == null && def.Kind != ItemKind.Supply) { r.Status = TransactionStatus.NotAllowed; r.Reason = "only supplies can be bought outside the offers"; return r; }
            r.ItemId = def.Id;

            if (offer != null && (def.Kind == ItemKind.Core || def.Kind == ItemKind.Mod) && offer.tierAtSample != build.Tier(def.Id))
            { r.Status = TransactionStatus.PriceMismatch; r.Reason = "tier changed"; return r; }

            long cost = offer != null ? (offer.free ? 0 : offer.priceMinor) : RogueCatalog.PriceMinor(def, 1, "", 0);
            if (tx.expectedPriceMinor != cost) { r.Status = TransactionStatus.PriceMismatch; r.Reason = "price changed"; return r; }
            string reject = build.RejectReason(def);
            if (reject != null) { r.Status = TransactionStatus.NotAllowed; r.Reason = reject; return r; }
            if (walletMinor < cost) { r.Status = TransactionStatus.InsufficientFunds; r.Reason = "not enough money"; return r; }
            if (cost < 0 || ((def.Kind == ItemKind.Core || def.Kind == ItemKind.Mod) && cost > RogueMoney.MaxWallet - build.PaidMinor(def.Id)))
            { r.Status = TransactionStatus.PriceMismatch; r.Reason = "invalid cumulative payment"; return r; }

            // commit
            walletMinor -= cost;
            if (def.Kind != ItemKind.Supply) r.ReplacedItemId = build.Apply(def, cost);
            if (offer != null)
            {
                offer.sold = true;
                if (offer.free) foreach (var o in offers) if (o.free) o.sold = true; // one reward pick per stage
            }
            shopVersion++;
            r.Status = TransactionStatus.Ok; r.PaidMinor = cost; r.NewShopVersion = shopVersion;
            Remember(processedTx, tx.txId);
            return r;
        }

        private static void Remember(List<string> processed, string txId)
        {
            if (processed == null) return;
            processed.Add(txId);
            if (processed.Count > 64) processed.RemoveRange(0, processed.Count - 64);
        }
    }
}
