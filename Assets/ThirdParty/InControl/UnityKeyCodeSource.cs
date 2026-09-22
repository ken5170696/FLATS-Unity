using System;
using UnityEngine;
namespace InControl
{
	public class UnityKeyCodeSource : InputControlSource
	{
		public KeyCode[] KeyCodeList;

		public UnityKeyCodeSource()
		{
		}

		public UnityKeyCodeSource(params KeyCode[] keyCodeList)
		{
			KeyCodeList = keyCodeList;
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
			for (int i = 0; i < KeyCodeList.Length; i++)
			{
				if (Input.GetKey(KeyCodeList[i]))
				{
					return true;
				}
			}
			return false;
		}




	}
}
