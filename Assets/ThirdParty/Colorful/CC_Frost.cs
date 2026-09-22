using System;
using UnityEngine;
[ExecuteInEditMode]
[AddComponentMenu("Colorful/Frost")]
public class CC_Frost : CC_Base
{
	[Range(0f, 16f)]
	public float scale;

	[Range(-100f, 100f)]
	public float sharpness;

	[Range(0f, 100f)]
	public float darkness;

	public bool enableVignette;

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (scale == 0f)
		{
			Graphics.Blit(source, destination);
			return;
		}
		base.material.SetFloat("_Scale", scale);
		base.material.SetFloat("_Sharpness", sharpness * 0.01f);
		base.material.SetFloat("_Darkness", darkness * 0.02f);
		Graphics.Blit(source, destination, base.material, enableVignette ? 1 : 0);
	}

	public CC_Frost()
	{
		scale = 1.2f;
		sharpness = 40f;
		darkness = 35f;
		enableVignette = true;

	}




}
