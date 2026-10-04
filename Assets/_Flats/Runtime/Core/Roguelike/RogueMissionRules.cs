using System;

namespace Flats.Core.Roguelike
{
    /// <summary>
    /// Tuning of mission objects and mission pressure (objectives, events, emergencies, finales), in one place.
    ///  - Targets the squad must destroy (signal device, supply drone, convoy carrier, vault core, commander) are sized from the
    ///    enemy health curve of the stage (RogueDepth.EnemyHealth), a chapter step on top of it and the squad size, never from a
    ///    fixed number: player firepower has no ceiling, so a fixed health would be a one-shot late in a run.
    ///  - Devices the squad must protect (repair devices) scale with the squad only, and the number of enemies that can wear one
    ///    down at once is capped, so a larger attack is more dangerous for the players without making the device a certain loss.
    ///  - Interactions (hold, carry, stand in a zone) are answered with an ambush squad when they start and a smaller squad every
    ///    <see cref="SustainSeconds"/> while they go on. Squads are extras: they are paid from the encounter's bonus pool
    ///    (RogueEconomy.ReserveExtra, never the stage budget) and the adapter never spawns past the concurrent enemy cap.
    /// Pure rules: the adapter (RogueMissionPressure and the runners) owns placement and timing.
    /// </summary>
    public static class RogueMissionRules
    {
        static int Players(int players) { return players < 1 ? 1 : (players > 4 ? 4 : players); }

        // ---------------------------------------------------------------- targets to destroy
        /// <summary>Health at depth 1, Normal, solo. Before this rule: signal device 1500, supply drone 600, carrier 3000.
        /// The carrier was 24000 at first: with a starting weapon the chapter 1 finale took several times as long as it used to.</summary>
        public const double SignalDeviceBaseHealth = 12000, SupplyDroneBaseHealth = 5000, ConvoyCarrierBaseHealth = 15000;
        /// <summary>Each chapter after the first adds this share of the base on top of the enemy health curve; bounded.</summary>
        public const double ChapterHealthStep = 0.5, MaxChapterHealthMul = 25;
        /// <summary>Each extra player adds this share (four players: x2.5): their fire adds up on one target.</summary>
        public const double SquadTargetHealthStep = 0.5;

        public static double ChapterHealthMul(int depth)
        { return Math.Min(MaxChapterHealthMul, 1.0 + ChapterHealthStep * (RogueDepth.ChapterOf(depth) - 1)); }

        /// <summary>Multiplier of a destructible target's base health: the stage's enemy health curve relative to depth 1 Normal,
        /// the chapter step and the squad size. 1 at depth 1, Normal, solo; finite at any depth.</summary>
        public static double TargetHealthScale(int depth, int difficulty, int players)
        {
            double reference = RogueDepth.EnemyHealth(1, 1);
            double curve = reference > 0 ? RogueDepth.EnemyHealth(depth, difficulty) / reference : 1.0;
            if (double.IsNaN(curve) || double.IsInfinity(curve) || curve < 1.0) curve = 1.0;
            return curve * ChapterHealthMul(depth) * (1.0 + SquadTargetHealthStep * (Players(players) - 1));
        }

        public static double TargetHealth(double baseHealth, int depth, int difficulty, int players)
        { return Math.Round(RogueStateBag.Positive(baseHealth) * TargetHealthScale(depth, difficulty, players)); }

        public static double SignalDeviceHealth(int depth, int difficulty, int players) { return TargetHealth(SignalDeviceBaseHealth, depth, difficulty, players); }
        public static double SupplyDroneHealth(int depth, int difficulty, int players) { return TargetHealth(SupplyDroneBaseHealth, depth, difficulty, players); }
        public static double ConvoyCarrierHealth(int depth, int difficulty, int players) { return TargetHealth(ConvoyCarrierBaseHealth, depth, difficulty, players); }

        /// <summary>Health multiplier of the finale target enemy (role.finale) over a rifleman of the stage. The stage's enemy health
        /// multiplier (depth curve, squad size) applies on top. Before this rule every finale target was x8.</summary>
        public const double VaultCoreHealthMul = 24, CommanderHealthMul = 20, DefaultFinaleHealthMul = 8;

        public static double FinaleTargetHealthMul(string finaleId, int depth)
        {
            switch (finaleId)
            {
                case "fin.vault": return VaultCoreHealthMul * ChapterHealthMul(depth);
                case "fin.commander": return CommanderHealthMul * ChapterHealthMul(depth);
                // the convoy's target is the carrier; its finale enemy leads the escort and must stay killable, or the carrier never stops
                default: return DefaultFinaleHealthMul;
            }
        }

        // ---------------------------------------------------------------- devices to protect
        /// <summary>Protect the Repair (was 1000) and the optional Repair Device (was 800), solo.</summary>
        public const double ProtectDeviceBaseHealth = 3000, RepairDeviceBaseHealth = 2000;
        public const double SquadDeviceHealthStep = 0.25;
        /// <summary>Health a device loses per second for each enemy near it, and how many enemies count at once.</summary>
        public const double ProtectDrainPerEnemy = 25, RepairDrainPerEnemy = 20;
        public const int MaxDrainingEnemies = 5;

        public static double DeviceHealth(double baseHealth, int players)
        { return Math.Round(RogueStateBag.Positive(baseHealth) * (1.0 + SquadDeviceHealthStep * (Players(players) - 1))); }

        public static double DeviceDrain(double perEnemy, int enemiesNear)
        { return RogueStateBag.NonNegative(perEnemy) * Math.Max(0, Math.Min(MaxDrainingEnemies, enemiesNear)); }

        // ---------------------------------------------------------------- pressure squads
        /// <summary>Seconds between squads while an interaction goes on (was 9: owner feedback 2026-10-04 asked for a little more
        /// pressure), and how long a contested place may stay thin.</summary>
        public const double SustainSeconds = 7, ReinforceDelaySeconds = 2;

        static int ChapterBonus(int depth) { return Math.Min(2, RogueDepth.ChapterOf(depth) - 1); }
        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

        /// <summary>Enemies (alive plus planned) a contested place keeps on the field: 5 solo in chapter 1, up to 10. Was 4 to 9.</summary>
        public static int PressureFloor(int players, int depth) { return Clamp(4 + Players(players) + ChapterBonus(depth), 5, 10); }
        /// <summary>The squad that answers the start of an interaction: 4 solo in chapter 1, up to 9. Was 3 to 8.</summary>
        public static int AmbushSize(int players, int depth) { return Clamp(3 + Players(players) + ChapterBonus(depth), 4, 9); }
        /// <summary>The squad that arrives every <see cref="SustainSeconds"/> while the interaction goes on: 2 to 5.</summary>
        public static int SustainSize(int players) { return 1 + Players(players); }
        /// <summary>One wave of the reinforcement signal (was a single rifleman): 2 to 5.</summary>
        public static int SignalWaveSize(int players) { return 1 + Players(players); }
        /// <summary>The wave an opened alarm cache calls (was 3): 3 to 6.</summary>
        public static int AlarmWaveSize(int players) { return 2 + Players(players); }
        /// <summary>The commander's new guard after a closed window (was 4): 4 to 8.</summary>
        public static int GuardWaveSize(int players, int depth) { return Clamp(3 + Players(players) + ChapterBonus(depth), 4, 8); }
        /// <summary>Bodyguards that arrive with the hunted elite (was none): 1 to 3.</summary>
        public static int HuntEscortSize(int players) { return 1 + Players(players) / 2; }

        /// <summary>Elites in a squad of <paramref name="count"/>: none while the stage has no elite share (RogueDepth.EliteFraction:
        /// chapter 1 on Normal) or in a squad under three, then one and a half times that share, at least one and at most half the squad.</summary>
        public static int EliteCount(int count, int depth, int difficulty)
        {
            double share = RogueDepth.EliteFraction(depth, difficulty);
            if (count < 3 || !(share > 0)) return 0;
            return Math.Max(1, Math.Min(count / 2, (int)Math.Round(count * share * 1.5)));
        }

        static readonly string[] SquadPattern = { "role.rusher", "role.rifleman", "role.flanker", "role.rifleman", "role.rusher", "role.marksman" };

        /// <summary>The role that leads an ambush squad (its first member): somebody who closes in on the player who is busy with the
        /// interaction. A Bruiser or a Flanker by turns once the depth has unlocked them (RogueCatalog MinDepth: the Flanker from
        /// stage 2, the Bruiser from stage 3); on the very first stage, where neither exists yet, a Rusher.</summary>
        public static string AmbushLeadRole(int depth, int ambushIndex)
        {
            int d = RogueDepth.Clamp(depth);
            var brawler = RogueCatalog.Role("role.brawler");
            var flanker = RogueCatalog.Role("role.flanker");
            bool hasBrawler = brawler != null && brawler.MinDepth <= d, hasFlanker = flanker != null && flanker.MinDepth <= d;
            if (hasBrawler && hasFlanker) return (ambushIndex < 0 ? 0 : ambushIndex) % 2 == 0 ? "role.brawler" : "role.flanker";
            if (hasBrawler) return "role.brawler";
            if (hasFlanker) return "role.flanker";
            return "role.rusher";
        }

        /// <summary>An ambush closes in from several sides: each member after the first arrives at least this many degrees (seen from
        /// the place under attack) from the members already placed, when one of AmbushSpreadDraws extra placements allows it.</summary>
        public const double AmbushSpreadDegrees = 60;
        public const int AmbushSpreadDraws = 3;

        /// <summary>Role of the ambush squad member at <paramref name="index"/>: the lead first, then the usual pattern.</summary>
        public static string AmbushRole(int index, int depth, int ambushIndex)
        { return index <= 0 ? AmbushLeadRole(depth, ambushIndex) : PressureRole(index - 1, depth); }

        /// <summary>Role of the squad member at <paramref name="index"/>: a fixed close/steady/flank mix of the original roles the
        /// depth has unlocked (RogueCatalog MinDepth); a locked or unknown role falls back to a rifleman.</summary>
        public static string PressureRole(int index, int depth)
        {
            string id = SquadPattern[(index < 0 ? 0 : index) % SquadPattern.Length];
            var def = RogueCatalog.Role(id);
            return def != null && def.MinDepth <= RogueDepth.Clamp(depth) ? id : "role.rifleman";
        }
    }
}
