using System;
using System.Collections.Generic;
using UnityEngine;
public class LeaderBoardSample : MonoBehaviour
{
	private enum gameState
	{
		waiting,
		running,
		enterscore,
		leaderboard
	}

	private float startTime;

	private float timeLeft;

	private int totalScore;

	private string playerName;

	private string code;

	private gameState gs;

	private dreamloLeaderBoard dl;

	private dreamloPromoCode pc;

	private void Start()
	{
		dl = dreamloLeaderBoard.GetSceneDreamloLeaderboard();
		pc = dreamloPromoCode.GetSceneDreamloPromoCode();
		timeLeft = startTime;
		gs = gameState.waiting;
	}

	private void Update()
	{
		if (gs == gameState.running)
		{
			timeLeft = Mathf.Clamp(timeLeft - Time.deltaTime, 0f, startTime);
			if (timeLeft == 0f)
			{
				gs = gameState.enterscore;
			}
		}
	}

	private void OnGUI()
	{
		GUILayoutOption[] options = new GUILayoutOption[1] { GUILayout.Width(200f) };
		float num = 400f;
		float num2 = 200f;
		Rect screenRect = new Rect((float)(Screen.width / 2) - num / 2f, (float)(Screen.height / 2) - num2, num, num2);
		GUILayout.BeginArea(screenRect, new GUIStyle("box"));
		GUILayout.BeginVertical();
		GUILayout.Label("Time Left:" + timeLeft.ToString("0.000"));
		if (gs == gameState.waiting || gs == gameState.running)
		{
			if (GUILayout.Button("Click me as much as you can in " + startTime.ToString("0") + " seconds!"))
			{
				totalScore++;
				gs = gameState.running;
			}
			GUILayout.Label("Total Score: " + totalScore);
		}
		if (gs == gameState.enterscore)
		{
			GUILayout.Label("Total Score: " + totalScore);
			GUILayout.BeginHorizontal();
			GUILayout.Label("Your Name: ");
			playerName = GUILayout.TextField(playerName, options);
			if (GUILayout.Button("Save Score"))
			{
				if (dl.publicCode == "")
				{
					Debug.LogError("You forgot to set the publicCode variable");
				}
				if (dl.privateCode == "")
				{
					Debug.LogError("You forgot to set the privateCode variable");
				}
				dl.AddScore(playerName, totalScore);
				gs = gameState.leaderboard;
			}
			GUILayout.EndHorizontal();
		}
		if (gs == gameState.leaderboard)
		{
			GUILayout.Label("High Scores:");
			List<dreamloLeaderBoard.Score> list = dl.ToListHighToLow();
			if (list == null)
			{
				GUILayout.Label("(loading...)");
			}
			else
			{
				int num3 = 20;
				int num4 = 0;
				foreach (dreamloLeaderBoard.Score item in list)
				{
					num4++;
					GUILayout.BeginHorizontal();
					GUILayout.Label(item.playerName, options);
					int score = item.score;
					GUILayout.Label(score.ToString(), options);
					GUILayout.EndHorizontal();
					if (num4 >= num3)
					{
						break;
					}
				}
			}
		}
		GUILayout.EndArea();
		screenRect.y = screenRect.y + screenRect.height + 20f;
		GUILayout.BeginArea(screenRect, new GUIStyle("box"));
		GUILayout.BeginHorizontal();
		GUILayout.Label("Redeem Code: ");
		code = GUILayout.TextField(code, options);
		if (GUILayout.Button("Redeem"))
		{
			pc.RedeemCode(code);
		}
		GUILayout.EndHorizontal();
		GUILayout.Space(50f);
		if (pc != null)
		{
			GUILayout.Label("State: " + pc.state);
			GUILayout.Label("Error: " + pc.error);
			GUILayout.Label("Value: " + pc.value);
		}
		GUILayout.EndVertical();
		GUILayout.EndArea();
	}

	public LeaderBoardSample()
	{
		startTime = 10f;
		playerName = "";
		code = "";

	}




}
