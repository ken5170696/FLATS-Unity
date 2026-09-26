using System;
using System.Runtime.InteropServices;
using InControl;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public class Keyboard : MonoBehaviour
{
	public string mode;

	public string[] keys;

	public static bool isOpen;

	private InputField target;

	private InputField currentInputField;

	private Transform mt;

	private bool shift;

	private bool intKeyboard;

	private void Start()
	{
		mt = base.transform;
		mt.localScale = new Vector3(0.8f, 0.8f, 0.8f);
		mt.localPosition = new Vector3(0f, -200f, 200f);
		mt.localEulerAngles = new Vector3(30f, 0f, 0f);
		target = EventSystem.current.currentSelectedGameObject.GetComponent<InputField>();
		if (target.contentType == InputField.ContentType.IntegerNumber)
		{
			intKeyboard = true;
		}
		if (intKeyboard)
		{
			currentInputField = mt.GetChild(1).GetChild(11).GetComponent<InputField>();
			mt.GetChild(1).gameObject.SetActive(true);
		}
		else
		{
			currentInputField = mt.GetChild(0).GetChild(33).GetComponent<InputField>();
			currentInputField.text = target.text;
			for (int i = 0; i < 26; i++)
			{
				mt.GetChild(0).GetChild(i).GetChild(0)
					.GetComponent<Text>()
					.text = keys[i];
			}
			mt.GetChild(0).gameObject.SetActive(true);
		}
		isOpen = true;
	}

	private void Update()
	{
		InputDevice activeDevice = InputManager.ActiveDevice;
		if ((!Menu.customControlEnabled && activeDevice.Action2.WasPressed) || (Menu.customControlEnabled && FlatsControls.LegacyPad(Menu.customControl["Pick"], 1)))
		{
			KeyboardInput("backspace");
		}
		else if ((!Menu.customControlEnabled && activeDevice.Action3.WasPressed) || (Menu.customControlEnabled && FlatsControls.LegacyPad(Menu.customControl["Reload"], 1)))
		{
			KeyboardInput("clear");
		}
		else if (!Menu.customControlEnabled && activeDevice.CommandWasPressed)
		{
			KeyboardInput("ok");
		}
		if (!target.gameObject.activeInHierarchy)
		{
			isOpen = false;
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	public void KeyboardInput(string key)
	{
		MonoBehaviour.print("test");
		if (key == "shift" && mode == "abc")
		{
			shift = !shift;
			if (shift)
			{
				for (int i = 0; i < 26; i++)
				{
					mt.GetChild(0).GetChild(i).GetChild(0)
						.GetComponent<Text>()
						.text = keys[i].ToUpper();
				}
			}
			else
			{
				for (int j = 0; j < 26; j++)
				{
					mt.GetChild(0).GetChild(j).GetChild(0)
						.GetComponent<Text>()
						.text = keys[j];
				}
			}
			return;
		}
		switch (key)
		{
		case "abc/123":
			mt.GetChild(0).GetChild(28).GetChild(0)
				.GetComponent<Text>()
				.text = mode;
			if (mode == "abc")
			{
				mode = "123";
				for (int k = 26; k < 51; k++)
				{
					mt.GetChild(0).GetChild(k - 26).GetChild(0)
						.GetComponent<Text>()
						.text = keys[k];
				}
				break;
			}
			mode = "abc";
			if (shift)
			{
				for (int l = 0; l < 26; l++)
				{
					mt.GetChild(0).GetChild(l).GetChild(0)
						.GetComponent<Text>()
						.text = keys[l].ToUpper();
				}
			}
			else
			{
				for (int m = 0; m < 26; m++)
				{
					mt.GetChild(0).GetChild(m).GetChild(0)
						.GetComponent<Text>()
						.text = keys[m];
				}
			}
			break;
		case ",":
		case ".":
			currentInputField.text += key;
			break;
		case "space":
			currentInputField.text += " ";
			break;
		case "backspace":
		{
			string text = currentInputField.text;
			if (text.Length > 0)
			{
				currentInputField.text = text.Remove(text.Length - 1);
			}
			break;
		}
		case "clear":
			currentInputField.text = "";
			break;
		case "ok":
			target.text = currentInputField.text;
			isOpen = false;
			UnityEngine.Object.Destroy(base.gameObject);
			break;
		default:
			if (intKeyboard)
			{
				currentInputField.text = key;
			}
			else
			{
				currentInputField.text += mt.GetChild(0).GetChild(int.Parse(key)).GetChild(0)
					.GetComponent<Text>()
					.text;
			}
			break;
		}
	}

	private void OnDestroy()
	{
		isOpen = false;
	}

	public Keyboard()
	{
		mode = "abc";

	}




}
