using System;
using UnityEngine;
[Serializable]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Image Effects/Camera/Tilt Shift (Lens Blur)")]
[ExecuteInEditMode]
public class TiltShiftHdr : PostEffectsBase
{
	[Serializable]
	public enum TiltShiftMode
	{
		TiltShiftMode,
		IrisMode
	}

	[Serializable]
	public enum TiltShiftQuality
	{
		Preview,
		Normal,
		High
	}

	public TiltShiftMode mode;

	public TiltShiftQuality quality;

	[Range(0f, 15f)]
	public float blurArea;

	[Range(0f, 25f)]
	public float maxBlurSize;

	[Range(0f, 1f)]
	public int downsample;

	public Shader tiltShiftShader;

	private Material tiltShiftMaterial;

	public TiltShiftHdr()
	{
		mode = TiltShiftMode.TiltShiftMode;
		quality = TiltShiftQuality.Normal;
		blurArea = 1f;
		maxBlurSize = 5f;
	}

	public override bool CheckResources()
	{
		CheckSupport(true);
		tiltShiftMaterial = CheckShaderAndCreateMaterial(tiltShiftShader, tiltShiftMaterial);
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
		tiltShiftMaterial.SetFloat("_BlurSize", (maxBlurSize >= 0f) ? maxBlurSize : 0f);
		tiltShiftMaterial.SetFloat("_BlurArea", blurArea);
		source.filterMode = FilterMode.Bilinear;
		RenderTexture renderTexture = destination;
		if (downsample != 0)
		{
			renderTexture = RenderTexture.GetTemporary(source.width >> downsample, source.height >> downsample, 0, source.format);
			renderTexture.filterMode = FilterMode.Bilinear;
		}
		int num = (int)quality;
		num *= 2;
		Graphics.Blit(source, renderTexture, tiltShiftMaterial, (mode != TiltShiftMode.TiltShiftMode) ? (num + 1) : num);
		if (downsample != 0)
		{
			tiltShiftMaterial.SetTexture("_Blurred", renderTexture);
			Graphics.Blit(source, destination, tiltShiftMaterial, 6);
		}
		if (renderTexture != destination)
		{
			RenderTexture.ReleaseTemporary(renderTexture);
		}
	}

	public override void Main()
	{
	}




}
