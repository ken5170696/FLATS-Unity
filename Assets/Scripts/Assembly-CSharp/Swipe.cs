using System;
using UnityEngine;
using UnityEngine.UI;
public class Swipe : MonoBehaviour
{
	public GameObject trail;

	public Text swipeText;

	private void OnEnable()
	{
		EasyTouch.On_SwipeStart += On_SwipeStart;
		EasyTouch.On_Swipe += On_Swipe;
		EasyTouch.On_SwipeEnd += On_SwipeEnd;
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
		EasyTouch.On_SwipeStart -= On_SwipeStart;
		EasyTouch.On_Swipe -= On_Swipe;
		EasyTouch.On_SwipeEnd -= On_SwipeEnd;
	}

	private void On_SwipeStart(Gesture gesture)
	{
		swipeText.text = "You start a swipe";
	}

	private void On_Swipe(Gesture gesture)
	{
		Vector3 touchToWorldPoint = gesture.GetTouchToWorldPoint(5f);
		trail.transform.position = touchToWorldPoint;
	}

	private void On_SwipeEnd(Gesture gesture)
	{
		float swipeOrDragAngle = gesture.GetSwipeOrDragAngle();
		swipeText.text = string.Concat("Last swipe : ", gesture.swipe.ToString(), " /  vector : ", gesture.swipeVector.normalized, " / angle : ", swipeOrDragAngle.ToString("f2"));
	}

	public Swipe()
	{
	}




}
