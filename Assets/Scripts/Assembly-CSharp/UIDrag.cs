using System;
using UnityEngine;
public class UIDrag : MonoBehaviour
{
	private int fingerId;

	private bool drag;

	private void OnEnable()
	{
		EasyTouch.On_TouchDown += On_TouchDown;
		EasyTouch.On_TouchStart += On_TouchStart;
		EasyTouch.On_TouchUp += On_TouchUp;
		EasyTouch.On_TouchStart2Fingers += On_TouchStart2Fingers;
		EasyTouch.On_TouchDown2Fingers += On_TouchDown2Fingers;
		EasyTouch.On_TouchUp2Fingers += On_TouchUp2Fingers;
	}

	private void OnDestroy()
	{
		EasyTouch.On_TouchDown -= On_TouchDown;
		EasyTouch.On_TouchStart -= On_TouchStart;
		EasyTouch.On_TouchUp -= On_TouchUp;
		EasyTouch.On_TouchStart2Fingers -= On_TouchStart2Fingers;
		EasyTouch.On_TouchDown2Fingers -= On_TouchDown2Fingers;
		EasyTouch.On_TouchUp2Fingers -= On_TouchUp2Fingers;
	}

	private void On_TouchStart(Gesture gesture)
	{
		if (gesture.isOverGui && drag && (gesture.pickedUIElement == base.gameObject || gesture.pickedUIElement.transform.IsChildOf(base.transform)) && fingerId == -1)
		{
			fingerId = gesture.fingerIndex;
			base.transform.SetAsLastSibling();
		}
	}

	private void On_TouchDown(Gesture gesture)
	{
		if (fingerId == gesture.fingerIndex && drag && gesture.isOverGui && (gesture.pickedUIElement == base.gameObject || gesture.pickedUIElement.transform.IsChildOf(base.transform)))
		{
			base.transform.position += (Vector3)gesture.deltaPosition;
		}
	}

	private void On_TouchUp(Gesture gesture)
	{
		if (fingerId == gesture.fingerIndex)
		{
			fingerId = -1;
		}
	}

	private void On_TouchStart2Fingers(Gesture gesture)
	{
		if (gesture.isOverGui && drag && (gesture.pickedUIElement == base.gameObject || gesture.pickedUIElement.transform.IsChildOf(base.transform)) && fingerId == -1)
		{
			base.transform.SetAsLastSibling();
		}
	}

	private void On_TouchDown2Fingers(Gesture gesture)
	{
		if (gesture.isOverGui && (gesture.pickedUIElement == base.gameObject || gesture.pickedUIElement.transform.IsChildOf(base.transform)))
		{
			if (gesture.pickedUIElement == base.gameObject || gesture.pickedUIElement.transform.IsChildOf(base.transform))
			{
				base.transform.position += (Vector3)gesture.deltaPosition;
			}
			drag = false;
		}
	}

	private void On_TouchUp2Fingers(Gesture gesture)
	{
		if (gesture.isOverGui && (gesture.pickedUIElement == base.gameObject || gesture.pickedUIElement.transform.IsChildOf(base.transform)))
		{
			drag = true;
		}
	}

	public UIDrag()
	{
		fingerId = -1;
		drag = true;

	}




}
