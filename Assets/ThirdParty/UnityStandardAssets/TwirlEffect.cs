using System;
using UnityEngine;
[ExecuteInEditMode]
[AddComponentMenu("Image Effects/Displacement/Twirl")]
public class TwirlEffect : ImageEffectBase
{
	public Vector2 radius;

	public float angle;

	public Vector2 center;

	private void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		ImageEffects.RenderDistortion(base.material, source, destination, angle, center, radius);
	}

	public TwirlEffect()
	{
		radius = new Vector2(0.3f, 0.3f);
		angle = 50f;
		center = new Vector2(0.5f, 0.5f);

	}




}
