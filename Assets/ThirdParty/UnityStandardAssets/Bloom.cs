using System;
using UnityEngine;
[Serializable]
[AddComponentMenu("Image Effects/Bloom and Glow/Bloom")]
[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class Bloom : PostEffectsBase
{
	[Serializable]
	public enum LensFlareStyle
	{
		Ghosting,
		Anamorphic,
		Combined
	}

	[Serializable]
	public enum TweakMode
	{
		Basic,
		Complex
	}

	[Serializable]
	public enum HDRBloomMode
	{
		Auto,
		On,
		Off
	}

	[Serializable]
	public enum BloomScreenBlendMode
	{
		Screen,
		Add
	}

	[Serializable]
	public enum BloomQuality
	{
		Cheap,
		High
	}

	public TweakMode tweakMode;

	public BloomScreenBlendMode screenBlendMode;

	public HDRBloomMode hdr;

	private bool doHdr;

	public float sepBlurSpread;

	public BloomQuality quality;

	public float bloomIntensity;

	public float bloomThreshhold;

	public Color bloomThreshholdColor;

	public int bloomBlurIterations;

	public int hollywoodFlareBlurIterations;

	public float flareRotation;

	public LensFlareStyle lensflareMode;

	public float hollyStretchWidth;

	public float lensflareIntensity;

	public float lensflareThreshhold;

	public float lensFlareSaturation;

	public Color flareColorA;

	public Color flareColorB;

	public Color flareColorC;

	public Color flareColorD;

	public float blurWidth;

	public Texture2D lensFlareVignetteMask;

	public Shader lensFlareShader;

	private Material lensFlareMaterial;

	public Shader screenBlendShader;

	private Material screenBlend;

	public Shader blurAndFlaresShader;

	private Material blurAndFlaresMaterial;

	public Shader brightPassFilterShader;

	private Material brightPassFilterMaterial;

	public Bloom()
	{
		screenBlendMode = BloomScreenBlendMode.Add;
		hdr = HDRBloomMode.Auto;
		sepBlurSpread = 2.5f;
		quality = BloomQuality.High;
		bloomIntensity = 0.5f;
		bloomThreshhold = 0.5f;
		bloomThreshholdColor = Color.white;
		bloomBlurIterations = 2;
		hollywoodFlareBlurIterations = 2;
		lensflareMode = LensFlareStyle.Anamorphic;
		hollyStretchWidth = 2.5f;
		lensflareThreshhold = 0.3f;
		lensFlareSaturation = 0.75f;
		flareColorA = new Color(0.4f, 0.4f, 0.8f, 0.75f);
		flareColorB = new Color(0.4f, 0.8f, 0.8f, 0.75f);
		flareColorC = new Color(0.8f, 0.4f, 0.8f, 0.75f);
		flareColorD = new Color(0.8f, 0.4f, 0f, 0.75f);
		blurWidth = 1f;
	}

	public override bool CheckResources()
	{
		CheckSupport(false);
		screenBlend = CheckShaderAndCreateMaterial(screenBlendShader, screenBlend);
		lensFlareMaterial = CheckShaderAndCreateMaterial(lensFlareShader, lensFlareMaterial);
		blurAndFlaresMaterial = CheckShaderAndCreateMaterial(blurAndFlaresShader, blurAndFlaresMaterial);
		brightPassFilterMaterial = CheckShaderAndCreateMaterial(brightPassFilterShader, brightPassFilterMaterial);
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
		doHdr = false;
		if (hdr == HDRBloomMode.Auto)
		{
			bool num = source.format == RenderTextureFormat.ARGBHalf;
			if (num)
			{
				num = GetComponent<Camera>().allowHDR;
			}
			doHdr = num;
		}
		else
		{
			doHdr = hdr == HDRBloomMode.On;
		}
		bool num2 = doHdr;
		if (num2)
		{
			num2 = supportHDRTextures;
		}
		doHdr = num2;
		BloomScreenBlendMode bloomScreenBlendMode = screenBlendMode;
		if (doHdr)
		{
			bloomScreenBlendMode = BloomScreenBlendMode.Add;
		}
		RenderTextureFormat format = ((!doHdr) ? RenderTextureFormat.Default : RenderTextureFormat.ARGBHalf);
		int width = source.width / 2;
		int height = source.height / 2;
		int width2 = source.width / 4;
		int height2 = source.height / 4;
		float num3 = 1f * (float)source.width / (1f * (float)source.height);
		float num4 = 0.001953125f;
		RenderTexture temporary = RenderTexture.GetTemporary(width2, height2, 0, format);
		RenderTexture temporary2 = RenderTexture.GetTemporary(width, height, 0, format);
		if (quality > BloomQuality.Cheap)
		{
			Graphics.Blit(source, temporary2, screenBlend, 2);
			RenderTexture temporary3 = RenderTexture.GetTemporary(width2, height2, 0, format);
			Graphics.Blit(temporary2, temporary3, screenBlend, 2);
			Graphics.Blit(temporary3, temporary, screenBlend, 6);
			RenderTexture.ReleaseTemporary(temporary3);
		}
		else
		{
			Graphics.Blit(source, temporary2);
			Graphics.Blit(temporary2, temporary, screenBlend, 6);
		}
		RenderTexture.ReleaseTemporary(temporary2);
		RenderTexture renderTexture = RenderTexture.GetTemporary(width2, height2, 0, format);
		BrightFilter(bloomThreshhold * bloomThreshholdColor, temporary, renderTexture);
		if (bloomBlurIterations < 1)
		{
			bloomBlurIterations = 1;
		}
		else if (bloomBlurIterations > 10)
		{
			bloomBlurIterations = 10;
		}
		for (int i = 0; i < bloomBlurIterations; i++)
		{
			float num5 = (1f + (float)i * 0.25f) * sepBlurSpread;
			RenderTexture temporary4 = RenderTexture.GetTemporary(width2, height2, 0, format);
			blurAndFlaresMaterial.SetVector("_Offsets", new Vector4(0f, num5 * num4, 0f, 0f));
			Graphics.Blit(renderTexture, temporary4, blurAndFlaresMaterial, 4);
			RenderTexture.ReleaseTemporary(renderTexture);
			renderTexture = temporary4;
			temporary4 = RenderTexture.GetTemporary(width2, height2, 0, format);
			blurAndFlaresMaterial.SetVector("_Offsets", new Vector4(num5 / num3 * num4, 0f, 0f, 0f));
			Graphics.Blit(renderTexture, temporary4, blurAndFlaresMaterial, 4);
			RenderTexture.ReleaseTemporary(renderTexture);
			renderTexture = temporary4;
			if (quality > BloomQuality.Cheap)
			{
				if (i == 0)
				{
					Graphics.SetRenderTarget(temporary);
					GL.Clear(false, true, Color.black);
					Graphics.Blit(renderTexture, temporary);
				}
				else
				{
					temporary.MarkRestoreExpected();
					Graphics.Blit(renderTexture, temporary, screenBlend, 10);
				}
			}
		}
		if (quality > BloomQuality.Cheap)
		{
			Graphics.SetRenderTarget(renderTexture);
			GL.Clear(false, true, Color.black);
			Graphics.Blit(temporary, renderTexture, screenBlend, 6);
		}
		if (!(lensflareIntensity <= float.Epsilon))
		{
			RenderTexture temporary5 = RenderTexture.GetTemporary(width2, height2, 0, format);
			if (lensflareMode == LensFlareStyle.Ghosting)
			{
				BrightFilter(lensflareThreshhold, renderTexture, temporary5);
				if (quality > BloomQuality.Cheap)
				{
					blurAndFlaresMaterial.SetVector("_Offsets", new Vector4(0f, 1.5f / (1f * (float)temporary.height), 0f, 0f));
					Graphics.SetRenderTarget(temporary);
					GL.Clear(false, true, Color.black);
					Graphics.Blit(temporary5, temporary, blurAndFlaresMaterial, 4);
					blurAndFlaresMaterial.SetVector("_Offsets", new Vector4(1.5f / (1f * (float)temporary.width), 0f, 0f, 0f));
					Graphics.SetRenderTarget(temporary5);
					GL.Clear(false, true, Color.black);
					Graphics.Blit(temporary, temporary5, blurAndFlaresMaterial, 4);
				}
				Vignette(0.975f, temporary5, temporary5);
				BlendFlares(temporary5, renderTexture);
			}
			else
			{
				float num6 = 1f * Mathf.Cos(flareRotation);
				float num7 = 1f * Mathf.Sin(flareRotation);
				float num8 = hollyStretchWidth * 1f / num3 * num4;
				float num9 = hollyStretchWidth * num4;
				blurAndFlaresMaterial.SetVector("_Offsets", new Vector4(num6, num7, 0f, 0f));
				blurAndFlaresMaterial.SetVector("_Threshhold", new Vector4(lensflareThreshhold, 1f, 0f, 0f));
				blurAndFlaresMaterial.SetVector("_TintColor", new Vector4(flareColorA.r, flareColorA.g, flareColorA.b, flareColorA.a) * flareColorA.a * lensflareIntensity);
				blurAndFlaresMaterial.SetFloat("_Saturation", lensFlareSaturation);
				temporary.DiscardContents();
				Graphics.Blit(temporary5, temporary, blurAndFlaresMaterial, 2);
				temporary5.DiscardContents();
				Graphics.Blit(temporary, temporary5, blurAndFlaresMaterial, 3);
				blurAndFlaresMaterial.SetVector("_Offsets", new Vector4(num6 * num8, num7 * num8, 0f, 0f));
				blurAndFlaresMaterial.SetFloat("_StretchWidth", hollyStretchWidth);
				temporary.DiscardContents();
				Graphics.Blit(temporary5, temporary, blurAndFlaresMaterial, 1);
				blurAndFlaresMaterial.SetFloat("_StretchWidth", hollyStretchWidth * 2f);
				temporary5.DiscardContents();
				Graphics.Blit(temporary, temporary5, blurAndFlaresMaterial, 1);
				blurAndFlaresMaterial.SetFloat("_StretchWidth", hollyStretchWidth * 4f);
				temporary.DiscardContents();
				Graphics.Blit(temporary5, temporary, blurAndFlaresMaterial, 1);
				for (int i = 0; i < hollywoodFlareBlurIterations; i++)
				{
					num8 = hollyStretchWidth * 2f / num3 * num4;
					blurAndFlaresMaterial.SetVector("_Offsets", new Vector4(num8 * num6, num8 * num7, 0f, 0f));
					temporary5.DiscardContents();
					Graphics.Blit(temporary, temporary5, blurAndFlaresMaterial, 4);
					blurAndFlaresMaterial.SetVector("_Offsets", new Vector4(num8 * num6, num8 * num7, 0f, 0f));
					temporary.DiscardContents();
					Graphics.Blit(temporary5, temporary, blurAndFlaresMaterial, 4);
				}
				if (lensflareMode == LensFlareStyle.Anamorphic)
				{
					AddTo(1f, temporary, renderTexture);
				}
				else
				{
					Vignette(1f, temporary, temporary5);
					BlendFlares(temporary5, temporary);
					AddTo(1f, temporary, renderTexture);
				}
			}
			RenderTexture.ReleaseTemporary(temporary5);
		}
		int pass = (int)bloomScreenBlendMode;
		screenBlend.SetFloat("_Intensity", bloomIntensity);
		screenBlend.SetTexture("_ColorBuffer", source);
		if (quality > BloomQuality.Cheap)
		{
			RenderTexture temporary6 = RenderTexture.GetTemporary(width, height, 0, format);
			Graphics.Blit(renderTexture, temporary6);
			Graphics.Blit(temporary6, destination, screenBlend, pass);
			RenderTexture.ReleaseTemporary(temporary6);
		}
		else
		{
			Graphics.Blit(renderTexture, destination, screenBlend, pass);
		}
		RenderTexture.ReleaseTemporary(temporary);
		RenderTexture.ReleaseTemporary(renderTexture);
	}

	private void AddTo(float intensity_, RenderTexture from, RenderTexture to)
	{
		screenBlend.SetFloat("_Intensity", intensity_);
		to.MarkRestoreExpected();
		Graphics.Blit(from, to, screenBlend, 9);
	}

	private void BlendFlares(RenderTexture from, RenderTexture to)
	{
		lensFlareMaterial.SetVector("colorA", new Vector4(flareColorA.r, flareColorA.g, flareColorA.b, flareColorA.a) * lensflareIntensity);
		lensFlareMaterial.SetVector("colorB", new Vector4(flareColorB.r, flareColorB.g, flareColorB.b, flareColorB.a) * lensflareIntensity);
		lensFlareMaterial.SetVector("colorC", new Vector4(flareColorC.r, flareColorC.g, flareColorC.b, flareColorC.a) * lensflareIntensity);
		lensFlareMaterial.SetVector("colorD", new Vector4(flareColorD.r, flareColorD.g, flareColorD.b, flareColorD.a) * lensflareIntensity);
		to.MarkRestoreExpected();
		Graphics.Blit(from, to, lensFlareMaterial);
	}

	private void BrightFilter(float thresh, RenderTexture from, RenderTexture to)
	{
		brightPassFilterMaterial.SetVector("_Threshhold", new Vector4(thresh, thresh, thresh, thresh));
		Graphics.Blit(from, to, brightPassFilterMaterial, 0);
	}

	private void BrightFilter(Color threshColor, RenderTexture from, RenderTexture to)
	{
		brightPassFilterMaterial.SetVector("_Threshhold", threshColor);
		Graphics.Blit(from, to, brightPassFilterMaterial, 1);
	}

	private void Vignette(float amount, RenderTexture from, RenderTexture to)
	{
		if ((bool)lensFlareVignetteMask)
		{
			screenBlend.SetTexture("_ColorBuffer", lensFlareVignetteMask);
			to.MarkRestoreExpected();
			Graphics.Blit((!(from == to)) ? from : null, to, screenBlend, (!(from == to)) ? 3 : 7);
		}
		else if (from != to)
		{
			Graphics.SetRenderTarget(to);
			GL.Clear(false, true, Color.black);
			Graphics.Blit(from, to);
		}
	}

	public override void Main()
	{
	}




}
