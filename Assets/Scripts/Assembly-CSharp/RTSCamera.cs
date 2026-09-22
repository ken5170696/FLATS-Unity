using System;
using UnityEngine;
public class RTSCamera : MonoBehaviour
{
	private Vector3 delta;

	private void OnEnable()
	{
		EasyTouch.On_Swipe += On_Swipe;
		EasyTouch.On_Drag += On_Drag;
		EasyTouch.On_Twist += On_Twist;
		EasyTouch.On_Pinch += On_Pinch;
	}

	private void On_Twist(Gesture gesture)
	{
		base.transform.Rotate(Vector3.up * gesture.twistAngle);
	}

	private void OnDestroy()
	{
		EasyTouch.On_Swipe -= On_Swipe;
		EasyTouch.On_Drag -= On_Drag;
		EasyTouch.On_Twist -= On_Twist;
	}

	private void On_Drag(Gesture gesture)
	{
		On_Swipe(gesture);
	}

	private void On_Swipe(Gesture gesture)
	{
		base.transform.Translate(Vector3.left * gesture.deltaPosition.x / Screen.width);
		base.transform.Translate(Vector3.back * gesture.deltaPosition.y / Screen.height);
	}

	private void On_Pinch(Gesture gesture)
	{
		Camera.main.fieldOfView += gesture.deltaPinch * Time.deltaTime;
	}

	public RTSCamera()
	{
	}




}
