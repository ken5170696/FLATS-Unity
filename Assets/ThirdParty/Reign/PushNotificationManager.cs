using Reign.Plugin;
namespace Reign
{
	public static class PushNotificationManager
	{
		private static IPushNotificationPlugin plugin;

		static PushNotificationManager()
		{
			plugin = new PushNotificationPlugin_WinRT();
		}

		public static void Init(PushNotificationsDesc desc)
		{
			plugin.Init(desc);
		}


	}
}
