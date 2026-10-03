using System;
using System.Collections.Generic;
using System.Linq;

namespace Flats.Core.Roguelike
{
    public sealed class UltimateRuntime
    {
        public string Id { get; private set; }
        public double DurationSeconds { get; private set; }
        public bool ReviveUsed { get; private set; }
        private bool activated;
        private double started;
        public UltimateRuntime(string id, bool reviveUsed = false)
        {
            var item = RogueCatalog.Item(id); if (item == null || item.Kind != ItemKind.Ultimate) throw new ArgumentException("ultimate id");
            Id = id; ReviveUsed = reviveUsed;
            DurationSeconds = RogueCatalog.UltimateSeconds(id);
        }
        // 充能及跨裝備的一次／run 旗標由 RunMachine.SpendUltimate 持有；此物件只處理效果時效。
        public bool Activate(double now) { RogueStateBag.NonNegative(now); if (IsActive(now)) return false; started = now; activated = true; if (Id == "ult.emergency_revive") ReviveUsed = true; return true; }
        public bool IsActive(double now) { return RemainingSeconds(now) > 0; }
        public double RemainingSeconds(double now) { RogueStateBag.NonNegative(now); return activated && now >= started ? Math.Max(0, DurationSeconds - (now - started)) : 0; }
        public void Cancel() { activated = false; }
    }

    public sealed class TacticalRuntime
    {
        public string Id { get; private set; }
        public double CooldownSeconds { get; private set; }
        public int MaxCharges { get; private set; }
        public const double ShieldCapacity = 900, ShieldDurationSeconds = 10;
        /// <summary>Most dash charges a runtime holds (Dash Charges tier 3 gives 4; the bound only guards the HUD's pip row).</summary>
        public const int MaxDashCharges = 8;
        /// <summary>The tactical shield's points and seconds for a build (Shield Boost raises both); the adapter raises the shield with these.</summary>
        public static double ShieldPointsFor(BuildStats stats) { return ShieldCapacity * (stats == null ? 1 : Math.Max(1, stats.ShieldCapacityMul)); }
        public static double ShieldSecondsFor(BuildStats stats) { return ShieldDurationSeconds + (stats == null ? 0 : Math.Max(0, stats.ShieldExtraSeconds)); }
        /// <summary>Jumps in the air between two landings: one for Double Jump plus the build's extra ones (Triple Jump).</summary>
        public int MaxAirJumps { get; private set; }
        public double ShieldPoints { get; private set; }
        public double ShieldSeconds { get; private set; }
        private readonly List<double> recharge = new List<double>();
        private double shield, shieldUntil, observedNow;
        private int airJumpsUsed;
        private bool committing;
        private double lastDash = double.NegativeInfinity;
        public TacticalRuntime(string id, BuildStats stats = null)
        {
            var item = RogueCatalog.Item(id); if (item == null || item.Kind != ItemKind.Tactical) throw new ArgumentException("tactical id");
            Id = id; stats = stats ?? new BuildStats();
            MaxCharges = id == "tactical.dash" ? Math.Max(1, Math.Min(MaxDashCharges, stats.DashCharges)) : 1;
            MaxAirJumps = id == "tactical.doublejump" ? 1 + Math.Max(0, stats.ExtraAirJumps) : 0;
            ShieldPoints = ShieldPointsFor(stats); ShieldSeconds = ShieldSecondsFor(stats);
            CooldownSeconds = id == "tactical.dash" ? RogueCatalog.DashCooldownSeconds * RogueStateBag.Positive(stats.DashCooldownMul) : id == "tactical.shield" ? RogueCatalog.ShieldCooldownSeconds * RogueStateBag.Positive(stats.ShieldCooldownMul) : 0;
        }
        public void Tick(double now) { RogueStateBag.NonNegative(now); if (now < observedNow) throw new ArgumentOutOfRangeException("now", "時間不可倒退"); observedNow = now; recharge.RemoveAll(t => t <= now); if (now >= shieldUntil) shield = 0; }
        public int Charges(double now) { Tick(now); return MaxCharges - recharge.Count; }
        // Logical charge index is zero based in the HUD: available charges precede the charging segment.
        public int RechargingIndex(double now) { Tick(now); return recharge.Count == 0 ? -1 : MaxCharges - recharge.Count; }
        public double RechargeProgress(double now) { Tick(now); return recharge.Count == 0 ? 1 : Math.Max(0, Math.Min(1, 1 - (recharge[0] - now) / CooldownSeconds)); }
        public double NextChargeAt(double now) { Tick(now); return recharge.Count == 0 ? double.PositiveInfinity : recharge[0]; }
        public double NextAvailableAt(double now) { Tick(now); return Math.Max(Id == "tactical.dash" ? lastDash + RogueCatalog.DashMinIntervalSeconds : now, recharge.Count < MaxCharges ? now : recharge[0]); }
        // The adapter validates geometry/action permission before committing. A rejected action spends nothing.
        public bool TryUse(double now, bool actionAllowed) { Tick(now); return actionAllowed && TryUse(now); }
        public bool TryUse(double now) { return TryUse(now, () => true); }
        /// <summary>Call the synchronous action only when a charge is available. False/throw spends nothing;
        /// true means movement actually started, then exactly one charge is committed. No reentrant use.</summary>
        public bool TryUse(double now, Func<bool> tryStartAction)
        {
            if (tryStartAction == null) throw new ArgumentNullException("tryStartAction");
            if (committing) return false;
            Tick(now); if (Id == "tactical.doublejump" || recharge.Count >= MaxCharges) return false;
            if (Id == "tactical.dash" && now < lastDash + RogueCatalog.DashMinIntervalSeconds) return false;
            committing = true;
            bool started;
            try { started = tryStartAction(); } finally { committing = false; }
            if (!started) return false;
            recharge.Add((recharge.Count == 0 ? now : recharge[recharge.Count - 1]) + CooldownSeconds);
            if (Id == "tactical.dash") lastDash = now;
            if (Id == "tactical.shield") { shield = ShieldPoints; shieldUntil = now + ShieldSeconds; }
            return true;
        }
        // 適配器每幀先 Tick(now)，或直接使用帶 now 的 overload，過期護盾不再吸收。
        public double Absorb(double damage) { RogueStateBag.NonNegative(damage); double absorbed = Math.Min(shield, damage); shield -= absorbed; return damage - absorbed; }
        public double Absorb(double damage, double now) { Tick(now); return Absorb(damage); }
        public double ShieldRemaining { get { return shield; } }
        /// <summary>A jump press: on the ground it resets the count; in the air it is a jump while one of MaxAirJumps is left.</summary>
        public bool OnJumpPressed(bool grounded) { if (grounded) { airJumpsUsed = 0; return false; } if (Id != "tactical.doublejump" || airJumpsUsed >= MaxAirJumps) return false; airJumpsUsed++; return true; }
        public int AirJumpsLeft { get { return Math.Max(0, MaxAirJumps - airJumpsUsed); } }
        public void Reset() { airJumpsUsed = 0; shield = 0; shieldUntil = 0; }
        public void Cancel() { Reset(); }
    }

    /// <summary>One instance per player/build; use the root trigger id for pellets, ricochets and penetrations.</summary>
    public sealed class SuppressionTracker
    {
        public const double DecayIntervalSeconds = .5;
        public double WindowSeconds { get; private set; }
        public int MaxStacks { get; private set; }
        private readonly HashSet<string> shots = new HashSet<string>(StringComparer.Ordinal);
        private int stacks;
        private double nextDecay = double.PositiveInfinity, observedNow;

        public SuppressionTracker(BuildStats stats = null)
        {
            stats = stats ?? BuildStats.Compute(new PlayerBuild { cores = new[] { "core.suppression" } });
            WindowSeconds = RogueStateBag.Positive(stats.SuppressionWindowSeconds);
            MaxStacks = stats.SuppressionStepMax <= 0 ? 0 : (int)Math.Ceiling(stats.SuppressionStepMax / RogueStateBag.Positive(stats.SuppressionStep));
        }

        public bool OnTriggerHit(double now, string shotId)
        {
            Stacks(now);
            if (string.IsNullOrEmpty(shotId)) throw new ArgumentException("shotId");
            if (shots.Count > 512) shots.Clear();   // ids are unique per trigger pull; a long fight must not grow the set forever
            if (!shots.Add(shotId) || MaxStacks == 0) return false;
            stacks = Math.Min(MaxStacks, stacks + 1);
            nextDecay = now + WindowSeconds + DecayIntervalSeconds;
            return true;
        }

        public int Stacks(double now)
        {
            RogueStateBag.NonNegative(now);
            if (now < observedNow) throw new ArgumentOutOfRangeException("now", "時間不可倒退");
            observedNow = now;
            if (stacks > 0 && now >= nextDecay)
            {
                int lost = (int)Math.Min(stacks, 1 + Math.Floor((now - nextDecay) / DecayIntervalSeconds));
                stacks -= lost; nextDecay += lost * DecayIntervalSeconds;
            }
            return stacks;
        }

        public void OnReload() { stacks /= 2; }
        public void OnReload(double now) { Stacks(now); OnReload(); }
        public void Clear() { shots.Clear(); stacks = 0; nextDecay = double.PositiveInfinity; observedNow = 0; }
    }

    /// <summary>Non-regenerating purchased shield. Apply temporary shields first, then pass their remaining damage here.</summary>
    public sealed class OverShield
    {
        public double Remaining { get; private set; }
        public void Grant(double maxHp) { Remaining = RogueStateBag.Positive(maxHp); }
        public double Absorb(double damage)
        {
            RogueStateBag.NonNegative(damage);
            double absorbed = Math.Min(Remaining, damage); Remaining -= absorbed;
            return damage - absorbed;
        }
        public void Clear() { Remaining = 0; }
        public double Fraction(double maxHp) { return Math.Min(1, Remaining / RogueStateBag.Positive(maxHp)); }
        /// <summary>Reconstruct from the authoritative fraction at a stage boundary, including changed maximum health.</summary>
        public void Restore(double maxHp, double fraction) { RogueStateBag.Positive(maxHp); RogueStateBag.Unit(fraction); Remaining = maxHp * fraction; }
    }

    public enum DamageKind { Direct, Chain, Homing, Explosion, Ricochet, Penetrate }
    public struct DamageContext
    {
        public string sourceKey, rootShotId;
        public DamageKind kind;
        public int depth;
        private bool requestedHeadshot;
        public bool headshot { get { return kind == DamageKind.Direct && depth == 0 && requestedHeadshot; } set { requestedHeadshot = value; } }
        public DamageContext(string sourceKey, string rootShotId, DamageKind kind = DamageKind.Direct, int depth = 0, bool headshot = false)
        { this.sourceKey = sourceKey; this.rootShotId = rootShotId; this.kind = kind; this.depth = depth; this.requestedHeadshot = kind == DamageKind.Direct && depth == 0 && headshot; }
        public DamageContext Derived(DamageKind next) { return new DamageContext(sourceKey, rootShotId, next, depth + 1, false); }
    }
    public struct ChainCandidate
    {
        public string Id;
        public double Distance;
        public bool Visible;
        public ChainCandidate(string id, double distance, bool visible) { Id = id; Distance = distance; Visible = visible; }
    }
    public sealed class EffectChainRules
    {
        // MaxDepth follows the most pierces / bounces a build can reach (RogueCatalog.MaxPierce / MaxBounces: core tier 7+ gives three, the mod one more)
        public const int MaxDepth = 4, MaxChainInFlightPerPlayer = 16, MaxExplosionsPerSecond = 4;
        public const int MaxHomingInFlightPerPlayer = 32, MaxRicochetInFlightPerPlayer = 32, MaxPenetrateInFlightPerPlayer = 32;
        /// <summary>Chain Bullets (2026-10-04): the first arcs carry the full damage of the hit; every further jump keeps RogueCatalog.ChainJumpFalloff.</summary>
        public const double ChainDamageFraction = 1;
        /// <summary>Performance bound of Chain Bullets: chained hits (damage and one arc effect each) one player may cause in any second.
        /// A fast weapon in a crowd reaches it; the hits beyond it are simply not chained. A performance and network bound (each
        /// chained hit is one hit report to the host in co-op), not a balance value.</summary>
        public const int MaxChainHitsPerSecond = 40;
        private readonly int penetrate, ricochet;
        private readonly Dictionary<string, HashSet<string>> hits = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> inFlight = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, Queue<double>> explosions = new Dictionary<string, Queue<double>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Queue<double>> chainHits = new Dictionary<string, Queue<double>>(StringComparer.Ordinal);
        public EffectChainRules(BuildStats stats = null) { stats = stats ?? new BuildStats(); penetrate = Math.Max(0, Math.Min(RogueCatalog.MaxPierce, stats.PenetrateDepth)); ricochet = Math.Max(0, Math.Min(RogueCatalog.MaxBounces, stats.RicochetBounces)); }

        /// <summary>Damage share of a chained hit at <paramref name="jump"/> (1 = the arcs leaving the enemy the bullet hit).</summary>
        public static double ChainJumpFraction(int jump) { return ChainDamageFraction * Math.Pow(RogueCatalog.ChainJumpFalloff, Math.Max(0, jump - 1)); }

        /// <summary>Takes one chained hit from the player's per-second budget; false when the budget of this second is spent.</summary>
        public bool TryTakeChainHit(string sourceKey, double now)
        {
            RogueStateBag.NonNegative(now); if (string.IsNullOrEmpty(sourceKey)) throw new ArgumentException("sourceKey");
            Queue<double> times; if (!chainHits.TryGetValue(sourceKey, out times)) chainHits[sourceKey] = times = new Queue<double>();
            while (times.Count > 0 && (times.Peek() <= now - 1 || times.Peek() > now)) times.Dequeue();   // older than a second, or from a clock that restarted
            if (times.Count >= MaxChainHitsPerSecond) return false; times.Enqueue(now); return true;
        }
        // kind/depth 描述「預計產生」的傷害。source overload 再套用來源不可自我連鎖規則。
        public bool CanTrigger(DamageKind kind, int depth)
        {
            if (depth < 0 || depth > MaxDepth) return false;
            switch (kind) { case DamageKind.Direct: return depth == 0; case DamageKind.Chain: return depth == 1; case DamageKind.Penetrate: return depth >= 1 && depth <= penetrate; case DamageKind.Ricochet: return depth >= 1 && depth <= ricochet; case DamageKind.Homing: case DamageKind.Explosion: return depth >= 1; default: return false; }
        }
        public bool CanTrigger(DamageContext source, DamageKind next)
        {
            if (!CanTrigger(source.kind, source.depth) || next == DamageKind.Direct) return false;
            if (source.kind == DamageKind.Chain && next == DamageKind.Chain || source.kind == DamageKind.Explosion && next == DamageKind.Explosion) return false;
            return CanTrigger(next, source.depth + 1);
        }
        public static string[] ChainTargets(IEnumerable<ChainCandidate> candidates, ISet<string> alreadyHit = null, int maxTargets = RogueCatalog.ChainMaxTargets, double maxDistance = RogueCatalog.ChainRange)
        {
            if (candidates == null) throw new ArgumentNullException("candidates"); RogueStateBag.NonNegative(maxDistance);
            return candidates.Where(c => c.Visible && !string.IsNullOrEmpty(c.Id) && !double.IsNaN(c.Distance) && c.Distance >= 0 && c.Distance <= maxDistance && (alreadyHit == null || !alreadyHit.Contains(c.Id))).OrderBy(c => c.Distance).ThenBy(c => c.Id, StringComparer.Ordinal).Select(c => c.Id).Distinct().Take(Math.Max(0, Math.Min(RogueCatalog.ChainTargetsCap, maxTargets))).ToArray();   // ChainTargetsCap: the base targets plus the most a mod adds
        }
        public static bool HomingSteer(double currentDirDot, double maxAngleDeg = RogueCatalog.HomingAngleDegrees) { if (double.IsNaN(currentDirDot) || double.IsInfinity(currentDirDot) || currentDirDot < -1 || currentDirDot > 1 || double.IsNaN(maxAngleDeg) || maxAngleDeg < 0 || maxAngleDeg > 180) return false; return currentDirDot + 1e-12 >= Math.Cos(maxAngleDeg * Math.PI / 180); }
        private static string ShotKey(DamageContext c) { if (string.IsNullOrEmpty(c.sourceKey) || string.IsNullOrEmpty(c.rootShotId)) throw new ArgumentException("source/rootShotId"); return c.sourceKey.Length + ":" + c.sourceKey + c.rootShotId; }
        public bool TryRegisterDerivedHit(DamageContext context, string targetId)
        {
            if (context.kind == DamageKind.Direct || !CanTrigger(context.kind, context.depth) || string.IsNullOrEmpty(targetId)) return false;
            if (hits.Count > 256) hits.Clear();   // one entry per round that chained; nothing releases them, so a long fight must not grow the table forever
            string key = ShotKey(context); HashSet<string> targets; if (!hits.TryGetValue(key, out targets)) hits[key] = targets = new HashSet<string>(StringComparer.Ordinal);
            return targets.Add(targetId);
        }
        public void ReleaseShot(string sourceKey, string rootShotId) { hits.Remove(ShotKey(new DamageContext(sourceKey, rootShotId))); }
        public bool TryStartChain(string sourceKey) { if (string.IsNullOrEmpty(sourceKey)) throw new ArgumentException("sourceKey"); int count; inFlight.TryGetValue(sourceKey, out count); if (count >= MaxChainInFlightPerPlayer) return false; inFlight[sourceKey] = count + 1; return true; }
        public void FinishChain(string sourceKey) { int count; if (inFlight.TryGetValue(sourceKey, out count)) inFlight[sourceKey] = Math.Max(0, count - 1); }
        public bool TryStartExplosion(string sourceKey, double now)
        {
            RogueStateBag.NonNegative(now); if (string.IsNullOrEmpty(sourceKey)) throw new ArgumentException("sourceKey");
            Queue<double> times; if (!explosions.TryGetValue(sourceKey, out times)) explosions[sourceKey] = times = new Queue<double>();
            if (times.Count > 0 && now < times.Last()) throw new ArgumentOutOfRangeException("now");
            while (times.Count > 0 && times.Peek() <= now - 1) times.Dequeue();
            if (times.Count >= MaxExplosionsPerSecond) return false; times.Enqueue(now); return true;
        }
        public void Clear() { hits.Clear(); inFlight.Clear(); explosions.Clear(); chainHits.Clear(); }
    }
    public static class TriggerCoefficients
    {
        public const double ShotgunPellet = .35, PenetrateSecondTarget = .6, Ricochet = .8, RubberRicochet = 1, Explosion = .4, Chain = EffectChainRules.ChainDamageFraction;
        public static double For(DamageKind kind, bool shotgunPellet = false, bool rubberRicochet = false) { double coefficient; switch (kind) { case DamageKind.Penetrate: coefficient = PenetrateSecondTarget; break; case DamageKind.Ricochet: coefficient = rubberRicochet ? RubberRicochet : Ricochet; break; case DamageKind.Explosion: coefficient = Explosion; break; case DamageKind.Chain: coefficient = Chain; break; default: coefficient = 1; break; } return coefficient * (shotgunPellet ? ShotgunPellet : 1); }
    }
}
