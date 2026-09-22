using System;
using System.Runtime.InteropServices;
namespace Reign.Plugin
{
	public class MessageBoxPlugin_Dumy : IMessageBoxPlugin
	{
		public void Show(string title, string message, MessageBoxTypes type, MessageBoxOptions options, MessageBoxCallback callback)
		{
			if (callback != null)
			{
				callback(MessageBoxResult.Cancel);
			}
		}

		public void Update()
		{
		}

		public MessageBoxPlugin_Dumy()
		{
		}




	}
}
