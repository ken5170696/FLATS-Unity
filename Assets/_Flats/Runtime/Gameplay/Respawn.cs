using System;
using System.Collections;
using UnityEngine;
public class Respawn : MonoBehaviour
{
	public UnityEngine.Object watchCamera;

	public GameObject original;

	private static bool MyView(GameObject go)
	{
		if (Menu.network == 0)
		{
			return true;
		}
		if (Menu.network == 1)
		{
			return false;
		}
		if (Menu.network == 2)
		{
			if (go.GetPhotonView().isMine)
			{
				return true;
			}
			return false;
		}
		return false;
	}

	private IEnumerator Start()
	{
		MonoBehaviour.print("Respawn...");
		if (Menu.network == 0)
		{
			if (Singleplayer.rule == 3)
			{
				Menu.canOpen = false;
				Singleplayer singleplayer = GameObject.Find("SingleplayerController").GetComponent<Singleplayer>();
				GameObject deadPlayer = base.transform.root.gameObject;
				Menu menu = GameObject.Find("Menu").GetComponent<Menu>();
				menu.StartCoroutine("BackgroundColor", "Respawn");
				singleplayer.StopCoroutine("RespawnLoop");
				GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
				GameObject[] array = enemies;
				foreach (GameObject obj in array)
				{
					UnityEngine.Object.Destroy(obj);
				}
				Singleplayer.respawnEnemy = 0;
				yield return new WaitForSeconds(2f);
				Transform sp = GameObject.Find("SpawnPoints").transform;
				UnityEngine.Object.Instantiate(position: sp.GetChild(UnityEngine.Random.Range(0, sp.childCount)).position, original: Resources.Load("Flatman"), rotation: Quaternion.identity);
				menu.StartCoroutine("BackgroundColor", "FadeOut");
				singleplayer.StartCoroutine("RespawnLoop");
				Menu.changedSettings = true;
				Menu.canOpen = true;
				UnityEngine.Object.Destroy(deadPlayer);
			}
		}
		else
		{
			if (!MyView(original))
			{
				yield break;
			}
			GameObject deadPlayer2 = base.transform.root.gameObject;
			UnityEngine.Object.Destroy(deadPlayer2.GetComponent<Destroy>());
			yield return new WaitForSeconds(1.5f);
			if (Menu.gameState != "Multiplayer" || (Multiplayer.rule == 8 && Multiplayer.end))
				yield break;
			var menuObject = GameObject.Find("Menu");
			Menu menu2 = menuObject != null ? menuObject.GetComponent<Menu>() : null;
			if (menu2 == null) yield break;
			if (!Multiplayer.end)
			{
				menu2.StartCoroutine("BackgroundColor", "Respawn");
				yield return new WaitForSeconds(3f);
				// The round may end or the player may leave during the fade delay.
				// Do not create a new player/spectator against a closed match UI.
				if (menu2 == null || Menu.gameState != "Multiplayer" || (Multiplayer.rule == 8 && Multiplayer.end))
					yield break;
				int respawnPattern = 0;
				if (Multiplayer.rule == 1 || Multiplayer.rule == 6)
				{
					respawnPattern = 0;
				}
				else if (Multiplayer.rule != 8 && Menu.network == 2 && PhotonNetwork.room.PlayerCount == 2)
				{
					respawnPattern = 0;
				}
				else if (Multiplayer.rule == 2 || Multiplayer.rule == 3 || Multiplayer.rule == 7)
				{
					respawnPattern = 1;
				}
				else if (Multiplayer.rule == 4 || Multiplayer.rule == 5)
				{
					respawnPattern = 2;
				}
				else if (Multiplayer.rule == 8)
				{
					respawnPattern = 3;
				}
				switch (respawnPattern)
				{
				case 0:
				{
					Transform sp2 = GameObject.Find("SpawnPoints").transform;
					Vector3 spawnPoint = Vector3.zero;
					LayerMask mask = 1 << LayerMask.NameToLayer("BlueTeam");
					int t = 0;
					while (spawnPoint == Vector3.zero)
					{
						Vector3 newPoint = sp2.GetChild(UnityEngine.Random.Range(0, sp2.childCount)).position;
						Collider[] colliders = Physics.OverlapSphere(newPoint, 50f, mask);
						if (colliders.Length == 0 || t > 10)
						{
							spawnPoint = newPoint;
						}
						else
						{
							t++;
						}
						yield return new WaitForSeconds(0f);
					}
					if (Menu.network == 0)
					{
						Debug.Log("Singleplayer");
					}
					else if (Menu.network != 1)
					{
						GameObject newPlayer = PhotonNetwork.Instantiate("Flatman", spawnPoint, Quaternion.identity, 0, null);
						if (deadPlayer2.transform.GetChild(0).GetComponent<Renderer>().material.color == new Color(0.5f, 0.5f, 0.5f))
						{
							newPlayer.GetPhotonView().RPC("StartZombie", PhotonTargets.AllBuffered);
						}
					}
					menu2.StartCoroutine("BackgroundColor", "FadeOut");
					break;
				}
				case 1:
				{
					string text2 = "";
					if (Menu.network == 0)
					{
						Debug.Log("Singleplayer");
					}
					else if (Menu.network != 1)
					{
						text2 = ((PhotonNetwork.player.GetTeam() != PunTeams.Team.red) ? "blue" : "red");
					}
					Transform transform5 = ((!(text2 == "red")) ? GameObject.Find("BlueTeamBase").transform : GameObject.Find("RedTeamBase").transform);
					int num4 = UnityEngine.Random.Range(-2, 2);
					int num5 = UnityEngine.Random.Range(-2, 2);
					if (Menu.network == 0)
					{
						Debug.Log("Singleplayer");
					}
					else if (Menu.network != 1)
					{
						PhotonNetwork.Instantiate("Flatman", transform5.position + Vector3.forward * num4 + Vector3.right * num5, Quaternion.identity, 0, null);
					}
					menu2.StartCoroutine("BackgroundColor", "FadeOut");
					break;
				}
				case 2:
				{
					Transform transform = GameObject.Find("SpawnPoints").transform;
					Transform transform2 = base.transform;
					string text = "";
					if (Menu.network == 0)
					{
						Debug.Log("Singleplayer");
					}
					else if (Menu.network != 1)
					{
						text = ((PhotonNetwork.player.GetTeam() != PunTeams.Team.red) ? "blue" : "red");
					}
					Transform transform3 = ((!(text == "red")) ? GameObject.Find("BlueTeamBase").transform : GameObject.Find("RedTeamBase").transform);
					GameObject[] array2 = GameObject.FindGameObjectsWithTag("Player");
					if (array2.Length != 0)
					{
						Transform[] array3 = null;
						if (Menu.network == 0)
						{
							Debug.Log("Singleplayer");
						}
						else if (Menu.network != 1)
						{
							PunTeams.Team key = ((text == "red") ? PunTeams.Team.red : PunTeams.Team.blue);
							array3 = new Transform[PunTeams.PlayersPerTeam[key].Count];
						}
						int num = 0;
						GameObject[] array4 = array2;
						foreach (GameObject gameObject in array4)
						{
							if (Menu.network == 0)
							{
								Debug.Log("Singleplayer");
							}
							else if (Menu.network != 1 && gameObject.GetPhotonView().owner.GetTeam() == PhotonNetwork.player.GetTeam())
							{
								array3[num] = gameObject.transform;
								num++;
							}
						}
						if (num != 0)
						{
							Vector3 position = array3[UnityEngine.Random.Range(0, num)].position;
							Vector3 a = new Vector3((transform3.position.x + position.x) / 2f, (transform3.position.y + position.y) / 2f, (transform3.position.z + position.z) / 2f);
							float num2 = 0f;
							foreach (Transform item in transform)
							{
								float num3 = Vector3.Distance(a, item.position);
								if (transform2 == base.transform || num3 < num2)
								{
									num2 = num3;
									transform2 = item;
								}
							}
						}
					}
					if (transform2 == null)
					{
						transform2 = transform.GetChild(UnityEngine.Random.Range(0, transform.childCount));
					}
					if (Menu.network == 0)
					{
						Debug.Log("Singleplayer");
					}
					else if (Menu.network != 1)
					{
						PhotonNetwork.Instantiate("Flatman", transform2.position, Quaternion.identity, 0, null);
					}
					menu2.StartCoroutine("BackgroundColor", "FadeOut");
					break;
				}
				case 3:
					UnityEngine.Object.Instantiate(watchCamera);
					menu2.StartCoroutine("BackgroundColor", "FadeOut");
					break;
				}
			}
			else
			{
				Transform sp3 = GameObject.Find("SpawnPoints").transform;
				Vector3 spawnPoint2 = Vector3.zero;
				LayerMask mask2 = 1 << LayerMask.NameToLayer("BlueTeam");
				while (spawnPoint2 == Vector3.zero)
				{
					Vector3 newPoint2 = sp3.GetChild(UnityEngine.Random.Range(0, sp3.childCount)).position;
					Collider[] colliders2 = Physics.OverlapSphere(newPoint2, 50f, mask2);
					if (colliders2.Length == 0)
					{
						spawnPoint2 = newPoint2;
					}
					yield return new WaitForSeconds(0f);
				}
				if (Menu.network == 0)
				{
					Debug.Log("Singleplayer");
				}
				else if (Menu.network != 1)
				{
					PhotonNetwork.Instantiate("Flatman", spawnPoint2, Quaternion.identity, 0, null);
				}
				menu2.StartCoroutine("BackgroundColor", "FadeOut");
			}
			Menu.changedSettings = true;
			UnityEngine.Object.Destroy(deadPlayer2);
		}
	}

	public Respawn()
	{
	}




}
