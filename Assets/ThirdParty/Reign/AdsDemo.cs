using System;
using System.Runtime.InteropServices;
using Reign;
using UnityEngine;
using UnityEngine.UI;
public class AdsDemo : MonoBehaviour
{
	private static AdsDemo singleton;

	private static bool created;

	private static Ad ad;

	public Text AdStatusText;

	public Button RefreshButton;

	public Button VisibilityButton;

	public Button BackButton;

	private void Start()
	{
		singleton = this;
		RefreshButton.Select();
		RefreshButton.onClick.AddListener(refreshClicked);
		VisibilityButton.onClick.AddListener(visibilityClicked);
		BackButton.onClick.AddListener(backClicked);
		if (created)
		{
			if (ad != null)
			{
				ad.Visible = true;
			}
			return;
		}
		created = true;
		AdDesc adDesc = new AdDesc();
		adDesc.Testing = true;
		adDesc.Visible = true;
		adDesc.EventCallback = eventCallback;
		adDesc.UseClassicGUI = false;
		adDesc.GUIOverrideEnabled = false;
		adDesc.UnityUI_SortIndex = 1000;
		adDesc.Editor_AdAPI = AdAPIs.EditorTestAd;
		adDesc.Editor_AdGravity = AdGravity.BottomCenter;
		adDesc.Editor_AdScale = 1.5f;
		adDesc.Editor_MillennialMediaAdvertising_APID = "";
		adDesc.Editor_MillennialMediaAdvertising_AdGravity = AdGravity.BottomCenter;
		adDesc.WinRT_AdAPI = AdAPIs.MicrosoftAdvertising;
		adDesc.WinRT_MicrosoftAdvertising_ApplicationID = "";
		adDesc.WinRT_MicrosoftAdvertising_UnitID = "";
		adDesc.WinRT_MicrosoftAdvertising_AdGravity = AdGravity.BottomCenter;
		adDesc.WinRT_MicrosoftAdvertising_AdSize = WinRT_MicrosoftAdvertising_AdSize.Wide_728x90;
		adDesc.WinRT_AdDuplex_ApplicationKey = "";
		adDesc.WinRT_AdDuplex_UnitID = "";
		adDesc.WinRT_AdDuplex_AdGravity = AdGravity.BottomCenter;
		adDesc.WinRT_AdDuplex_AdSize = WinRT_AdDuplex_AdSize.Wide_728x90;
		adDesc.WP8_AdAPI = AdAPIs.MicrosoftAdvertising;
		adDesc.WP8_MicrosoftAdvertising_ApplicationID = "";
		adDesc.WP8_MicrosoftAdvertising_UnitID = "";
		adDesc.WP8_MicrosoftAdvertising_AdGravity = AdGravity.BottomCenter;
		adDesc.WP8_MicrosoftAdvertising_AdSize = WP8_MicrosoftAdvertising_AdSize.Wide_480x80;
		adDesc.WP8_AdDuplex_ApplicationKey = "";
		adDesc.WP8_AdDuplex_UnitID = "";
		adDesc.WP8_AdDuplex_AdGravity = AdGravity.BottomCenter;
		adDesc.WP8_AdMob_UnitID = "";
		adDesc.WP8_AdMob_AdGravity = AdGravity.BottomCenter;
		adDesc.WP8_AdMob_AdSize = WP8_AdMob_AdSize.Banner;
		adDesc.BB10_AdAPI = AdAPIs.MillennialMediaAdvertising;
		adDesc.BB10_BlackBerryAdvertising_ZoneID = "";
		adDesc.BB10_BlackBerryAdvertising_AdGravity = AdGravity.BottomCenter;
		adDesc.BB10_BlackBerryAdvertising_AdSize = BB10_BlackBerryAdvertising_AdSize.Wide_320x53;
		adDesc.BB10_MillennialMediaAdvertising_APID = "";
		adDesc.BB10_MillennialMediaAdvertising_AdGravity = AdGravity.BottomCenter;
		adDesc.BB10_AdScale = 1.5f;
		adDesc.iOS_AdAPI = AdAPIs.iAd;
		adDesc.iOS_iAd_AdGravity = AdGravity.BottomCenter;
		adDesc.iOS_AdMob_AdGravity = AdGravity.BottomCenter;
		adDesc.iOS_AdMob_UnitID = "";
		adDesc.iOS_AdMob_AdSize = iOS_AdMob_AdSize.Banner_320x50;
		adDesc.Android_AdAPI = AdAPIs.AdMob;
		adDesc.Android_AdMob_UnitID = "";
		adDesc.Android_AdMob_AdGravity = AdGravity.BottomCenter;
		adDesc.Android_AdMob_AdSize = Android_AdMob_AdSize.Banner_320x50;
		adDesc.Android_AmazonAds_ApplicationKey = "";
		adDesc.Android_AmazonAds_AdSize = Android_AmazonAds_AdSize.Wide_320x50;
		adDesc.Android_AmazonAds_AdGravity = AdGravity.BottomCenter;
		ad = AdManager.CreateAd(adDesc, adCreatedCallback);
	}

	private void refreshClicked()
	{
		ad.Refresh();
	}

	private void visibilityClicked()
	{
		ad.Visible = !ad.Visible;
	}

	private void backClicked()
	{
		ad.Visible = false;
		Application.LoadLevel("MainDemo");
	}

	private void adCreatedCallback(bool succeeded)
	{
		AdStatusText.text = (succeeded ? "Ads Succeded" : "Ads Failed");
	}

	private static void eventCallback(AdEvents adEvent, string eventMessage)
	{
		if (!(singleton.AdStatusText == null))
		{
			switch (adEvent)
			{
			case AdEvents.Refreshed:
				singleton.AdStatusText.text = "Refreshed";
				break;
			case AdEvents.Clicked:
				singleton.AdStatusText.text = "Clicked";
				break;
			case AdEvents.Error:
				singleton.AdStatusText.text = "Error: " + eventMessage;
				break;
			}
		}
	}

	private void Update()
	{
		if (Input.GetKeyUp(KeyCode.Escape))
		{
			Application.Quit();
		}
	}

	public AdsDemo()
	{
	}




}
