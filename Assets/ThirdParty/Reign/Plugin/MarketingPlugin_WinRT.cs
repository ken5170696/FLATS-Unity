using System;
namespace Reign.Plugin
{
	public class MarketingPlugin_WinRT : IIMarketingPlugin
	{
		public delegate void InitNativeMethod(MarketingPlugin_WinRT plugin);

		public IIMarketingPlugin Native;

		public static InitNativeMethod InitNative;

		public MarketingPlugin_WinRT()
		{
			InitNative(this);
		}

		public void OpenStore(MarketingDesc desc)
		{
			Native.OpenStore(desc);
		}

		public void OpenStoreForReview(MarketingDesc desc)
		{
			Native.OpenStoreForReview(desc);
		}




	}
}
