using System;
namespace Reign.Plugin
{
	public class Dumy_InterstitialAdPlugin : IInterstitialAdPlugin
	{
		private InterstitialAdEventCallbackMethod eventCallback;

		public Dumy_InterstitialAdPlugin(InterstitialAdDesc desc, InterstitialAdCreatedCallbackMethod createdCallback)
		{
			eventCallback = desc.EventCallback;
			if (createdCallback != null)
			{
				createdCallback(true);
			}
		}

		public void Cache()
		{
		}

		public void Show()
		{
			if (eventCallback != null)
			{
				eventCallback(InterstitialAdEvents.Canceled, null);
			}
		}

		public void Dispose()
		{
		}

		public void Update()
		{
		}

		public void OnGUI()
		{
		}

		public void OverrideOnGUI()
		{
		}




	}
}
