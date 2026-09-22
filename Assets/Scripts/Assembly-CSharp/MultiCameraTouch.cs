using System;
using UnityEngine;
using UnityEngine.UI;
public class MultiCameraTouch : MonoBehaviour
{
	public Text label;

	private void OnEnable()
	{
		EasyTouch.On_TouchDown += On_TouchDown;
		EasyTouch.On_TouchUp += On_TouchUp;
	}

	private void OnDestroy()
	{
		EasyTouch.On_TouchDown -= On_TouchDown;
		EasyTouch.On_TouchUp -= On_TouchUp;
	}

	private void On_TouchDown(Gesture gesture)
	{
		if (gesture.pickedObject != null)
		{
			label.text = "You touch : " + gesture.pickedObject.name + " on camera : " + gesture.pickedCamera.name;
		}
	}

	private void On_TouchUp(Gesture gesture)
	{
		label.text = "";
	}

	public MultiCameraTouch()
	{
	}




}
