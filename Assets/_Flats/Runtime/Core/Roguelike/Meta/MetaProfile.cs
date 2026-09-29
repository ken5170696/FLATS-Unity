using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    /// <summary>A saved loadout: its own skill allocation and armory picks. Switching presets never costs anything.</summary>
    [Serializable]
    public sealed class MetaPreset
    {
        public string name = "";
        public string[] skills = new string[0];
        public string primary = RogueArmory.DefaultPrimary, secondary = RogueArmory.DefaultSecondary, melee = RogueArmory.DefaultMelee;
        public string primarySight = "sight.reflex", secondarySight = "sight.iron";
    }

    [Serializable]
    public sealed class ChallengeProgress
    {
        public string id = "";
        public long progress;
        public bool claimed;
    }

    /// <summary>
    /// Out-of-run profile, schema 2 (file roguelike-profile-v2.json). The schema-1 record document
    /// (roguelike-meta-v1.json) is migrated once and embedded unchanged in <see cref="records"/>;
    /// the v1 file itself is left in place so an older build never sees a newer schema.
    /// Design rule (replaces schema 1's "records and unlocks only, never permanent power"): the
    /// profile may carry bounded permanent power, but only through skill points from levels;
    /// merits buy options (weapons, melee, sights), never numbers.
    /// </summary>
    [Serializable]
    public sealed class MetaProfile
    {
        public const int CurrentSchema = 2;
        public int schema = CurrentSchema;
        public string migratedFrom = "";
        public RogueMetaDocument records = new RogueMetaDocument();
        public long xp, merits, lifetimeMerits, spentMerits;
        public string[] unlocked = new string[0];
        public MetaPreset[] presets = new MetaPreset[0];
        public int activePreset;
        public string[] rewardedRuns = new string[0];     // idempotency ring: a run id is rewarded once
        public string[] firstTimes = new string[0];
        public string[] weaponKills = new string[0];      // "armoryId|kills"
        public string[] masteryClaimed = new string[0];   // "armoryId|milestone"
        public string challengeDay = "", challengeWeek = "";
        public ChallengeProgress[] challenges = new ChallengeProgress[0];
        public string[] tutorialSeen = new string[0];
        public int heatUnlocked, lastHeat;
        public int respecs;
        public long totalKills, totalHeadshots, totalMeleeKills, totalRescues, totalRuns;
        public double totalSeconds;
        public string pendingRunId = "";                  // a started run whose end was never recorded (crash, force quit)

        public int Level { get { return MetaProgression.LevelFor(xp); } }
        public MetaPreset Active { get { return presets != null && activePreset >= 0 && activePreset < presets.Length ? presets[activePreset] : null; } }
        public bool Owns(string id) { return RogueArmory.IsStarter(id) || Array.IndexOf(unlocked, id) >= 0; }
    }

    /// <summary>Result of a profile mutation. Nothing changes unless Ok.</summary>
    public struct MetaResult
    {
        public bool Ok; public string Reason;
        public static MetaResult Fail(string reason) { return new MetaResult { Ok = false, Reason = reason }; }
        public static readonly MetaResult Success = new MetaResult { Ok = true, Reason = "" };
    }

    public static class MetaProfiles
    {
        public const string FileName = "roguelike-profile-v2.json";
        public const int PresetCount = 3, RewardRing = 64;
        public static readonly int[] MasteryMilestones = { 25, 100, 250, 500 };
        public static readonly long[] MasteryMerits = { 20, 40, 80, 150 };
        public static readonly string[] MasteryNames = { "Bronze", "Silver", "Gold", "Master" };

        public static MetaProfile CreateNew()
        {
            var p = new MetaProfile();
            EnsureShape(p);
            return p;
        }

        /// <summary>
        /// Schema 1 → 2. Keeps every v1 record and grants retroactive experience for runs already
        /// played (150 per evacuation, 60 per wipe, capped at level 10), so veterans do not start at zero.
        /// </summary>
        public static MetaProfile Migrate(RogueMetaDocument v1)
        {
            var p = CreateNew();
            if (v1 == null) return p;
            p.records = v1;
            p.migratedFrom = "roguelike-meta-v1 schema " + v1.schema;
            long retro = 150L * Math.Max(0, v1.runsEvacuated) + 60L * Math.Max(0, v1.runsWiped);
            p.xp = Math.Min(retro, MetaProgression.XpForLevel(10));
            p.totalRuns = Math.Max(0, v1.runsStarted);
            // the merits that the retroactive levels would have paid
            int level = MetaProgression.LevelFor(p.xp);
            for (int l = 2; l <= level; l++) p.merits += MetaProgression.MeritsForLevelUp(l);
            p.lifetimeMerits = p.merits;
            if (!string.IsNullOrEmpty(v1.lastRunId)) p.rewardedRuns = new[] { v1.lastRunId };
            return p;
        }

        /// <summary>Guarantees arrays and presets exist (JsonUtility leaves missing arrays null when a field is absent).</summary>
        public static void EnsureShape(MetaProfile p)
        {
            if (p.records == null) p.records = new RogueMetaDocument();
            if (p.records.seenItems == null) p.records.seenItems = new string[0];
            if (p.records.clearedFinales == null) p.records.clearedFinales = new string[0];
            if (p.unlocked == null) p.unlocked = new string[0];
            if (p.rewardedRuns == null) p.rewardedRuns = new string[0];
            if (p.firstTimes == null) p.firstTimes = new string[0];
            if (p.weaponKills == null) p.weaponKills = new string[0];
            if (p.masteryClaimed == null) p.masteryClaimed = new string[0];
            if (p.challenges == null) p.challenges = new ChallengeProgress[0];
            if (p.tutorialSeen == null) p.tutorialSeen = new string[0];
            if (p.presets == null || p.presets.Length != PresetCount)
            {
                var list = new List<MetaPreset>(p.presets ?? new MetaPreset[0]);
                while (list.Count < PresetCount) list.Add(new MetaPreset());
                while (list.Count > PresetCount) list.RemoveAt(list.Count - 1);
                p.presets = list.ToArray();
            }
            for (int i = 0; i < p.presets.Length; i++)
            {
                if (p.presets[i] == null) p.presets[i] = new MetaPreset();
                if (p.presets[i].skills == null) p.presets[i].skills = new string[0];
                if (string.IsNullOrEmpty(p.presets[i].name)) p.presets[i].name = "Loadout " + (i + 1);
            }
            if (p.activePreset < 0 || p.activePreset >= PresetCount) p.activePreset = 0;
        }

        /// <summary>
        /// Range and consistency checks for a loaded profile (tampering, older builds, bugs). Values are
        /// clamped, unknown ids dropped, skills re-validated against the level's point budget, presets
        /// repaired to owned content. Returns what was fixed; the caller logs it.
        /// </summary>
        public static List<string> Sanitize(MetaProfile p)
        {
            var errors = new List<string>();
            EnsureShape(p);
            if (p.xp < 0 || p.xp > MetaProgression.MaxXp) { errors.Add("experience out of range"); p.xp = Math.Max(0, Math.Min(MetaProgression.MaxXp, p.xp)); }
            if (p.merits < 0 || p.merits > MetaProgression.MaxMerits) { errors.Add("merits out of range"); p.merits = Math.Max(0, Math.Min(MetaProgression.MaxMerits, p.merits)); }
            if (p.lifetimeMerits < p.merits) p.lifetimeMerits = p.merits + Math.Max(0, p.spentMerits);
            p.heatUnlocked = RogueHeat.Clamp(p.heatUnlocked);
            p.lastHeat = Math.Min(RogueHeat.Clamp(p.lastHeat), p.heatUnlocked);
            var owned = new List<string>();
            foreach (var id in p.unlocked) { if (RogueArmory.IsUnlockable(id) && !RogueArmory.IsStarter(id) && !owned.Contains(id)) owned.Add(id); else errors.Add("dropped unlock " + id); }
            p.unlocked = owned.ToArray();
            int budget = MetaProgression.PointsForLevel(p.Level);
            foreach (var preset in p.presets) RepairPreset(p, preset, budget, errors);
            if (p.rewardedRuns.Length > RewardRing) { var l = new List<string>(p.rewardedRuns); l.RemoveRange(0, l.Count - RewardRing); p.rewardedRuns = l.ToArray(); }
            return errors;
        }

        static void RepairPreset(MetaProfile p, MetaPreset preset, int budget, List<string> errors)
        {
            var fixedSkills = SkillTree.Sanitize(preset.skills, budget, errors);
            preset.skills = fixedSkills.ToArray();
            if (RogueArmory.Weapon(preset.primary) == null || !p.Owns(preset.primary)) { errors.Add("primary reset"); preset.primary = RogueArmory.DefaultPrimary; }
            if (RogueArmory.Weapon(preset.secondary) == null || !p.Owns(preset.secondary)) { errors.Add("secondary reset"); preset.secondary = RogueArmory.DefaultSecondary; }
            if (RogueArmory.Weapon(preset.primary).BaseModel == RogueArmory.Weapon(preset.secondary).BaseModel)
            {
                errors.Add("primary and secondary share a model");
                preset.secondary = RogueArmory.Weapon(preset.primary).BaseModel == RogueArmory.Weapon(RogueArmory.DefaultSecondary).BaseModel ? RogueArmory.DefaultPrimary : RogueArmory.DefaultSecondary;
            }
            if (RogueArmory.MeleeWeapon(preset.melee) == null || !p.Owns(preset.melee)) { errors.Add("melee reset"); preset.melee = RogueArmory.DefaultMelee; }
            preset.primarySight = SightOrDefault(p, preset.primary, preset.primarySight, errors);
            preset.secondarySight = SightOrDefault(p, preset.secondary, preset.secondarySight, errors);
        }

        static string SightOrDefault(MetaProfile p, string weaponId, string sightId, List<string> errors)
        {
            if (SightAllowed(p, weaponId, sightId) == null) return sightId;
            if (errors != null) errors.Add("sight reset for " + weaponId);
            return SightAllowed(p, weaponId, "sight.reflex") == null ? "sight.reflex" : "sight.iron";
        }

        /// <summary>Why a sight cannot go on a weapon, or null.</summary>
        public static string SightAllowed(MetaProfile p, string weaponId, string sightId)
        {
            var w = RogueArmory.Weapon(weaponId); var s = RogueArmory.Sight(sightId);
            if (w == null || s == null) return "Unknown item";
            if (!p.Owns(sightId)) return "Not unlocked";
            if (s.Index > RogueArmory.Resolve(w).MaxSightIndex) return "Too strong for this weapon";
            if (!WeaponRules.CanAim(w) && s.Index != 0) return "This weapon cannot aim";
            return null;
        }

        // ------------------------------------------------------------------ transactions (all-or-nothing)
        public static MetaResult Unlock(MetaProfile p, string id)
        {
            if (!RogueArmory.IsUnlockable(id)) return MetaResult.Fail("Unknown item");
            if (p.Owns(id)) return MetaResult.Fail("Already owned");
            int price = RogueArmory.PriceOf(id);
            if (price <= 0) return MetaResult.Fail("Unknown item");
            if (p.merits < price) return MetaResult.Fail("Not enough merits");
            p.merits -= price;
            p.spentMerits += price;
            var list = new List<string>(p.unlocked) { id };
            p.unlocked = list.ToArray();
            return MetaResult.Success;
        }

        public static int AvailablePoints(MetaProfile p, MetaPreset preset) { return MetaProgression.PointsForLevel(p.Level) - SkillTree.Spent(preset.skills); }

        public static MetaResult Learn(MetaProfile p, int presetIndex, string skillId)
        {
            var preset = PresetAt(p, presetIndex); if (preset == null) return MetaResult.Fail("Unknown loadout");
            string reason = SkillTree.CannotLearn(preset.skills, skillId, AvailablePoints(p, preset));
            if (reason != null) return MetaResult.Fail(reason);
            var list = new List<string>(preset.skills) { skillId };
            preset.skills = list.ToArray();
            return MetaResult.Success;
        }

        public static MetaResult Forget(MetaProfile p, int presetIndex, string skillId)
        {
            var preset = PresetAt(p, presetIndex); if (preset == null) return MetaResult.Fail("Unknown loadout");
            string reason = SkillTree.CannotForget(preset.skills, skillId);
            if (reason != null) return MetaResult.Fail(reason);
            var list = new List<string>(preset.skills); list.Remove(skillId);
            preset.skills = list.ToArray();
            return MetaResult.Success;
        }

        /// <summary>Full refund, always free: points are not bought, so a free respec costs the economy nothing and invites experiments.</summary>
        public static MetaResult Respec(MetaProfile p, int presetIndex)
        {
            var preset = PresetAt(p, presetIndex); if (preset == null) return MetaResult.Fail("Unknown loadout");
            if (preset.skills.Length == 0) return MetaResult.Fail("Nothing to reset");
            preset.skills = new string[0];
            p.respecs++;
            return MetaResult.Success;
        }

        public enum Slot { Primary, Secondary, Melee, PrimarySight, SecondarySight }

        public static MetaResult Equip(MetaProfile p, int presetIndex, Slot slot, string id)
        {
            var preset = PresetAt(p, presetIndex); if (preset == null) return MetaResult.Fail("Unknown loadout");
            if (!p.Owns(id)) return MetaResult.Fail("Not unlocked");
            switch (slot)
            {
                case Slot.Primary:
                case Slot.Secondary:
                {
                    var w = RogueArmory.Weapon(id); if (w == null) return MetaResult.Fail("Unknown item");
                    string other = slot == Slot.Primary ? preset.secondary : preset.primary;
                    var o = RogueArmory.Weapon(other);
                    if (o != null && o.BaseModel == w.BaseModel && other != id) return MetaResult.Fail("Both slots would use the same gun model");
                    if (other == id) { if (slot == Slot.Primary) { preset.secondary = preset.primary; } else { preset.primary = preset.secondary; } }   // swap slots
                    if (slot == Slot.Primary) preset.primary = id; else preset.secondary = id;
                    // keep sights legal for the new weapon
                    preset.primarySight = SightOrDefault(p, preset.primary, preset.primarySight, null);
                    preset.secondarySight = SightOrDefault(p, preset.secondary, preset.secondarySight, null);
                    return MetaResult.Success;
                }
                case Slot.Melee:
                    if (RogueArmory.MeleeWeapon(id) == null) return MetaResult.Fail("Unknown item");
                    preset.melee = id; return MetaResult.Success;
                case Slot.PrimarySight:
                case Slot.SecondarySight:
                {
                    string weapon = slot == Slot.PrimarySight ? preset.primary : preset.secondary;
                    string reason = SightAllowed(p, weapon, id); if (reason != null) return MetaResult.Fail(reason);
                    if (slot == Slot.PrimarySight) preset.primarySight = id; else preset.secondarySight = id;
                    return MetaResult.Success;
                }
            }
            return MetaResult.Fail("Unknown slot");
        }

        static MetaPreset PresetAt(MetaProfile p, int index) { EnsureShape(p); return index >= 0 && index < p.presets.Length ? p.presets[index] : null; }

        /// <summary>The loadout a run receives: the active preset, validated again, plus the owned list the shop filters by.</summary>
        public static MetaLoadout ToLoadout(MetaProfile p)
        {
            EnsureShape(p);
            var preset = p.Active;
            var owned = new List<string>();
            foreach (var id in RogueArmory.AllUnlockIds()) if (p.Owns(id)) owned.Add(id);
            return new MetaLoadout
            {
                level = p.Level, skills = (string[])preset.skills.Clone(), primary = preset.primary, secondary = preset.secondary, melee = preset.melee,
                primarySight = preset.primarySight, secondarySight = preset.secondarySight, unlocked = owned.ToArray(),
            };
        }

        /// <summary>
        /// Authority check of a loadout a client declares when joining. The master cannot read another
        /// player's save, so it enforces structure: level range, skill budget, tree rules, known ids,
        /// equipped items listed as owned, sight limits. Anything illegal is replaced by the starter kit.
        /// </summary>
        public static MetaLoadout SanitizeLoadout(MetaLoadout m, List<string> errors)
        {
            var r = m != null ? m.Clone() : new MetaLoadout();
            if (r.skills == null) r.skills = new string[0];
            if (r.unlocked == null) r.unlocked = new string[0];
            r.level = Math.Max(1, Math.Min(MetaProgression.MaxLevel, r.level));
            r.skills = SkillTree.Sanitize(r.skills, MetaProgression.PointsForLevel(r.level), errors).ToArray();
            var owned = new List<string>();
            foreach (var id in r.unlocked) if (RogueArmory.IsUnlockable(id) && !owned.Contains(id)) owned.Add(id);
            foreach (var id in RogueArmory.AllUnlockIds()) if (RogueArmory.IsStarter(id) && !owned.Contains(id)) owned.Add(id);
            r.unlocked = owned.ToArray();
            Func<string, bool> has = id => owned.Contains(id);
            if (RogueArmory.Weapon(r.primary) == null || !has(r.primary)) { if (errors != null) errors.Add("primary"); r.primary = RogueArmory.DefaultPrimary; }
            if (RogueArmory.Weapon(r.secondary) == null || !has(r.secondary)) { if (errors != null) errors.Add("secondary"); r.secondary = RogueArmory.DefaultSecondary; }
            if (RogueArmory.Weapon(r.primary).BaseModel == RogueArmory.Weapon(r.secondary).BaseModel) { if (errors != null) errors.Add("shared model"); r.primary = RogueArmory.DefaultPrimary; r.secondary = RogueArmory.DefaultSecondary; }
            if (RogueArmory.MeleeWeapon(r.melee) == null || !has(r.melee)) { if (errors != null) errors.Add("melee"); r.melee = RogueArmory.DefaultMelee; }
            r.primarySight = LegalSight(r.primary, r.primarySight, has, errors);
            r.secondarySight = LegalSight(r.secondary, r.secondarySight, has, errors);
            return r;
        }

        static string LegalSight(string weapon, string sight, Func<string, bool> has, List<string> errors)
        {
            var w = RogueArmory.Weapon(weapon); var s = RogueArmory.Sight(sight);
            if (w != null && s != null && has(sight) && s.Index <= RogueArmory.Resolve(w).MaxSightIndex && (WeaponRules.CanAim(w) || s.Index == 0)) return sight;
            if (errors != null && !string.IsNullOrEmpty(sight)) errors.Add("sight " + sight);
            return WeaponRules.CanAim(w) ? "sight.reflex" : "sight.iron";
        }

        // ------------------------------------------------------------------ run lifecycle
        /// <summary>A run starts: remember it, so a crash or force quit before the end is detectable and rewarded at most once.</summary>
        public static void NoteRunStarted(MetaProfile p, string runId)
        {
            p.records.runsStarted++;
            p.totalRuns++;
            p.pendingRunId = runId ?? "";
        }

        public static bool AlreadyRewarded(MetaProfile p, string runId) { return !string.IsNullOrEmpty(runId) && Array.IndexOf(p.rewardedRuns, runId) >= 0; }

        /// <summary>
        /// Applies a run's reward exactly once per run id (all-or-nothing on the profile object; the
        /// caller writes the file atomically). Adds level-up merits, overlevel merits, mastery, first
        /// times, challenges and heat unlock to the reward so the result screen lists every source.
        /// Returns false when the run was already rewarded.
        /// </summary>
        public static bool ApplyRunReward(MetaProfile p, RunReward reward, RunFacts facts, DateTime utcNow)
        {
            EnsureShape(p);
            if (reward == null || facts == null || string.IsNullOrEmpty(reward.runId) || AlreadyRewarded(p, reward.runId)) return false;
            var lines = new List<RewardLine>(reward.lines);
            var first = new List<string>(); var mastery = new List<string>(); var done = new List<string>();

            // weapon kills → mastery
            var kills = ParseCounts(p.weaponKills);
            foreach (var entry in facts.WeaponKills ?? new string[0])
            {
                string id; int n; if (!SplitCount(entry, out id, out n) || n <= 0 || (!RogueArmory.IsUnlockable(id))) continue;
                long before; kills.TryGetValue(id, out before);
                long after = Math.Min(1000000, before + Math.Min(n, 5000));
                kills[id] = after;
                for (int i = 0; i < MasteryMilestones.Length; i++)
                {
                    string key = id + "|" + MasteryMilestones[i];
                    if (before < MasteryMilestones[i] && after >= MasteryMilestones[i] && Array.IndexOf(p.masteryClaimed, key) < 0)
                    {
                        mastery.Add(key);
                        lines.Add(new RewardLine { source = "Mastery {0}: {1}", arg = MasteryNames[i] + "|@" + ArmoryName(id), merits = MasteryMerits[i] });
                    }
                }
            }

            // first times
            foreach (var ft in FirstTimes.All)
                if (Array.IndexOf(p.firstTimes, ft.Id) < 0 && ft.Earned(facts)) { first.Add(ft.Id); lines.Add(new RewardLine { source = "First time: {0}", arg = "@" + ft.Text, merits = ft.Merits, xp = ft.Xp }); }

            // challenges
            Challenges.Roll(p, utcNow);
            foreach (var c in p.challenges)
            {
                var def = Challenges.Def(c.id);
                if (def == null || c.claimed) continue;
                c.progress = Math.Min(def.Goal, c.progress + def.Measure(facts));
                if (c.progress >= def.Goal) { c.claimed = true; done.Add(c.id); lines.Add(new RewardLine { source = "Challenge: {0}", arg = "@" + def.Text + "|" + def.Goal, merits = def.Merits }); }
            }

            long xpGain = 0, meritGain = 0;
            foreach (var l in lines) { xpGain += l.xp; meritGain += l.merits; }
            xpGain = Math.Max(0, xpGain); meritGain = Math.Max(0, meritGain);

            int levelBefore = p.Level;
            long xpBefore = p.xp;
            p.xp = Math.Min(MetaProgression.MaxXp, p.xp + xpGain);
            int levelAfter = p.Level;
            long levelMerits = 0;
            for (int l = levelBefore + 1; l <= levelAfter; l++) levelMerits += MetaProgression.MeritsForLevelUp(l);
            if (levelMerits > 0) lines.Add(new RewardLine { source = "Level up to {0}", arg = levelAfter.ToString(), merits = levelMerits });
            // overlevel: every OverlevelXp beyond the level-50 threshold pays merits
            long cap = MetaProgression.XpForLevel(MetaProgression.MaxLevel);
            long overBefore = Math.Max(0, xpBefore - cap) / MetaProgression.OverlevelXp, overAfter = Math.Max(0, p.xp - cap) / MetaProgression.OverlevelXp;
            if (overAfter > overBefore) { long m = (overAfter - overBefore) * MetaProgression.OverlevelMerits; levelMerits += m; lines.Add(new RewardLine { source = "Beyond level {0} x{1}", arg = MetaProgression.MaxLevel + "|" + (overAfter - overBefore), merits = m }); }

            long totalMerits = meritGain + levelMerits;
            p.merits = Math.Min(MetaProgression.MaxMerits, p.merits + totalMerits);
            p.lifetimeMerits += totalMerits;

            p.weaponKills = FormatCounts(kills);
            var mc = new List<string>(p.masteryClaimed); mc.AddRange(mastery); p.masteryClaimed = mc.ToArray();
            var ftl = new List<string>(p.firstTimes); ftl.AddRange(first); p.firstTimes = ftl.ToArray();
            var ring = new List<string>(p.rewardedRuns) { reward.runId }; if (ring.Count > RewardRing) ring.RemoveRange(0, ring.Count - RewardRing); p.rewardedRuns = ring.ToArray();
            if (p.pendingRunId == reward.runId) p.pendingRunId = "";

            // heat: finishing a chapter at the highest unlocked heat opens the next one
            if (facts.Heat >= p.heatUnlocked && facts.FinalesCleared > 0 && p.heatUnlocked < RogueHeat.MaxHeat) { p.heatUnlocked++; lines.Add(new RewardLine { source = "Heat {0} unlocked", arg = p.heatUnlocked.ToString() }); }

            p.totalKills += facts.Kills; p.totalHeadshots += facts.Headshots; p.totalMeleeKills += facts.MeleeKills; p.totalRescues += facts.Rescues;
            p.totalSeconds += Math.Max(0, Math.Min(facts.Seconds, 86400));

            reward.lines = lines.ToArray();
            reward.xp = xpGain; reward.merits = totalMerits; reward.levelUpMerits = levelMerits;
            reward.levelBefore = levelBefore; reward.levelAfter = levelAfter;
            reward.pointsGained = MetaProgression.PointsForLevel(levelAfter) - MetaProgression.PointsForLevel(levelBefore);
            reward.firstTimes = first.ToArray(); reward.mastery = mastery.ToArray(); reward.challenges = done.ToArray();
            return true;
        }

        public static string ArmoryName(string id)
        {
            var w = RogueArmory.Weapon(id); if (w != null) return w.Name;
            var m = RogueArmory.MeleeWeapon(id); if (m != null) return m.Name;
            var s = RogueArmory.Sight(id); if (s != null) return s.Name;
            return id;
        }

        public static long KillsWith(MetaProfile p, string id) { long n; return ParseCounts(p.weaponKills).TryGetValue(id, out n) ? n : 0; }

        /// <summary>Mastery tier reached with a weapon: -1 none, 0 Bronze .. 3 Master.</summary>
        public static int MasteryTier(MetaProfile p, string id)
        {
            long k = KillsWith(p, id); int tier = -1;
            for (int i = 0; i < MasteryMilestones.Length; i++) if (k >= MasteryMilestones[i]) tier = i;
            return tier;
        }

        static Dictionary<string, long> ParseCounts(string[] entries)
        {
            var d = new Dictionary<string, long>();
            if (entries != null) foreach (var e in entries) { string id; int n; if (SplitCount(e, out id, out n) && n >= 0) d[id] = n; }
            return d;
        }

        static string[] FormatCounts(Dictionary<string, long> d)
        {
            var keys = new List<string>(d.Keys); keys.Sort(StringComparer.Ordinal);
            var r = new string[keys.Count]; for (int i = 0; i < keys.Count; i++) r[i] = keys[i] + "|" + d[keys[i]];
            return r;
        }

        public static bool SplitCount(string entry, out string id, out int n)
        {
            id = null; n = 0;
            if (string.IsNullOrEmpty(entry)) return false;
            int bar = entry.LastIndexOf('|'); if (bar <= 0) return false;
            id = entry.Substring(0, bar);
            return int.TryParse(entry.Substring(bar + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out n);
        }

        public static bool SeenTutorial(MetaProfile p, string key) { return Array.IndexOf(p.tutorialSeen, key) >= 0; }
        public static void MarkTutorial(MetaProfile p, string key) { if (!SeenTutorial(p, key)) { var l = new List<string>(p.tutorialSeen) { key }; p.tutorialSeen = l.ToArray(); } }
    }
}
