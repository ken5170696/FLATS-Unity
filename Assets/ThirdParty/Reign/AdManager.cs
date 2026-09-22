using System.Collections;
using System.Collections.Generic;
using Reign.Plugin;
using UnityEngine;
namespace Reign
{
	public static class AdManager
	{
		private static List<IAdPlugin> plugins;

		private static bool creatingAds;

		private static AdCreatedCallbackMethod createdCallback;

		static AdManager()
		{
			ReignServices.CheckStatus();
			plugins = new List<IAdPlugin>();
			ReignServices.AddService(update, onGui, null);
		}

		private static void update()
		{
			foreach (IAdPlugin plugin in plugins)
			{
				plugin.Update();
			}
		}

		private static void onGui()
		{
			foreach (IAdPlugin plugin in plugins)
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

		public static Ad CreateAd(AdDesc desc, AdCreatedCallbackMethod createdCallback)
		{
			if (creatingAds)
			{
				Debug.LogError("You must wait for the last ad to finish being created!");
				if (createdCallback != null)
				{
					createdCallback(false);
				}
				return null;
			}
			creatingAds = true;
			AdManager.createdCallback = createdCallback;
			plugins.Add(AdPluginAPI.New(desc, async_CreatedCallback));
			return new Ad(plugins[plugins.Count - 1]);
		}

		public static Ad[] CreateAd(AdDesc[] descs, AdCreatedCallbackMethod createdCallback)
		{
			if (creatingAds)
			{
				Debug.LogError("You must wait for the last ads to finish being created!");
				if (createdCallback != null)
				{
					createdCallback(false);
				}
				return null;
			}
			creatingAds = true;
			AdManager.createdCallback = createdCallback;
			int count = plugins.Count;
			for (int i = 0; i != descs.Length; i++)
			{
				plugins.Add(AdPluginAPI.New(descs[i], async_CreatedCallback));
			}
			Ad[] array = new Ad[descs.Length];
			int num = 0;
			int num2 = count;
			while (num != descs.Length)
			{
				array[num] = new Ad(plugins[num2]);
				num++;
				num2++;
			}
			return array;
		}

		public static void DisposeAd(Ad ad)
		{
			int adIndex = getAdIndex(ad);
			if (adIndex == -1)
			{
				Debug.LogError("DisposeAd Failed: Ad not found in AdManager.");
				return;
			}
			IAdPlugin adPlugin = plugins[adIndex];
			adPlugin.Dispose();
			plugins.Remove(adPlugin);
		}

		private static int getAdIndex(Ad ad)
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
