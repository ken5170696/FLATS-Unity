using System;
using UnityEngine;
[ExecuteInEditMode]
[AddComponentMenu("Colorful/Halftone")]
public class CC_Halftone : CC_Base
{
	[Range(0f, 512f)]
	public float density;

	public int mode;

	public bool antialiasing;

	public bool showOriginal;

	protected Camera m_Camera;

	protected override void Start()
	{
		base.Start();
		m_Camera = GetComponent<Camera>();
	}

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		base.material.SetFloat("_Density", density);
		base.material.SetFloat("_AspectRatio", m_Camera.aspect);
		int pass = 0;
		if (mode == 0)
		{
			if (antialiasing && showOriginal)
			{
				pass = 3;
			}
			else if (antialiasing)
			{
				pass = 1;
			}
			else if (showOriginal)
			{
				pass = 2;
			}
		}
		else if (mode == 1)
		{
			pass = (antialiasing ? 5 : 4);
		}
		Graphics.Blit(source, destination, base.material, pass);
	}

	public CC_Halftone()
	{
		density = 64f;
		mode = 1;
		antialiasing = true;

	}




}
