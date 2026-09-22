using System;
using UnityEngine;
[Serializable]
public class PhotonTransformViewPositionModel 
{
	public enum InterpolateOptions
	{
		Disabled,
		FixedSpeed,
		EstimatedSpeed,
		SynchronizeValues,
		Lerp
	}

	public enum ExtrapolateOptions
	{
		Disabled,
		SynchronizeValues,
		EstimateSpeedAndTurn,
		FixedSpeed
	}

	public bool SynchronizeEnabled;

	public bool TeleportEnabled;

	public float TeleportIfDistanceGreaterThan;

	public InterpolateOptions InterpolateOption;

	public float InterpolateMoveTowardsSpeed;

	public float InterpolateLerpSpeed;

	public float InterpolateMoveTowardsAcceleration;

	public float InterpolateMoveTowardsDeceleration;

	public AnimationCurve InterpolateSpeedCurve;

	public ExtrapolateOptions ExtrapolateOption;

	public float ExtrapolateSpeed;

	public bool ExtrapolateIncludingRoundTripTime;

	public int ExtrapolateNumberOfStoredPositions;

	public bool DrawErrorGizmo;

	public PhotonTransformViewPositionModel()
	{
		TeleportEnabled = true;
		TeleportIfDistanceGreaterThan = 3f;
		InterpolateOption = InterpolateOptions.EstimatedSpeed;
		InterpolateMoveTowardsSpeed = 1f;
		InterpolateLerpSpeed = 1f;
		InterpolateMoveTowardsAcceleration = 2f;
		InterpolateMoveTowardsDeceleration = 2f;
		InterpolateSpeedCurve = new AnimationCurve(new Keyframe(-1f, 0f, 0f, float.PositiveInfinity), new Keyframe(0f, 1f, 0f, 0f), new Keyframe(1f, 1f, 0f, 1f), new Keyframe(4f, 4f, 1f, 0f));
		ExtrapolateSpeed = 1f;
		ExtrapolateIncludingRoundTripTime = true;
		ExtrapolateNumberOfStoredPositions = 1;
		DrawErrorGizmo = true;

	}




}
