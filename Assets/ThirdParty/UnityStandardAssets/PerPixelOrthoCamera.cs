using System;
using UnityEngine;
public class PerPixelOrthoCamera : MonoBehaviour
{
	public bool ScreenScaleValues;

	public bool BottomLeftMode;

	private new Camera camera;

	private void Start()
	{
		camera = GetComponent<Camera>();
		LateUpdate();
	}

	private void LateUpdate()
	{
		if (BottomLeftMode)
		{
			Vector3 position = camera.transform.position;
			if (ScreenScaleValues)
			{
				position.x = (float)Screen.width / 2f;
				position.y = (float)Screen.height / 2f;
			}
			else
			{
				position.x = camera.pixelWidth / 2f;
				position.y = camera.pixelHeight / 2f;
			}
			camera.transform.position = position;
		}
		if (ScreenScaleValues)
		{
			camera.orthographicSize = (float)Screen.height / 2f;
			return;
		}
		camera.orthographicSize = camera.pixelHeight / 2f;
		camera.aspect = camera.pixelWidth / camera.pixelHeight;
	}

	public PerPixelOrthoCamera()
	{
	}




}
