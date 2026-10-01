using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    /// <summary>One token per fired round, shared by all its pellets. Only direct hits call Headshot.</summary>
    public sealed class FreshRound
    {
        public bool Eligible { get; private set; }
        private bool hit;
        internal FreshRound(bool eligible) { Eligible = eligible; }
        public bool Headshot(bool naturalHeadshot)
        {
            bool force = Eligible && !hit;
            hit = true;
            return naturalHeadshot || force;
        }
    }

    /// <summary>
    /// Full reload completion arms that weapon's next actually fired round. Initial spawn, swaps,
    /// cancelled/partial reloads and ammo refunds never arm it. Firing consumes it even on a miss.
    /// The first direct pellet hit may be converted; all remaining pellets use their natural hit.
    /// Adapter supplies increasing per-weapon reload/round sequences; duplicate callbacks are safe.
    /// Keep this instance across Bind/snapshot updates; Clear only on run end or skill removal.
    /// </summary>
    public sealed class FreshMagazineRuntime
    {
        sealed class WeaponState
        {
            public bool armed;
            public long reload = -1, round = -1;
            public FreshRound token;
        }
        readonly Dictionary<string, WeaponState> weapons = new Dictionary<string, WeaponState>();
        WeaponState Get(string weapon)
        {
            if (string.IsNullOrEmpty(weapon)) throw new ArgumentException("weapon");
            WeaponState state;
            if (!weapons.TryGetValue(weapon, out state)) weapons.Add(weapon, state = new WeaponState());
            return state;
        }
        public void OnReloadCompleted(string weapon, long reloadSequence, int rounds, int capacity)
        {
            var s = Get(weapon);
            if (reloadSequence < 0) throw new ArgumentOutOfRangeException("reloadSequence");
            if (reloadSequence <= s.reload) return;
            s.reload = reloadSequence;
            if (capacity > 0 && rounds >= capacity) s.armed = true;
        }
        public FreshRound OnFired(string weapon, long roundSequence)
        {
            var s = Get(weapon);
            if (roundSequence < 0 || roundSequence < s.round) throw new ArgumentOutOfRangeException("roundSequence");
            if (roundSequence == s.round) return s.token;
            s.round = roundSequence;
            s.token = new FreshRound(s.armed);
            s.armed = false;
            return s.token;
        }
        public void Clear() { weapons.Clear(); }
    }

    /// <summary>Revive shield is replaced/refilled to 500 for 3 seconds, never added to itself.</summary>
    public sealed class RescueShieldRuntime
    {
        double remaining, until, observed;
        void Tick(double now)
        {
            RogueStateBag.NonNegative(now);
            if (now < observed) throw new ArgumentOutOfRangeException("now");
            observed = now;
            if (now >= until) remaining = 0;
        }
        public void Grant(BuildStats stats, double now)
        {
            Tick(now);
            remaining = Math.Max(0, stats.RescueShieldPoints);
            until = now + Math.Max(0, stats.RescueShieldSeconds);
        }
        public double Remaining(double now) { Tick(now); return remaining; }
        public double Absorb(double damage, double now)
        {
            Tick(now); RogueStateBag.NonNegative(damage);
            double absorbed = Math.Min(damage, remaining); remaining -= absorbed;
            return damage - absorbed;
        }
        public void Clear() { remaining = 0; until = 0; }
    }
}
