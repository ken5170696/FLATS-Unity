namespace Reign.Plugin
{
	public interface IIMarketingPlugin
	{
		void OpenStore(MarketingDesc desc);

		void OpenStoreForReview(MarketingDesc desc);
	}
}
