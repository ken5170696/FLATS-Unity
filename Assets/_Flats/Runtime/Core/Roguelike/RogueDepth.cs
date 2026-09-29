using System;

namespace Flats.Core.Roguelike
{
    /// <summary>
    /// Bounded depth curves. Depth is the 1-based stage index of a run and has no designed
    /// end; every curve here saturates so that depth 1,000,000 produces finite, sane values
    /// (no overflow, no NaN, no unbounded object counts). Difficulty tiers shift the curves
    /// instead of multiplying them without limit.
    /// </summary>
    public static class RogueDepth
    {
        public const int StagesPerChapter = 5;
        public const int MaxDepth = 1000000;
        public const int MaxDifficulty = 3;

        public static int Clamp(int depth) { return depth < 1 ? 1 : (depth > MaxDepth ? MaxDepth : depth); }
        public static int ClampDifficulty(int difficulty) { return difficulty < 1 ? 1 : (difficulty > MaxDifficulty ? MaxDifficulty : difficulty); }

        public static int ChapterOf(int depth) { return (Clamp(depth) - 1) / StagesPerChapter + 1; }
        public static int StageInChapter(int depth) { return (Clamp(depth) - 1) % StagesPerChapter + 1; }
        public static bool IsFinale(int depth) { return StageInChapter(depth) == StagesPerChapter; }

        /// <summary>Saturating growth: 1 + max * (1 - exp(-depth / scale)). Never exceeds 1 + max.</summary>
        public static double Saturate(int depth, double max, double scale)
        {
            depth = Clamp(depth);
            return 1.0 + max * (1.0 - Math.Exp(-(depth - 1) / scale));
        }

        /// <summary>Enemy hit-point multiplier: reaches ~+150% around chapter 8 and caps at +300%.</summary>
        public static double EnemyHealth(int depth, int difficulty)
        {
            double d = ClampDifficulty(difficulty);
            return Math.Min(4.5, Saturate(depth, 3.0, 40.0) * (0.85 + 0.15 * d));
        }

        /// <summary>Enemy health scale for the squad size: +12% per extra player on top of the larger wave counts, so four
        /// players face both more and sturdier enemies without turning every rifleman into a bullet sponge.</summary>
        public static double SquadHealthScale(int players)
        {
            int p = players < 1 ? 1 : (players > 4 ? 4 : players);
            return 1.0 + 0.12 * (p - 1);
        }

        /// <summary>Enemy outgoing damage multiplier; hard-capped at +120% so late enemies never one-shot a full-health player.</summary>
        public static double EnemyDamage(int depth, int difficulty)
        {
            double d = ClampDifficulty(difficulty);
            return Math.Min(2.2, Saturate(depth, 1.2, 60.0) * (0.9 + 0.1 * d));
        }

        /// <summary>Enemy stat tier 0..5 used by the legacy AI colour/score table (attack == defense).</summary>
        public static int EnemyStatTier(int depth, int difficulty)
        {
            int tier = (Clamp(depth) - 1) / 4 + (ClampDifficulty(difficulty) - 1);
            return tier < 0 ? 0 : (tier > 5 ? 5 : tier);
        }

        /// <summary>Concurrent enemies allowed on the field. Bounded for performance regardless of depth.</summary>
        public static int ConcurrentEnemyCap(int depth, int difficulty, int players)
        {
            int p = players < 1 ? 1 : (players > 4 ? 4 : players);
            int cap = 6 + (int)Math.Round(Saturate(depth, 1.0, 30.0) * 4) + p * 2 + (ClampDifficulty(difficulty) - 1) * 2;
            return cap > 24 ? 24 : cap;
        }

        /// <summary>Total enemy weight of a stage's regular waves (before elites/finale). Bounded.</summary>
        public static int StageEnemyBudget(int depth, int difficulty, int players)
        {
            int p = players < 1 ? 1 : (players > 4 ? 4 : players);
            double baseCount = 8 + 6 * (Saturate(depth, 2.0, 25.0) - 1.0);
            double perPlayer = 1.0 + 0.55 * (p - 1);
            double diff = 0.9 + 0.15 * ClampDifficulty(difficulty);
            int count = (int)Math.Round(baseCount * perPlayer * diff);
            return count < 6 ? 6 : (count > 90 ? 90 : count);
        }

        /// <summary>Per-player combat income budget in coins (G). Grows with depth but saturates, matching the price table.</summary>
        public static long BudgetCoins(int depth, int difficulty)
        {
            double g = 80.0 * Saturate(depth, 6.0, 30.0) * (1.0 + 0.1 * (ClampDifficulty(difficulty) - 1));
            return (long)Math.Round(g);
        }

        /// <summary>Price multiplier by chapter: fixed, readable steps; saturates at x4.</summary>
        public static double PriceMultiplier(int chapter)
        {
            if (chapter < 1) chapter = 1;
            double[] table = { 1.0, 1.3, 1.6, 2.0, 2.4, 2.8, 3.2, 3.6, 4.0 };
            return chapter - 1 < table.Length ? table[chapter - 1] : 4.0;
        }

        /// <summary>Elite share of enemies: 0 in chapter 1, rising to 35% of the budget by chapter 6.</summary>
        public static double EliteFraction(int depth, int difficulty)
        {
            int chapter = ChapterOf(depth);
            double f = chapter <= 1 ? 0.0 : Math.Min(0.35, 0.07 * (chapter - 1));
            return Math.Min(0.5, f + 0.05 * (ClampDifficulty(difficulty) - 1));
        }
    }
}
