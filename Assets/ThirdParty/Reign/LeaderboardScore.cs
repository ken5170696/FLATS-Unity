using System;
using System.Runtime.InteropServices;
namespace Reign
{
	public class LeaderboardScore
	{
		public string Username { get; private set; }

		public long Score { get; private set; }

		public LeaderboardScore(string username, long score)
		{
			Username = username;
			Score = score;
		}




	}
}
