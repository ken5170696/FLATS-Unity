using System;
using UnityEngine;
public class PinchMe : MonoBehaviour
{
	private TextMesh textMesh;

	private void OnEnable()
	{
		EasyTouch.On_TouchStart2Fingers += On_TouchStart2Fingers;
		EasyTouch.On_PinchIn += On_PinchIn;
		EasyTouch.On_PinchOut += On_PinchOut;
		EasyTouch.On_PinchEnd += On_PinchEnd;
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
		EasyTouch.On_TouchStart2Fingers -= On_TouchStart2Fingers;
		EasyTouch.On_PinchIn -= On_PinchIn;
		EasyTouch.On_PinchOut -= On_PinchOut;
		EasyTouch.On_PinchEnd -= On_PinchEnd;
	}

	private void Start()
	{
		textMesh = GetComponentInChildren<TextMesh>();
	}

	private void On_TouchStart2Fingers(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			EasyTouch.SetEnableTwist(false);
			EasyTouch.SetEnablePinch(true);
		}
	}

	private void On_PinchIn(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			float num = Time.deltaTime * gesture.deltaPinch;
			Vector3 localScale = base.transform.localScale;
			base.transform.localScale = new Vector3(localScale.x - num, localScale.y - num, localScale.z - num);
			textMesh.text = "Delta pinch : " + gesture.deltaPinch;
		}
	}

	private void On_PinchOut(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			float num = Time.deltaTime * gesture.deltaPinch;
			Vector3 localScale = base.transform.localScale;
			base.transform.localScale = new Vector3(localScale.x + num, localScale.y + num, localScale.z + num);
			textMesh.text = "Delta pinch : " + gesture.deltaPinch;
		}
	}

	private void On_PinchEnd(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			base.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
			EasyTouch.SetEnableTwist(true);
			textMesh.text = "Pinch me";
		}
	}

	public PinchMe()
	{
	}




}
