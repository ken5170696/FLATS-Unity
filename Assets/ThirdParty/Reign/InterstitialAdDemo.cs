using System;
using System.Runtime.InteropServices;
using Reign;
using UnityEngine;
using UnityEngine.UI;
public class InterstitialAdDemo : MonoBehaviour
{
	private static InterstitialAdDemo singleton;

	private static bool created;

	private static bool adIsCahced;

	private static InterstitialAd ad;

	public Button CacheAdButton;

	public Button ShowAdButton;

	public Button BackButton;

	public Text MessageText;

	private void Start()
	{
		singleton = this;
		CacheAdButton.Select();
		CacheAdButton.onClick.AddListener(cacheAdClicked);
		ShowAdButton.onClick.AddListener(showAdClicked);
		BackButton.onClick.AddListener(backClicked);
		if (!created)
		{
			created = true;
			InterstitialAdDesc interstitialAdDesc = new InterstitialAdDesc();
			interstitialAdDesc.Testing = true;
			interstitialAdDesc.EventCallback = eventCallback;
			interstitialAdDesc.UseClassicGUI = false;
			interstitialAdDesc.GUIOverrideEnabled = false;
			interstitialAdDesc.UnityUI_SortIndex = 1001;
			interstitialAdDesc.WinRT_AdAPI = InterstitialAdAPIs.AdDuplex;
			interstitialAdDesc.WP8_AdAPI = InterstitialAdAPIs.AdMob;
			interstitialAdDesc.WP8_AdMob_UnitID = "";
			interstitialAdDesc.WP8_AdDuplex_ApplicationKey = "";
			interstitialAdDesc.WP8_AdDuplex_UnitID = "";
			interstitialAdDesc.iOS_AdAPI = InterstitialAdAPIs.AdMob;
			interstitialAdDesc.iOS_AdMob_UnitID = "";
			interstitialAdDesc.Android_AdAPI = InterstitialAdAPIs.AdMob;
			interstitialAdDesc.Android_AdMob_UnitID = "";
			interstitialAdDesc.Android_Amazon_ApplicationKey = "";
			ad = InterstitialAdManager.CreateAd(interstitialAdDesc, createdCallback);
		}
	}

	private void cacheAdClicked()
	{
		if (adIsCahced)
		{
			MessageText.text = "Ad already cached!";
			return;
		}
		MessageText.text = "Caching Ad...";
		ad.Cache();
	}

	private void showAdClicked()
	{
		if (!adIsCahced)
		{
			MessageText.text = "Ad must be cached first!";
			return;
		}
		adIsCahced = false;
		ad.Show();
	}

	private void backClicked()
	{
		Application.LoadLevel("MainDemo");
	}

	private void createdCallback(bool success)
	{
		Debug.Log(success);
		if (success)
		{
			MessageText.text = "Ad created successfully!";
			return;
		}
		Debug.LogError("Failed to create InterstitialAd!");
		MessageText.text = "Failed to create InterstitialAd!";
	}

	private static void eventCallback(InterstitialAdEvents adEvent, string eventMessage)
	{
		Debug.Log(adEvent);
		switch (adEvent)
		{
		case InterstitialAdEvents.Error:
			Debug.LogError(eventMessage);
			singleton.MessageText.text = eventMessage;
			break;
		case InterstitialAdEvents.Canceled:
			singleton.MessageText.text = "Ad Canceled!";
			break;
		case InterstitialAdEvents.Clicked:
			singleton.MessageText.text = "Ad Clicked!";
			break;
		case InterstitialAdEvents.Cached:
			adIsCahced = true;
			singleton.MessageText.text = "Ad Cached!";
			break;
		case InterstitialAdEvents.Shown:
			singleton.MessageText.text = "Ad Shown!";
			break;
		}
	}

	private void Update()
	{
		if (Input.GetKeyUp(KeyCode.Escape))
		{
			Application.Quit();
		}
	}

	public InterstitialAdDemo()
	{
	}




}
