using System;
using UnityEngine;
public class TwistMe : MonoBehaviour
{
	private TextMesh textMesh;

	private void OnEnable()
	{
		EasyTouch.On_TouchStart2Fingers += On_TouchStart2Fingers;
		EasyTouch.On_Twist += On_Twist;
		EasyTouch.On_TwistEnd += On_TwistEnd;
		EasyTouch.On_Cancel2Fingers += On_Cancel2Fingers;
	}

	private void OnDisable()
	{
		UnsubscribeEvent();
	}

	private void OnDestroy()
	{
		UnsubscribeEvent();
	}

	private void UnsubscribeEvent()
	{
		EasyTouch.On_TouchStart2Fingers -= On_TouchStart2Fingers;
		EasyTouch.On_Twist -= On_Twist;
		EasyTouch.On_TwistEnd -= On_TwistEnd;
		EasyTouch.On_Cancel2Fingers -= On_Cancel2Fingers;
	}

	private void Start()
	{
		textMesh = GetComponentInChildren<TextMesh>();
	}

	private void On_TouchStart2Fingers(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			EasyTouch.SetEnableTwist(true);
			EasyTouch.SetEnablePinch(false);
		}
	}

	private void On_Twist(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			base.transform.Rotate(new Vector3(0f, 0f, gesture.twistAngle));
			textMesh.text = "Delta angle : " + gesture.twistAngle;
		}
	}

	private void On_TwistEnd(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			EasyTouch.SetEnablePinch(true);
			base.transform.rotation = Quaternion.identity;
			textMesh.text = "Twist me";
		}
	}

	private void On_Cancel2Fingers(Gesture gesture)
	{
		EasyTouch.SetEnablePinch(true);
		base.transform.rotation = Quaternion.identity;
		textMesh.text = "Twist me";
	}

	public TwistMe()
	{
	}




}
