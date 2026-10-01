using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    /// <summary>One line of the end-of-run breakdown. Source is an English template key; Amount is exact.</summary>
    [Serializable]
    public sealed class RewardLine
    {
        public string source = "";         // template, e.g. "Kills x{0}"
        public string arg = "";            // value shown in the template
        public long xp, merits;
    }

    /// <summary>Everything a finished run grants one player. Built by MetaProgression.RunReward from authoritative run data.</summary>
    [Serializable]
    public sealed class RunReward
    {
        public string runId = "";
        public RewardLine[] lines = new RewardLine[0];
        public long xp, merits;
        public int levelBefore, levelAfter, pointsGained;
        public long levelUpMerits;
        public string[] firstTimes = new string[0];
        public string[] mastery = new string[0];      // "weaponId|milestone"
        public string[] challenges = new string[0];   // challenge ids completed by this run
        public string abuse = "";                     // why the reward was reduced, "" when normal
    }

    /// <summary>Per-player facts of a finished run, extracted from the authoritative RunState on every client.</summary>
    public sealed class RunFacts
    {
        public string RunId = "";
        public RunEnd End;
        public int StagesCleared, DeepestDepth, Difficulty, Heat, Players;
        public int Kills, Headshots, Rescues, Objectives, Events, FinalesCleared, MeleeKills;
        public double Seconds;
        public bool Afk, SecondsEstimated;
        public int SquadAverageLevel, SquadHighestLevel, Level;
        public string[] WeaponKills = new string[0];   // "weaponId|kills"
        public string Primary = "", Secondary = "", Melee = "";
        public bool UsedMeleeOnly;
    }

    /// <summary>
    /// Levels, experience and merits. Merits are the out-of-run currency (icon: medal) and are never
    /// mixed with in-run coins (RogueMoney). Merits only unlock armory content; stats come only from
    /// skill points, which come only from levels.
    /// </summary>
    public static class MetaProgression
    {
        public const int MaxLevel = 50;
        public const long OverlevelXp = 6000;            // after level 50 every chunk pays OverlevelMerits
        public const long OverlevelMerits = 60;
        public const long MaxXp = 50000000, MaxMerits = 9999999;
        public const double LowLevelBonus = 0.25;         // highest squad level - own level >= 5 → +25% experience
        public const int LowLevelGap = 5;
        public const double HeatRewardStep = 0.08;
        public const int MinStagesForReward = 1;
        public const double MinSecondsForReward = 90;
        public const int MinStagesWhenAbandoned = 2;
        /// <summary>A full run (not abandoned, 2+ stages, 15+ minutes) pays at least this many merits and at most MeritCeiling from its base lines.</summary>
        public const long MeritFloor = 165, MeritCeiling = 200; public const double FloorSeconds = 900;
        /// <summary>Base experience (before difficulty, heat and catch-up bonuses) is limited to this many per minute of the run. It is
        /// a safety net against abnormal clear speeds only: ordinary play earns about 60-110 base experience a minute (a stage pays
        /// 45 plus its objective, event, kills and headshots, about 200, and takes 2-3 minutes with prep and shop), and a fast, clean
        /// run about 130. The old limit of 50 a minute sat below ordinary play and removed 40-60% of a normal run's base experience
        /// (playtest report 2026-09-30).</summary>
        public const double BaseXpPerMinute = 150;

        /// <summary>Experience from level L to L+1. Level 2 arrives after one ordinary first run.</summary>
        public static long XpToNext(int level)
        {
            if (level < 1) level = 1;
            double k = level - 1;
            return (long)Math.Round(180 + 70 * k + 2.4 * k * k);
        }

        public static long XpForLevel(int level)
        {
            long total = 0;
            for (int l = 1; l < Math.Min(level, MaxLevel); l++) total += XpToNext(l);
            return total;
        }

        /// <summary>Level reached with <paramref name="totalXp"/> lifetime experience (1..MaxLevel).</summary>
        public static int LevelFor(long totalXp)
        {
            int level = 1;
            long need = 0;
            while (level < MaxLevel) { need += XpToNext(level); if (totalXp < need) break; level++; }
            return level;
        }

        /// <summary>Experience into the current level and the size of that level (for the bar). At max level: overlevel progress.</summary>
        public static void Progress(long totalXp, out long into, out long size)
        {
            int level = LevelFor(totalXp);
            long start = XpForLevel(level);
            if (level >= MaxLevel) { into = (totalXp - start) % OverlevelXp; size = OverlevelXp; return; }
            into = totalXp - start; size = XpToNext(level);
        }

        /// <summary>Skill points granted by a level: one per level after the first, one extra every tenth level.</summary>
        public static int PointsForLevel(int level)
        {
            level = Math.Max(1, Math.Min(MaxLevel, level));
            return (level - 1) + level / 10;
        }

        /// <summary>Merits paid for reaching <paramref name="level"/>.</summary>
        public static long MeritsForLevelUp(int level) { return 40 + 5 * (level / 5); }

        /// <summary>
        /// The end-of-run reward. Pure: the same facts always produce the same numbers, so the
        /// breakdown shown on the result screen is exactly what is written to the save.
        /// Anti-abuse: no reward without a cleared stage and 90 s of play, AFK players get none,
        /// abandoning keeps 40%, a wipe 75%, per-run totals are clamped.
        /// </summary>
        /// <summary>Why a finished run earns nothing at all (no experience, merits, mastery, challenges, first times or heat), or "".</summary>
        public static string Ineligible(RunFacts f)
        {
            if (f == null || f.End == RunEnd.None || double.IsNaN(f.Seconds) || double.IsInfinity(f.Seconds) || f.Seconds < 0) return "Invalid or unfinished run";
            if (f.Afk) return "Inactive for the whole run";
            if (f.Kills + f.Rescues <= 0) return "No kills or revives this run";
            if (f.StagesCleared < MinStagesForReward) return "Clear at least one stage to earn experience";
            if (f.Seconds < MinSecondsForReward) return "Play at least {0} seconds to earn experience";
            if (f.End == RunEnd.Abandoned && f.StagesCleared < MinStagesWhenAbandoned) return "Leaving before stage {0} earns nothing";
            return "";
        }

        public static RewardLine IneligibleLine(RunFacts f)
        {
            string reason = Ineligible(f);
            string arg = reason == "Play at least {0} seconds to earn experience" ? MinSecondsForReward.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : reason == "Leaving before stage {0} earns nothing" ? MinStagesWhenAbandoned.ToString() : "";
            return new RewardLine { source = reason, arg = arg };
        }

        public static RunReward RunReward(RunFacts f)
        {
            var r = new RunReward { runId = f == null ? "" : f.RunId ?? "" };
            var lines = new List<RewardLine>();
            r.abuse = Ineligible(f);
            if (r.abuse != "") { r.lines = new[] { IneligibleLine(f) }; return r; }
            if (f.SecondsEstimated) lines.Add(new RewardLine { source = "Legacy play time estimated ({0} min)", arg = ((int)(f.Seconds / 60)).ToString() });

            Action<string, string, long, long> add = (src, arg, xp, merits) => { if (xp != 0 || merits != 0) lines.Add(new RewardLine { source = src, arg = arg, xp = xp, merits = merits }); };
            add("Stages cleared x{0}", f.StagesCleared.ToString(), 45L * f.StagesCleared, 6L * f.StagesCleared);
            add("Kills x{0}", f.Kills.ToString(), 3L * Math.Min(f.Kills, 2000), f.Kills / 10);
            add("Headshots x{0}", f.Headshots.ToString(), 2L * Math.Min(f.Headshots, 2000), 0);
            add("Objectives x{0}", f.Objectives.ToString(), 60L * f.Objectives, 4L * f.Objectives);
            add("Events x{0}", f.Events.ToString(), 40L * f.Events, 3L * f.Events);
            add("Rescues x{0}", f.Rescues.ToString(), 35L * f.Rescues, 3L * f.Rescues);
            add("Finales x{0}", f.FinalesCleared.ToString(), 150L * f.FinalesCleared, 20L * f.FinalesCleared);
            if (f.End == RunEnd.Evacuated) add("Evacuated", "", 60, 15);

            long xpSum = 0, meritSum = 0; foreach (var l in lines) { xpSum += l.xp; meritSum += l.merits; }
            long paceCap = (long)Math.Floor(Math.Max(0, f.Seconds) / 60.0 * BaseXpPerMinute);
            if (xpSum > paceCap) { add("Run pace limit ({0} min)", ((int)(f.Seconds / 60)).ToString(), paceCap - xpSum, 0); xpSum = paceCap; }
            double mul = 1;
            if (f.Difficulty > 1) { double d = 0.15 * (f.Difficulty - 1); add("Difficulty +{0}%", RogueArmory.Pct(d), (long)Math.Round(xpSum * d), (long)Math.Round(meritSum * d)); }
            if (f.Heat > 0) { double h = HeatRewardStep * f.Heat; add("Heat {0} +{1}%", f.Heat + "|" + RogueArmory.Pct(h), (long)Math.Round(xpSum * h), (long)Math.Round(meritSum * h)); }
            if (f.SquadHighestLevel - f.Level >= LowLevelGap) add("Squad catch-up +{0}%", RogueArmory.Pct(LowLevelBonus), (long)Math.Round(xpSum * LowLevelBonus), 0);
            xpSum = 0; meritSum = 0; foreach (var l in lines) { xpSum += l.xp; meritSum += l.merits; }
            if (f.End == RunEnd.Wiped) mul = 0.75;
            else if (f.End == RunEnd.Abandoned) mul = 0.40;
            if (mul < 1)
            {
                add(f.End == RunEnd.Wiped ? "Squad wiped -{0}%" : "Left early -{0}%", RogueArmory.Pct(1 - mul), -(long)Math.Round(xpSum * (1 - mul)), -(long)Math.Round(meritSum * (1 - mul)));
            }
            // merit floor/ceiling of a full run: steady progress for newer players, no windfall from a single long run
            long meritBase = 0; foreach (var l in lines) meritBase += l.merits;
            if (f.End != RunEnd.Abandoned && f.StagesCleared >= MinStagesWhenAbandoned && f.Seconds >= FloorSeconds)
            {
                long target = Math.Min(Math.Max(meritBase, MeritFloor), MeritCeiling);
                if (target != meritBase) add(target > meritBase ? "Full run minimum" : "Run merit limit", "", 0, target - meritBase);
            }
            else if (meritBase > MeritCeiling) add("Run merit limit", "", 0, MeritCeiling - meritBase);
            long rawXp = 0, rawMerits = 0;
            foreach (var l in lines) { rawXp += l.xp; rawMerits += l.merits; }
            r.xp = Math.Max(0, Math.Min(rawXp, 60000));
            r.merits = Math.Max(0, Math.Min(rawMerits, 3000));
            add("Run experience limit", "", r.xp - rawXp, 0);
            add("Run merit safety limit", "", 0, r.merits - rawMerits);
            r.lines = lines.ToArray();
            return r;
        }
    }

    /// <summary>
    /// Heat: stacked difficulty levels unlocked by finishing a chapter at the previous heat.
    /// Each level adds one named modifier and +8% rewards. Every modifier is read from this table
    /// by the director and the adapter; the menu lists them from the same table.
    /// </summary>
    public static class RogueHeat
    {
        public const int MaxHeat = 10;
        public struct Modifier { public string Text; public double EnemyHealth, EnemyDamage, EliteFraction, BleedOutMul; public int Rerolls, EnemyCap; }

        public static readonly Modifier[] Levels =
        {
            new Modifier { Text = "Enemies have +{0}% health.", EnemyHealth = 0.10 },
            new Modifier { Text = "Enemies deal +{0}% damage.", EnemyDamage = 0.08 },
            new Modifier { Text = "Elites appear more often (+{0}% of enemies on average).", EliteFraction = 0.07 },
            new Modifier { Text = "One fewer shop reroll per visit.", Rerolls = -1 },
            new Modifier { Text = "Enemies have +{0}% health.", EnemyHealth = 0.10 },
            new Modifier { Text = "Downed players bleed out {0}% faster.", BleedOutMul = 0.70 },
            new Modifier { Text = "Enemies deal +{0}% damage.", EnemyDamage = 0.08 },
            new Modifier { Text = "Two more enemies on the field at once.", EnemyCap = 2 },
            new Modifier { Text = "Enemies have +{0}% health.", EnemyHealth = 0.10 },
            new Modifier { Text = "Elites appear more often (+{0}% of enemies on average).", EliteFraction = 0.08 },
        };

        public static int Clamp(int heat) { return heat < 0 ? 0 : heat > MaxHeat ? MaxHeat : heat; }

        public static Modifier Total(int heat)
        {
            heat = Clamp(heat);
            var t = new Modifier { BleedOutMul = 1 };
            for (int i = 0; i < heat; i++)
            {
                var m = Levels[i];
                t.EnemyHealth += m.EnemyHealth; t.EnemyDamage += m.EnemyDamage; t.EliteFraction += m.EliteFraction;
                t.Rerolls += m.Rerolls; t.EnemyCap += m.EnemyCap;
                if (m.BleedOutMul > 0) t.BleedOutMul *= m.BleedOutMul;
            }
            return t;
        }

        /// <summary>The value substituted into a level's text ("" when the text has no number).</summary>
        public static string Arg(Modifier m)
        {
            if (m.EnemyHealth > 0) return RogueArmory.Pct(m.EnemyHealth);
            if (m.EnemyDamage > 0) return RogueArmory.Pct(m.EnemyDamage);
            if (m.EliteFraction > 0) return RogueArmory.Pct(m.EliteFraction);
            if (m.BleedOutMul > 0) return RogueArmory.Pct(1 - m.BleedOutMul);
            return "";
        }
    }

    /// <summary>Squad fairness: enemies scale with the average meta power, never with the strongest player.</summary>
    public static class MetaBalance
    {
        public const double MaxEnemyHealthFromMeta = 0.12, MaxEnemyDamageFromMeta = 0.06;

        public static double AveragePower(IList<MetaLoadout> squad)
        {
            if (squad == null || squad.Count == 0) return 0;
            double sum = 0; int n = 0;
            foreach (var m in squad) { if (m == null) continue; sum += SkillTree.Power(m.skills); n++; }
            return n == 0 ? 0 : sum / n;
        }

        public static double EnemyHealthMul(double averagePower) { return 1 + MaxEnemyHealthFromMeta * Math.Max(0, Math.Min(1, averagePower)); }
        public static double EnemyDamageMul(double averagePower) { return 1 + MaxEnemyDamageFromMeta * Math.Max(0, Math.Min(1, averagePower)); }
    }
}
