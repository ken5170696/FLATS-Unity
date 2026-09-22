using System;
using System.Runtime.InteropServices;
using Reign;
using UnityEngine;
using UnityEngine.UI;
public class IAPDemo : MonoBehaviour
{
	private static bool created;

	private string item1;

	private string item2;

	private string item3;

	public Text StatusText;

	public Button BuyDurableButton;

	public Button BuyConsumableButton;

	public Button RestoreButton;

	public Button GetPriceInfoButton;

	public Button BackButton;

	private void Start()
	{
		BuyDurableButton.Select();
		BuyDurableButton.onClick.AddListener(buyDurableClicked);
		BuyConsumableButton.onClick.AddListener(buyConsumableClicked);
		RestoreButton.onClick.AddListener(restoreClicked);
		GetPriceInfoButton.onClick.AddListener(getPriceInfoClicked);
		BackButton.onClick.AddListener(backClicked);
		if (!created)
		{
			created = true;
			InAppPurchaseID[] array = new InAppPurchaseID[3]
			{
				new InAppPurchaseID(item1, 1.99m, "$", InAppPurchaseTypes.NonConsumable),
				new InAppPurchaseID(item2, 0.99m, "$", InAppPurchaseTypes.NonConsumable),
				new InAppPurchaseID(item3, 2.49m, "$", InAppPurchaseTypes.Consumable)
			};
			InAppPurchaseDesc inAppPurchaseDesc = new InAppPurchaseDesc();
			inAppPurchaseDesc.Testing = true;
			inAppPurchaseDesc.ClearNativeCache = false;
			inAppPurchaseDesc.Editor_InAppIDs = array;
			inAppPurchaseDesc.WinRT_InAppPurchaseAPI = InAppPurchaseAPIs.MicrosoftStore;
			inAppPurchaseDesc.WinRT_MicrosoftStore_InAppIDs = array;
			inAppPurchaseDesc.WP8_InAppPurchaseAPI = InAppPurchaseAPIs.MicrosoftStore;
			inAppPurchaseDesc.WP8_MicrosoftStore_InAppIDs = array;
			inAppPurchaseDesc.BB10_InAppPurchaseAPI = InAppPurchaseAPIs.BlackBerryWorld;
			inAppPurchaseDesc.BB10_BlackBerryWorld_InAppIDs = array;
			inAppPurchaseDesc.iOS_InAppPurchaseAPI = InAppPurchaseAPIs.AppleStore;
			inAppPurchaseDesc.iOS_AppleStore_InAppIDs = array;
			inAppPurchaseDesc.iOS_AppleStore_SharedSecretKey = "";
			inAppPurchaseDesc.Android_InAppPurchaseAPI = InAppPurchaseAPIs.GooglePlay;
			inAppPurchaseDesc.Android_GooglePlay_InAppIDs = array;
			inAppPurchaseDesc.Android_GooglePlay_Base64Key = "";
			inAppPurchaseDesc.Android_Amazon_InAppIDs = array;
			inAppPurchaseDesc.Android_Samsung_InAppIDs = array;
			inAppPurchaseDesc.Android_Samsung_ItemGroupID = "";
			InAppPurchaseManager.Init(inAppPurchaseDesc, createdCallback);
		}
	}

	private void buyDurableClicked()
	{
		StatusText.text = "";
		InAppPurchaseManager.MainInAppAPI.Buy(item1, buyAppCallback);
	}

	private void buyConsumableClicked()
	{
		StatusText.text = "";
		InAppPurchaseManager.MainInAppAPI.Buy(item3, buyAppCallback);
	}

	private void restoreClicked()
	{
		StatusText.text = "";
		InAppPurchaseManager.MainInAppAPI.Restore(restoreAppsCallback);
	}

	private void getPriceInfoClicked()
	{
		StatusText.text = "";
		InAppPurchaseManager.MainInAppAPI.GetProductInfo(productInfoCallback);
	}

	private void backClicked()
	{
		Application.LoadLevel("MainDemo");
	}

	private void createdCallback(bool succeeded)
	{
		StatusText.text = "Init: " + succeeded + Environment.NewLine + Environment.NewLine;
		InAppPurchaseManager.MainInAppAPI.AwardInterruptedPurchases(awardInterruptedPurchases);
	}

	private void awardInterruptedPurchases(string inAppID, bool succeeded)
	{
		int appIndexForAppID = InAppPurchaseManager.MainInAppAPI.GetAppIndexForAppID(inAppID);
		if (appIndexForAppID != -1)
		{
			Text statusText = StatusText;
			object text = statusText.text;
			statusText.text = string.Concat(text, "Interrupted Restore Status: ", inAppID, ": ", succeeded, " Index: ", appIndexForAppID);
			Text statusText2 = StatusText;
			statusText2.text = statusText2.text + Environment.NewLine + Environment.NewLine;
		}
	}

	private void productInfoCallback(InAppPurchaseInfo[] priceInfos, bool succeeded)
	{
		if (succeeded)
		{
			StatusText.text = "";
			foreach (InAppPurchaseInfo inAppPurchaseInfo in priceInfos)
			{
				if (inAppPurchaseInfo.ID == item1)
				{
					StatusText.text += string.Format("ID: {0} Price: {1}", new object[2] { inAppPurchaseInfo.ID, inAppPurchaseInfo.FormattedPrice });
				}
			}
		}
		else
		{
			StatusText.text += "Get Price Info Failed!";
		}
	}

	private void buyAppCallback(string inAppID, string receipt, bool succeeded)
	{
		int appIndexForAppID = InAppPurchaseManager.MainInAppAPI.GetAppIndexForAppID(inAppID);
		if (appIndexForAppID != -1)
		{
			Text statusText = StatusText;
			object text = statusText.text;
			statusText.text = string.Concat(text, "Buy Status: ", inAppID, ": ", succeeded, " Index: ", appIndexForAppID);
			if (!string.IsNullOrEmpty(receipt))
			{
				Text statusText2 = StatusText;
				statusText2.text = statusText2.text + Environment.NewLine + Environment.NewLine;
				StatusText.text += receipt;
			}
		}
		else
		{
			Text statusText3 = StatusText;
			statusText3.text = statusText3.text + "Failed: " + inAppID + Environment.NewLine;
		}
	}

	private void restoreAppsCallback(string inAppID, bool succeeded)
	{
		int appIndexForAppID = InAppPurchaseManager.MainInAppAPI.GetAppIndexForAppID(inAppID);
		if (appIndexForAppID != -1)
		{
			Text statusText = StatusText;
			object text = statusText.text;
			statusText.text = string.Concat(text, "Restore Status: ", inAppID, ": ", succeeded, " Index: ", appIndexForAppID);
			Text statusText2 = StatusText;
			statusText2.text = statusText2.text + Environment.NewLine + Environment.NewLine;
		}
		else
		{
			Text statusText3 = StatusText;
			statusText3.text = statusText3.text + "Failed: " + inAppID + Environment.NewLine;
		}
	}

	private void Update()
	{
		if (Input.GetKeyUp(KeyCode.Escape))
		{
			Application.Quit();
		}
	}

	public IAPDemo()
	{
		item1 = "com.reignstudios.test_app1";
		item2 = "com.reignstudios.test_app2";
		item3 = "com.reignstudios.test_app3";

	}




}
