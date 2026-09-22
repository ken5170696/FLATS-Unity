using System;
namespace Reign.Plugin
{
	internal static class InterstitialAdPluginAPI
	{
		public static IInterstitialAdPlugin New(InterstitialAdDesc desc, InterstitialAdCreatedCallbackMethod callback)
		{
			if (desc.WinRT_AdAPI == InterstitialAdAPIs.AdDuplex)
			{
				return new AdDuplex_InterstitialAdPlugin_WinRT(desc, callback);
			}
			throw new Exception("Unsuported WinRT_AdAPI: " + desc.WinRT_AdAPI);
		}


	}
}
