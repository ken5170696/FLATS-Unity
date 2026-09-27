using System;
using System.IO;

namespace Flats.Core
{
    // One entry of a scope.view@1 payload: a telescopic sight ("2x", "4x", "6x" or "8x")
    // and how many times larger its lens image is drawn while aiming (1 = authored size).
    [Serializable] public sealed class ScopeViewLens
    {
        public string lens;
        public float scale = 1;
    }

    // scope.view@1 payload. It changes only how large the local player's lens image is on
    // their own screen; the aim ray, damage and what other players see are unchanged, so
    // the adapter is ClientOnly. Missing lenses keep the authored size.
    [Serializable] public sealed class ScopeViewPayload
    {
        public int schema = 1;
        // true: the sight camera's field of view widens with the lens image so 8x still
        // magnifies 8 times. false: only the image grows, so magnification grows with it.
        public bool preserveMagnification = true;
        // Integer multiple of the authored 256x256 sight render texture (1-4).
        public int renderTextureScale = 1;
        public ScopeViewLens[] lenses;

        public const float MinimumScale = 1f, MaximumScale = 3f;
        public const int MinimumTextureScale = 1, MaximumTextureScale = 4;
        public static readonly string[] Lenses = { "2x", "4x", "6x", "8x" };

        public void Validate()
        {
            if (schema != 1) throw new InvalidDataException("Unsupported scope view schema (expected 1)");
            if (renderTextureScale < MinimumTextureScale || renderTextureScale > MaximumTextureScale)
                throw new InvalidDataException("renderTextureScale must be an integer between " + MinimumTextureScale + " and " + MaximumTextureScale);
            if (lenses == null) return;
            for (int i = 0; i < lenses.Length; i++)
            {
                var entry = lenses[i];
                if (entry == null) throw new InvalidDataException("Scope view lens entry is empty");
                if (Array.IndexOf(Lenses, entry.lens) < 0) throw new InvalidDataException("Unknown scope view lens: " + entry.lens + " (expected 2x, 4x, 6x or 8x)");
                for (int j = 0; j < i; j++) if (lenses[j] != null && lenses[j].lens == entry.lens) throw new InvalidDataException("Duplicate scope view lens: " + entry.lens);
                if (float.IsNaN(entry.scale) || float.IsInfinity(entry.scale) || entry.scale < MinimumScale || entry.scale > MaximumScale)
                    throw new InvalidDataException("Scope view " + entry.lens + " scale must be between " + MinimumScale + " and " + MaximumScale);
            }
        }

        public float Scale(string lens)
        {
            if (lenses == null) return 1;
            foreach (var entry in lenses) if (entry != null && entry.lens == lens) return entry.scale;
            return 1;
        }
    }

    // Lens image enlargement for the local player's telescopic sights. Everything stays at
    // the authored size unless a scope.view module is active.
    public static class ScopeView
    {
        public const string Adapter = "scope.view@1";
        public static bool Active { get; private set; }
        public static bool PreserveMagnification { get; private set; } = true;
        public static int RenderTextureScale { get; private set; } = 1;
        static ScopeViewPayload payload;

        // 1 for the reflex sight, unknown lenses and while no module is active.
        public static float Scale(string lens) { return Active ? payload.Scale(lens) : 1; }

        public static void Apply(ScopeViewPayload value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            value.Validate();
            if (Active) throw new InvalidOperationException("Another scope view module is already active");
            payload = value; PreserveMagnification = value.preserveMagnification; RenderTextureScale = value.renderTextureScale; Active = true;
        }

        public static void Reset() { payload = null; PreserveMagnification = true; RenderTextureScale = 1; Active = false; }

        // Largest usable scale for a lens image whose diameter is `baseFraction` of the viewport
        // height at scale 1: the enlarged image must stay within `limit` of the shorter viewport
        // side (aspect = width / height), so a phone held upright clamps harder than a monitor.
        // Never returns less than 1: the authored size is always allowed.
        public static float Fit(float requested, float baseFraction, float aspect, float limit)
        {
            if (float.IsNaN(requested) || requested < ScopeViewPayload.MinimumScale) requested = ScopeViewPayload.MinimumScale;
            if (requested > ScopeViewPayload.MaximumScale) requested = ScopeViewPayload.MaximumScale;
            if (!(baseFraction > 0) || !(aspect > 0) || !(limit > 0)) return requested;
            float shorter = aspect < 1 ? aspect : 1;
            float maximum = limit * shorter / baseFraction;
            if (requested > maximum) requested = maximum;
            return requested < ScopeViewPayload.MinimumScale ? ScopeViewPayload.MinimumScale : requested;
        }

        // Vertical field of view (degrees) that keeps the same world angle per screen pixel when
        // the image is drawn `scale` times larger: tan(f'/2) = scale * tan(f/2).
        public static float WidenFieldOfView(float fieldOfView, float scale)
        {
            if (!(scale > 0)) return fieldOfView;
            double half = fieldOfView * Math.PI / 360.0;
            return (float)(Math.Atan(scale * Math.Tan(half)) * 360.0 / Math.PI);
        }
    }
}
