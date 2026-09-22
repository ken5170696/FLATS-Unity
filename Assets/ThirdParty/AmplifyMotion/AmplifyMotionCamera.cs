using System;
using System.Collections.Generic;
using AmplifyMotion;
using UnityEngine;
[RequireComponent(typeof(Camera))]
[AddComponentMenu("")]
public class AmplifyMotionCamera : MonoBehaviour
{
	internal AmplifyMotionEffectBase Instance;

	internal Matrix4x4 PrevViewProjMatrix;

	internal Matrix4x4 ViewProjMatrix;

	internal Matrix4x4 InvViewProjMatrix;

	internal Matrix4x4 PrevViewProjMatrixRT;

	internal Matrix4x4 ViewProjMatrixRT;

	internal Transform Transform;

	private bool m_linked;

	private bool m_initialized;

	private bool m_starting;

	private bool m_autoStep;

	private bool m_step;

	private bool m_overlay;

	private Camera m_camera;

	private int m_prevFrameCount;

	private HashSet<AmplifyMotionObjectBase> m_affectedObjectsTable;

	private AmplifyMotionObjectBase[] m_affectedObjects;

	private bool m_affectedObjectsChanged;

	public bool Initialized
	{
		get
		{
			return m_initialized;
		}
	}

	public bool AutoStep
	{
		get
		{
			return m_autoStep;
		}
	}

	public bool Overlay
	{
		get
		{
			return m_overlay;
		}
	}

	public Camera Camera
	{
		get
		{
			return m_camera;
		}
	}

	public void RegisterObject(AmplifyMotionObjectBase obj)
	{
		m_affectedObjectsTable.Add(obj);
		m_affectedObjectsChanged = true;
	}

	public void UnregisterObject(AmplifyMotionObjectBase obj)
	{
		m_affectedObjectsTable.Remove(obj);
		m_affectedObjectsChanged = true;
	}

	private void UpdateAffectedObjects()
	{
		if (m_affectedObjects == null || m_affectedObjectsTable.Count != m_affectedObjects.Length)
		{
			m_affectedObjects = new AmplifyMotionObjectBase[m_affectedObjectsTable.Count];
		}
		m_affectedObjectsTable.CopyTo(m_affectedObjects);
		m_affectedObjectsChanged = false;
	}

	public void LinkTo(AmplifyMotionEffectBase instance, bool overlay)
	{
		Instance = instance;
		m_camera = GetComponent<Camera>();
		m_camera.depthTextureMode |= DepthTextureMode.Depth;
		m_overlay = overlay;
		m_linked = true;
	}

	public void Initialize()
	{
		m_step = false;
		UpdateMatrices();
		m_initialized = true;
	}

	private void Awake()
	{
		Transform = base.transform;
	}

	private void OnEnable()
	{
		AmplifyMotionEffectBase.RegisterCamera(this);
	}

	private void OnDisable()
	{
		m_initialized = false;
		AmplifyMotionEffectBase.UnregisterCamera(this);
	}

	private void OnDestroy()
	{
		if (Instance != null)
		{
			Instance.RemoveCamera(m_camera);
		}
	}

	public void StopAutoStep()
	{
		if (m_autoStep)
		{
			m_autoStep = false;
			m_step = true;
		}
	}

	public void StartAutoStep()
	{
		m_autoStep = true;
	}

	public void Step()
	{
		m_step = true;
	}

	private void Update()
	{
		if (m_linked && Instance.isActiveAndEnabled)
		{
			if (!m_initialized)
			{
				Initialize();
			}
			if ((m_camera.depthTextureMode & DepthTextureMode.Depth) == 0)
			{
				m_camera.depthTextureMode |= DepthTextureMode.Depth;
			}
		}
	}

	private void UpdateMatrices()
	{
		if (!m_starting)
		{
			PrevViewProjMatrix = ViewProjMatrix;
			PrevViewProjMatrixRT = ViewProjMatrixRT;
		}
		Matrix4x4 worldToCameraMatrix = m_camera.worldToCameraMatrix;
		Matrix4x4 gPUProjectionMatrix = GL.GetGPUProjectionMatrix(m_camera.projectionMatrix, false);
		ViewProjMatrix = gPUProjectionMatrix * worldToCameraMatrix;
		InvViewProjMatrix = Matrix4x4.Inverse(ViewProjMatrix);
		Matrix4x4 gPUProjectionMatrix2 = GL.GetGPUProjectionMatrix(m_camera.projectionMatrix, true);
		ViewProjMatrixRT = gPUProjectionMatrix2 * worldToCameraMatrix;
		if (m_starting)
		{
			PrevViewProjMatrix = ViewProjMatrix;
			PrevViewProjMatrixRT = ViewProjMatrixRT;
		}
	}

	public void FixedUpdateTransform(AmplifyMotionEffectBase inst)
	{
		if (!m_initialized)
		{
			Initialize();
		}
		if (m_affectedObjectsChanged)
		{
			UpdateAffectedObjects();
		}
		for (int i = 0; i < m_affectedObjects.Length; i++)
		{
			if (m_affectedObjects[i].FixedStep)
			{
				m_affectedObjects[i].OnUpdateTransform(inst, m_camera, m_starting);
			}
		}
	}

	public void UpdateTransform(AmplifyMotionEffectBase inst)
	{
		if (!m_initialized)
		{
			Initialize();
		}
		if (Time.frameCount <= m_prevFrameCount || (!m_autoStep && !m_step))
		{
			return;
		}
		UpdateMatrices();
		if (m_affectedObjectsChanged)
		{
			UpdateAffectedObjects();
		}
		for (int i = 0; i < m_affectedObjects.Length; i++)
		{
			if (!m_affectedObjects[i].FixedStep)
			{
				m_affectedObjects[i].OnUpdateTransform(inst, m_camera, m_starting);
			}
		}
		m_starting = false;
		m_step = false;
		m_prevFrameCount = Time.frameCount;
	}

	public void RenderReprojectionVectors(RenderTexture destination, float scale)
	{
		Shader.SetGlobalMatrix("_AM_MATRIX_CURR_REPROJ", PrevViewProjMatrix * InvViewProjMatrix);
		Shader.SetGlobalFloat("_AM_MOTION_SCALE", scale);
		Graphics.Blit(null, destination, Instance.ReprojectionMaterial);
	}

	public void PreRenderVectors(RenderTexture motionRT, bool clearColor, float rcpDepthThreshold)
	{
		Graphics.SetRenderTarget(motionRT);
		GL.Clear(true, clearColor, Color.black);
	}

	public void RenderVectors(float scale, float fixedScale, Quality quality)
	{
		if (!m_initialized)
		{
			Initialize();
		}
		float nearClipPlane = m_camera.nearClipPlane;
		float farClipPlane = m_camera.farClipPlane;
		Vector4 vec = default(Vector4);
		if (AmplifyMotionEffectBase.IsD3D)
		{
			vec.x = 1f - farClipPlane / nearClipPlane;
			vec.y = farClipPlane / nearClipPlane;
		}
		else
		{
			vec.x = (1f - farClipPlane / nearClipPlane) / 2f;
			vec.y = (1f + farClipPlane / nearClipPlane) / 2f;
		}
		vec.z = vec.x / farClipPlane;
		vec.w = vec.y / farClipPlane;
		Shader.SetGlobalVector("_AM_ZBUFFER_PARAMS", vec);
		if (m_affectedObjectsChanged)
		{
			UpdateAffectedObjects();
		}
		for (int i = 0; i < m_affectedObjects.Length; i++)
		{
			if ((m_camera.cullingMask & (1 << m_affectedObjects[i].gameObject.layer)) != 0)
			{
				m_affectedObjects[i].OnRenderVectors(m_camera, m_affectedObjects[i].FixedStep ? fixedScale : scale, quality);
			}
		}
	}

	private void OnPostRender()
	{
		if (m_linked && Instance.isActiveAndEnabled)
		{
			if (!m_initialized)
			{
				Initialize();
			}
			if (m_overlay)
			{
				RenderTexture renderTexture = RenderTexture.active;
				Graphics.SetRenderTarget(Instance.MotionRenderTexture);
				RenderVectors(Instance.MotionScaleNorm, Instance.FixedMotionScaleNorm, Instance.QualityLevel);
				RenderTexture.active = renderTexture;
			}
		}
	}

	public AmplifyMotionCamera()
	{
		m_starting = true;
		m_autoStep = true;
		m_affectedObjectsTable = new HashSet<AmplifyMotionObjectBase>();
		m_affectedObjectsChanged = true;

	}




}
