using System;
using UnityEngine;
[ExecuteInEditMode]
[AddComponentMenu("Colorful/Convolution Matrix 3x3")]
public class CC_Convolution3x3 : CC_Base
{
	public Vector3 kernelTop;

	public Vector3 kernelMiddle;

	public Vector3 kernelBottom;

	public float divisor;

	[Range(0f, 1f)]
	public float amount;

	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		base.material.SetFloat("_PX", 1f / (float)Screen.width);
		base.material.SetFloat("_PY", 1f / (float)Screen.height);
		base.material.SetFloat("_Amount", amount);
		base.material.SetVector("_KernelT", kernelTop / divisor);
		base.material.SetVector("_KernelM", kernelMiddle / divisor);
		base.material.SetVector("_KernelB", kernelBottom / divisor);
		Graphics.Blit(source, destination, base.material);
	}

	public CC_Convolution3x3()
	{
		kernelTop = Vector3.zero;
		kernelMiddle = Vector3.up;
		kernelBottom = Vector3.zero;
		divisor = 1f;
		amount = 1f;

	}




}
