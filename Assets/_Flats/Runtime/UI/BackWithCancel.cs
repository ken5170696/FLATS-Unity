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
		InputDevice activeDevice = InputManager.ActiveDevice;
		if (Input.GetKeyUp(KeyCode.Escape) || activeDevice.CommandWasReleased || (!Menu.customControlEnabled && activeDevice.Action2.WasPressed) || (Menu.customControlEnabled && Input.GetButtonDown(Menu.customControl["Pick"])))
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
