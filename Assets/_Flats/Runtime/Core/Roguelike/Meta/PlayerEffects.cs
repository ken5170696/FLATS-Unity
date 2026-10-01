using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    /// <summary>How an effect reaches the HUD effect row.</summary>
    public enum PlayerEffectKind
    {
        Instant,   // a notice that something just happened (a refund, a mark, an elite's harder hit): shown briefly
        Timed,     // a pulse that lasts a known time: shown with a draining ring
        State,     // reported every frame by its owner while it is true (stacks, zones, windows): shown while reported
    }

    /// <summary>One effect the local player can have: short label (a FlatsChinese key), icon (RogueIconSet name) and look.</summary>
    public sealed class PlayerEffectDef
    {
        public readonly string Id, Label, Icon;
        public readonly PlayerEffectKind Kind;
        /// <summary>Debuffs (red frame and plate) come first when the row is full.</summary>
        public readonly bool Negative;
        /// <summary>A state with a known end (a window or a timer): it keeps its place ahead of open-ended conditions.</summary>
        public readonly bool Clock;
        /// <summary>A short cue when the chip appears: triggers the player caused, never conditions that toggle while moving or firing.</summary>
        public readonly bool Cue;

        public PlayerEffectDef(string id, string label, string icon, PlayerEffectKind kind, bool negative, bool clock, bool cue)
        {
            Id = id; Label = label; Icon = icon; Kind = kind; Negative = negative; Clock = clock; Cue = cue;
        }

        /// <summary>Lower shows first when more effects are active than the row holds: debuffs, clocked effects, open conditions, notices.</summary>
        public int Rank { get { return Negative ? 0 : Kind == PlayerEffectKind.Timed || (Kind == PlayerEffectKind.State && Clock) ? 1 : Kind == PlayerEffectKind.State ? 2 : 3; } }
    }

    /// <summary>
    /// QA-51: the table of player effects the HUD effect row shows, and the numbers it prints. The runtime (RogueMetaRuntime)
    /// reports states from the same rule fields the effects use; these helpers mirror the rule formulas so the rule tests can
    /// check that the row shows exactly what the next round or hit will get.
    /// </summary>
    public static class PlayerEffects
    {
        static PlayerEffectDef Buff(string id, string label, string icon, PlayerEffectKind kind, bool clock, bool cue)
        { return new PlayerEffectDef(id, label ?? SkillName(id), icon, kind, false, clock, cue); }
        static PlayerEffectDef Debuff(string id, string label, string icon, PlayerEffectKind kind, bool clock)
        { return new PlayerEffectDef(id, label, icon, kind, true, clock, false); }
        static string SkillName(string id) { var n = SkillTree.Node(id); return n != null ? n.Name : id; }

        const PlayerEffectKind I = PlayerEffectKind.Instant, T = PlayerEffectKind.Timed, S = PlayerEffectKind.State;

        /// <summary>Every effect id the row knows. Icons are unique across the table (one concept, one picture).</summary>
        public static readonly PlayerEffectDef[] All =
        {
            // weapon trait of the gun in hand
            Buff("wt.kill_frenzy", "Kill Frenzy", "Flame", S, true, true),        // after a kill, until the magazine is reloaded (ring: rounds left)
            Buff("wt.follow_up", "Follow-Up", "Arrow", S, false, false),          // after a headshot, the next trigger pull cycles faster
            Buff("wt.spin_up", "Spin-Up", "Experience", S, false, false),         // damage per consecutive round while the burst lasts
            Buff("wt.patient_shot", "Patient Shot", "Timer", S, false, false),    // aimed and still: the next shot hits harder
            Buff("wt.last_round", "Last Round", "Target", S, false, false),       // the last round in the magazine
            Buff("wt.refund", "Rounds Back", "Plus", I, false, true),             // AmmoOnKill / HeadshotRefund put rounds back
            // run cores
            Buff("rk.assault_rush", "Assault Rush", "Run", S, true, true),        // Assault: a close kill cuts damage taken and speeds you up
            Buff("rk.reload_burst", "Reload Burst", "Battery", S, true, true),    // Reload Burst: damage after a deep reload
            Buff("rk.suppression", "Suppression", "Crosshair", S, false, false),  // Suppression: stacks from hits, decaying
            Buff("rk.momentum", "Momentum", "Dash", S, true, false),              // Mobility: the first shot after a dash or a landing
            // out-of-run skills
            Buff("sk.berserker", null, "Skull", S, true, true),
            Buff("sk.rhythm", null, "Music", S, true, true),
            Buff("sk.shredder", null, "Saw", S, false, false),
            Buff("sk.hold_line", null, "Flag", S, false, false),
            Buff("sk.juggernaut", null, "Singleplayer3", S, false, false),
            Buff("sk.steady_breath", null, "Wind", S, false, false),
            Buff("sk.adrenal", null, "Bolt", S, true, true),
            Buff("sk.squad_link", null, "Link", S, true, true),
            Buff("sk.rescue_shield", null, "Shield", S, true, true),
            Buff("sk.guardian", null, "Star", I, false, true),                   // the HUD's invulnerability badge carries its timer
            Buff("sk.spotter_eye", null, "Eye", T, true, true),
            Buff("sk.fresh_mag", null, "Reload", I, false, true),
            Buff("sk.kill_reload", null, "Ammo", I, false, true),
            Buff("sk.scavenger", null, "Crate", I, false, true),
            Buff("sk.brawler", null, "Fist", I, false, true),
            Buff("sk.endless_belt", null, "Infinity", I, false, true),
            Buff("sk.second_wind", null, "Heart", I, false, true),
            // world
            Buff("env.low_gravity", "Low Gravity", "Wings", S, false, false),     // inside a low gravity zone (the zone's waypoint uses the same icon)
            // debuffs
            Debuff("af.suppressor", "Slowed", "Snow", S, true),                    // an elite Suppressor's hit
            Debuff("af.opening", "Ambush Hit", "Enemy", I, false),                 // an Ambusher hit harder (you were at full health)
            Debuff("af.marksman", "Sniped", "Zoom", I, false),                     // a Deadeye hit harder from range
            Debuff("af.berserker", "Enraged Hit", "Fire", I, false),               // an enraged elite hit harder
            Debuff("env.gas", "In Gas", "Warning", I, false),                      // refreshed by every gas damage tick
            Debuff("env.jammed", "Ultimate Jammed", "Lock", S, false),             // a live jammer blocks your ultimate charge
        };

        static Dictionary<string, PlayerEffectDef> byId;
        public static PlayerEffectDef Def(string id)
        {
            if (byId == null) { var d = new Dictionary<string, PlayerEffectDef>(StringComparer.Ordinal); foreach (var e in All) d[e.Id] = e; byId = d; }
            PlayerEffectDef found; return id != null && byId.TryGetValue(id, out found) ? found : null;
        }

        // ------------------------------------------------------------------ texts (cached: the row asks every frame)
        const int PercentMin = -100, PercentMax = 500, CountMax = 9999;
        static readonly string[] percents = new string[PercentMax - PercentMin + 1], times = new string[100], counts = new string[CountMax + 1];

        /// <summary>"+24%", "0%", "-20%" (clamped to -100..500).</summary>
        public static string Percent(int percent)
        {
            percent = Math.Max(PercentMin, Math.Min(PercentMax, percent));
            int i = percent - PercentMin;
            return percents[i] ?? (percents[i] = (percent > 0 ? "+" : "") + percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%");
        }

        /// <summary>A fraction as a signed whole percent: 0.24 is "+24%", -0.2 is "-20%".</summary>
        public static string PercentOf(double fraction) { return Percent((int)Math.Round(fraction * 100.0, MidpointRounding.AwayFromZero)); }

        /// <summary>"x3" (ASCII x: the HUD font has no multiplication sign). Clamped to 0..99.</summary>
        public static string Times(int n)
        {
            n = Math.Max(0, Math.Min(99, n));
            return times[n] ?? (times[n] = "x" + n.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>A whole number 0..9999 as text (shield points).</summary>
        public static string Count(int n)
        {
            n = Math.Max(0, Math.Min(CountMax, n));
            return counts[n] ?? (counts[n] = n.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>Ring fill for <paramref name="remaining"/> of <paramref name="length"/> seconds (0..1), or -1 without a length.</summary>
        public static double Fraction(double remaining, double length)
        {
            if (!(length > 0)) return -1;
            return Math.Max(0, Math.Min(1, remaining / length));
        }

        // ------------------------------------------------------------------ rule mirrors (checked against the rules by the tests)
        /// <summary>SpinUpDamage: the bonus the next round of the chain gets (WeaponTraitState.NextRound uses the count before it).</summary>
        public static double SpinUpBonus(RangedWeaponDef d, int consecutive)
        {
            if (d == null || d.Trait != TraitKind.SpinUpDamage || consecutive <= 0) return 0;
            return Math.Min(d.T2, d.T1 * consecutive);
        }

        /// <summary>PatientShot: steps the next aimed trigger pull would get after this long aimed without firing or moving.</summary>
        public static int PatientSteps(RangedWeaponDef d, double aimedStillSeconds)
        {
            if (d == null || d.Trait != TraitKind.PatientShot || !(aimedStillSeconds > 0)) return 0;
            return Math.Min(WeaponTraitState.MaxPatientSteps, (int)Math.Floor(aimedStillSeconds / Math.Max(0.05, d.T2)));
        }

        /// <summary>Suppression core: the damage bonus of <paramref name="stacks"/> stacks (BuildStats.DirectDamage).</summary>
        public static double SuppressionBonus(BuildStats s, int stacks)
        {
            if (s == null || s.SuppressionStepMax <= 0 || stacks <= 0) return 0;
            return Math.Min(s.SuppressionStepMax, s.SuppressionStep * stacks);
        }

        /// <summary>Berserker / Marksman's Rhythm: the bonus of the stacks, before the meta envelope (BuildStats.MetaDamageMul).</summary>
        public static double SkillStackBonus(double step, int stacks)
        {
            if (!(step > 0) || stacks <= 0) return 0;
            return step * Math.Min(BuildStats.MaxSkillStacks, stacks);
        }

        /// <summary>Labels only while the player wants them and few effects are shown; icons carry a busy row.</summary>
        public static bool ShowLabels(bool fullMode, int shown, int limit) { return fullMode && shown <= limit; }
    }

    /// <summary>
    /// What the HUD effect row shows, without Unity: pulses (instant notices and timers) and states reported by their owner,
    /// a capacity with a priority order (debuffs first), a stable order on screen, and a short fade when something ends.
    /// Allocation-free once warm: the row calls it every frame.
    /// </summary>
    public sealed class EffectRowModel
    {
        public sealed class Entry
        {
            public PlayerEffectDef Def { get; internal set; }
            public string Id { get { return Def.Id; } }
            /// <summary>Insertion order: the row shows entries oldest first, and an effect that comes back while fading keeps its place.</summary>
            public long Order { get; internal set; }
            /// <summary>Within the capacity (the row has a chip for it).</summary>
            public bool Shown { get; internal set; }
            /// <summary>"x3", "+24%" or "" (a state's text; kept while it fades).</summary>
            public string Stacks { get; internal set; }
            internal bool state;
            internal double stateFraction = -1, stateEndedAt = double.NegativeInfinity;
            internal double pulseUntil = double.NegativeInfinity, pulseLength;
            internal bool pulseRing;

            /// <summary>Reported now or a pulse still running (not only fading).</summary>
            public bool Active(double now) { return state || pulseUntil > now; }

            /// <summary>Ring fill 0..1, or -1 for no ring. A state keeps its last fill while it fades.</summary>
            public double Fraction(double now)
            {
                if (state || (stateFraction >= 0 && !(pulseUntil > now))) return stateFraction;
                if (pulseRing && pulseLength > 0 && pulseUntil > now) return Math.Max(0, Math.Min(1, (pulseUntil - now) / pulseLength));
                return -1;
            }

            /// <summary>1 while active; fades over the last <paramref name="fade"/> seconds of a pulse, and for <paramref name="fade"/> seconds after a state ends.</summary>
            public double Alpha(double now, double fade)
            {
                if (state) return 1;
                double a = 0;
                if (pulseUntil > now) a = fade > 0 ? Math.Min(1, (pulseUntil - now) / fade) : 1;
                if (fade > 0 && now - stateEndedAt < fade) a = Math.Max(a, 1 - (now - stateEndedAt) / fade);
                return Math.Max(0, Math.Min(1, a));
            }

            internal bool Alive(double now, double fade) { return state || pulseUntil > now || now - stateEndedAt < fade; }
        }

        readonly Dictionary<string, Entry> byId = new Dictionary<string, Entry>(StringComparer.Ordinal);
        readonly List<Entry> entries = new List<Entry>(), ranked = new List<Entry>();
        readonly Stack<Entry> spare = new Stack<Entry>();
        long order;

        /// <summary>Chips the row has room for.</summary>
        public int Capacity { get; set; }
        /// <summary>Seconds an instant notice stays.</summary>
        public double InstantSeconds { get; set; }
        /// <summary>Seconds of the fade at the end.</summary>
        public double FadeSeconds { get; set; }
        /// <summary>Entries in insertion order (oldest first), shown or not.</summary>
        public IList<Entry> Entries { get { return entries; } }
        public int ShownCount { get; private set; }
        /// <summary>Changes whenever an entry is added, removed or moves in or out of the capacity (the row re-sorts its chips).</summary>
        public int Version { get; private set; }

        public EffectRowModel(int capacity = 5, double instantSeconds = 1.4, double fadeSeconds = 0.3)
        {
            Capacity = Math.Max(1, capacity); InstantSeconds = instantSeconds; FadeSeconds = fadeSeconds;
        }

        public Entry Find(string id) { Entry e; return id != null && byId.TryGetValue(id, out e) ? e : null; }

        /// <summary>A trigger: an instant notice, or a timer of <paramref name="seconds"/> for a Timed effect. States ignore pulses
        /// (their owner reports them). Returns true when the effect was not on the row before (the row plays the cue).</summary>
        public bool Pulse(PlayerEffectDef def, double seconds, double now)
        {
            if (def == null || def.Kind == PlayerEffectKind.State) return false;
            bool timed = def.Kind == PlayerEffectKind.Timed && seconds > 0;
            double length = timed ? seconds : InstantSeconds;
            bool added;
            var e = GetOrAdd(def, out added);
            bool wasOnRow = !added && e.Alive(now, FadeSeconds);   // still fading counts: the chip never left
            e.pulseUntil = now + length; e.pulseLength = length; e.pulseRing = timed;
            if (added) Rank();
            return !wasOnRow;
        }

        /// <summary>A state reported by its owner (every frame, true or false). <paramref name="fraction"/> is the ring (0..1) or
        /// -1 for none; <paramref name="stacks"/> the text in the corner. Returns true when the effect just appeared.</summary>
        public bool SetState(PlayerEffectDef def, bool active, double fraction, string stacks, double now)
        {
            if (def == null) return false;
            Entry e;
            if (!active)
            {
                if (byId.TryGetValue(def.Id, out e) && e.state) { e.state = false; e.stateEndedAt = now; }
                return false;
            }
            bool added;
            e = GetOrAdd(def, out added);
            bool wasOnRow = !added && e.Alive(now, FadeSeconds);
            e.state = true; e.stateEndedAt = double.NegativeInfinity;
            e.stateFraction = fraction >= 0 ? Math.Min(1, fraction) : -1;
            e.Stacks = stacks ?? "";
            if (added) Rank();
            return !wasOnRow;
        }

        /// <summary>Drops what ended and faded. Call once a frame before reading the entries.</summary>
        public void Tick(double now)
        {
            bool removed = false;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                if (e.Alive(now, FadeSeconds)) continue;
                entries.RemoveAt(i); byId.Remove(e.Def.Id); spare.Push(e); removed = true;
            }
            if (removed) Rank();
        }

        /// <summary>Everything ends at once (run end, respec, the player object leaving).</summary>
        public void Clear()
        {
            foreach (var e in entries) spare.Push(e);
            entries.Clear(); byId.Clear(); ShownCount = 0; Version++;
        }

        Entry GetOrAdd(PlayerEffectDef def, out bool added)
        {
            Entry e;
            if (byId.TryGetValue(def.Id, out e)) { added = false; return e; }
            e = spare.Count > 0 ? spare.Pop() : new Entry();
            e.Def = def; e.Order = ++order; e.Shown = false; e.Stacks = "";
            e.state = false; e.stateFraction = -1; e.stateEndedAt = double.NegativeInfinity;
            e.pulseUntil = double.NegativeInfinity; e.pulseLength = 0; e.pulseRing = false;
            entries.Add(e); byId[def.Id] = e;
            added = true;
            return e;
        }

        static bool Before(Entry a, Entry b)
        {
            int ra = a.Def.Rank, rb = b.Def.Rank;
            return ra != rb ? ra < rb : a.Order > b.Order;   // newer first within a rank
        }

        void Rank()
        {
            // insertion sort: a handful of entries, and List.Sort allocates a comparison delegate on every call
            ranked.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                int at = ranked.Count;
                while (at > 0 && Before(e, ranked[at - 1])) at--;
                ranked.Insert(at, e);
            }
            int shown = 0;
            for (int i = 0; i < ranked.Count; i++) { bool s = i < Capacity; ranked[i].Shown = s; if (s) shown++; }
            ShownCount = shown;
            Version++;
        }
    }
}
