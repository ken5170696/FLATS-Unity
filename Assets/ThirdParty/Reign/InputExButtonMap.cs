using System;
using UnityEngine;
namespace Reign
{
	public class InputExButtonMap
	{
		public ButtonTypes Type;

		public ControllerPlayers Player;

		public string Name;

		public string AnalogName;

		public bool AnalogPositive;

		internal bool analogOn;

		internal bool analogDown;

		internal bool analogUp;

		public InputExButtonMap(ButtonTypes type, ControllerPlayers player, string name)
		{
			Type = type;
			Player = player;
			Name = name;
		}

		public InputExButtonMap(ButtonTypes type, ControllerPlayers player, string name, string analogName, bool analogPositive)
		{
			Type = type;
			Player = player;
			Name = name;
			AnalogName = analogName;
			AnalogPositive = analogPositive;
		}

		internal void update()
		{
			if (AnalogName != null)
			{
				bool flag = analogOn;
				if (AnalogPositive)
				{
					analogOn = (double)Input.GetAxisRaw(AnalogName) >= 0.5;
				}
				else
				{
					analogOn = (double)Input.GetAxisRaw(AnalogName) <= -0.5;
				}
				if (analogOn && !flag)
				{
					analogDown = true;
				}
				else
				{
					analogDown = false;
				}
				if (!analogOn && flag)
				{
					analogUp = true;
				}
				else
				{
					analogUp = false;
				}
			}
		}




	}
}
