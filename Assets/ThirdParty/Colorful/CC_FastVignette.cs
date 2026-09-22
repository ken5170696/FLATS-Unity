using System;
using UnityEngine;
[ExecuteInEditMode]
[AddComponentMenu("Colorful/Fast Vignette")]
public class CC_FastVignette : CC_Base
{
	public Vector2 center;

	[Range(-100f, 100f)]
	public float sharpness;

	[Range(0f, 100f)]
	public float darkness;

	public bool desaturate;

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		base.material.SetVector("_Data", new Vector4(center.x, center.y, sharpness * 0.01f, darkness * 0.02f));
		Graphics.Blit(source, destination, base.material, desaturate ? 1 : 0);
	}

	public CC_FastVignette()
	{
		center = new Vector2(0.5f, 0.5f);
		sharpness = 10f;
		darkness = 30f;

	}




}
