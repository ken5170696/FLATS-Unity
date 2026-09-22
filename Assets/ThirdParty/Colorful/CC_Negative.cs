using System;
using UnityEngine;
[AddComponentMenu("Colorful/Negative")]
[ExecuteInEditMode]
public class CC_Negative : CC_Base
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

	public CC_Negative()
	{
		amount = 1f;

	}




}
