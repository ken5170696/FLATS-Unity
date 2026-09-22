using System;
namespace InControl
{
	public class UnityButtonSource : InputControlSource
	{
		public int ButtonIndex;

		public UnityButtonSource(int buttonIndex)
		{
			ButtonIndex = buttonIndex;
		}

		public float GetValue(InputDevice inputDevice)
		{
			if (!GetState(inputDevice))
			{
				return 0f;
			}
			return 1f;
		}

		public bool GetState(InputDevice inputDevice)
		{
			UnityInputDevice unityInputDevice = inputDevice as UnityInputDevice;
			return unityInputDevice.ReadRawButtonState(ButtonIndex);
		}




	}
}
