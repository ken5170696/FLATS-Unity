using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
namespace Reign.Plugin
{
	public class ReignScores_UnityUI : MonoBehaviour, IScores_UI
	{
		public GameObject TitlePanel;

		public GameObject LoginPanel;

		public GameObject CreateUserPanel;

		public GameObject LoaderPanel;

		public GameObject ScoresPanel;

		public GameObject AchievementsPanel;

		public Text LoginScreen_ErrorMessage;

		public Text CreateUserScreen_ErrorMessage;

		public RectTransform Spinner;

		public Button LoginScreen_LoginButton;

		public Button LoginScreen_CreateUserButton;

		public Button LoginScreen_CancelButton;

		public InputField LoginScreen_UsernameInput;

		public InputField LoginScreen_PasswordInput;

		public Button CreateUserScreen_CreateButton;

		public Button CreateUserScreen_CancleButton;

		public InputField CreateUserScreen_UsernameInput;

		public InputField CreateUserScreen_PasswordInput;

		public InputField CreateUserScreen_PasswordInput2;

		public Image ScoresImage;

		public Image AchievementsImage;

		public Button Scores_NextButton;

		public Button Scores_PrevButton;

		public Button Scores_CloseButton;

		public Button Achievements_NextButton;

		public Button Achievements_PrevButton;

		public Button Achievements_CloseButton;

		public Text[] Scores_Usernames;

		public Text[] Scores_ScoreValues;

		public Text[] Achievements_Names;

		public Text[] Achievements_Descs;

		public Image[] AchievementImages;

		public int TopScoresToListPerPage;

		public int AchievementsToListPerPage;

		private IScorePlugin plugin;

		private AuthenticateCallbackMethod authenticateCallback;

		private ShowNativeViewDoneCallbackMethod showNativeViewDoneCallback;

		private int scoreOffset;

		private int achievementOffset;

		private string leaderboardID;

		private Achievement[] achievements;

		private ReignScores_UnityUIModes _mode;

		private ReignScores_UnityUIModes mode
		{
			get
			{
				return _mode;
			}
			set
			{
				switch (value)
				{
				case ReignScores_UnityUIModes.None:
					base.gameObject.SetActive(false);
					break;
				case ReignScores_UnityUIModes.Login:
					base.gameObject.SetActive(true);
					disableAll(_mode != ReignScores_UnityUIModes.LoggingIn);
					LoginPanel.gameObject.SetActive(true);
					LoginScreen_UsernameInput.text = "";
					LoginScreen_PasswordInput.text = "";
					break;
				case ReignScores_UnityUIModes.LoggingIn:
					LoaderPanel.gameObject.SetActive(true);
					LoginScreen_ErrorMessage.text = "";
					plugin.ManualLogin(LoginScreen_UsernameInput.text, LoginScreen_PasswordInput.text, null, this);
					break;
				case ReignScores_UnityUIModes.CreateUser:
					base.gameObject.SetActive(true);
					disableAll(_mode != ReignScores_UnityUIModes.CreatingUser);
					CreateUserPanel.gameObject.SetActive(true);
					CreateUserScreen_UsernameInput.text = "";
					CreateUserScreen_PasswordInput.text = "";
					CreateUserScreen_PasswordInput2.text = "";
					break;
				case ReignScores_UnityUIModes.CreatingUser:
					LoaderPanel.gameObject.SetActive(true);
					CreateUserScreen_ErrorMessage.text = "";
					plugin.ManualCreateUser(CreateUserScreen_UsernameInput.text, CreateUserScreen_PasswordInput.text, null, this);
					break;
				case ReignScores_UnityUIModes.LoadingScores:
				case ReignScores_UnityUIModes.LoadingAchievements:
					base.gameObject.SetActive(true);
					disableAll(true);
					TitlePanel.gameObject.SetActive(false);
					LoaderPanel.gameObject.SetActive(true);
					break;
				case ReignScores_UnityUIModes.ShowingScores:
					LoaderPanel.gameObject.SetActive(false);
					ScoresPanel.gameObject.SetActive(true);
					break;
				case ReignScores_UnityUIModes.ShowingAchievements:
					LoaderPanel.gameObject.SetActive(false);
					AchievementsPanel.gameObject.SetActive(true);
					break;
				default:
					Debug.LogError("Unimplemented type: " + value);
					break;
				}
				_mode = value;
			}
		}

		public event ScoreFormatCallbackMethod ScoreFormatCallback;

		public void Init(IScorePlugin plugin)
		{
			this.plugin = plugin;
			mode = ReignScores_UnityUIModes.None;
			fitImageInView(ScoresImage);
			fitImageInView(AchievementsImage);
			LoginScreen_LoginButton.onClick.AddListener(LoginScreen_LoginButton_Clicked);
			LoginScreen_CreateUserButton.onClick.AddListener(LoginScreen_CreateUserButton_Clicked);
			LoginScreen_CancelButton.onClick.AddListener(LoginScreen_CancelButton_Clicked);
			CreateUserScreen_CreateButton.onClick.AddListener(CreateUserScreen_CreateButton_Clicked);
			CreateUserScreen_CancleButton.onClick.AddListener(CreateUserScreen_CancleButton_Clicked);
			Scores_NextButton.onClick.AddListener(Scores_NextButton_Clicked);
			Scores_PrevButton.onClick.AddListener(Scores_PrevButton_Clicked);
			Scores_CloseButton.onClick.AddListener(Scores_CloseButton_Clicked);
			Achievements_NextButton.onClick.AddListener(Achievements_NextButton_Clicked);
			Achievements_PrevButton.onClick.AddListener(Achievements_PrevButton_Clicked);
			Achievements_CloseButton.onClick.AddListener(Achievements_CloseButton_Clicked);
		}

		private void fitImageInView(Image image)
		{
			RectTransform component = image.GetComponent<RectTransform>();
			float objectWidth = (float)image.mainTexture.width / (float)Screen.width;
			float objectHeight = (float)image.mainTexture.height / (float)Screen.height;
			Vector2 vector = MathUtilities.FitInView(objectWidth, objectHeight, 1f, 1f);
			component.anchorMin = new Vector2(0f - vector.x * 0.5f + 0.5f, 0f - vector.y * 0.5f + 0.5f);
			component.anchorMax = new Vector2(0f - vector.x * 0.5f + 0.5f + vector.x, 0f - vector.y * 0.5f + 0.5f + vector.y);
			component.offsetMin = Vector2.zero;
			component.offsetMax = Vector2.zero;
		}

		public void RequestLogin(AuthenticateCallbackMethod callback)
		{
			if (mode == ReignScores_UnityUIModes.None)
			{
				mode = ReignScores_UnityUIModes.Login;
				authenticateCallback = callback;
			}
		}

		public void AutoLogin(AuthenticateCallbackMethod callback)
		{
			mode = ReignScores_UnityUIModes.None;
			authenticateCallback = callback;
		}

		public void LoginCallback(bool succeeded, string errorMessage)
		{
			if (succeeded)
			{
				LoginScreen_ErrorMessage.text = "";
				CreateUserScreen_ErrorMessage.text = "";
				mode = ReignScores_UnityUIModes.None;
				if (authenticateCallback != null)
				{
					authenticateCallback(true, null);
				}
				return;
			}
			string text = ((errorMessage != null) ? errorMessage : "Unknown Error ???");
			LoginScreen_ErrorMessage.text = text;
			CreateUserScreen_ErrorMessage.text = text;
			if (mode == ReignScores_UnityUIModes.LoggingIn)
			{
				mode = ReignScores_UnityUIModes.Login;
			}
			else if (mode == ReignScores_UnityUIModes.CreatingUser)
			{
				mode = ReignScores_UnityUIModes.CreateUser;
			}
			else
			{
				mode = ReignScores_UnityUIModes.Login;
			}
		}

		public void ShowNativeScoresPage(string leaderboardID, ShowNativeViewDoneCallbackMethod callback)
		{
			mode = ReignScores_UnityUIModes.LoadingScores;
			showNativeViewDoneCallback = callback;
			this.leaderboardID = leaderboardID;
			scoreOffset = 0;
			plugin.RequestScores(leaderboardID, scoreOffset, TopScoresToListPerPage, requestScoresCallback, this);
		}

		private void requestScoresCallback(LeaderboardScore[] scores, bool succeeded, string errorMessage)
		{
			if (succeeded)
			{
				mode = ReignScores_UnityUIModes.ShowingScores;
				Text[] scores_Usernames = Scores_Usernames;
				foreach (Text text in scores_Usernames)
				{
					text.text = "";
				}
				Text[] scores_ScoreValues = Scores_ScoreValues;
				foreach (Text text2 in scores_ScoreValues)
				{
					text2.text = "";
				}
				for (int k = 0; k != TopScoresToListPerPage && k < Scores_Usernames.Length && k < Scores_ScoreValues.Length && k < scores.Length; k++)
				{
					LeaderboardScore leaderboardScore = scores[k];
					Scores_Usernames[k].text = leaderboardScore.Username;
					string scoreValue;
					if (this.ScoreFormatCallback != null)
					{
						this.ScoreFormatCallback(leaderboardScore.Score, out scoreValue);
					}
					else
					{
						scoreValue = leaderboardScore.Score.ToString();
					}
					Scores_ScoreValues[k].text = scoreValue;
				}
			}
			else
			{
				mode = ReignScores_UnityUIModes.None;
				if (showNativeViewDoneCallback != null)
				{
					showNativeViewDoneCallback(false, errorMessage);
				}
			}
		}

		public void ShowNativeAchievementsPage(ShowNativeViewDoneCallbackMethod callback)
		{
			mode = ReignScores_UnityUIModes.LoadingAchievements;
			achievementOffset = 0;
			showNativeViewDoneCallback = callback;
			plugin.RequestAchievements(requestAchievementsCallback, this);
		}

		private void requestAchievementsCallback(Achievement[] achievements, bool succeeded, string errorMessage)
		{
			if (succeeded)
			{
				mode = ReignScores_UnityUIModes.ShowingAchievements;
				this.achievements = achievements;
				processAchievements();
				return;
			}
			mode = ReignScores_UnityUIModes.None;
			if (showNativeViewDoneCallback != null)
			{
				showNativeViewDoneCallback(false, errorMessage);
			}
		}

		private void processAchievements()
		{
			Text[] achievements_Names = Achievements_Names;
			foreach (Text text in achievements_Names)
			{
				text.text = "";
			}
			Text[] achievements_Descs = Achievements_Descs;
			foreach (Text text2 in achievements_Descs)
			{
				text2.text = "";
			}
			Image[] achievementImages = AchievementImages;
			foreach (Image image in achievementImages)
			{
				image.gameObject.SetActive(false);
			}
			int num = achievementOffset;
			int num2 = 0;
			while (num != achievementOffset + AchievementsToListPerPage && num2 < Achievements_Names.Length && num2 < Achievements_Descs.Length && num2 < AchievementImages.Length && num < achievements.Length)
			{
				Achievement achievement = achievements[num];
				Achievements_Names[num2].text = achievement.Name;
				Achievements_Descs[num2].text = achievement.Desc;
				AchievementImages[num2].sprite = (achievement.IsAchieved ? achievement.AchievedSprite : achievement.UnachievedSprite);
				AchievementImages[num2].gameObject.SetActive(true);
				num++;
				num2++;
			}
		}

		private void disableAll(bool clearErrors)
		{
			if (clearErrors)
			{
				LoginScreen_ErrorMessage.text = "";
				CreateUserScreen_ErrorMessage.text = "";
			}
			TitlePanel.gameObject.SetActive(true);
			LoginPanel.gameObject.SetActive(false);
			CreateUserPanel.gameObject.SetActive(false);
			LoaderPanel.gameObject.SetActive(false);
			ScoresPanel.gameObject.SetActive(false);
			AchievementsPanel.gameObject.SetActive(false);
		}

		private void LoginScreen_LoginButton_Clicked()
		{
			if (string.IsNullOrEmpty(LoginScreen_UsernameInput.text) || LoginScreen_UsernameInput.text.Length < 3)
			{
				string text = "Username must be at least 3 characters long";
				Debug.LogError(text);
				LoginScreen_ErrorMessage.text = text;
			}
			else if (string.IsNullOrEmpty(LoginScreen_PasswordInput.text) || LoginScreen_PasswordInput.text.Length < 4)
			{
				string text2 = "Password must be at least 4 characters long";
				Debug.LogError(text2);
				LoginScreen_ErrorMessage.text = text2;
			}
			else
			{
				mode = ReignScores_UnityUIModes.LoggingIn;
			}
		}

		private void LoginScreen_CreateUserButton_Clicked()
		{
			mode = ReignScores_UnityUIModes.CreateUser;
		}

		private void LoginScreen_CancelButton_Clicked()
		{
			mode = ReignScores_UnityUIModes.None;
			if (authenticateCallback != null)
			{
				authenticateCallback(false, "Canceled");
			}
		}

		private void CreateUserScreen_CreateButton_Clicked()
		{
			if (string.IsNullOrEmpty(CreateUserScreen_UsernameInput.text) || CreateUserScreen_UsernameInput.text.Length < 3)
			{
				string text = "Username must be at least 3 characters long";
				Debug.LogError(text);
				CreateUserScreen_ErrorMessage.text = text;
			}
			else if (string.IsNullOrEmpty(CreateUserScreen_PasswordInput.text) || CreateUserScreen_PasswordInput.text.Length < 4)
			{
				string text2 = "Password must be at least 4 characters long";
				Debug.LogError(text2);
				CreateUserScreen_ErrorMessage.text = text2;
			}
			else if (string.IsNullOrEmpty(CreateUserScreen_PasswordInput2.text))
			{
				string text3 = "Please Re-Enter your password";
				Debug.LogError(text3);
				CreateUserScreen_ErrorMessage.text = text3;
			}
			else if (CreateUserScreen_PasswordInput.text != CreateUserScreen_PasswordInput2.text)
			{
				string text4 = "Passwords do not match";
				Debug.LogError(text4);
				CreateUserScreen_ErrorMessage.text = text4;
			}
			else
			{
				mode = ReignScores_UnityUIModes.CreatingUser;
			}
		}

		private void CreateUserScreen_CancleButton_Clicked()
		{
			mode = ReignScores_UnityUIModes.Login;
		}

		private void Scores_NextButton_Clicked()
		{
			if (Scores_Usernames == null || Scores_Usernames.Length == 0 || !string.IsNullOrEmpty(Scores_Usernames[0].text))
			{
				scoreOffset += TopScoresToListPerPage;
				plugin.RequestScores(leaderboardID, scoreOffset, TopScoresToListPerPage, requestScoresCallback, this);
			}
		}

		private void Scores_PrevButton_Clicked()
		{
			if (scoreOffset - TopScoresToListPerPage >= 0)
			{
				scoreOffset -= TopScoresToListPerPage;
				plugin.RequestScores(leaderboardID, scoreOffset, TopScoresToListPerPage, requestScoresCallback, this);
			}
		}

		private void Scores_CloseButton_Clicked()
		{
			mode = ReignScores_UnityUIModes.None;
			if (showNativeViewDoneCallback != null)
			{
				showNativeViewDoneCallback(true, null);
			}
		}

		private void Achievements_NextButton_Clicked()
		{
			int num = achievementOffset + AchievementsToListPerPage;
			if (num < achievements.Length)
			{
				achievementOffset = num;
				processAchievements();
			}
		}

		private void Achievements_PrevButton_Clicked()
		{
			int num = achievementOffset - AchievementsToListPerPage;
			if (num >= 0)
			{
				achievementOffset = num;
				processAchievements();
			}
		}

		private void Achievements_CloseButton_Clicked()
		{
			mode = ReignScores_UnityUIModes.None;
			if (showNativeViewDoneCallback != null)
			{
				showNativeViewDoneCallback(true, null);
			}
		}

		private void Update()
		{
			if (LoaderPanel.gameObject.activeSelf)
			{
				Spinner.Rotate(Vector3.forward, 1f);
			}
		}

		public ReignScores_UnityUI()
		{
			TopScoresToListPerPage = 10;
			AchievementsToListPerPage = 10;

		}




	}
}
