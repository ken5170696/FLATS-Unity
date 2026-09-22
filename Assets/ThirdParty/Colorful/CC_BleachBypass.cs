using System;
using UnityEngine;
[AddComponentMenu("Colorful/Bleach Bypass")]
[ExecuteInEditMode]
public class CC_BleachBypass : CC_Base
{
	[Range(0f, 1f)]
	public float amount;

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (amount == 0f)
		{
			Graphics.Blit(source, destination);
			return;
		}
		base.material.SetFloat("_Amount", amount);
		Graphics.Blit(source, destination, base.material);
	}

	public CC_BleachBypass()
	{
		amount = 1f;

	}




}
