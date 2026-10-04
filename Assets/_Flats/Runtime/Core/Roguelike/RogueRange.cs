using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    /// <summary>
    /// Rules of the Roguelike shooting range: a run whose state carries <see cref="RunState.range"/>. It stays in the Combat phase
    /// with an empty plan (no waves, no objective, no shop, no ledger), nobody can be downed or wiped, nothing is ever saved or
    /// rewarded, and every player edits its own build freely through the Range* methods of <see cref="RunMachine"/>. Every one of
    /// those methods refuses (returns false and changes nothing) when the state is not a range, so an ordinary run can never be
    /// edited through them whoever sends the command.
    /// </summary>
    public static class RogueRange
    {
        /// <summary>Most enemies on the field at once (the same bound as RogueDepth.ConcurrentEnemyCap).</summary>
        public const int EnemyCap = 28;
        /// <summary>Stage depths the spawn menu offers (enemy health, damage, stat tier and kits follow the depth).</summary>
        public static readonly int[] Depths = { 1, 10, 20, 40 };
        /// <summary>Enemies per spawn request the menu offers.</summary>
        public static readonly int[] Counts = { 1, 5, 10 };
        /// <summary>Most enemies one request may ask for.</summary>
        public const int MaxSpawnCount = 10;
        /// <summary>Heat used while an elite with a chosen affix is spawned: one affix, drawn from the whole pool.</summary>
        public const int AffixHeat = 7;
        /// <summary>Per-player range switches (RunPlayer.rangeFlags).</summary>
        public const int GodFlag = 1, InfiniteAmmoFlag = 2;
        /// <summary>World switch (RunState.rangeFlags): every enemy is held in place.</summary>
        public const int FrozenFlag = 1;
        /// <summary>Instance ids tried when a spawn asks for a specific melee weapon or affix (both derive from the id).</summary>
        public const int InstanceSearch = 4000;
        public const string FinaleRole = "role.finale";

        public static bool Is(RunState state) { return state != null && state.range; }

        public static bool HasFlag(RunPlayer p, int flag) { return p != null && (p.rangeFlags & flag) != 0; }

        public static bool ValidRole(string roleId) { return roleId == FinaleRole || RogueCatalog.Role(roleId) != null; }

        /// <summary>How many of <paramref name="requested"/> enemies fit under the cap with <paramref name="alive"/> on the field.</summary>
        public static int SpawnRoom(int alive, int requested)
        {
            return Math.Max(0, Math.Min(Math.Min(requested, MaxSpawnCount), EnemyCap - Math.Max(0, alive)));
        }

        /// <summary>The range's stage plan at a depth: the enemy curves of that depth and nothing else (no waves, objective or event).</summary>
        public static EncounterPlan Encounter(int encounterId, int depth, int difficulty, int players, string mapId)
        {
            depth = RogueDepth.Clamp(depth); difficulty = RogueDepth.ClampDifficulty(difficulty);
            players = Math.Max(1, Math.Min(4, players));
            return new EncounterPlan
            {
                encounterId = encounterId, depth = depth, difficulty = difficulty, players = players, mapId = mapId ?? "", routeTag = "",
                concurrentCap = EnemyCap,
                enemyHealthMul = RogueDepth.EnemyHealth(depth, difficulty) * RogueDepth.SquadHealthScale(players),
                enemyDamageMul = RogueDepth.EnemyDamage(depth, difficulty),
                enemyStatTier = RogueDepth.EnemyStatTier(depth, difficulty),
            };
        }

        /// <summary>
        /// An unused instance id at or after <paramref name="from"/> whose derived kit carries <paramref name="meleeId"/> and whose
        /// derived affixes start with <paramref name="affixId"/> (each "" = any). Every copy derives a brawler's melee weapon and an
        /// elite's affix from the run id and the instance id, so choosing the id is how the range chooses them. -1 when no id within
        /// <see cref="InstanceSearch"/> matches (the combination does not exist at this depth or heat).
        /// </summary>
        public static int FindInstance(string runId, int from, ICollection<int> used, string roleId, EncounterPlan plan, int heat, bool elite, string meleeId, string affixId)
        {
            if (plan == null) return -1;
            bool finale = roleId == FinaleRole;
            var def = RogueCatalog.Role(roleId);
            bool wantsMelee = !string.IsNullOrEmpty(meleeId) && def != null && (def.Melee || def.Operative);
            bool wantsAffix = !string.IsNullOrEmpty(affixId);
            from = Math.Max(1, from);
            for (int id = from; id < from + InstanceSearch; id++)
            {
                if (used != null && used.Contains(id)) continue;
                if (wantsMelee)
                {
                    var kit = EnemyKits.For(runId, id, roleId, plan.depth, plan.players, plan.depth + ":" + plan.encounterId);
                    if (kit == null || kit.MeleeId != meleeId) continue;
                }
                if (wantsAffix)
                {
                    var affixes = EliteAffixes.For(runId, id, heat, elite, finale);
                    if (affixes.Length == 0 || affixes[0] != affixId) continue;
                }
                return id;
            }
            return -1;
        }

        /// <summary>The armory weapon that stands for a legacy gun model: its unmodified original when the armory has one.</summary>
        public static string ArmoryIdForModel(int model)
        {
            var original = RogueArmory.OriginalOf(model);
            return original != null ? original.Id : "";
        }
    }

    public sealed partial class RunMachine
    {
        /// <summary>A new shooting-range state: Combat phase from the first frame, empty plan, no shop offers.</summary>
        public static RunState CreateRange(string runId, long seed, int difficulty, string mapId, IList<string> playerKeys, IList<string> playerNames, IList<int> primaryWeapons, IList<int> secondaryWeapons)
        {
            if (playerKeys == null) throw new ArgumentNullException("playerKeys");
            var s = new RunState
            {
                runId = runId, seed = seed, difficulty = RogueDepth.ClampDifficulty(difficulty), mapId = mapId ?? "", contentHash = RogueCatalog.ContentHash(),
                depth = 1, phase = RunPhase.Combat, routeTag = "", range = true, heat = 0, encounterCounter = 1, paidDepth = 1,
            };
            var list = new List<RunPlayer>();
            for (int i = 0; i < playerKeys.Count; i++)
            {
                var p = new RunPlayer { key = playerKeys[i], name = playerNames != null && i < playerNames.Count ? playerNames[i] : playerKeys[i] };
                p.build.primaryWeapon = primaryWeapons != null && i < primaryWeapons.Count ? primaryWeapons[i] : -1;
                p.build.secondaryWeapon = secondaryWeapons != null && i < secondaryWeapons.Count ? secondaryWeapons[i] : -1;
                list.Add(p);
            }
            s.players = list.ToArray();
            s.encounter = RogueRange.Encounter(s.encounterCounter, 1, s.difficulty, Math.Max(1, list.Count), s.mapId);
            return s;
        }

        RunPlayer RangePlayer(string key) { return State.range ? State.Player(key) : null; }

        /// <summary>Range: the depth the next spawned enemies use (a new plan id, so kits are drawn again).</summary>
        public bool RangeSetDepth(int depth)
        {
            if (!State.range) return false;
            State.depth = RogueDepth.Clamp(depth);
            if (State.depth > State.deepestDepth) State.deepestDepth = State.depth;
            State.encounterCounter++;
            // the plan is replaced for its enemy curves only: a running event (and the anchor points every client built it on) stays
            var old = State.encounter;
            State.encounter = RogueRange.Encounter(State.encounterCounter, State.depth, State.difficulty, State.ConnectedPlayers, State.mapId);
            if (old != null)
            {
                State.encounter.eventId = old.eventId ?? ""; State.encounter.emergencyId = old.emergencyId ?? "";
                State.encounter.points = old.points ?? new int[0];
            }
            State.paidDepth = State.depth;
            return true;
        }

        /// <summary>Range: the heat elite affixes are drawn at (0 = no affixes).</summary>
        public bool RangeSetHeat(int heat)
        {
            if (!State.range) return false;
            State.heat = RogueHeat.Clamp(heat);
            return true;
        }

        /// <summary>Range: a stat's tier, clamped to 0..its top tier.</summary>
        public bool RangeSetStat(string key, string statId, int tier)
        {
            var p = RangePlayer(key);
            var def = RogueCatalog.Item(statId);
            if (p == null || def == null || def.Kind != ItemKind.Stat) return false;
            p.build.SetStatTier(statId, tier);
            return true;
        }

        /// <summary>Range: a core's or a mod's tier (0 removes it; no slot limit in the range), or the tactical / ultimate
        /// (tier above 0 equips it, 0 takes it off when it is the equipped one).</summary>
        public bool RangeSetItem(string key, string itemId, int tier)
        {
            var p = RangePlayer(key);
            var def = RogueCatalog.Item(itemId);
            if (p == null || def == null) return false;
            var b = p.build; b.Normalize();
            switch (def.Kind)
            {
                case ItemKind.Core: SetSlotTier(itemId, tier, ref b.cores, ref b.coreTiers, ref b.corePaidMinor); return true;
                case ItemKind.Mod: SetSlotTier(itemId, tier, ref b.mods, ref b.modTiers, ref b.modPaidMinor); return true;
                case ItemKind.Tactical:
                    if (tier > 0) b.tactical = itemId; else if (b.tactical == itemId) b.tactical = "";
                    return true;
                case ItemKind.Ultimate:
                    if (tier > 0) { if (b.ultimate != itemId) { p.ultimateCharge = 0; ultimateRemainder.Remove(p.key); } b.ultimate = itemId; }
                    else if (b.ultimate == itemId) { b.ultimate = ""; p.ultimateCharge = 0; ultimateRemainder.Remove(p.key); }
                    return true;
            }
            return false;
        }

        static void SetSlotTier(string id, int tier, ref string[] ids, ref int[] tiers, ref long[] paid)
        {
            tier = Math.Max(0, Math.Min(RogueCatalog.MaxTier(id), tier));
            int i = Array.IndexOf(ids, id);
            if (tier == 0)
            {
                if (i < 0) return;
                var names = new List<string>(ids); var ranks = new List<int>(tiers); var paidList = new List<long>(paid);
                names.RemoveAt(i); ranks.RemoveAt(i); paidList.RemoveAt(i);
                ids = names.ToArray(); tiers = ranks.ToArray(); paid = paidList.ToArray();
                return;
            }
            if (i < 0)
            {
                i = ids.Length;
                Array.Resize(ref ids, i + 1); Array.Resize(ref tiers, i + 1); Array.Resize(ref paid, i + 1);
                ids[i] = id; paid[i] = 0;
            }
            tiers[i] = tier;
        }

        /// <summary>Range: no cores, mods, tactical, ultimate, stat tiers or learned skills (the weapons stay).</summary>
        public bool RangeClearBuild(string key)
        {
            var p = RangePlayer(key);
            if (p == null) return false;
            var b = p.build;
            b.cores = new string[0]; b.mods = new string[0]; b.coreTiers = new int[0]; b.modTiers = new int[0]; b.corePaidMinor = new long[0]; b.modPaidMinor = new long[0];
            b.tactical = ""; b.ultimate = "";
            foreach (var stat in RogueCatalog.Stats) b.SetStatTier(stat.Id, 0);
            if (b.meta == null) b.meta = new MetaLoadout();
            b.meta.skills = new string[0];
            p.ultimateCharge = 0; ultimateRemainder.Remove(p.key);
            return true;
        }

        /// <summary>Range: learns or forgets one skill-tree node, without points, rows or capstone exclusion.</summary>
        public bool RangeSetSkill(string key, string nodeId, bool learned)
        {
            var p = RangePlayer(key);
            if (p == null || SkillTree.Node(nodeId) == null) return false;
            if (p.build.meta == null) p.build.meta = new MetaLoadout();
            var skills = new List<string>(p.build.meta.skills ?? new string[0]);
            skills.Remove(nodeId);
            if (learned) skills.Add(nodeId);
            p.build.meta.skills = skills.ToArray();
            return true;
        }

        /// <summary>Range: the armory weapon of a slot (0 primary, 1 secondary), or a bare gun model as "model:N". Refused when the
        /// other slot already holds the same gun model (one model cannot be two different weapons).</summary>
        public bool RangeSetWeapon(string key, int slot, string weaponId)
        {
            var p = RangePlayer(key);
            if (p == null || slot < 0 || slot > 1 || string.IsNullOrEmpty(weaponId)) return false;
            int model; string armoryId;
            if (weaponId.StartsWith("model:", StringComparison.Ordinal))
            {
                if (!int.TryParse(weaponId.Substring(6), out model) || model < 0 || model >= WeaponCatalog.Count) return false;
                armoryId = RogueRange.ArmoryIdForModel(model);
            }
            else
            {
                var def = RogueArmory.Weapon(weaponId);
                if (def == null) return false;
                model = def.BaseModel; armoryId = def.Id;
            }
            var b = p.build;
            if (b.meta == null) b.meta = new MetaLoadout();
            int other = slot == 0 ? b.secondaryWeapon : b.primaryWeapon;
            if (other == model) return false;
            if (slot == 0) { b.primaryWeapon = model; b.meta.primary = armoryId; b.meta.primarySight = RangeLegalSight(armoryId, b.meta.primarySight); }
            else { b.secondaryWeapon = model; b.meta.secondary = armoryId; b.meta.secondarySight = RangeLegalSight(armoryId, b.meta.secondarySight); }
            return true;
        }

        /// <summary>Range: the sight of a slot's weapon. Refused when that weapon cannot mount it.</summary>
        public bool RangeSetSight(string key, int slot, string sightId)
        {
            var p = RangePlayer(key);
            if (p == null || slot < 0 || slot > 1 || RogueArmory.Sight(sightId) == null) return false;
            var b = p.build;
            if (b.meta == null) b.meta = new MetaLoadout();
            string weapon = slot == 0 ? b.meta.primary : b.meta.secondary;
            if (!RangeSightFits(weapon, sightId)) return false;
            if (slot == 0) b.meta.primarySight = sightId; else b.meta.secondarySight = sightId;
            return true;
        }

        /// <summary>True when the armory weapon can mount the sight (its largest sight index, and only weapons that aim take optics).</summary>
        public static bool RangeSightFits(string weaponId, string sightId)
        {
            var w = RogueArmory.Weapon(weaponId); var s = RogueArmory.Sight(sightId);
            if (w == null || s == null) return false;
            return s.Index <= RogueArmory.Resolve(w).MaxSightIndex && (WeaponRules.CanAim(w) || s.Index == 0);
        }

        static string RangeLegalSight(string weaponId, string sightId)
        {
            if (RogueArmory.Weapon(weaponId) == null) return "";
            return RangeSightFits(weaponId, sightId) ? sightId : "sight.iron";
        }

        /// <summary>Range: the melee weapon.</summary>
        public bool RangeSetMelee(string key, string meleeId)
        {
            var p = RangePlayer(key);
            if (p == null || RogueArmory.MeleeWeapon(meleeId) == null) return false;
            if (p.build.meta == null) p.build.meta = new MetaLoadout();
            p.build.meta.melee = meleeId;
            return true;
        }

        /// <summary>Range: replaces the out-of-run loadout (skills, armory weapons, sights, melee) with an already sanitized one.</summary>
        public bool RangeSetLoadout(string key, MetaLoadout loadout)
        {
            var p = RangePlayer(key);
            if (p == null || loadout == null) return false;
            p.build.meta = loadout.Clone();
            var pw = RogueArmory.Weapon(loadout.primary); var sw = RogueArmory.Weapon(loadout.secondary);
            if (pw != null) p.build.primaryWeapon = pw.BaseModel;
            if (sw != null) p.build.secondaryWeapon = sw.BaseModel;
            return true;
        }

        /// <summary>Range: the ultimate is fully charged (nothing without an equipped ultimate).</summary>
        public bool RangeFillUltimate(string key)
        {
            var p = RangePlayer(key);
            if (p == null || string.IsNullOrEmpty(p.build.ultimate)) return false;
            p.ultimateCharge = 100; ultimateRemainder.Remove(p.key);
            return true;
        }

        /// <summary>Range: one of the player's own switches (RogueRange.GodFlag, RogueRange.InfiniteAmmoFlag).</summary>
        public bool RangeSetFlag(string key, int flag, bool on)
        {
            var p = RangePlayer(key);
            if (p == null || (flag != RogueRange.GodFlag && flag != RogueRange.InfiniteAmmoFlag)) return false;
            p.rangeFlags = on ? p.rangeFlags | flag : p.rangeFlags & ~flag;
            return true;
        }

        /// <summary>Range: a world switch (RogueRange.FrozenFlag).</summary>
        public bool RangeSetWorldFlag(int flag, bool on)
        {
            if (!State.range || flag != RogueRange.FrozenFlag) return false;
            State.rangeFlags = on ? State.rangeFlags | flag : State.rangeFlags & ~flag;
            return true;
        }

        /// <summary>Range: a kill counts for the killer's tally and charges ultimates like a paid kill of that weight; no money.</summary>
        public bool RangeEnemyKilled(string killerKey, int weight, bool headshot, IList<string> jammedKeys)
        {
            if (!State.range) return false;
            var killer = State.Player(killerKey);
            if (killer != null) { killer.kills++; if (headshot) killer.headshots++; }
            ChargeUltimates(Math.Max(0, weight), killerKey, jammedKeys);
            return true;
        }

        /// <summary>Range: every tally of the session back to zero (kills, headshots, deaths) and every ultimate uncharged.</summary>
        public bool RangeResetTallies()
        {
            if (!State.range) return false;
            foreach (var p in State.players) { p.kills = 0; p.headshots = 0; p.deaths = 0; p.rescues = 0; p.ultimateCharge = 0; p.life = PlayerLife.Alive; }
            ultimateRemainder.Clear();
            return true;
        }
    }
}
