using System;
using System.Collections.Generic;
using UnityEngine;
namespace FxProNS
{
	public class DOFHelper : Singleton<DOFHelper>, IDisposable
	{
		private static Material _mat;

		private DOFHelperParams _p;

		private float _curAutoFocusDist;

		public static Material Mat
		{
			get
			{
				if (null == _mat)
				{
					Material material = new Material(Shader.Find("Hidden/DOFPro"));
					material.hideFlags = HideFlags.HideAndDontSave;
					_mat = material;
				}
				return _mat;
			}
		}

		public void SetParams(DOFHelperParams p)
		{
			_p = p;
		}

		public void Init(bool searchForNonDepthmapAlphaObjects)
		{
			if (_p == null)
			{
				Debug.LogError("Call SetParams first");
			}
			else if (null == _p.EffectCamera)
			{
				Debug.LogError("null == p.camera");
			}
			else
			{
				if (null == Mat)
				{
					return;
				}
				if (!_p.UseUnityDepthBuffer)
				{
					_p.EffectCamera.depthTextureMode = DepthTextureMode.None;
					Mat.DisableKeyword("USE_CAMERA_DEPTH_TEXTURE");
					Mat.EnableKeyword("DONT_USE_CAMERA_DEPTH_TEXTURE");
				}
				else
				{
					if (_p.EffectCamera.depthTextureMode != DepthTextureMode.DepthNormals)
					{
						_p.EffectCamera.depthTextureMode = DepthTextureMode.Depth;
					}
					Mat.EnableKeyword("USE_CAMERA_DEPTH_TEXTURE");
					Mat.DisableKeyword("DONT_USE_CAMERA_DEPTH_TEXTURE");
				}
				_p.FocalLengthMultiplier = Mathf.Clamp(_p.FocalLengthMultiplier, 0.01f, 0.99f);
				_p.DepthCompression = Mathf.Clamp(_p.DepthCompression, 1f, 10f);
				Shader.SetGlobalFloat("_OneOverDepthScale", _p.DepthCompression);
				Shader.SetGlobalFloat("_OneOverDepthFar", 1f / _p.EffectCamera.farClipPlane);
				if (_p.BokehEnabled)
				{
					Mat.SetFloat("_BokehThreshold", _p.BokehThreshold);
					Mat.SetFloat("_BokehGain", _p.BokehGain);
					Mat.SetFloat("_BokehBias", _p.BokehBias);
				}
			}
		}

		public void SetBlurRadius(int radius)
		{
			Shader.DisableKeyword("BLUR_RADIUS_10");
			Shader.DisableKeyword("BLUR_RADIUS_5");
			Shader.DisableKeyword("BLUR_RADIUS_3");
			Shader.DisableKeyword("BLUR_RADIUS_2");
			Shader.DisableKeyword("BLUR_RADIUS_1");
			if (radius != 10 && radius != 5 && radius != 3 && radius != 2 && radius != 1)
			{
				radius = 5;
			}
			if (radius < 3)
			{
				radius = 3;
			}
			Shader.EnableKeyword("BLUR_RADIUS_" + radius);
		}

		private void CalculateAndUpdateFocalDist()
		{
			if (null == _p.EffectCamera)
			{
				Debug.LogError("null == p.camera");
				return;
			}
			float num = ((_p.AutoFocus || !(null != _p.Target)) ? (_curAutoFocusDist = Mathf.Lerp(_curAutoFocusDist, CalculateAutoFocusDist(), Time.deltaTime * _p.AutoFocusSpeed)) : _p.EffectCamera.WorldToViewportPoint(_p.Target.position).z);
			num /= _p.EffectCamera.farClipPlane;
			num *= _p.FocalDistMultiplier * _p.DepthCompression;
			Mat.SetFloat("_FocalDist", num);
			Mat.SetFloat("_FocalLength", num * _p.FocalLengthMultiplier);
		}

		private float CalculateAutoFocusDist()
		{
			if (null == _p.EffectCamera)
			{
				return 0f;
			}
			RaycastHit hitInfo;
			if (!Physics.Raycast(_p.EffectCamera.transform.position, _p.EffectCamera.transform.forward, out hitInfo, float.PositiveInfinity, _p.AutoFocusLayerMask.value))
			{
				return _p.EffectCamera.farClipPlane;
			}
			return hitInfo.distance;
		}

		public void RenderCOCTexture(RenderTexture src, RenderTexture dest, float blurScale)
		{
			CalculateAndUpdateFocalDist();
			if (null == _p.EffectCamera)
			{
				Debug.LogError("null == p.camera");
				return;
			}
			if (_p.EffectCamera.depthTextureMode == DepthTextureMode.None)
			{
				_p.EffectCamera.depthTextureMode = DepthTextureMode.Depth;
			}
			if (_p.DOFBlurSize > 0.001f)
			{
				RenderTexture renderTexture = RenderTextureManager.Instance.RequestRenderTexture(src.width, src.height, src.depth, src.format);
				RenderTexture renderTexture2 = RenderTextureManager.Instance.RequestRenderTexture(src.width, src.height, src.depth, src.format);
				Graphics.Blit(src, renderTexture, Mat, 0);
				Mat.SetVector("_SeparableBlurOffsets", new Vector4(blurScale, 0f, 0f, 0f));
				Graphics.Blit(renderTexture, renderTexture2, Mat, 2);
				Mat.SetVector("_SeparableBlurOffsets", new Vector4(0f, blurScale, 0f, 0f));
				Graphics.Blit(renderTexture2, dest, Mat, 2);
				RenderTextureManager.Instance.ReleaseRenderTexture(renderTexture);
				RenderTextureManager.Instance.ReleaseRenderTexture(renderTexture2);
			}
			else
			{
				Graphics.Blit(src, dest, Mat, 0);
			}
		}

		public void RenderDOFBlur(RenderTexture src, RenderTexture dest, RenderTexture cocTexture)
		{
			if (null == cocTexture)
			{
				Debug.LogError("null == cocTexture");
				return;
			}
			Mat.SetTexture("_COCTex", cocTexture);
			if (_p.BokehEnabled)
			{
				Mat.SetFloat("_BlurIntensity", _p.DOFBlurSize);
				Graphics.Blit(src, dest, Mat, 4);
				return;
			}
			RenderTexture renderTexture = RenderTextureManager.Instance.RequestRenderTexture(src.width, src.height, src.depth, src.format);
			Mat.SetVector("_SeparableBlurOffsets", new Vector4(_p.DOFBlurSize, 0f, 0f, 0f));
			Graphics.Blit(src, renderTexture, Mat, 1);
			Mat.SetVector("_SeparableBlurOffsets", new Vector4(0f, _p.DOFBlurSize, 0f, 0f));
			Graphics.Blit(renderTexture, dest, Mat, 1);
			RenderTextureManager.Instance.ReleaseRenderTexture(renderTexture);
		}

		public void RenderEffect(RenderTexture src, RenderTexture dest)
		{
			RenderEffect(src, dest, false);
		}

		public void RenderEffect(RenderTexture src, RenderTexture dest, bool visualizeCOC)
		{
			RenderTexture renderTexture = RenderTextureManager.Instance.RequestRenderTexture(src.width, src.height, src.depth, src.format);
			RenderCOCTexture(src, renderTexture, 0f);
			if (visualizeCOC)
			{
				Graphics.Blit(renderTexture, dest);
				RenderTextureManager.Instance.ReleaseRenderTexture(renderTexture);
				RenderTextureManager.Instance.ReleaseAllRenderTextures();
			}
			else
			{
				RenderDOFBlur(src, dest, renderTexture);
				RenderTextureManager.Instance.ReleaseRenderTexture(renderTexture);
			}
		}

		public static GameObject[] GetNonDepthmapAlphaObjects()
		{
			if (!Application.isPlaying)
			{
				return new GameObject[0];
			}
			Renderer[] array = UnityEngine.Object.FindObjectsOfType<Renderer>();
			List<GameObject> list = new List<GameObject>();
			List<Material> list2 = new List<Material>();
			Renderer[] array2 = array;
			foreach (Renderer renderer in array2)
			{
				if (renderer.sharedMaterials == null || null != renderer.GetComponent<ParticleSystem>())
				{
					continue;
				}
				Material[] sharedMaterials = renderer.sharedMaterials;
				foreach (Material material in sharedMaterials)
				{
					if (null == material.shader)
					{
						continue;
					}
					bool flag = null == material.GetTag("RenderType", false);
					if (flag || (!(material.GetTag("RenderType", false).ToLower() == "transparent") && !(material.GetTag("Queue", false).ToLower() == "transparent")))
					{
						if (material.GetTag("OUTPUT_DEPTH_TO_ALPHA", false) == null || material.GetTag("OUTPUT_DEPTH_TO_ALPHA", false).ToLower() != "true")
						{
							flag = true;
						}
						if (flag && !list2.Contains(material))
						{
							list2.Add(material);
							Debug.Log("Non-depthmapped: " + GetFullPath(renderer.gameObject));
							list.Add(renderer.gameObject);
						}
					}
				}
			}
			return list.ToArray();
		}

		public static string GetFullPath(GameObject obj)
		{
			string text = "/" + obj.name;
			while (obj.transform.parent != null)
			{
				obj = obj.transform.parent.gameObject;
				text = "/" + obj.name + text;
			}
			return "'" + text + "'";
		}

		public void Dispose()
		{
			if (null != Mat)
			{
				UnityEngine.Object.DestroyImmediate(Mat);
			}
			RenderTextureManager.Instance.Dispose();
		}

		public DOFHelper()
		{
		}




	}
}
