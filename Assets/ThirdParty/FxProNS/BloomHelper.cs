using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace FxProNS
{
	public class BloomHelper : Singleton<BloomHelper>, IDisposable
	{
		private static Material _mat;

		private BloomHelperParams p;

		private int bloomSamples;

		private float bloomBlurRadius;

		public static Material Mat
		{
			get
			{
				if (null == _mat)
				{
					Material material = new Material(Shader.Find("Hidden/BloomPro"));
					material.hideFlags = HideFlags.HideAndDontSave;
					_mat = material;
				}
				return _mat;
			}
		}

		public void SetParams(BloomHelperParams _p)
		{
			p = _p;
		}

		public void Init()
		{
			float value = Mathf.Exp(p.BloomIntensity) - 1f;
			if (Application.platform == RuntimePlatform.IPhonePlayer)
			{
				p.BloomThreshold *= 0.75f;
			}
			Mat.SetFloat("_BloomThreshold", p.BloomThreshold);
			Mat.SetFloat("_BloomIntensity", value);
			Mat.SetColor("_BloomTint", p.BloomTint);
			if (p.Quality == EffectsQuality.High || p.Quality == EffectsQuality.Normal)
			{
				bloomSamples = 5;
				Mat.EnableKeyword("BLOOM_SAMPLES_5");
				Mat.DisableKeyword("BLOOM_SAMPLES_3");
			}
			if (p.Quality == EffectsQuality.Fast || p.Quality == EffectsQuality.Fastest)
			{
				bloomSamples = 3;
				Mat.EnableKeyword("BLOOM_SAMPLES_3");
				Mat.DisableKeyword("BLOOM_SAMPLES_5");
			}
			if (p.Quality == EffectsQuality.High)
			{
				bloomBlurRadius = 10f;
				Mat.EnableKeyword("BLUR_RADIUS_10");
				Mat.DisableKeyword("BLUR_RADIUS_5");
			}
			else
			{
				bloomBlurRadius = 5f;
				Mat.EnableKeyword("BLUR_RADIUS_5");
				Mat.DisableKeyword("BLUR_RADIUS_10");
			}
			float[] array = CalculateBloomTexFactors(Mathf.Exp(p.BloomSoftness) - 1f);
			if (array.Length == 5)
			{
				Mat.SetVector("_BloomTexFactors1", new Vector4(array[0], array[1], array[2], array[3]));
				Mat.SetVector("_BloomTexFactors2", new Vector4(array[4], 0f, 0f, 0f));
			}
			else if (array.Length == 3)
			{
				Mat.SetVector("_BloomTexFactors1", new Vector4(array[0], array[1], array[2], 0f));
			}
			else
			{
				Debug.LogError("Unsupported bloomTexFactors.Length: " + array.Length);
			}
			RenderTextureManager.Instance.Dispose();
		}

		public void RenderBloomTexture(RenderTexture source, RenderTexture dest)
		{
			RenderTexture a = RenderTextureManager.Instance.RequestRenderTexture(source.width, source.height, source.depth, source.format);
			Graphics.Blit(source, a, Mat, 0);
			for (int i = 1; i <= bloomSamples; i++)
			{
				float spread = Mathf.Lerp(1f, 2f, (float)(i - 1) / (float)bloomSamples);
				RenderTextureManager.Instance.SafeAssign(ref a, FxPro.DownsampleTex(a, 2f));
				RenderTextureManager.Instance.SafeAssign(ref a, BlurTex(a, spread));
				Mat.SetTexture("_DsTex" + i, a);
			}
			Graphics.Blit(null, dest, Mat, 1);
			RenderTextureManager.Instance.ReleaseRenderTexture(a);
		}

		public RenderTexture BlurTex(RenderTexture _input, float _spread)
		{
			float num = _spread * 10f / bloomBlurRadius;
			RenderTexture renderTexture = RenderTextureManager.Instance.RequestRenderTexture(_input.width, _input.height, _input.depth, _input.format);
			RenderTexture renderTexture2 = RenderTextureManager.Instance.RequestRenderTexture(_input.width, _input.height, _input.depth, _input.format);
			Mat.SetVector("_SeparableBlurOffsets", new Vector4(1f, 0f, 0f, 0f) * num);
			Graphics.Blit(_input, renderTexture, Mat, 2);
			Mat.SetVector("_SeparableBlurOffsets", new Vector4(0f, 1f, 0f, 0f) * num);
			Graphics.Blit(renderTexture, renderTexture2, Mat, 2);
			renderTexture = RenderTextureManager.Instance.ReleaseRenderTexture(renderTexture);
			return renderTexture2;
		}

		private float[] CalculateBloomTexFactors(float softness)
		{
			float[] array = new float[bloomSamples];
			for (int i = 0; i < array.Length; i++)
			{
				float t = (float)i / (float)(array.Length - 1);
				array[i] = Mathf.Lerp(1f, softness, t);
			}
			return MakeSumOne(array);
		}

		private float[] MakeSumOne(IList<float> _in)
		{
			float num = _in.Sum();
			float[] array = new float[_in.Count];
			for (int i = 0; i < _in.Count; i++)
			{
				array[i] = _in[i] / num;
			}
			return array;
		}

		public void Dispose()
		{
			if (null != Mat)
			{
				UnityEngine.Object.DestroyImmediate(Mat);
			}
			RenderTextureManager.Instance.Dispose();
		}

		public BloomHelper()
		{
			bloomSamples = 5;
			bloomBlurRadius = 5f;

		}




	}
}
