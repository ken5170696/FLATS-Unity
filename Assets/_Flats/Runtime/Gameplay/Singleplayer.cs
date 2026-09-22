using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
public class Singleplayer : MonoBehaviour
{
	public static int rule = 0;

	public static int enemy = 0;

	public static int bot = 0;

	public static int maxChain = 0;

	public static bool chance = false;

	public static bool nextPhaseReady = false;

	public static int respawnEnemy = 10;

	public AudioClip[] singleplayerBGM;

	public AudioClip clearedSE;

	private Transform spawnPoints;

	private Transform phaseSkippers;

	private Transform[] spawnPoint;

	private GameObject[] phaseSkipper;

	private GameObject lastSkipper;

	private Text phaseText;

	private Menu menu;

	private bool loaded;

	private AudioSource normalBGM;

	private AudioSource chanceBGM;

	private Text score;

	private Transform ui;

	private Dictionary<int, string> ruleTitleText;

	private Dictionary<int, string> ruleExpText;

	private int assortmentEnemyCount;

	private int actualAssortmentEnemyCount;

	public static int timeLimit = 0;

	public static int currentAssortmentRule = 0;

	public static int lastRule = 0;

	public static bool cleared = false;

	private GameObject player;

	public static int headshotChain = 0;

	private void Awake()
	{
		currentAssortmentRule = 0;
		headshotChain = 0;
	}

	private IEnumerator Start()
	{
		spawnPoints = GameObject.Find("SpawnPoints").transform;
		enemy = 0;
		bot = 0;
		chance = false;
		nextPhaseReady = false;
		respawnEnemy = assortmentEnemyCount;
		cleared = false;
		if (Menu.gameState == "Singleplayer" || Multiplayer.rule == 8)
		{
			menu = GameObject.Find("Menu").GetComponent<Menu>();
			if (Menu.gameState == "Singleplayer")
			{
				if (rule == 1)
				{
					Transform transform = GameObject.Find("RedTeamBase").transform;
					player = UnityEngine.Object.Instantiate(Resources.Load("Flatman"), transform.position, Quaternion.identity) as GameObject;
				}
				else
				{
					Transform transform2 = GameObject.Find("SpawnPoints").transform;
					UnityEngine.Object.Instantiate(Resources.Load("Flatman"), transform2.GetChild(UnityEngine.Random.Range(0, transform2.childCount)).position, Quaternion.identity);
				}
			}
			else if (Multiplayer.rule == 8)
			{
				Menu.currentSurvivalScore = 0;
				Menu.currentSurvivalPhase = 0;
			}
			GameObject ambient = GameObject.Find("Ambient");
			AudioSource[] aSources = ambient.GetComponents<AudioSource>();
			if (Menu.gameState == "Singleplayer")
			{
				if (rule == 0)
				{
					aSources[0].clip = singleplayerBGM[0];
					aSources[1].clip = singleplayerBGM[1];
				}
				else if (rule == 1)
				{
					aSources[0].clip = singleplayerBGM[2];
					aSources[1].clip = singleplayerBGM[3];
				}
				else if (rule == 2)
				{
					aSources[0].clip = singleplayerBGM[4];
					aSources[1].clip = singleplayerBGM[5];
				}
			}
			normalBGM = aSources[0];
			chanceBGM = aSources[1];
			chanceBGM.volume = 0f;
			AudioSource[] array = aSources;
			foreach (AudioSource audioSource in array)
			{
				audioSource.Play();
			}
			GameObject message = GameObject.Find("Message");
			score = GameObject.Find("Score").GetComponent<Text>();
			ui = score.transform.parent;
			phaseText = message.transform.GetChild(0).GetComponent<Text>();
			Array.Resize(ref spawnPoint, spawnPoints.childCount);
			for (int j = 0; j < spawnPoint.Length; j++)
			{
				spawnPoint[j] = spawnPoints.GetChild(j).transform;
			}
			if (Menu.gameState == "Singleplayer" && rule <= 1)
			{
				phaseSkippers = GameObject.Find("PhaseSkippers").transform;
				Array.Resize(ref phaseSkipper, phaseSkippers.childCount);
				for (int k = 0; k < phaseSkipper.Length; k++)
				{
					phaseSkipper[k] = phaseSkippers.GetChild(k).gameObject;
				}
			}
			if (rule == 0)
			{
				if (Menu.currentSurvivalPhase > 1)
				{
					loaded = true;
					StartCoroutine("Survival", false);
				}
				else
				{
					if (Menu.gameState == "Singleplayer")
					{
						StartCoroutine("Survival", true);
					}
					else if (Multiplayer.rule == 8 && Menu.isMaster())
					{
						while (true)
						{
							GameObject[] currentPlayers = GameObject.FindGameObjectsWithTag("Player");
							if (Menu.network == 0)
							{
								Debug.Log("Singleplayer");
							}
							else if (Menu.network != 1)
							{
								Debug.Log("Spawned:" + currentPlayers.Length + " Expected PlayerCount:" + PhotonNetwork.room.PlayerCount);
								if (currentPlayers.Length >= PhotonNetwork.room.PlayerCount)
								{
									break;
								}
							}
							yield return new WaitForSeconds(1f);
						}
						if (Menu.network == 0)
						{
							Debug.Log("Singleplayer");
						}
						else if (Menu.network != 1)
						{
							base.gameObject.GetPhotonView().RPC("Survival", PhotonTargets.AllBuffered, true);
						}
					}
					nextPhaseReady = false;
				}
				while (true)
				{
					if (Menu.gameState == "Singleplayer" && chance && chanceBGM.volume < (float)Menu.mySettings.sound_bgm / 10f)
					{
						chanceBGM.volume = (float)Menu.mySettings.sound_bgm / 10f;
					}
					else if (Menu.gameState == "Singleplayer" && !chance && chanceBGM.volume > 0f)
					{
						chanceBGM.volume -= 2f * Time.deltaTime;
					}
					if (enemy == 0 && nextPhaseReady)
					{
						if (lastSkipper != null && Application.loadedLevelName == "FlatCity")
						{
							lastSkipper.SetActive(false);
							lastSkipper.GetComponent<Collider>().enabled = true;
							lastSkipper.GetComponent<Renderer>().enabled = true;
						}
						yield return new WaitForSeconds(1f);
						if (Menu.gameState == "Singleplayer")
						{
							StartCoroutine("Survival", false);
						}
						else if (Multiplayer.rule == 8)
						{
							StartCoroutine("Survival", false);
						}
						nextPhaseReady = false;
						chance = false;
					}
					yield return new WaitForSeconds(0f);
				}
			}
			if (rule == 1)
			{
				ruleTitleText[0] = "Loading...";
				ruleTitleText[1] = "Deathmatch";
				ruleTitleText[2] = "Team Deathmatch";
				ruleTitleText[3] = "Territory";
				ruleTitleText[4] = "Capture the Flag";
				ruleTitleText[5] = "Blow up the Base";
				ruleTitleText[6] = "Zombie";
				ruleTitleText[7] = "VIP";
				ruleExpText[0] = "Loading...";
				ruleExpText[1] = "Kill 5 enemies.";
				ruleExpText[2] = "Kill 10 enemies.";
				ruleExpText[3] = "Dominate the territory 15 seconds.";
				ruleExpText[4] = "Get enemies' flag.";
				ruleExpText[5] = "Blow up enemies' base with the bomb.";
				ruleExpText[6] = "Survive 30 seconds.";
				ruleExpText[7] = "Kill enemies' VIP.";
				StartCoroutine("Assortment");
				while (true)
				{
					if (chance && chanceBGM.volume < (float)Menu.mySettings.sound_bgm / 10f)
					{
						chanceBGM.volume = (float)Menu.mySettings.sound_bgm / 10f;
					}
					else if (!chance && chanceBGM.volume > 0f)
					{
						chanceBGM.volume -= 2f * Time.deltaTime;
					}
					if (((currentAssortmentRule == 1 || currentAssortmentRule == 2) && enemy <= 0) || cleared)
					{
						yield return new WaitForSeconds(1f);
						chance = false;
						if (normalBGM.volume > 0f)
						{
							normalBGM.volume -= 4f * Time.deltaTime;
						}
						if (chanceBGM.volume > 0f)
						{
							chanceBGM.volume -= 2f * Time.deltaTime;
						}
					}
					yield return new WaitForSeconds(0f);
				}
			}
			if (rule == 2)
			{
				StartCoroutine("HeadshotChallenge");
				while (true)
				{
					if (chance && chanceBGM.volume < (float)Menu.mySettings.sound_bgm / 10f)
					{
						chanceBGM.volume = (float)Menu.mySettings.sound_bgm / 10f;
					}
					else if (!chance && chanceBGM.volume > 0f)
					{
						chanceBGM.volume -= 2f * Time.deltaTime;
					}
					yield return new WaitForSeconds(0f);
				}
			}
			if (rule == 3)
			{
				respawnEnemy = 0;
				StartCoroutine("RespawnLoop");
				phaseText.enabled = true;
				phaseText.text = "Training";
				yield return new WaitForSeconds(2f);
				phaseText.text = "";
				phaseText.enabled = false;
			}
		}
		else
		{
			UnityEngine.Object.Destroy(this);
		}
	}

	private void Update()
	{
		if (rule == 0 || Multiplayer.rule == 8)
		{
			score.text = "Score: " + Menu.currentSurvivalScore + " Phase: " + Menu.currentSurvivalPhase;
		}
		else if (rule == 1)
		{
			if (currentAssortmentRule == 1)
			{
				score.text = "Score: " + Menu.currentAssortmentScore + " Rule: " + ruleTitleText[currentAssortmentRule] + " " + (5 - enemy) + "/5";
			}
			else if (currentAssortmentRule == 2)
			{
				score.text = "Score: " + Menu.currentAssortmentScore + " Rule: " + ruleTitleText[currentAssortmentRule] + " " + (10 - enemy) + "/10";
			}
			else if (currentAssortmentRule == 3)
			{
				score.text = "Score: " + Menu.currentAssortmentScore + " Rule: " + ruleTitleText[currentAssortmentRule] + " " + (15 - timeLimit) + "/15";
			}
			else if (currentAssortmentRule == 6)
			{
				score.text = "Score: " + Menu.currentAssortmentScore + " Rule: " + ruleTitleText[currentAssortmentRule] + " " + (30 - timeLimit) + "/30";
			}
			else
			{
				score.text = "Score: " + Menu.currentAssortmentScore + " Rule: " + ruleTitleText[currentAssortmentRule];
			}
		}
		else if (rule == 2)
		{
			score.text = "Score: " + Menu.currentHeadshotScore + " HeadshotChain: " + headshotChain;
		}
	}

	private IEnumerator HeadshotChallenge()
	{
		phaseText.enabled = true;
		if (Menu.currentHeadshotScore > 0)
		{
			phaseText.text = "Load succeeded.";
		}
		else
		{
			phaseText.text = "Make headshots in a row.";
		}
		yield return new WaitForSeconds(5f);
		phaseText.text = "";
		phaseText.enabled = false;
		while (true)
		{
			if (enemy < 8)
			{
				int num = UnityEngine.Random.Range(0, spawnPoint.Length);
				GameObject gameObject = UnityEngine.Object.Instantiate(Resources.Load("Flatman_Enemy"), spawnPoint[num].position, Quaternion.identity) as GameObject;
				num = UnityEngine.Random.Range(0, 6);
				if (num % 2 == 1)
				{
					num--;
				}
				gameObject.GetComponent<AI>().stats_Attack = num;
				gameObject.GetComponent<AI>().stats_Defense = 0;
				enemy++;
			}
			yield return new WaitForSeconds(0f);
		}
	}

	private IEnumerator Assortment()
	{
		phaseText.enabled = true;
		phaseText.text = "Please wait...";
		yield return new WaitForSeconds(3f);
		if (Menu.currentAssortmentPhase == 0)
		{
			Menu.currentAssortmentPhase = 1;
		}
		currentAssortmentRule = UnityEngine.Random.Range(1, 8);
		if (currentAssortmentRule == lastRule)
		{
			currentAssortmentRule++;
			if (currentAssortmentRule >= 8)
			{
				currentAssortmentRule = 1;
			}
		}
		lastRule = currentAssortmentRule;
		phaseText.enabled = true;
		phaseText.text = "Rule:" + ruleTitleText[currentAssortmentRule] + "\nObjective:" + ruleExpText[currentAssortmentRule];
		if (currentAssortmentRule == 1)
		{
			enemy = 5;
		}
		else if (currentAssortmentRule == 2)
		{
			enemy = 10;
		}
		else if (currentAssortmentRule == 3)
		{
			GameObject.Find("Territory").transform.GetChild(0).gameObject.SetActive(true);
			timeLimit = 15;
		}
		else if (currentAssortmentRule == 4)
		{
			if (Menu.rule == 4)
			{
				Transform transform = UnityEngine.Object.Instantiate(Resources.Load("FlagCamera"), new Vector3(0f, 0f, 1000f), Quaternion.identity) as Transform;
				transform.LookAt(Vector3.zero);
			}
		}
		else if (currentAssortmentRule != 5)
		{
			if (currentAssortmentRule == 6)
			{
				StartCoroutine("TimeCount", 30);
			}
			else if (currentAssortmentRule == 7)
			{
				Transform transform2 = GameObject.Find("BlueTeamBase").transform;
				GameObject currentEnemy = UnityEngine.Object.Instantiate(Resources.Load("Flatman_Enemy"), transform2.position, Quaternion.identity) as GameObject;
				currentEnemy.GetComponent<AI>().vip = true;
				currentEnemy.GetComponent<AI>().stats_Attack = 5;
				currentEnemy.GetComponent<AI>().stats_Defense = 5;
			}
		}
		yield return new WaitForSeconds(3f);
		phaseText.text = "";
		phaseText.enabled = false;
		actualAssortmentEnemyCount = assortmentEnemyCount;
		if (currentAssortmentRule == 3 || currentAssortmentRule == 4 || currentAssortmentRule == 5)
		{
			actualAssortmentEnemyCount = assortmentEnemyCount / 2;
		}
		for (int i = 0; i < actualAssortmentEnemyCount; i++)
		{
			int ram = UnityEngine.Random.Range(0, spawnPoint.Length);
			GameObject currentEnemy = UnityEngine.Object.Instantiate(Resources.Load("Flatman_Enemy"), spawnPoint[ram].position, Quaternion.identity) as GameObject;
			ram = ((Menu.currentAssortmentPhase < 5) ? UnityEngine.Random.Range(0, Menu.currentAssortmentPhase) : ((Menu.currentAssortmentPhase >= 10) ? 5 : UnityEngine.Random.Range(Menu.currentAssortmentPhase - 5, 6)));
			if (currentAssortmentRule == 6)
			{
				currentEnemy.GetComponent<AI>().stats_Attack = 10;
				currentEnemy.GetComponent<AI>().stats_Defense = 10;
			}
			else
			{
				currentEnemy.GetComponent<AI>().stats_Attack = ram;
				currentEnemy.GetComponent<AI>().stats_Defense = ram;
			}
			yield return new WaitForSeconds(1f);
		}
		if (currentAssortmentRule == 2)
		{
			Transform rtb = GameObject.Find("RedTeamBase").transform;
			for (int a = 0; a < 3; a++)
			{
				GameObject botAI = UnityEngine.Object.Instantiate(Resources.Load("Flatman_Enemy"), rtb.position, Quaternion.identity) as GameObject;
				botAI.layer = LayerMask.NameToLayer("RedTeam");
				Log("Spawn: Ally-bot");
				int allyRam = UnityEngine.Random.Range(0, 6);
				botAI.GetComponent<AI>().team = 0;
				botAI.GetComponent<AI>().stats_Attack = allyRam;
				botAI.GetComponent<AI>().stats_Defense = allyRam;
				bot++;
				yield return new WaitForSeconds(1f);
			}
		}
		if (currentAssortmentRule != 1)
		{
			StartCoroutine("BotRespawn");
		}
		respawnEnemy = actualAssortmentEnemyCount;
		StartCoroutine("RespawnLoop");
		if (Menu.gameState == "Singleplayer" && rule <= 1)
		{
			int num = UnityEngine.Random.Range(0, phaseSkipper.Length);
			if (phaseSkipper[num] == lastSkipper)
			{
				num = ((num <= phaseSkipper.Length) ? (num + 1) : 0);
			}
			phaseSkipper[num].SetActive(true);
			lastSkipper = phaseSkipper[num];
		}
		while (!(player != null) || (((currentAssortmentRule != 1 && currentAssortmentRule != 2) || enemy > 0) && !cleared))
		{
			yield return new WaitForSeconds(0f);
		}
		cleared = true;
		GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
		GameObject[] array = enemies;
		foreach (GameObject obj in array)
		{
			UnityEngine.Object.Destroy(obj);
		}
		Menu.canOpen = false;
		yield return new WaitForSeconds(1f);
		base.GetComponent<AudioSource>().PlayOneShot(clearedSE);
		phaseText.enabled = true;
		if (Menu.currentAssortmentPhase < 100)
		{
			phaseText.text = "You cleared " + ruleTitleText[currentAssortmentRule] + "!\nBonus+" + Menu.currentAssortmentPhase * 500;
			Menu.currentAssortmentScore += Menu.currentAssortmentPhase * 500;
		}
		else
		{
			phaseText.text = "You cleared " + ruleTitleText[currentAssortmentRule] + "!\nBonus+50000";
			Menu.currentAssortmentScore += 50000;
		}
		yield return new WaitForSeconds(3f);
		phaseText.text = "Saving...";
		yield return new WaitForSeconds(2f);
		Menu.myCurrent.assortment_Score = Menu.currentAssortmentScore;
		Menu.myCurrent.assortment_Phase = Menu.currentAssortmentPhase + 1;
		SaveDataController.Save();
		phaseText.text = "";
		phaseText.enabled = false;
		menu.StartCoroutine("BackgroundColor", "Respawn");
		yield return new WaitForSeconds(1f);
		int nextStage = UnityEngine.Random.Range(2, 8);
		if (nextStage == Application.loadedLevel)
		{
			nextStage++;
			if (nextStage > 7)
			{
				nextStage = 2;
			}
		}
		Application.LoadLevel(nextStage);
	}

	private IEnumerator TimeCount(int time)
	{
		timeLimit = time;
		yield return new WaitForSeconds(3f);
		while (true)
		{
			if (Menu.canOpen)
			{
				timeLimit--;
				if (timeLimit <= 0)
				{
					break;
				}
			}
			yield return new WaitForSeconds(1f);
		}
		cleared = true;
	}

	public IEnumerator RespawnLoop()
	{
		while (!cleared)
		{
			if ((rule == 3 && respawnEnemy < 8) || (respawnEnemy < actualAssortmentEnemyCount && !cleared))
			{
				int ram = UnityEngine.Random.Range(0, spawnPoint.Length);
				GameObject currentEnemy = UnityEngine.Object.Instantiate(Resources.Load("Flatman_Enemy"), spawnPoint[ram].position, Quaternion.identity) as GameObject;
				ram = ((Menu.currentAssortmentPhase < 5) ? UnityEngine.Random.Range(0, Menu.currentAssortmentPhase) : ((Menu.currentAssortmentPhase >= 10) ? 5 : UnityEngine.Random.Range(Menu.currentAssortmentPhase - 5, 6)));
				if (rule == 3)
				{
					ram = UnityEngine.Random.Range(0, 6);
				}
				if (currentAssortmentRule == 6)
				{
					currentEnemy.GetComponent<AI>().stats_Attack = 10;
					currentEnemy.GetComponent<AI>().stats_Defense = 10;
				}
				else
				{
					currentEnemy.GetComponent<AI>().stats_Attack = ram;
					currentEnemy.GetComponent<AI>().stats_Defense = ram;
				}
				respawnEnemy++;
				if (rule == 3 || currentAssortmentRule == 2 || currentAssortmentRule == 6)
				{
					yield return new WaitForSeconds(5f);
				}
				else
				{
					yield return new WaitForSeconds(10f);
				}
			}
			yield return new WaitForSeconds(0f);
		}
	}

	[PunRPC]
	public IEnumerator Survival(bool skip)
	{
		if (!skip)
		{
			if (loaded)
			{
				if (Menu.currentSurvivalPhase != 0)
				{
					Menu.currentSurvivalPhase--;
				}
				Debug.Log("Resume from phase " + Menu.currentSurvivalPhase);
				phaseText.enabled = true;
				phaseText.text = "Load succeeded.";
				yield return new WaitForSeconds(2f);
				phaseText.text = "";
				phaseText.enabled = false;
				yield return new WaitForSeconds(2f);
			}
			else
			{
				base.GetComponent<AudioSource>().PlayOneShot(clearedSE);
				phaseText.enabled = true;
				if (Menu.currentSurvivalPhase <= 50)
				{
					phaseText.text = "You cleared phase " + Menu.currentSurvivalPhase + "!!\nBonus+" + Menu.currentSurvivalPhase * 1000;
					Menu.currentSurvivalScore += Menu.currentSurvivalPhase * 1000;
				}
				else
				{
					phaseText.text = "You cleared phase " + Menu.currentSurvivalPhase + "!!\nBonus+50000";
					Menu.currentSurvivalScore += 50000;
				}
				yield return new WaitForSeconds(3f);
				if (Menu.gameState == "Singleplayer")
				{
					phaseText.text = "Saving...";
					Menu.myCurrent.survival_Score = Menu.currentSurvivalScore;
					Menu.myCurrent.survival_Phase = Menu.currentSurvivalPhase;
					SaveDataController.Save();
					yield return new WaitForSeconds(2f);
				}
				phaseText.text = "";
				phaseText.enabled = false;
				yield return new WaitForSeconds(2f);
			}
		}
		loaded = false;
		yield return new WaitForSeconds(1f);
		Menu.currentSurvivalPhase++;
		phaseText.enabled = true;
		phaseText.text = "Phase " + Menu.currentSurvivalPhase + "\nstart in 3 seconds...";
		yield return new WaitForSeconds(1f);
		phaseText.text = "Phase " + Menu.currentSurvivalPhase + "\nstart in 2 seconds...";
		yield return new WaitForSeconds(1f);
		phaseText.text = "Phase " + Menu.currentSurvivalPhase + "\nstart in 1 second...";
		yield return new WaitForSeconds(1f);
		phaseText.text = "";
		phaseText.enabled = false;
		int p = ((Menu.currentSurvivalPhase < 9) ? 3 : ((Menu.currentSurvivalPhase < 13) ? 2 : ((Menu.currentSurvivalPhase < 16) ? 1 : 0)));
		int b = ((Menu.currentSurvivalPhase < 2) ? 2 : ((Menu.currentSurvivalPhase < 6) ? 3 : ((Menu.currentSurvivalPhase < 8) ? 4 : ((Menu.currentSurvivalPhase < 11) ? 3 : ((Menu.currentSurvivalPhase < 14) ? 2 : ((Menu.currentSurvivalPhase < 17) ? 1 : 0))))));
		int g = ((Menu.currentSurvivalPhase >= 3) ? ((Menu.currentSurvivalPhase < 5) ? 1 : ((Menu.currentSurvivalPhase < 8) ? 2 : ((Menu.currentSurvivalPhase < 10) ? 3 : ((Menu.currentSurvivalPhase < 13) ? 2 : ((Menu.currentSurvivalPhase < 15) ? 3 : ((Menu.currentSurvivalPhase < 18) ? 2 : ((Menu.currentSurvivalPhase < 20) ? 1 : 0))))))) : 0);
		int y = ((Menu.currentSurvivalPhase >= 4) ? ((Menu.currentSurvivalPhase < 7) ? 1 : ((Menu.currentSurvivalPhase < 9) ? 2 : ((Menu.currentSurvivalPhase < 12) ? 3 : ((Menu.currentSurvivalPhase < 14) ? 2 : ((Menu.currentSurvivalPhase < 16) ? 3 : ((Menu.currentSurvivalPhase < 18) ? 4 : ((Menu.currentSurvivalPhase < 20) ? 5 : ((Menu.currentSurvivalPhase < 22) ? 6 : ((Menu.currentSurvivalPhase < 27) ? (6 - (Menu.currentSurvivalPhase - 21)) : 0))))))))) : 0);
		int o = ((Menu.currentSurvivalPhase >= 8) ? ((Menu.currentSurvivalPhase < 10) ? 1 : ((Menu.currentSurvivalPhase < 12) ? 2 : ((Menu.currentSurvivalPhase < 17) ? 3 : ((Menu.currentSurvivalPhase < 19) ? 4 : ((Menu.currentSurvivalPhase < 21) ? 3 : ((Menu.currentSurvivalPhase < 25) ? (Menu.currentSurvivalPhase - 19) : ((Menu.currentSurvivalPhase < 28) ? 6 : ((Menu.currentSurvivalPhase < 29) ? 4 : ((Menu.currentSurvivalPhase < 30) ? 2 : 0))))))))) : 0);
		int r = ((Menu.currentSurvivalPhase >= 11) ? ((Menu.currentSurvivalPhase < 15) ? 1 : ((Menu.currentSurvivalPhase < 19) ? 2 : ((Menu.currentSurvivalPhase < 21) ? 3 : ((Menu.currentSurvivalPhase < 26) ? 4 : ((Menu.currentSurvivalPhase < 28) ? (Menu.currentSurvivalPhase - 21) : ((Menu.currentSurvivalPhase < 29) ? 8 : ((Menu.currentSurvivalPhase >= 30) ? 12 : 10))))))) : 0);
		int enemyCount = (enemy = p + b + g + y + o + r);
		int lastPoint = 0;
		for (int i = 0; i < enemyCount; i++)
		{
			int ram = UnityEngine.Random.Range(0, spawnPoint.Length);
			if (ram == lastPoint)
			{
				ram = ((ram <= spawnPoint.Length) ? (ram + 1) : 0);
			}
			int[] statsOfEnemy = new int[2];
			if (p > 0)
			{
				statsOfEnemy[0] = 0;
				statsOfEnemy[1] = 0;
				p--;
			}
			else if (p == 0 && b > 0)
			{
				statsOfEnemy[0] = 1;
				statsOfEnemy[1] = 1;
				b--;
			}
			else if (p == 0 && b == 0 && g > 0)
			{
				statsOfEnemy[0] = 2;
				statsOfEnemy[1] = 2;
				g--;
			}
			else if (p == 0 && b == 0 && g == 0 && y > 0)
			{
				statsOfEnemy[0] = 3;
				statsOfEnemy[1] = 3;
				y--;
			}
			else if (p == 0 && b == 0 && g == 0 && y == 0 && o > 0)
			{
				statsOfEnemy[0] = 4;
				statsOfEnemy[1] = 4;
				o--;
			}
			else if (p == 0 && b == 0 && g == 0 && y == 0 && o == 0 && r > 0)
			{
				statsOfEnemy[0] = 5;
				statsOfEnemy[1] = 5;
				r--;
			}
			if (Menu.network == 0)
			{
				GameObject currentEnemy = UnityEngine.Object.Instantiate(Resources.Load("Flatman_Enemy"), spawnPoint[ram].position, Quaternion.identity) as GameObject;
				currentEnemy.GetComponent<AI>().stats_Attack = statsOfEnemy[0];
				currentEnemy.GetComponent<AI>().stats_Defense = statsOfEnemy[1];
			}
			else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
			{
				GameObject currentEnemy = PhotonNetwork.InstantiateSceneObject("Flatman_Enemy", spawnPoint[ram].position, Quaternion.identity, 0, null);
				currentEnemy.GetComponent<AI>().stats_Attack = statsOfEnemy[0];
				currentEnemy.GetComponent<AI>().stats_Defense = statsOfEnemy[1];
			}
			yield return new WaitForSeconds(1f);
		}
		if (Menu.gameState == "Singleplayer" && rule <= 1)
		{
			int num = UnityEngine.Random.Range(0, phaseSkipper.Length);
			if (phaseSkipper[num] == lastSkipper)
			{
				num = ((num < phaseSkipper.Length - 1) ? (num + 1) : 0);
			}
			phaseSkipper[num].SetActive(true);
			lastSkipper = phaseSkipper[num];
		}
		nextPhaseReady = true;
	}

	private IEnumerator BotRespawn()
	{
		while (true)
		{
			if (bot < 3 && !cleared)
			{
				int num = UnityEngine.Random.Range(0, spawnPoint.Length);
				GameObject gameObject = UnityEngine.Object.Instantiate(Resources.Load("Flatman_Enemy"), spawnPoint[num].position, Quaternion.identity) as GameObject;
				gameObject.layer = LayerMask.NameToLayer("RedTeam");
				Log("Spawn: Ally-bot");
				num = UnityEngine.Random.Range(0, 6);
				gameObject.GetComponent<AI>().team = 0;
				gameObject.GetComponent<AI>().stats_Attack = num;
				gameObject.GetComponent<AI>().stats_Defense = num;
				bot++;
			}
			yield return new WaitForSeconds(10f);
		}
	}

	public void Log(string t)
	{
		GameObject gameObject = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("Log"));
		gameObject.transform.SetParent(ui.GetChild(13), false);
		gameObject.transform.SetAsFirstSibling();
		gameObject.GetComponent<Text>().text = t;
	}

	public Singleplayer()
	{
		ruleTitleText = new Dictionary<int, string>();
		ruleExpText = new Dictionary<int, string>();
		assortmentEnemyCount = 8;

	}




}
