using System;
using InControl;
using UnityEngine;
public class BackWithCancel : MonoBehaviour
{
	public bool destroy;

	private void OnEnable()
	{
		Menu.backWithCancel = true;
	}

	private void OnDisable()
	{
		Menu.backWithCancel = false;
	}

	private void Update()
	{
        if (FlatsControls.Capturing) return;
		InputDevice activeDevice = InputManager.ActiveDevice;
		if (Input.GetKeyUp(KeyCode.Escape) || activeDevice.CommandWasReleased || activeDevice.Action2.WasPressed)
		{
			if (destroy)
			{
				UnityEngine.Object.Destroy(base.gameObject);
			}
			else
			{
				base.gameObject.SetActive(false);
			}
		}
	}

	public BackWithCancel()
	{
	}




}
