using System;
using UnityEngine;
[Serializable]
public class PhotonTransformViewRotationModel 
{
	public enum InterpolateOptions
	{
		Disabled,
		RotateTowards,
		Lerp
	}

	public bool SynchronizeEnabled;

	public InterpolateOptions InterpolateOption;

	public float InterpolateRotateTowardsSpeed;

	public float InterpolateLerpSpeed;

	public PhotonTransformViewRotationModel()
	{
		InterpolateOption = InterpolateOptions.RotateTowards;
		InterpolateRotateTowardsSpeed = 180f;
		InterpolateLerpSpeed = 5f;

	}




}
