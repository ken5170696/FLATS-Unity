using System;
using UnityEngine;
public class DragMe : MonoBehaviour
{
	private TextMesh textMesh;

	private Color startColor;

	private Vector3 deltaPosition;

	private int fingerIndex;

	private void OnEnable()
	{
		EasyTouch.On_Drag += On_Drag;
		EasyTouch.On_DragStart += On_DragStart;
		EasyTouch.On_DragEnd += On_DragEnd;
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
		EasyTouch.On_Drag -= On_Drag;
		EasyTouch.On_DragStart -= On_DragStart;
		EasyTouch.On_DragEnd -= On_DragEnd;
	}

	private void Start()
	{
		textMesh = GetComponentInChildren<TextMesh>();
		startColor = base.gameObject.GetComponent<Renderer>().material.color;
	}

	private void On_DragStart(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			fingerIndex = gesture.fingerIndex;
			RandomColor();
			Vector3 touchToWorldPoint = gesture.GetTouchToWorldPoint(gesture.pickedObject.transform.position);
			deltaPosition = touchToWorldPoint - base.transform.position;
		}
	}

	private void On_Drag(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject && fingerIndex == gesture.fingerIndex)
		{
			Vector3 touchToWorldPoint = gesture.GetTouchToWorldPoint(gesture.pickedObject.transform.position);
			base.transform.position = touchToWorldPoint - deltaPosition;
			float swipeOrDragAngle = gesture.GetSwipeOrDragAngle();
			textMesh.text = swipeOrDragAngle.ToString("f2") + " / " + gesture.swipe;
		}
	}

	private void On_DragEnd(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			base.gameObject.GetComponent<Renderer>().material.color = startColor;
			textMesh.text = "Drag me";
		}
	}

	private void RandomColor()
	{
		base.gameObject.GetComponent<Renderer>().material.color = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
	}

	public DragMe()
	{
	}




}
