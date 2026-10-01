using System;
using System.Collections.Generic;
using System.Globalization;

namespace Flats.Core.Roguelike
{
    public sealed class FirstTimeDef
    {
        public string Id, Text; public long Merits, Xp;
        public Func<RunFacts, bool> Earned;
    }

    /// <summary>One-off achievements. Claimed ids live in MetaProfile.firstTimes, so each pays once.</summary>
    public static class FirstTimes
    {
        /// <summary>"Reach stage 2-5 (10 stages deep)": the chapter-stage form every screen uses, with the depth the rule checks.</summary>
        static string DepthText(int depth) { return "Reach stage " + RogueDepth.ChapterOf(depth) + "-" + RogueDepth.StageInChapter(depth) + " (" + depth + " stages deep)"; }

        public static readonly FirstTimeDef[] All =
        {
            new FirstTimeDef { Id = "ft.evac", Text = "Evacuate a squad", Merits = 60, Xp = 100, Earned = f => f.End == RunEnd.Evacuated },
            new FirstTimeDef { Id = "ft.chapter1", Text = "Clear a chapter finale", Merits = 100, Xp = 150, Earned = f => f.FinalesCleared >= 1 },
            new FirstTimeDef { Id = "ft.chapter2", Text = "Clear two chapter finales in one run", Merits = 150, Xp = 250, Earned = f => f.FinalesCleared >= 2 },
            new FirstTimeDef { Id = "ft.depth10", Text = DepthText(10), Merits = 120, Xp = 200, Earned = f => f.DeepestDepth >= 10 },
            new FirstTimeDef { Id = "ft.depth20", Text = DepthText(20), Merits = 200, Xp = 300, Earned = f => f.DeepestDepth >= 20 },
            new FirstTimeDef { Id = "ft.hard", Text = "Clear a finale on Hard", Merits = 120, Xp = 150, Earned = f => f.FinalesCleared >= 1 && f.Difficulty >= 2 },
            new FirstTimeDef { Id = "ft.chaos", Text = "Clear a finale on Chaos", Merits = 180, Xp = 200, Earned = f => f.FinalesCleared >= 1 && f.Difficulty >= 3 },
            new FirstTimeDef { Id = "ft.heat3", Text = "Clear a finale at Heat 3", Merits = 150, Xp = 200, Earned = f => f.FinalesCleared >= 1 && f.Heat >= 3 },
            new FirstTimeDef { Id = "ft.heat6", Text = "Clear a finale at Heat 6", Merits = 220, Xp = 300, Earned = f => f.FinalesCleared >= 1 && f.Heat >= 6 },
            new FirstTimeDef { Id = "ft.heat10", Text = "Clear a finale at Heat 10", Merits = 400, Xp = 500, Earned = f => f.FinalesCleared >= 1 && f.Heat >= 10 },
            new FirstTimeDef { Id = "ft.melee25", Text = "25 melee kills in one run", Merits = 80, Xp = 100, Earned = f => f.MeleeKills >= 25 },
            new FirstTimeDef { Id = "ft.rescue5", Text = "Revive teammates 5 times in one run", Merits = 80, Xp = 100, Earned = f => f.Rescues >= 5 },
            new FirstTimeDef { Id = "ft.squad4", Text = "Evacuate with a squad of four", Merits = 100, Xp = 150, Earned = f => f.End == RunEnd.Evacuated && f.Players >= 4 },
        };
    }

    public enum ChallengeMetric { Kills, Headshots, StagesCleared, Evacuations, MeleeKills, Rescues, Objectives, Finales, KillsSMG, KillsRifle, KillsShotgun, KillsSniper, KillsHandgun, KillsHeavy }

    public sealed class ChallengeDef
    {
        public string Id, Text; public ChallengeMetric Metric; public long Goal, Merits; public bool Weekly;
        public long Measure(RunFacts f)
        {
            switch (Metric)
            {
                case ChallengeMetric.Kills: return f.Kills;
                case ChallengeMetric.Headshots: return f.Headshots;
                case ChallengeMetric.StagesCleared: return f.StagesCleared;
                case ChallengeMetric.Evacuations: return f.End == RunEnd.Evacuated ? 1 : 0;
                case ChallengeMetric.MeleeKills: return f.MeleeKills;
                case ChallengeMetric.Rescues: return f.Rescues;
                case ChallengeMetric.Objectives: return f.Objectives;
                case ChallengeMetric.Finales: return f.FinalesCleared;
                case ChallengeMetric.KillsSMG: return ClassKills(f, WeaponClass.SMG);
                case ChallengeMetric.KillsRifle: return ClassKills(f, WeaponClass.AssaultRifle);
                case ChallengeMetric.KillsShotgun: return ClassKills(f, WeaponClass.Shotgun);
                case ChallengeMetric.KillsSniper: return ClassKills(f, WeaponClass.Sniper);
                case ChallengeMetric.KillsHandgun: return ClassKills(f, WeaponClass.Handgun);
                case ChallengeMetric.KillsHeavy: return ClassKills(f, WeaponClass.LMG) + ClassKills(f, WeaponClass.Launcher);
            }
            return 0;
        }

        static long ClassKills(RunFacts f, WeaponClass c)
        {
            long n = 0;
            foreach (var e in f.WeaponKills ?? new string[0]) { string id; int k; if (MetaProfiles.SplitCount(e, out id, out k)) { var w = RogueArmory.Weapon(id); if (w != null && w.Class == c) n += k; } }
            return n;
        }
    }

    /// <summary>
    /// Daily (3) and weekly (2) challenges drawn from fixed pools with a seed from the UTC date, so
    /// every player gets the same set without a server. Progress resets when the period changes.
    /// </summary>
    public static class Challenges
    {
        public const int DailyCount = 3, WeeklyCount = 2;

        public static readonly ChallengeDef[] Pool =
        {
            new ChallengeDef { Id = "cd.kills", Text = "Kill {0} enemies", Metric = ChallengeMetric.Kills, Goal = 120, Merits = 40 },
            new ChallengeDef { Id = "cd.heads", Text = "Land {0} headshot kills", Metric = ChallengeMetric.Headshots, Goal = 40, Merits = 40 },
            new ChallengeDef { Id = "cd.stages", Text = "Clear {0} stages", Metric = ChallengeMetric.StagesCleared, Goal = 8, Merits = 40 },
            new ChallengeDef { Id = "cd.melee", Text = "Get {0} melee kills", Metric = ChallengeMetric.MeleeKills, Goal = 15, Merits = 50 },
            new ChallengeDef { Id = "cd.smg", Text = "Kill {0} enemies with SMGs", Metric = ChallengeMetric.KillsSMG, Goal = 60, Merits = 45 },
            new ChallengeDef { Id = "cd.rifle", Text = "Kill {0} enemies with assault rifles", Metric = ChallengeMetric.KillsRifle, Goal = 60, Merits = 45 },
            new ChallengeDef { Id = "cd.shotgun", Text = "Kill {0} enemies with shotguns", Metric = ChallengeMetric.KillsShotgun, Goal = 40, Merits = 45 },
            new ChallengeDef { Id = "cd.sniper", Text = "Kill {0} enemies with sniper rifles", Metric = ChallengeMetric.KillsSniper, Goal = 30, Merits = 50 },
            new ChallengeDef { Id = "cd.handgun", Text = "Kill {0} enemies with handguns", Metric = ChallengeMetric.KillsHandgun, Goal = 30, Merits = 50 },
            new ChallengeDef { Id = "cd.heavy", Text = "Kill {0} enemies with heavy weapons", Metric = ChallengeMetric.KillsHeavy, Goal = 50, Merits = 45 },
            new ChallengeDef { Id = "cd.rescue", Text = "Revive teammates {0} times", Metric = ChallengeMetric.Rescues, Goal = 3, Merits = 45 },
            new ChallengeDef { Id = "cd.objectives", Text = "Complete {0} objectives", Metric = ChallengeMetric.Objectives, Goal = 6, Merits = 40 },
            new ChallengeDef { Id = "cw.evac", Text = "Evacuate {0} times", Metric = ChallengeMetric.Evacuations, Goal = 3, Merits = 150, Weekly = true },
            new ChallengeDef { Id = "cw.finales", Text = "Clear {0} chapter finales", Metric = ChallengeMetric.Finales, Goal = 3, Merits = 160, Weekly = true },
            new ChallengeDef { Id = "cw.kills", Text = "Kill {0} enemies", Metric = ChallengeMetric.Kills, Goal = 800, Merits = 150, Weekly = true },
            new ChallengeDef { Id = "cw.melee", Text = "Get {0} melee kills", Metric = ChallengeMetric.MeleeKills, Goal = 80, Merits = 150, Weekly = true },
            new ChallengeDef { Id = "cw.stages", Text = "Clear {0} stages", Metric = ChallengeMetric.StagesCleared, Goal = 40, Merits = 150, Weekly = true },
        };

        public static ChallengeDef Def(string id) { foreach (var c in Pool) if (c.Id == id) return c; return null; }

        public static string DayKey(DateTime utc) { return utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        public static string WeekKey(DateTime utc)
        {
            var monday = utc.Date.AddDays(-(((int)utc.DayOfWeek + 6) % 7));
            return "W" + monday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>Replaces the daily/weekly sets when their period changed. Progress of the current period is kept.</summary>
        public static void Roll(MetaProfile p, DateTime utcNow)
        {
            string day = DayKey(utcNow), week = WeekKey(utcNow);
            var keep = new List<ChallengeProgress>();
            bool newDay = p.challengeDay != day, newWeek = p.challengeWeek != week;
            foreach (var c in p.challenges ?? new ChallengeProgress[0])
            {
                var def = Def(c.id); if (def == null) continue;
                if (def.Weekly ? !newWeek : !newDay) keep.Add(c);
            }
            if (newDay) foreach (var id in Pick(day, false, DailyCount)) keep.Add(new ChallengeProgress { id = id });
            if (newWeek) foreach (var id in Pick(week, true, WeeklyCount)) keep.Add(new ChallengeProgress { id = id });
            p.challenges = keep.ToArray();
            p.challengeDay = day; p.challengeWeek = week;
        }

        static List<string> Pick(string period, bool weekly, int count)
        {
            var pool = new List<string>(); foreach (var c in Pool) if (c.Weekly == weekly) pool.Add(c.Id);
            ulong h = 14695981039346656037UL; foreach (char ch in period) { h ^= ch; h = unchecked(h * 1099511628211UL); }
            var rng = new RogueRng(h);
            var chosen = new List<string>();
            while (chosen.Count < count && pool.Count > 0) { int i = rng.Range(0, pool.Count); chosen.Add(pool[i]); pool.RemoveAt(i); }
            return chosen;
        }
    }

    /// <summary>Per-run tally of what each skill, weapon trait or drawback actually did. Filled by the owner's copy from real hits.</summary>
    [Serializable]
    public sealed class Contribution
    {
        public string id = "";
        public int triggers;
        public double extraDamage, absorbed;
    }

    public sealed class ContributionLedger
    {
        readonly Dictionary<string, Contribution> entries = new Dictionary<string, Contribution>();

        public void Trigger(string id, int count = 1) { if (!string.IsNullOrEmpty(id)) Get(id).triggers += count; }

        /// <summary>Credits the part of <paramref name="finalDamage"/> that the multiplier added (final - final/mul).</summary>
        public void Damage(string id, double finalDamage, double mul)
        {
            if (string.IsNullOrEmpty(id) || mul <= 1 || finalDamage <= 0) return;
            var c = Get(id); c.triggers++; c.extraDamage += finalDamage - finalDamage / mul;
        }

        public void Absorbed(string id, double amount) { if (!string.IsNullOrEmpty(id) && amount > 0) { var c = Get(id); c.triggers++; c.absorbed += amount; } }

        Contribution Get(string id) { Contribution c; if (!entries.TryGetValue(id, out c)) { c = new Contribution { id = id }; entries[id] = c; } return c; }

        public void Clear() { entries.Clear(); }

        /// <summary>The most impactful entries (damage and absorption first, then trigger count).</summary>
        public Contribution[] Top(int n)
        {
            var list = new List<Contribution>(entries.Values);
            list.Sort((a, b) => { int c = (b.extraDamage + b.absorbed).CompareTo(a.extraDamage + a.absorbed); return c != 0 ? c : b.triggers.CompareTo(a.triggers); });
            if (list.Count > n) list.RemoveRange(n, list.Count - n);
            return list.ToArray();
        }

        public Contribution Of(string id) { Contribution c; return entries.TryGetValue(id, out c) ? c : null; }
    }
}
