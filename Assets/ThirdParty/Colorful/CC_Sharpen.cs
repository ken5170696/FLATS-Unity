using System;
using UnityEngine;
[AddComponentMenu("Colorful/Sharpen")]
[ExecuteInEditMode]
public class CC_Sharpen : CC_Base
{
	[Range(0f, 5f)]
	public float strength;

	[Range(0f, 1f)]
	public float clamp;

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (strength == 0f)
		{
			Graphics.Blit(source, destination);
			return;
		}
		base.material.SetFloat("_PX", 1f / (float)Screen.width);
		base.material.SetFloat("_PY", 1f / (float)Screen.height);
		base.material.SetFloat("_Strength", strength);
		base.material.SetFloat("_Clamp", clamp);
		Graphics.Blit(source, destination, base.material);
	}

	public CC_Sharpen()
	{
		strength = 0.6f;
		clamp = 0.05f;

	}




}
