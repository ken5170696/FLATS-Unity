using System;
using UnityEngine;
public class TwoDragMe : MonoBehaviour
{
	private TextMesh textMesh;

	private Vector3 deltaPosition;

	private Color startColor;

	private void OnEnable()
	{
		EasyTouch.On_DragStart2Fingers += On_DragStart2Fingers;
		EasyTouch.On_Drag2Fingers += On_Drag2Fingers;
		EasyTouch.On_DragEnd2Fingers += On_DragEnd2Fingers;
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
		EasyTouch.On_DragStart2Fingers -= On_DragStart2Fingers;
		EasyTouch.On_Drag2Fingers -= On_Drag2Fingers;
		EasyTouch.On_DragEnd2Fingers -= On_DragEnd2Fingers;
		EasyTouch.On_Cancel2Fingers -= On_Cancel2Fingers;
	}

	private void Start()
	{
		textMesh = GetComponentInChildren<TextMesh>();
		startColor = base.gameObject.GetComponent<Renderer>().material.color;
	}

	private void On_DragStart2Fingers(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			RandomColor();
			Vector3 touchToWorldPoint = gesture.GetTouchToWorldPoint(gesture.pickedObject.transform.position);
			deltaPosition = touchToWorldPoint - base.transform.position;
		}
	}

	private void On_Drag2Fingers(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			Vector3 touchToWorldPoint = gesture.GetTouchToWorldPoint(gesture.pickedObject.transform.position);
			base.transform.position = touchToWorldPoint - deltaPosition;
			float swipeOrDragAngle = gesture.GetSwipeOrDragAngle();
			textMesh.text = swipeOrDragAngle.ToString("f2") + " / " + gesture.swipe;
		}
	}

	private void On_DragEnd2Fingers(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			base.gameObject.GetComponent<Renderer>().material.color = startColor;
			textMesh.text = "Drag me";
		}
	}

	private void On_Cancel2Fingers(Gesture gesture)
	{
		On_DragEnd2Fingers(gesture);
	}

	private void RandomColor()
	{
		base.gameObject.GetComponent<Renderer>().material.color = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
	}

	public TwoDragMe()
	{
	}




}
