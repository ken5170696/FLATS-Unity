using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    /// <summary>QA-52: where an arrival stands relative to the squad's facing.</summary>
    public enum ArrivalSide { Unknown = 0, Front = 1, Flank = 2, Rear = 3 }

    /// <summary>QA-52: how an arrival position was found.</summary>
    public enum ArrivalSource { Authored = 0, Synthesized = 1, Fallback = 2 }

    /// <summary>QA-52 diagnostics: why one synthesis sample was rejected (the Unity side reports each rejected sample).</summary>
    public enum SynthFailure
    {
        BearingRefused = 0,   // the drawn bearing fell outside the route cone or into a capped rear arc
        NoNavMesh = 1,        // no NavMesh near the guess
        LeftSector = 2,       // the NavMesh sample slid out of the sector, the cone or into a capped rear arc
        OutsideRing = 3,      // anchored: the sample left the anchor ring
        TooClose = 4,         // nearer than the minimum distance to a player
        InView = 5,           // inside a player's view cone with a clear line of sight
        Unreachable = 6,      // no complete NavMesh path to the nearest standing player
    }

    /// <summary>QA-52: one authored spawn point as the arrival director sees it. Plain numbers; the Unity side fills them.</summary>
    public struct ArrivalCandidate
    {
        /// <summary>World position on the ground plane (metres).</summary>
        public double x, z;
        /// <summary>Metres to the nearest present player (downed players included).</summary>
        public double nearestPlayer;
        /// <summary>Break Out opening wave: the point is nearer the exit than the nearest player is.</summary>
        public bool onRoute;
        /// <summary>SpawnAnchor set: the point lies in the anchor ring.</summary>
        public bool inRing;
    }

    /// <summary>QA-52: the squad and the rules in force for one arrival.</summary>
    public sealed class ArrivalContext
    {
        /// <summary>Origin of the sectors: the centre of the standing players, or the SpawnAnchor in ring mode.</summary>
        public double centreX, centreZ;
        /// <summary>Squad facing as a bearing in degrees (0 = +Z, 90 = +X); NaN when unknown (no side bias, no rear rules).</summary>
        public double facing = double.NaN;
        /// <summary>Stage seconds. It restarts every stage; a value lower than the last recorded one resets the memory.</summary>
        public double now;
        /// <summary>Wave index: 0 is the opening wave, -1 an extra outside the plan.</summary>
        public int wave = -1;
        /// <summary>One player present (the solo fairness knob applies in early chapters).</summary>
        public bool solo;
        public int chapter = 1;
        /// <summary>SpawnAnchor ring rule: only candidates flagged inRing, and synthesized arrivals in the ring.</summary>
        public bool anchored;
        /// <summary>Break Out opening wave: bearing from the squad to the exit; NaN otherwise.</summary>
        public double routeBearing = double.NaN;
        /// <summary>Authored point used last; not repeated while another point in its sector qualifies.</summary>
        public int lastCandidate = -1;
        /// <summary>Distance band (metres to the nearest player), the same numbers as RoguelikeController's constants.</summary>
        public double minDistance = 40, bandDistance = 120, openingMinDistance = 60;
        /// <summary>The opening-wave rules apply (wave 0 outside the anchor ring, as the old picker ordered them).</summary>
        public bool Opening { get { return wave == 0 && !anchored; } }
    }

    /// <summary>QA-52: the sectors to try for one arrival, best first. candidates[i] is an authored point index, or -1 to synthesize
    /// a NavMesh arrival inside sectors[i]; a synthesized bearing must pass AllowsBearing(i, bearing).</summary>
    public sealed class ArrivalPlan
    {
        public readonly List<int> sectors = new List<int>();
        public readonly List<int> candidates = new List<int>();
        readonly List<bool> rearBlocked = new List<bool>();
        readonly List<int> levels = new List<int>();
        double facing = double.NaN, routeBearing = double.NaN;

        public int Count { get { return sectors.Count; } }

        /// <summary>Relaxation level of entry i: 0 = every rule holds; 1 opening coverage, 2 consecutive sector, 3 rear share and
        /// 4 the solo rear window were relaxed to offer it.</summary>
        public int Level(int i) { return i >= 0 && i < levels.Count ? levels[i] : int.MaxValue; }

        internal void Clear(ArrivalContext ctx) { sectors.Clear(); candidates.Clear(); rearBlocked.Clear(); levels.Clear(); facing = ctx.facing; routeBearing = ctx.routeBearing; }
        internal void Add(int sector, int candidate, bool rear, int level) { sectors.Add(sector); candidates.Add(candidate); rearBlocked.Add(rear); levels.Add(level); }
        internal bool Contains(int sector) { return sectors.Contains(sector); }

        /// <summary>A synthesized arrival for entry i may stand at this bearing (inside the route cone; outside the rear arc while the
        /// rear is capped for that entry).</summary>
        public bool AllowsBearing(int i, double bearing)
        {
            if (i < 0 || i >= sectors.Count || double.IsNaN(bearing) || double.IsInfinity(bearing)) return false;
            if (ArrivalDirector.SectorOf(bearing) != sectors[i]) return false;
            if (!double.IsNaN(routeBearing) && Math.Abs(ArrivalDirector.AngleDiff(bearing, routeBearing)) > ArrivalDirector.RouteHalfAngle) return false;
            if (rearBlocked[i] && ArrivalDirector.SideOf(bearing, facing) == ArrivalSide.Rear) return false;
            return true;
        }
    }

    /// <summary>
    /// QA-52: the cap on holding one arrival back. A held slot is keyed by stage and slot id (slot ids restart at 1 every stage) and
    /// timed on a clock that never runs backwards (the caller passes Time.time), so a record left over from an earlier stage, a
    /// cleared queue or a cancelled plan can only shorten a hold, never extend it: an arrival waits at most MaxHoldSeconds.
    /// </summary>
    public sealed class ArrivalHold
    {
        int stage = int.MinValue, slot = -1;
        double since;

        /// <summary>This slot of this stage is the one being held back.</summary>
        public bool IsHolding(int stageKey, int slotId) { return slot >= 0 && slot == slotId && stage == stageKey; }

        /// <summary>The slot may still be held back at monotonic time now (a slot not held yet always may).</summary>
        public bool MayHold(int stageKey, int slotId, double now, double maxHoldSeconds)
        {
            if (!IsHolding(stageKey, slotId)) return true;
            if (double.IsNaN(now) || double.IsInfinity(now) || now < since) { Clear(); return true; }   // a clock that restarted: a new hold
            return now - since < maxHoldSeconds;
        }

        /// <summary>The slot was held back at monotonic time now; the first hold of a slot starts its clock.</summary>
        public void Held(int stageKey, int slotId, double now)
        {
            if (IsHolding(stageKey, slotId) && !(now < since)) return;
            stage = stageKey; slot = slotId; since = now;
        }

        /// <summary>The slot left the queue (placed, voided, or the queue was cleared).</summary>
        public void Released(int stageKey, int slotId) { if (IsHolding(stageKey, slotId)) Clear(); }

        public void Clear() { stage = int.MinValue; slot = -1; since = 0; }
    }

    /// <summary>QA-52: one recorded arrival (diagnostics).</summary>
    public struct ArrivalRecord
    {
        public double time, bearing, relativeBearing;   // relativeBearing is NaN when the facing was unknown
        public double distance;                          // metres from the sector origin (the squad centre, or the anchor)
        public int wave, sector;
        public ArrivalSide side;
        public ArrivalSource source;
    }

    /// <summary>
    /// QA-52: direction-aware arrival director. The old picker chose spawn points by distance only, so a stage's arrivals came from
    /// one side. The circle around the squad (or around the SpawnAnchor) is split into SectorCount sectors; each arrival ranks them by
    /// the distance band (authored points in the band, else a synthesized NavMesh arrival), by novelty (sectors used recently weigh
    /// less, their neighbours a little less) and by a mild flank/rear bias, under hard rules: no two consecutive arrivals in one sector,
    /// the opening wave covers OpeningMinSectors sectors, the rear share is capped, and a solo player in an early chapter gets at most
    /// SoloEarlyMaxRear arrivals from behind per SoloEarlyRearWindowSeconds. Rules relax in that order (coverage, consecutive, rear
    /// share, solo rear) only when nothing else qualifies, so a map that allows one side still spawns. Pacing, counts and money are
    /// untouched: the director only chooses where.
    /// Bearings are degrees clockwise from +Z (0 = north, 90 = east); sector 0 is centred on north.
    /// </summary>
    public sealed class ArrivalDirector
    {
        /// <summary>Sectors around the squad (45 degrees each).</summary>
        public const int SectorCount = 8;
        public const double SectorDegrees = 360.0 / SectorCount;
        /// <summary>Recent arrivals remembered for novelty and the rear share.</summary>
        public const int HistoryLength = 8;
        /// <summary>Novelty weight = 1 / (1 + NoveltyPenalty * sum over history of NoveltyDecay^age * (1 same sector, AdjacentNovelty
        /// neighbour)). The newest arrival has age 0.</summary>
        public const double NoveltyPenalty = 1.5, NoveltyDecay = 0.8, AdjacentNovelty = 0.4;
        /// <summary>Front arc: |bearing - facing| at most FrontHalfAngle. Rear arc: at least 180 - RearHalfAngle (a 90 degree arc
        /// behind the squad). The rest is the flanks.</summary>
        public const double FrontHalfAngle = 60, RearHalfAngle = 45;
        /// <summary>Mild side bias (multiplies a sector's weight): flanks and rear a little more than the front.</summary>
        public const double FrontBias = 1.0, FlankBias = 1.3, RearBias = 1.15;
        /// <summary>Rear share cap: no rear arrival while RearMaxInHistory of the last HistoryLength arrivals came from the rear
        /// (at most 25 percent of any 9 consecutive arrivals).</summary>
        public const int RearMaxInHistory = 2;
        /// <summary>The opening wave covers at least this many sectors when the map allows it.</summary>
        public const int OpeningMinSectors = 3;
        /// <summary>Solo fairness knob: in chapters up to SoloEarlyMaxChapter a solo player gets at most SoloEarlyMaxRear arrivals
        /// from the rear arc within any SoloEarlyRearWindowSeconds (the opening wave lands inside one window, so it holds at most one
        /// enemy behind the player: three sides of pressure, never a pincer closing from behind).</summary>
        public const int SoloEarlyMaxChapter = 1, SoloEarlyMaxRear = 1;
        public const double SoloEarlyRearWindowSeconds = 12;
        /// <summary>A sector without an authored point in the band is still offered, synthesized, at this weight (authored lanes are
        /// the designer's choice and come first when they exist).</summary>
        public const double SynthesizedFactor = 0.75;
        /// <summary>Opening wave: a sector whose only points stand between minDistance and openingMinDistance.</summary>
        public const double OpeningCloseFactor = 0.6;
        /// <summary>Opening wave: a sector's weight falls with its nearest point beyond openingMinDistance over this many metres.</summary>
        public const double OpeningFalloffMetres = 60;
        /// <summary>A sector whose synthesis failed is not offered for synthesis again for SynthRetrySeconds, doubling with each
        /// further failure up to SynthRetryMaxSeconds (a map edge or open ground stays unavailable instead of eating every arrival's
        /// budget), until the sector origin moves SynthRetryMoveMetres from where it failed or a synthesis there succeeds.</summary>
        public const double SynthRetrySeconds = 8, SynthRetryMaxSeconds = 45, SynthRetryMoveMetres = 30;
        /// <summary>Self-healing ground memory (a memory that only grew could leave every sector resting on a corner start whose few open
        /// sides are part water, and most arrivals then fell back to the old rule):
        /// - a sector failure counts as ground only when at least GroundFailRatio of its rejected samples were the ground's
        ///   (no NavMesh, off the sector, outside the ring, no path); a partly open sector keeps being tried;
        /// - a stage reset decays the failure count by one and carries at most SynthCarryMaxSeconds of rest;
        /// - the plan always keeps MinEligibleSectors sectors eligible, re-admitting the resting ones with the fewest failures first;
        /// - the squad moving SynthRetryMoveMetres or a success in the sector clears it.</summary>
        public const double GroundFailRatio = 0.7, SynthCarryMaxSeconds = 16;
        public const int MinEligibleSectors = 3;
        /// <summary>A sector whose first NoMeshGiveUpSamples samples all found no NavMesh is given up for this arrival (off the map),
        /// keeping the per-arrival budget for the sectors that exist; a sector that is part land continues (0.45^4 = 4% false stops at
        /// 55% land).</summary>
        public const int NoMeshGiveUpSamples = 4;
        /// <summary>Break Out opening wave: synthesized arrivals stay within this angle of the exit's bearing (the old fan was 70).</summary>
        public const double RouteHalfAngle = 70;
        /// <summary>Synthesized sectors the Unity side tries for one arrival: up to MaxStrictSynthesizedSectors while the entries keep
        /// every rule (level 0), so an early failure does not relax the opening spread or the consecutive rule; relaxed entries are
        /// synthesized only while fewer than MaxSynthesizedSectors were tried.</summary>
        public const int MaxSynthesizedSectors = 3, MaxStrictSynthesizedSectors = 6;
        /// <summary>A player sees what lies within this many degrees of its facing (a 16:9 to 21:9 view is 90-110 degrees wide,
        /// plus margin for turning); outside the cone an arrival is out of sight even on open ground. Inside it, the line of sight
        /// must be blocked.</summary>
        public const double ViewConeHalfAngle = 80;
        /// <summary>Recent arrivals kept for diagnostics.</summary>
        public const int RecordCapacity = 256;

        struct Entry { public int sector; public ArrivalSide side; }

        readonly RogueRng rng;
        readonly List<Entry> history = new List<Entry>();
        readonly List<double> rearTimes = new List<double>();
        readonly List<int> waveSectors = new List<int>();
        readonly double[] synthUnavailableUntil = new double[SectorCount];
        readonly int[] synthFails = new int[SectorCount];
        readonly bool[] synthGeometric = new bool[SectorCount];   // the last failure was the ground (no NavMesh, no path), not a view
        readonly bool[] synthReleased = new bool[SectorCount];    // re-admitted for this plan although resting (MinEligibleSectors)
        readonly double[] synthFailX = new double[SectorCount], synthFailZ = new double[SectorCount];
        readonly ArrivalPlan plan = new ArrivalPlan();
        int currentWave = int.MinValue, lastSector = -1;
        double lastTime = double.NegativeInfinity;

        // cumulative diagnostics (not reset per stage; ResetStats clears them)
        public readonly long[] WorldSectorCounts = new long[SectorCount];
        public readonly long[] RelativeSectorCounts = new long[SectorCount];   // 0 = ahead of the squad, 4 = behind
        public readonly long[] SideCounts = new long[4];                       // indexed by ArrivalSide
        public readonly long[] SourceCounts = new long[3];                     // indexed by ArrivalSource
        public readonly long[] SynthFailureCounts = new long[7];               // rejected samples, indexed by SynthFailure
        public readonly long[] SynthFailedSectorCounts = new long[SectorCount]; // sectors whose whole synthesis failed
        /// <summary>Arrivals held back because every admissible position failed (the solo rear window would have been broken).</summary>
        public long Deferrals;
        /// <summary>Old-rule positions refused because they broke the solo rear window.</summary>
        public long FallbackRefusals;
        /// <summary>Positions outside synthesis (authored points, the old rule) refused because a player would see them appear.</summary>
        public long SeenRefusals;
        readonly List<ArrivalRecord> records = new List<ArrivalRecord>();

        public ArrivalDirector(ulong seed) { rng = new RogueRng(seed); for (int s = 0; s < SectorCount; s++) synthUnavailableUntil[s] = double.NegativeInfinity; }

        /// <summary>The last RecordCapacity arrivals, oldest first.</summary>
        public IList<ArrivalRecord> Records { get { return records.AsReadOnly(); } }

        /// <summary>Sector of the latest arrival of this stage (-1 before the first).</summary>
        public int LastSector { get { return lastSector; } }

        // ---------------------------------------------------------------- geometry
        public static double Normalize(double degrees) { double d = degrees % 360.0; if (d < 0) d += 360.0; return d >= 360.0 ? 0.0 : d; }

        /// <summary>Signed smallest difference a - b in (-180, 180].</summary>
        public static double AngleDiff(double a, double b) { double d = Normalize(a - b); return d > 180.0 ? d - 360.0 : d; }

        /// <summary>Bearing from one ground point to another, degrees clockwise from +Z. Coincident points give 0.</summary>
        public static double Bearing(double fromX, double fromZ, double toX, double toZ)
        {
            double dx = toX - fromX, dz = toZ - fromZ;
            if (dx == 0 && dz == 0) return 0;
            return Normalize(Math.Atan2(dx, dz) * 180.0 / Math.PI);
        }

        /// <summary>Sector of a bearing; sector 0 is centred on 0 degrees (north), sector 2 on east.</summary>
        public static int SectorOf(double bearing) { return (int)Math.Floor(Normalize(bearing + SectorDegrees / 2) / SectorDegrees) % SectorCount; }

        public static double SectorCentre(int sector) { return Normalize(sector * SectorDegrees); }

        /// <summary>First bearing of a sector (it spans SectorDegrees clockwise from here).</summary>
        public static double SectorStart(int sector) { return Normalize(sector * SectorDegrees - SectorDegrees / 2); }

        /// <summary>A point lies inside a viewer's view cone (ViewConeHalfAngle either side of its facing). Unknown facing: inside.</summary>
        public static bool InViewCone(double viewerX, double viewerZ, double viewerFacing, double x, double z)
        {
            if (double.IsNaN(viewerFacing) || double.IsInfinity(viewerFacing)) return true;
            return Math.Abs(AngleDiff(Bearing(viewerX, viewerZ, x, z), viewerFacing)) <= ViewConeHalfAngle;
        }

        public static ArrivalSide SideOf(double bearing, double facing)
        {
            if (double.IsNaN(facing) || double.IsInfinity(facing) || double.IsNaN(bearing) || double.IsInfinity(bearing)) return ArrivalSide.Unknown;
            double rel = Math.Abs(AngleDiff(bearing, facing));
            if (rel <= FrontHalfAngle) return ArrivalSide.Front;
            if (rel >= 180.0 - RearHalfAngle) return ArrivalSide.Rear;
            return ArrivalSide.Flank;
        }

        static double SideBias(ArrivalSide side) { return side == ArrivalSide.Flank ? FlankBias : side == ArrivalSide.Rear ? RearBias : FrontBias; }

        /// <summary>Shannon entropy of a histogram divided by log2 of its bin count: 0 = one bin, 1 = perfectly even.</summary>
        public static double NormalizedEntropy(long[] counts)
        {
            if (counts == null || counts.Length < 2) return 0;
            double total = 0; foreach (var c in counts) total += Math.Max(0, c);
            if (total <= 0) return 0;
            double h = 0;
            foreach (var c in counts) if (c > 0) { double p = c / total; h -= p * Math.Log(p, 2); }
            return h / Math.Log(counts.Length, 2);
        }

        // ---------------------------------------------------------------- memory
        /// <summary>Forgets the stage (history, wave coverage, synthesis cooldowns). Diagnostics stay.</summary>
        public void Reset()
        {
            history.Clear(); rearTimes.Clear(); waveSectors.Clear();
            // the ground does not change between stages: a sector that failed for lack of NavMesh or a path keeps part of its rest into the
            // next stage (forgetting it made the first arrival of every stage re-probe the off-map sectors and run out of budget),
            // but the memory decays: one failure less per stage and at most SynthCarryMaxSeconds carried (a memory that only grew
            // could leave every sector resting). The squad moving or a success still clears it.
            for (int s = 0; s < SectorCount; s++)
            {
                synthFails[s] = Math.Max(0, synthFails[s] - 1);
                if (synthFails[s] > 0 && synthGeometric[s]) synthUnavailableUntil[s] = Math.Min(SynthCarryMaxSeconds, SynthRetrySeconds * Math.Pow(2, synthFails[s] - 1));
                else { synthUnavailableUntil[s] = double.NegativeInfinity; synthFails[s] = 0; synthGeometric[s] = false; }
            }
            currentWave = int.MinValue; lastSector = -1; lastTime = double.NegativeInfinity;
        }

        public void ResetStats()
        {
            Array.Clear(WorldSectorCounts, 0, SectorCount); Array.Clear(RelativeSectorCounts, 0, SectorCount);
            Array.Clear(SideCounts, 0, SideCounts.Length); Array.Clear(SourceCounts, 0, SourceCounts.Length); records.Clear();
            Array.Clear(SynthFailureCounts, 0, SynthFailureCounts.Length); Array.Clear(SynthFailedSectorCounts, 0, SectorCount);
            Deferrals = 0; FallbackRefusals = 0; SeenRefusals = 0;
        }

        void Sync(ArrivalContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException("ctx");
            if (double.IsNaN(ctx.now) || double.IsInfinity(ctx.now)) throw new ArgumentOutOfRangeException("ctx", "now must be finite");
            if (ctx.now + 1e-6 < lastTime) Reset();   // stage seconds restarted: a new stage
            if (ctx.wave != currentWave) { currentWave = ctx.wave; waveSectors.Clear(); }
            double cutoff = ctx.now - SoloEarlyRearWindowSeconds;
            rearTimes.RemoveAll(t => t <= cutoff);
        }

        int RearInHistory() { int n = 0; foreach (var e in history) if (e.side == ArrivalSide.Rear) n++; return n; }

        static bool SoloEarly(ArrivalContext ctx) { return ctx.solo && ctx.chapter <= SoloEarlyMaxChapter; }

        /// <summary>A synthesis in this sector failed with the sector origin at an unknown place (it never counts as moved).</summary>
        public void MarkUnavailable(int sector, double now) { MarkUnavailable(sector, now, double.NaN, double.NaN); }

        /// <summary>A synthesis in this sector failed with the sector origin at (x, z) because of the ground (no NavMesh, no path).</summary>
        public void MarkUnavailable(int sector, double now, double x, double z) { MarkUnavailable(sector, now, x, z, true); }

        /// <summary>A synthesis in this sector failed with the sector origin at (x, z). A ground failure (geometric: no NavMesh, no
        /// path) rests SynthRetrySeconds doubling per repeated failure (at most SynthRetryMaxSeconds), survives the stage reset and is
        /// released when the origin moves SynthRetryMoveMetres away. A failure because every hidden spot was in view (or refused by
        /// the rules) does not rest the sector: the players turn and the view changes within the opening's quarter seconds, and a
        /// re-test is cheap (a NavMesh sample and a linecast, bounded per arrival). A rest there kept a solo opening on open ground
        /// from reaching its third side. Both are counted in SynthFailedSectorCounts.</summary>
        public void MarkUnavailable(int sector, double now, double x, double z, bool geometric)
        {
            if (sector < 0 || sector >= SectorCount || double.IsNaN(now) || double.IsInfinity(now)) return;
            SynthFailedSectorCounts[sector]++;
            if (!geometric) return;
            synthFails[sector] = Math.Min(synthFails[sector] + 1, 16);
            synthUnavailableUntil[sector] = now + Math.Min(SynthRetryMaxSeconds, SynthRetrySeconds * Math.Pow(2, synthFails[sector] - 1));
            synthGeometric[sector] = true;
            synthFailX[sector] = x; synthFailZ[sector] = z;
        }

        /// <summary>A sector may be offered for synthesis now.</summary>
        public bool SynthAvailable(ArrivalContext ctx, int sector)
        {
            if (ctx == null || sector < 0 || sector >= SectorCount) return false;
            if (ctx.now >= synthUnavailableUntil[sector]) return true;
            double fx = synthFailX[sector], fz = synthFailZ[sector];
            if (double.IsNaN(fx) || double.IsNaN(fz)) return false;
            double dx = ctx.centreX - fx, dz = ctx.centreZ - fz;
            if (dx * dx + dz * dz < SynthRetryMoveMetres * SynthRetryMoveMetres) return false;
            synthUnavailableUntil[sector] = double.NegativeInfinity; synthFails[sector] = 0; synthGeometric[sector] = false;   // the squad moved: the ground is new
            return true;
        }

        /// <summary>Diagnostics: one synthesis sample was rejected for this reason.</summary>
        public void NoteSynthFailure(SynthFailure reason) { int i = (int)reason; if (i >= 0 && i < SynthFailureCounts.Length) SynthFailureCounts[i]++; }

        /// <summary>Diagnostics: an arrival was held back (nothing admissible).</summary>
        public void NoteDeferred() { Deferrals++; }

        /// <summary>A solo opening in an early chapter is still saving its one rear arrival (fewer than OpeningMinSectors - 1 sides open):
        /// the plan offers no rear, and a position found outside the plan should avoid the rear when it can (it is not a hard rule: an
        /// arrival is never held back for it).</summary>
        public bool RearReserved(ArrivalContext ctx)
        {
            Sync(ctx);
            return ctx.Opening && SoloEarly(ctx) && waveSectors.Count < OpeningMinSectors - 1;
        }

        /// <summary>The position lies in the rear arc of the squad's facing.</summary>
        public static bool IsRear(ArrivalContext ctx, double x, double z)
        {
            if (ctx == null) throw new ArgumentNullException("ctx");
            return SideOf(Bearing(ctx.centreX, ctx.centreZ, x, z), ctx.facing) == ArrivalSide.Rear;
        }

        /// <summary>A position found outside the plan (the old rule, an authored point scan) may be used: it does not break the solo
        /// rear window, the one rule that holds on every path. The other rules shape the plan but never hold an arrival back.</summary>
        public bool Admits(ArrivalContext ctx, double x, double z)
        {
            Sync(ctx);
            if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(z) || double.IsInfinity(z)) return false;
            if (!SoloEarly(ctx) || rearTimes.Count < SoloEarlyMaxRear) return true;
            return SideOf(Bearing(ctx.centreX, ctx.centreZ, x, z), ctx.facing) != ArrivalSide.Rear;
        }

        /// <summary>Diagnostics: an old-rule position was refused by Admits.</summary>
        public void NoteFallbackRefused() { FallbackRefusals++; }

        /// <summary>Diagnostics: an authored or old-rule position was refused because a player would see it appear.</summary>
        public void NoteSeenRefusal() { SeenRefusals++; }

        /// <summary>A sector's synthesis failed because of the ground (see GroundFailRatio), from its rejected-sample counts.</summary>
        public static bool IsGroundFailure(int groundRejects, int otherRejects)
        {
            return groundRejects > 0 && groundRejects >= GroundFailRatio * (groundRejects + Math.Max(0, otherRejects));
        }

        /// <summary>Records where an arrival actually stood (every arrival, including the old-rule fallback, so novelty sees it).</summary>
        public void Record(ArrivalContext ctx, double x, double z, ArrivalSource source)
        {
            Sync(ctx);
            if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(z) || double.IsInfinity(z)) return;
            double bearing = Bearing(ctx.centreX, ctx.centreZ, x, z);
            int sector = SectorOf(bearing);
            var side = SideOf(bearing, ctx.facing);
            history.Add(new Entry { sector = sector, side = side });
            if (history.Count > HistoryLength) history.RemoveAt(0);
            if (side == ArrivalSide.Rear) rearTimes.Add(ctx.now);
            if (!waveSectors.Contains(sector)) waveSectors.Add(sector);
            if (source == ArrivalSource.Synthesized) { synthFails[sector] = 0; synthGeometric[sector] = false; synthUnavailableUntil[sector] = double.NegativeInfinity; }
            lastSector = sector; lastTime = ctx.now;

            double relative = side == ArrivalSide.Unknown ? double.NaN : Normalize(bearing - ctx.facing);
            WorldSectorCounts[sector]++;
            if (!double.IsNaN(relative)) RelativeSectorCounts[SectorOf(relative)]++;
            SideCounts[(int)side]++;
            SourceCounts[(int)source]++;
            double dx = x - ctx.centreX, dz = z - ctx.centreZ;
            records.Add(new ArrivalRecord { time = ctx.now, bearing = bearing, relativeBearing = relative, distance = Math.Sqrt(dx * dx + dz * dz), wave = ctx.wave, sector = sector, side = side, source = source });
            if (records.Count > RecordCapacity) records.RemoveAt(0);
        }

        /// <summary>One line for logs: relative sector counts (0 = ahead, clockwise), sides, sources and the normalized entropy.</summary>
        public string Describe()
        {
            var sb = new System.Text.StringBuilder("arrivals rel[");
            for (int s = 0; s < SectorCount; s++) sb.Append(s == 0 ? "" : " ").Append(RelativeSectorCounts[s]);
            sb.Append("] world[");
            for (int s = 0; s < SectorCount; s++) sb.Append(s == 0 ? "" : " ").Append(WorldSectorCounts[s]);
            sb.Append("] front ").Append(SideCounts[(int)ArrivalSide.Front]).Append(" flank ").Append(SideCounts[(int)ArrivalSide.Flank])
              .Append(" rear ").Append(SideCounts[(int)ArrivalSide.Rear]).Append(" unknown ").Append(SideCounts[(int)ArrivalSide.Unknown])
              .Append(" authored ").Append(SourceCounts[0]).Append(" synthesized ").Append(SourceCounts[1]).Append(" fallback ").Append(SourceCounts[2])
              .Append(" entropy ").Append(NormalizedEntropy(RelativeSectorCounts).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture))
              .Append(" synthReject[bearing ").Append(SynthFailureCounts[0]).Append(" nomesh ").Append(SynthFailureCounts[1])
              .Append(" left ").Append(SynthFailureCounts[2]).Append(" ring ").Append(SynthFailureCounts[3]).Append(" close ").Append(SynthFailureCounts[4])
              .Append(" view ").Append(SynthFailureCounts[5]).Append(" path ").Append(SynthFailureCounts[6]).Append("] sectorFails[");
            for (int s = 0; s < SectorCount; s++) sb.Append(s == 0 ? "" : " ").Append(SynthFailedSectorCounts[s]);
            sb.Append("] deferred ").Append(Deferrals).Append(" fallbackRefused ").Append(FallbackRefusals).Append(" seenRefused ").Append(SeenRefusals);
            return sb.ToString();
        }

        // ---------------------------------------------------------------- planning
        struct Option { public int index; public double distance; public ArrivalSide side; }

        /// <summary>Ranks the sectors for the next arrival. The plan object is reused by the next call. Candidates that are invalid,
        /// nearer than minDistance to a player, outside the band (outside the ring when anchored) or off the Break Out route are ignored;
        /// the sectors they would have served are offered for synthesis instead.</summary>
        public ArrivalPlan Plan(ArrivalContext ctx, IList<ArrivalCandidate> candidates)
        {
            Sync(ctx);
            plan.Clear(ctx);
            bool opening = ctx.Opening;
            bool route = opening && !double.IsNaN(ctx.routeBearing) && !double.IsInfinity(ctx.routeBearing);

            // authored options per sector
            var bySector = new List<Option>[SectorCount];
            for (int s = 0; s < SectorCount; s++) bySector[s] = new List<Option>();
            bool anyRoute = false;
            if (route && candidates != null)
                foreach (var c in candidates) if (c.onRoute && Usable(ctx, c)) { anyRoute = true; break; }
            if (candidates != null)
                for (int i = 0; i < candidates.Count; i++)
                {
                    var c = candidates[i];
                    if (!Usable(ctx, c) || (anyRoute && !c.onRoute)) continue;
                    double bearing = Bearing(ctx.centreX, ctx.centreZ, c.x, c.z);
                    bySector[SectorOf(bearing)].Add(new Option { index = i, distance = c.nearestPlayer, side = SideOf(bearing, ctx.facing) });
                }

            // never let the ground memory close the whole circle: keep MinEligibleSectors sectors eligible, re-admitting the resting
            // sectors with the fewest failures (then the soonest to recover) for this plan
            int eligible = 0;
            for (int s = 0; s < SectorCount; s++) { synthReleased[s] = false; if (bySector[s].Count > 0 || SynthAvailable(ctx, s)) eligible++; }
            while (eligible < MinEligibleSectors)
            {
                int best = -1;
                for (int s = 0; s < SectorCount; s++)
                {
                    if (synthReleased[s] || bySector[s].Count > 0 || SynthAvailable(ctx, s)) continue;
                    if (best < 0 || synthFails[s] < synthFails[best] || (synthFails[s] == synthFails[best] && synthUnavailableUntil[s] < synthUnavailableUntil[best])) best = s;
                }
                if (best < 0) break;
                synthReleased[best] = true; eligible++;
            }

            bool rearShareFull = RearInHistory() >= RearMaxInHistory;
            bool soloRearFull = SoloEarly(ctx) && rearTimes.Count >= SoloEarlyMaxRear;

            // level 0 has every rule; each later level drops one (coverage, consecutive, rear share, solo rear)
            var weights = new double[SectorCount];
            var picks = new int[SectorCount];
            for (int level = 0; level <= 4; level++)
            {
                bool coverage = level < 1, consecutive = level < 2, rearShare = level < 3, soloRear = level < 4;
                // a solo opening keeps its one rear arrival for after the front and flanks have opened: spent first, it left maps whose
                // other sides are few (a corner start) unable to open a third side
                bool saveRear = coverage && opening && SoloEarly(ctx) && waveSectors.Count < OpeningMinSectors - 1;
                bool rearBlocked = (rearShare && rearShareFull) || (soloRear && soloRearFull) || saveRear;
                double total = 0;
                for (int s = 0; s < SectorCount; s++)
                {
                    weights[s] = 0; picks[s] = -1;
                    if (plan.Contains(s)) continue;
                    if (consecutive && s == lastSector) continue;
                    if (coverage && opening && waveSectors.Count < OpeningMinSectors && waveSectors.Contains(s)) continue;
                    double baseWeight = SectorBase(ctx, s, bySector[s], rearBlocked, opening, route, out picks[s]);
                    if (baseWeight <= 0) continue;
                    weights[s] = baseWeight * Novelty(s) * SideBias(SideOf(SectorCentre(s), ctx.facing));
                    total += weights[s];
                }
                // weighted draw without replacement: every qualifying sector of this level, best-drawn first
                while (total > 1e-12)
                {
                    double roll = rng.NextDouble() * total;
                    int chosen = -1;
                    for (int s = 0; s < SectorCount; s++)
                    {
                        if (weights[s] <= 0) continue;
                        chosen = s;
                        roll -= weights[s];
                        if (roll < 0) break;
                    }
                    if (chosen < 0) break;
                    plan.Add(chosen, picks[chosen], rearBlocked, level);
                    total -= weights[chosen]; weights[chosen] = 0;
                }
            }
            return plan;
        }

        static bool Usable(ArrivalContext ctx, ArrivalCandidate c)
        {
            if (double.IsNaN(c.x) || double.IsInfinity(c.x) || double.IsNaN(c.z) || double.IsInfinity(c.z) || double.IsNaN(c.nearestPlayer)) return false;
            if (c.nearestPlayer < ctx.minDistance) return false;   // no face spawns
            return ctx.anchored ? c.inRing : c.nearestPlayer <= ctx.bandDistance;
        }

        double Novelty(int sector)
        {
            double load = 0, weight = 1;
            for (int i = history.Count - 1; i >= 0; i--, weight *= NoveltyDecay)
            {
                int d = Math.Abs(history[i].sector - sector); d = Math.Min(d, SectorCount - d);
                if (d == 0) load += weight; else if (d == 1) load += weight * AdjacentNovelty;
            }
            return 1.0 / (1.0 + NoveltyPenalty * load);
        }

        /// <summary>Weight of a sector before novelty and side bias, and its authored pick (-1 = synthesize). 0 = not offered.</summary>
        double SectorBase(ArrivalContext ctx, int sector, List<Option> options, bool rearBlocked, bool opening, bool route, out int pick)
        {
            pick = -1;
            var viable = new List<Option>();
            foreach (var o in options) if (!(rearBlocked && o.side == ArrivalSide.Rear)) viable.Add(o);
            if (viable.Count > 1) viable.RemoveAll(o => o.index == ctx.lastCandidate);
            if (viable.Count > 0)
            {
                if (!opening) { pick = viable[rng.Next(viable.Count)].index; return 1.0; }
                // opening: contact within seconds but not in the squad's face, as the old rule (nearest points past openingMinDistance)
                var far = viable.FindAll(o => o.distance >= ctx.openingMinDistance);
                if (far.Count == 0) { pick = viable[rng.Next(viable.Count)].index; return OpeningCloseFactor; }
                far.Sort((a, b) => a.distance.CompareTo(b.distance));
                pick = far[rng.Next(Math.Min(2, far.Count))].index;
                return 1.0 / (1.0 + (far[0].distance - ctx.openingMinDistance) / OpeningFalloffMetres);
            }
            // no authored point here: offer a synthesized arrival unless the sector failed recently, lies wholly in a capped rear arc
            // or wholly outside the Break Out cone
            if (!synthReleased[sector] && !SynthAvailable(ctx, sector)) return 0;
            if (rearBlocked && !SectorHasNonRear(sector, ctx.facing)) return 0;
            if (route && Math.Abs(AngleDiff(SectorCentre(sector), ctx.routeBearing)) >= RouteHalfAngle + SectorDegrees / 2) return 0;
            return SynthesizedFactor;
        }

        static bool SectorHasNonRear(int sector, double facing)
        {
            double start = SectorStart(sector);
            for (int k = 0; k <= 8; k++) if (SideOf(start + SectorDegrees * k / 8.0, facing) != ArrivalSide.Rear) return true;
            return false;
        }
    }
}
