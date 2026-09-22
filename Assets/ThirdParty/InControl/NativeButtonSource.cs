using System;
namespace InControl
{
	public class NativeButtonSource : InputControlSource
	{
		public int ButtonIndex;

		public NativeButtonSource(int buttonIndex)
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
			NativeInputDevice nativeInputDevice = inputDevice as NativeInputDevice;
			return nativeInputDevice.ReadRawButtonState(ButtonIndex);
		}




	}
}
