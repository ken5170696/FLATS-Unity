using System;
using UnityEngine;
[ExecuteInEditMode]
[AddComponentMenu("Colorful/Vibrance")]
public class CC_Vibrance : CC_Base
{
	[Range(-100f, 100f)]
	public float amount;

	[Range(-5f, 5f)]
	public float redChannel;

	[Range(-5f, 5f)]
	public float greenChannel;

	[Range(-5f, 5f)]
	public float blueChannel;

	public bool advanced;

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (amount == 0f)
		{
			Graphics.Blit(source, destination);
		}
		else if (advanced)
		{
			base.material.SetFloat("_Amount", amount * 0.01f);
			base.material.SetVector("_Channels", new Vector3(redChannel, greenChannel, blueChannel));
			Graphics.Blit(source, destination, base.material, 1);
		}
		else
		{
			base.material.SetFloat("_Amount", amount * 0.02f);
			Graphics.Blit(source, destination, base.material, 0);
		}
	}

	public CC_Vibrance()
	{
		redChannel = 1f;
		greenChannel = 1f;
		blueChannel = 1f;

	}




}
