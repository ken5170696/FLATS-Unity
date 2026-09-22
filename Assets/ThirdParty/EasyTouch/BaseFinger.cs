using System;
using UnityEngine;
public class BaseFinger
{
	public int fingerIndex;

	public int touchCount;

	public Vector2 startPosition;

	public Vector2 position;

	public Vector2 deltaPosition;

	public float actionTime;

	public float deltaTime;

	public Camera pickedCamera;

	public GameObject pickedObject;

	public bool isGuiCamera;

	public bool isOverGui;

	public GameObject pickedUIElement;

	public Gesture GetGesture()
	{
		Gesture gesture = new Gesture();
		gesture.fingerIndex = fingerIndex;
		gesture.touchCount = touchCount;
		gesture.startPosition = startPosition;
		gesture.position = position;
		gesture.deltaPosition = deltaPosition;
		gesture.actionTime = actionTime;
		gesture.deltaTime = deltaTime;
		gesture.isOverGui = isOverGui;
		gesture.pickedCamera = pickedCamera;
		gesture.pickedObject = pickedObject;
		gesture.isGuiCamera = isGuiCamera;
		gesture.pickedUIElement = pickedUIElement;
		return gesture;
	}

	public BaseFinger()
	{
	}




}
