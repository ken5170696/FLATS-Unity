using System;
namespace Reign.Plugin
{
	internal static class ScorePluginAPI
	{
		public static IScorePlugin New(ScoreDesc desc, CreatedScoreAPICallbackMethod callback)
		{
			if (desc.WinRT_ScoreAPI == ScoreAPIs.None)
			{
				return new Dumy_ScorePluginPlugin(desc, callback);
			}
			if (desc.WinRT_ScoreAPI == ScoreAPIs.ReignScores)
			{
				return new ReignScores_ScorePlugin(desc, callback);
			}
			throw new Exception("Unsuported WinRT_ScoreAPI: " + desc.WinRT_ScoreAPI);
		}


	}
}
