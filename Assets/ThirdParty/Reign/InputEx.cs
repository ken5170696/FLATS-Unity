using UnityEngine;
namespace Reign
{
	public static class InputEx
	{
		public static float AnalogTolerance;

		public static float AnalogTriggerTolerance;

		public static InputExButtonMap[] ButtonMappings;

		public static InputExAnalogMap[] AnalogMappings;

		static InputEx()
		{
			AnalogTolerance = 0.25f;
			AnalogTriggerTolerance = 0.1f;
			ButtonMappings = new InputExButtonMap[16]
			{
				new InputExButtonMap(ButtonTypes.CrossButtonLeft, ControllerPlayers.Any, "Left CrossButton - PlayerAny"),
				new InputExButtonMap(ButtonTypes.CrossButtonRight, ControllerPlayers.Any, "Right CrossButton - PlayerAny"),
				new InputExButtonMap(ButtonTypes.CrossButtonBottom, ControllerPlayers.Any, "Bottom CrossButton - PlayerAny"),
				new InputExButtonMap(ButtonTypes.CrossButtonTop, ControllerPlayers.Any, "Top CrossButton - PlayerAny"),
				new InputExButtonMap(ButtonTypes.DPadLeft, ControllerPlayers.Any, "Left DPadButton - PlayerAny", "Horizontal DPadAnalog - PlayerAny", false),
				new InputExButtonMap(ButtonTypes.DPadRight, ControllerPlayers.Any, "Right DPadButton - PlayerAny", "Horizontal DPadAnalog - PlayerAny", true),
				new InputExButtonMap(ButtonTypes.DPadDown, ControllerPlayers.Any, "Down DPadButton - PlayerAny", "Vertical DPadAnalog - PlayerAny", false),
				new InputExButtonMap(ButtonTypes.DPadUp, ControllerPlayers.Any, "Up DPadButton - PlayerAny", "Vertical DPadAnalog - PlayerAny", true),
				new InputExButtonMap(ButtonTypes.BumperLeft, ControllerPlayers.Any, "Left Bumper - PlayerAny"),
				new InputExButtonMap(ButtonTypes.BumperRight, ControllerPlayers.Any, "Right Bumper - PlayerAny"),
				new InputExButtonMap(ButtonTypes.AnalogLeft, ControllerPlayers.Any, "Left AnalogButton - PlayerAny"),
				new InputExButtonMap(ButtonTypes.AnalogRight, ControllerPlayers.Any, "Right AnalogButton - PlayerAny"),
				new InputExButtonMap(ButtonTypes.TriggerLeft, ControllerPlayers.Any, "Left TriggerButton - PlayerAny"),
				new InputExButtonMap(ButtonTypes.TriggerRight, ControllerPlayers.Any, "Right TriggerButton - PlayerAny"),
				new InputExButtonMap(ButtonTypes.Start, ControllerPlayers.Any, "Start Button - PlayerAny"),
				new InputExButtonMap(ButtonTypes.Back, ControllerPlayers.Any, "Back Button - PlayerAny")
			};
			AnalogMappings = new InputExAnalogMap[6]
			{
				new InputExAnalogMap(AnalogTypes.AxisLeftX, ControllerPlayers.Any, "Left AnalogX - PlayerAny"),
				new InputExAnalogMap(AnalogTypes.AxisLeftY, ControllerPlayers.Any, "Left AnalogY - PlayerAny"),
				new InputExAnalogMap(AnalogTypes.AxisRightX, ControllerPlayers.Any, "Right AnalogX - PlayerAny"),
				new InputExAnalogMap(AnalogTypes.AxisRightY, ControllerPlayers.Any, "Right AnalogY - PlayerAny"),
				new InputExAnalogMap(AnalogTypes.TriggerLeft, ControllerPlayers.Any, "Left Trigger - PlayerAny"),
				new InputExAnalogMap(AnalogTypes.TriggerRight, ControllerPlayers.Any, "Right Trigger - PlayerAny")
			};
		}

		public static string LogKeys()
		{
			string text = null;
			for (int i = 0; i != 430; i++)
			{
				KeyCode keyCode = (KeyCode)i;
				if (Input.GetKeyDown(keyCode))
				{
					text = "KeyPressed: " + keyCode;
					Debug.Log(text);
				}
			}
			return text;
		}

		public static string LogButtons()
		{
			string text = null;
			for (int i = 0; i != 16; i++)
			{
				ButtonTypes buttonTypes = (ButtonTypes)i;
				if (GetButtonDown(buttonTypes, ControllerPlayers.Any))
				{
					text = "ButtonPressed: " + buttonTypes;
					Debug.Log(text);
				}
			}
			return text;
		}

		public static string LogAnalogs()
		{
			string text = null;
			for (int i = 0; i != 6; i++)
			{
				AnalogTypes analogTypes = (AnalogTypes)i;
				float axis = GetAxis(analogTypes, ControllerPlayers.Any);
				if (axis != 0f)
				{
					text = string.Format("AnalogType {0} value: {1}", new object[2] { analogTypes, axis });
					Debug.Log(text);
				}
			}
			return text;
		}

		private static string findButtonName(ButtonTypes type, ControllerPlayers player, out InputExButtonMap mapping)
		{
			InputExButtonMap[] buttonMappings = ButtonMappings;
			foreach (InputExButtonMap inputExButtonMap in buttonMappings)
			{
				if (inputExButtonMap.Type == type && inputExButtonMap.Player == player)
				{
					mapping = inputExButtonMap;
					return inputExButtonMap.Name;
				}
			}
			Debug.LogError(string.Format("Failed to find Button {0} for Player {1}", new object[2] { type, player }));
			mapping = null;
			return "Unknown";
		}

		public static bool GetButton(ButtonTypes type, ControllerPlayers player)
		{
			InputExButtonMap mapping;
			string buttonName = findButtonName(type, player, out mapping);
			if (mapping != null && mapping.analogOn)
			{
				return true;
			}
			return Input.GetButton(buttonName);
		}

		public static bool GetButtonDown(ButtonTypes type, ControllerPlayers player)
		{
			InputExButtonMap mapping;
			string buttonName = findButtonName(type, player, out mapping);
			if (mapping != null && mapping.analogDown)
			{
				return true;
			}
			return Input.GetButtonDown(buttonName);
		}

		public static bool GetButtonUp(ButtonTypes type, ControllerPlayers player)
		{
			InputExButtonMap mapping;
			string buttonName = findButtonName(type, player, out mapping);
			if (mapping != null && mapping.analogUp)
			{
				return true;
			}
			return Input.GetButtonUp(buttonName);
		}

		private static string findAnalogName(AnalogTypes type, ControllerPlayers player)
		{
			InputExAnalogMap[] analogMappings = AnalogMappings;
			foreach (InputExAnalogMap inputExAnalogMap in analogMappings)
			{
				if (inputExAnalogMap.Type == type && inputExAnalogMap.Player == player)
				{
					return inputExAnalogMap.Name;
				}
			}
			Debug.LogError(string.Format("Failed to find Analog {0} for Player {1}", new object[2] { type, player }));
			return "Unknown";
		}

		private static float processAnalogValue(AnalogTypes type, float value)
		{
			float num = AnalogTolerance;
			switch (type)
			{
			case AnalogTypes.TriggerLeft:
			case AnalogTypes.TriggerRight:
				num = AnalogTriggerTolerance;
				break;
			}
			if (!(Mathf.Abs(value) <= num))
			{
				return value;
			}
			return 0f;
		}

		public static float GetAxis(AnalogTypes type, ControllerPlayers player)
		{
			return processAnalogValue(type, Input.GetAxisRaw(findAnalogName(type, player)));
		}

		public static float GetAxisRaw(AnalogTypes type, ControllerPlayers player)
		{
			return Input.GetAxisRaw(findAnalogName(type, player));
		}


	}
}
