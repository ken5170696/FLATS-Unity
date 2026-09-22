using System;
using System.Runtime.InteropServices;
namespace Reign.Plugin
{
	public class Dumy_InAppPurchasePlugin : IInAppPurchasePlugin
	{
		public bool IsTrial { get; private set; }

		public InAppPurchaseID[] InAppIDs { get; private set; }

		public Dumy_InAppPurchasePlugin(InAppPurchaseDesc desc, InAppPurchaseCreatedCallbackMethod callback)
		{
			IsTrial = desc.TestTrialMode;
			InAppIDs = new InAppPurchaseID[0];
			if (callback != null)
			{
				callback(true);
			}
		}

		public void GetProductInfo(InAppPurchaseGetProductInfoCallbackMethod callback)
		{
		}

		public void Restore(InAppPurchaseRestoreCallbackMethod restoreCallback)
		{
		}

		public void BuyInApp(string inAppID, InAppPurchaseBuyCallbackMethod purchasedCallback)
		{
			if (purchasedCallback != null)
			{
				purchasedCallback(inAppID, null, false);
			}
		}

		public void Update()
		{
		}




	}
}
