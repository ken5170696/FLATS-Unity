using System;
using UnityEngine;
public class ETWindow : MonoBehaviour
{
	private bool drag;

	private void OnEnable()
	{
		EasyTouch.On_TouchDown += On_TouchDown;
		EasyTouch.On_TouchStart += On_TouchStart;
	}

	private void OnDestroy()
	{
		EasyTouch.On_TouchDown -= On_TouchDown;
		EasyTouch.On_TouchStart -= On_TouchStart;
	}

	private void On_TouchStart(Gesture gesture)
	{
		drag = false;
		if (gesture.isOverGui && (gesture.pickedUIElement == base.gameObject || gesture.pickedUIElement.transform.IsChildOf(base.transform)))
		{
			base.transform.SetAsLastSibling();
			drag = true;
		}
	}

	private void On_TouchDown(Gesture gesture)
	{
		if (gesture.isOverGui && (gesture.pickedUIElement == base.gameObject || gesture.pickedUIElement.transform.IsChildOf(base.transform)) && drag)
		{
			base.transform.position += (Vector3)gesture.deltaPosition;
		}
	}

	public ETWindow()
	{
	}




}
