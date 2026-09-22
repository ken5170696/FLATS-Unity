using System;
using UnityEngine;
[ExecuteInEditMode]
[AddComponentMenu("Colorful/Lookup Filter (Color Grading)")]
public class CC_LookupFilter : CC_Base
{
	public Texture lookupTexture;

	[Range(0f, 1f)]
	public float amount;

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (lookupTexture == null)
		{
			Graphics.Blit(source, destination);
			return;
		}
		base.material.SetTexture("_LookupTex", lookupTexture);
		base.material.SetFloat("_Amount", amount);
		Graphics.Blit(source, destination, base.material, CC_Base.IsLinear() ? 1 : 0);
	}

	public CC_LookupFilter()
	{
		amount = 1f;

	}




}
