using System;
using UnityEngine;
namespace FxProNS
{
	[Serializable]
	public class DOFHelperParams 
	{
		public bool UseUnityDepthBuffer;

		public bool AutoFocus;

		public LayerMask AutoFocusLayerMask;

		[Range(2f, 8f)]
		public float AutoFocusSpeed;

		[Range(0.01f, 1f)]
		public float FocalLengthMultiplier;

		public float FocalDistMultiplier;

		[Range(0.5f, 2f)]
		public float DOFBlurSize;

		public bool BokehEnabled;

		[Range(2f, 8f)]
		public float DepthCompression;

		public Camera EffectCamera;

		public Transform Target;

		[Range(0f, 1f)]
		public float BokehThreshold;

		[Range(0.5f, 5f)]
		public float BokehGain;

		[Range(0f, 1f)]
		public float BokehBias;

		public bool DoubleIntensityBlur;

		public DOFHelperParams()
		{
			UseUnityDepthBuffer = true;
			AutoFocus = true;
			AutoFocusLayerMask = -1;
			AutoFocusSpeed = 5f;
			FocalLengthMultiplier = 0.33f;
			FocalDistMultiplier = 1f;
			DOFBlurSize = 1f;
			DepthCompression = 4f;
			BokehThreshold = 0.5f;
			BokehGain = 2f;
			BokehBias = 0.5f;

		}




	}
}
