using System;
using UnityEngine;
namespace InControl
{
	public class UnityMouseButtonSource : InputControlSource
	{
		public int ButtonId;

		public UnityMouseButtonSource()
		{
		}

		public UnityMouseButtonSource(int buttonId)
		{
			ButtonId = buttonId;
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
			return Input.GetMouseButton(ButtonId);
		}




	}
}
