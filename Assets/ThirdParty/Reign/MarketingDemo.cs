using System;
using Reign;
using UnityEngine;
using UnityEngine.UI;
public class MarketingDemo : MonoBehaviour
{
	public Button ReviewButton;

	public Button BackButton;

	private void Start()
	{
		ReviewButton.Select();
		ReviewButton.onClick.AddListener(reviewClicked);
		BackButton.onClick.AddListener(backClicked);
	}

	private void reviewClicked()
	{
		MarketingDesc marketingDesc = new MarketingDesc();
		marketingDesc.Editor_URL = "http://reign-studios.net/";
		marketingDesc.Win8_PackageFamilyName = "";
		marketingDesc.WP8_AppID = "";
		marketingDesc.iOS_AppID = "";
		marketingDesc.BB10_AppID = "";
		marketingDesc.Android_MarketingStore = MarketingStores.GooglePlay;
		marketingDesc.Android_GooglePlay_BundleID = "";
		marketingDesc.Android_Amazon_BundleID = "";
		marketingDesc.Android_Samsung_BundleID = "";
		MarketingManager.OpenStoreForReview(marketingDesc);
	}

	private void backClicked()
	{
		Application.LoadLevel("MainDemo");
	}

	private void Update()
	{
		if (Input.GetKeyUp(KeyCode.Escape))
		{
			Application.Quit();
		}
	}

	public MarketingDemo()
	{
	}




}
