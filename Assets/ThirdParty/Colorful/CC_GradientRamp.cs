using System;
using UnityEngine;
[AddComponentMenu("Colorful/Gradient Ramp")]
[ExecuteInEditMode]
public class CC_GradientRamp : CC_Base
{
	public Texture rampTexture;

	[Range(0f, 1f)]
	public float amount;

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (rampTexture == null || amount == 0f)
		{
			Graphics.Blit(source, destination);
			return;
		}
		base.material.SetTexture("_RampTex", rampTexture);
		base.material.SetFloat("_Amount", amount);
		Graphics.Blit(source, destination, base.material);
	}

	public CC_GradientRamp()
	{
		amount = 1f;

	}




}
