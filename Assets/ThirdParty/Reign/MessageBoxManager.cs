using System;
using System.Runtime.InteropServices;
using Reign.Plugin;
namespace Reign
{
	public static class MessageBoxManager
	{
		private static IMessageBoxPlugin plugin;

		static MessageBoxManager()
		{
			ReignServices.CheckStatus();
			plugin = new MessageBoxPlugin_WinRT();
			ReignServices.AddService(update, null, null);
		}

		public static void Show(string title, string message)
		{
			plugin.Show(title, message, MessageBoxTypes.Ok, new MessageBoxOptions(), null);
		}

		public static void Show(string title, string message, MessageBoxTypes type, MessageBoxCallback callback)
		{
			plugin.Show(title, message, type, new MessageBoxOptions(), callback);
		}

		public static void Show(string title, string message, MessageBoxTypes type, MessageBoxOptions options, MessageBoxCallback callback)
		{
			if (options == null)
			{
				options = new MessageBoxOptions();
			}
			plugin.Show(title, message, type, options, callback);
		}

		private static void update()
		{
			plugin.Update();
		}


	}
}
