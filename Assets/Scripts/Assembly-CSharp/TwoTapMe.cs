using System;
using UnityEngine;
public class TwoTapMe : MonoBehaviour
{
	private void OnEnable()
	{
		EasyTouch.On_SimpleTap2Fingers += On_SimpleTap2Fingers;
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
		EasyTouch.On_SimpleTap2Fingers -= On_SimpleTap2Fingers;
	}

	private void On_SimpleTap2Fingers(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			RandomColor();
		}
	}

	private void RandomColor()
	{
		base.gameObject.GetComponent<Renderer>().material.color = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
	}

	public TwoTapMe()
	{
	}




}
