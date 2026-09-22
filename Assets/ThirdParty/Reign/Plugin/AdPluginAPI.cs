using System;
namespace Reign.Plugin
{
	internal static class AdPluginAPI
	{
		public static IAdPlugin New(AdDesc desc, AdCreatedCallbackMethod callback)
		{
			if (desc.WinRT_AdAPI == AdAPIs.None)
			{
				return new Dumy_AdPlugin(desc, callback);
			}
			if (desc.WinRT_AdAPI == AdAPIs.MicrosoftAdvertising)
			{
				return new MicrosoftAdvertising_AdPlugin_WinRT(desc, callback);
			}
			if (desc.WinRT_AdAPI == AdAPIs.MillennialMediaAdvertising)
			{
				return new MM_AdPlugin(desc, callback, ReignServices.Singleton);
			}
			if (desc.WinRT_AdAPI == AdAPIs.AdDuplex)
			{
				return new AdDuplex_AdPlugin_WinRT(desc, callback);
			}
			throw new Exception("Unsuported WinRT_AdAPI: " + desc.WinRT_AdAPI);
		}


	}
}
