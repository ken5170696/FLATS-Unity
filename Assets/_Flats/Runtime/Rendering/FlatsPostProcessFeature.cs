using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Flats.Rendering
{
    // The existing components remain the serialized settings/API. Only this URP
    // feature renders them; no Built-in camera callbacks or replacement cameras run.
    public sealed class FlatsPostProcessFeature : ScriptableRendererFeature
    {
        public Shader shader;
        EffectPass opaquePass, imagePass;
        readonly Dictionary<Camera, CameraState> states = new Dictionary<Camera, CameraState>();
        sealed class CameraState
        {
            public readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();
            public float focusDistance;
            public int lastMotionFrame = int.MinValue;
            public Texture2D curves;
            public int curveHash;
            public readonly Dictionary<long, Material> objectMaterials = new Dictionary<long, Material>();
            public readonly Dictionary<int, Matrix4x4> previousTransforms = new Dictionary<int, Matrix4x4>();
            public Material Get(Shader shader, int key)
            {
                if (!materials.TryGetValue(key, out var material))
                    materials[key] = material = CoreUtils.CreateEngineMaterial(shader);
                return material;
            }
            public void Dispose()
            {
                foreach (var material in materials.Values) CoreUtils.Destroy(material);
                foreach (var material in objectMaterials.Values) CoreUtils.Destroy(material);
                CoreUtils.Destroy(curves);
            }
        }
        public override void Create()
        {
            opaquePass = new EffectPass(this, true) { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
            imagePass = new EffectPass(this, false) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            var camera = data.cameraData.camera;
            if (camera.cameraType != CameraType.Game || shader == null) return;
            var dead = new List<Camera>();
            foreach (var pair in states) if (pair.Key == null) { pair.Value.Dispose(); dead.Add(pair.Key); }
            foreach (var key in dead) states.Remove(key);
            if (!states.ContainsKey(camera)) states[camera] = new CameraState();
            var edge = camera.GetComponent<EdgeDetectEffectNormals>();
            if (Active(edge))
            {
                opaquePass.ConfigureInput(ScriptableRenderPassInput.Depth |
                    ((int)edge.mode < 2 ? ScriptableRenderPassInput.Normal : ScriptableRenderPassInput.None));
                renderer.EnqueuePass(opaquePass);
            }
            var fx = camera.GetComponent<FxPro>();
            var motion = camera.GetComponent<AmplifyMotionEffectBase>();
            var curves = camera.GetComponent<ColorCorrectionCurves>();
            if (Active(fx) || Active(motion) || Active(camera.GetComponent<CC_Grayscale>()) ||
                Active(camera.GetComponent<FXAA>()) || Active(camera.GetComponent<Fisheye>()) || Active(curves) || Active(camera.GetComponent<BlurEffect>()))
            {
                var input = ScriptableRenderPassInput.None;
                if (Active(fx) || Active(motion) || (Active(curves) && curves.useDepthCorrection)) input |= ScriptableRenderPassInput.Depth;
                if (Active(motion)) input |= ScriptableRenderPassInput.Motion;
                imagePass.ConfigureInput(input);
                renderer.EnqueuePass(imagePass);
            }
        }
        static bool Active(Behaviour component) => component != null && component.isActiveAndEnabled;
        protected override void Dispose(bool disposing)
        {
            foreach (var state in states.Values) state.Dispose();
            states.Clear();
        }
        sealed class EffectPass : ScriptableRenderPass
        {
            readonly FlatsPostProcessFeature owner;
            readonly bool opaque;
            static readonly int AuxId = Shader.PropertyToID("_FlatsAuxTex");
            static readonly int CocId = Shader.PropertyToID("_FlatsCoCTex");
            static readonly int IdTexture = Shader.PropertyToID("_FlatsMotionIds");
            static readonly int ObjectId = Shader.PropertyToID("_FlatsMotionId");
            static readonly int AlphaTexture = Shader.PropertyToID("_FlatsMotionAlpha");
            static readonly int AlphaST = Shader.PropertyToID("_FlatsMotionAlphaST");
            static readonly int AlphaCutoff = Shader.PropertyToID("_FlatsMotionCutoff");
            sealed class IdDraw
            {
                public Renderer renderer;
                public Material material;
                public int submesh;
            }
            sealed class IdPassData
            {
                public List<IdDraw> draws;
            }
            TextureHandle ObjectIds(RenderGraph graph, UniversalResourceData resources, Camera camera, CameraState state, AmplifyMotionEffectBase motion)
            {
                var desc = graph.GetTextureDesc(resources.activeColorTexture);
                desc.name = "FLATS motion object IDs";
                desc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm;
                desc.clearBuffer = true; desc.clearColor = Color.clear;
                var ids = graph.CreateTexture(desc);
                var draws = new List<IdDraw>();
                var planes = GeometryUtility.CalculateFrustumPlanes(camera);
                // Enumerate only while the option is on. Runtime-spawned enemies and
                // pickups must enter the same frame, so no stale scene-only registry.
                var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                var liveInstances = new HashSet<int>();
                foreach (var renderer in renderers) liveInstances.Add(renderer.GetInstanceID());
                var staleMaterials = new List<long>();
                foreach (var pair in state.objectMaterials) if (!liveInstances.Contains((int)(pair.Key >> 32))) staleMaterials.Add(pair.Key);
                foreach (var stale in staleMaterials) { CoreUtils.Destroy(state.objectMaterials[stale]); state.objectMaterials.Remove(stale); }
                var staleTransforms = new List<int>();
                foreach (var pair in state.previousTransforms) if (!liveInstances.Contains(pair.Key)) staleTransforms.Add(pair.Key);
                foreach (var stale in staleTransforms) state.previousTransforms.Remove(stale);
                int nextId = 2;
                foreach (var renderer in renderers)
                {
                    if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                    if (!renderer.enabled || renderer.forceRenderingOff || !renderer.isVisible || !renderer.gameObject.activeInHierarchy || renderer.gameObject.isStatic || renderer.isPartOfStaticBatch) continue;
                    int layer = 1 << renderer.gameObject.layer;
                    if ((camera.cullingMask & layer) == 0 || !GeometryUtility.TestPlanesAABB(planes, renderer.bounds)) continue;
                    int instance = renderer.GetInstanceID();
                    bool included = (motion.CullingMask.value & layer) != 0;
                    var matrix = renderer.localToWorldMatrix;
                    bool moved = !state.previousTransforms.TryGetValue(instance, out var previous) || matrix != previous;
                    state.previousTransforms[instance] = matrix;
                    if (included && renderer is MeshRenderer && !moved) continue;
                    // IDs are compared within this frame only, as in Amplify's
                    // ResetObjectId. Never alias live objects after repeated spawns.
                    int id = included ? (nextId < 254 ? nextId++ : 254) : 255;
                    var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null || mesh.subMeshCount == 0) continue;
                    var materials = renderer.sharedMaterials;
                    for (int index = 0; index < materials.Length; index++)
                    {
                        var material = materials[index]; if (material == null) continue;
                        string type = material.GetTag("RenderType", false);
                        if (type != "Opaque" && type != "TransparentCutout") continue;
                        string textureName = material.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                        Texture alpha = material.HasProperty(textureName) ? material.GetTexture(textureName) : null;
                        var scale = material.HasProperty(textureName) ? material.GetTextureScale(textureName) : Vector2.one;
                        var offset = material.HasProperty(textureName) ? material.GetTextureOffset(textureName) : Vector2.zero;
                        long materialKey = ((long)instance << 32) | (uint)index;
                        if (!state.objectMaterials.TryGetValue(materialKey, out var idMaterial))
                            state.objectMaterials[materialKey] = idMaterial = CoreUtils.CreateEngineMaterial(owner.shader);
                        idMaterial.SetFloat(ObjectId, id / 255f);
                        idMaterial.SetTexture(AlphaTexture, alpha != null ? alpha : Texture2D.whiteTexture);
                        idMaterial.SetVector(AlphaST, new Vector4(scale.x, scale.y, offset.x, offset.y));
                        idMaterial.SetFloat(AlphaCutoff, type == "TransparentCutout" ? (material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : .5f) : -1f);
                        draws.Add(new IdDraw { renderer = renderer, submesh = Mathf.Min(index, mesh.subMeshCount - 1), material = idMaterial });
                    }
                }
                using (var builder = graph.AddRasterRenderPass<IdPassData>("FLATS motion object identity", out var data))
                {
                    data.draws = draws;
                    builder.SetRenderAttachment(ids, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((IdPassData pass, RasterGraphContext context) =>
                    {
                        foreach (var draw in pass.draws)
                        {
                            if (draw.renderer == null) continue;
                            context.cmd.DrawRenderer(draw.renderer, draw.material, draw.submesh, 14);
                        }
                    });
                }
                return ids;
            }
            class PassData
            {
                public TextureHandle source, aux, coc, ids;
                public Material material;
                public int index;
            }
            public EffectPass(FlatsPostProcessFeature owner, bool opaque)
            {
                this.owner = owner; this.opaque = opaque; requiresIntermediateTexture = true;
            }
            TextureHandle Draw(RenderGraph graph, UniversalResourceData resources, TextureHandle source,
                Material material, int index, string name, int divisor = 1,
                TextureHandle aux = default, TextureHandle coc = default, TextureHandle output = default, TextureHandle ids = default)
            {
                var desc = graph.GetTextureDesc(source);
                desc.name = name; desc.clearBuffer = false;
                desc.width = Mathf.Max(1, desc.width / divisor); desc.height = Mathf.Max(1, desc.height / divisor);
                desc.msaaSamples = MSAASamples.None;
                var target = output.IsValid() ? output : graph.CreateTexture(desc);
                using (var builder = graph.AddRasterRenderPass<PassData>(name, out var data))
                {
                    data.source = source; data.aux = aux; data.coc = coc; data.ids = ids; data.material = material; data.index = index;
                    builder.UseTexture(source);
                    if (aux.IsValid()) builder.UseTexture(aux);
                    if (coc.IsValid()) builder.UseTexture(coc);
                    if (ids.IsValid()) builder.UseTexture(ids);
                    if (resources.cameraDepthTexture.IsValid()) builder.UseTexture(resources.cameraDepthTexture);
                    if (resources.cameraNormalsTexture.IsValid()) builder.UseTexture(resources.cameraNormalsTexture);
                    if (resources.motionVectorColor.IsValid()) builder.UseTexture(resources.motionVectorColor);
                    builder.UseAllGlobalTextures(true);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderAttachment(target, 0, AccessFlags.Write);
                    builder.SetRenderFunc((PassData pass, RasterGraphContext context) =>
                    {
                        if (pass.aux.IsValid()) context.cmd.SetGlobalTexture(AuxId, pass.aux);
                        if (pass.coc.IsValid()) context.cmd.SetGlobalTexture(CocId, pass.coc);
                        if (pass.ids.IsValid()) context.cmd.SetGlobalTexture(IdTexture, pass.ids);
                        Blitter.BlitTexture(context.cmd, pass.source, new Vector4(1, 1, 0, 0), pass.material, pass.index);
                    });
                }
                return target;
            }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>().camera;
                if (resources.isActiveTargetBackBuffer || !owner.states.TryGetValue(camera, out var state)) return;
                var original = resources.activeColorTexture;
                var source = original;
                if (opaque)
                {
                    var edge = camera.GetComponent<EdgeDetectEffectNormals>();
                    if (!Active(edge)) return;
                    var material = state.Get(owner.shader, 3);
                    material.SetVector("_Edge", new Vector4((int)edge.mode, edge.sampleDist, edge.edgeExp, edge.lumThreshhold));
                    material.SetVector("_Sensitivity", new Vector4(edge.sensitivityDepth, edge.sensitivityNormals, edge.edgesOnly, 0));
                    material.SetColor("_EdgeBackground", edge.edgesOnlyBgColor);
                    source = Draw(graph, resources, source, material, 3, "FLATS opaque outlines");
                }
                else
                {
                    // Component order is meaningful: this mirrors the order of the
                    // original image effects instead of applying a global volume to UI/scopes.
                    foreach (var component in camera.GetComponents<MonoBehaviour>())
                    {
                        if (!Active(component)) continue;
                        if (component is FxPro fx)
                        {
                            var material = state.Get(owner.shader, 0);
                            var parameters = fx.DOFParams;
                            float focus;
                            if (parameters.AutoFocus || parameters.Target == null)
                            {
                                float target = Physics.Raycast(camera.transform.position, camera.transform.forward, out var hit,
                                    Mathf.Infinity, parameters.AutoFocusLayerMask.value) ? hit.distance : camera.farClipPlane;
                                state.focusDistance = Mathf.Lerp(state.focusDistance, target, Time.deltaTime * parameters.AutoFocusSpeed);
                                focus = state.focusDistance;
                            }
                            else focus = camera.WorldToViewportPoint(parameters.Target.position).z;
                            float compression = Mathf.Clamp(parameters.DepthCompression, 1, 10);
                            focus = focus / camera.farClipPlane * parameters.FocalDistMultiplier * compression;
                            material.SetVector("_Focus", new Vector4(focus, focus * Mathf.Clamp(parameters.FocalLengthMultiplier, .01f, .99f), compression, parameters.DOFBlurSize));
                            material.SetFloat("_SCurve", fx.SCurveIntensity);
                            int radius = (int)fx.Quality >= 2 ? 3 : 5;
                            if (parameters.DoubleIntensityBlur) radius = radius == 3 ? 5 : 10;
                            material.SetFloat("_Radius", radius);
                            material.SetVector("_Bokeh", new Vector4(parameters.BokehEnabled ? 1 : 0, parameters.BokehThreshold, parameters.BokehGain, parameters.BokehBias));
                            if (fx.DOFEnabled)
                            {
                                var half = Draw(graph, resources, source, material, 12, "FLATS DOF downsample", 2);
                                if (fx.Quality == FxProNS.EffectsQuality.Fastest)
                                    half = Draw(graph, resources, half, material, 12, "FLATS DOF quarter downsample", 2);
                                var coc = Draw(graph, resources, half, material, 0, "FLATS circle of confusion");
                                if (fx.BlurCOCTexture)
                                {
                                    var horizontalCoc = state.Get(owner.shader, 11); horizontalCoc.CopyPropertiesFromMaterial(material);
                                    horizontalCoc.SetVector("_Direction", new Vector4(1.5f, 0, 0, 0));
                                    coc = Draw(graph, resources, coc, horizontalCoc, 10, "FLATS CoC horizontal");
                                    var verticalCoc = state.Get(owner.shader, 12); verticalCoc.CopyPropertiesFromMaterial(material);
                                    verticalCoc.SetVector("_Direction", new Vector4(0, 1.5f, 0, 0));
                                    coc = Draw(graph, resources, coc, verticalCoc, 10, "FLATS CoC vertical");
                                }
                                var h = state.Get(owner.shader, 1); h.CopyPropertiesFromMaterial(material); h.SetVector("_Direction", new Vector4(1, 0, 0, 0));
                                var blurred = Draw(graph, resources, half, h, 1, "FLATS DOF horizontal", coc: coc);
                                if (!parameters.BokehEnabled)
                                {
                                    var v = state.Get(owner.shader, 2); v.CopyPropertiesFromMaterial(material); v.SetVector("_Direction", new Vector4(0, 1, 0, 0));
                                    blurred = Draw(graph, resources, blurred, v, 1, "FLATS DOF vertical", coc: coc);
                                }
                                source = Draw(graph, resources, source, material, 2, "FLATS DOF composite", aux: blurred, coc: coc);
                            }
                            else source = Draw(graph, resources, source, material, 11, "FLATS tone curve");
                        }
                        else if (component is AmplifyMotionEffectBase motion)
                        {
                            var motionSource = source;
                            bool motionHistoryValid = state.lastMotionFrame >= Time.frameCount - 1;
                            state.lastMotionFrame = Time.frameCount;
                            var ids = ObjectIds(graph, resources, camera, state, motion);
                            int steps = Mathf.Clamp(motion.QualitySteps, 1, 8);
                            for (int step = 0; step < steps; step++)
                            {
                                var material = state.Get(owner.shader, 20 + step);
                                material.SetVector("_Motion", new Vector4(motionHistoryValid ? motion.MotionScale / Mathf.Max(Time.unscaledDeltaTime, .0001f) : 0f, motion.MinVelocity,
                                    motion.MaxVelocity, motion.DepthThreshold));
                                material.SetVector("_MotionOptions", new Vector4(1f - (float)step / steps,
                                    motion.QualityLevel == AmplifyMotion.Quality.Mobile ? 1 : (motion.QualityLevel == AmplifyMotion.Quality.Standard_SM3 ? 4 : 2),
                                    motion.DebugMode ? 1 : 0, motion.CameraMotionMult));
                                source = Draw(graph, resources, source, material, 4, "FLATS camera and object motion " + step, ids: ids);
                            }
                            if (motion.QualityLevel == AmplifyMotion.Quality.Mobile && !motion.DebugMode)
                                source = Draw(graph, resources, motionSource, state.Get(owner.shader, 20), 13, "FLATS mobile motion composite", aux: source, ids: ids);
                        }
                        else if (component is BlurEffect blur)
                        {
                            var downsample = state.Get(owner.shader, 60);
                            downsample.SetFloat("_ConeSpread", 1f);
                            source = Draw(graph, resources, source, downsample, 15, "FLATS death blur quarter downsample", 4);
                            for (int iteration = 0; iteration < blur.iterations; iteration++)
                            {
                                var cone = state.Get(owner.shader, 61 + iteration);
                                cone.SetFloat("_ConeSpread", .5f + iteration * blur.blurSpread);
                                source = Draw(graph, resources, source, cone, 15, "FLATS death blur cone " + iteration);
                            }
                        }
                        else if (component is CC_Grayscale gray)
                        {
                            var material = state.Get(owner.shader, 5);
                            material.SetVector("_Gray", new Vector4(gray.redLuminance, gray.greenLuminance, gray.blueLuminance, gray.amount));
                            source = Draw(graph, resources, source, material, 5, "FLATS saturation filter");
                        }
                        else if (component is Fisheye fish)
                        {
                            var material = state.Get(owner.shader, 6);
                            material.SetVector("_Fish", new Vector4(fish.strengthX * camera.aspect * 5f / 32, fish.strengthY * 5f / 32, 0, 0));
                            source = Draw(graph, resources, source, material, 6, "FLATS fisheye");
                        }
                        else if (component is ColorCorrectionCurves curves)
                        {
                            var material = state.Get(owner.shader, 7);
                            UpdateCurves(state, curves);
                            material.SetTexture("_Curves", state.curves);
                            material.SetVector("_Correction", new Vector4(curves.saturation, curves.useDepthCorrection ? 1 : 0, curves.selectiveCc ? 1 : 0, 0));
                            material.SetColor("_SelectiveFrom", curves.selectiveFromColor); material.SetColor("_SelectiveTo", curves.selectiveToColor);
                            source = Draw(graph, resources, source, material, 7, "FLATS color correction");
                        }
                        else if (component is FXAA)
                            source = Draw(graph, resources, source, state.Get(owner.shader, 8), 8, "FLATS FXAA");
                    }
                }
                // Keep the persistent camera stack color attachment. Swapping it for
                // a transient graph texture can discard base-camera color on overlays.
                if (!source.Equals(original)) Draw(graph, resources, source, state.Get(owner.shader, 9), 9, "FLATS effects resolve", output: original);
            }
            static void UpdateCurves(CameraState state, ColorCorrectionCurves effect)
            {
                var curves = new[] { effect.redChannel, effect.greenChannel, effect.blueChannel,
                    effect.depthRedChannel, effect.depthGreenChannel, effect.depthBlueChannel, effect.zCurve };
                int hash = 17;
                foreach (var curve in curves) foreach (var key in curve.keys) hash = unchecked(hash * 31 + key.GetHashCode());
                if (state.curves != null && state.curveHash == hash) return;
                state.curveHash = hash;
                if (state.curves == null) state.curves = new Texture2D(256, 7, TextureFormat.RGBA32, false, true)
                    { name = "FLATS color curves", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var colors = new Color[256 * 7];
                for (int row = 0; row < 7; row++) for (int x = 0; x < 256; x++)
                {
                    float value = Mathf.Clamp01(curves[row].Evaluate(x / 255f)); colors[row * 256 + x] = new Color(value, value, value, 1);
                }
                state.curves.SetPixels(colors); state.curves.Apply(false, false);
            }
        }
    }
}
