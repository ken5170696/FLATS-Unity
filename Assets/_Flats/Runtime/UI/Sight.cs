using System;
using UnityEngine;
public class Sight : MonoBehaviour
{
	public Camera rtc;

	private void Start()
	{
		rtc = GetComponent<Camera>();
	}

	private void Update()
	{
		if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null) rtc.Render();
	}

	public Sight()
	{
	}




}
