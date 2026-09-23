using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Preserve the original depth-ordered camera composition, including Supershot's
// runtime exchange of the world camera. Scope render textures stay independent.
public static class FlatsCameraStack
{
    static readonly List<Camera> ordered = new List<Camera>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        RenderPipelineManager.beginContextRendering -= Prepare;
        RenderPipelineManager.beginContextRendering += Prepare;
    }

    static void Prepare(ScriptableRenderContext context, List<Camera> cameras)
    {
        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) return;
        ordered.Clear();
        foreach (var camera in cameras)
            if (camera != null && camera.cameraType == CameraType.Game && camera.isActiveAndEnabled)
                ordered.Add(camera);
        ordered.Sort((a, b) => a.depth.CompareTo(b.depth));
        foreach (var camera in ordered)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Base;
            data.cameraStack.Clear();
            data.renderPostProcessing = false; // FLATS renderer feature owns the original effects.
            data.antialiasing = AntialiasingMode.None;
        }
        for (int i = 0; i < ordered.Count; ++i)
        {
            var camera = ordered[i];
            if (camera.targetTexture != null ||
                (camera.clearFlags != CameraClearFlags.Depth && camera.clearFlags != CameraClearFlags.Nothing)) continue;
            Camera world = null;
            // A later cinematic base clears the former main camera's output.
            // Never replay that earlier world camera over the cinematic view.
            foreach (var candidate in ordered)
                if (candidate.targetTexture == null && candidate.targetDisplay == camera.targetDisplay &&
                    (candidate.clearFlags == CameraClearFlags.Skybox || candidate.clearFlags == CameraClearFlags.SolidColor))
                {
                    if (candidate.depth <= camera.depth) world = candidate;
                }
            if (world == null) continue;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Overlay;
            // URP 17.3 clearDepth is read-only; its serialized default is true.
            // All authored overlays use Depth. URP's AdditionalCameraData.Start
            // changes their Camera.clearFlags to Nothing on the following frame;
            // continue treating those cameras as overlays instead of yellow-clearing bases.
            world.GetUniversalAdditionalCameraData().cameraStack.Add(camera);
        }
    }
}
