using System;

namespace Reign
{
	public class ScoreDesc
	{
		public ScoreAPIs Editor_ScoreAPI;

		public ScoreAPIs Win32_ScoreAPI;

		public ScoreAPIs OSX_ScoreAPI;

		public ScoreAPIs Linux_ScoreAPI;

		public ScoreAPIs Web_ScoreAPI;

		public ScoreAPIs WebGL_ScoreAPI;

		public ScoreAPIs WinRT_ScoreAPI;

		public ScoreAPIs WP8_ScoreAPI;

		public ScoreAPIs BB10_ScoreAPI;

		public ScoreAPIs Tizen_ScoreAPI;

		public ScoreAPIs iOS_ScoreAPI;

		public ScoreAPIs Android_ScoreAPI;

		public IScores_UI ReignScores_UI;

		public string ReignScores_ServicesURL;

		public string ReignScores_GameKey;

		public string ReignScores_UserKey;

		public LeaderboardDesc[] LeaderboardDescs;

		public AchievementDesc[] AchievementDescs;

		public string Editor_ReignScores_GameID;

		public string Win32_ReignScores_GameID;

		public string Linux_ReignScores_GameID;

		public string OSX_ReignScores_GameID;

		public string Web_ReignScores_GameID;

		public string WebGL_ReignScores_GameID;

		public string WinRT_ReignScores_GameID;

		public string WP8_ReignScores_GameID;

		public string BB10_ReignScores_GameID;

		public string Tizen_ReignScores_GameID;

		public string iOS_ReignScores_GameID;

		public string Android_ReignScores_GameID;

		public bool Android_GooglePlay_DisableUsernameRetrieval;

		public ScoreDesc()
		{
		}


	}
}
