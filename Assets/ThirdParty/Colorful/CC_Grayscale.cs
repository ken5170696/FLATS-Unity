using System;
using UnityEngine;
[AddComponentMenu("Colorful/Grayscale")]
[ExecuteInEditMode]
public class CC_Grayscale : CC_Base
{
	[Range(0f, 1f)]
	public float redLuminance;

	[Range(0f, 1f)]
	public float greenLuminance;

	[Range(0f, 1f)]
	public float blueLuminance;

	[Range(0f, 1f)]
	public float amount;

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (amount == 0f)
		{
			Graphics.Blit(source, destination);
			return;
		}
		base.material.SetVector("_Data", new Vector4(redLuminance, greenLuminance, blueLuminance, amount));
		Graphics.Blit(source, destination, base.material);
	}

	public CC_Grayscale()
	{
		redLuminance = 0.299f;
		greenLuminance = 0.587f;
		blueLuminance = 0.114f;
		amount = 1f;

	}




}
