using System;
using UnityEngine;
public class TwoFingerGesture
{
	public EasyTouch.GestureType currentGesture;

	public EasyTouch.GestureType oldGesture;

	public int finger0;

	public int finger1;

	public float startTimeAction;

	public float timeSinceStartAction;

	public Vector2 startPosition;

	public Vector2 position;

	public Vector2 deltaPosition;

	public Vector2 oldStartPosition;

	public float fingerDistance;

	public float oldFingerDistance;

	public GameObject pickedObject;

	public GameObject oldPickedObject;

	public Camera pickedCamera;

	public bool isGuiCamera;

	public bool isOverGui;

	public GameObject pickedUIElement;

	public bool dragStart;

	public bool swipeStart;

	public bool inSingleDoubleTaps;

	public float tapCurentTime;

	public void ClearPickedObjectData()
	{
		pickedObject = null;
		oldPickedObject = null;
		pickedCamera = null;
		isGuiCamera = false;
	}

	public void ClearPickedUIData()
	{
		isOverGui = false;
		pickedUIElement = null;
	}

	public TwoFingerGesture()
	{
		currentGesture = EasyTouch.GestureType.None;
		oldGesture = EasyTouch.GestureType.None;

	}




}
