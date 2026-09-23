using System;
using UnityEngine;
[AddComponentMenu("Image Effects/FXAA")]
[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class FXAA : FXAAPostEffectsBase
{
	public Shader shader;

	private Material mat;

	private void CreateMaterials()
	{
		if (mat == null)
		{
			mat = CheckShaderAndCreateMaterial(shader, mat);
		}
	}

	private void Start()
	{
		// URP executes this serialized control through FlatsPostProcessFeature.
		if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null) return;
		shader = Shader.Find("Hidden/FXAA3");
		CreateMaterials();
		CheckSupport(false);
	}

	public void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		CreateMaterials();
		float num = 1f / (float)Screen.width;
		float num2 = 1f / (float)Screen.height;
		mat.SetVector("_rcpFrame", new Vector4(num, num2, 0f, 0f));
		mat.SetVector("_rcpFrameOpt", new Vector4(num * 2f, num2 * 2f, num * 0.5f, num2 * 0.5f));
		Graphics.Blit(source, destination, mat);
	}

	public FXAA()
	{
	}




}
