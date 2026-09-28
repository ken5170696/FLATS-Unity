using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Flats.Rendering
{
    // Per-camera decisions FlatsCameraStack makes before each context render. The
    // defaults reproduce the original game: no URP post-processing and no URP
    // anti-aliasing on any game camera, and the authored volume layer mask untouched.
    public struct CameraRenderSettings
    {
        public bool renderPostProcessing;
        public AntialiasingMode antialiasing;
        // Null keeps the camera's serialized volume layer mask.
        public LayerMask? volumeLayerMask;
        public static CameraRenderSettings Original => new CameraRenderSettings
        {
            renderPostProcessing = false, antialiasing = AntialiasingMode.None, volumeLayerMask = null,
        };
    }

    // Mod API 1.2.0 extension point. A registered policy is asked about every enabled
    // Game camera on the screen or a render texture; it may keep the defaults, or opt a
    // camera into URP post-processing and anti-aliasing. It must not touch scene state.
    public interface IRenderPolicy
    {
        void Configure(Camera camera, UniversalAdditionalCameraData data, ref CameraRenderSettings settings);
    }

    // At most one policy is active. Registration returns the handle that removes it,
    // so a module can hand that handle to ModuleLifetime.Own before changing anything.
    public static class RenderPolicy
    {
        static IRenderPolicy current;
        public static IRenderPolicy Current => current;
        public static bool Registered => current != null;

        public static IDisposable Register(IRenderPolicy policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (current != null && !ReferenceEquals(current, policy)) throw new InvalidOperationException("Another render policy is already registered");
            current = policy;
            return new Handle(policy);
        }

        public static void Unregister(IRenderPolicy policy)
        {
            if (policy != null && ReferenceEquals(current, policy)) current = null;
        }

        // Used by FlatsCameraStack. A throwing policy falls back to the original
        // settings for that camera so a faulty module cannot blank the screen.
        internal static CameraRenderSettings Resolve(Camera camera, UniversalAdditionalCameraData data)
        {
            var settings = CameraRenderSettings.Original;
            var policy = current;
            if (policy == null) return settings;
            try { policy.Configure(camera, data, ref settings); }
            catch (Exception error)
            {
                Debug.LogException(error);
                settings = CameraRenderSettings.Original;
            }
            return settings;
        }

        sealed class Handle : IDisposable
        {
            IRenderPolicy policy;
            public Handle(IRenderPolicy p) { policy = p; }
            public void Dispose() { Unregister(policy); policy = null; }
        }
    }
}
