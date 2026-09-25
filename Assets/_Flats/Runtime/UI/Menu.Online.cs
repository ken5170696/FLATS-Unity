using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ExitGames.Client.Photon;
using InControl;
using Reign;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

// Leaderboard, information feed and advertising callbacks.
public partial class Menu
{
		private IEnumerator Leaderboard(bool upload)
		{
			foreach (Transform item in leaderboardScroll.GetChild(0))
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
			if (myCharacter != null)
			{
				if (myCharacter.name == "" || myCharacter.name == null)
				{
					myCharacter.name = "No Name";
				}
				float kd = myCharacter.death == 0 ? myCharacter.kill : (float)myCharacter.kill / myCharacter.death;
				int average = (int)(((long)myCharacter.survivalScore + myCharacter.assortmentScore + myCharacter.headshotScore) / 3);
				int num = (int)Math.Min(int.MaxValue, Math.Max(0d, Math.Round(((double)kd + 1d) * average / 2d)));
                totalScore = num.ToString();
                leaderboardScreen.GetChild(0).GetChild(3).GetComponent<Text>().text = totalScore;
				string shortText = myCharacter.name + "$" + myCharacter.kill + "$" + myCharacter.death + "$" + myCharacter.survivalScore + "$" + myCharacter.assortmentScore + "$" + myCharacter.headshotScore;
				dl.AddScore("user-" + myCharacter.id, num, 0, shortText);
			}
			dl.LoadScores();
            uploadButton.SetActive(true);
            var uploadLabel = uploadButton.GetComponentInChildren<Text>(true);
            if (uploadLabel != null) uploadLabel.text = "Upload (unavailable)";
            foreach (var label in leaderboardScreen.GetComponentsInChildren<Text>(true))
                if (label.text == "Leaderboard") label.text = "Leaderboard (local)";
            if (upload) ShowConfirm("Scores are local only", "No scores were uploaded. The original cloud service is unavailable. This leaderboard shows scores stored on this device. Use save files or LAN Sync to transfer scores.", null, "OK", null);
			List<dreamloLeaderBoard.Score> playerList = new List<dreamloLeaderBoard.Score>();
			int maxToDisplay = 20;
			int count = 0;
			yield return null;
			playerList = dl.ToListHighToLow();
			foreach (dreamloLeaderBoard.Score item2 in playerList)
			{
				GameObject gameObject = (GameObject)UnityEngine.Object.Instantiate(leaderboardContent);
				gameObject.transform.SetParent(leaderboardScroll.GetChild(0), false);
				Text component = gameObject.transform.GetChild(0).GetComponent<Text>();
				Text component2 = gameObject.transform.GetChild(1).GetComponent<Text>();
				Text component3 = gameObject.transform.GetChild(2).GetComponent<Text>();
				if (item2.shortText != "")
				{
					count++;
					component.text = count.ToString();
					string[] array = item2.shortText.Split(new string[1] { "$" }, StringSplitOptions.None);
					component2.text = array[0].Replace("+", " ");
					int score2 = item2.score;
					component3.text = score2.ToString();
				}
				if (count >= maxToDisplay || count >= playerList.Count)
				{
					break;
				}
			}
			leaderboardLoading.SetActive(false);
		}

        private IEnumerator Information()
        {
            purchaseButton.text = "Store purchases unavailable";
#if UNITY_WEBGL && !UNITY_EDITOR
            news.text = "FLATS Web\nOnline play uses the site's configured Photon service.\nBuilt-in crosshair settings and data presets are supported.\nDownloaded DLL mods require desktop Mono.\nBrowser saves may be cleared by the browser.\n\nOriginal game: © Foliage Games LLC";
#else
            news.text = "FLATS reconstruction\nScores and settings are saved on this device.\nOnline multiplayer uses your configured Photon app.\nStore purchase verification is unavailable.\n\nOriginal game: © Foliage Games LLC";
#endif
            yield break;
        }

		private void imageLoadedCallback(Stream stream, bool succeeded)
		{
			if (!succeeded)
			{
				if (stream != null)
				{
					stream.Dispose();
				}
				return;
			}
			try
			{
				byte[] array = new byte[stream.Length];
				stream.Read(array, 0, array.Length);
				Texture2D texture2D = new Texture2D(128, 128);
				texture2D.LoadImage(array);
				texture2D.Apply();
				if (texture2D.width < 128 || texture2D.height < 128)
				{
					ShowConfirm("Image size error", "Image scale must be larger than 128x128.", null, "OK", null);
					return;
				}
				array = texture2D.EncodeToPNG();
				System.IO.File.WriteAllBytes((FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath) + "/Flats_UserIcon.png", array);
				characterScreen.GetChild(0).GetChild(0)
					.GetChild(0)
					.GetComponent<Image>()
					.sprite = Sprite.Create(texture2D, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f));
			}
			catch (Exception ex)
			{
				Debug.Log(ex.Message);
			}
			finally
			{
				if (stream != null)
				{
					stream.Dispose();
				}
			}
		}

		public void ReadyForAd()
		{ /* Advertising disabled in the standalone offline recovery. */ }

		private void adCreatedCallback(bool succeeded)
		{
			if (succeeded && current == "Result")
			{
				adForWin.Visible = true;
			}
		}

		private static void eventCallback(AdEvents adEvent, string eventMessage)
		{
		}
}
