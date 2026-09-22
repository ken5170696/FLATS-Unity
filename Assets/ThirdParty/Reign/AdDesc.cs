using System;

namespace Reign
{
	public class AdDesc
	{
		public bool Visible;

		public bool Testing;

		public AdEventCallbackMethod EventCallback;

		public int UnityUI_SortIndex;

		public bool UseClassicGUI;

		public bool GUIOverrideEnabled;

		public AdAPIs Editor_AdAPI;

		public string Editor_MillennialMediaAdvertising_APID;

		public AdGravity Editor_AdGravity;

		public AdGravity Editor_MillennialMediaAdvertising_AdGravity;

		public int Editor_MillennialMediaAdvertising_RefreshRate;

		public float Editor_AdScale;

		public int Editor_FixedWidthOverride;

		public int Editor_FixedHeightOverride;

		public AdAPIs WinRT_AdAPI;

		public string WinRT_MicrosoftAdvertising_ApplicationID;

		public string WinRT_MicrosoftAdvertising_UnitID;

		public WinRT_MicrosoftAdvertising_AdSize WinRT_MicrosoftAdvertising_AdSize;

		public bool WinRT_MicrosoftAdvertising_UseBuiltInRefresh;

		public int WinRT_MicrosoftAdvertising_RefreshRate;

		public AdGravity WinRT_MicrosoftAdvertising_AdGravity;

		public AdGravity WinRT_MillennialMediaAdvertising_AdGravity;

		public AdGravity WinRT_AdDuplex_AdGravity;

		public string WinRT_AdDuplex_ApplicationKey;

		public string WinRT_AdDuplex_UnitID;

		public int WinRT_AdDuplex_RefreshRate;

		public WinRT_AdDuplex_AdSize WinRT_AdDuplex_AdSize;

		public string WinRT_MillennialMediaAdvertising_APID;

		public int WinRT_MillennialMediaAdvertising_RefreshRate;

		public float WinRT_AdScale;

		public AdAPIs WP8_AdAPI;

		public string WP8_MicrosoftAdvertising_ApplicationID;

		public string WP8_MicrosoftAdvertising_UnitID;

		public bool WP8_MicrosoftAdvertising_UseBuiltInRefresh;

		public int WP8_MicrosoftAdvertising_RefreshRate;

		public string WP8_AdDuplex_ApplicationKey;

		public string WP8_AdDuplex_UnitID;

		public int WP8_AdDuplex_RefreshRate;

		public string WP8_AdMob_UnitID;

		public WP8_MicrosoftAdvertising_AdSize WP8_MicrosoftAdvertising_AdSize;

		public WP8_AdMob_AdSize WP8_AdMob_AdSize;

		public AdGravity WP8_MicrosoftAdvertising_AdGravity;

		public AdGravity WP8_AdMob_AdGravity;

		public AdGravity WP8_MillennialMediaAdvertising_AdGravity;

		public AdGravity WP8_AdDuplex_AdGravity;

		public string WP8_MillennialMediaAdvertising_APID;

		public int WP8_MillennialMediaAdvertising_RefreshRate;

		public float WP8_AdScale;

		public AdAPIs BB10_AdAPI;

		public string BB10_BlackBerryAdvertising_ZoneID;

		public string BB10_MillennialMediaAdvertising_APID;

		public BB10_BlackBerryAdvertising_AdSize BB10_BlackBerryAdvertising_AdSize;

		public AdGravity BB10_BlackBerryAdvertising_AdGravity;

		public AdGravity BB10_MillennialMediaAdvertising_AdGravity;

		public int BB10_BlackBerryAdvertising_RefreshRate;

		public int BB10_MillennialMediaAdvertising_RefreshRate;

		public float BB10_AdScale;

		public AdAPIs iOS_AdAPI;

		public string iOS_AdMob_UnitID;

		public string iOS_DFP_UnitID;

		public iOS_AdMob_AdSize iOS_AdMob_AdSize;

		public iOS_DFP_AdSize iOS_DFP_AdSize;

		public AdGravity iOS_iAd_AdGravity;

		public AdGravity iOS_AdMob_AdGravity;

		public AdGravity iOS_DFP_AdGravity;

		public AdGravity iOS_MillennialMediaAdvertising_AdGravity;

		public string iOS_MillennialMediaAdvertising_APID;

		public int iOS_MillennialMediaAdvertising_RefreshRate;

		public float iOS_AdScale;

		public AdAPIs Android_AdAPI;

		public string Android_AdMob_UnitID;

		public string Android_DFP_UnitID;

		public string Android_AmazonAds_ApplicationKey;

		public Android_AdMob_AdSize Android_AdMob_AdSize;

		public Android_DFP_AdSize Android_DFP_AdSize;

		public Android_AmazonAds_AdSize Android_AmazonAds_AdSize;

		public int Android_AmazonAds_RefreshRate;

		public AdGravity Android_AdMob_AdGravity;

		public AdGravity Android_DFP_AdGravity;

		public AdGravity Android_AmazonAds_AdGravity;

		public AdGravity Android_MillennialMediaAdvertising_AdGravity;

		public string Android_MillennialMediaAdvertising_APID;

		public int Android_MillennialMediaAdvertising_RefreshRate;

		public float Android_AdScale;

		public AdAPIs Win32_AdAPI;

		public string Win32_MillennialMediaAdvertising_APID;

		public int Win32_MillennialMediaAdvertising_RefreshRate;

		public float Win32_AdScale;

		public AdAPIs OSX_AdAPI;

		public string OSX_MillennialMediaAdvertising_APID;

		public int OSX_MillennialMediaAdvertising_RefreshRate;

		public float OSX_AdScale;

		public AdAPIs Linux_AdAPI;

		public string Linux_MillennialMediaAdvertising_APID;

		public int Linux_MillennialMediaAdvertising_RefreshRate;

		public float Linux_AdScale;

		public AdDesc()
		{
			Visible = true;
			UnityUI_SortIndex = 1000;
			Editor_AdGravity = AdGravity.TopCenter;
			Editor_MillennialMediaAdvertising_AdGravity = AdGravity.TopCenter;
			Editor_MillennialMediaAdvertising_RefreshRate = 120;
			Editor_AdScale = 1f;
			WinRT_MicrosoftAdvertising_AdSize = WinRT_MicrosoftAdvertising_AdSize.Square_250x250;
			WinRT_MicrosoftAdvertising_UseBuiltInRefresh = true;
			WinRT_MicrosoftAdvertising_RefreshRate = 120;
			WinRT_MicrosoftAdvertising_AdGravity = AdGravity.TopCenter;
			WinRT_MillennialMediaAdvertising_AdGravity = AdGravity.TopCenter;
			WinRT_AdDuplex_AdGravity = AdGravity.TopCenter;
			WinRT_AdDuplex_RefreshRate = 120;
			WinRT_AdDuplex_AdSize = WinRT_AdDuplex_AdSize.Wide_728x90;
			WinRT_MillennialMediaAdvertising_RefreshRate = 120;
			WinRT_AdScale = 1f;
			WP8_MicrosoftAdvertising_UseBuiltInRefresh = true;
			WP8_MicrosoftAdvertising_RefreshRate = 120;
			WP8_AdDuplex_RefreshRate = 120;
			WP8_MicrosoftAdvertising_AdSize = WP8_MicrosoftAdvertising_AdSize.Wide_480x80;
			WP8_MicrosoftAdvertising_AdGravity = AdGravity.TopCenter;
			WP8_AdMob_AdGravity = AdGravity.TopCenter;
			WP8_MillennialMediaAdvertising_AdGravity = AdGravity.TopCenter;
			WP8_AdDuplex_AdGravity = AdGravity.TopCenter;
			WP8_MillennialMediaAdvertising_RefreshRate = 120;
			WP8_AdScale = 1f;
			BB10_BlackBerryAdvertising_AdGravity = AdGravity.TopCenter;
			BB10_MillennialMediaAdvertising_AdGravity = AdGravity.TopCenter;
			BB10_BlackBerryAdvertising_RefreshRate = 120;
			BB10_MillennialMediaAdvertising_RefreshRate = 120;
			BB10_AdScale = 1f;
			iOS_iAd_AdGravity = AdGravity.TopCenter;
			iOS_AdMob_AdGravity = AdGravity.TopCenter;
			iOS_DFP_AdGravity = AdGravity.TopCenter;
			iOS_MillennialMediaAdvertising_AdGravity = AdGravity.TopCenter;
			iOS_MillennialMediaAdvertising_RefreshRate = 120;
			iOS_AdScale = 1f;
			Android_AmazonAds_AdSize = Android_AmazonAds_AdSize.Wide_320x50;
			Android_AmazonAds_RefreshRate = 120;
			Android_AdMob_AdGravity = AdGravity.TopCenter;
			Android_DFP_AdGravity = AdGravity.TopCenter;
			Android_AmazonAds_AdGravity = AdGravity.TopCenter;
			Android_MillennialMediaAdvertising_AdGravity = AdGravity.TopCenter;
			Android_MillennialMediaAdvertising_RefreshRate = 120;
			Android_AdScale = 1f;
			Win32_MillennialMediaAdvertising_RefreshRate = 120;
			Win32_AdScale = 1f;
			OSX_MillennialMediaAdvertising_RefreshRate = 120;
			OSX_AdScale = 1f;
			Linux_MillennialMediaAdvertising_RefreshRate = 120;
			Linux_AdScale = 1f;

		}


	}
}
