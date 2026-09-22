using System;
using UnityEngine;
[ExecuteInEditMode]
[AddComponentMenu("Colorful/LED")]
public class CC_Led : CC_Base
{
	[Range(1f, 255f)]
	public float scale;

	[Range(0f, 10f)]
	public float brightness;

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
		base.material.SetFloat("_Brightness", brightness);
		Graphics.Blit(source, destination, base.material);
	}

	public CC_Led()
	{
		scale = 80f;
		brightness = 1f;
		ratio = 1f;

	}




}
