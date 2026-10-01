using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    /// <summary>On-disk run checkpoint. Separate from the Classic profile; its own schema.</summary>
    [Serializable]
    public sealed class RunSaveDocument
    {
        public const int CurrentSchema = 2;
        public int schema = CurrentSchema;
        public string savedAtUtc = "";
        public string gameVersion = "";
        public string localPlayerKey = "";     // whose checkpoint this is (solo or the host's copy)
        public RunState run = new RunState();
    }

    /// <summary>Out-of-run progress: records and unlocks only. Never permanent power.</summary>
    [Serializable]
    public sealed class RogueMetaDocument
    {
        public const int CurrentSchema = 1;
        public int schema = CurrentSchema;
        public int runsStarted, runsEvacuated, runsWiped;
        public int deepestDepth;
        public long mostEarnedMinor;
        public int mostKills, mostHeadshots;
        public string[] seenItems = new string[0];       // codex: which cores/mods/ultimates were ever owned
        public string[] clearedFinales = new string[0];
        public string lastRunId = "";
    }

    public static class RogueSave
    {
        public const string RunFileName = "roguelike-run-v1.json";
        public const string MetaFileName = "roguelike-meta-v1.json";

        /// <summary>Migrate before validation/resume. Unknown schemas are left intact for Validate to reject.</summary>
        public static void Migrate(RunSaveDocument doc)
        {
            if (doc == null || doc.schema < 1 || doc.schema > RunSaveDocument.CurrentSchema || doc.run == null) return;
            bool legacy = doc.schema == 1;
            var run = doc.run;
            foreach (var p in run.players ?? new RunPlayer[0])
            {
                if (p == null || p.build == null) continue;
                if (legacy) { p.build.coreTiers = null; p.build.modTiers = null; p.build.corePaidMinor = null; p.build.modPaidMinor = null; }
                p.build.Normalize();
                if (legacy)
                {
                    foreach (var o in p.offers ?? new ShopOffer[0]) if (o != null) o.tierAtSample = p.build.Owned(o.itemId);
                    foreach (var o in p.rewardOffers ?? new ShopOffer[0]) if (o != null) o.tierAtSample = p.build.Owned(o.itemId);
                }
            }
            if (legacy)
            {
                run.rewardPaidPlayers = new string[0];
                if (run.ledger != null && run.ledger.depth == run.depth && run.encounter != null)
                {
                    run.paidDepth = run.depth;
                    run.ledger.objectivePaid = run.ledger.objectiveMinor == 0 || run.phase == RunPhase.ChapterEnd;
                    var def = RogueCatalog.Encounter(run.encounter.IsFinale ? run.encounter.finaleId : run.encounter.objectiveId);
                    run.ledger.objectiveMinor = RogueMoney.MulFraction(run.ledger.budgetMinor, def == null ? 0 : def.RewardFraction);
                    var picked = new List<string>();
                    foreach (var p in run.players ?? new RunPlayer[0])
                    {
                        if (p == null) continue;
                        bool paid = run.phase == RunPhase.ChapterEnd;
                        foreach (var o in p.rewardOffers ?? new ShopOffer[0]) if (o != null && o.sold) paid = true;
                        if (paid) picked.Add(p.key);
                    }
                    run.rewardPaidPlayers = picked.ToArray();
                }
            }
            MetaRun.MigrateProgress(run);
            doc.schema = RunSaveDocument.CurrentSchema; run.schema = RunSaveDocument.CurrentSchema;
        }

        /// <summary>Structural validation for a loaded run. Errors mean "do not resume"; the caller shows them.</summary>
        public static List<string> Validate(RunSaveDocument doc)
        {
            var errors = new List<string>();
            if (doc == null) { errors.Add("empty document"); return errors; }
            if (doc.schema > RunSaveDocument.CurrentSchema) { errors.Add("newer save format (" + doc.schema + ")"); return errors; }
            if (doc.schema < 1) errors.Add("invalid schema");
            if (doc.schema >= 1 && doc.schema <= RunSaveDocument.CurrentSchema) Migrate(doc);
            var run = doc.run;
            if (run == null) { errors.Add("missing run"); return errors; }
            if (string.IsNullOrEmpty(run.runId)) errors.Add("missing run id");
            if (run.rulesVersion != RogueCatalog.RulesVersion) errors.Add("rules version " + run.rulesVersion + " differs from " + RogueCatalog.RulesVersion);
            // A different content hash (reworded text, tuned prices) is not a reason to lose a run: every id the checkpoint
            // references is checked below, so an update that keeps those ids resumes. See ContentChanged.
            if (double.IsNaN(run.elapsedSeconds) || double.IsInfinity(run.elapsedSeconds) || run.elapsedSeconds < 0) errors.Add("invalid elapsed seconds");
            if (run.totalStagesCleared < 0 || run.totalObjectives < 0 || run.totalEvents < 0 || run.totalFinales < 0) errors.Add("invalid progress totals");
            if (run.depth < 1 || run.depth > RogueDepth.MaxDepth) errors.Add("depth out of range");
            if (run.difficulty < 1 || run.difficulty > RogueDepth.MaxDifficulty) errors.Add("difficulty out of range");
            if (run.phase != RunPhase.Prep && run.phase != RunPhase.ChapterEnd) errors.Add("checkpoint is not at a safe boundary");
            if (run.players == null || run.players.Length == 0 || run.players.Length > 4) errors.Add("player count out of range");
            if (RogueCatalog.Map(run.mapId) == null) errors.Add("unknown map " + run.mapId);
            // collections the run machine dereferences without null checks: a checksum-valid but truncated file must be refused here,
            // not crash on the first purchase
            if (run.encounter == null) errors.Add("missing encounter");
            if (run.ledger == null) errors.Add("missing ledger");
            if (run.history == null) errors.Add("missing history");
            if (run.routeOptions == null) errors.Add("missing route options");
            if (run.rescuesPaid == null) errors.Add("missing rescue record");
            if (run.rewardPaidPlayers == null) errors.Add("missing reward payment record");
            if (run.paidDepth < 0 || run.paidDepth > run.depth) errors.Add("paid depth out of range");
            if (run.phase == RunPhase.ChapterEnd && !string.IsNullOrEmpty(run.routeTag) && RogueCatalog.Route(run.routeTag).Tag != run.routeTag) errors.Add("unknown route " + run.routeTag);
            var keys = new HashSet<string>();
            if (run.players != null)
                foreach (var p in run.players)
                {
                    if (p == null || string.IsNullOrEmpty(p.key)) { errors.Add("player without key"); continue; }
                    if (!keys.Add(p.key)) errors.Add("duplicate player " + p.key);
                    if (p.walletMinor < 0 || p.walletMinor > RogueMoney.MaxWallet) errors.Add("wallet out of range for " + p.key);
                    if (p.refundedMinor < 0) errors.Add("refund total out of range for " + p.key);
                    if (double.IsNaN(p.overshieldFraction) || double.IsInfinity(p.overshieldFraction) || p.overshieldFraction < 0 || p.overshieldFraction > 1) errors.Add("overshield out of range for " + p.key);
                    if (p.ultimateCharge < 0 || p.ultimateCharge > 100) errors.Add("ultimate charge out of range for " + p.key);
                    if (p.rerollTickets < 0) errors.Add("negative reroll tickets for " + p.key);
                    if (p.rerollsLeft < 0 || p.rerollsLeft > RogueShop.MaxRerollsChapterEnd) errors.Add("rerolls out of range for " + p.key);
                    if (p.processedTx == null || p.offers == null || p.rewardOffers == null) errors.Add("missing shop record for " + p.key);
                    if (p.build == null) { errors.Add("missing build for " + p.key); continue; }
                    foreach (var e in p.build.Validate()) errors.Add(p.key + ": " + e);
                    if (p.offers != null) foreach (var o in p.offers) if (o == null || RogueCatalog.Item(o.itemId) == null || o.priceMinor < 0) errors.Add("bad offer for " + p.key);
                    if (p.rewardOffers != null) foreach (var o in p.rewardOffers) if (o == null || RogueCatalog.Item(o.itemId) == null) errors.Add("bad reward for " + p.key);
                }
            return errors;
        }

        /// <summary>The checkpoint was written by a build with different content (text, prices, tuning). It still resumes when Validate passes;
        /// the caller refreshes run.contentHash and tells the player that prices and descriptions follow the new build.</summary>
        public static bool ContentChanged(RunSaveDocument doc)
        {
            return doc != null && doc.run != null && doc.run.contentHash != RogueCatalog.ContentHash();
        }

        /// <summary>Applies a run's end to the meta record; idempotent per run id.</summary>
        public static bool RecordRunEnd(RogueMetaDocument meta, RunState run, string localPlayerKey)
        {
            if (meta == null || run == null || run.phase != RunPhase.Ended || meta.lastRunId == run.runId) return false;
            meta.lastRunId = run.runId;
            if (run.end == RunEnd.Evacuated) meta.runsEvacuated++; else if (run.end == RunEnd.Wiped) meta.runsWiped++;
            if (run.deepestDepth > meta.deepestDepth) meta.deepestDepth = run.deepestDepth;
            var me = run.Player(localPlayerKey);
            if (me != null)
            {
                if (me.earnedMinor > meta.mostEarnedMinor) meta.mostEarnedMinor = me.earnedMinor;
                if (me.kills > meta.mostKills) meta.mostKills = me.kills;
                if (me.headshots > meta.mostHeadshots) meta.mostHeadshots = me.headshots;
                var seen = new List<string>(meta.seenItems);
                foreach (var id in me.build.cores) if (!seen.Contains(id)) seen.Add(id);
                foreach (var id in me.build.mods) if (!seen.Contains(id)) seen.Add(id);
                if (!string.IsNullOrEmpty(me.build.ultimate) && !seen.Contains(me.build.ultimate)) seen.Add(me.build.ultimate);
                if (!string.IsNullOrEmpty(me.build.tactical) && !seen.Contains(me.build.tactical)) seen.Add(me.build.tactical);
                meta.seenItems = seen.ToArray();
            }
            var cleared = new List<string>(meta.clearedFinales);
            foreach (var h in run.history) if (!string.IsNullOrEmpty(h.finaleId) && !cleared.Contains(h.finaleId)) cleared.Add(h.finaleId);
            meta.clearedFinales = cleared.ToArray();
            return true;
        }
    }
}
