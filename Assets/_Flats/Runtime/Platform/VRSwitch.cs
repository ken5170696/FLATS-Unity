using System;
using UnityEngine;
using UnityEngine.UI;
public class VRSwitch : MonoBehaviour
{
	public bool oculus;

	public Transform canvasForVR;

	public static bool oculusMode;

	private void Awake()
	{
		oculusMode = oculus;
		if (oculusMode && VRController.device == "")
		{
			VRController.device = "oculus";
			Menu.VRmode = true;
			QualitySettings.antiAliasing = 0;
			Debug.Log("Oculus Rift/GearVR");
			if (Input.GetJoystickNames().Length == 0)
			{
				canvasForVR.GetChild(4).gameObject.SetActive(true);
			}
		}
		else if (!oculusMode)
		{
			VRController.device = "cardboard";
			Debug.Log("Cardboard");
		}
	}

	private void Start()
	{
		if (Application.loadedLevel == 0)
		{
			canvasForVR.GetChild(2).GetComponent<Text>().text = "Flats ver." + FlatsPreferences.GetString("version") + "\n© Foliage Games";
		}
	}

	public VRSwitch()
	{
	}




}
