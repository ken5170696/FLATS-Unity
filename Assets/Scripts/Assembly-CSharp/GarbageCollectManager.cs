using System;
using UnityEngine;
public class GarbageCollectManager : MonoBehaviour
{
	public int frameFreq;

	private void Update()
	{
		if (Time.frameCount % frameFreq == 0)
		{
			GC.Collect();
		}
	}

	public GarbageCollectManager()
	{
		frameFreq = 60;

	}




}
