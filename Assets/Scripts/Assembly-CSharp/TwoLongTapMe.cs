using System;
using UnityEngine;
public class TwoLongTapMe : MonoBehaviour
{
	private TextMesh textMesh;

	private Color startColor;

	private void OnEnable()
	{
		EasyTouch.On_LongTapStart2Fingers += On_LongTapStart2Fingers;
		EasyTouch.On_LongTap2Fingers += On_LongTap2Fingers;
		EasyTouch.On_LongTapEnd2Fingers += On_LongTapEnd2Fingers;
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
		EasyTouch.On_LongTapStart2Fingers -= On_LongTapStart2Fingers;
		EasyTouch.On_LongTap2Fingers -= On_LongTap2Fingers;
		EasyTouch.On_LongTapEnd2Fingers -= On_LongTapEnd2Fingers;
		EasyTouch.On_Cancel2Fingers -= On_Cancel2Fingers;
	}

	private void Start()
	{
		textMesh = GetComponentInChildren<TextMesh>();
		startColor = base.gameObject.GetComponent<Renderer>().material.color;
	}

	private void On_LongTapStart2Fingers(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			RandomColor();
		}
	}

	private void On_LongTap2Fingers(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			textMesh.text = gesture.actionTime.ToString("f2");
		}
	}

	private void On_LongTapEnd2Fingers(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			base.gameObject.GetComponent<Renderer>().material.color = startColor;
			textMesh.text = "Long tap me";
		}
	}

	private void On_Cancel2Fingers(Gesture gesture)
	{
		On_LongTapEnd2Fingers(gesture);
	}

	private void RandomColor()
	{
		base.gameObject.GetComponent<Renderer>().material.color = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
	}

	public TwoLongTapMe()
	{
	}




}
