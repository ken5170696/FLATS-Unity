using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    /// <summary>
    /// Where the meta layer touches a run: Heat and squad meta power on the encounter plan,
    /// Starter Kit mods at creation, the shop's armory filter, and the facts a finished run
    /// hands to MetaProgression. The authority calls these; clients read the replicated result.
    /// </summary>
    public static class MetaRun
    {
        /// <summary>Squad meta power of the connected players (their replicated loadouts).</summary>
        public static double SquadPower(RunState run)
        {
            var list = new List<MetaLoadout>();
            if (run != null) foreach (var p in run.players) if (p.connected && p.build != null) list.Add(p.build.meta);
            return MetaBalance.AveragePower(list);
        }

        public static int SquadAverageLevel(RunState run)
        {
            int sum = 0, n = 0;
            if (run != null) foreach (var p in run.players) if (p.connected && p.build != null && p.build.meta != null) { sum += Math.Max(1, p.build.meta.level); n++; }
            return n == 0 ? 1 : (int)Math.Round(sum / (double)n);
        }

        public static int SquadHighestLevel(RunState run)
        {
            int best = 1;
            if (run != null) foreach (var p in run.players) if (p.connected && p.build != null && p.build.meta != null) best = Math.Max(best, p.build.meta.level);
            return best;
        }

        /// <summary>
        /// Heat modifiers and squad-power scaling on a freshly planned stage. Deterministic: extra elites
        /// are promoted from the front of each wave, so every client derives the same plan from the snapshot.
        /// </summary>
        public static void ApplyToPlan(EncounterPlan plan, int heat, double squadPower)
        {
            if (plan == null) return;
            var h = RogueHeat.Total(heat);
            plan.enemyHealthMul *= (1 + h.EnemyHealth) * MetaBalance.EnemyHealthMul(squadPower);
            plan.enemyDamageMul *= (1 + h.EnemyDamage) * MetaBalance.EnemyDamageMul(squadPower);
            plan.concurrentCap = Math.Min(28, plan.concurrentCap + h.EnemyCap);
            if (h.EliteFraction > 0 && plan.waves != null)
            {
                for (int wi = 0; wi < plan.waves.Length; wi++)
                {
                    var w = plan.waves[wi];
                    if (w == null || w.elite == null) continue;
                    int total = w.elite.Length, elites = 0;
                    foreach (var e in w.elite) if (e) elites++;
                    // expected extra elites = total * fraction; the fractional part becomes one more elite with that probability,
                    // drawn from the plan's own id so every client promotes the same enemies (small early waves still get elites)
                    double expected = total * h.EliteFraction;
                    int extra = (int)Math.Floor(expected);
                    if (Unit(plan.encounterId, wi) < expected - extra) extra++;
                    int want = Math.Min(total, elites + extra);
                    for (int i = 0; i < total && elites < want; i++)
                        if (!w.elite[i]) { w.elite[i] = true; elites++; if (w.weights != null && i < w.weights.Length) w.weights[i] *= RogueCatalog.EliteWeightMultiplier; }
                }
                int sum = 0; foreach (var w in plan.waves) if (w != null && w.weights != null) foreach (var x in w.weights) sum += x;
                plan.totalWeight = sum;
            }
        }

        static double Unit(int encounterId, int wave)
        {
            ulong x = unchecked((ulong)(encounterId * 73856093) ^ (ulong)(wave * 19349663) ^ 0x9E3779B97F4A7C15UL);
            x ^= x >> 33; x = unchecked(x * 0xff51afd7ed558ccdUL); x ^= x >> 33;
            return (x >> 11) * (1.0 / (1UL << 53));
        }

        public static int Rerolls(int baseRerolls, int heat) { return Math.Max(0, baseRerolls + RogueHeat.Total(heat).Rerolls); }

        public static double BleedOutMul(int heat) { return RogueHeat.Total(heat).BleedOutMul; }

        /// <summary>Starter Kit: players with the skill receive random common mods at depth 1 (the authority's RNG, so it is replicated).</summary>
        public static List<string> ApplyStarterMods(RunPlayer p, RogueRng rng)
        {
            var given = new List<string>();
            if (p == null || p.build == null) return given;
            int count = BuildStats.Compute(p.build).StarterMods;
            for (int k = 0; k < count; k++)
            {
                var pool = new List<ItemDef>();
                foreach (var m in RogueCatalog.Mods) if (m.Rarity == 0 && p.build.RejectReason(m) == null && !MissingPrerequisite(m, p.build)) pool.Add(m);
                if (pool.Count == 0) break;
                var pick = pool[rng.Range(0, pool.Count)];
                p.build.Apply(pick);
                given.Add(pick.Id);
            }
            return given;
        }

        // mods whose effect needs a core (sustained fire, burst extender, spotter) are never handed out without it
        static bool MissingPrerequisite(ItemDef m, PlayerBuild b)
        {
            switch (m.Id)
            {
                case "mod.sustained_fire": return !b.HasCore("core.suppression");
                case "mod.burst_extender": return !b.HasCore("core.reloadburst");
                case "mod.spotter": case "mod.bounty_hunter": case "mod.team_radio": return !b.HasCore("core.marker");
                case "mod.double_dash": return b.tactical != "tactical.dash";
            }
            return false;
        }

        /// <summary>The armory weapon a shop weapon item gives this player: their owned variant of that model (the original first), or null.</summary>
        public static RangedWeaponDef ShopVariant(PlayerBuild b, int model)
        {
            if (b == null || b.meta == null) return RogueArmory.OriginalOf(model);
            var owned = b.meta.unlocked ?? new string[0];
            var original = RogueArmory.OriginalOf(model);
            if (original != null && (original.Starter || Array.IndexOf(owned, original.Id) >= 0)) return original;
            foreach (var w in RogueArmory.Ranged) if (w.BaseModel == model && (w.Starter || Array.IndexOf(owned, w.Id) >= 0)) return w;
            return null;
        }

        /// <summary>Shop filter: only models the player owns a variant of, never the model already in the secondary slot.</summary>
        public static bool ShopOffers(PlayerBuild b, int model)
        {
            if (b == null || b.meta == null || b.meta.Empty) return true;   // no meta loadout: legacy behaviour
            var sec = RogueArmory.Weapon(b.meta.secondary);
            if (sec != null && sec.BaseModel == model) return false;
            return ShopVariant(b, model) != null;
        }

        /// <summary>Facts for one player of an ended run. Kills and rescues are the authority's counters; the rest is the owner's tally.</summary>
        public static RunFacts Facts(RunState run, string key, double seconds, int meleeKills, string[] weaponKills, int heat)
        {
            var f = new RunFacts();
            if (run == null) return f;
            var me = run.Player(key);
            f.RunId = run.runId; f.End = run.end; f.Difficulty = run.difficulty; f.Heat = heat;
            f.DeepestDepth = run.deepestDepth; f.Seconds = seconds;
            f.StagesCleared = run.history != null ? run.history.Length : 0;
            foreach (var h in run.history ?? new EncounterHistory[0])
            {
                if (!string.IsNullOrEmpty(h.objectiveId)) f.Objectives++;
                if (!string.IsNullOrEmpty(h.eventId)) f.Events++;
                if (!string.IsNullOrEmpty(h.finaleId)) f.FinalesCleared++;
            }
            int players = 0; foreach (var p in run.players) if (p.connected) players++;
            f.Players = Math.Max(1, players);
            if (me != null)
            {
                f.Kills = me.kills; f.Headshots = me.headshots; f.Rescues = me.rescues; f.Afk = me.afk;
                f.Level = me.build != null && me.build.meta != null ? me.build.meta.level : 1;
                f.Primary = me.build != null && me.build.meta != null ? me.build.meta.primary : "";
                f.Secondary = me.build != null && me.build.meta != null ? me.build.meta.secondary : "";
                f.Melee = me.build != null && me.build.meta != null ? me.build.meta.melee : "";
            }
            f.MeleeKills = Math.Max(0, Math.Min(meleeKills, f.Kills));
            f.WeaponKills = ClampKills(weaponKills, f.Kills);
            f.SquadAverageLevel = SquadAverageLevel(run);
            f.SquadHighestLevel = SquadHighestLevel(run);
            return f;
        }

        // the owner's per-weapon tally can never exceed the kills the authority counted for that player
        static string[] ClampKills(string[] entries, int authorityKills)
        {
            var list = new List<string>();
            int left = Math.Max(0, authorityKills);
            foreach (var e in entries ?? new string[0])
            {
                string id; int n;
                if (!MetaProfiles.SplitCount(e, out id, out n) || n <= 0 || !RogueArmory.IsUnlockable(id)) continue;
                int k = Math.Min(n, left); left -= k;
                if (k > 0) list.Add(id + "|" + k);
            }
            return list.ToArray();
        }
    }
}
