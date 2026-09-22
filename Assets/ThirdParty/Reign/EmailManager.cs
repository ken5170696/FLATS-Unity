using System;
using System.Runtime.InteropServices;
using Reign.Plugin;

namespace Reign
{
	public static class EmailManager
	{
		private static IEmailPlugin plugin;

		static EmailManager()
		{
			ReignServices.CheckStatus();
			plugin = new EmailPlugin_WinRT();
		}

		public static void Send(string to, string subject, string body)
		{
			plugin.Send(to, subject, body);
		}


	}
}
