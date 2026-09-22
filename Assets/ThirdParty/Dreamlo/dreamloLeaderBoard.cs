using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
public class dreamloLeaderBoard : MonoBehaviour
{
	[Serializable]
	public struct Score
	{
		public string playerName;

		public int score;

		public int seconds;

		public string shortText;

		public string dateString;
	}

	private string dreamloWebserviceURL;

	public string privateCode;

	public string publicCode;

	private string highScores;

	private void Start()
	{
		ReadLocalScores();
	}

	public void OverWriteScore(string playerName, int totalScore, int totalSeconds, string shortText)
	{
		StartCoroutine(OverWriteScoreWithPipe(playerName, totalScore, totalSeconds, shortText));
	}

	public IEnumerator OverWriteScoreWithPipe(string playerName, int totalScore, int totalSeconds, string shortText)
	{ StoreLocalScore(playerName, totalScore, totalSeconds, shortText, true); yield break; }

	public static dreamloLeaderBoard GetSceneDreamloLeaderboard()
	{
		GameObject gameObject = GameObject.Find("dreamloPrefab");
		if (gameObject == null)
		{
			Debug.LogError("Could not find dreamloPrefab in the scene.");
			return null;
		}
		return gameObject.GetComponent<dreamloLeaderBoard>();
	}

	public void AddScore(string playerName, int totalScore)
	{
		StartCoroutine(AddScoreWithPipe(playerName, totalScore));
	}

	public void AddScore(string playerName, int totalScore, int totalSeconds)
	{
		StartCoroutine(AddScoreWithPipe(playerName, totalScore, totalSeconds));
	}

	public void AddScore(string playerName, int totalScore, int totalSeconds, string shortText)
	{
		StartCoroutine(AddScoreWithPipe(playerName, totalScore, totalSeconds, shortText));
	}

	private IEnumerator AddScoreWithPipe(string playerName, int totalScore)
	{ StoreLocalScore(playerName, totalScore, 0, "", false); yield break; }

	private IEnumerator AddScoreWithPipe(string playerName, int totalScore, int totalSeconds)
	{ StoreLocalScore(playerName, totalScore, totalSeconds, "", false); yield break; }

	private IEnumerator AddScoreWithPipe(string playerName, int totalScore, int totalSeconds, string shortText)
	{ StoreLocalScore(playerName, totalScore, totalSeconds, shortText, false); yield break; }

	private IEnumerator GetScores()
	{ ReadLocalScores(); yield break; }

	private IEnumerator GetSingleScore(string playerName)
	{ ReadLocalScores(); var all = ToListHighToLow(); highScores = ""; foreach (var item in all) if (item.playerName == Clean(playerName)) highScores += Format(item); yield break; }

	public void LoadScores()
	{
		StartCoroutine(GetScores());
	}

	public string[] ToStringArray()
	{
		if (highScores == null)
		{
			return null;
		}
		if (highScores == "")
		{
			return null;
		}
		return highScores.Split(new char[1] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
	}

	public List<Score> ToListLowToHigh()
	{
		Score[] array = ToScoreArray();
		if (array == null)
		{
			return new List<Score>();
		}
		List<Score> list = new List<Score>(array);
		list.Sort((Score x, Score y) => x.score.CompareTo(y.score));
		return list;
	}

	public List<Score> ToListHighToLow()
	{
		Score[] array = ToScoreArray();
		if (array == null)
		{
			return new List<Score>();
		}
		List<Score> list = new List<Score>(array);
		list.Sort((Score x, Score y) => y.score.CompareTo(x.score));
		return list;
	}

	public Score[] ToScoreArray()
	{
		string[] array = ToStringArray();
		if (array == null)
		{
			return null;
		}
		int num = array.Length;
		if (num <= 0)
		{
			return null;
		}
		Score[] array2 = new Score[num];
		for (int i = 0; i < num; i++)
		{
			string[] array3 = array[i].Split(new char[1] { '|' }, StringSplitOptions.None);
			Score score = new Score
			{
				playerName = array3[0],
				score = 0,
				seconds = 0,
				shortText = "",
				dateString = ""
			};
			if (array3.Length > 1)
			{
				score.score = CheckInt(array3[1]);
			}
			if (array3.Length > 2)
			{
				score.seconds = CheckInt(array3[2]);
			}
			if (array3.Length > 3)
			{
				score.shortText = array3[3];
			}
			if (array3.Length > 4)
			{
				score.dateString = array3[4];
			}
			array2[i] = score;
		}
		return array2;
	}

    [Serializable] private class LocalTable { public List<Score> scores = new List<Score>(); }
    private const string LocalKey = "Flats.OfflineLeaderboard.v1";
    private LocalTable ReadLocalScores()
    {
        LocalTable table = new LocalTable();
        string json = PlayerPrefs.GetString(LocalKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { table = JsonUtility.FromJson<LocalTable>(json) ?? new LocalTable(); }
            catch (ArgumentException) { PlayerPrefs.SetString(LocalKey+".CorruptBackup."+DateTime.UtcNow.Ticks,json); PlayerPrefs.Save(); Debug.LogWarning("Local leaderboard could not be read; original JSON retained in a recovery key."); }
        }
        if (table.scores == null) table.scores = new List<Score>();
        highScores = "";
        foreach (var item in table.scores) highScores += Format(item);
        return table;
    }
    private string Format(Score item)
    {
        return Clean(item.playerName) + "|" + item.score + "|" + item.seconds + "|" + Clean(item.shortText) + "|" + item.dateString + "\n";
    }
    private void StoreLocalScore(string playerName, int totalScore, int totalSeconds, string shortText, bool overwrite)
    {
        var table = ReadLocalScores();
        playerName = Clean(playerName);
        int index = table.scores.FindIndex(item => item.playerName == playerName);
        if (index >= 0 && !overwrite && table.scores[index].score > totalScore) return;
        var score = new Score { playerName = playerName, score = totalScore, seconds = totalSeconds,
            shortText = Clean(shortText), dateString = DateTime.UtcNow.ToString("o") };
        if (index >= 0) table.scores[index] = score; else table.scores.Add(score);
        table.scores.Sort((a,b) => b.score.CompareTo(a.score));
        if (table.scores.Count > 100) table.scores.RemoveRange(100, table.scores.Count - 100);
        PlayerPrefs.SetString(LocalKey, JsonUtility.ToJson(table));
        PlayerPrefs.Save();
        ReadLocalScores();
    }

	private string Clean(string s)
	{
		s = (s ?? "").Replace("\r", "").Replace("\n", "");
		s = s.Replace("/", "");
		s = s.Replace("|", "");
		return s;
	}

	private int CheckInt(string s)
	{
		int result = 0;
		int.TryParse(s, out result);
		return result;
	}

	public dreamloLeaderBoard()
	{
		dreamloWebserviceURL = "http://dreamlo.com/lb/";
		privateCode = "";
		publicCode = "";
		highScores = "";

	}

	[CompilerGenerated]
	private static int _003CToListLowToHigh_003Eb__12(Score x, Score y)
	{
		return x.score.CompareTo(y.score);
	}

	[CompilerGenerated]
	private static int _003CToListHighToLow_003Eb__14(Score x, Score y)
	{
		return y.score.CompareTo(x.score);
	}




}
