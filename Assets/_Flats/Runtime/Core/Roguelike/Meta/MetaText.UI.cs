using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Flats.Core.Roguelike
{
    public static partial class MetaText
    {
        public static TextLine Value(string template, params object[] values)
        {
            var args = new string[values.Length];
            for (int i = 0; i < args.Length; i++) args[i] = Convert.ToString(values[i], CultureInfo.InvariantCulture);
            return new TextLine(template, args);
        }
        public static TextLine Wallet(long merits) { return Value("{0} Merits", merits); }
        public static TextLine Level(int level) { return Value("Level {0}", level); }
        public static TextLine Points(int points) { return Value("{0} skill points", points); }
        public static TextLine Count(long value, long maximum) { return Value("{0} / {1}", value, maximum); }
        public static TextLine Price(string id) { return Wallet(RogueArmory.PriceOf(id)); }
        public static TextLine Purchase(MetaProfile p, string id) { return Value("Cost: {0} Merits. Remaining: {1} Merits.", RogueArmory.PriceOf(id), p.merits - RogueArmory.PriceOf(id)); }
        public static TextLine HeatReward(int heat) { return Value("Rewards +{0}%", RogueArmory.Pct(MetaProgression.HeatRewardStep * heat)); }
        public static TextLine BranchNeeded(MetaPreset p, SkillDef n) { return Value("Spend {0} more points in this branch", Math.Max(0, SkillTree.RowPointsRequired[n.Row] - SkillTree.SpentIn(p.skills, n.Branch))); }
        public static TextLine Refund(MetaPreset p) { return Value("Refund {0} skill points. These effects will be removed:", SkillTree.Spent(p.skills)); }
        public static TextLine Challenge(ChallengeDef d) { return Value(d.Text, d.Goal); }
        public static TextLine TimeLeft(bool weekly, DateTime utc)
        {
            DateTime end = utc.Date.AddDays(weekly ? 7 - ((int)utc.DayOfWeek + 6) % 7 : 1);
            var t = end - utc;
            return Value("Resets in {0}h {1}m (UTC)", (int)t.TotalHours, t.Minutes);
        }
        public static TextLine PlayTime(double seconds) { return Value("{0}h {1}m played", (int)(seconds / 3600), (int)(seconds / 60) % 60); }
        public static TextLine NextXp(long xp)
        {
            long into, size; MetaProgression.Progress(xp, out into, out size);
            return Value(MetaProgression.LevelFor(xp) == MetaProgression.MaxLevel ? "{0} XP to the next overlevel reward" : "{0} XP to the next level", size - into);
        }
        public static TextLine Contribution(Contribution c) { return Value("Triggered {0} times · extra damage {1} · absorbed {2}", c.triggers, N(c.extraDamage), N(c.absorbed)); }
        public static TextLine Fairness(int own, int highest)
        {
            return own + MetaProgression.LowLevelGap <= highest
                ? Value("Squad catch-up: +{0}% experience. Enemies scale with average squad power.", P(MetaProgression.LowLevelBonus))
                : new TextLine("Enemies scale with average squad power, so friends at different levels can play together.");
        }
        // Comparing the actual derived block includes conditional parameters as well as unconditional multipliers.
        // Mechanic switches remain explicit; no preview pretends that a conditional bonus is always active.
        public static List<TextLine> Compare(MetaLoadout before, MetaLoadout after)
        {
            var a = BuildStats.Compute(new PlayerBuild { meta = before });
            var b = BuildStats.Compute(new PlayerBuild { meta = after });
            var result = new List<TextLine>();
            foreach (var f in typeof(BuildStats).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (f.FieldType != typeof(double) && f.FieldType != typeof(int) && f.FieldType != typeof(bool)) continue;
                object x = f.GetValue(a), y = f.GetValue(b);
                if (Equals(x, y)) continue;
                string label = System.Text.RegularExpressions.Regex.Replace(f.Name, "([a-z])([A-Z])", "$1 $2");
                if(f.Name==nameof(BuildStats.AdsTimeMul)) label="Aim settling time multiplier";
                if (f.FieldType == typeof(bool)) result.Add(Value("{0}: {1} → {2}", label, (bool)x ? "Enabled" : "Disabled", (bool)y ? "Enabled" : "Disabled"));
                else result.Add(Value("{0}: {1} → {2}", label, Convert.ToDouble(x).ToString("0.00", CultureInfo.InvariantCulture), Convert.ToDouble(y).ToString("0.00", CultureInfo.InvariantCulture)));
            }
            return result;
        }
    }
}
