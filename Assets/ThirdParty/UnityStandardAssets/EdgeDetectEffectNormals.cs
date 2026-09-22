using System;
using UnityEngine;
[Serializable]
[AddComponentMenu("Image Effects/Edge Detection/Edge Detection")]
[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class EdgeDetectEffectNormals : PostEffectsBase
{
	public EdgeDetectMode mode;

	public float sensitivityDepth;

	public float sensitivityNormals;

	public float lumThreshhold;

	public float edgeExp;

	public float sampleDist;

	public float edgesOnly;

	public Color edgesOnlyBgColor;

	public Shader edgeDetectShader;

	private Material edgeDetectMaterial;

	private EdgeDetectMode oldMode;
	private bool supportWarningReported;

	public EdgeDetectEffectNormals()
	{
		mode = EdgeDetectMode.SobelDepthThin;
		sensitivityDepth = 1f;
		sensitivityNormals = 1f;
		lumThreshhold = 0.2f;
		edgeExp = 1f;
		sampleDist = 1f;
		edgesOnlyBgColor = Color.white;
		oldMode = EdgeDetectMode.SobelDepthThin;
	}

	public override bool CheckResources()
	{
		if (!CheckSupport(true) || edgeDetectShader == null || !edgeDetectShader.isSupported)
		{
			NotSupported();
			if (!supportWarningReported)
			{
				Debug.LogWarning("Edge detection disabled: missing/unsupported edge shader or depth render texture.", this);
				supportWarningReported = true;
			}
			return false;
		}
		edgeDetectMaterial = CheckShaderAndCreateMaterial(edgeDetectShader, edgeDetectMaterial);
		if (mode != oldMode)
		{
			SetCameraFlag();
		}
		oldMode = mode;
		if (!isSupported && !supportWarningReported)
		{
			ReportAutoDisable();
			supportWarningReported = true;
		}
		return isSupported && edgeDetectMaterial != null;
	}

	public override void Start()
	{
		oldMode = mode;
	}

	public virtual void SetCameraFlag()
	{
		if (mode == EdgeDetectMode.SobelDepth || mode == EdgeDetectMode.SobelDepthThin)
		{
			GetComponent<Camera>().depthTextureMode |= DepthTextureMode.Depth;
		}
		else if (mode == EdgeDetectMode.TriangleDepthNormals || mode == EdgeDetectMode.RobertsCrossDepthNormals)
		{
			GetComponent<Camera>().depthTextureMode |= DepthTextureMode.DepthNormals;
		}
	}

	public override void OnEnable()
	{
		SetCameraFlag();
	}

	[ImageEffectOpaque]
	public virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (!CheckResources())
		{
			Graphics.Blit(source, destination);
			return;
		}
		Vector2 vector = new Vector2(sensitivityDepth, sensitivityNormals);
		edgeDetectMaterial.SetVector("_Sensitivity", new Vector4(vector.x, vector.y, 1f, vector.y));
		edgeDetectMaterial.SetFloat("_BgFade", edgesOnly);
		edgeDetectMaterial.SetFloat("_SampleDistance", sampleDist);
		edgeDetectMaterial.SetVector("_BgColor", edgesOnlyBgColor);
		edgeDetectMaterial.SetFloat("_Exponent", edgeExp);
		edgeDetectMaterial.SetFloat("_Threshold", lumThreshhold);
		Graphics.Blit(source, destination, edgeDetectMaterial, (int)mode);
	}

	public override void Main()
	{
	}




}
