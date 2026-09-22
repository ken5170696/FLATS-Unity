using System;
using InControl;
using UnityEngine;
using UnityEngine.UI;
public class GamepadInputFieldHelper : MonoBehaviour
{
	private InputField _inputField;

	private GameObject keyboard;

	private void Start()
	{
		_inputField = GetComponent<InputField>();
	}

	private void Update()
	{
		if (!_inputField.isFocused)
		{
			return;
		}
		if (keyboard == null && Menu.VRmode)
		{
			keyboard = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("Keyboard"));
			keyboard.transform.SetParent(base.transform.root.GetChild(0));
			keyboard.transform.position = new Vector3(0f, -150f, 500f);
			keyboard.transform.SetAsLastSibling();
		}
		if (Input.GetJoystickNames().Length <= 0)
		{
			return;
		}
		InputDevice activeDevice = InputManager.ActiveDevice;
		if (Input.GetButtonUp("Cancel") || activeDevice.LeftStickX.WasPressed || activeDevice.LeftStickY.WasPressed)
		{
			if (Menu.VRmode)
			{
				keyboard.GetComponent<Keyboard>().KeyboardInput("close");
			}
			else
			{
				_inputField.DeactivateInputField();
			}
		}
	}

	public GamepadInputFieldHelper()
	{
	}




}
