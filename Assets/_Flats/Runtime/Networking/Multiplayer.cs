using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
public class Multiplayer : MonoBehaviour
{
	public AudioClip multiplayerBGM;

	public static bool end;

	public static int redTeamScore;

	public static int blueTeamScore;

	public static int noneTeamScore;

	public static int privateKillCount;

	public static int privateDeathCount;

	public static int turn;

	public static int limit;

	public static int zombieCount;

	public static int rule;

	public static int bot;

	public int detailedObjective;

	private Transform ui;

	private GameObject redBase;

	private GameObject blueBase;

	private Text score;

	private Text phaseText;

	private PhotonPlayer preZombie;

	private int syncedPlayer;
	private bool changedOfflineCollision, redIgnored, blueIgnored;
	private void OnDestroy()
	{
		if (!changedOfflineCollision) return;
		Physics.IgnoreLayerCollision(8,10,redIgnored);
		Physics.IgnoreLayerCollision(9,11,blueIgnored);
	}

	private void Awake()
	{
		if (Menu.gameState == "Multiplayer")
		{
			rule = Menu.rule;
			FlatsOfflineScores.Reset();
			if (FlatsOfflineScores.FreeForAll)
			{
				redIgnored=Physics.GetIgnoreLayerCollision(8,10);blueIgnored=Physics.GetIgnoreLayerCollision(9,11);
				changedOfflineCollision=true;
				Physics.IgnoreLayerCollision(8,10,false);Physics.IgnoreLayerCollision(9,11,false);
			}
			Time.timeScale = 1f;
			if (rule != 8)
			{
				if (Menu.network == 0)
				{
					Debug.Log("Singleplayer");
				}
				else if (Menu.network != 1)
				{
					// Reset the server properties too: local cache changes alone can be
					// overwritten by the previous match's values after joining again.
					PhotonNetwork.SetPlayerCustomProperties(new ExitGames.Client.Photon.Hashtable { { "K", 0 }, { "D", 0 } });
					PhotonPlayer[] playerList = PhotonNetwork.playerList;
					foreach (PhotonPlayer photonPlayer in playerList)
					{
						photonPlayer.CustomProperties["K"] = 0;
						photonPlayer.CustomProperties["D"] = 0;
						Debug.Log(string.Concat(photonPlayer.NickName, " K:", photonPlayer.CustomProperties["K"], " D:", photonPlayer.CustomProperties["D"]));
					}
				}
			}
			redTeamScore = 0;
			blueTeamScore = 0;
			noneTeamScore = 0;
			privateKillCount = 0;
			privateDeathCount = 0;
			turn = 0;
			limit = 0;
			zombieCount = 0;
			bot = 0;
			end = false;
			GrabbedObject.canGrab = true;
			ui = GameObject.Find("UI").transform;
			GameObject gameObject = GameObject.Find("Ambient");
			AudioSource component = gameObject.GetComponent<AudioSource>();
			component.clip = multiplayerBGM;
			component.Play();
			if (Menu.rule == 4)
			{
				GameObject gameObject2 = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("FlagCamera"), new Vector3(0f, 0f, 1000f), Quaternion.identity);
				gameObject2.transform.LookAt(Vector3.zero);
			}
			if (Menu.rule >= 5 && Menu.rule <= 7)
			{
				if (rule == 5)
				{
					limit = 60 + Menu.playerCount * 20;
				}
				else if (rule == 6)
				{
					limit = 40 + Menu.playerCount * 20;
				}
				else
				{
					limit = 100 + Menu.playerCount * 20;
				}
			}
			else if (Menu.rule != 8)
			{
				limit = 270 + Menu.objective * 30;
			}
			if (Menu.rule == 8)
			{
				Singleplayer.rule = 0;
			}
		}
		else
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	private IEnumerator Start()
	{
		score = ui.Find("Score").GetComponent<Text>();
		if (!(Menu.gameState == "Multiplayer"))
		{
			yield break;
		}
		if (Menu.rule == 1)
		{
			if (Menu.objective == 1)
			{
				detailedObjective = 3;
			}
			else if (Menu.objective == 2)
			{
				detailedObjective = 5;
			}
			else if (Menu.objective == 3)
			{
				detailedObjective = 10;
			}
		}
		else if (Menu.rule == 2)
		{
			if (Menu.objective == 1)
			{
				detailedObjective = 5;
			}
			else if (Menu.objective == 2)
			{
				detailedObjective = 10;
			}
			else if (Menu.objective == 3)
			{
				detailedObjective = 15;
			}
		}
		else if (Menu.rule == 3)
		{
			if (Menu.objective == 1)
			{
				detailedObjective = 30;
			}
			else if (Menu.objective == 2)
			{
				detailedObjective = 60;
			}
			else if (Menu.objective == 3)
			{
				detailedObjective = 90;
			}
		}
		else if (Menu.rule != 8)
		{
			detailedObjective = Menu.objective;
		}
		Debug.Log("Rule:" + rule + " Detailed Objective:" + detailedObjective);
		yield return new WaitForSeconds(2f);
		int myTeam = 0;
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1)
		{
			myTeam = ((PhotonNetwork.player.GetTeam() != PunTeams.Team.red) ? ((PhotonNetwork.player.GetTeam() == PunTeams.Team.blue) ? 1 : 2) : 0);
		}
		redBase = GameObject.Find("RedTeamBase");
		blueBase = GameObject.Find("BlueTeamBase");
		Vector3 spawnPoint = Vector3.zero;
		if (Menu.rule == 1 || Menu.rule == 6)
		{
			Transform sp = GameObject.Find("SpawnPoints").transform;
			LayerMask mask = 1 << LayerMask.NameToLayer("BlueTeam");
			while (spawnPoint == Vector3.zero)
			{
				Vector3 newPoint = sp.GetChild(UnityEngine.Random.Range(0, sp.childCount)).position;
				Collider[] colliders = Physics.OverlapSphere(newPoint, 50f, mask);
				if (colliders.Length == 0)
				{
					spawnPoint = newPoint;
				}
				yield return new WaitForSeconds(0f);
			}
		}
		else
		{
			Transform transform = ((myTeam != 0) ? blueBase.transform : redBase.transform);
			int num = UnityEngine.Random.Range(-2, 2);
			int num2 = UnityEngine.Random.Range(-2, 2);
			spawnPoint = transform.position + Vector3.forward * num + Vector3.right * num2;
		}
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1)
		{
			base.gameObject.GetPhotonView().RPC("Sync", PhotonTargets.AllBuffered);
		}
		if (Menu.network != 0)
		{
			while (syncedPlayer < Menu.playerCount)
			{
				yield return new WaitForSeconds(0f);
			}
			syncedPlayer = 0;
		}
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1)
		{
			PhotonNetwork.Instantiate("Flatman", spawnPoint, Quaternion.identity, 0, null);
		}
		if ((bool)GetComponent<AudioListener>())
		{
			UnityEngine.Object.Destroy(GetComponent<AudioListener>());
		}
		GameObject mes = GameObject.Find("Message");
		phaseText = mes.transform.GetChild(0).GetComponent<Text>();
		Dictionary<int, string> ruleTitleText = new Dictionary<int, string>();
		ruleTitleText[0] = "Random Match";
		ruleTitleText[1] = "Deathmatch";
		ruleTitleText[2] = "Team Deathmatch";
		ruleTitleText[3] = "Territory";
		ruleTitleText[4] = "Capture the Flag";
		ruleTitleText[5] = "Blow up the Base";
		ruleTitleText[6] = "Zombie";
		ruleTitleText[7] = "VIP";
		ruleTitleText[8] = "Co-op Survival";
		phaseText.enabled = true;
		phaseText.text = ruleTitleText[Menu.rule];
		yield return new WaitForSeconds(3f);
		if (Menu.botCount > 0)
		{
			if (Menu.network == 0)
			{
				Debug.Log("Singleplayer");
			}
			else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
			{
				GameObject[] array = GameObject.FindGameObjectsWithTag("Player");
				List<GameObject> list = new List<GameObject>();
				List<GameObject> list2 = new List<GameObject>();
				GameObject[] array2 = array;
				foreach (GameObject gameObject in array2)
				{
					if (gameObject.GetPhotonView().owner.GetTeam() == PunTeams.Team.red)
					{
						list.Add(gameObject);
					}
					else
					{
						list2.Add(gameObject);
					}
				}
				for (int j = 0; j < Menu.botCount; j++)
				{
					if (list.Count > list2.Count)
					{
						Vector3 position = blueBase.transform.position;
						GameObject gameObject2 = PhotonNetwork.InstantiateSceneObject("Flatman_Enemy", position, Quaternion.identity, 0, null);
						AI component = gameObject2.GetComponent<AI>();
						component.team = 1;
						component.stats_Attack = 5;
						component.stats_Defense = 5;
						list2.Add(gameObject2);
					}
					else
					{
						Vector3 position2 = redBase.transform.position;
						GameObject gameObject3 = PhotonNetwork.InstantiateSceneObject("Flatman_Enemy", position2, Quaternion.identity, 0, null);
						AI component2 = gameObject3.GetComponent<AI>();
						component2.team = 0;
						component2.stats_Attack = 5;
						component2.stats_Defense = 5;
						list.Add(gameObject3);
					}
				}
				base.gameObject.GetPhotonView().RPC("BotRespawn", PhotonTargets.AllBuffered);
			}
		}
		if (Menu.rule == 5)
		{
			phaseText.enabled = true;
			if (myTeam == 0)
			{
				phaseText.text = "You are offense side. Set the bomb on the enemies' base.";
			}
			else
			{
				phaseText.text = "You are defense side. Defend your base.";
			}
		}
		else if (Menu.rule == 6)
		{
			phaseText.enabled = true;
			phaseText.text = "Choosing the Mother Zombie...";
			if (Menu.network == 0)
			{
				Debug.Log("Singleplayer");
			}
			else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
			{
				GameObject[] currentPlayers;
				while (true)
				{
					currentPlayers = GameObject.FindGameObjectsWithTag("Player");
					Debug.Log("Spawned:" + currentPlayers.Length + " Expected PlayerCount:" + PhotonNetwork.room.PlayerCount);
					if (currentPlayers.Length >= PhotonNetwork.room.PlayerCount)
					{
						break;
					}
					yield return new WaitForSeconds(1f);
				}
				yield return new WaitForSeconds(3f);
				int ram = UnityEngine.Random.Range(0, currentPlayers.Length - 1);
				if (currentPlayers[ram] == null)
				{
					currentPlayers = GameObject.FindGameObjectsWithTag("Player");
					ram = 0;
				}
				currentPlayers[ram].GetPhotonView().RPC("Zombie", PhotonTargets.AllBuffered, -1);
				if (PhotonNetwork.offlineMode) foreach (var bot in FindObjectsOfType<AI>()) bot.SetOfflineZombie(false);
				preZombie = currentPlayers[ram].GetPhotonView().owner;
			}
			StartCoroutine("ZombieCount");
		}
		else if (rule == 7)
		{
			if (Menu.network == 0)
			{
				Debug.Log("Singleplayer");
			}
			else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
			{
				GameObject[] currentPlayers2;
				while (true)
				{
					currentPlayers2 = GameObject.FindGameObjectsWithTag("Player");
					Debug.Log("Spawned:" + currentPlayers2.Length + " Expected PlayerCount:" + PhotonNetwork.room.PlayerCount);
					if (currentPlayers2.Length >= PhotonNetwork.room.PlayerCount)
					{
						break;
					}
					yield return new WaitForSeconds(1f);
				}
				yield return new WaitForSeconds(1f);
				SelectVIPs(currentPlayers2);
			}
		}
		yield return new WaitForSeconds(2f);
		phaseText.text = "";
		phaseText.enabled = false;
		while (true)
		{
			if (Menu.network == 0)
			{
				Debug.Log("Singleplayer");
			}
			else if (Menu.network != 1 && PhotonNetwork.isMasterClient && !end && limit <= 0 && rule != 5)
			{
				if (rule == 6)
				{
					base.gameObject.GetPhotonView().RPC("ZombieRound", PhotonTargets.AllBuffered, 0);
				}
				else if (rule != 8)
				{
					int[] array4 = new int[2] { 2, 0 };
					base.gameObject.GetPhotonView().RPC("GetTeamScore", PhotonTargets.All, array4);
				}
			}
			if (rule == 1)
			{
				score.text = "Kill:" + privateKillCount + " Death:" + privateDeathCount + " Time:" + limit;
			}
			else if (rule == 6)
			{
				score.text = "Time:" + limit;
			}
			else if (rule != 8)
			{
				score.text = "Red:" + redTeamScore + " Blue:" + blueTeamScore + " Time:" + limit;
			}
			if (limit > 0 && rule != 8 && Menu.isMaster())
			{
				limit--;
				if (Menu.network != 0 && Menu.network != 1)
					gameObject.GetPhotonView().RPC("SyncMatchClock", PhotonTargets.Others, limit);
			}
			yield return new WaitForSeconds(1f);
		}
	}

	[PunRPC]
	private void SyncMatchClock(int remaining, PhotonMessageInfo info)
	{
		if (info.sender != null && info.sender.IsMasterClient)
			limit = remaining;
	}

	[PunRPC]
	private IEnumerator BotRespawn()
	{
		bot = Menu.botCount;
		while (true)
		{
			if (bot < Menu.botCount && Menu.network != 1 && PhotonNetwork.isMasterClient)
			{
				GameObject[] array = GameObject.FindGameObjectsWithTag("Player");
				List<GameObject> list = new List<GameObject>();
				List<GameObject> list2 = new List<GameObject>();
				GameObject[] array2 = array;
				foreach (GameObject gameObject in array2)
				{
					if (gameObject.GetPhotonView().owner.GetTeam() == PunTeams.Team.red)
					{
						list.Add(gameObject);
					}
					else
					{
						list2.Add(gameObject);
					}
				}
				GameObject[] array3 = GameObject.FindGameObjectsWithTag("Enemy");
				GameObject[] array4 = array3;
				foreach (GameObject gameObject2 in array4)
				{
					if (gameObject2.layer == LayerMask.NameToLayer("RedTeam"))
					{
						list.Add(gameObject2);
					}
					else
					{
						list2.Add(gameObject2);
					}
				}
				if (list.Count > list2.Count)
				{
					Vector3 position = blueBase.transform.position;
					GameObject gameObject3 = PhotonNetwork.InstantiateSceneObject("Flatman_Enemy", position, Quaternion.identity, 0, null);
					gameObject3.GetComponent<AI>().team = 1;
					gameObject3.GetComponent<AI>().stats_Attack = 5;
					gameObject3.GetComponent<AI>().stats_Defense = 5;
					list2.Add(gameObject3);
					bot++;
				}
				else if (list2.Count > list.Count)
				{
					Vector3 position2 = redBase.transform.position;
					GameObject gameObject4 = PhotonNetwork.InstantiateSceneObject("Flatman_Enemy", position2, Quaternion.identity, 0, null);
					gameObject4.GetComponent<AI>().team = 0;
					gameObject4.GetComponent<AI>().stats_Attack = 5;
					gameObject4.GetComponent<AI>().stats_Defense = 5;
					list.Add(gameObject4);
					bot++;
				}
			}
			yield return new WaitForSeconds(10f);
		}
	}

	private IEnumerator ZombieCount()
	{
		while (true)
		{
			int z = 0;
			GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
			GameObject[] array = players;
			foreach (GameObject gameObject in array)
			{
				if ((bool)gameObject.GetComponent<FPSController>() && gameObject.GetComponent<FPSController>().zombie)
				{
					z++;
				}
			}
			int participants = players.Length;
			if (PhotonNetwork.offlineMode)
				foreach (var bot in FindObjectsOfType<AI>()) { participants++; if (bot.zombie) z++; }
			zombieCount = z;
			if (Menu.network == 0)
			{
				Debug.Log("Singleplayer");
			}
			else if (Menu.network != 1 && participants > 0 && zombieCount >= participants)
			{
				break;
			}
			yield return new WaitForSeconds(5f);
		}
		if (PhotonNetwork.isMasterClient)
		{
			base.gameObject.GetPhotonView().RPC("ZombieRound", PhotonTargets.AllBuffered, 1);
		}
	}

	[PunRPC]
	private void Sync()
	{
		syncedPlayer++;
	}

	[PunRPC]
	private IEnumerator ZombieRound(int winner)
	{
		StopCoroutine("ZombieCount");
		DamageReceiver.invincibility = true;
		Menu.canOpen = false;
		int[] array3 = new int[2];
		ui.parent.GetChild(2).GetChild(0).GetComponent<Text>()
			.enabled = true;
		int[] sendData;
		if (winner == 0)
		{
			ui.parent.GetChild(2).GetChild(0).GetComponent<Text>()
				.text = "Winners: Survivors";
			sendData = new int[2] { 0, 1 };
		}
		else
		{
			ui.parent.GetChild(2).GetChild(0).GetComponent<Text>()
				.text = "Winners: Zombies";
			sendData = new int[2] { 1, 1 };
		}
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
		{
			base.gameObject.GetPhotonView().RPC("GetTeamScore", PhotonTargets.All, sendData);
		}
		limit = 300 + Menu.playerCount * 30;
		yield return new WaitForSeconds(5f);
		ui.parent.GetChild(2).GetChild(0).GetComponent<Text>()
			.text = "";
		ui.parent.GetChild(2).GetChild(0).GetComponent<Text>()
			.enabled = false;
		if (end)
		{
			yield break;
		}
		syncedPlayer = 0;
		ui.Find("Mask").GetComponent<Image>().enabled = true;
		ui.parent.BroadcastMessage("BackgroundColor", "FadeIn");
		yield return new WaitForSeconds(2f);
		InformationUI[] infoUIs = UnityEngine.Object.FindObjectsOfType<InformationUI>();
		InformationUI[] array = infoUIs;
		foreach (InformationUI informationUI in array)
		{
			UnityEngine.Object.Destroy(informationUI.gameObject);
		}
		GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
		GameObject[] array2 = players;
		foreach (GameObject obj in array2)
		{
			UnityEngine.Object.Destroy(obj);
		}
		base.gameObject.AddComponent<AudioListener>();
		while (true)
		{
			players = GameObject.FindGameObjectsWithTag("Player");
			if (players.Length == 0)
			{
				break;
			}
			yield return new WaitForSeconds(0f);
		}
		Debug.Log("Destroyed all players");
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1)
		{
			base.gameObject.GetPhotonView().RPC("Sync", PhotonTargets.AllBuffered);
		}
		if (Menu.network != 0)
		{
			while (true)
			{
				Debug.Log("Synced player:" + syncedPlayer);
				if (syncedPlayer >= Menu.playerCount)
				{
					break;
				}
				yield return new WaitForSeconds(0f);
			}
			syncedPlayer = 0;
		}
		Transform sp = GameObject.Find("SpawnPoints").transform;
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1)
		{
			PhotonNetwork.Instantiate("Flatman", sp.GetChild(UnityEngine.Random.Range(0, sp.childCount)).position, Quaternion.identity, 0, null);
		}
		if ((bool)GetComponent<AudioListener>())
		{
			UnityEngine.Object.Destroy(GetComponent<AudioListener>());
		}
		ui.parent.BroadcastMessage("BackgroundColor", "FadeOut");
		yield return new WaitForSeconds(2f);
		phaseText.enabled = true;
		phaseText.text = "Choosing the Mother Zombie...";
		while (true)
		{
			players = GameObject.FindGameObjectsWithTag("Player");
			if (players.Length >= Menu.playerCount)
			{
				break;
			}
			yield return new WaitForSeconds(1f);
		}
		yield return new WaitForSeconds(3f);
		phaseText.text = "";
		phaseText.enabled = false;
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
		{
			int num = UnityEngine.Random.Range(0, players.Length - 1);
			if (players[num] == null)
			{
				players = GameObject.FindGameObjectsWithTag("Player");
				num = 0;
			}
			if (players[num].GetPhotonView().owner != null && players[num].GetPhotonView().owner == preZombie)
			{
				num++;
				if (num >= players.Length)
				{
					num = 0;
				}
			}
			players[num].GetPhotonView().RPC("Zombie", PhotonTargets.AllBuffered, -1);
			if (PhotonNetwork.offlineMode) foreach (var bot in FindObjectsOfType<AI>()) bot.SetOfflineZombie(false);
		}
		limit = 100 + Menu.playerCount * 30;
		zombieCount = 0;
		DamageReceiver.invincibility = false;
		Menu.canOpen = true;
		StartCoroutine("ZombieCount");
	}

	private void SelectVIPs(GameObject[] players)
	{
		var red = new List<GameObject>();
		var blue = new List<GameObject>();
		foreach (var player in players)
			(player.GetPhotonView().owner.GetTeam() == PunTeams.Team.red ? red : blue).Add(player);
		if (PhotonNetwork.offlineMode)
			foreach (var bot in FindObjectsOfType<AI>())
			{
				bot.SetOfflineVIP(false);
				(bot.gameObject.layer == LayerMask.NameToLayer("RedTeam") ? red : blue).Add(bot.gameObject);
			}
		foreach (var team in new[] { red, blue })
		{
			if (team.Count == 0) continue;
			var chosen = team[UnityEngine.Random.Range(0, team.Count)];
			var bot = chosen.GetComponent<AI>();
			if (bot != null) bot.SetOfflineVIP(true);
			else chosen.GetPhotonView().RPC("VIP", PhotonTargets.All);
		}
	}

	[PunRPC]
	public IEnumerator VIPRound()
	{
		limit += 30;
		yield return new WaitForSeconds(6f);
		if (Menu.botCount > 0 && Menu.isMaster())
		{
			GameObject[] array = GameObject.FindGameObjectsWithTag("Enemy");
			if (array.Length > 0)
			{
				GameObject[] array2 = array;
				foreach (GameObject gameObject in array2)
				{
					if (gameObject.layer == LayerMask.NameToLayer("RedTeam"))
					{
						gameObject.transform.position = redBase.transform.position;
					}
					else
					{
						gameObject.transform.position = blueBase.transform.position;
					}
				}
			}
		}
		if (end)
		{
			yield break;
		}
		ui.Find("Mask").GetComponent<Image>().enabled = true;
		ui.parent.BroadcastMessage("BackgroundColor", "FadeIn");
		yield return new WaitForSeconds(2f);
		GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
		GameObject[] array3 = players;
		foreach (GameObject gameObject2 in array3)
		{
			FPSController component = gameObject2.GetComponent<FPSController>();
			component.primaryWeapon.GetComponent<Gun>().currentAmmo = component.primaryWeapon.GetComponent<Gun>().limitAmmo;
			component.primaryWeapon.GetComponent<Gun>().maxAmmo = component.primaryWeapon.GetComponent<Gun>().limitMaxAmmo;
			if (Menu.network == 0)
			{
				Debug.Log("Singleplayer");
			}
			else
			{
				if (Menu.network == 1)
				{
					continue;
				}
				if (component.vip)
				{
					SkinnedMeshRenderer[] componentsInChildren = GetComponentsInChildren<SkinnedMeshRenderer>();
					if (component.gameObject.GetPhotonView().owner.GetTeam() == PunTeams.Team.red)
					{
						SkinnedMeshRenderer[] array4 = componentsInChildren;
						foreach (SkinnedMeshRenderer skinnedMeshRenderer in array4)
						{
							Color color = ui.GetChild(0).GetChild(5).GetChild(1)
								.GetChild(9)
								.GetComponent<Image>()
								.color;
							skinnedMeshRenderer.material.color = color;
						}
					}
					else
					{
						SkinnedMeshRenderer[] array5 = componentsInChildren;
						foreach (SkinnedMeshRenderer skinnedMeshRenderer2 in array5)
						{
							Color color2 = ui.GetChild(0).GetChild(5).GetChild(1)
								.GetChild(7)
								.GetComponent<Image>()
								.color;
							skinnedMeshRenderer2.material.color = color2;
						}
					}
					component.vip = false;
				}
				if (gameObject2.GetPhotonView().isMine)
				{
					if (gameObject2.GetPhotonView().owner.GetTeam() == PunTeams.Team.red)
					{
						gameObject2.transform.position = redBase.transform.position;
					}
					else
					{
						gameObject2.transform.position = blueBase.transform.position;
					}
				}
			}
		}
		ui.parent.BroadcastMessage("BackgroundColor", "FadeOut");
		yield return new WaitForSeconds(2f);
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
		{
			SelectVIPs(players);
		}
		limit = 110 + Menu.playerCount * 30;
		DamageReceiver.invincibility = false;
		Menu.canOpen = true;
	}

	[PunRPC]
	private void GetScore(int[] receivedData)
	{
		if (Menu.network != 1 && PhotonNetwork.player.ID == receivedData[0])
		{
			privateKillCount++;
		}
		if (rule == 1 && privateKillCount >= detailedObjective)
		{
			if (Menu.network == 0)
			{
				Debug.Log("Singleplayer");
			}
			else if (Menu.network != 1)
			{
				int[] array = new int[2] { 2, privateKillCount };
				base.gameObject.GetPhotonView().RPC("GetTeamScore", PhotonTargets.All, array);
			}
		}
	}

	[PunRPC]
	private void GetTeamScore(int[] receivedData)
	{
		int num = receivedData[0];
		int num2 = receivedData[1];
		switch (num)
		{
		case 0:
			redTeamScore += num2;
			break;
		case 1:
			blueTeamScore += num2;
			break;
		default:
			if (num2 >= detailedObjective)
			{
				noneTeamScore += num2;
			}
			break;
		}
		int num3 = 0;
		num3 = ((redTeamScore < blueTeamScore) ? blueTeamScore : redTeamScore);
		if (rule == 1 || rule == 8)
		{
			num3 = noneTeamScore;
		}
		if (!end && (num2 == 0 || num3 >= detailedObjective || rule == 8))
		{
			MonoBehaviour.print("GameOver");
			end = true;
			GameObject.Find("Menu").BroadcastMessage("GameOver", SendMessageOptions.DontRequireReceiver);
		}
	}

	[PunRPC]
	public void Log(string t)
	{
		GameObject gameObject = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("Log"));
		gameObject.transform.SetParent(ui.GetChild(13), false);
		gameObject.transform.SetAsFirstSibling();
		gameObject.GetComponent<Text>().text = t;
	}

	private void OnPhotonPlayerDisconnected(PhotonPlayer pp)
	{
		if (PhotonNetwork.isMasterClient)
		{
			base.gameObject.GetPhotonView().RPC("Log", PhotonTargets.AllBuffered, pp.NickName + " quit game.");
		}
		if (!end && ((rule != 1 && rule != 6 && rule != 8 && (PunTeams.PlayersPerTeam[PunTeams.Team.red].Count == 0 || PunTeams.PlayersPerTeam[PunTeams.Team.blue].Count == 0)) || (PhotonNetwork.playerList.Length == 1 && rule != 8)))
		{
			end = true;
			GameObject.Find("Menu").BroadcastMessage("GameOver", SendMessageOptions.DontRequireReceiver);
		}
	}

	public Multiplayer()
	{
	}




}
