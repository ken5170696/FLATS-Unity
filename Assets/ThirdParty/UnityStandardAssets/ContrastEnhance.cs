using System;
using UnityEngine;
[Serializable]
[AddComponentMenu("Image Effects/Color Adjustments/Contrast Enhance (Unsharp Mask)")]
[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class ContrastEnhance : PostEffectsBase
{
	public float intensity;

	public float threshhold;

	private Material separableBlurMaterial;

	private Material contrastCompositeMaterial;

	public float blurSpread;

	public Shader separableBlurShader;

	public Shader contrastCompositeShader;

	public ContrastEnhance()
	{
		intensity = 0.5f;
		blurSpread = 1f;
	}

	public override bool CheckResources()
	{
		CheckSupport(false);
		contrastCompositeMaterial = CheckShaderAndCreateMaterial(contrastCompositeShader, contrastCompositeMaterial);
		separableBlurMaterial = CheckShaderAndCreateMaterial(separableBlurShader, separableBlurMaterial);
		if (!isSupported)
		{
			ReportAutoDisable();
		}
		return isSupported;
	}

	public virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (!CheckResources())
		{
			Graphics.Blit(source, destination);
			return;
		}
		int width = source.width;
		int height = source.height;
		RenderTexture temporary = RenderTexture.GetTemporary(width / 2, height / 2, 0);
		Graphics.Blit(source, temporary);
		RenderTexture temporary2 = RenderTexture.GetTemporary(width / 4, height / 4, 0);
		Graphics.Blit(temporary, temporary2);
		RenderTexture.ReleaseTemporary(temporary);
		separableBlurMaterial.SetVector("offsets", new Vector4(0f, blurSpread * 1f / (float)temporary2.height, 0f, 0f));
		RenderTexture temporary3 = RenderTexture.GetTemporary(width / 4, height / 4, 0);
		Graphics.Blit(temporary2, temporary3, separableBlurMaterial);
		RenderTexture.ReleaseTemporary(temporary2);
		separableBlurMaterial.SetVector("offsets", new Vector4(blurSpread * 1f / (float)temporary2.width, 0f, 0f, 0f));
		temporary2 = RenderTexture.GetTemporary(width / 4, height / 4, 0);
		Graphics.Blit(temporary3, temporary2, separableBlurMaterial);
		RenderTexture.ReleaseTemporary(temporary3);
		contrastCompositeMaterial.SetTexture("_MainTexBlurred", temporary2);
		contrastCompositeMaterial.SetFloat("intensity", intensity);
		contrastCompositeMaterial.SetFloat("threshhold", threshhold);
		Graphics.Blit(source, destination, contrastCompositeMaterial);
		RenderTexture.ReleaseTemporary(temporary2);
	}

	public override void Main()
	{
	}




}
