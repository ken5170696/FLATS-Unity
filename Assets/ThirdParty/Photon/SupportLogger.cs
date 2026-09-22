using System;
using UnityEngine;
public class SupportLogger : MonoBehaviour
{
	public bool LogTrafficStats;

	public void Start()
	{
		GameObject gameObject = GameObject.Find("PunSupportLogger");
		if (gameObject == null)
		{
			gameObject = new GameObject("PunSupportLogger");
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			SupportLogging supportLogging = gameObject.AddComponent<SupportLogging>();
			supportLogging.LogTrafficStats = LogTrafficStats;
		}
	}

	public SupportLogger()
	{
		LogTrafficStats = true;

	}




}
