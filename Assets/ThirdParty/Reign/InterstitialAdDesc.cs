using System;

namespace Reign
{
	public class InterstitialAdDesc
	{
		public bool Testing;

		public InterstitialAdEventCallbackMethod EventCallback;

		public bool UseClassicGUI;

		public bool GUIOverrideEnabled;

		public int UnityUI_SortIndex;

		public InterstitialAdAPIs WinRT_AdAPI;

		public InterstitialAdAPIs WP8_AdAPI;

		public string WP8_AdMob_UnitID;

		public string WP8_AdDuplex_ApplicationKey;

		public string WP8_AdDuplex_UnitID;

		public InterstitialAdAPIs iOS_AdAPI;

		public string iOS_AdMob_UnitID;

		public string iOS_DFP_UnitID;

		public InterstitialAdAPIs Android_AdAPI;

		public string Android_AdMob_UnitID;

		public string Android_DFP_UnitID;

		public string Android_Amazon_ApplicationKey;

		public InterstitialAdDesc()
		{
			UnityUI_SortIndex = 1001;

		}


	}
}
