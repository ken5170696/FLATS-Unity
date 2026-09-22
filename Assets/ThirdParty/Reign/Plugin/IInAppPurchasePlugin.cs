namespace Reign.Plugin
{
	public interface IInAppPurchasePlugin
	{
		bool IsTrial { get; }

		InAppPurchaseID[] InAppIDs { get; }

		void GetProductInfo(InAppPurchaseGetProductInfoCallbackMethod callback);

		void Restore(InAppPurchaseRestoreCallbackMethod restoreCallback);

		void BuyInApp(string inAppID, InAppPurchaseBuyCallbackMethod purchasedCallback);

		void Update();
	}
}
