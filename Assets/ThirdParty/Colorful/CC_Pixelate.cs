using System;
using UnityEngine;
[AddComponentMenu("Colorful/Pixelate")]
[ExecuteInEditMode]
public class CC_Pixelate : CC_Base
{
	[Range(1f, 1024f)]
	public float scale;

	public bool automaticRatio;

	public float ratio;

	public int mode;

	protected Camera m_Camera;

	protected override void Start()
	{
		base.Start();
		m_Camera = GetComponent<Camera>();
	}

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		switch (mode)
		{
		case 0:
			base.material.SetFloat("_Scale", scale);
			break;
		default:
			base.material.SetFloat("_Scale", m_Camera.pixelWidth / scale);
			break;
		}
		base.material.SetFloat("_Ratio", automaticRatio ? (m_Camera.pixelWidth / m_Camera.pixelHeight) : ratio);
		Graphics.Blit(source, destination, base.material);
	}

	public CC_Pixelate()
	{
		scale = 80f;
		ratio = 1f;

	}




}
