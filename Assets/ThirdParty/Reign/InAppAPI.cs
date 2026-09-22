using System;
using System.Collections;
using System.Runtime.InteropServices;
using Reign.Plugin;
using UnityEngine;
namespace Reign
{
	public class InAppAPI
	{
		private IInAppPurchasePlugin plugin;

		private InAppPurchaseAPIs pluginAPI;

		private bool restoringProducts;

		private bool buyingProduct;

		private InAppPurchaseCreatedCallbackMethod createdCallback;

		private InAppPurchaseRestoreCallbackMethod restoreCallback;

		private InAppPurchaseBuyCallbackMethod buyCallback;

		public bool IsTrial
		{
			get
			{
				return plugin.IsTrial;
			}
		}

		internal void init(InAppPurchaseDesc desc, InAppPurchaseCreatedCallbackMethod createdCallback)
		{
			pluginAPI = desc.WinRT_InAppPurchaseAPI;
			this.createdCallback = createdCallback;
			plugin = InAppPurchaseAPI.New(desc, async_CreatedCallback);
		}

		internal void update()
		{
			plugin.Update();
		}

		private void async_CreatedCallback(bool succeeded)
		{
			ReignServices.Singleton.StartCoroutine(createdCallbackDelay(succeeded));
		}

		private IEnumerator createdCallbackDelay(bool succeeded)
		{
			yield return null;
			if (createdCallback != null)
			{
				createdCallback(succeeded);
			}
		}

		private void async_RestoreCallback(string inAppID, bool succeeded)
		{
			restoringProducts = false;
			saveBuyToPrefs(inAppID, succeeded);
			if (restoreCallback != null)
			{
				restoreCallback(inAppID, succeeded);
			}
		}

		private void async_BuyCallback(string inAppID, string receipt, bool succeeded)
		{
			buyingProduct = false;
			saveBuyToPrefs(inAppID, succeeded);
			if (buyCallback != null)
			{
				buyCallback(inAppID, receipt, succeeded);
			}
		}

		private void saveBuyToPrefs(string inAppID, bool succeeded)
		{
			if (succeeded)
			{
				PlayerPrefs.SetInt("ReignIAP_PurchasedAwarded_" + inAppID, 1);
				PlayerPrefs.SetInt(string.Concat(pluginAPI, "_", inAppID), 1);
				PlayerPrefs.Save();
			}
		}

		public void ClearPlayerPrefData()
		{
			InAppPurchaseID[] inAppIDs = plugin.InAppIDs;
			foreach (InAppPurchaseID inAppPurchaseID in inAppIDs)
			{
				string key = string.Concat(pluginAPI, "_", inAppPurchaseID.ID);
				if (PlayerPrefs.HasKey(key))
				{
					PlayerPrefs.DeleteKey(key);
				}
			}
		}

		public int GetAppIndexForAppID(string inAppID)
		{
			for (int i = 0; i != plugin.InAppIDs.Length; i++)
			{
				if (plugin.InAppIDs[i].ID == inAppID)
				{
					return i;
				}
			}
			return -1;
		}

		public InAppPurchaseID GetAppID(string inAppID)
		{
			for (int i = 0; i != plugin.InAppIDs.Length; i++)
			{
				if (plugin.InAppIDs[i].ID == inAppID)
				{
					return plugin.InAppIDs[i];
				}
			}
			return null;
		}

		public bool IsPurchased(int inAppIndex)
		{
			if (plugin.InAppIDs[inAppIndex].Type == InAppPurchaseTypes.Consumable)
			{
				return false;
			}
			return PlayerPrefs.GetInt(string.Concat(pluginAPI, "_", plugin.InAppIDs[inAppIndex].ID)) == 1;
		}

		public bool IsPurchased(string inAppID)
		{
			int appIndexForAppID = GetAppIndexForAppID(inAppID);
			if (appIndexForAppID == -1)
			{
				return false;
			}
			if (plugin.InAppIDs[appIndexForAppID].Type == InAppPurchaseTypes.Consumable)
			{
				return false;
			}
			return PlayerPrefs.GetInt(string.Concat(pluginAPI, "_", plugin.InAppIDs[appIndexForAppID].ID)) == 1;
		}

		public void GetProductInfo(InAppPurchaseGetProductInfoCallbackMethod callback)
		{
			if (restoringProducts || buyingProduct)
			{
				Debug.LogError("You must wait for the last restore, buy or consume to finish!");
				if (callback != null)
				{
					callback(null, false);
				}
			}
			else
			{
				plugin.GetProductInfo(callback);
			}
		}

		public void Restore(InAppPurchaseRestoreCallbackMethod restoreCallback)
		{
			if (restoringProducts || buyingProduct)
			{
				Debug.LogError("You must wait for the last restore, buy or consume to finish!");
				if (restoreCallback != null)
				{
					restoreCallback(null, false);
				}
			}
			else
			{
				restoringProducts = true;
				this.restoreCallback = restoreCallback;
				plugin.Restore(async_RestoreCallback);
			}
		}

		public void Buy(InAppPurchaseID inAppID, InAppPurchaseBuyCallbackMethod buyCallback)
		{
			Buy(inAppID.ID, buyCallback);
		}

		public void Buy(int inAppIndex, InAppPurchaseBuyCallbackMethod buyCallback)
		{
			Buy(plugin.InAppIDs[inAppIndex].ID, buyCallback);
		}

		public void Buy(string inAppID, InAppPurchaseBuyCallbackMethod buyCallback)
		{
			if (buyingProduct || restoringProducts)
			{
				Debug.LogError("You must wait for the last buy, restore or consume to finish!");
				if (buyCallback != null)
				{
					buyCallback(inAppID, null, false);
				}
				return;
			}
			buyingProduct = true;
			this.buyCallback = buyCallback;
			if (IsPurchased(inAppID))
			{
				Debug.Log("InApp already puchased: " + inAppID);
				buyingProduct = false;
				if (buyCallback != null)
				{
					buyCallback(inAppID, null, true);
				}
			}
			else
			{
				plugin.BuyInApp(inAppID, async_BuyCallback);
			}
		}

		public void AwardInterruptedPurchases(InAppPurchaseAwardCallbackMethod callback)
		{
			if (callback == null)
			{
				return;
			}
			InAppPurchaseID[] inAppIDs = plugin.InAppIDs;
			foreach (InAppPurchaseID inAppPurchaseID in inAppIDs)
			{
				if (PlayerPrefs.HasKey("ReignIAP_PurchasedAwarded_" + inAppPurchaseID.ID) && PlayerPrefs.GetInt("ReignIAP_PurchasedAwarded_" + inAppPurchaseID.ID) == 0)
				{
					PlayerPrefs.SetInt("ReignIAP_PurchasedAwarded_" + inAppPurchaseID.ID, 1);
					callback(inAppPurchaseID.ID, true);
				}
			}
		}

		public InAppAPI()
		{
		}




	}
}
