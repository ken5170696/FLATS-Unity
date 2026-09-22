using System;
using UnityEngine;
public class LongTapMe : MonoBehaviour
{
	private TextMesh textMesh;

	private Color startColor;

	private void OnEnable()
	{
		EasyTouch.On_LongTapStart += On_LongTapStart;
		EasyTouch.On_LongTap += On_LongTap;
		EasyTouch.On_LongTapEnd += On_LongTapEnd;
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
		EasyTouch.On_LongTapStart -= On_LongTapStart;
		EasyTouch.On_LongTap -= On_LongTap;
		EasyTouch.On_LongTapEnd -= On_LongTapEnd;
	}

	private void Start()
	{
		textMesh = GetComponentInChildren<TextMesh>();
		startColor = base.gameObject.GetComponent<Renderer>().material.color;
	}

	private void On_LongTapStart(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			RandomColor();
		}
	}

	private void On_LongTap(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			textMesh.text = gesture.actionTime.ToString("f2");
		}
	}

	private void On_LongTapEnd(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			base.gameObject.GetComponent<Renderer>().material.color = startColor;
			textMesh.text = "Long tap me";
		}
	}

	private void RandomColor()
	{
		base.gameObject.GetComponent<Renderer>().material.color = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
	}

	public LongTapMe()
	{
	}




}
