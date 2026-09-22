using System;
using System.Runtime.InteropServices;
using Reign.Plugin.XML;
using UnityEngine;
namespace Reign.Plugin
{
	public class ReignScores_ScorePlugin : IScorePlugin
	{
		private ReignScores_ServicesHelper helper;

		private IScores_UI ui;

		private ScoreDesc desc;

		private string gameID;

		private Achievement[] achievements;

		private ReportScoreCallbackMethod ReportScore_callback;

		private RequestScoresCallbackMethod RequestScores_callback;

		private ReportAchievementCallbackMethod ReportAchievement_callback;

		private RequestAchievementsCallbackMethod RequestAchievements_callback;

		public bool IsAuthenticated { get; private set; }

		public string Username { get; private set; }

		public string UserID { get; private set; }

		public ReignScores_ScorePlugin(ScoreDesc desc, CreatedScoreAPICallbackMethod callback)
		{
			this.desc = desc;
			gameID = desc.WinRT_ReignScores_GameID;
			ui = desc.ReignScores_UI;
			ui.Init(this);
			helper = new ReignScores_ServicesHelper(desc);
			if (callback != null)
			{
				callback(true, null);
			}
		}

		public void Authenticate(AuthenticateCallbackMethod callback, MonoBehaviour services)
		{
			try
			{
				if (PlayerPrefs.HasKey("ReignScores_Username"))
				{
					ui.AutoLogin(callback);
					string text = PlayerPrefs.GetString("ReignScores_Username");
					string text2 = PlayerPrefs.GetString("ReignScores_Pass");
					helper.InvokeServiceMethod(ReignScores_ServiceTypes.Users, "Login", loginCallback, services, "game_id=" + gameID, "username=" + text, "password=" + text2);
				}
				else
				{
					ui.RequestLogin(callback);
				}
			}
			catch (Exception ex)
			{
				string text3 = "ReignScores Authenticate error: " + ex.Message;
				Debug.LogError(text3);
				IsAuthenticated = false;
				if (callback != null)
				{
					callback(false, text3);
				}
			}
		}

		public void Logout()
		{
			IsAuthenticated = false;
			UserID = "???";
			Username = "???";
			if (PlayerPrefs.HasKey("ReignScores_Username"))
			{
				PlayerPrefs.DeleteKey("ReignScores_Username");
			}
			if (PlayerPrefs.HasKey("ReignScores_Pass"))
			{
				PlayerPrefs.DeleteKey("ReignScores_Pass");
			}
		}

		public void ManualLogin(string username, string password, AuthenticateCallbackMethod callback, MonoBehaviour services)
		{
			PlayerPrefs.SetString("ReignScores_Username", username);
			PlayerPrefs.SetString("ReignScores_Pass", password);
			helper.InvokeServiceMethod(ReignScores_ServiceTypes.Users, "Login", loginCallback, services, "game_id=" + gameID, "username=" + username, "password=" + password);
		}

		private void loginCallback(bool succeeded, WebResponse response)
		{
			IsAuthenticated = succeeded;
			if (succeeded)
			{
				UserID = response.UserID;
				Username = response.Username;
			}
			else
			{
				Debug.LogError((response != null) ? response.ErrorMessage : "Unkown");
				Logout();
			}
			ui.LoginCallback(succeeded, (response != null) ? response.ErrorMessage : "Unkown");
		}

		public void ManualCreateUser(string username, string password, AuthenticateCallbackMethod callback, MonoBehaviour services)
		{
			PlayerPrefs.SetString("ReignScores_Username", username);
			PlayerPrefs.SetString("ReignScores_Pass", password);
			helper.InvokeServiceMethod(ReignScores_ServiceTypes.Games, "CreateUser", createUserCallback, services, "game_id=" + gameID, "username=" + username, "password=" + password);
		}

		private void createUserCallback(bool succeeded, WebResponse response)
		{
			IsAuthenticated = succeeded;
			if (succeeded)
			{
				UserID = response.UserID;
				Username = response.Username;
			}
			else
			{
				Debug.LogError((response != null) ? response.ErrorMessage : "Unkown");
				Logout();
			}
			ui.LoginCallback(succeeded, (response != null) ? response.ErrorMessage : "Unkown");
		}

		private LeaderboardDesc findLeaderboard(string leaderboardID)
		{
			LeaderboardDesc leaderboardDesc = null;
			LeaderboardDesc[] leaderboardDescs = desc.LeaderboardDescs;
			foreach (LeaderboardDesc leaderboardDesc2 in leaderboardDescs)
			{
				if (leaderboardDesc2.ID == leaderboardID)
				{
					leaderboardDesc = leaderboardDesc2;
					break;
				}
			}
			if (leaderboardDesc == null)
			{
				Debug.LogError("Failed to find leaderboardID: " + leaderboardID);
				return null;
			}
			return leaderboardDesc;
		}

		private AchievementDesc findAchievement(string achievementID)
		{
			AchievementDesc achievementDesc = null;
			AchievementDesc[] achievementDescs = desc.AchievementDescs;
			foreach (AchievementDesc achievementDesc2 in achievementDescs)
			{
				if (achievementDesc2.ID == achievementID)
				{
					achievementDesc = achievementDesc2;
					break;
				}
			}
			if (achievementDesc == null)
			{
				Debug.LogError("Failed: achievementID not found.");
				return null;
			}
			return achievementDesc;
		}

		public void ReportScore(string leaderboardID, long score, ReportScoreCallbackMethod callback, MonoBehaviour services)
		{
			LeaderboardDesc leaderboardDesc = findLeaderboard(leaderboardID);
			if (leaderboardDesc == null)
			{
				if (callback != null)
				{
					callback(false, "Failed to find leaderboardID: " + leaderboardID);
				}
				return;
			}
			Guid winRT_ReignScores_ID = leaderboardDesc.WinRT_ReignScores_ID;
			ReportScore_callback = callback;
			helper.InvokeServiceMethod(ReignScores_ServiceTypes.Users, "ReportScore", reportScoreCallback, services, "user_id=" + UserID, "leaderboard_id=" + winRT_ReignScores_ID, "score=" + score);
		}

		private void reportScoreCallback(bool succeeded, WebResponse response)
		{
			if (ReportScore_callback != null)
			{
				ReportScore_callback(succeeded, (response != null) ? response.ErrorMessage : "Unknown");
			}
		}

		public void RequestScores(string leaderboardID, int offset, int range, RequestScoresCallbackMethod callback, MonoBehaviour services)
		{
			LeaderboardDesc leaderboardDesc = findLeaderboard(leaderboardID);
			if (leaderboardDesc == null)
			{
				if (callback != null)
				{
					callback(null, false, "Failed to find leaderboardID: " + leaderboardID);
				}
				return;
			}
			Guid winRT_ReignScores_ID = leaderboardDesc.WinRT_ReignScores_ID;
			RequestScores_callback = callback;
			helper.InvokeServiceMethod(ReignScores_ServiceTypes.Games, "RequestScores", requestScoresCallback, services, "user_id=" + UserID, "leaderboard_id=" + winRT_ReignScores_ID, "offset=" + offset, "range=" + range, "sort_order=" + leaderboardDesc.SortOrder);
		}

		private void requestScoresCallback(bool succeeded, WebResponse response)
		{
			if (succeeded)
			{
				LeaderboardScore[] array = new LeaderboardScore[response.Scores.Count];
				for (int i = 0; i != response.Scores.Count; i++)
				{
					WebResponse_Score webResponse_Score = response.Scores[i];
					array[i] = new LeaderboardScore(webResponse_Score.Username, webResponse_Score.Score);
				}
				if (RequestScores_callback != null)
				{
					RequestScores_callback(array, true, null);
				}
			}
			else if (RequestScores_callback != null)
			{
				RequestScores_callback(null, false, (response != null) ? response.ErrorMessage : "Unkown");
			}
		}

		public void ReportAchievement(string achievementID, float percentComplete, ReportAchievementCallbackMethod callback, MonoBehaviour services)
		{
			AchievementDesc achievementDesc = findAchievement(achievementID);
			if (achievementDesc == null)
			{
				if (callback != null)
				{
					callback(false, "Failed to find achievementID: " + achievementID);
				}
				return;
			}
			Guid winRT_ReignScores_ID = achievementDesc.WinRT_ReignScores_ID;
			ReportAchievement_callback = callback;
			helper.InvokeServiceMethod(ReignScores_ServiceTypes.Users, "ReportAchievement", reportAchievementCallback, services, "user_id=" + UserID, "achievement_id=" + winRT_ReignScores_ID, "percent_complete=" + percentComplete / (float)achievementDesc.PercentCompletedAtValue * 100f);
		}

		private void reportAchievementCallback(bool succeeded, WebResponse response)
		{
			if (ReportAchievement_callback != null)
			{
				ReportAchievement_callback(succeeded, (response != null) ? response.ErrorMessage : "Unknown");
			}
		}

		public void RequestAchievements(RequestAchievementsCallbackMethod callback, MonoBehaviour services)
		{
			RequestAchievements_callback = callback;
			helper.InvokeServiceMethod(ReignScores_ServiceTypes.Users, "RequestAchievements", requestAchievementsCallback, services, "user_id=" + UserID);
		}

		private void requestAchievementsCallback(bool succeeded, WebResponse response)
		{
			if (succeeded)
			{
				if (achievements == null || achievements.Length != response.Achievements.Count)
				{
					achievements = new Achievement[desc.AchievementDescs.Length];
				}
				for (int i = 0; i != desc.AchievementDescs.Length; i++)
				{
					AchievementDesc achievementDesc = desc.AchievementDescs[i];
					Guid winRT_ReignScores_ID = achievementDesc.WinRT_ReignScores_ID;
					WebResponse_Achievement webResponse_Achievement = null;
					foreach (WebResponse_Achievement achievement in response.Achievements)
					{
						if (winRT_ReignScores_ID == new Guid(achievement.AchievementID))
						{
							webResponse_Achievement = achievement;
							break;
						}
					}
					if (achievements[i] == null)
					{
						string text = "Reign/Achievements/" + achievementDesc.ID + "_achieved";
						Texture2D texture2D = (Texture2D)Resources.Load(text);
						if (texture2D == null)
						{
							string text2 = "RequestAchievements Failed to load texture: " + text;
							Debug.LogError(text2);
							if (RequestAchievements_callback != null)
							{
								RequestAchievements_callback(null, false, text2);
							}
							return;
						}
						text = "Reign/Achievements/" + achievementDesc.ID + "_unachieved";
						Texture2D texture2D2 = (Texture2D)Resources.Load(text);
						if (texture2D2 == null)
						{
							string text3 = "RequestAchievements Failed to load texture: " + text;
							Debug.LogError(text3);
							if (RequestAchievements_callback != null)
							{
								RequestAchievements_callback(null, false, text3);
							}
							return;
						}
						if (webResponse_Achievement != null)
						{
							achievements[i] = new Achievement(webResponse_Achievement.PercentComplete >= (float)achievementDesc.PercentCompletedAtValue, webResponse_Achievement.PercentComplete / 100f * (float)achievementDesc.PercentCompletedAtValue, achievementDesc.ID, achievementDesc.Name, achievementDesc.Desc, texture2D, texture2D2);
						}
						else
						{
							achievements[i] = new Achievement(false, 0f, achievementDesc.ID, achievementDesc.Name, achievementDesc.Desc, texture2D, texture2D2);
						}
					}
					else if (webResponse_Achievement != null)
					{
						achievements[i].IsAchieved = webResponse_Achievement.PercentComplete >= 100f;
						achievements[i].PercentComplete = webResponse_Achievement.PercentComplete;
					}
					else
					{
						achievements[i].IsAchieved = false;
						achievements[i].PercentComplete = 0f;
					}
				}
				if (RequestAchievements_callback != null)
				{
					RequestAchievements_callback(achievements, true, null);
				}
			}
			else if (RequestAchievements_callback != null)
			{
				RequestAchievements_callback(null, false, (response != null) ? response.ErrorMessage : "Unkown");
			}
		}

		public void ShowNativeScoresPage(string leaderboardID, ShowNativeViewDoneCallbackMethod callback, MonoBehaviour services)
		{
			ui.ShowNativeScoresPage(leaderboardID, callback);
		}

		public void ShowNativeAchievementsPage(ShowNativeViewDoneCallbackMethod callback, MonoBehaviour services)
		{
			ui.ShowNativeAchievementsPage(callback);
		}

		public void ResetUserAchievementsProgress(ResetUserAchievementsCallbackMethod callback, MonoBehaviour services)
		{
			if (callback != null)
			{
				callback(false, "Unsupported on this platform!");
			}
		}

		public void Update()
		{
		}




	}
}
