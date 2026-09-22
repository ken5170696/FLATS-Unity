using System;
using InControl;
using UnityEngine;
public class TitleCamera : MonoBehaviour
{
	private Transform ct;

	private void Start()
	{
		ct = base.transform;
	}

	private void Update()
	{
		if (Menu.VRmode)
		{
			InputDevice activeDevice = InputManager.ActiveDevice;
			float num = ((!FPSController.invertY) ? ((0f - (float)activeDevice.RightStickY) * (float)FPSController.sensitivity * 1.5f) : ((float)activeDevice.RightStickY * (float)FPSController.sensitivity * 1.5f));
			float num2 = (float)activeDevice.RightStickX * (float)FPSController.sensitivity * 2f;
			Vector3 vector = new Vector3(0f, num2 + FPSController.headRotation.y, 0f);
			Vector3 vector2 = new Vector3(num + FPSController.headRotation.x, 0f, 0f);
			float angle = ct.localEulerAngles.x + vector2.x;
			float y = ct.localEulerAngles.y + vector.y;
			ct.localEulerAngles = new Vector3(ClampAngle(angle), y, 0f);
		}
	}

	private static float ClampAngle(float angle)
	{
		if (angle < -360f)
		{
			angle += 360f;
		}
		if (angle > 360f)
		{
			angle -= 360f;
		}
		if (angle < 305f && angle > 270f && angle < 360f)
		{
			angle = 310f;
		}
		if (angle > 55f && angle > 0f && angle < 90f)
		{
			angle = 50f;
		}
		return angle;
	}

	public TitleCamera()
	{
	}




}
