using System;
using System.Runtime.InteropServices;
using UnityEngine;
namespace Reign.Plugin
{
	public class ReignScores_ClassicGUI : MonoBehaviour, IScores_UI
	{
		public string LoginTitle;

		public string CreateUserTitle;

		public Texture BackgroudTexture;

		public Texture TopScoreBoardTexture;

		public Texture AchievementBoardTexture;

		public Texture TopScoreBoardButton_CloseNormal;

		public Texture TopScoreBoardButton_CloseHover;

		public Texture AchievementBoardButton_CloseNormal;

		public Texture AchievementBoardButton_CloseHover;

		public Texture TopScoreBoardButton_PrevNormal;

		public Texture TopScoreBoardButton_PrevHover;

		public Texture TopScoreBoardButton_NextNormal;

		public Texture TopScoreBoardButton_NextHover;

		public Texture AchievementBoardButton_PrevNormal;

		public Texture AchievementBoardButton_PrevHover;

		public Texture AchievementBoardButton_NextNormal;

		public Texture AchievementBoardButton_NextHover;

		public Rect TopScoreBoardFrame_Usernames;

		public Rect TopScoreBoardFrame_Scores;

		public Rect TopScoreBoardFrame_PrevButton;

		public Rect TopScoreBoardFrame_NextButton;

		public Rect TopScoreBoardFrame_CloseBox;

		public Rect AchievementBoardFrame_Names;

		public Rect AchievementBoardFrame_Descs;

		public Rect AchievementBoardFrame_PrevButton;

		public Rect AchievementBoardFrame_NextButton;

		public Rect AchievementBoardFrame_CloseBox;

		public int TopScoreBoardFont_Size;

		public int AchievementBoardFont_Size;

		public Color TopScoreBoardFont_Color;

		public Color AchievementBoardFont_Color;

		public int TopScoresToListPerPage;

		public int AchievementsToListPerPage;

		public bool EnableTestRects;

		public AudioSource AudioSource;

		public AudioClip ButtonClick;

		private IScorePlugin plugin;

		private AuthenticateCallbackMethod authenticateCallback;

		private ReignScores_ClassicGuiModes guiMode;

		private ShowNativeViewDoneCallbackMethod guiShowNativeViewDoneCallback;

		private LeaderboardScore[] guiScores;

		private int guiScoreOffset;

		private string guiLeaderboardID;

		private Achievement[] guiAchievements;

		private int guiAchievementOffset;

		private string userAccount_Name;

		private string userAccount_Pass;

		private string userAccount_ConfPass;

		private string errorText;

		public event ScoreFormatCallbackMethod ScoreFormatCallback;

		public void Init(IScorePlugin plugin)
		{
			this.plugin = plugin;
		}

		public void RequestLogin(AuthenticateCallbackMethod callback)
		{
			if (guiMode == ReignScores_ClassicGuiModes.None)
			{
				guiMode = ReignScores_ClassicGuiModes.Login;
				authenticateCallback = callback;
			}
		}

		public void AutoLogin(AuthenticateCallbackMethod callback)
		{
			guiMode = ReignScores_ClassicGuiModes.None;
			authenticateCallback = callback;
		}

		public void LoginCallback(bool succeeded, string errorMessage)
		{
			if (succeeded)
			{
				errorText = "";
				guiMode = ReignScores_ClassicGuiModes.None;
				if (authenticateCallback != null)
				{
					authenticateCallback(true, null);
				}
				return;
			}
			errorText = ((errorMessage != null) ? errorMessage : "???");
			if (guiMode == ReignScores_ClassicGuiModes.LoggingIn)
			{
				guiMode = ReignScores_ClassicGuiModes.Login;
			}
			else if (guiMode == ReignScores_ClassicGuiModes.CreatingUser)
			{
				guiMode = ReignScores_ClassicGuiModes.CreateUser;
			}
		}

		private void guiRequestScoresCallback(LeaderboardScore[] scores, bool succeeded, string errorMessage)
		{
			if (succeeded)
			{
				guiScores = scores;
				guiMode = ReignScores_ClassicGuiModes.ShowingScores;
				return;
			}
			guiMode = ReignScores_ClassicGuiModes.None;
			if (guiShowNativeViewDoneCallback != null)
			{
				guiShowNativeViewDoneCallback(false, errorMessage);
			}
		}

		public void ShowNativeScoresPage(string leaderboardID, ShowNativeViewDoneCallbackMethod callback)
		{
			guiMode = ReignScores_ClassicGuiModes.LoadingScores;
			guiShowNativeViewDoneCallback = callback;
			guiLeaderboardID = leaderboardID;
			guiScoreOffset = 0;
			plugin.RequestScores(leaderboardID, guiScoreOffset, TopScoresToListPerPage, guiRequestScoresCallback, this);
		}

		private void guiRequestAchievementsCallback(Achievement[] achievements, bool succeeded, string errorMessage)
		{
			if (succeeded)
			{
				guiAchievements = achievements;
				guiMode = ReignScores_ClassicGuiModes.ShowingAchievements;
				return;
			}
			guiMode = ReignScores_ClassicGuiModes.None;
			if (guiShowNativeViewDoneCallback != null)
			{
				guiShowNativeViewDoneCallback(false, errorMessage);
			}
		}

		public void ShowNativeAchievementsPage(ShowNativeViewDoneCallbackMethod callback)
		{
			guiMode = ReignScores_ClassicGuiModes.LoadingAchievements;
			guiAchievementOffset = 0;
			guiShowNativeViewDoneCallback = callback;
			plugin.RequestAchievements(guiRequestAchievementsCallback, this);
		}

		private void OnGUI()
		{
			if (guiMode == ReignScores_ClassicGuiModes.None)
			{
				return;
			}
			GUI.color = Color.white;
			GUI.matrix = Matrix4x4.identity;
			float num = new Vector2(Screen.width, Screen.height).magnitude / new Vector2(1280f, 720f).magnitude;
			if (BackgroudTexture != null)
			{
				Vector2 vector = MathUtilities.FillView(BackgroudTexture.width, BackgroudTexture.height, Screen.width, Screen.height);
				float left = 0f - Mathf.Max((vector.x - (float)Screen.width) * 0.5f, 0f);
				float top = 0f - Mathf.Max((vector.y - (float)Screen.height) * 0.5f, 0f);
				GUI.DrawTexture(new Rect(left, top, vector.x, vector.y), BackgroudTexture);
			}
			float num2 = 128f * num;
			float num3 = 64f * num;
			float num4 = 256f * num;
			float num5 = 32f * num;
			float num6 = Screen.height / 2;
			if (guiMode == ReignScores_ClassicGuiModes.Login)
			{
				if (!string.IsNullOrEmpty(LoginTitle))
				{
					GUIStyle gUIStyle = new GUIStyle();
					gUIStyle.fontSize = (int)(128f * num);
					gUIStyle.alignment = TextAnchor.MiddleCenter;
					gUIStyle.normal.textColor = Color.white;
					GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height / 4), LoginTitle, gUIStyle);
				}
				GUI.Label(new Rect((float)(Screen.width / 2) - num4 - 10f * num, num6, num4, num5), "Username");
				GUI.Label(new Rect((float)(Screen.width / 2) + 10f * num, num6, num4, num5), "Password");
				num6 += num5;
				userAccount_Name = GUI.TextField(new Rect((float)(Screen.width / 2) - num4 - 10f * num, num6, num4, num5), userAccount_Name);
				userAccount_Pass = GUI.PasswordField(new Rect((float)(Screen.width / 2) + 10f * num, num6, num4, num5), userAccount_Pass, '*');
				num6 += num5 * 2f;
				if (GUI.Button(new Rect((float)(Screen.width / 2) - num2 - 10f * num, num6, num2, num3), "Cancel"))
				{
					errorText = null;
					guiMode = ReignScores_ClassicGuiModes.None;
					if (authenticateCallback != null)
					{
						authenticateCallback(false, "Canceled");
					}
				}
				if (GUI.Button(new Rect((float)(Screen.width / 2) + 10f * num, num6, num2, num3), "Login"))
				{
					errorText = null;
					bool flag = true;
					if (string.IsNullOrEmpty(userAccount_Name))
					{
						flag = false;
						errorText = "Invalid username.";
						Debug.LogError(errorText);
					}
					else if (string.IsNullOrEmpty(userAccount_Pass))
					{
						flag = false;
						errorText = "Invalid user password.";
						Debug.LogError(errorText);
					}
					if (flag)
					{
						guiMode = ReignScores_ClassicGuiModes.LoggingIn;
						plugin.ManualLogin(userAccount_Name, userAccount_Pass, null, this);
					}
				}
				num6 += num3 * 2f;
				if (GUI.Button(new Rect((float)(Screen.width / 2) - num2 - 10f * num, num6, num2 * 2f + 10f * num, num3), "Create New User"))
				{
					guiMode = ReignScores_ClassicGuiModes.CreateUser;
					errorText = null;
				}
			}
			else if (guiMode == ReignScores_ClassicGuiModes.CreateUser)
			{
				if (!string.IsNullOrEmpty(CreateUserTitle))
				{
					GUIStyle gUIStyle2 = new GUIStyle();
					gUIStyle2.fontSize = (int)(128f * num);
					gUIStyle2.alignment = TextAnchor.MiddleCenter;
					gUIStyle2.normal.textColor = Color.white;
					GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height / 4), CreateUserTitle, gUIStyle2);
				}
				float num7 = (10f * num + num4) * -0.5f;
				GUI.Label(new Rect((float)(Screen.width / 2) - num4 - 10f * num + num7, num6, num4, num5), "Username");
				GUI.Label(new Rect((float)(Screen.width / 2) + 10f * num + num7, num6, num4, num5), "Password");
				GUI.Label(new Rect((float)(Screen.width / 2) + 20f * num + num4 + num7, num6, num4, num5), "Confirm Password");
				num6 += num5;
				userAccount_Name = GUI.TextField(new Rect((float)(Screen.width / 2) - num4 - 10f * num + num7, num6, num4, num5), userAccount_Name);
				userAccount_Pass = GUI.PasswordField(new Rect((float)(Screen.width / 2) + 10f * num + num7, num6, num4, num5), userAccount_Pass, '*');
				userAccount_ConfPass = GUI.PasswordField(new Rect((float)(Screen.width / 2) + 20f * num + num4 + num7, num6, num4, num5), userAccount_ConfPass, '*');
				num6 += num5 * 2f;
				if (GUI.Button(new Rect((float)(Screen.width / 2) - num2 - 10f * num, num6, num2, num3), "Cancel"))
				{
					errorText = null;
					guiMode = ReignScores_ClassicGuiModes.None;
					if (authenticateCallback != null)
					{
						authenticateCallback(false, "Canceled");
					}
				}
				if (GUI.Button(new Rect((float)(Screen.width / 2) + 10f * num, num6, num2, num3), "Create"))
				{
					errorText = null;
					bool flag2 = true;
					if (string.IsNullOrEmpty(userAccount_Name))
					{
						flag2 = false;
						errorText = "Invalid username.";
						Debug.LogError(errorText);
					}
					else if (string.IsNullOrEmpty(userAccount_Pass) || string.IsNullOrEmpty(userAccount_ConfPass))
					{
						flag2 = false;
						errorText = "Invalid user password.";
						Debug.LogError(errorText);
					}
					else if (userAccount_Pass != userAccount_ConfPass)
					{
						flag2 = false;
						errorText = "Passwords dont match.";
						Debug.LogError(errorText);
					}
					else if (userAccount_Pass.Length < 6)
					{
						flag2 = false;
						errorText = "Passwords to short.";
						Debug.LogError(errorText);
					}
					if (flag2)
					{
						guiMode = ReignScores_ClassicGuiModes.CreatingUser;
						plugin.ManualCreateUser(userAccount_Name, userAccount_Pass, null, this);
					}
				}
				num6 += num3 * 2f;
				if (GUI.Button(new Rect((float)(Screen.width / 2) - num2 - 10f * num, num6, num2 * 2f + 10f * num, num3), "Login Existing User"))
				{
					guiMode = ReignScores_ClassicGuiModes.Login;
					errorText = null;
				}
			}
			else if (guiMode == ReignScores_ClassicGuiModes.LoggingIn)
			{
				GUIStyle gUIStyle3 = new GUIStyle();
				gUIStyle3.fontSize = (int)(128f * num);
				gUIStyle3.alignment = TextAnchor.MiddleCenter;
				gUIStyle3.normal.textColor = Color.white;
				GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), "Logging In...", gUIStyle3);
			}
			else if (guiMode == ReignScores_ClassicGuiModes.ShowingScores)
			{
				if (TopScoreBoardTexture != null)
				{
					Vector2 vector2 = MathUtilities.FitInView(TopScoreBoardTexture.width, TopScoreBoardTexture.height, Screen.width, Screen.height);
					float num8 = (float)Screen.width * 0.5f - vector2.x * 0.5f;
					float num9 = (float)Screen.height * 0.5f - vector2.y * 0.5f;
					GUI.DrawTexture(new Rect(num8, num9, vector2.x, vector2.y), TopScoreBoardTexture);
					Vector2 mainScale = MathUtilities.ScaleToFitInView(TopScoreBoardTexture.width, TopScoreBoardTexture.height, Screen.width, Screen.height);
					if (Input.GetKeyUp(KeyCode.Escape) || processButton(TopScoreBoardFrame_CloseBox, TopScoreBoardButton_CloseNormal, TopScoreBoardButton_CloseHover, mainScale, num8, num9))
					{
						guiMode = ReignScores_ClassicGuiModes.None;
						if (guiShowNativeViewDoneCallback != null)
						{
							guiShowNativeViewDoneCallback(true, null);
						}
					}
					if (processButton(TopScoreBoardFrame_PrevButton, TopScoreBoardButton_PrevNormal, TopScoreBoardButton_PrevHover, mainScale, num8, num9) && guiScoreOffset != 0)
					{
						guiScoreOffset -= TopScoresToListPerPage;
						if (guiScoreOffset < 0)
						{
							guiScoreOffset = 0;
						}
						plugin.RequestScores(guiLeaderboardID, guiScoreOffset, TopScoresToListPerPage, guiRequestScoresCallback, this);
					}
					if (processButton(TopScoreBoardFrame_NextButton, TopScoreBoardButton_NextNormal, TopScoreBoardButton_NextHover, mainScale, num8, num9) && guiScores.Length == TopScoresToListPerPage)
					{
						guiScoreOffset += TopScoresToListPerPage;
						plugin.RequestScores(guiLeaderboardID, guiScoreOffset, TopScoresToListPerPage, guiRequestScoresCallback, this);
					}
					Rect position = calculateFrame(TopScoreBoardFrame_Usernames, mainScale, num8, num9);
					Rect position2 = calculateFrame(TopScoreBoardFrame_Scores, mainScale, num8, num9);
					if (EnableTestRects)
					{
						GUI.Button(position, "TEST RECT");
						GUI.Button(position2, "TEST RECT");
					}
					GUIStyle gUIStyle4 = new GUIStyle();
					gUIStyle4.fontSize = (int)((float)TopScoreBoardFont_Size * num);
					gUIStyle4.alignment = TextAnchor.LowerLeft;
					gUIStyle4.normal.textColor = TopScoreBoardFont_Color;
					int num10 = 0;
					int num11 = 0;
					LeaderboardScore[] array = guiScores;
					foreach (LeaderboardScore leaderboardScore in array)
					{
						float num12 = position.height / (float)TopScoresToListPerPage;
						GUI.Label(new Rect(position.x, position.y + (float)num10, position.width, num12), leaderboardScore.Username, gUIStyle4);
						num10 += (int)num12;
						num12 = position2.height / (float)TopScoresToListPerPage;
						string scoreValue;
						if (this.ScoreFormatCallback != null)
						{
							this.ScoreFormatCallback(leaderboardScore.Score, out scoreValue);
						}
						else
						{
							scoreValue = leaderboardScore.Score.ToString();
						}
						GUI.Label(new Rect(position2.x, position2.y + (float)num11, position2.width, num12), scoreValue, gUIStyle4);
						num11 += (int)num12;
					}
				}
				else
				{
					errorText = "ReignScores TopScoreBoardTexture MUST be set!";
					Debug.LogError(errorText);
				}
			}
			else if (guiMode == ReignScores_ClassicGuiModes.ShowingAchievements)
			{
				if (AchievementBoardTexture != null)
				{
					Vector2 vector3 = MathUtilities.FitInView(AchievementBoardTexture.width, AchievementBoardTexture.height, Screen.width, Screen.height);
					float num13 = (float)Screen.width * 0.5f - vector3.x * 0.5f;
					float num14 = (float)Screen.height * 0.5f - vector3.y * 0.5f;
					GUI.DrawTexture(new Rect(num13, num14, vector3.x, vector3.y), AchievementBoardTexture);
					Vector2 mainScale2 = MathUtilities.ScaleToFitInView(AchievementBoardTexture.width, AchievementBoardTexture.height, Screen.width, Screen.height);
					if (Input.GetKeyUp(KeyCode.Escape) || processButton(AchievementBoardFrame_CloseBox, AchievementBoardButton_CloseNormal, AchievementBoardButton_CloseHover, mainScale2, num13, num14))
					{
						guiMode = ReignScores_ClassicGuiModes.None;
						if (guiShowNativeViewDoneCallback != null)
						{
							guiShowNativeViewDoneCallback(true, null);
						}
					}
					if (processButton(AchievementBoardFrame_PrevButton, AchievementBoardButton_PrevNormal, AchievementBoardButton_PrevHover, mainScale2, num13, num14) && guiAchievementOffset != 0)
					{
						guiAchievementOffset -= AchievementsToListPerPage;
						if (guiAchievementOffset < 0)
						{
							guiAchievementOffset = 0;
						}
					}
					if (processButton(AchievementBoardFrame_NextButton, AchievementBoardButton_NextNormal, AchievementBoardButton_NextHover, mainScale2, num13, num14) && guiAchievementOffset + AchievementsToListPerPage < guiAchievements.Length)
					{
						guiAchievementOffset += AchievementsToListPerPage;
					}
					Rect position3 = calculateFrame(AchievementBoardFrame_Names, mainScale2, num13, num14);
					Rect position4 = calculateFrame(AchievementBoardFrame_Descs, mainScale2, num13, num14);
					if (EnableTestRects)
					{
						GUI.Button(position3, "TEST RECT");
						GUI.Button(position4, "TEST RECT");
					}
					GUIStyle gUIStyle5 = new GUIStyle();
					gUIStyle5.fontSize = (int)((float)AchievementBoardFont_Size * num);
					gUIStyle5.alignment = TextAnchor.LowerLeft;
					gUIStyle5.normal.textColor = AchievementBoardFont_Color;
					int num15 = 0;
					int num16 = 0;
					for (int j = guiAchievementOffset; j < guiAchievementOffset + AchievementsToListPerPage && j != guiAchievements.Length; j++)
					{
						Achievement achievement = guiAchievements[j];
						float num17 = position3.height / (float)AchievementsToListPerPage;
						float num18 = num17 * 0.8f;
						GUI.DrawTexture(new Rect(position3.x, position3.y + (float)num15 + num17 - num18, num18, num18), achievement.IsAchieved ? achievement.AchievedImage : achievement.UnachievedImage);
						GUI.Label(new Rect(num17 + position3.x, position3.y + (float)num15, position3.width, num17), achievement.Name, gUIStyle5);
						num15 += (int)num17;
						num17 = position4.height / (float)AchievementsToListPerPage;
						GUI.Label(new Rect(position4.x, position4.y + (float)num16, position4.width, num17), achievement.Desc, gUIStyle5);
						num16 += (int)num17;
					}
				}
				else
				{
					errorText = "ReignScores AchievementBoardTexture MUST be set!";
					Debug.LogError(errorText);
				}
			}
			else if (guiMode == ReignScores_ClassicGuiModes.CreatingUser)
			{
				GUIStyle gUIStyle6 = new GUIStyle();
				gUIStyle6.fontSize = (int)(128f * num);
				gUIStyle6.alignment = TextAnchor.MiddleCenter;
				gUIStyle6.normal.textColor = Color.white;
				GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), "Creating User...", gUIStyle6);
			}
			else if (guiMode == ReignScores_ClassicGuiModes.LoadingScores || guiMode == ReignScores_ClassicGuiModes.LoadingAchievements)
			{
				GUIStyle gUIStyle7 = new GUIStyle();
				gUIStyle7.fontSize = (int)(128f * num);
				gUIStyle7.alignment = TextAnchor.MiddleCenter;
				gUIStyle7.normal.textColor = Color.white;
				GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), "Loading...", gUIStyle7);
			}
			if (!string.IsNullOrEmpty(errorText))
			{
				GUIStyle gUIStyle8 = new GUIStyle();
				gUIStyle8.fontSize = (int)(32f * num);
				gUIStyle8.alignment = TextAnchor.MiddleCenter;
				gUIStyle8.normal.textColor = Color.red;
				GUI.Label(new Rect(0f, Screen.height - Screen.height / 8, Screen.width, Screen.height / 8), errorText, gUIStyle8);
			}
		}

		private Rect calculateFrame(Rect frame, Vector2 mainScale, float offsetX, float offsetY)
		{
			Rect result = frame;
			result.x = result.x / mainScale.x + offsetX;
			result.y = result.y / mainScale.y + offsetY;
			result.width /= mainScale.x;
			result.height /= mainScale.y;
			return result;
		}

		private bool processButton(Rect frame, Texture normal, Texture hover, Vector2 mainScale, float offsetX, float offsetY)
		{
			Rect position = calculateFrame(frame, mainScale, offsetX, offsetY);
			GUIStyle style = new GUIStyle();
			bool flag = ((!(normal != null)) ? GUI.Button(position, "???") : GUI.Button(position, normal, style));
			if (hover != null)
			{
				Vector3 mousePosition = Input.mousePosition;
				mousePosition.y = (float)Screen.height - mousePosition.y;
				if (mousePosition.x > position.xMin && mousePosition.x < position.xMax && mousePosition.y > position.yMin && mousePosition.y < position.yMax)
				{
					GUI.DrawTexture(position, hover);
				}
			}
			if (flag)
			{
				if (AudioSource != null && ButtonClick != null)
				{
					AudioSource.PlayOneShot(ButtonClick);
				}
				return true;
			}
			return false;
		}

		public ReignScores_ClassicGUI()
		{
			LoginTitle = "Login";
			CreateUserTitle = "Create Account";
			TopScoreBoardFrame_Usernames = new Rect(225f, 250f, 340f, 560f);
			TopScoreBoardFrame_Scores = new Rect(585f, 250f, 240f, 560f);
			TopScoreBoardFrame_PrevButton = new Rect(150f, 750f, 256f, 256f);
			TopScoreBoardFrame_NextButton = new Rect(650f, 750f, 256f, 256f);
			TopScoreBoardFrame_CloseBox = new Rect(750f, 50f, 128f, 128f);
			AchievementBoardFrame_Names = new Rect(225f, 250f, 270f, 560f);
			AchievementBoardFrame_Descs = new Rect(520f, 250f, 300f, 560f);
			AchievementBoardFrame_PrevButton = new Rect(150f, 750f, 256f, 256f);
			AchievementBoardFrame_NextButton = new Rect(650f, 750f, 256f, 256f);
			AchievementBoardFrame_CloseBox = new Rect(750f, 50f, 128f, 128f);
			TopScoreBoardFont_Size = 12;
			AchievementBoardFont_Size = 12;
			TopScoreBoardFont_Color = Color.white;
			AchievementBoardFont_Color = Color.white;
			TopScoresToListPerPage = 10;
			AchievementsToListPerPage = 10;
			userAccount_Name = "";
			userAccount_Pass = "";
			userAccount_ConfPass = "";

		}




	}
}
