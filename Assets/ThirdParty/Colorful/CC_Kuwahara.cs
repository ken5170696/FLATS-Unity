using System;
using UnityEngine;
[ExecuteInEditMode]
[AddComponentMenu("Colorful/Kuwahara")]
public class CC_Kuwahara : CC_Base
{
	[Range(1f, 4f)]
	public int radius;

	protected Camera m_Camera;

	protected override void Start()
	{
		base.Start();
		m_Camera = GetComponent<Camera>();
	}

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		radius = Mathf.Clamp(radius, 1, 4);
		base.material.SetVector("_TexelSize", new Vector2(1f / m_Camera.pixelWidth, 1f / m_Camera.pixelHeight));
		Graphics.Blit(source, destination, base.material, radius - 1);
	}

	public CC_Kuwahara()
	{
		radius = 3;

	}




}
