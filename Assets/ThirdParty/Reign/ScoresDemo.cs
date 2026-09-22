using System;
using System.Runtime.InteropServices;
using Reign;
using Reign.Plugin;
using UnityEngine;
using UnityEngine.UI;
public class ScoresDemo : MonoBehaviour
{
	private static bool created;

	public bool UseUnityUI;

	public GameObject ReignScores_ModernRenderer;

	public GameObject ReignScores_ClassicRenderer;

	public Button BackButton;

	public Button LogoutButton;

	public Button ReportScoreButton;

	public Button ReportAchievementButton;

	public Button ShowLeaderboardsButton;

	public Button ShowAchievementsButton;

	public Text StatusText;

	private bool disableUI;

	private GUIStyle uiStyle;

	private void Start()
	{
		BackButton.Select();
		LogoutButton.onClick.AddListener(LogoutButton_Clicked);
		BackButton.onClick.AddListener(BackButton_Clicked);
		ReportScoreButton.onClick.AddListener(ReportScoreButton_Clicked);
		ReportAchievementButton.onClick.AddListener(ReportAchievementButton_Clicked);
		ShowLeaderboardsButton.onClick.AddListener(ShowLeaderboardsButton_Clicked);
		ShowAchievementsButton.onClick.AddListener(ShowAchievementsButton_Clicked);
		if (!created)
		{
			created = true;
			if (!UseUnityUI)
			{
				uiStyle = new GUIStyle
				{
					alignment = TextAnchor.MiddleCenter,
					fontSize = 32,
					normal = new GUIStyleState
					{
						textColor = Color.white
					}
				};
			}
			LeaderboardDesc[] array = new LeaderboardDesc[1];
			LeaderboardDesc leaderboardDesc = (array[0] = new LeaderboardDesc());
			Guid guid = new Guid("f55e3800-eacd-4728-ae4f-31b00aaa63bf");
			leaderboardDesc.SortOrder = LeaderboardSortOrders.Ascending;
			leaderboardDesc.ScoreFormat = LeaderbaordScoreFormats.Numerical;
			leaderboardDesc.ScoreFormat_DecimalPlaces = 0;
			leaderboardDesc.ScoreTimeFormat = LeaderboardScoreTimeFormats.Milliseconds;
			leaderboardDesc.ID = "Level1";
			leaderboardDesc.Desc = "Level1 Desc...";
			leaderboardDesc.Editor_ReignScores_ID = guid;
			leaderboardDesc.WinRT_ReignScores_ID = guid;
			leaderboardDesc.WP8_ReignScores_ID = guid;
			leaderboardDesc.BB10_ReignScores_ID = guid;
			leaderboardDesc.iOS_ReignScores_ID = guid;
			leaderboardDesc.iOS_GameCenter_ID = "";
			leaderboardDesc.Android_ReignScores_ID = guid;
			leaderboardDesc.Android_GooglePlay_ID = "";
			leaderboardDesc.Android_GameCircle_ID = "";
			leaderboardDesc.Win32_ReignScores_ID = guid;
			leaderboardDesc.OSX_ReignScores_ID = guid;
			leaderboardDesc.Linux_ReignScores_ID = guid;
			AchievementDesc[] array2 = new AchievementDesc[1];
			AchievementDesc achievementDesc = (array2[0] = new AchievementDesc());
			Guid guid2 = new Guid("352ce53d-142f-4a10-a4fb-804ad38be879");
			achievementDesc.ID = "Achievement1";
			achievementDesc.Name = "Achievement1";
			achievementDesc.Desc = "Achievement1 Desc...";
			achievementDesc.PercentCompletedAtValue = 100;
			achievementDesc.IsIncremental = true;
			achievementDesc.Editor_ReignScores_ID = guid2;
			achievementDesc.WinRT_ReignScores_ID = guid2;
			achievementDesc.WP8_ReignScores_ID = guid2;
			achievementDesc.BB10_ReignScores_ID = guid2;
			achievementDesc.iOS_ReignScores_ID = guid2;
			achievementDesc.iOS_GameCenter_ID = "";
			achievementDesc.Android_ReignScores_ID = guid2;
			achievementDesc.Android_GooglePlay_ID = "";
			achievementDesc.Android_GameCircle_ID = "";
			achievementDesc.Win32_ReignScores_ID = guid2;
			achievementDesc.OSX_ReignScores_ID = guid2;
			achievementDesc.Linux_ReignScores_ID = guid2;
			ScoreDesc scoreDesc = new ScoreDesc();
			if (UseUnityUI)
			{
				scoreDesc.ReignScores_UI = ReignScores_ModernRenderer.GetComponent<ReignScores_UnityUI>();
			}
			else
			{
				scoreDesc.ReignScores_UI = ReignScores_ClassicRenderer.GetComponent<MonoBehaviour>() as IScores_UI;
			}
			scoreDesc.ReignScores_UI.ScoreFormatCallback += scoreFormatCallback;
			scoreDesc.ReignScores_ServicesURL = "http://localhost:5537/Services/";
			scoreDesc.ReignScores_GameKey = "04E0676D-AAF8-4836-A584-DE0C1D618D84";
			scoreDesc.ReignScores_UserKey = "CE8E55E1-F383-4F05-9388-5C89F27B7FF2";
			scoreDesc.LeaderboardDescs = array;
			scoreDesc.AchievementDescs = array2;
			scoreDesc.Editor_ScoreAPI = ScoreAPIs.ReignScores;
			scoreDesc.Editor_ReignScores_GameID = "B2A24047-0487-41C4-B151-0F175BB54D0E";
			scoreDesc.WinRT_ScoreAPI = ScoreAPIs.ReignScores;
			scoreDesc.WinRT_ReignScores_GameID = "B2A24047-0487-41C4-B151-0F175BB54D0E";
			scoreDesc.WP8_ScoreAPI = ScoreAPIs.ReignScores;
			scoreDesc.WP8_ReignScores_GameID = "B2A24047-0487-41C4-B151-0F175BB54D0E";
			scoreDesc.BB10_ScoreAPI = ScoreAPIs.ReignScores;
			scoreDesc.BB10_ReignScores_GameID = "B2A24047-0487-41C4-B151-0F175BB54D0E";
			scoreDesc.iOS_ScoreAPI = ScoreAPIs.GameCenter;
			scoreDesc.iOS_ReignScores_GameID = "B2A24047-0487-41C4-B151-0F175BB54D0E";
			scoreDesc.Android_ScoreAPI = ScoreAPIs.ReignScores;
			scoreDesc.Android_ReignScores_GameID = "B2A24047-0487-41C4-B151-0F175BB54D0E";
			scoreDesc.Win32_ScoreAPI = ScoreAPIs.ReignScores;
			scoreDesc.Win32_ReignScores_GameID = "B2A24047-0487-41C4-B151-0F175BB54D0E";
			scoreDesc.OSX_ScoreAPI = ScoreAPIs.ReignScores;
			scoreDesc.OSX_ReignScores_GameID = "B2A24047-0487-41C4-B151-0F175BB54D0E";
			scoreDesc.Linux_ScoreAPI = ScoreAPIs.ReignScores;
			scoreDesc.Linux_ReignScores_GameID = "B2A24047-0487-41C4-B151-0F175BB54D0E";
			ScoreManager.Init(scoreDesc, createdCallback);
		}
	}

	private void BackButton_Clicked()
	{
		Application.LoadLevel("MainDemo");
	}

	private void LogoutButton_Clicked()
	{
		ScoreManager.Logout();
		StatusText.text = "Logged out...";
	}

	private void ReportScoreButton_Clicked()
	{
		ScoreManager.ReportScore("Level1", UnityEngine.Random.Range(0, 500), reportScoreCallback);
	}

	private void ReportAchievementButton_Clicked()
	{
		string achievementID = "Achievement" + 1;
		ScoreManager.ReportAchievement(achievementID, 100f, reportAchievementCallback);
	}

	private void ShowLeaderboardsButton_Clicked()
	{
		ScoreManager.ShowNativeScoresPage("Level1", showNativePageCallback);
	}

	private void ShowAchievementsButton_Clicked()
	{
		ScoreManager.ShowNativeAchievementsPage(showNativePageCallback);
	}

	private void createdCallback(bool success, string errorMessage)
	{
		if (!success)
		{
			Debug.LogError(errorMessage);
		}
		else
		{
			ScoreManager.Authenticate(authenticateCallback);
		}
	}

	private void scoreFormatCallback(long score, out string scoreValue)
	{
		scoreValue = TimeSpan.FromSeconds(score).ToString();
	}

	private void authenticateCallback(bool succeeded, string errorMessage)
	{
		Debug.Log("Authenticated: " + succeeded);
		if (succeeded)
		{
			StatusText.text = "Authenticated as: " + ScoreManager.Username;
		}
		else
		{
			StatusText.text = "Authenticated: " + succeeded;
		}
		if (!succeeded && errorMessage != null)
		{
			Debug.LogError(errorMessage);
		}
		if (succeeded)
		{
			ScoreManager.RequestAchievements(requestAchievementsCallback);
		}
	}

	private void requestAchievementsCallback(Achievement[] achievements, bool succeeded, string errorMessage)
	{
		if (succeeded)
		{
			Debug.Log("Got Achievement count: " + achievements.Length);
			foreach (Achievement achievement in achievements)
			{
				Debug.Log(string.Format("Achievement {0} PercentCompleted {1}", new object[2] { achievement.ID, achievement.PercentComplete }));
			}
		}
		else
		{
			string text = "Request Achievements Error: " + errorMessage;
			Debug.LogError(text);
			StatusText.text = text;
		}
	}

	private void showNativePageCallback(bool succeeded, string errorMessage)
	{
		disableUI = false;
		Debug.Log("Show Native Page: " + succeeded);
		if (!succeeded)
		{
			Debug.LogError(errorMessage);
			StatusText.text = errorMessage;
		}
	}

	private void reportScoreCallback(bool succeeded, string errorMessage)
	{
		Debug.Log("Report Score Done: " + succeeded);
		if (!succeeded)
		{
			Debug.LogError(errorMessage);
			StatusText.text = errorMessage;
		}
	}

	private void reportAchievementCallback(bool succeeded, string errorMessage)
	{
		Debug.Log("Report Achievement Done: " + succeeded);
		if (!succeeded)
		{
			Debug.LogError(errorMessage);
			StatusText.text = errorMessage;
		}
	}

	private void OnGUI()
	{
		if (UseUnityUI)
		{
			return;
		}
		if (ScoreManager.IsAuthenticated && !disableUI)
		{
			float num = 0f;
			GUI.Label(new Rect((float)(Screen.width / 2) - 128f, num, 256f, 32f), "<< Leaderboards & Achievements Demo >>", uiStyle);
			if (GUI.Button(new Rect(0f, num, 64f, 32f), "Back"))
			{
				base.gameObject.SetActive(false);
				Application.LoadLevel("MainDemo");
				return;
			}
			num += 34f;
			GUI.Label(new Rect(0f, num, Screen.width, Screen.height / 8), "Authenticated Username: " + ScoreManager.Username);
			if (GUI.Button(new Rect(0f, Screen.height - 64, 256f, 64f), "Show Leaderboard Scores") || Input.GetKeyUp(KeyCode.L))
			{
				disableUI = true;
				ShowLeaderboardsButton_Clicked();
			}
			if (GUI.Button(new Rect(256f, Screen.height - 64, 256f, 64f), "Show Achievements") || Input.GetKeyUp(KeyCode.A))
			{
				disableUI = true;
				ShowAchievementsButton_Clicked();
			}
			if (GUI.Button(new Rect(Screen.width - 256, num, 256f, 64f), "Report Random Score") || Input.GetKeyUp(KeyCode.S))
			{
				ReportScoreButton_Clicked();
			}
			if (GUI.Button(new Rect(Screen.width - 256, 64f + num, 256f, 64f), "Report Random Achievement") || Input.GetKeyUp(KeyCode.R))
			{
				ReportAchievementButton_Clicked();
			}
			if (GUI.Button(new Rect(Screen.width / 2 - 128, (float)(Screen.height / 2 - 32) + num, 256f, 64f), "Logout") || Input.GetKeyUp(KeyCode.O))
			{
				LogoutButton_Clicked();
			}
		}
		else
		{
			GUI.Label(new Rect(0f, 0f, 256f, 64f), "Not Authenticated!");
		}
	}

	private void Update()
	{
		if (Input.GetKeyUp(KeyCode.Escape))
		{
			Application.Quit();
		}
	}

	public ScoresDemo()
	{
		UseUnityUI = true;

	}




}
