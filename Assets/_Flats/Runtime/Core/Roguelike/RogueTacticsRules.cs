using System;

namespace Flats.Core.Roguelike
{
    /// <summary>
    /// Tuning of the enemy squad brain (RogueEnemyTactics and RogueSquad in the adapter), in one place. Pure rules: every number the
    /// adapter uses to decide where an enemy stands, who may land shots, what the squad knows about a player and when it pushes
    /// comes from here, and grows with the chapter so the first stages stay beatable while later squads coordinate visibly better.
    /// Distances are world units ("m" in this project: a character is about 6 m tall).
    /// </summary>
    public static class RogueTacticsRules
    {
        static int Players(int players) { return players < 1 ? 1 : (players > 4 ? 4 : players); }
        static int Chapter(int depth) { return RogueDepth.ChapterOf(depth); }
        static double Clamp(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }

        // ---------------------------------------------------------------- decisions and their cost
        /// <summary>Seconds between an enemy's decisions (each enemy adds its own offset inside ThinkJitter, so 28 enemies never decide
        /// on the same frame), position searches allowed per frame for the whole squad, and candidate spots per search (two sight
        /// lines each).</summary>
        public const double ThinkSeconds = 0.3, ThinkJitter = 0.2;
        public const int PositionQueriesPerFrame = 2, PositionCandidates = 24, RaysPerCandidate = 2;

        // ---------------------------------------------------------------- what the squad knows
        /// <summary>An enemy that loses sight still knows where its target is for this long (it saw which way it went).</summary>
        public const double IntuitionSeconds = 2.0;
        /// <summary>A sighting reaches the rest of the squad after this delay (a call-out), to enemies within ShareRadius.</summary>
        public static double ShareDelaySeconds(int depth) { return Clamp(1.4 - 0.2 * (Chapter(depth) - 1), 0.5, 1.4); }
        public const double ShareRadius = 120;
        /// <summary>A player's gunfire is heard this far; the squad learns the place to within HearNoise.</summary>
        public const double HearGunfireRadius = 140, HearNoise = 6;
        /// <summary>An enemy killed in view of nobody still tells the squad roughly where the fight is.</summary>
        public const double DeathAlertNoise = 10;
        /// <summary>Survival pressure: with no sighting at all the squad is told roughly where a player is every PingSeconds, to
        /// within PingNoise, so nobody wanders the map. Later chapters hear sooner and closer.</summary>
        public static double PingSeconds(int depth) { return Clamp(6.0 - 0.75 * (Chapter(depth) - 1), 3.0, 6.0); }
        public static double PingNoise(int depth) { return Clamp(14.0 - 2.0 * (Chapter(depth) - 1), 6.0, 14.0); }
        /// <summary>Search spots around the last known place when nobody is found there: ring radii and spots per ring.</summary>
        public const double SearchRingNear = 12, SearchRingFar = 24;
        public const int SearchSpots = 8;

        // ---------------------------------------------------------------- reaction and who lands shots
        /// <summary>Seconds from seeing a target to the first burst: 0.65 s in chapter 1 on Normal, never under 0.3 s.</summary>
        public const double MinReactionSeconds = 0.3, MaxReactionSeconds = 0.65;
        public static double ReactionSeconds(int depth, int difficulty)
        { return Clamp(MaxReactionSeconds - 0.08 * (Chapter(depth) - 1) - 0.05 * (RogueDepth.ClampDifficulty(difficulty) - 1), MinReactionSeconds, MaxReactionSeconds); }
        /// <summary>A marksman settles its aim for longer; a rusher shoots from the hip.</summary>
        public static double RoleReactionMul(string roleId)
        { return roleId == "role.marksman" ? 1.5 : roleId == "role.rusher" ? 0.7 : 1.0; }
        /// <summary>The reaction time of one role: the stage's time scaled by the role, and still never under
        /// <see cref="MinReactionSeconds"/> (a rusher's x0.7 on the late-chapter floor would otherwise read 0.21 s).</summary>
        public static double ReactionSeconds(string roleId, int depth, int difficulty)
        { return Math.Max(MinReactionSeconds, ReactionSeconds(depth, difficulty) * RoleReactionMul(roleId)); }

        /// <summary>Enemies that aim true at one player at the same time. The rest keep shooting but deliberately wide (suppression):
        /// 4 on one player in chapter 1 on Normal, one more per chapter and per difficulty step, at most 8.</summary>
        public static int AttackTokens(int depth, int difficulty)
        { return (int)Clamp(4 + (Chapter(depth) - 1) + (RogueDepth.ClampDifficulty(difficulty) - 1), 4, 8); }
        /// <summary>Enemies inside point-blank distance that aim true at one player without an attack token (the nearest first; one
        /// more while the squad presses). A fourth enemy at arm's length asks for an ordinary token. Before this cap every enemy
        /// inside that distance aimed true: eight at once were measured on one player.</summary>
        public const int PointBlankTokens = 3;
        /// <summary>A nearer enemy takes a point-blank place from the farthest holder when it is closer by this much (m).</summary>
        public const double PointBlankSwapMeters = 2.0;
        /// <summary>Extra tokens while the squad presses (the player reloads, revives, carries, or sat in cover too long).</summary>
        public const int PushExtraTokens = 1;
        /// <summary>A holder gives its token up after this long when somebody else is waiting, and waits TokenCooldown before asking again.</summary>
        public const double TokenHoldSeconds = 3.0, TokenCooldownSeconds = 1.2, TokenStaleSeconds = 0.9;
        /// <summary>How wide a suppressing enemy aims (m beside the target, m above or below). A character is about 3 m wide: at
        /// 1.8 m beside its centre the rounds still landed on a player who strafes (measured 2026-10-04 with twelve chapter-1
        /// enemies: 11% of the suppressing rounds hit a standing player, 22% a strafing one).</summary>
        public const double SuppressMissMeters = 3.0, SuppressMissVertical = 0.4;
        /// <summary>Seconds an enemy without a token waits between its suppressing bursts. The token holders carry the squad's fire;
        /// with 28 enemies on the field this also keeps the number of rounds in the air (and their cost) down. Kept under three
        /// seconds: an enemy that has the player in view and stays silent longer reads as "it is not shooting at me".</summary>
        public const double SuppressPauseMin = 1.2, SuppressPauseMax = 2.4;
        /// <summary>Up to this many enemies on one player suppress at the full rate; a larger crowd stretches each one's pause in
        /// proportion (28 enemies: x2.8), so the squad's volume of fire, and the cost of its rounds, stays about level.</summary>
        public const int SuppressFullRateSquad = 10;

        // ---------------------------------------------------------------- positions
        /// <summary>Seconds a role stays at a firing spot before it looks for another (shorter in later chapters).</summary>
        public static double HoldSeconds(string roleId, int depth)
        {
            double scale = Clamp(1.0 - 0.08 * (Chapter(depth) - 1), 0.6, 1.0);
            switch (roleId)
            {
                case "role.rusher": return 1.6 * scale;
                case "role.flanker": return 3.5 * scale;
                case "role.shieldbearer": return 3.0 * scale;
                case "role.marksman": return 9.0 * scale;
                case "role.jammer": return 8.0 * scale;
                default: return 6.0 * scale;
            }
        }
        /// <summary>Share of its spawn health an enemy may lose at one spot before it relocates (the shield bearer keeps coming).</summary>
        public const double HitRelocateShare = 0.12;
        /// <summary>Seconds without a line to the target after which a holder leaves its spot to get one.</summary>
        public const double LostSightSeconds = 1.5;
        /// <summary>Rings (m) searched around the enemy itself, spacing kept from squad mates (m), and the cone (degrees) around a
        /// player's view inside which an open spot counts as "in front of the crosshair".</summary>
        public const double NearRing = 7, FarRing = 14, SpacingMeters = 8, AimConeDegrees = 14;
        /// <summary>When the role's own sector holds no spot with a line of fire, one more search looks all round the target: this
        /// many directions at the role's range, and as many between them at AllRoundInnerShare of it.</summary>
        public const int AllRoundDirections = 12;
        public const double AllRoundInnerShare = 0.55;
        /// <summary>Score weights of a candidate spot.</summary>
        public const double WeightSight = 4.0, WeightCover = 2.5, WeightHidden = 3.5, WeightRange = 2.0, WeightSpacing = 1.5,
            WeightAimCone = 2.0, WeightTravel = 1.0, WeightFlank = 3.0, WeightStay = 1.0, WeightBehindShield = 1.0, WeightHighGround = 1.0;
        /// <summary>Closest an enemy with a gun stands to its target by role (the rusher's own preferred range is its floor).</summary>
        public static double MinRange(string roleId, double preferred)
        {
            switch (roleId)
            {
                case "role.rusher": return Math.Max(5.0, preferred * 0.6);
                case "role.marksman": return preferred * 0.4;
                case "role.jammer": return 18;
                case "role.shieldbearer": return Math.Max(8.0, preferred * 0.6);
                default: return Math.Max(10.0, preferred * 0.45);
            }
        }
        /// <summary>With no firing spot in reach an enemy closes in on the last known place by this much (m) at a time.</summary>
        public const double CloseInStep = 12;
        /// <summary>... or by this share of the distance, whichever is more: an enemy 100 m from the fight walks 35 m at a time.</summary>
        public const double CloseInShare = 0.35;
        /// <summary>Degrees off the squad's line of fire a flanking enemy aims for, and how far out (m) it swings on the way.</summary>
        public const double FlankDegrees = 95, RusherLaneDegrees = 50, FlankSwingMeters = 30;
        /// <summary>Enemies (besides the Flanker role itself) the squad sends round the side at once: none in chapter 1, then 1, at most 3.</summary>
        public static int FlankDuty(int depth) { return (int)Clamp(Chapter(depth) - 1, 0, 3); }
        /// <summary>No enemy body walks closer to a player than this (m): a crowd used to shove the player around.</summary>
        public const double BodySeparation = 2.6;

        // ---------------------------------------------------------------- the jammer
        /// <summary>The jammer's loop: hidden (its field over the player) for JammerHideSeconds, then at a spot with a line of fire
        /// for JammerPeekSeconds, and again. A phase's clock starts once the phase is real (no line / it sees the target), or
        /// JammerSettleSeconds after the change at the latest. Kept short enough that a jammer near a player is never silent for
        /// much more than ten seconds.</summary>
        public const double JammerHideSeconds = 4.0, JammerPeekSeconds = 4.5, JammerSettleSeconds = 3.0;

        // ---------------------------------------------------------------- a place to attack
        /// <summary>Enemies the squad sends to a device a player repairs (Protect the Repair, the optional Repair Device): they
        /// stand inside its drain radius and wear it down until they are killed. 3 solo in chapter 1, one more per extra player and
        /// per chapter (two chapters at most), at most 6; the device counts at most RogueMissionRules.MaxDrainingEnemies of them.</summary>
        public static int DeviceAttackers(int players, int depth)
        { return (int)Clamp(2 + Players(players) + Math.Min(2, Chapter(depth) - 1), 3, 6); }
        /// <summary>Enemies sent into a zone the squad holds (Hold the Zone) to contest it: one fewer than for a device, at least 2.</summary>
        public static int ZoneContesters(int players, int depth)
        { return (int)Clamp(1 + Players(players) + Math.Min(2, Chapter(depth) - 1), 2, 5); }
        /// <summary>How far from the middle of the place they stand, as a share of its radius.</summary>
        public const double ObjectiveStandShare = 0.7;

        // ---------------------------------------------------------------- pushes
        /// <summary>Seconds the squad presses a player who reloads, revives a teammate or carries an objective.</summary>
        public const double OpportunitySeconds = 3.0;
        /// <summary>A player who stays within CampRadius for CampSeconds is flushed out: flankers and rushers charge for
        /// FlushSeconds; then the squad waits FlushCooldown. Later chapters lose patience sooner.</summary>
        public const double CampRadius = 9, FlushSeconds = 6, FlushCooldown = 14;
        public static double CampSeconds(int depth) { return Clamp(11.0 - 1.5 * (Chapter(depth) - 1), 5.0, 11.0); }
        /// <summary>Speed multipliers on an enemy's own speed: closing in unseen, charging, heading for cover.</summary>
        public const double AdvanceSpeed = 1.3, ChargeSpeed = 1.4, CoverSpeed = 1.2;
        /// <summary>Preferred range is shortened by this factor while the squad presses.</summary>
        public const double PushRangeMul = 0.65;
    }
}
