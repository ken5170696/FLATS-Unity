using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    /// <summary>How damage numbers are shown (Settings row "DamageNumbers", QA-48).</summary>
    public enum DamageNumberMode { Off = 0, Floating = 1, Stacked = 2 }

    /// <summary>Timing of one on-screen damage number, in unscaled seconds. Every value is a design parameter the view passes in.</summary>
    public struct DamageNumberTiming
    {
        /// <summary>Stacked: a hit this soon after the previous one adds to the same number.</summary>
        public double Window;
        /// <summary>Full opacity after the last hit, and after a killing hit.</summary>
        public double Hold, KillHold;
        /// <summary>Fade-out after the hold.</summary>
        public double Fade;
        /// <summary>Scale right after each add (and after the killing hit), easing back to 1 over PunchSeconds.</summary>
        public double PunchSeconds, PunchScale, KillPunchScale;
        /// <summary>Stacked: the headshot colour and size last this long after a headshot adds to the stack.</summary>
        public double HeadshotFlash;

        public static DamageNumberTiming StackedDefault
        {
            get { return new DamageNumberTiming { Window = .8, Hold = .8, KillHold = 1.0, Fade = .35, PunchSeconds = .12, PunchScale = 1.3, KillPunchScale = 1.45, HeadshotFlash = .35 }; }
        }

        public static DamageNumberTiming FloatingDefault
        {
            get { return new DamageNumberTiming { Window = 0, Hold = .5, KillHold = .7, Fade = .3, PunchSeconds = .1, PunchScale = 1.3, KillPunchScale = 1.45, HeadshotFlash = 10 }; }
        }
    }

    /// <summary>
    /// One number's running state: a stack of hits on one target, or a single floating hit. Pure data; the view owns the clock and
    /// passes "now" in. Instances are pooled by the view, so Start resets every field.
    /// </summary>
    public sealed class DamageNumberState
    {
        public bool Active;
        public double Total;
        public int Hits;
        public bool Killed, AnyHeadshot;
        public double StartedAt, LastAt, KilledAt;
        public double HeadshotAt = double.NegativeInfinity;

        public void Start(double now, double damage, bool headshot, bool kill)
        {
            Active = true; Total = 0; Hits = 0; Killed = false; AnyHeadshot = false;
            StartedAt = now; LastAt = now; KilledAt = 0; HeadshotAt = double.NegativeInfinity;
            Add(now, damage, headshot, kill);
        }

        /// <summary>Adds one settled hit. NaN, infinite and negative damage add nothing but still count as a hit (the stack pops).</summary>
        public void Add(double now, double damage, bool headshot, bool kill)
        {
            if (!Active) { Start(now, damage, headshot, kill); return; }
            if (damage > 0 && !double.IsInfinity(damage)) Total += damage;
            Hits++;
            LastAt = now;
            if (headshot) { AnyHeadshot = true; HeadshotAt = now; }
            if (kill && !Killed) { Killed = true; KilledAt = now; }
        }

        public void Clear() { Active = false; Total = 0; Hits = 0; Killed = false; AnyHeadshot = false; HeadshotAt = double.NegativeInfinity; }

        /// <summary>The whole number shown (rounded, bounded so it always fits the label).</summary>
        public int Shown { get { return DamageNumberRules.Rounded(Total); } }
    }

    /// <summary>
    /// Rules of the combat damage numbers (QA-48, Apex-style stacking): the setting and its migration, stacking and timing, and the
    /// screen placement (outside the crosshair's clear radius, inside a raised sight's lens or the screen). Pure: no engine types.
    /// </summary>
    public static class DamageNumberRules
    {
        /// <summary>Stored values, indexed by DamageNumberMode.</summary>
        public static readonly string[] StoredValues = { "off", "floating", "stacked" };
        public const int MaxShown = 999999;

        // ---------------------------------------------------------------- setting

        /// <summary>
        /// The stored preference as a mode. Missing, "on" (saves from before QA-48, when the setting was on/off) and unknown values are
        /// Stacked, the default; "off" stays Off.
        /// </summary>
        public static DamageNumberMode ParseMode(string stored)
        {
            if (stored == "off") return DamageNumberMode.Off;
            if (stored == "floating") return DamageNumberMode.Floating;
            return DamageNumberMode.Stacked;
        }

        public static string StoredValue(DamageNumberMode mode)
        {
            int i = (int)mode;
            return i >= 0 && i < StoredValues.Length ? StoredValues[i] : StoredValues[(int)DamageNumberMode.Stacked];
        }

        /// <summary>Values a save import may carry: the three current ones and the pre-QA-48 "on".</summary>
        public static bool IsStoredValue(string value)
        {
            return value == "on" || value == "off" || value == "floating" || value == "stacked";
        }

        /// <summary>Next mode for a settings row's plus (direction &gt; 0) or minus (&lt; 0), wrapping around.</summary>
        public static DamageNumberMode Cycle(DamageNumberMode mode, int direction)
        {
            int count = StoredValues.Length, step = direction < 0 ? -1 : 1;
            int i = (int)mode;
            if (i < 0 || i >= count) i = (int)DamageNumberMode.Stacked;
            return (DamageNumberMode)(((i + step) % count + count) % count);
        }

        // ---------------------------------------------------------------- stacking and timing

        public static int Rounded(double total)
        {
            if (double.IsNaN(total) || total <= 0) return 0;
            double r = Math.Round(total, MidpointRounding.AwayFromZero);
            return r >= MaxShown ? MaxShown : (int)r;
        }

        /// <summary>Stacked: the next hit on the same target adds to this number (it is showing, not a kill, and within the window).</summary>
        public static bool Merges(DamageNumberState s, double now, DamageNumberTiming t)
        {
            if (s == null || !s.Active || s.Killed) return false;
            double since = now - s.LastAt;
            return since >= 0 && since <= t.Window && !Expired(s, now, t);
        }

        public static double HoldOf(DamageNumberState s, DamageNumberTiming t) { return s != null && s.Killed ? Math.Max(t.Hold, t.KillHold) : t.Hold; }

        /// <summary>Opacity 0..1: full through the hold after the last hit, then a linear fade.</summary>
        public static double Alpha(DamageNumberState s, double now, DamageNumberTiming t)
        {
            if (s == null || !s.Active) return 0;
            double age = now - s.LastAt, hold = HoldOf(s, t);
            if (age <= hold) return 1;
            if (!(t.Fade > 0)) return 0;
            double a = 1 - (age - hold) / t.Fade;
            return a <= 0 ? 0 : a >= 1 ? 1 : a;
        }

        public static bool Expired(DamageNumberState s, double now, DamageNumberTiming t)
        {
            if (s == null || !s.Active) return true;
            return now - s.LastAt > HoldOf(s, t) + Math.Max(0, t.Fade);
        }

        /// <summary>Scale punch after the latest add (the kill punch is bigger), easing linearly back to 1.</summary>
        public static double Punch(DamageNumberState s, double now, DamageNumberTiming t)
        {
            if (s == null || !s.Active || !(t.PunchSeconds > 0)) return 1;
            double age = now - s.LastAt;
            if (age < 0 || age >= t.PunchSeconds) return 1;
            double peak = s.Killed && s.KilledAt >= s.LastAt ? t.KillPunchScale : t.PunchScale;
            return peak + (1 - peak) * (age / t.PunchSeconds);
        }

        /// <summary>The headshot look is showing: for a while after a headshot added to the stack (a floating headshot keeps it).</summary>
        public static bool HeadshotFlashing(DamageNumberState s, double now, DamageNumberTiming t)
        {
            if (s == null || !s.Active || !s.AnyHeadshot) return false;
            double since = now - s.HeadshotAt;
            return since >= 0 && since <= t.HeadshotFlash;
        }

        /// <summary>Index of the number to reuse when the pool is full: an inactive one, else the one whose last hit is oldest.</summary>
        public static int ReuseIndex(IList<DamageNumberState> states)
        {
            if (states == null || states.Count == 0) return -1;
            int best = 0;
            for (int i = 0; i < states.Count; i++)
            {
                var s = states[i];
                if (s == null || !s.Active) return i;
                var b = states[best];
                // a kill confirmation is kept over an ordinary number of the same age
                if (s.LastAt < b.LastAt || (s.LastAt == b.LastAt && !s.Killed && b.Killed)) best = i;
            }
            return best;
        }

        /// <summary>Floating: how far a number has risen, as a fraction of its full rise (ease-out).</summary>
        public static double Rise(double age, double lifetime)
        {
            if (!(lifetime > 0)) return 1;
            double t = age / lifetime;
            t = t <= 0 ? 0 : t >= 1 ? 1 : t;
            return 1 - (1 - t) * (1 - t);
        }

        // ---------------------------------------------------------------- screen placement (pixels)

        /// <summary>
        /// A point in front of a sight camera, drawn into its lens: x, y, z in the sight's unrolled camera space (z forward),
        /// tanHalfV = tan(vertical fov / 2), aspect = width / height of the sight image, imageRadius = half the lens image height in
        /// pixels. Returns the offset from the lens centre in pixels; false when the point is behind the sight.
        /// </summary>
        public static bool LensOffset(double x, double y, double z, double tanHalfV, double aspect, double imageRadius, out double px, out double py)
        {
            px = py = 0;
            if (!(z > 1e-4) || !(tanHalfV > 0) || !(aspect > 0) || !(imageRadius > 0)) return false;
            px = x / z / (tanHalfV * aspect) * imageRadius;
            py = y / z / tanHalfV * imageRadius;
            return !(double.IsNaN(px) || double.IsNaN(py) || double.IsInfinity(px) || double.IsInfinity(py));
        }

        /// <summary>Moves (x, y) out to at least radius from (cx, cy); a point on the centre goes up and to the right.</summary>
        public static void PushOutside(ref double x, ref double y, double cx, double cy, double radius)
        {
            if (!(radius > 0)) return;
            double dx = x - cx, dy = y - cy, d = Math.Sqrt(dx * dx + dy * dy);
            if (d >= radius) return;
            if (d < 1e-6) { dx = 0.8944271909999159; dy = 0.4472135954999579; d = 1; }
            x = cx + dx / d * radius; y = cy + dy / d * radius;
        }

        /// <summary>Moves (x, y) in to at most radius from (cx, cy).</summary>
        public static void ClampInside(ref double x, ref double y, double cx, double cy, double radius)
        {
            if (!(radius > 0)) { x = cx; y = cy; return; }
            double dx = x - cx, dy = y - cy, d = Math.Sqrt(dx * dx + dy * dy);
            if (d <= radius) return;
            x = cx + dx / d * radius; y = cy + dy / d * radius;
        }

        /// <summary>
        /// A number's centre inside a raised sight's lens: kept clear of the reticle centre by clear (plus the number's own extent) and
        /// fully inside the lens circle. extent: the distance from the number's centre to its farthest corner; margin: extra padding.
        /// When the lens is too small for both, the clear radius gives way (the number stays inside the lens).
        /// </summary>
        public static void PlaceInLens(ref double x, ref double y, double cx, double cy, double lensRadius, double clear, double extent, double margin)
        {
            double outer = lensRadius - extent - Math.Max(0, margin);
            if (outer < 0) outer = 0;
            double inner = Math.Max(0, clear) + extent;
            if (inner > outer * .75) inner = outer * .75;
            PushOutside(ref x, ref y, cx, cy, inner);
            ClampInside(ref x, ref y, cx, cy, outer);
        }

        /// <summary>
        /// A number's centre on the open screen (hip fire or a sight without a lens): clear of the crosshair at (cx, cy), then inside
        /// the screen rectangle less halfWidth/halfHeight plus margin on every side.
        /// </summary>
        public static void PlaceOnScreen(ref double x, ref double y, double width, double height, double cx, double cy, double clear, double halfWidth, double halfHeight, double margin)
        {
            double extent = Math.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight);
            PushOutside(ref x, ref y, cx, cy, Math.Max(0, clear) + extent);
            ClampToRect(ref x, ref y, halfWidth + margin, halfHeight + margin, width - halfWidth - margin, height - halfHeight - margin);
        }

        public static void ClampToRect(ref double x, ref double y, double minX, double minY, double maxX, double maxY)
        {
            x = minX > maxX ? (minX + maxX) * .5 : x < minX ? minX : x > maxX ? maxX : x;
            y = minY > maxY ? (minY + maxY) * .5 : y < minY ? minY : y > maxY ? maxY : y;
        }

        /// <summary>The anchor is close enough to the screen to show a number for it (in front, and within slack of the edges).</summary>
        public static bool NearScreen(double x, double y, double width, double height, double slack)
        {
            return x >= -slack * width && x <= width * (1 + slack) && y >= -slack * height && y <= height * (1 + slack);
        }
    }
}
