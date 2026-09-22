using System;
using UnityEngine;
public class DoubleTapMe : MonoBehaviour
{
	private void OnEnable()
	{
		EasyTouch.On_DoubleTap += On_DoubleTap;
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
		EasyTouch.On_DoubleTap -= On_DoubleTap;
	}

	private void On_DoubleTap(Gesture gesture)
	{
		if (gesture.pickedObject == base.gameObject)
		{
			base.gameObject.GetComponent<Renderer>().material.color = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
		}
	}

	public DoubleTapMe()
	{
	}




}
