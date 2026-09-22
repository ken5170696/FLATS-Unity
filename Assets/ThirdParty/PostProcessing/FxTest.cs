using System;
using UnityEngine;
public class FxTest : MonoBehaviour
{
	private void Start()
	{
		base.GetComponent<Camera>().depthTextureMode = DepthTextureMode.Depth;
	}

	private void Update()
	{
		if (Application.targetFrameRate != 60)
		{
			Application.targetFrameRate = 60;
		}
	}

	public FxTest()
	{
	}




}
