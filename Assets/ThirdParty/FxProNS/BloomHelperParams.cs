using System;
using UnityEngine;
namespace FxProNS
{
	[Serializable]
	public class BloomHelperParams 
	{
		public EffectsQuality Quality;

		public Color BloomTint;

		[Range(0f, 0.99f)]
		public float BloomThreshold;

		[Range(0f, 3f)]
		public float BloomIntensity;

		[Range(0.01f, 3f)]
		public float BloomSoftness;

		public BloomHelperParams()
		{
			BloomTint = Color.white;
			BloomThreshold = 0.8f;
			BloomIntensity = 1.5f;
			BloomSoftness = 0.5f;

		}




	}
}
