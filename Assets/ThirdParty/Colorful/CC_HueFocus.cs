using System;
using UnityEngine;
[AddComponentMenu("Colorful/Hue Focus")]
[ExecuteInEditMode]
public class CC_HueFocus : CC_Base
{
	[Range(0f, 360f)]
	public float hue;

	[Range(1f, 180f)]
	public float range;

	[Range(0f, 1f)]
	public float boost;

	[Range(0f, 1f)]
	public float amount;

	[ImageEffectTransformsToLDR]
	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		float num = hue / 360f;
		float num2 = range / 180f;
		base.material.SetVector("_Range", new Vector2(num - num2, num + num2));
		base.material.SetVector("_Params", new Vector3(num, boost + 1f, amount));
		Graphics.Blit(source, destination, base.material);
	}

	public CC_HueFocus()
	{
		range = 30f;
		boost = 0.5f;
		amount = 1f;

	}




}
