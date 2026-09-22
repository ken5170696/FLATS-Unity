using System;
using UnityEngine;
[ExecuteInEditMode]
[AddComponentMenu("Image Effects/Displacement/Vortex")]
public class VortexEffect : ImageEffectBase
{
	public Vector2 radius;

	public float angle;

	public Vector2 center;

	private void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		ImageEffects.RenderDistortion(base.material, source, destination, angle, center, radius);
	}

	public VortexEffect()
	{
		radius = new Vector2(0.4f, 0.4f);
		angle = 50f;
		center = new Vector2(0.5f, 0.5f);

	}




}
