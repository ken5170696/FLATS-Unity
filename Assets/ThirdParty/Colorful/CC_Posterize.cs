using System;
using UnityEngine;
[ExecuteInEditMode]
[AddComponentMenu("Colorful/Posterize")]
public class CC_Posterize : CC_Base
{
	[Range(2f, 255f)]
	public int levels;

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		base.material.SetFloat("_Levels", levels);
		Graphics.Blit(source, destination, base.material);
	}

	public CC_Posterize()
	{
		levels = 4;

	}




}
