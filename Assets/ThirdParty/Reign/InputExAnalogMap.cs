using System;

namespace Reign
{
	public class InputExAnalogMap
	{
		public AnalogTypes Type;

		public ControllerPlayers Player;

		public string Name;

		public InputExAnalogMap(AnalogTypes type, ControllerPlayers player, string name)
		{
			Type = type;
			Player = player;
			Name = name;
		}


	}
}
