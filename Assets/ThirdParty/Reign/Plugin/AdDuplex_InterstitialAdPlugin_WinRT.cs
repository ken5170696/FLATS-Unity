using System;
namespace Reign.Plugin
{
	public class AdDuplex_InterstitialAdPlugin_WinRT : IInterstitialAdPlugin
	{
		public delegate void InitNativeMethod(AdDuplex_InterstitialAdPlugin_WinRT plugin, InterstitialAdDesc desc, InterstitialAdCreatedCallbackMethod createdCallback);

		public IInterstitialAdPlugin Native;

		public static InitNativeMethod InitNative;

		public AdDuplex_InterstitialAdPlugin_WinRT(InterstitialAdDesc desc, InterstitialAdCreatedCallbackMethod createdCallback)
		{
			InitNative(this, desc, createdCallback);
		}

		public void Cache()
		{
			Native.Cache();
		}

		public void Show()
		{
			Native.Show();
		}

		public void Dispose()
		{
			Native.Dispose();
		}

		public void Update()
		{
			Native.Update();
		}

		public void OnGUI()
		{
			Native.OnGUI();
		}

		public void OverrideOnGUI()
		{
			Native.OverrideOnGUI();
		}




	}
}
