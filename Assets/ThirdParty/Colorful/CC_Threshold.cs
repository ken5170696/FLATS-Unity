using System;
using UnityEngine;
[AddComponentMenu("Colorful/Threshold")]
[ExecuteInEditMode]
public class CC_Threshold : CC_Base
{
	[Range(1f, 255f)]
	public float threshold;

	[Range(0f, 128f)]
	public float noiseRange;

	public bool useNoise;

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		base.material.SetFloat("_Threshold", threshold / 255f);
		base.material.SetFloat("_Range", noiseRange / 255f);
		Graphics.Blit(source, destination, base.material, useNoise ? 1 : 0);
	}

	public CC_Threshold()
	{
		threshold = 128f;
		noiseRange = 48f;

	}




}
