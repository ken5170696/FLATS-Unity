using System.IO;
using Flats.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Ordinary asset-authoring command: reproducibly creates the production pipeline.
public static class FlatsUrpSetup
{
    const string Root = "Assets/_Flats/Settings/Rendering";
    [MenuItem("FLATS/Rendering/Configure URP")]
    public static void Configure()
    {
        Directory.CreateDirectory(Root);
        AssetDatabase.Refresh();
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Root + "/FlatsRenderer.asset");
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, Root + "/FlatsRenderer.asset");
        }
        renderer.renderingMode = RenderingMode.Forward;
        renderer.intermediateTextureMode = IntermediateTextureMode.Always;
        var feature = renderer.rendererFeatures.Find(f => f is FlatsPostProcessFeature) as FlatsPostProcessFeature;
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<FlatsPostProcessFeature>();
            feature.name = "FLATS original effects";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
        }
        var serializedFeature = new SerializedObject(feature);
        var shader = serializedFeature.FindProperty("shader");
        if (shader != null) shader.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Flats/Art/Shared/Shaders/FlatsPostProcess.shader");
        serializedFeature.ApplyModifiedPropertiesWithoutUndo();
        renderer.SetDirty();
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root + "/FlatsPipeline.asset");
        if (pipeline == null)
        {
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, Root + "/FlatsPipeline.asset");
        }
        pipeline.supportsCameraDepthTexture = true;
        pipeline.supportsCameraOpaqueTexture = false;
        pipeline.supportsHDR = false;
        pipeline.msaaSampleCount = 1;
        pipeline.renderScale = 1;
        var serializedPipeline = new SerializedObject(pipeline);
        serializedPipeline.FindProperty("m_MainLightShadowsSupported").boolValue = false;
        serializedPipeline.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
        serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
        pipeline.shadowDistance = 0;
        GraphicsSettings.defaultRenderPipeline = pipeline;
        int previous = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; ++i)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = pipeline;
        }
        QualitySettings.SetQualityLevel(previous, false);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(feature);
        EditorUtility.SetDirty(pipeline);
        AssetDatabase.SaveAssets();
        Debug.Log("FLATS_URP_CONFIGURED");
    }
}
