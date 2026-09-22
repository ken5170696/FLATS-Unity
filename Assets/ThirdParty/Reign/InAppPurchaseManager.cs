namespace Reign
{
	public static class InAppPurchaseManager
	{
		public static InAppAPI MainInAppAPI
		{
			get
			{
				return InAppAPIs[0];
			}
		}

		public static InAppAPI[] InAppAPIs { get; private set; }

		static InAppPurchaseManager()
		{
			ReignServices.CheckStatus();
			ReignServices.AddService(update, null, null);
		}

		private static void update()
		{
			if (InAppAPIs != null)
			{
				InAppAPI[] inAppAPIs = InAppAPIs;
				foreach (InAppAPI inAppAPI in inAppAPIs)
				{
					inAppAPI.update();
				}
			}
		}

		public static InAppAPI Init(bool testTrialMode, InAppPurchaseCreatedCallbackMethod createdCallback)
		{
			InAppPurchaseDesc inAppPurchaseDesc = new InAppPurchaseDesc();
			inAppPurchaseDesc.Testing = testTrialMode;
			inAppPurchaseDesc.TestTrialMode = testTrialMode;
			InAppPurchaseDesc desc = inAppPurchaseDesc;
			return Init(desc, createdCallback);
		}

		public static InAppAPI Init(InAppPurchaseDesc desc, InAppPurchaseCreatedCallbackMethod createdCallback)
		{
			InAppAPIs = new InAppAPI[1];
			InAppAPIs[0] = new InAppAPI();
			InAppAPIs[0].init(desc, createdCallback);
			return InAppAPIs[0];
		}

		public static InAppAPI[] Init(InAppPurchaseDesc[] descs, InAppPurchaseCreatedCallbackMethod createdCallback)
		{
			InAppAPI[] array = new InAppAPI[descs.Length];
			for (int i = 0; i != descs.Length; i++)
			{
				array[i] = new InAppAPI();
				array[i].init(descs[i], createdCallback);
			}
			return array;
		}


	}
}
