using System;
using System.Collections;
using UnityEngine;
public class OVRHead : MonoBehaviour
{
	private Transform cam;

	private Vector3 lastEulerAngles;

	private Vector3 currentEulerAngles;

	private void Start()
	{
		cam = base.transform;
		if (cam.name == "Main Camera" && Application.loadedLevel != 0 && VRController.device == "oculus")
		{
			cam.GetChild(0).GetChild(0).GetChild(0)
				.localPosition = new Vector3(0.01f, 0f, 0f);
			cam.GetChild(0).GetChild(0).GetChild(2)
				.localPosition = new Vector3(0.06f, 0f, 0f);
		}
		if (Application.loadedLevel == 0 || base.GetComponent<Camera>() == Camera.main)
		{
			StartCoroutine("UpdateHead");
		}
	}

	private IEnumerator UpdateHead()
	{
		while (true)
		{
			if (!(VRController.device == "oculus"))
			{
				bool flag = VRController.device == "cardboard";
			}
			yield return new WaitForEndOfFrame();
			if (!(VRController.device == "oculus"))
			{
				bool flag2 = VRController.device == "cardboard";
			}
			if (!Menu.VRmode || Menu.mySettings.vr_headRotation == 0)
			{
				lastEulerAngles = new Vector3(0f, 0f, 0f);
				currentEulerAngles = lastEulerAngles;
			}
			Vector3 deltaRotation = currentEulerAngles - lastEulerAngles;
			FPSController.headRotation = deltaRotation;
			yield return new WaitForSeconds(0f);
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

	public OVRHead()
	{
	}




}
