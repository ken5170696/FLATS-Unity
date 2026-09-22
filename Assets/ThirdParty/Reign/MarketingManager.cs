using Reign.Plugin;
namespace Reign
{
	public static class MarketingManager
	{
		private static IIMarketingPlugin plugin;

		static MarketingManager()
		{
			ReignServices.CheckStatus();
			plugin = new MarketingPlugin_WinRT();
		}

		public static void OpenStore(MarketingDesc desc)
		{
			plugin.OpenStore(desc);
		}

		public static void OpenStoreForReview(MarketingDesc desc)
		{
			plugin.OpenStoreForReview(desc);
		}


	}
}
