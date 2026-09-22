using System;
using UnityEngine;
[Serializable]
[AddComponentMenu("Image Effects/Bloom and Glow/Bloom (Optimized)")]
[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class FastBloom : PostEffectsBase
{
	[Serializable]
	public enum Resolution
	{
		Low,
		High
	}

	[Serializable]
	public enum BlurType
	{
		Standard,
		Sgx
	}

	[Range(0f, 1.5f)]
	public float threshhold;

	[Range(0f, 2.5f)]
	public float intensity;

	[Range(0.25f, 5.5f)]
	public float blurSize;

	public Resolution resolution;

	[Range(1f, 4f)]
	public int blurIterations;

	public BlurType blurType;

	public Shader fastBloomShader;

	private Material fastBloomMaterial;

	public FastBloom()
	{
		threshhold = 0.25f;
		intensity = 0.75f;
		blurSize = 1f;
		resolution = Resolution.Low;
		blurIterations = 1;
		blurType = BlurType.Standard;
	}

	public override bool CheckResources()
	{
		CheckSupport(false);
		fastBloomMaterial = CheckShaderAndCreateMaterial(fastBloomShader, fastBloomMaterial);
		if (!isSupported)
		{
			ReportAutoDisable();
		}
		return isSupported;
	}

	public virtual void OnDisable()
	{
		if ((bool)fastBloomMaterial)
		{
			UnityEngine.Object.DestroyImmediate(fastBloomMaterial);
		}
	}

	public virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (!CheckResources())
		{
			Graphics.Blit(source, destination);
			return;
		}
		int num = ((resolution != Resolution.Low) ? 2 : 4);
		float num2 = ((resolution != Resolution.Low) ? 1f : 0.5f);
		fastBloomMaterial.SetVector("_Parameter", new Vector4(blurSize * num2, 0f, threshhold, intensity));
		source.filterMode = FilterMode.Bilinear;
		int width = source.width / num;
		int height = source.height / num;
		RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 0, source.format);
		renderTexture.filterMode = FilterMode.Bilinear;
		Graphics.Blit(source, renderTexture, fastBloomMaterial, 1);
		int num3 = ((blurType != BlurType.Standard) ? 2 : 0);
		for (int i = 0; i < blurIterations; i++)
		{
			fastBloomMaterial.SetVector("_Parameter", new Vector4(blurSize * num2 + (float)i * 1f, 0f, threshhold, intensity));
			RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, source.format);
			temporary.filterMode = FilterMode.Bilinear;
			Graphics.Blit(renderTexture, temporary, fastBloomMaterial, 2 + num3);
			RenderTexture.ReleaseTemporary(renderTexture);
			renderTexture = temporary;
			temporary = RenderTexture.GetTemporary(width, height, 0, source.format);
			temporary.filterMode = FilterMode.Bilinear;
			Graphics.Blit(renderTexture, temporary, fastBloomMaterial, 3 + num3);
			RenderTexture.ReleaseTemporary(renderTexture);
			renderTexture = temporary;
		}
		fastBloomMaterial.SetTexture("_Bloom", renderTexture);
		Graphics.Blit(source, destination, fastBloomMaterial, 0);
		RenderTexture.ReleaseTemporary(renderTexture);
	}

	public override void Main()
	{
	}




}
