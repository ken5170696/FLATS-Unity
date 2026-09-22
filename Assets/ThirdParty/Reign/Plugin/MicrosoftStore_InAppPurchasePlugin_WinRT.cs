using System;
using System.Runtime.InteropServices;
namespace Reign.Plugin
{
	public class MicrosoftStore_InAppPurchasePlugin_WinRT : IInAppPurchasePlugin
	{
		public delegate void InitNativeMethod(MicrosoftStore_InAppPurchasePlugin_WinRT plugin, InAppPurchaseDesc desc, InAppPurchaseCreatedCallbackMethod createdCallback);

		public IInAppPurchasePlugin Native;

		public static InitNativeMethod InitNative;

		public bool IsTrial
		{
			get
			{
				return Native.IsTrial;
			}
		}

		public InAppPurchaseID[] InAppIDs
		{
			get
			{
				return Native.InAppIDs;
			}
		}

		public MicrosoftStore_InAppPurchasePlugin_WinRT(InAppPurchaseDesc desc, InAppPurchaseCreatedCallbackMethod createdCallback)
		{
			InitNative(this, desc, createdCallback);
		}

		public void GetProductInfo(InAppPurchaseGetProductInfoCallbackMethod callback)
		{
			Native.GetProductInfo(callback);
		}

		public void Restore(InAppPurchaseRestoreCallbackMethod restoreCallback)
		{
			Native.Restore(restoreCallback);
		}

		public void BuyInApp(string inAppID, InAppPurchaseBuyCallbackMethod purchasedCallback)
		{
			Native.BuyInApp(inAppID, purchasedCallback);
		}

		public void Update()
		{
			Native.Update();
		}




	}
}
