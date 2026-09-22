using System;
using System.Collections.Generic;
namespace ExitGames.Client.Photon
{
	public class EventData
	{
		public byte Code;

		public Dictionary<byte, object> Parameters;

		public object this[byte key]
		{
			get
			{
				object value;
				Parameters.TryGetValue(key, out value);
				return value;
			}
			set
			{
				Parameters[key] = value;
			}
		}

		public override string ToString()
		{
			return string.Format("Event {0}.", new object[1] { Code.ToString() });
		}

		public string ToStringFull()
		{
			return string.Format("Event {0}: {1}", new object[2]
			{
				Code,
				SupportClass.DictionaryToString(Parameters)
			});
		}

		public EventData()
		{
		}




	}
}
