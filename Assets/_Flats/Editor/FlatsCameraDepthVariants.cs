using System.Collections.Generic;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

// Shader Graph's Built-in stripper removes directional ShadowCaster variants
// when QualitySettings.shadows is disabled. These unlit shaders also need that
// pass for camera depth (DoF, outlines and motion), independently of shadows.
internal static class FlatsCameraDepthVariants
{
    internal static readonly Dictionary<string, ShaderCompilerData[]> Pending = new Dictionary<string, ShaderCompilerData[]>();

    internal static bool Required(Shader shader, ShaderSnippetData snippet)
    {
        if (GraphicsSettings.currentRenderPipeline != null || snippet.passType != PassType.ShadowCaster)
            return false;
        return shader.name == "Texture Only" || shader.name == "Simple Color Texture"
            || shader.name == "Mobile/Unlit (Supports Lightmap)" || shader.name == "Unlit/Transparent Cutout";
    }

    internal static string Key(Shader shader, ShaderSnippetData snippet)
    {
        return shader.GetInstanceID() + "/" + snippet.passName + "/" + snippet.shaderType;
    }
}

internal sealed class FlatsRememberCameraDepthVariants : IPreprocessShaders
{
    public int callbackOrder { get { return int.MinValue; } }
    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        if (!FlatsCameraDepthVariants.Required(shader, snippet)) return;
        var variants = new ShaderCompilerData[data.Count];
        data.CopyTo(variants, 0);
        FlatsCameraDepthVariants.Pending[FlatsCameraDepthVariants.Key(shader, snippet)] = variants;
    }
}

internal sealed class FlatsRetainCameraDepthVariants : IPreprocessShaders
{
    public int callbackOrder { get { return int.MaxValue; } }
    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        ShaderCompilerData[] variants;
        string key = FlatsCameraDepthVariants.Key(shader, snippet);
        if (!FlatsCameraDepthVariants.Pending.TryGetValue(key, out variants)) return;
        data.Clear();
        foreach (var variant in variants) data.Add(variant);
        FlatsCameraDepthVariants.Pending.Remove(key);
    }
}
