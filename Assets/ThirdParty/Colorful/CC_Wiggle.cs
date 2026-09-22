using System;
using UnityEngine;
[ExecuteInEditMode]
[AddComponentMenu("Colorful/Wiggle")]
public class CC_Wiggle : CC_Base
{
	public float timer;

	public float speed;

	public float scale;

	public bool autoTimer;

	protected virtual void Update()
	{
		if (autoTimer)
		{
			timer += speed * Time.deltaTime;
		}
	}

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		base.material.SetFloat("_Timer", timer);
		base.material.SetFloat("_Scale", scale);
		Graphics.Blit(source, destination, base.material);
	}

	public CC_Wiggle()
	{
		speed = 1f;
		scale = 12f;
		autoTimer = true;

	}




}
