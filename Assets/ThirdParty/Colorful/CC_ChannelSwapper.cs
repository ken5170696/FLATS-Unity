using System;
using UnityEngine;
[ExecuteInEditMode]
[AddComponentMenu("Colorful/Channel Swapper")]
public class CC_ChannelSwapper : CC_Base
{
	public int red;

	public int green;

	public int blue;

	private static Vector4[] m_Channels = new Vector4[3]
	{
		new Vector4(1f, 0f, 0f, 0f),
		new Vector4(0f, 1f, 0f, 0f),
		new Vector4(0f, 0f, 1f, 0f)
	};

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		base.material.SetVector("_Red", m_Channels[red]);
		base.material.SetVector("_Green", m_Channels[green]);
		base.material.SetVector("_Blue", m_Channels[blue]);
		Graphics.Blit(source, destination, base.material);
	}

	public CC_ChannelSwapper()
	{
		green = 1;
		blue = 2;

	}




}
