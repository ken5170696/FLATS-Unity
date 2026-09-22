using System;
using UnityEngine;

public class Finger : BaseFinger
{
	public float startTimeAction;

	public Vector2 oldPosition;

	public int tapCount;

	public TouchPhase phase;

	public EasyTouch.GestureType gesture;

	public EasyTouch.SwipeDirection oldSwipeType;

	public Finger()
	{
	}


}
