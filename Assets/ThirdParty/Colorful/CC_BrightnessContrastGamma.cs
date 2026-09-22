using System;
using UnityEngine;
[AddComponentMenu("Colorful/Brightness, Contrast, Gamma")]
[ExecuteInEditMode]
public class CC_BrightnessContrastGamma : CC_Base
{
	[Range(-100f, 100f)]
	public float brightness;

	[Range(-100f, 100f)]
	public float contrast;

	[Range(0f, 1f)]
	public float redCoeff;

	[Range(0f, 1f)]
	public float greenCoeff;

	[Range(0f, 1f)]
	public float blueCoeff;

	[Range(0.1f, 9.9f)]
	public float gamma;

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (brightness == 0f && contrast == 0f && gamma == 1f)
		{
			Graphics.Blit(source, destination);
			return;
		}
		base.material.SetVector("_BCG", new Vector4((brightness + 100f) * 0.01f, (contrast + 100f) * 0.01f, 1f / gamma));
		base.material.SetVector("_Coeffs", new Vector4(redCoeff, greenCoeff, blueCoeff, 1f));
		Graphics.Blit(source, destination, base.material);
	}

	public CC_BrightnessContrastGamma()
	{
		redCoeff = 0.5f;
		greenCoeff = 0.5f;
		blueCoeff = 0.5f;
		gamma = 1f;

	}




}
