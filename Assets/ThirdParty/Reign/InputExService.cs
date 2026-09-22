using System;
using Reign;
using UnityEngine;
public class InputExService : MonoBehaviour
{
	private static InputExService singleton;

	private void Start()
	{
		if (singleton != null)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		singleton = this;
		UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
	}

	private void Update()
	{
		InputExButtonMap[] buttonMappings = InputEx.ButtonMappings;
		foreach (InputExButtonMap inputExButtonMap in buttonMappings)
		{
			inputExButtonMap.update();
		}
	}

	public InputExService()
	{
	}




}
