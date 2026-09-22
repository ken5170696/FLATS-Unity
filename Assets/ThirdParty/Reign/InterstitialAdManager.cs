using System.Collections;
using System.Collections.Generic;
using Reign.Plugin;
using UnityEngine;
namespace Reign
{
	public static class InterstitialAdManager
	{
		private static List<IInterstitialAdPlugin> plugins;

		private static bool creatingAds;

		private static InterstitialAdCreatedCallbackMethod createdCallback;

		static InterstitialAdManager()
		{
			ReignServices.CheckStatus();
			plugins = new List<IInterstitialAdPlugin>();
			ReignServices.AddService(update, onGui, null);
		}

		private static void update()
		{
			foreach (IInterstitialAdPlugin plugin in plugins)
			{
				plugin.Update();
			}
		}

		private static void onGui()
		{
			foreach (IInterstitialAdPlugin plugin in plugins)
			{
				plugin.OnGUI();
			}
		}

		private static void async_CreatedCallback(bool succeeded)
		{
			creatingAds = false;
			ReignServices.Singleton.StartCoroutine(createdCallbackDelay(succeeded));
		}

		private static IEnumerator createdCallbackDelay(bool succeeded)
		{
			yield return null;
			if (createdCallback != null)
			{
				createdCallback(succeeded);
			}
		}

		public static InterstitialAd CreateAd(InterstitialAdDesc desc, InterstitialAdCreatedCallbackMethod createdCallback)
		{
			if (creatingAds)
			{
				Debug.LogError("You must wait for the last interstitial ad to finish being created!");
				if (createdCallback != null)
				{
					createdCallback(false);
				}
				return null;
			}
			creatingAds = true;
			InterstitialAdManager.createdCallback = createdCallback;
			plugins.Add(InterstitialAdPluginAPI.New(desc, async_CreatedCallback));
			return new InterstitialAd(plugins[plugins.Count - 1]);
		}

		public static InterstitialAd[] CreateAd(InterstitialAdDesc[] descs, InterstitialAdCreatedCallbackMethod createdCallback)
		{
			if (creatingAds)
			{
				Debug.LogError("You must wait for the last interstitial ads to finish being created!");
				if (createdCallback != null)
				{
					createdCallback(false);
				}
				return null;
			}
			creatingAds = true;
			InterstitialAdManager.createdCallback = createdCallback;
			int count = plugins.Count;
			for (int i = 0; i != descs.Length; i++)
			{
				plugins.Add(InterstitialAdPluginAPI.New(descs[i], async_CreatedCallback));
			}
			InterstitialAd[] array = new InterstitialAd[descs.Length];
			int num = 0;
			int num2 = count;
			while (num != descs.Length)
			{
				array[num] = new InterstitialAd(plugins[num2]);
				num++;
				num2++;
			}
			return array;
		}

		public static void DisposeAd(InterstitialAd ad)
		{
			int adIndex = getAdIndex(ad);
			if (adIndex == -1)
			{
				Debug.LogError("DisposeAd Failed: InterstitialAd not found in InterstitialAdManager.");
				return;
			}
			IInterstitialAdPlugin interstitialAdPlugin = plugins[adIndex];
			interstitialAdPlugin.Dispose();
			plugins.Remove(interstitialAdPlugin);
		}

		private static int getAdIndex(InterstitialAd ad)
		{
			for (int i = 0; i != plugins.Count; i++)
			{
				if (ad.plugin == plugins[i])
				{
					return i;
				}
			}
			return -1;
		}


	}
}
