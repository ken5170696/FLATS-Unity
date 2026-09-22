using System;
using UnityEngine;
[Serializable]
public class PhotonTransformViewScaleModel 
{
	public enum InterpolateOptions
	{
		Disabled,
		MoveTowards,
		Lerp
	}

	public bool SynchronizeEnabled;

	public InterpolateOptions InterpolateOption;

	public float InterpolateMoveTowardsSpeed;

	public float InterpolateLerpSpeed;

	public PhotonTransformViewScaleModel()
	{
		InterpolateMoveTowardsSpeed = 1f;

	}




}
