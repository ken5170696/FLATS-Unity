using System;
using UnityEngine;
public class CubeSelect : MonoBehaviour
{
	private GameObject cube;

	private void OnEnable()
	{
		EasyTouch.On_SimpleTap += On_SimpleTap;
	}

	private void OnDestroy()
	{
		EasyTouch.On_SimpleTap -= On_SimpleTap;
	}

	private void Start()
	{
		cube = null;
	}

	private void On_SimpleTap(Gesture gesture)
	{
		if (gesture.pickedObject != null && gesture.pickedObject.name == "Cube")
		{
			ResteColor();
			cube = gesture.pickedObject;
			cube.GetComponent<Renderer>().material.color = Color.red;
		}
	}

	private void ResteColor()
	{
		if (cube != null)
		{
			cube.GetComponent<Renderer>().material.color = new Color(0.23529412f, 0.56078434f, 67f / 85f);
		}
	}

	public CubeSelect()
	{
	}




}
