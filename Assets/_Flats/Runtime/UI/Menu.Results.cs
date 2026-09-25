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

// Match results and the return to the menu after a game.
public partial class Menu
{
		private IEnumerator GameOver()
		{
			if (gameState == "Singleplayer" || (gameState == "Multiplayer" && Multiplayer.rule == 8))
			{
				GameObject[] array = GameObject.FindGameObjectsWithTag("Enemy");
				GameObject[] array2 = array;
				foreach (GameObject gameObject in array2)
				{
					if ((bool)gameObject.GetComponent<AI>())
					{
						gameObject.GetComponent<AI>().StopAllCoroutines();
					}
				}
			}
			canOpen = false;
			EasyTouch.SetEnabled(false);
			stick.transform.parent.gameObject.SetActive(false);
			Text phaseText = GameObject.Find("Message").transform.GetChild(0).GetComponent<Text>();
			phaseText.enabled = true;
			phaseText.text = "Game Over";
			if (gameState == "Singleplayer")
			{
				if (Singleplayer.rule == 0)
				{
					myCurrent.survival_Score = 0;
					myCurrent.survival_Phase = 0;
				}
				else if (Singleplayer.rule == 1)
				{
					myCurrent.assortment_Score = 0;
					myCurrent.assortment_Phase = 0;
				}
				else if (Singleplayer.rule == 2)
				{
					myCurrent.headshot_Score = 0;
					myCurrent.headshot_Chain = 0;
				}
				SaveDataController.Save();
			}
			yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(1.5f));
			yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(1f));
			Time.timeScale = 0f;
			if (gameState == "Multiplayer" && Multiplayer.rule != 8)
			{
				List<GameObject> list = new List<GameObject>();
				if (network == 1)
				{
					foreach (PlayerInfo localNetworkPlayer in localNetworkPlayerList)
					{
						GameObject gameObject2 = (GameObject)UnityEngine.Object.Instantiate(result);
						if (Multiplayer.rule == 1 || Multiplayer.rule == 6)
						{
							int color = localNetworkPlayer.color;
							gameObject2.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(color)
								.GetComponent<Image>()
								.color;
						}
						else if (localNetworkPlayer.team == "red")
						{
							gameObject2.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(9)
								.GetComponent<Image>()
								.color;
						}
						else
						{
							gameObject2.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(7)
								.GetComponent<Image>()
								.color;
						}
						gameObject2.transform.GetChild(1).GetComponent<Text>().text = localNetworkPlayer.name;
						int kill = localNetworkPlayer.kill;
						int death = localNetworkPlayer.death;
						gameObject2.transform.GetChild(2).GetComponent<Text>().text = kill.ToString();
						gameObject2.transform.GetChild(3).GetComponent<Text>().text = death.ToString();
						if (Multiplayer.rule >= 3)
						{
							gameObject2.transform.GetChild(4).GetComponent<Text>().text = "--";
						}
						else if (death == 0)
						{
							gameObject2.transform.GetChild(4).GetComponent<Text>().text = ((float)kill).ToString("F2");
						}
						else
						{
							gameObject2.transform.GetChild(4).GetComponent<Text>().text = ((float)kill / (float)death).ToString("F2");
						}
						gameObject2.transform.SetParent(multiplayerResultList, false);
						list.Add(gameObject2);
					}
				}
				else
				{
					PhotonPlayer[] playerList = PhotonNetwork.playerList;
					foreach (PhotonPlayer photonPlayer in playerList)
					{
						GameObject gameObject3 = (GameObject)UnityEngine.Object.Instantiate(result);
						if (Multiplayer.rule == 1 || Multiplayer.rule == 6)
						{
							int index = (int)photonPlayer.CustomProperties["TC"];
							gameObject3.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(index)
								.GetComponent<Image>()
								.color;
						}
						else if (photonPlayer.GetTeam() == PunTeams.Team.red)
						{
							gameObject3.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(9)
								.GetComponent<Image>()
								.color;
						}
						else
						{
							gameObject3.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(7)
								.GetComponent<Image>()
								.color;
						}
						gameObject3.transform.GetChild(1).GetComponent<Text>().text = photonPlayer.NickName;
						int num = (int)photonPlayer.CustomProperties["K"];
						int num2 = (int)photonPlayer.CustomProperties["D"];
						gameObject3.transform.GetChild(2).GetComponent<Text>().text = num.ToString();
						gameObject3.transform.GetChild(3).GetComponent<Text>().text = num2.ToString();
						if (Multiplayer.rule >= 3)
						{
							gameObject3.transform.GetChild(4).GetComponent<Text>().text = "--";
						}
						else if (num2 == 0)
						{
							gameObject3.transform.GetChild(4).GetComponent<Text>().text = ((float)num).ToString("F2");
						}
						else
						{
							gameObject3.transform.GetChild(4).GetComponent<Text>().text = ((float)num / (float)num2).ToString("F2");
						}
						gameObject3.transform.SetParent(multiplayerResultList, false);
						list.Add(gameObject3);
						if (Multiplayer.rule <= 2 && photonPlayer.IsLocal)
						{
							myCharacter.kill += num;
							myCharacter.death += num2;
							SaveDataController.Save();
						}
					}
				}
				if (Multiplayer.rule < 3)
				{
					if (PhotonNetwork.offlineMode)
						foreach (var botScore in FlatsOfflineScores.Bots)
						{
							var row=(GameObject)UnityEngine.Object.Instantiate(result);
							row.GetComponent<Image>().color=characterScreen.GetChild(1).GetChild(botScore.team==0?9:7).GetComponent<Image>().color;
							row.transform.GetChild(1).GetComponent<Text>().text=botScore.name;
							row.transform.GetChild(2).GetComponent<Text>().text=botScore.kills.ToString();
							row.transform.GetChild(3).GetComponent<Text>().text=botScore.deaths.ToString();
							row.transform.GetChild(4).GetComponent<Text>().text=((float)botScore.kills/Mathf.Max(1,botScore.deaths)).ToString("F2");
							row.transform.SetParent(multiplayerResultList,false);list.Add(row);
						}
					list.Sort((GameObject x, GameObject y) => float.Parse(y.transform.GetChild(2).GetComponent<Text>().text).CompareTo(float.Parse(x.transform.GetChild(2).GetComponent<Text>().text)));
				}
				int rs = Multiplayer.redTeamScore;
				int bs = Multiplayer.blueTeamScore;
				if (Multiplayer.rule != 1 && Multiplayer.rule != 6)
				{
					if (rs >= bs)
					{
						for (int num3 = list.Count - 1; num3 > -1; num3--)
						{
							if (list[num3].GetComponent<Image>().color == characterScreen.GetChild(1).GetChild(9)
								.GetComponent<Image>()
								.color)
							{
								list[num3].transform.SetAsFirstSibling();
							}
						}
					}
					else
					{
						for (int num4 = list.Count - 1; num4 > -1; num4--)
						{
							if (list[num4].GetComponent<Image>().color == characterScreen.GetChild(1).GetChild(7)
								.GetComponent<Image>()
								.color)
							{
								list[num4].transform.SetAsFirstSibling();
							}
						}
					}
					if (rs > bs)
					{
						resultIndex.text = "Winner:Red Team";
					}
					else if (bs > rs)
					{
						resultIndex.text = "Winner:Blue Team";
					}
					else
					{
						resultIndex.text = "Draw";
					}
				}
				else if (rule == 6)
				{
					for (int num5 = list.Count - 1; num5 > -1; num5--)
					{
						list[num5].transform.SetAsFirstSibling();
					}
					resultIndex.text = "Result";
					if (Multiplayer.rule == 6)
					{
						if (rs > bs)
						{
							resultIndex.text = "Winner:Survivors";
						}
						else if (bs > rs)
						{
							resultIndex.text = "Winner:Zombies";
						}
						else
						{
							resultIndex.text = "Draw";
						}
					}
				}
				else
				{
					for (int num6 = list.Count - 1; num6 > -1; num6--)
					{
						list[num6].transform.SetAsFirstSibling();
					}
				}
				for (int num7 = 0; num7 < multiplayerResultList.childCount; num7++)
				{
					multiplayerResultList.GetChild(num7).GetChild(0).GetComponent<Text>()
						.text = (num7 + 1).ToString();
				}
				if (Multiplayer.rule != 1 && Multiplayer.rule != 6)
				{
					GameObject gameObject4 = (GameObject)UnityEngine.Object.Instantiate(result);
					gameObject4.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(9)
						.GetComponent<Image>()
						.color;
					for (int num8 = 0; num8 < gameObject4.transform.childCount; num8++)
					{
						switch (num8)
						{
						case 1:
							gameObject4.transform.GetChild(num8).GetComponent<Text>().text = "Red Team";
							break;
						case 4:
							gameObject4.transform.GetChild(num8).GetComponent<Text>().text = rs.ToString();
							break;
						default:
							gameObject4.transform.GetChild(num8).GetComponent<Text>().text = "";
							break;
						}
					}
					GameObject gameObject5 = (GameObject)UnityEngine.Object.Instantiate(result);
					gameObject5.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(7)
						.GetComponent<Image>()
						.color;
					for (int num9 = 0; num9 < gameObject5.transform.childCount; num9++)
					{
						switch (num9)
						{
						case 1:
							gameObject5.transform.GetChild(num9).GetComponent<Text>().text = "Blue Team";
							break;
						case 4:
							gameObject5.transform.GetChild(num9).GetComponent<Text>().text = bs.ToString();
							break;
						default:
							gameObject5.transform.GetChild(num9).GetComponent<Text>().text = "";
							break;
						}
					}
					gameObject4.transform.SetParent(multiplayerResultList, false);
					gameObject5.transform.SetParent(multiplayerResultList, false);
					if (rs >= bs)
					{
						gameObject4.transform.SetAsFirstSibling();
						int siblingIndex = 0;
						for (int num10 = 0; num10 < multiplayerResultList.childCount; num10++)
						{
							if (multiplayerResultList.GetChild(num10).GetComponent<Image>().color == characterScreen.GetChild(1).GetChild(7)
								.GetComponent<Image>()
								.color)
							{
								siblingIndex = num10;
								break;
							}
						}
						gameObject5.transform.SetSiblingIndex(siblingIndex);
					}
					else
					{
						gameObject5.transform.SetAsFirstSibling();
						int siblingIndex2 = 0;
						for (int num11 = 0; num11 < multiplayerResultList.childCount; num11++)
						{
							if (multiplayerResultList.GetChild(num11).GetComponent<Image>().color == characterScreen.GetChild(1).GetChild(9)
								.GetComponent<Image>()
								.color)
							{
								siblingIndex2 = num11;
								break;
							}
						}
						gameObject4.transform.SetSiblingIndex(siblingIndex2);
					}
				}
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(2f));
				if (!Application.isMobilePlatform && Input.mousePresent)
				{
					Screen.lockCursor = false;
					UnityEngine.Cursor.visible = true;
				}
				AudioSource[] sources = ambient.GetComponents<AudioSource>();
				AudioSource[] array3 = sources;
				foreach (AudioSource audioSource in array3)
				{
					audioSource.Stop();
				}
				phaseText.text = "";
				phaseText.enabled = false;
				anim.Play("Multiplayer Result");
				current = "Result";
				canOpen = true;
				skipTitle = true;
				backButton.SetActive(true);
			}
			else if (gameState == "Singleplayer" || Multiplayer.rule == 8)
			{
				int myScore = 0;
				if (Singleplayer.rule == 0 || Multiplayer.rule == 8)
				{
					resultIndex.text = "Result";
					singleplayerResult.GetChild(0).GetComponent<Text>().text = "Score: " + currentSurvivalScore + "\nDied at Phase " + currentSurvivalPhase;
					myScore = currentSurvivalScore;
				}
				else if (Singleplayer.rule == 1)
				{
					resultIndex.text = "Result";
					singleplayerResult.GetChild(0).GetComponent<Text>().text = "Score: " + currentAssortmentScore + "\nDied at Phase " + currentAssortmentPhase;
					myScore = currentAssortmentScore;
				}
				else if (Singleplayer.rule == 2)
				{
					resultIndex.text = "Result";
					singleplayerResult.GetChild(0).GetComponent<Text>().text = "Score: " + currentHeadshotScore + "\nMax Headshot Chain: " + currentHeadshotChain;
					myScore = currentHeadshotScore;
				}
				string comment = ((myScore < 5000) ? "Beginner" : ((myScore < 10000) ? "Good Shooter" : ((myScore < 20000) ? "Survivor" : ((myScore < 50000) ? "Tough Guy" : ((myScore < 80000) ? "Gun Devil" : ((myScore < 100000) ? "Ninja" : ((myScore < 150000) ? "Crazy Killer" : ((myScore < 200000) ? "FPS Zombie" : ((myScore >= 300000) ? "Day Dreamer" : "Fribbler")))))))));
				string highscored = "";
				if (Singleplayer.rule == 0 && myScore > myCharacter.survivalScore)
				{
					myCharacter.survivalScore = myScore;
					highscored = "Highscore!!\n\n";
				}
				else if (Singleplayer.rule == 1 && myScore > myCharacter.assortmentScore)
				{
					myCharacter.assortmentScore = myScore;
					highscored = "Highscore!!\n\n";
				}
				else if (Singleplayer.rule == 2 && myScore > myCharacter.headshotScore)
				{
					myCharacter.headshotScore = myScore;
					highscored = "Highscore!!\n\n";
				}
				if (highscored != "")
				{
					SaveDataController.Save();
				}
				singleplayerResult.GetChild(1).GetComponent<Text>().text = highscored + "Your level is...\n" + comment;
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(2f));
				StartCoroutine("BackgroundColor", "OpenMenu");
				if (!Application.isMobilePlatform && Input.mousePresent)
				{
					Screen.lockCursor = false;
					UnityEngine.Cursor.visible = true;
				}
				AudioSource[] sources2 = ambient.GetComponents<AudioSource>();
				AudioSource[] array4 = sources2;
				foreach (AudioSource audioSource2 in array4)
				{
					audioSource2.Stop();
				}
				phaseText.text = "";
				phaseText.enabled = false;
				anim.Play("Singleplayer Result");
				current = "Result";
				canOpen = true;
				skipTitle = true;
				backButton.SetActive(true);
			}
			if (VRmode)
			{
				Debug.Log("VR dead.");
				base.transform.parent.GetChild(6).gameObject.SetActive(false);
				Time.timeScale = 0f;
				Vector3 deadCamPos = Camera.main.transform.position + Vector3.up * 4f;
				Camera.main.gameObject.SetActive(false);
				GameObject deadVrCam = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("DeadVRCamera"));
				deadVrCam.transform.position = deadCamPos;
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(0.1f));
				mt.parent.localPosition = deadVrCam.transform.position + deadVrCam.transform.forward * 2.1f;
				mt.parent.eulerAngles = new Vector3(deadVrCam.transform.eulerAngles.x, deadVrCam.transform.eulerAngles.y, 0f);
				mt.GetComponent<Canvas>().worldCamera = deadVrCam.GetComponent<Camera>();
			}
			if (!adFree && VRController.device == "cardboard")
			{
				ReadyForAd();
			}
		}
}
