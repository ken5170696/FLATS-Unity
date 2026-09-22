using System;
using System.Collections;
using System.Runtime.InteropServices;
using Reign;
using UnityEngine;
using UnityEngine.UI;
public class InAppPurchase : MonoBehaviour
{
	private const string item1 = "adremover";

	private const string item2 = "beer";

	public Text purchaseButton;

	private static bool created;

    public void Start()
    {
        // Missing platform verification is not a purchase entitlement.
        if (purchaseButton != null) purchaseButton.text = "Store purchases unavailable";
    }

    public void Buy(string command)
    {
        GetComponent<Menu>().ShowConfirm("Store unavailable", "This build cannot verify or restore Store purchases. No purchase has been made and no entitlement has been granted.", null, "OK", null);
    }

	private void buyAppCallback(string inAppID, string receipt, bool succeeded)
	{
		int appIndexForAppID = InAppPurchaseManager.MainInAppAPI.GetAppIndexForAppID(inAppID);
		if (appIndexForAppID != -1)
		{
			Debug.Log("Buy Status: " + inAppID + ": " + succeeded + " Index: " + appIndexForAppID);
			if (inAppID == "adremover" && succeeded)
			{
				GetComponent<Menu>().purchaseButton.text = "Goodbye Ads!";
				Menu.adFree = true;
			}
			if (inAppID == "beer" && succeeded)
			{
				GetComponent<Menu>().purchaseButton.text = "Thank you!";
			}
			if (!string.IsNullOrEmpty(receipt))
			{
				Debug.Log(Environment.NewLine + Environment.NewLine);
				Debug.Log(receipt);
			}
		}
		else
		{
			Debug.Log("Failed: " + inAppID + Environment.NewLine);
		}
	}

	private void restoreAppsCallback(string inAppID, bool succeeded)
	{
		int appIndexForAppID = InAppPurchaseManager.MainInAppAPI.GetAppIndexForAppID(inAppID);
		if (appIndexForAppID != -1)
		{
			Debug.Log("Restore Status: " + inAppID + ": " + succeeded + " Index: " + appIndexForAppID);
			Debug.Log(Environment.NewLine + Environment.NewLine);
			if (!(inAppID == "adremover"))
			{
				return;
			}
			if (succeeded)
			{
				Debug.Log("You already bought adremover, Ads will be removed.");
				GetComponent<Menu>().purchaseButton.text = "Beer for Developer";
				Menu.adFree = true;
				return;
			}
			Debug.Log("No adremover, ready to show ads.");
			if (!Menu.created)
			{
				GetComponent<Menu>().ReadyForAd();
			}
		}
		else
		{
			Debug.Log("Failed: " + inAppID + Environment.NewLine);
		}
	}

	private void createdCallback(bool succeeded)
	{
		Debug.Log("Init: " + succeeded + Environment.NewLine + Environment.NewLine);
		InAppPurchaseManager.MainInAppAPI.AwardInterruptedPurchases(awardInterruptedPurchases);
		StartCoroutine("AutoRestore");
	}

	private IEnumerator AutoRestore()
	{
		yield return new WaitForSeconds(2f);
		InAppPurchaseManager.MainInAppAPI.Restore(restoreAppsCallback);
	}

	private void awardInterruptedPurchases(string inAppID, bool succeeded)
	{
		int appIndexForAppID = InAppPurchaseManager.MainInAppAPI.GetAppIndexForAppID(inAppID);
		if (appIndexForAppID != -1)
		{
			Debug.Log("Interrupted Restore Status: " + inAppID + ": " + succeeded + " Index: " + appIndexForAppID);
			Debug.Log(Environment.NewLine + Environment.NewLine);
		}
	}

	public InAppPurchase()
	{
	}




}
