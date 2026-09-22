using System;
using UnityEngine;
[AddComponentMenu("Colorful/Technicolor")]
[ExecuteInEditMode]
public class CC_Technicolor : CC_Base
{
	[Range(0f, 8f)]
	public float exposure;

	public Vector3 balance;

	[Range(0f, 1f)]
	public float amount;

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		base.material.SetFloat("_Exposure", 8f - exposure);
		base.material.SetVector("_Balance", Vector3.one - balance);
		base.material.SetFloat("_Amount", amount);
		Graphics.Blit(source, destination, base.material);
	}

	public CC_Technicolor()
	{
		exposure = 4f;
		balance = new Vector3(0.25f, 0.25f, 0.25f);
		amount = 0.5f;

	}




}
