using System;
using UnityEngine;
[ExecuteInEditMode]
[AddComponentMenu("Colorful/White Balance")]
public class CC_WhiteBalance : CC_Base
{
	public Color white;

	public int mode;

	protected virtual void Reset()
	{
		white = (CC_Base.IsLinear() ? new Color((float)Math.PI * 59f / 254f, (float)Math.PI * 59f / 254f, (float)Math.PI * 59f / 254f) : new Color(0.5f, 0.5f, 0.5f));
	}

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		base.material.SetColor("_White", white);
		Graphics.Blit(source, destination, base.material, mode);
	}

	public CC_WhiteBalance()
	{
		white = new Color(0.5f, 0.5f, 0.5f);
		mode = 1;

	}




}
