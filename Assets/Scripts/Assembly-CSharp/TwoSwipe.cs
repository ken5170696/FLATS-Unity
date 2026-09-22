using System;
using UnityEngine;
using UnityEngine.UI;
public class TwoSwipe : MonoBehaviour
{
	public GameObject trail;

	public Text swipeData;

	private void OnEnable()
	{
		EasyTouch.On_SwipeStart2Fingers += On_SwipeStart2Fingers;
		EasyTouch.On_Swipe2Fingers += On_Swipe2Fingers;
		EasyTouch.On_SwipeEnd2Fingers += On_SwipeEnd2Fingers;
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
		EasyTouch.On_SwipeStart2Fingers -= On_SwipeStart2Fingers;
		EasyTouch.On_Swipe2Fingers -= On_Swipe2Fingers;
		EasyTouch.On_SwipeEnd2Fingers -= On_SwipeEnd2Fingers;
	}

	private void On_SwipeStart2Fingers(Gesture gesture)
	{
		swipeData.text = "You start a swipe";
	}

	private void On_Swipe2Fingers(Gesture gesture)
	{
		Vector3 touchToWorldPoint = gesture.GetTouchToWorldPoint(5f);
		trail.transform.position = touchToWorldPoint;
	}

	private void On_SwipeEnd2Fingers(Gesture gesture)
	{
		float swipeOrDragAngle = gesture.GetSwipeOrDragAngle();
		swipeData.text = string.Concat("Last swipe : ", gesture.swipe.ToString(), " /  vector : ", gesture.swipeVector.normalized, " / angle : ", swipeOrDragAngle.ToString("f2"));
	}

	public TwoSwipe()
	{
	}




}
