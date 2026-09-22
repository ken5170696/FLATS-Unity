using System;
using UnityEngine;
[ExecuteInEditMode]
[AddComponentMenu("Colorful/Analog TV")]
public class CC_AnalogTV : CC_Base
{
	public bool autoPhase;

	public float phase;

	public bool grayscale;

	[Range(0f, 1f)]
	public float noiseIntensity;

	[Range(0f, 10f)]
	public float scanlinesIntensity;

	[Range(0f, 4096f)]
	public float scanlinesCount;

	public float scanlinesOffset;

	[Range(-2f, 2f)]
	public float distortion;

	[Range(-2f, 2f)]
	public float cubicDistortion;

	[Range(0.01f, 2f)]
	public float scale;

	protected virtual void Update()
	{
		if (autoPhase)
		{
			phase += Time.deltaTime * 0.25f;
		}
	}

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		base.material.SetFloat("_Phase", phase);
		base.material.SetFloat("_NoiseIntensity", noiseIntensity);
		base.material.SetFloat("_ScanlinesIntensity", scanlinesIntensity);
		base.material.SetFloat("_ScanlinesCount", (int)scanlinesCount);
		base.material.SetFloat("_ScanlinesOffset", scanlinesOffset);
		base.material.SetFloat("_Distortion", distortion);
		base.material.SetFloat("_CubicDistortion", cubicDistortion);
		base.material.SetFloat("_Scale", scale);
		Graphics.Blit(source, destination, base.material, grayscale ? 1 : 0);
	}

	public CC_AnalogTV()
	{
		autoPhase = true;
		phase = 0.5f;
		noiseIntensity = 0.5f;
		scanlinesIntensity = 2f;
		scanlinesCount = 768f;
		distortion = 0.2f;
		cubicDistortion = 0.6f;
		scale = 0.8f;

	}




}
