using System;

namespace Reign
{
	public class LeaderboardDesc
	{
		public string ID;

		public string Name;

		public string Desc;

		public LeaderbaordScoreFormats ScoreFormat;

		public int ScoreFormat_DecimalPlaces;

		public LeaderboardScoreTimeFormats ScoreTimeFormat;

		public LeaderboardSortOrders SortOrder;

		public Guid Editor_ReignScores_ID;

		public Guid Win32_ReignScores_ID;

		public Guid OSX_ReignScores_ID;

		public Guid Linux_ReignScores_ID;

		public Guid Web_ReignScores_ID;

		public Guid WebGL_ReignScores_ID;

		public Guid WinRT_ReignScores_ID;

		public Guid WP8_ReignScores_ID;

		public Guid BB10_ReignScores_ID;

		public Guid Tizen_ReignScores_ID;

		public Guid iOS_ReignScores_ID;

		public Guid Android_ReignScores_ID;

		public string Android_GooglePlay_ID;

		public string Android_GameCircle_ID;

		public string iOS_GameCenter_ID;

		public LeaderboardDesc()
		{
			ScoreTimeFormat = LeaderboardScoreTimeFormats.Milliseconds;

		}


	}
}
