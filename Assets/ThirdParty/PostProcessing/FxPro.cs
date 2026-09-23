using System;
using System.Collections.Generic;
using FxProNS;
using UnityEngine;
[AddComponentMenu("Image Effects/FxPro™")]
[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class FxPro : MonoBehaviour
{
	private const bool VisualizeLensCurvature = false;

	public EffectsQuality Quality;

	private static Material _mat;

	private static Material _tapMat;

	public bool BloomEnabled;

	public BloomHelperParams BloomParams;

	public bool VisualizeBloom;

	public Texture2D LensDirtTexture;

	[Range(0f, 2f)]
	public float LensDirtIntensity;

	public bool ChromaticAberration;

	public bool ChromaticAberrationPrecise;

	[Range(1f, 2.5f)]
	public float ChromaticAberrationOffset;

	[Range(0f, 1f)]
	public float SCurveIntensity;

	public bool LensCurvatureEnabled;

	[Range(1f, 2f)]
	public float LensCurvaturePower;

	public bool LensCurvaturePrecise;

	[Range(0f, 1f)]
	public float FilmGrainIntensity;

	[Range(1f, 10f)]
	public float FilmGrainTiling;

	[Range(0f, 1f)]
	public float VignettingIntensity;

	public bool DOFEnabled;

	public bool BlurCOCTexture;

	public DOFHelperParams DOFParams;

	public bool VisualizeCOC;

	private Texture2D _gridTexture;

	private List<Texture2D> _filmGrainTextures;

	public bool ColorEffectsEnabled;

	public Color CloseTint;

	public Color FarTint;

	[Range(0f, 1f)]
	public float CloseTintStrength;

	[Range(0f, 1f)]
	public float FarTintStrength;

	[Range(0f, 2f)]
	public float DesaturateDarksStrength;

	[Range(0f, 1f)]
	public float DesaturateFarObjsStrength;

	public Color FogTint;

	[Range(0f, 1f)]
	public float FogStrength;

	public static Material Mat
	{
		get
		{
			if (null == _mat)
			{
				Material material = new Material(Shader.Find("Hidden/FxPro"));
				material.hideFlags = HideFlags.HideAndDontSave;
				_mat = material;
			}
			return _mat;
		}
	}

	private static Material TapMat
	{
		get
		{
			if (null == _tapMat)
			{
				Material material = new Material(Shader.Find("Hidden/FxProTap"));
				material.hideFlags = HideFlags.HideAndDontSave;
				_tapMat = material;
			}
			return _tapMat;
		}
	}

	public void Start()
	{
		// URP executes this serialized control through FlatsPostProcessFeature.
		if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null) return;
		_filmGrainTextures = new List<Texture2D>();
		for (int i = 1; i <= 4; i++)
		{
			string text = "filmgrain_0" + i;
			Texture2D texture2D = Resources.Load(text) as Texture2D;
			if (null == texture2D)
			{
				Debug.LogError("Unable to load grain texture '" + text + "'");
			}
			else
			{
				_filmGrainTextures.Add(texture2D);
			}
		}
	}

	public void Init(bool searchForNonDepthmapAlphaObjects)
	{
		// URP executes this serialized control through FlatsPostProcessFeature.
		if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null) return;
		if (!CheckEffectSupport()) return;
		Mat.SetFloat("_DirtIntensity", Mathf.Exp(LensDirtIntensity) - 1f);
		if (null == LensDirtTexture || LensDirtIntensity <= 0f)
		{
			Mat.DisableKeyword("LENS_DIRT_ON");
			Mat.EnableKeyword("LENS_DIRT_OFF");
		}
		else
		{
			Mat.SetTexture("_LensDirtTex", LensDirtTexture);
			Mat.EnableKeyword("LENS_DIRT_ON");
			Mat.DisableKeyword("LENS_DIRT_OFF");
		}
		if (ChromaticAberration)
		{
			Mat.EnableKeyword("CHROMATIC_ABERRATION_ON");
			Mat.DisableKeyword("CHROMATIC_ABERRATION_OFF");
		}
		else
		{
			Mat.EnableKeyword("CHROMATIC_ABERRATION_OFF");
			Mat.DisableKeyword("CHROMATIC_ABERRATION_ON");
		}
		if (base.GetComponent<Camera>().allowHDR)
		{
			Shader.EnableKeyword("FXPRO_HDR_ON");
			Shader.DisableKeyword("FXPRO_HDR_OFF");
		}
		else
		{
			Shader.EnableKeyword("FXPRO_HDR_OFF");
			Shader.DisableKeyword("FXPRO_HDR_ON");
		}
		Mat.SetFloat("_SCurveIntensity", SCurveIntensity);
		if (DOFEnabled)
		{
			if (null == DOFParams.EffectCamera)
			{
				DOFParams.EffectCamera = GetComponent<Camera>();
			}
			DOFParams.DepthCompression = Mathf.Clamp(DOFParams.DepthCompression, 2f, 8f);
			Singleton<DOFHelper>.Instance.SetParams(DOFParams);
			Singleton<DOFHelper>.Instance.Init(searchForNonDepthmapAlphaObjects);
			Mat.DisableKeyword("DOF_DISABLED");
			Mat.EnableKeyword("DOF_ENABLED");
			if (!DOFParams.DoubleIntensityBlur)
			{
				Singleton<DOFHelper>.Instance.SetBlurRadius((Quality == EffectsQuality.Fastest || Quality == EffectsQuality.Fast) ? 3 : 5);
			}
			else
			{
				Singleton<DOFHelper>.Instance.SetBlurRadius((Quality == EffectsQuality.Fastest || Quality == EffectsQuality.Fast) ? 5 : 10);
			}
		}
		else
		{
			Mat.EnableKeyword("DOF_DISABLED");
			Mat.DisableKeyword("DOF_ENABLED");
		}
		if (BloomEnabled)
		{
			BloomParams.Quality = Quality;
			Singleton<BloomHelper>.Instance.SetParams(BloomParams);
			Singleton<BloomHelper>.Instance.Init();
			Mat.DisableKeyword("BLOOM_DISABLED");
			Mat.EnableKeyword("BLOOM_ENABLED");
		}
		else
		{
			Mat.EnableKeyword("BLOOM_DISABLED");
			Mat.DisableKeyword("BLOOM_ENABLED");
		}
		if (LensCurvatureEnabled)
		{
			UpdateLensCurvatureZoom();
			Mat.SetFloat("_LensCurvatureBarrelPower", LensCurvaturePower);
		}
		if (FilmGrainIntensity >= 0.001f)
		{
			Mat.SetFloat("_FilmGrainIntensity", FilmGrainIntensity);
			Mat.SetFloat("_FilmGrainTiling", FilmGrainTiling);
			Mat.EnableKeyword("FILM_GRAIN_ON");
			Mat.DisableKeyword("FILM_GRAIN_OFF");
		}
		else
		{
			Mat.EnableKeyword("FILM_GRAIN_OFF");
			Mat.DisableKeyword("FILM_GRAIN_ON");
		}
		if (VignettingIntensity <= 1f)
		{
			Mat.SetFloat("_VignettingIntensity", VignettingIntensity);
			Mat.EnableKeyword("VIGNETTING_ON");
			Mat.DisableKeyword("VIGNETTING_OFF");
		}
		else
		{
			Mat.EnableKeyword("VIGNETTING_OFF");
			Mat.DisableKeyword("VIGNETTING_ON");
		}
		Mat.SetFloat("_ChromaticAberrationOffset", ChromaticAberrationOffset);
		if (ColorEffectsEnabled)
		{
			Mat.EnableKeyword("COLOR_FX_ON");
			Mat.DisableKeyword("COLOR_FX_OFF");
			Mat.SetColor("_CloseTint", CloseTint);
			Mat.SetColor("_FarTint", FarTint);
			Mat.SetFloat("_CloseTintStrength", CloseTintStrength);
			Mat.SetFloat("_FarTintStrength", FarTintStrength);
			Mat.SetFloat("_DesaturateDarksStrength", DesaturateDarksStrength);
			Mat.SetFloat("_DesaturateFarObjsStrength", DesaturateFarObjsStrength);
			Mat.SetColor("_FogTint", FogTint);
			Mat.SetFloat("_FogStrength", FogStrength);
		}
		else
		{
			Mat.EnableKeyword("COLOR_FX_OFF");
			Mat.DisableKeyword("COLOR_FX_ON");
		}
	}

	public void OnEnable()
	{
		Init(true);
	}

	public void OnDisable()
	{
		// URP executes this serialized control through FlatsPostProcessFeature.
		if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null) return;
		if (null != _mat)
		{
			UnityEngine.Object.DestroyImmediate(_mat);
		}
		RenderTextureManager.Instance.Dispose();
		Singleton<DOFHelper>.Instance.Dispose();
		Singleton<BloomHelper>.Instance.Dispose();
	}

	public void OnValidate()
	{
		Init(false);
	}

	private bool supportWarningReported;
	private bool CheckEffectSupport()
	{
		// License tier is unrelated to image-effect support in supported Unity versions.
		string missing = null;
		foreach (string shaderName in new[] { "Hidden/FxPro", "Hidden/FxProTap",
			DOFEnabled ? "Hidden/DOFPro" : null, BloomEnabled ? "Hidden/BloomPro" : null })
		{
			if (shaderName == null) continue;
			Shader shader = Shader.Find(shaderName);
			if (shader == null || !shader.isSupported) { missing = shaderName; break; }
		}
		if (DOFEnabled && !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth))
			missing = "depth render texture";
		if (missing == null) return true;
		if (!supportWarningReported)
		{
			Debug.LogWarning("FxPro disabled: unavailable or unsupported " + missing, this);
			supportWarningReported = true;
		}
		enabled = false;
		return false;
	}

	public static RenderTexture DownsampleTex(RenderTexture input, float downsampleBy)
	{
		RenderTexture renderTexture = RenderTextureManager.Instance.RequestRenderTexture(Mathf.RoundToInt((float)input.width / downsampleBy), Mathf.RoundToInt((float)input.height / downsampleBy), input.depth, input.format);
		renderTexture.filterMode = FilterMode.Bilinear;
		Graphics.BlitMultiTap(input, renderTexture, TapMat, new Vector2(-1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(1f, -1f));
		return renderTexture;
	}

	private RenderTexture ApplyColorEffects(RenderTexture input)
	{
		if (!ColorEffectsEnabled)
		{
			return input;
		}
		RenderTexture renderTexture = RenderTextureManager.Instance.RequestRenderTexture(input.width, input.height, input.depth, input.format);
		Graphics.Blit(input, renderTexture, Mat, 5);
		return renderTexture;
	}

	private RenderTexture ApplyLensCurvature(RenderTexture input)
	{
		if (!LensCurvatureEnabled)
		{
			return input;
		}
		RenderTexture renderTexture = RenderTextureManager.Instance.RequestRenderTexture(input.width, input.height, input.depth, input.format);
		Graphics.Blit(input, renderTexture, Mat, LensCurvaturePrecise ? 3 : 4);
		return renderTexture;
	}

	private RenderTexture ApplyChromaticAberration(RenderTexture input)
	{
		if (!ChromaticAberration)
		{
			return null;
		}
		RenderTexture renderTexture = RenderTextureManager.Instance.RequestRenderTexture(input.width, input.height, input.depth, input.format);
		renderTexture.filterMode = FilterMode.Bilinear;
		Graphics.Blit(input, renderTexture, Mat, 2);
		Mat.SetTexture("_ChromAberrTex", renderTexture);
		return renderTexture;
	}

	private Vector2 ApplyLensCurvature(Vector2 uv, float barrelPower, bool precise)
	{
		uv = uv * 2f - Vector2.one;
		uv.x *= base.GetComponent<Camera>().aspect * 2f;
		float f = Mathf.Atan2(uv.y, uv.x);
		float magnitude = uv.magnitude;
		magnitude = ((!precise) ? Mathf.Lerp(magnitude, magnitude * magnitude, Mathf.Clamp01(barrelPower - 1f)) : Mathf.Pow(magnitude, barrelPower));
		uv.x = magnitude * Mathf.Cos(f);
		uv.y = magnitude * Mathf.Sin(f);
		uv.x /= base.GetComponent<Camera>().aspect * 2f;
		return 0.5f * (uv + Vector2.one);
	}

	private void UpdateLensCurvatureZoom()
	{
		float value = 1f / ApplyLensCurvature(new Vector2(1f, 1f), LensCurvaturePower, LensCurvaturePrecise).x;
		Mat.SetFloat("_LensCurvatureZoom", value);
	}

	private void UpdateFilmGrain()
	{
		if (FilmGrainIntensity >= 0.001f)
		{
			int index = UnityEngine.Random.Range(0, 3);
			Mat.SetTexture("_FilmGrainTex", _filmGrainTextures[index]);
			switch (UnityEngine.Random.Range(0, 3))
			{
			case 0:
				Mat.SetVector("_FilmGrainChannel", new Vector4(1f, 0f, 0f, 0f));
				break;
			case 1:
				Mat.SetVector("_FilmGrainChannel", new Vector4(0f, 1f, 0f, 0f));
				break;
			case 2:
				Mat.SetVector("_FilmGrainChannel", new Vector4(0f, 0f, 1f, 0f));
				break;
			case 3:
				Mat.SetVector("_FilmGrainChannel", new Vector4(0f, 0f, 0f, 1f));
				break;
			}
		}
	}

	private void RenderEffects(RenderTexture source, RenderTexture destination)
	{
		source.filterMode = FilterMode.Bilinear;
		UpdateFilmGrain();
		RenderTexture tex = source;
		RenderTexture a = source;
		RenderTexture a2 = ApplyColorEffects(source);
		RenderTextureManager.Instance.SafeAssign(ref a2, ApplyLensCurvature(a2));
		if (ChromaticAberrationPrecise)
		{
			tex = ApplyChromaticAberration(a2);
		}
		RenderTextureManager.Instance.SafeAssign(ref a, DownsampleTex(a2, 2f));
		if (Quality == EffectsQuality.Fastest)
		{
			RenderTextureManager.Instance.SafeAssign(ref a, DownsampleTex(a, 2f));
		}
		RenderTexture renderTexture = null;
		RenderTexture renderTexture2 = null;
		if (DOFEnabled)
		{
			if (null == DOFParams.EffectCamera)
			{
				Debug.LogError("null == DOFParams.camera");
				return;
			}
			renderTexture = RenderTextureManager.Instance.RequestRenderTexture(a.width, a.height, a.depth, a.format);
			Singleton<DOFHelper>.Instance.RenderCOCTexture(a, renderTexture, BlurCOCTexture ? 1.5f : 0f);
			if (VisualizeCOC)
			{
				Graphics.Blit(renderTexture, destination, DOFHelper.Mat, 3);
				RenderTextureManager.Instance.ReleaseRenderTexture(renderTexture);
				RenderTextureManager.Instance.ReleaseRenderTexture(a);
				return;
			}
			renderTexture2 = RenderTextureManager.Instance.RequestRenderTexture(a.width, a.height, a.depth, a.format);
			Singleton<DOFHelper>.Instance.RenderDOFBlur(a, renderTexture2, renderTexture);
			Mat.SetTexture("_DOFTex", renderTexture2);
			Mat.SetTexture("_COCTex", renderTexture);
			Graphics.Blit(renderTexture2, destination);
		}
		if (!ChromaticAberrationPrecise)
		{
			tex = ApplyChromaticAberration(a);
		}
		if (BloomEnabled)
		{
			RenderTexture renderTexture3 = RenderTextureManager.Instance.RequestRenderTexture(a.width, a.height, a.depth, a.format);
			Singleton<BloomHelper>.Instance.RenderBloomTexture(a, renderTexture3);
			Mat.SetTexture("_BloomTex", renderTexture3);
			if (VisualizeBloom)
			{
				Graphics.Blit(renderTexture3, destination);
				return;
			}
		}
		Graphics.Blit(a2, destination, Mat, 0);
		RenderTextureManager.Instance.ReleaseRenderTexture(renderTexture);
		RenderTextureManager.Instance.ReleaseRenderTexture(renderTexture2);
		RenderTextureManager.Instance.ReleaseRenderTexture(a);
		RenderTextureManager.Instance.ReleaseRenderTexture(tex);
	}

	[ImageEffectTransformsToLDR]
	public void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		RenderEffects(source, destination);
		RenderTextureManager.Instance.ReleaseAllRenderTextures();
	}

	public FxPro()
	{
		Quality = EffectsQuality.Normal;
		BloomEnabled = true;
		BloomParams = new BloomHelperParams();
		LensDirtIntensity = 1f;
		ChromaticAberration = true;
		ChromaticAberrationOffset = 1f;
		SCurveIntensity = 0.5f;
		LensCurvatureEnabled = true;
		LensCurvaturePower = 1.1f;
		FilmGrainIntensity = 0.5f;
		FilmGrainTiling = 4f;
		VignettingIntensity = 0.5f;
		DOFEnabled = true;
		BlurCOCTexture = true;
		DOFParams = new DOFHelperParams();
		ColorEffectsEnabled = true;
		CloseTint = new Color(1f, 0.5f, 0f, 1f);
		FarTint = new Color(0f, 0f, 1f, 1f);
		CloseTintStrength = 0.5f;
		FarTintStrength = 0.5f;
		DesaturateDarksStrength = 0.5f;
		DesaturateFarObjsStrength = 0.5f;
		FogTint = Color.white;
		FogStrength = 0.5f;

	}




}
