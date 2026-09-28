using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    [Serializable]
    public sealed class WavePlan
    {
        public string[] roles = new string[0];   // role ids, one per enemy
        public bool[] elite = new bool[0];       // parallel to roles
        public int[] weights = new int[0];       // bounty weights (hundredths), parallel
        public double releaseAfterSeconds;       // earliest release relative to combat start
    }

    /// <summary>What one stage asks of the squad. Produced by the authority once per stage and replicated.</summary>
    [Serializable]
    public sealed class EncounterPlan
    {
        public int encounterId;
        public int depth, difficulty, players;
        public string mapId = "", routeTag = "";
        public string objectiveId = "", eventId = "", emergencyId = "", finaleId = "";
        public WavePlan[] waves = new WavePlan[0];
        public int concurrentCap;
        public double enemyHealthMul = 1, enemyDamageMul = 1;
        public int enemyStatTier;
        public int totalWeight;

        public bool IsFinale { get { return !string.IsNullOrEmpty(finaleId); } }
    }

    /// <summary>A stage that already happened, for cooldown and anti-repetition sampling.</summary>
    [Serializable]
    public sealed class EncounterHistory
    {
        public int depth;
        public string objectiveId = "", eventId = "", emergencyId = "", finaleId = "";
    }

    /// <summary>
    /// Encounter director: chapter/depth set the base difficulty, the plan sets what the stage is,
    /// and Pacing (below) decides when pressure and rest happen from what the squad is going
    /// through — never from measured player DPS. Content sampling respects map compatibility,
    /// cooldowns, mutual exclusion and recency so finite content does not repeat back to back.
    /// </summary>
    public static class RogueDirector
    {
        public const double BaseEventChance = 0.45, BaseEmergencyChance = 0.30;
        public const int RecentWindow = 3;

        public static EncounterPlan Plan(RogueRng root, string runSalt, int encounterId, int depth, int difficulty, int players, MapDef map, string routeTag, IList<EncounterHistory> history)
        {
            var rng = root.Derive("director:" + runSalt, depth);
            var route = RogueCatalog.Route(routeTag);
            var tags = new List<string>(map != null ? map.Tags : new string[0]);
            var plan = new EncounterPlan
            {
                encounterId = encounterId, depth = depth, difficulty = difficulty, players = Math.Max(1, Math.Min(4, players)),
                mapId = map != null ? map.Id : "", routeTag = routeTag ?? "",
                concurrentCap = RogueDepth.ConcurrentEnemyCap(depth, difficulty, players),
                enemyHealthMul = RogueDepth.EnemyHealth(depth, difficulty), enemyDamageMul = RogueDepth.EnemyDamage(depth, difficulty),
                enemyStatTier = RogueDepth.EnemyStatTier(depth, difficulty),
            };

            bool finale = RogueDepth.IsFinale(depth);
            if (finale)
            {
                var fin = Choose(rng, RogueCatalog.Finales, depth, tags, history, h => h.finaleId, null);
                plan.finaleId = fin != null ? fin.Id : RogueCatalog.Finales[0].Id;
                plan.objectiveId = "";
            }
            else
            {
                var obj = Choose(rng, RogueCatalog.Objectives, depth, tags, history, h => h.objectiveId, null);
                plan.objectiveId = obj != null ? obj.Id : "obj.clear";
                var exclusive = new List<string>(RogueCatalog.Encounter(plan.objectiveId).Exclusive) { plan.objectiveId };

                double eventChance = Math.Min(1.0, BaseEventChance * route.EventChanceMul);
                if (depth >= 2 && rng.Chance(eventChance))
                {
                    var ev = Choose(rng, RogueCatalog.Events, depth, tags, history, h => h.eventId, exclusive);
                    if (ev != null) { plan.eventId = ev.Id; exclusive.Add(ev.Id); exclusive.AddRange(ev.Exclusive); }
                }
                double emergencyChance = Math.Min(1.0, BaseEmergencyChance * route.EventChanceMul) * (routeTag == "safe" ? 0.0 : 1.0);
                // at most one emergency, never on the first two stages, never on a stage that already has a strongly spatial objective it excludes
                if (depth >= 3 && rng.Chance(emergencyChance))
                {
                    var em = Choose(rng, RogueCatalog.Emergencies, depth, tags, history, h => h.emergencyId, exclusive);
                    if (em != null) plan.emergencyId = em.Id;
                }
            }

            plan.waves = PlanWaves(rng, depth, difficulty, plan.players, route, finale);
            int total = 0;
            foreach (var w in plan.waves) foreach (var wt in w.weights) total += wt;
            plan.totalWeight = total;
            return plan;
        }

        /// <summary>Weighted choice honouring min depth, map tags, cooldown, exclusion and recency; null when nothing fits.</summary>
        public static EncounterDef Choose(RogueRng rng, EncounterDef[] pool, int depth, IList<string> mapTags, IList<EncounterHistory> history, Func<EncounterHistory, string> idOf, IList<string> exclusive)
        {
            var candidates = new List<EncounterDef>();
            var weights = new List<double>();
            foreach (var def in pool)
            {
                if (depth < def.MinDepth || !def.CompatibleWith(mapTags)) continue;
                if (exclusive != null && (exclusive.IndexOf(def.Id) >= 0 || Overlaps(def.Exclusive, exclusive))) continue;
                double w = def.Weight;
                int last = LastDepth(history, idOf, def.Id);
                if (last > 0)
                {
                    int since = depth - last;
                    if (since <= def.Cooldown) continue;              // hard cooldown in stages
                    if (since <= RecentWindow) w *= 0.25;              // soft anti-repetition
                }
                candidates.Add(def); weights.Add(w);
            }
            int i = rng.WeightedIndex(weights);
            return i < 0 ? null : candidates[i];
        }

        private static bool Overlaps(string[] a, IList<string> b) { foreach (var x in a) if (b.IndexOf(x) >= 0) return true; return false; }

        private static int LastDepth(IList<EncounterHistory> history, Func<EncounterHistory, string> idOf, string id)
        {
            int last = 0;
            if (history != null) foreach (var h in history) if (idOf(h) == id && h.depth > last) last = h.depth;
            return last;
        }

        /// <summary>
        /// Splits the stage budget into 2-4 waves. Role weights unlock by depth; elites take a bounded share.
        /// A finale replaces the last regular wave with the finale target (weight x6) plus its guard.
        /// </summary>
        public static WavePlan[] PlanWaves(RogueRng rng, int depth, int difficulty, int players, RouteDef route, bool finale)
        {
            int budget = RogueDepth.StageEnemyBudget(depth, difficulty, players);
            if (finale) budget = Math.Max(6, budget * 2 / 3);
            int waveCount = budget <= 10 ? 2 : budget <= 24 ? 3 : 4;
            var roles = new List<EnemyRoleDef>();
            var roleWeights = new List<double>();
            foreach (var r in RogueCatalog.EnemyRoles)
            {
                if (depth < r.MinDepth) continue;
                double w = r.Id == "role.rifleman" ? 3.0 : 1.0;
                if (r.Id == "role.jammer" || r.Id == "role.shieldbearer") w = 0.6;
                roles.Add(r); roleWeights.Add(w);
            }
            double eliteFraction = Math.Min(0.6, RogueDepth.EliteFraction(depth, difficulty) * route.EliteMul);
            var waves = new WavePlan[waveCount];
            int remaining = budget;
            for (int i = 0; i < waveCount; i++)
            {
                int count = i == waveCount - 1 ? remaining : Math.Max(2, budget / waveCount);
                count = Math.Min(count, remaining);
                remaining -= count;
                var rolesOut = new List<string>(); var elite = new List<bool>(); var weights = new List<int>();
                int shieldCount = 0, jammerCount = 0;
                for (int k = 0; k < count; k++)
                {
                    var role = roles[rng.WeightedIndex(roleWeights)];
                    if (role.Id == "role.shieldbearer" && shieldCount >= 2) role = roles[0];
                    if (role.Id == "role.jammer" && jammerCount >= 1) role = roles[0];
                    if (role.Id == "role.shieldbearer") shieldCount++;
                    if (role.Id == "role.jammer") jammerCount++;
                    bool isElite = i > 0 && rng.Chance(eliteFraction);
                    rolesOut.Add(role.Id); elite.Add(isElite); weights.Add(isElite ? role.Weight * RogueCatalog.EliteWeightMultiplier : role.Weight);
                }
                waves[i] = new WavePlan { roles = rolesOut.ToArray(), elite = elite.ToArray(), weights = weights.ToArray(), releaseAfterSeconds = i == 0 ? 0 : 25 + 20 * i };
            }
            if (finale)
            {
                // the finale target is the first entry of the last wave; the rest is its guard
                var last = waves[waveCount - 1];
                var r = new List<string>(last.roles); var e = new List<bool>(last.elite); var w = new List<int>(last.weights);
                r.Insert(0, "role.finale"); e.Insert(0, true); w.Insert(0, 100 * RogueCatalog.FinaleWeightMultiplier);
                last.roles = r.ToArray(); last.elite = e.ToArray(); last.weights = w.ToArray();
            }
            return waves;
        }

        /// <summary>Total spawn count of a plan (for the enemy-count budget check).</summary>
        public static int CountEnemies(EncounterPlan plan)
        {
            int n = 0;
            foreach (var w in plan.waves) n += w.roles.Length;
            return n;
        }
    }

    /// <summary>
    /// Pressure pacing. Inputs are squad state, not measured output. It tells the adapter when
    /// to release the next wave and when to hold (rest window after heavy damage or a down).
    /// </summary>
    public sealed class RoguePacing
    {
        public double Pressure;                 // 0..1
        public double RestUntil;                // absolute seconds
        public const double RestAfterDown = 8, RestAfterHeavyDamage = 4, MinWaveGap = 12;
        private double lastRelease = -1000;

        public void OnPlayerDamaged(double now, double fraction)
        {
            Pressure = Math.Min(1, Pressure + fraction * 0.5);
            if (fraction >= 0.3) RestUntil = Math.Max(RestUntil, now + RestAfterHeavyDamage);
        }

        public void OnPlayerDowned(double now)
        {
            Pressure = 1;
            RestUntil = Math.Max(RestUntil, now + RestAfterDown);
        }

        public void Tick(double dt)
        {
            Pressure = Math.Max(0, Pressure - dt * 0.08);
        }

        /// <summary>Release when the wave's earliest time passed, the rest window ended, the field has room and pressure is not maxed.</summary>
        public bool ShouldRelease(double now, WavePlan wave, int aliveEnemies, int concurrentCap, bool objectiveActive, bool emergencyActive)
        {
            if (now < wave.releaseAfterSeconds) return false;
            if (now < RestUntil) return false;
            if (now - lastRelease < MinWaveGap) return false;
            if (aliveEnemies + wave.roles.Length > concurrentCap && aliveEnemies > concurrentCap / 3) return false;
            if (emergencyActive && aliveEnemies > concurrentCap / 2) return false;
            if (Pressure > 0.85 && aliveEnemies > 2) return false;
            return true;
        }

        public void Released(double now) { lastRelease = now; }
    }
}
