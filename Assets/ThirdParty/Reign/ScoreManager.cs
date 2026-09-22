using System;
using System.Collections;
using System.Runtime.InteropServices;
using Reign.Plugin;
using UnityEngine;
namespace Reign
{
	public static class ScoreManager
	{
		private static IScorePlugin plugin;

		private static ScoreDesc desc;

		private static bool waitingForOperation;

		private static AuthenticateCallbackMethod authenticateCallback;

		private static ReportScoreCallbackMethod reportScoreCallback;

		private static RequestScoresCallbackMethod requestScoresCallback;

		private static ReportAchievementCallbackMethod reportAchievementCallback;

		private static RequestAchievementsCallbackMethod requestAchievementsCallback;

		private static ShowNativeViewDoneCallbackMethod showNativeViewCallback;

		private static CreatedScoreAPICallbackMethod createdCallback;

		private static ResetUserAchievementsCallbackMethod resetUserAchievementsCallback;

		public static bool IsAuthenticated
		{
			get
			{
				if (plugin == null)
				{
					return false;
				}
				return plugin.IsAuthenticated;
			}
		}

		public static string Username
		{
			get
			{
				if (plugin == null)
				{
					return "???";
				}
				return plugin.Username;
			}
		}

		private static void async_CreatedCallback(bool succeeded, string errorMessage)
		{
			ReignServices.Singleton.StartCoroutine(createdCallbackDelay(succeeded, errorMessage));
		}

		private static IEnumerator createdCallbackDelay(bool succeeded, string errorMessage)
		{
			yield return null;
			if (createdCallback != null)
			{
				createdCallback(succeeded, errorMessage);
			}
		}

		public static void Init(ScoreDesc desc, CreatedScoreAPICallbackMethod callback)
		{
			createdCallback = callback;
			ReignServices.CheckStatus();
			plugin = ScorePluginAPI.New(desc, async_CreatedCallback);
			ScoreManager.desc = desc;
			ReignServices.AddService(update, null, null);
		}

		private static void update()
		{
			plugin.Update();
		}

		private static void async_authenticateCallback(bool succeeded, string errorMessage)
		{
			waitingForOperation = false;
			if (authenticateCallback != null)
			{
				authenticateCallback(succeeded, errorMessage);
			}
		}

		private static void async_reportScoreCallback(bool succeeded, string errorMessage)
		{
			waitingForOperation = false;
			if (reportScoreCallback != null)
			{
				reportScoreCallback(succeeded, errorMessage);
			}
		}

		private static void async_requestScoresCallback(LeaderboardScore[] scores, bool succeeded, string errorMessage)
		{
			waitingForOperation = false;
			if (requestScoresCallback != null)
			{
				requestScoresCallback(scores, succeeded, errorMessage);
			}
		}

		private static void async_reportAchievementCallback(bool succeeded, string errorMessage)
		{
			waitingForOperation = false;
			if (reportAchievementCallback != null)
			{
				reportAchievementCallback(succeeded, errorMessage);
			}
		}

		private static void async_requestAchievementsCallback(Achievement[] achievements, bool succeeded, string errorMessage)
		{
			waitingForOperation = false;
			if (requestAchievementsCallback != null)
			{
				requestAchievementsCallback(achievements, succeeded, errorMessage);
			}
		}

		private static void async_showNativeViewCallback(bool succeeded, string errorMessage)
		{
			waitingForOperation = false;
			if (showNativeViewCallback != null)
			{
				showNativeViewCallback(succeeded, errorMessage);
			}
		}

		private static void async_resetUserAchievementsCallback(bool succeeded, string errorMessage)
		{
			waitingForOperation = false;
			if (resetUserAchievementsCallback != null)
			{
				resetUserAchievementsCallback(succeeded, errorMessage);
			}
		}

		public static void Authenticate(AuthenticateCallbackMethod callback)
		{
			if (waitingForOperation)
			{
				Debug.LogError("Must wait for last Score operation to complete.");
				return;
			}
			waitingForOperation = true;
			authenticateCallback = callback;
			plugin.Authenticate(async_authenticateCallback, ReignServices.Singleton);
		}

		public static void Logout()
		{
			plugin.Logout();
		}

		public static void ManualLogin(string username, string password, AuthenticateCallbackMethod callback)
		{
			if (waitingForOperation)
			{
				Debug.LogError("Must wait for last Score operation to complete.");
				return;
			}
			waitingForOperation = true;
			authenticateCallback = callback;
			plugin.ManualLogin(username, password, async_authenticateCallback, ReignServices.Singleton);
		}

		public static void ManualCreateUser(string username, string password, AuthenticateCallbackMethod callback)
		{
			if (waitingForOperation)
			{
				Debug.LogError("Must wait for last Score operation to complete.");
				return;
			}
			waitingForOperation = true;
			authenticateCallback = callback;
			plugin.ManualCreateUser(username, password, async_authenticateCallback, ReignServices.Singleton);
		}

		public static void ReportScoreRaw(string leaderboardID, long score, ReportScoreCallbackMethod callback)
		{
			if (waitingForOperation)
			{
				Debug.LogError("Must wait for last Score operation to complete.");
				return;
			}
			waitingForOperation = true;
			reportScoreCallback = callback;
			plugin.ReportScore(leaderboardID, score, async_reportScoreCallback, ReignServices.Singleton);
		}

		public static void ReportScore(string leaderboardID, int score, ReportScoreCallbackMethod callback)
		{
			if (waitingForOperation)
			{
				Debug.LogError("Must wait for last Score operation to complete.");
				return;
			}
			LeaderboardDesc leaderboardDesc = findLeaderboard(leaderboardID);
			if (leaderboardDesc == null)
			{
				Debug.LogError("Failed to find leaderboard with ID: " + leaderboardID);
				return;
			}
			if (leaderboardDesc.ScoreFormat != LeaderbaordScoreFormats.Numerical)
			{
				Debug.LogError("Leaderboard Formating type must be Numerical not: " + leaderboardDesc.ScoreFormat);
				return;
			}
			long score2 = (long)((double)score * Math.Pow(10.0, leaderboardDesc.ScoreFormat_DecimalPlaces));
			waitingForOperation = true;
			reportScoreCallback = callback;
			plugin.ReportScore(leaderboardID, score2, async_reportScoreCallback, ReignServices.Singleton);
		}

		public static void ReportScore(string leaderboardID, float score, ReportScoreCallbackMethod callback)
		{
			if (waitingForOperation)
			{
				Debug.LogError("Must wait for last Score operation to complete.");
				return;
			}
			LeaderboardDesc leaderboardDesc = findLeaderboard(leaderboardID);
			if (leaderboardDesc == null)
			{
				Debug.LogError("Failed to find leaderboard with ID: " + leaderboardID);
				return;
			}
			if (leaderboardDesc.ScoreFormat != LeaderbaordScoreFormats.Numerical)
			{
				Debug.LogError("Leaderboard Formating type must be Numerical not: " + leaderboardDesc.ScoreFormat);
				return;
			}
			long score2 = (long)(Math.Round(score, leaderboardDesc.ScoreFormat_DecimalPlaces) * Math.Pow(10.0, leaderboardDesc.ScoreFormat_DecimalPlaces));
			waitingForOperation = true;
			reportScoreCallback = callback;
			plugin.ReportScore(leaderboardID, score2, async_reportScoreCallback, ReignServices.Singleton);
		}

		public static void ReportScore(string leaderboardID, TimeSpan score, ReportScoreCallbackMethod callback)
		{
			if (waitingForOperation)
			{
				Debug.LogError("Must wait for last Score operation to complete.");
				return;
			}
			LeaderboardDesc leaderboardDesc = findLeaderboard(leaderboardID);
			if (leaderboardDesc == null)
			{
				Debug.LogError("Failed to find leaderboard with ID: " + leaderboardID);
				return;
			}
			if (leaderboardDesc.ScoreFormat != LeaderbaordScoreFormats.Time)
			{
				Debug.LogError("Leaderboard Formating type must be Time not: " + leaderboardDesc.ScoreFormat);
				return;
			}
			long num = 0L;
			switch (leaderboardDesc.ScoreTimeFormat)
			{
			case LeaderboardScoreTimeFormats.Minutes:
				num = (long)score.TotalMinutes;
				break;
			case LeaderboardScoreTimeFormats.Seconds:
				num = (long)score.TotalSeconds;
				break;
			case LeaderboardScoreTimeFormats.Centiseconds:
				num = (long)(score.TotalSeconds / 100.0);
				break;
			case LeaderboardScoreTimeFormats.Milliseconds:
				num = (long)score.TotalMilliseconds;
				break;
			default:
				Debug.LogError("Unsuported LeaderboardScoreTimeFormat: " + leaderboardDesc.ScoreTimeFormat);
				return;
			}
			waitingForOperation = true;
			reportScoreCallback = callback;
			plugin.ReportScore(leaderboardID, num, async_reportScoreCallback, ReignServices.Singleton);
		}

		private static LeaderboardDesc findLeaderboard(string leaderboardID)
		{
			LeaderboardDesc[] leaderboardDescs = desc.LeaderboardDescs;
			foreach (LeaderboardDesc leaderboardDesc in leaderboardDescs)
			{
				if (leaderboardDesc.ID == leaderboardID)
				{
					return leaderboardDesc;
				}
			}
			return null;
		}

		public static void RequestScores(string leaderboardID, int offset, int range, RequestScoresCallbackMethod callback)
		{
			if (waitingForOperation)
			{
				Debug.LogError("Must wait for last Score operation to complete.");
				return;
			}
			waitingForOperation = true;
			requestScoresCallback = callback;
			plugin.RequestScores(leaderboardID, offset, range, async_requestScoresCallback, ReignServices.Singleton);
		}

		public static void ReportAchievement(string achievementID, float percentComplete, ReportAchievementCallbackMethod callback)
		{
			if (waitingForOperation)
			{
				Debug.LogError("Must wait for last Score operation to complete.");
				return;
			}
			if (percentComplete < 0f)
			{
				percentComplete = 0f;
			}
			waitingForOperation = true;
			reportAchievementCallback = callback;
			plugin.ReportAchievement(achievementID, percentComplete, async_reportAchievementCallback, ReignServices.Singleton);
		}

		public static void RequestAchievements(RequestAchievementsCallbackMethod callback)
		{
			if (waitingForOperation)
			{
				Debug.LogError("Must wait for last Score operation to complete.");
				return;
			}
			waitingForOperation = true;
			requestAchievementsCallback = callback;
			plugin.RequestAchievements(async_requestAchievementsCallback, ReignServices.Singleton);
		}

		public static void ShowNativeScoresPage(string leaderboardID, ShowNativeViewDoneCallbackMethod callback)
		{
			if (waitingForOperation)
			{
				Debug.LogError("Must wait for last Score operation to complete.");
				return;
			}
			waitingForOperation = true;
			showNativeViewCallback = callback;
			plugin.ShowNativeScoresPage(leaderboardID, async_showNativeViewCallback, ReignServices.Singleton);
		}

		public static void ShowNativeAchievementsPage(ShowNativeViewDoneCallbackMethod callback)
		{
			if (waitingForOperation)
			{
				Debug.LogError("Must wait for last Score operation to complete.");
				return;
			}
			waitingForOperation = true;
			showNativeViewCallback = callback;
			plugin.ShowNativeAchievementsPage(async_showNativeViewCallback, ReignServices.Singleton);
		}

		public static void ResetUserAchievementsProgress(ResetUserAchievementsCallbackMethod callback)
		{
			if (waitingForOperation)
			{
				Debug.LogError("Must wait for last Score operation to complete.");
				return;
			}
			waitingForOperation = true;
			resetUserAchievementsCallback = callback;
			plugin.ResetUserAchievementsProgress(async_resetUserAchievementsCallback, ReignServices.Singleton);
		}


	}
}
