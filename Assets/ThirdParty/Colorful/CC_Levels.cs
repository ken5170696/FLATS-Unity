using System;
using UnityEngine;
[AddComponentMenu("Colorful/Levels")]
[ExecuteInEditMode]
public class CC_Levels : CC_Base
{
	public bool isRGB;

	public float inputMinL;

	public float inputMaxL;

	public float inputGammaL;

	public float inputMinR;

	public float inputMaxR;

	public float inputGammaR;

	public float inputMinG;

	public float inputMaxG;

	public float inputGammaG;

	public float inputMinB;

	public float inputMaxB;

	public float inputGammaB;

	public float outputMinL;

	public float outputMaxL;

	public float outputMinR;

	public float outputMaxR;

	public float outputMinG;

	public float outputMaxG;

	public float outputMinB;

	public float outputMaxB;

	public int currentChannel;

	public bool logarithmic;

	public int mode
	{
		get
		{
			if (!isRGB)
			{
				return 0;
			}
			return 1;
		}
		set
		{
			isRGB = ((value > 0) ? true : false);
		}
	}

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (!isRGB)
		{
			base.material.SetVector("_InputMin", new Vector4(inputMinL / 255f, inputMinL / 255f, inputMinL / 255f, 1f));
			base.material.SetVector("_InputMax", new Vector4(inputMaxL / 255f, inputMaxL / 255f, inputMaxL / 255f, 1f));
			base.material.SetVector("_InputGamma", new Vector4(inputGammaL, inputGammaL, inputGammaL, 1f));
			base.material.SetVector("_OutputMin", new Vector4(outputMinL / 255f, outputMinL / 255f, outputMinL / 255f, 1f));
			base.material.SetVector("_OutputMax", new Vector4(outputMaxL / 255f, outputMaxL / 255f, outputMaxL / 255f, 1f));
		}
		else
		{
			base.material.SetVector("_InputMin", new Vector4(inputMinR / 255f, inputMinG / 255f, inputMinB / 255f, 1f));
			base.material.SetVector("_InputMax", new Vector4(inputMaxR / 255f, inputMaxG / 255f, inputMaxB / 255f, 1f));
			base.material.SetVector("_InputGamma", new Vector4(inputGammaR, inputGammaG, inputGammaB, 1f));
			base.material.SetVector("_OutputMin", new Vector4(outputMinR / 255f, outputMinG / 255f, outputMinB / 255f, 1f));
			base.material.SetVector("_OutputMax", new Vector4(outputMaxR / 255f, outputMaxG / 255f, outputMaxB / 255f, 1f));
		}
		Graphics.Blit(source, destination, base.material);
	}

	public CC_Levels()
	{
		inputMaxL = 255f;
		inputGammaL = 1f;
		inputMaxR = 255f;
		inputGammaR = 1f;
		inputMaxG = 255f;
		inputGammaG = 1f;
		inputMaxB = 255f;
		inputGammaB = 1f;
		outputMaxL = 255f;
		outputMaxR = 255f;
		outputMaxG = 255f;
		outputMaxB = 255f;

	}




}
