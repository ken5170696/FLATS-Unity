using System;
using UnityEngine;
[Serializable]
[AddComponentMenu("Image Effects/Rendering/Screen Space Ambient Obscurance")]
[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class AmbientObscurance : PostEffectsBase
{
	[Range(0f, 3f)]
	public float intensity;

	[Range(0.1f, 3f)]
	public float radius;

	[Range(0f, 3f)]
	public int blurIterations;

	[Range(0f, 5f)]
	public float blurFilterDistance;

	[Range(0f, 1f)]
	public int downsample;

	public Texture2D rand;

	public Shader aoShader;

	private Material aoMaterial;

	public AmbientObscurance()
	{
		intensity = 0.5f;
		radius = 0.2f;
		blurIterations = 1;
		blurFilterDistance = 1.25f;
	}

	public override bool CheckResources()
	{
		CheckSupport(true);
		aoMaterial = CheckShaderAndCreateMaterial(aoShader, aoMaterial);
		if (!isSupported)
		{
			ReportAutoDisable();
		}
		return isSupported;
	}

	public virtual void OnDisable()
	{
		if ((bool)aoMaterial)
		{
			UnityEngine.Object.DestroyImmediate(aoMaterial);
		}
		aoMaterial = null;
	}

	[ImageEffectOpaque]
	public virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (!CheckResources())
		{
			Graphics.Blit(source, destination);
			return;
		}
		Matrix4x4 projectionMatrix = GetComponent<Camera>().projectionMatrix;
		Matrix4x4 inverse = projectionMatrix.inverse;
		Vector4 vector = new Vector4(-2f / ((float)Screen.width * projectionMatrix[0]), -2f / ((float)Screen.height * projectionMatrix[5]), (1f - projectionMatrix[2]) / projectionMatrix[0], (1f + projectionMatrix[6]) / projectionMatrix[5]);
		aoMaterial.SetVector("_ProjInfo", vector);
		aoMaterial.SetMatrix("_ProjectionInv", inverse);
		aoMaterial.SetTexture("_Rand", rand);
		aoMaterial.SetFloat("_Radius", radius);
		aoMaterial.SetFloat("_Radius2", radius * radius);
		aoMaterial.SetFloat("_Intensity", intensity);
		aoMaterial.SetFloat("_BlurFilterDistance", blurFilterDistance);
		int width = source.width;
		int height = source.height;
		RenderTexture renderTexture = RenderTexture.GetTemporary(width >> downsample, height >> downsample);
		RenderTexture renderTexture2 = null;
		Graphics.Blit(source, renderTexture, aoMaterial, 0);
		if (downsample > 0)
		{
			renderTexture2 = RenderTexture.GetTemporary(width, height);
			Graphics.Blit(renderTexture, renderTexture2, aoMaterial, 4);
			RenderTexture.ReleaseTemporary(renderTexture);
			renderTexture = renderTexture2;
		}
		for (int i = 0; i < blurIterations; i++)
		{
			aoMaterial.SetVector("_Axis", new Vector2(1f, 0f));
			renderTexture2 = RenderTexture.GetTemporary(width, height);
			Graphics.Blit(renderTexture, renderTexture2, aoMaterial, 1);
			RenderTexture.ReleaseTemporary(renderTexture);
			aoMaterial.SetVector("_Axis", new Vector2(0f, 1f));
			renderTexture = RenderTexture.GetTemporary(width, height);
			Graphics.Blit(renderTexture2, renderTexture, aoMaterial, 1);
			RenderTexture.ReleaseTemporary(renderTexture2);
		}
		aoMaterial.SetTexture("_AOTex", renderTexture);
		Graphics.Blit(source, destination, aoMaterial, 2);
		RenderTexture.ReleaseTemporary(renderTexture);
	}

	public override void Main()
	{
	}




}
