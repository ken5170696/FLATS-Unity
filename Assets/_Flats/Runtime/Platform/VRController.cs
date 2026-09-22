using System;
using UnityEngine;
using UnityEngine.EventSystems;
public class VRController : MonoBehaviour
{
	public static string device = "";

	public static float offset = 0f;

	public bool enableRotate;

	private bool enableVR;

	private Transform mt;

	private void Awake()
	{
		mt = base.transform;
		if (device == "cardboard")
		{
			EventSystem.current.gameObject.GetComponent<LookInputModule>().enabled = Menu.VRmode;
		}
		else
		{
			bool flag = device == "oculus";
		}
		if (!Menu.VRmode && base.gameObject.name == "UICamera")
		{
			Canvas[] componentsInChildren = mt.GetComponentsInChildren<Canvas>();
			Canvas[] array = componentsInChildren;
			foreach (Canvas canvas in array)
			{
				canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			}
		}
	}

	public VRController()
	{
	}




}
