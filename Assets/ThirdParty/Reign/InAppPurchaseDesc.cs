using System;

namespace Reign
{
	public class InAppPurchaseDesc
	{
		public bool Testing;

		public bool TestTrialMode;

		public bool ClearNativeCache;

		public InAppPurchaseID[] Editor_InAppIDs;

		public InAppPurchaseAPIs WinRT_InAppPurchaseAPI;

		public InAppPurchaseID[] WinRT_MicrosoftStore_InAppIDs;

		public InAppPurchaseAPIs WP8_InAppPurchaseAPI;

		public InAppPurchaseID[] WP8_MicrosoftStore_InAppIDs;

		public InAppPurchaseAPIs BB10_InAppPurchaseAPI;

		public InAppPurchaseID[] BB10_BlackBerryWorld_InAppIDs;

		public InAppPurchaseAPIs iOS_InAppPurchaseAPI;

		public InAppPurchaseID[] iOS_AppleStore_InAppIDs;

		public string iOS_AppleStore_SharedSecretKey;

		public InAppPurchaseAPIs Android_InAppPurchaseAPI;

		public InAppPurchaseID[] Android_GooglePlay_InAppIDs;

		public InAppPurchaseID[] Android_Amazon_InAppIDs;

		public InAppPurchaseID[] Android_Samsung_InAppIDs;

		public string Android_GooglePlay_Base64Key;

		public string Android_Samsung_ItemGroupID;

		public InAppPurchaseDesc()
		{
		}


	}
}
