using System;
using Reign.Plugin;
namespace Reign
{
	public class InterstitialAd
	{
		internal IInterstitialAdPlugin plugin;

		public InterstitialAd(IInterstitialAdPlugin plugin)
		{
			this.plugin = plugin;
		}

		public void Cache()
		{
			plugin.Cache();
		}

		public void Show()
		{
			plugin.Show();
		}

		public void Draw()
		{
			plugin.OverrideOnGUI();
		}




	}
}
