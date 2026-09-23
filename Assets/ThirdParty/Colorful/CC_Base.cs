using System;
using UnityEngine;
[RequireComponent(typeof(Camera))]
[AddComponentMenu("")]
public class CC_Base : MonoBehaviour
{
	public Shader shader;

	protected Material _material;

	protected Material material
	{
		get
		{
			if (_material == null)
			{
				_material = new Material(shader);
				_material.hideFlags = HideFlags.HideAndDontSave;
			}
			return _material;
		}
	}

	public static bool IsLinear()
	{
		return QualitySettings.activeColorSpace == ColorSpace.Linear;
	}

	protected virtual void Start()
	{
		// URP executes this serialized control through FlatsPostProcessFeature.
		if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null) return;
		if (!SystemInfo.supportsImageEffects)
		{
			base.enabled = false;
		}
		else if (!shader || !shader.isSupported)
		{
			base.enabled = false;
		}
	}

	protected virtual void OnDisable()
	{
		if ((bool)_material)
		{
			UnityEngine.Object.DestroyImmediate(_material);
		}
	}

	public CC_Base()
	{
	}




}
