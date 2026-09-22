using System;
using UnityEngine;
[AddComponentMenu("Colorful/Photo Filter")]
[ExecuteInEditMode]
public class CC_PhotoFilter : CC_Base
{
	public Color color;

	[Range(0f, 1f)]
	public float density;

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (density == 0f)
		{
			Graphics.Blit(source, destination);
			return;
		}
		base.material.SetColor("_RGB", color);
		base.material.SetFloat("_Density", density);
		Graphics.Blit(source, destination, base.material);
	}

	public CC_PhotoFilter()
	{
		color = new Color(1f, 0.5f, 0.2f, 1f);
		density = 0.35f;

	}




}
