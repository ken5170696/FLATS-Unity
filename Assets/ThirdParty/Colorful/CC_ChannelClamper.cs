using System;
using UnityEngine;
[AddComponentMenu("Colorful/Channel Clamper")]
[ExecuteInEditMode]
public class CC_ChannelClamper : CC_Base
{
	public Vector2 red;

	public Vector2 green;

	public Vector2 blue;

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		base.material.SetVector("_RedClamp", red);
		base.material.SetVector("_GreenClamp", green);
		base.material.SetVector("_BlueClamp", blue);
		Graphics.Blit(source, destination, base.material);
	}

	public CC_ChannelClamper()
	{
		red = new Vector2(0f, 1f);
		green = new Vector2(0f, 1f);
		blue = new Vector2(0f, 1f);

	}




}
