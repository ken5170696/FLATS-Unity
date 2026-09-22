using System;
using UnityEngine;
[AddComponentMenu("Image Effects/Noise/Noise and Scratches")]
[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class NoiseEffect : MonoBehaviour
{
	public bool monochrome;

	private bool rgbFallback;

	public float grainIntensityMin;

	public float grainIntensityMax;

	public float grainSize;

	public float scratchIntensityMin;

	public float scratchIntensityMax;

	public float scratchFPS;

	public float scratchJitter;

	public Texture grainTexture;

	public Texture scratchTexture;

	public Shader shaderRGB;

	public Shader shaderYUV;

	private Material m_MaterialRGB;

	private Material m_MaterialYUV;

	private float scratchTimeLeft;

	private float scratchX;

	private float scratchY;

	protected Material material
	{
		get
		{
			if (m_MaterialRGB == null)
			{
				m_MaterialRGB = new Material(shaderRGB);
				m_MaterialRGB.hideFlags = HideFlags.HideAndDontSave;
			}
			if (m_MaterialYUV == null && !rgbFallback)
			{
				m_MaterialYUV = new Material(shaderYUV);
				m_MaterialYUV.hideFlags = HideFlags.HideAndDontSave;
			}
			if (rgbFallback || monochrome)
			{
				return m_MaterialRGB;
			}
			return m_MaterialYUV;
		}
	}

	protected void Start()
	{
		if (!SystemInfo.supportsImageEffects)
		{
			base.enabled = false;
		}
		else if (shaderRGB == null || shaderYUV == null)
		{
			Debug.Log("Noise shaders are not set up! Disabling noise effect.");
			base.enabled = false;
		}
		else if (!shaderRGB.isSupported)
		{
			base.enabled = false;
		}
		else if (!shaderYUV.isSupported)
		{
			rgbFallback = true;
		}
	}

	protected void OnDisable()
	{
		if ((bool)m_MaterialRGB)
		{
			UnityEngine.Object.DestroyImmediate(m_MaterialRGB);
		}
		if ((bool)m_MaterialYUV)
		{
			UnityEngine.Object.DestroyImmediate(m_MaterialYUV);
		}
	}

	private void SanitizeParameters()
	{
		grainIntensityMin = Mathf.Clamp(grainIntensityMin, 0f, 5f);
		grainIntensityMax = Mathf.Clamp(grainIntensityMax, 0f, 5f);
		scratchIntensityMin = Mathf.Clamp(scratchIntensityMin, 0f, 5f);
		scratchIntensityMax = Mathf.Clamp(scratchIntensityMax, 0f, 5f);
		scratchFPS = Mathf.Clamp(scratchFPS, 1f, 30f);
		scratchJitter = Mathf.Clamp(scratchJitter, 0f, 1f);
		grainSize = Mathf.Clamp(grainSize, 0.1f, 50f);
	}

	private void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		SanitizeParameters();
		if (scratchTimeLeft <= 0f)
		{
			scratchTimeLeft = UnityEngine.Random.value * 2f / scratchFPS;
			scratchX = UnityEngine.Random.value;
			scratchY = UnityEngine.Random.value;
		}
		scratchTimeLeft -= Time.deltaTime;
		Material material = this.material;
		material.SetTexture("_GrainTex", grainTexture);
		material.SetTexture("_ScratchTex", scratchTexture);
		float num = 1f / grainSize;
		material.SetVector("_GrainOffsetScale", new Vector4(UnityEngine.Random.value, UnityEngine.Random.value, (float)Screen.width / (float)grainTexture.width * num, (float)Screen.height / (float)grainTexture.height * num));
		material.SetVector("_ScratchOffsetScale", new Vector4(scratchX + UnityEngine.Random.value * scratchJitter, scratchY + UnityEngine.Random.value * scratchJitter, (float)Screen.width / (float)scratchTexture.width, (float)Screen.height / (float)scratchTexture.height));
		material.SetVector("_Intensity", new Vector4(UnityEngine.Random.Range(grainIntensityMin, grainIntensityMax), UnityEngine.Random.Range(scratchIntensityMin, scratchIntensityMax), 0f, 0f));
		Graphics.Blit(source, destination, material);
	}

	public NoiseEffect()
	{
		monochrome = true;
		grainIntensityMin = 0.1f;
		grainIntensityMax = 0.2f;
		grainSize = 2f;
		scratchIntensityMin = 0.05f;
		scratchIntensityMax = 0.25f;
		scratchFPS = 10f;
		scratchJitter = 0.01f;

	}




}
