using System;
using System.Runtime.InteropServices;
using UnityEngine;
namespace Reign.Plugin
{
	public class Dumy_ScorePluginPlugin : IScorePlugin
	{
		public bool IsAuthenticated { get; private set; }

		public string Username { get; private set; }

		public Dumy_ScorePluginPlugin(ScoreDesc desc, CreatedScoreAPICallbackMethod callback)
		{
			IsAuthenticated = false;
			Username = "???";
			if (callback != null)
			{
				callback(false, "Dumy Score object");
			}
		}

		public void Authenticate(AuthenticateCallbackMethod callback, MonoBehaviour services)
		{
			if (callback != null)
			{
				callback(false, "Dumy Score Obj");
			}
		}

		public void Logout()
		{
		}

		public void ManualCreateUser(string userID, string password, AuthenticateCallbackMethod callback, MonoBehaviour services)
		{
			if (callback != null)
			{
				callback(false, "Dumy Score Obj");
			}
		}

		public void ManualLogin(string userID, string password, AuthenticateCallbackMethod callback, MonoBehaviour services)
		{
			if (callback != null)
			{
				callback(false, "Dumy Score Obj");
			}
		}

		public void ReportAchievement(string achievementID, float percentComplete, ReportAchievementCallbackMethod callback, MonoBehaviour services)
		{
			if (callback != null)
			{
				callback(false, "Dumy Score Obj");
			}
		}

		public void ReportScore(string leaderboardID, long score, ReportScoreCallbackMethod callback, MonoBehaviour services)
		{
			if (callback != null)
			{
				callback(false, "Dumy Score Obj");
			}
		}

		public void RequestAchievements(RequestAchievementsCallbackMethod callback, MonoBehaviour services)
		{
			if (callback != null)
			{
				callback(null, false, "Dumy Score Obj");
			}
		}

		public void RequestScores(string leaderboardID, int offset, int range, RequestScoresCallbackMethod callback, MonoBehaviour services)
		{
			if (callback != null)
			{
				callback(null, false, "Dumy Score Obj");
			}
		}

		public void ShowNativeAchievementsPage(ShowNativeViewDoneCallbackMethod callback, MonoBehaviour services)
		{
			if (callback != null)
			{
				callback(false, "Dumy Score Obj");
			}
		}

		public void ShowNativeScoresPage(string leaderboardID, ShowNativeViewDoneCallbackMethod callback, MonoBehaviour services)
		{
			if (callback != null)
			{
				callback(false, "Dumy Score Obj");
			}
		}

		public void ResetUserAchievementsProgress(ResetUserAchievementsCallbackMethod callback, MonoBehaviour services)
		{
			if (callback != null)
			{
				callback(false, "Dumy Score Obj");
			}
		}

		public void Update()
		{
		}




	}
}
