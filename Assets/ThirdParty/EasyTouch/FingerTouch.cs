using System;
using UnityEngine;
public class FingerTouch : MonoBehaviour
{
	private TextMesh textMesh;

	public Vector3 deltaPosition;

	public int fingerId;

	private void OnEnable()
	{
		EasyTouch.On_TouchStart += On_TouchStart;
		EasyTouch.On_TouchUp += On_TouchUp;
		EasyTouch.On_Swipe += On_Swipe;
		EasyTouch.On_Drag += On_Drag;
		EasyTouch.On_DoubleTap += On_DoubleTap;
		textMesh = GetComponentInChildren<TextMesh>();
	}

	private void OnDestroy()
	{
		EasyTouch.On_TouchStart -= On_TouchStart;
		EasyTouch.On_TouchUp -= On_TouchUp;
		EasyTouch.On_Swipe -= On_Swipe;
		EasyTouch.On_Drag -= On_Drag;
		EasyTouch.On_DoubleTap -= On_DoubleTap;
	}

	private void On_Drag(Gesture gesture)
	{
		if (gesture.pickedObject.transform.IsChildOf(base.gameObject.transform) && fingerId == gesture.fingerIndex)
		{
			Vector3 touchToWorldPoint = gesture.GetTouchToWorldPoint(gesture.pickedObject.transform.position);
			base.transform.position = touchToWorldPoint - deltaPosition;
		}
	}

	private void On_Swipe(Gesture gesture)
	{
		if (fingerId == gesture.fingerIndex)
		{
			Vector3 touchToWorldPoint = gesture.GetTouchToWorldPoint(base.transform.position);
			base.transform.position = touchToWorldPoint - deltaPosition;
		}
	}

	private void On_TouchStart(Gesture gesture)
	{
		if (gesture.pickedObject != null && gesture.pickedObject.transform.IsChildOf(base.gameObject.transform))
		{
			fingerId = gesture.fingerIndex;
			textMesh.text = fingerId.ToString();
			Vector3 touchToWorldPoint = gesture.GetTouchToWorldPoint(gesture.pickedObject.transform.position);
			deltaPosition = touchToWorldPoint - base.transform.position;
		}
	}

	private void On_TouchUp(Gesture gesture)
	{
		if (gesture.fingerIndex == fingerId)
		{
			fingerId = -1;
			textMesh.text = "";
		}
	}

	public void InitTouch(int ind)
	{
		fingerId = ind;
		textMesh.text = fingerId.ToString();
	}

	private void On_DoubleTap(Gesture gesture)
	{
		if (gesture.pickedObject != null && gesture.pickedObject.transform.IsChildOf(base.gameObject.transform))
		{
			UnityEngine.Object.DestroyImmediate(base.transform.gameObject);
		}
	}

	public FingerTouch()
	{
		deltaPosition = Vector2.zero;
		fingerId = -1;

	}




}
