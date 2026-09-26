using System;
using System.Collections;
using ExitGames.Client.Photon;
using UnityEngine;
using UnityEngine.UI;
public class DamageReceiver : MonoBehaviour
{
	public static bool invincibility;

	public bool userIsPlayer;

	public float hitPoints;

	public GameObject effectCamera;

	public GameObject deadReplacement;

	public AudioClip damageSE;

	public int score;

	private Transform mt;

	private Transform ct;

	private Transform ui;

	private FPSController myFPSController;

	private AI myAI;

	private GameObject multiplayer;

	private Image damageEffect;

	private Scrollbar healthbar;

	private bool died;

	private string command;

	private Transform shooter;

	private Transform killer;

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
		mt = base.transform;
		if (userIsPlayer)
		{
			myFPSController = GetComponent<FPSController>();
			ct = myFPSController.myCamera.transform;
		}
		else if (MyView(base.gameObject))
		{
			ct = Camera.main.transform.parent;
		}
		if (Menu.network != 0)
		{
			multiplayer = GameObject.Find("MultiplayerController");
		}
		while (Multiplayer.end && !(Menu.gameState == "Singleplayer"))
		{
			yield return new WaitForSeconds(0f);
		}
		if (userIsPlayer && MyView(base.gameObject))
		{
			ui = GameObject.Find("UI").transform;
			damageEffect = ui.Find("DamageEffect").GetComponent<Image>();
			healthbar = ui.Find("Healthbar").GetComponent<Scrollbar>();
			hitPoints = 1000f * (1f + (float)Menu.myCharacter.defense * 0.1f);
			damageEffect.color = new Color(1f, 1f, 1f, 0f);
			healthbar.size = 1f;
			invincibility = true;
			yield return new WaitForSeconds(3f);
			invincibility = false;
		}
		// Other actors spawning must not end the local player's protection, so the static
		// flag is only cleared by its owners (spawn timer, kill camera, round changes).
		if (!userIsPlayer)
		{
			myAI = GetComponent<AI>();
			hitPoints = 1000f * (1f + (float)myAI.stats_Defense * 0.1f) * Flats.Core.EnemyTuning.Health;
		}
		while (true)
		{
			if (userIsPlayer && MyView(base.gameObject))
			{
				float num = 1000f * (1f + (float)Menu.myCharacter.defense * 0.1f);
				if (myFPSController.zombie)
				{
					num = 6000f;
				}
				if (myFPSController.motherZombie)
				{
					num = 10000f;
				}
				if (hitPoints <= num * 0.99f)
				{
					hitPoints += 100f * Time.deltaTime;
					if (myFPSController.zombie)
					{
						hitPoints += 120f * Time.deltaTime;
					}
					if (myFPSController.motherZombie)
					{
						hitPoints += 120f * Time.deltaTime;
					}
				}
				else
				{
					hitPoints = num;
				}
				Color color = default(Color);
				float num2 = hitPoints / num;
				if (hitPoints < num)
				{
					color = new Color(1f, 1f, 1f, 1f - num2);
				}
				if (color.a > 0.9f)
				{
					color = new Color(1f, 1f, 1f, 1f);
				}
				if ((bool)damageEffect)
				{
					damageEffect.color = color;
				}
				if ((bool)healthbar)
				{
					if (died)
					{
						break;
					}
					if (healthbar.gameObject.activeSelf)
					{
						healthbar.size = num2;
					}
				}
			}
			yield return new WaitForSeconds(0f);
		}
		healthbar.size = 0f;
	}

	// Destroy(myAI) completes at the end of the frame; adding the sink before then
	// would give Photon two methods with the same RPC name.
	private IEnumerator AddDeadAIRpcSink()
	{
		yield return null;
		if (this != null && GetComponent<AI>() == null && GetComponent<DeadAIRpcSink>() == null)
		{
			base.gameObject.AddComponent<DeadAIRpcSink>();
		}
	}

	[PunRPC]
	private void NetworkDamage(int[] receivedData)
	{
		// End-of-match spectator respawns intentionally do not initialize combat UI.
		if (Menu.gameState == "Multiplayer" && Multiplayer.end) return;
		if (died)
		{
			return;
		}
		// Invincibility protects players (spawn, kill camera, round changes), as in ApplyDamage.
		if (!base.gameObject.activeSelf || (invincibility && userIsPlayer))
		{
			return;
		}
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1)
		{
			shooter = PhotonView.Find(receivedData[2]).transform;
		}
		if ((userIsPlayer && !MyView(base.gameObject)) || hitPoints <= 0f)
		{
			return;
		}
		if (receivedData[1] == 1 && !userIsPlayer && !myAI.vip && (Menu.network == 0 || Multiplayer.rule == 8))
		{
			command = "head";
			killer = shooter;
			if (Menu.network == 0)
			{
				Die(receivedData[2]);
			}
			else if (Menu.network != 1)
			{
				base.gameObject.GetPhotonView().RPC("Die", PhotonTargets.All, receivedData[2]);
			}
			return;
		}
		hitPoints -= receivedData[0];
		if (hitPoints <= 0f)
		{
			command = "normal";
			killer = shooter;
			if (Menu.network == 0)
			{
				Die(receivedData[2]);
			}
			else if (Menu.network != 1)
			{
				base.gameObject.GetPhotonView().RPC("Die", PhotonTargets.All, receivedData[2]);
			}
		}
		else if (!userIsPlayer && !myAI.vip && (Menu.network == 0 || Multiplayer.rule == 8))
		{
			int num = 0;
			num = ((!Singleplayer.chance) ? UnityEngine.Random.Range(0, 60) : UnityEngine.Random.Range(0, 12));
			if (num == 0 && receivedData[1] != -1)
			{
				command = "mortal";
				killer = shooter;
				if (Menu.network == 0)
				{
					Die(receivedData[2]);
				}
				else if (Menu.network != 1)
				{
					base.gameObject.GetPhotonView().RPC("Die", PhotonTargets.All, receivedData[2]);
				}
			}
		}
		else
		{
			killer = shooter;
		}
	}

	public void ApplyDamage(float damage, int headshot, Transform shooter)
	{
		if (Menu.gameState == "Multiplayer" && Multiplayer.end) return;
		if ((invincibility && userIsPlayer) || damage == 0f || died)
		{
			return;
		}
		if (base.GetComponent<AudioSource>().enabled && shooter != null && shooter.gameObject.tag == "Player" && MyView(shooter.gameObject))
		{
			base.GetComponent<AudioSource>().PlayOneShot(damageSE);
		}
		if (Menu.network == 0 || Multiplayer.rule == 8)
		{
			if (hitPoints <= 0f)
			{
				return;
			}
			if (headshot == 1 && !userIsPlayer && !myAI.vip)
			{
				command = "head";
				killer = shooter;
				if (killer.tag == "Enemy")
				{
					if (base.gameObject.layer == LayerMask.NameToLayer("BlueTeam"))
					{
						command = "ally";
					}
					else
					{
						command = "normal";
					}
				}
				if (Menu.network == 0)
				{
					Die(0);
				}
				else if (Menu.network != 1)
				{
					base.gameObject.GetPhotonView().RPC("Die", PhotonTargets.All, 0);
				}
				return;
			}
			hitPoints -= damage;
			if (!userIsPlayer && myAI.isPatrol && myAI.targets[0] != null)
			{
				Vector3 normalized = (myAI.targets[0].position - mt.position).normalized;
				Quaternion rotation = Quaternion.LookRotation(normalized);
				rotation.x = 0f;
				rotation.z = 0f;
				mt.rotation = rotation;
			}
			if (hitPoints <= 0f && (Menu.gameState == "Singleplayer" || !userIsPlayer || (Multiplayer.rule == 8 && MyView(base.gameObject))))
			{
				command = "normal";
				killer = shooter;
				if (killer.tag == "Enemy")
				{
					if (base.gameObject.layer == LayerMask.NameToLayer("BlueTeam"))
					{
						command = "ally";
					}
					else
					{
						command = "normal";
					}
				}
				if (Menu.network == 0)
				{
					if (!userIsPlayer && myAI.vip)
					{
						command = "vip";
					}
					Die(0);
				}
				else if (Menu.network != 1)
				{
					base.gameObject.GetPhotonView().RPC("Die", PhotonTargets.All, 0);
				}
			}
			else
			{
				if (userIsPlayer || myAI.vip || Singleplayer.rule == 2)
				{
					return;
				}
				int num = 0;
				num = ((!Singleplayer.chance) ? UnityEngine.Random.Range(0, 60) : UnityEngine.Random.Range(0, 12));
				if (num != 0 || headshot == -1)
				{
					return;
				}
				command = "mortal";
				killer = shooter;
				if (killer.tag == "Enemy")
				{
					if (base.gameObject.layer == LayerMask.NameToLayer("BlueTeam"))
					{
						command = "ally";
					}
					else
					{
						command = "normal";
					}
				}
				if (Menu.network == 0)
				{
					Die(0);
				}
				else if (Menu.network != 1)
				{
					base.gameObject.GetPhotonView().RPC("Die", PhotonTargets.All, 0);
				}
			}
			return;
		}
		int[] array = new int[3];
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1)
		{
			// Every client simulates every bullet, so each copy of a hit would report it
			// and the target would take the damage once per client. Only the shooter's
			// owner reports (the master client for AI shooters).
			var shooterView = shooter != null ? shooter.gameObject.GetPhotonView() : null;
			if (shooterView == null || !shooterView.isMine)
			{
				return;
			}
			array[0] = (int)damage;
			array[1] = headshot;
			array[2] = shooter.gameObject.GetPhotonView().viewID;
			if (base.gameObject.tag == "Player")
			{
				base.gameObject.GetPhotonView().RPC("NetworkDamage", PhotonTargets.All, array);
			}
			else
			{
				base.gameObject.GetPhotonView().RPC("NetworkDamage", PhotonTargets.MasterClient, array);
			}
		}
	}

	[PunRPC]
	private void Die(int receivedData)
	{
		if (died)
		{
			return;
		}
		died = true;
		GameObject gameObject = UnityEngine.Object.Instantiate(deadReplacement, mt.position, mt.rotation) as GameObject;
		if (gameObject == null)
		{
			return;
		}
		gameObject.GetComponent<AudioSource>().volume = 0.5f;
		gameObject.GetComponent<AudioSource>().pitch = 0.75f;
		gameObject.GetComponent<AudioSource>().PlayOneShot(damageSE);
		SkinnedMeshRenderer[] componentsInChildren = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
		SkinnedMeshRenderer[] array = componentsInChildren;
		foreach (SkinnedMeshRenderer skinnedMeshRenderer in array)
		{
			skinnedMeshRenderer.material = mt.GetChild(0).GetComponent<Renderer>().material;
		}
		if (base.gameObject.tag == "Player")
		{
			UnityEngine.Object.Destroy(myFPSController);
		}
		else
		{
			UnityEngine.Object.Destroy(myAI);
			if (Menu.network != 0) StartCoroutine(AddDeadAIRpcSink());
		}
		UnityEngine.Object.Destroy(GetComponent<CharacterController>());
		for (int j = 0; j < mt.childCount; j++)
		{
			mt.GetChild(j).gameObject.SetActive(false);
		}
		if (ct == null && (bool)Camera.main.gameObject)
		{
			ct = Camera.main.transform;
		}
		if (command == "head" && ct != null)
		{
			if (Singleplayer.rule == 0 || Multiplayer.rule == 8)
			{
				score = 500;
			}
			else if (Singleplayer.rule == 2)
			{
				Singleplayer.headshotChain++;
				if (Singleplayer.headshotChain >= 100)
				{
					Menu.currentHeadshotScore += 10000;
				}
				else
				{
					Menu.currentHeadshotScore += 100 * Singleplayer.headshotChain;
				}
				if (Singleplayer.headshotChain > Menu.currentHeadshotChain)
				{
					Menu.currentHeadshotChain = Singleplayer.headshotChain;
					GameObject.Find("SingleplayerController").GetComponent<Singleplayer>().Log("Achieved new headshot record.");
				}
			}
			if (MyView(killer.gameObject))
			{
				Supershot.PlayKill(effectCamera, ct, gameObject.transform, true);
			}
		}
		else if (command == "mortal" && ct != null)
		{
			if (MyView(killer.gameObject))
			{
				Supershot.PlayKill(effectCamera, ct, gameObject.transform, false);
			}
		}
		else if (command == "vip" && ct != null)
		{
			if (Menu.network == 0 || !MyView(base.gameObject))
			{
				Supershot.PlayKill(effectCamera, ct, gameObject.transform, false, true);
			}
		}
		else if (command == "ally" && ct != null)
		{
			GameObject.Find("SingleplayerController").GetComponent<Singleplayer>().Log("Ally-bot killed Enemy.");
		}
		else if (command == "normal" && (userIsPlayer || (Menu.network != 0 && Multiplayer.rule != 8)))
		{
			if (Menu.network == 0)
			{
				healthbar.size = 0f;
				gameObject.transform.GetChild(4).gameObject.SetActive(true);
				gameObject.transform.GetChild(4).gameObject.GetComponent<Respawn>().original = base.gameObject;
				UnityEngine.Object.Destroy(gameObject.GetComponent<Destroy>());
				if (!Menu.VRmode)
				{
					gameObject.transform.GetChild(4).GetChild(0).GetComponent<BlurEffect>()
						.enabled = true;
				}
				if (Singleplayer.rule == 3)
				{
					Debug.Log("Respawn for training...");
				}
				else
				{
					GameObject[] array2 = GameObject.FindGameObjectsWithTag("Enemy");
					if (array2 != null)
					{
						GameObject[] array3 = array2;
						foreach (GameObject gameObject5 in array3)
						{
							if (gameObject5.layer == base.gameObject.layer)
							{
								UnityEngine.Object.Destroy(gameObject5);
							}
						}
					}
					GameObject.Find("Menu").BroadcastMessage("GameOver", SendMessageOptions.DontRequireReceiver);
				}
			}
			else if (!userIsPlayer || MyView(base.gameObject))
			{
				if (userIsPlayer)
				{
					healthbar.size = 0f;
					gameObject.transform.GetChild(4).gameObject.SetActive(true);
					gameObject.transform.GetChild(4).gameObject.GetComponent<Respawn>().original = base.gameObject;
					if (!Menu.VRmode)
					{
						gameObject.transform.GetChild(4).GetChild(0).GetComponent<BlurEffect>()
							.enabled = true;
					}
				}
				if (killer == mt)
				{
					string text = "";
					if (killer == mt && userIsPlayer)
					{
						if (Menu.network == 0)
						{
							Debug.Log("Singleplayer");
						}
						else if (Menu.network != 1)
						{
							text = "Suicide:" + base.gameObject.GetPhotonView().owner.NickName;
							multiplayer.GetPhotonView().RPC("Log", PhotonTargets.All, text);
							// A VIP lost to a fall or their own grenade still starts the next VIP round.
							if (Multiplayer.rule == 7 && myFPSController.vip && !Multiplayer.end)
							{
								invincibility = true;
								multiplayer.GetPhotonView().RPC("VIPRound", PhotonTargets.All);
							}
						}
					}
				}
				else if (Multiplayer.rule != 8)
				{
					if (Menu.network == 0)
					{
						Debug.Log("Singleplayer");
					}
					else if (Menu.network != 1)
					{
						PhotonPlayer photonPlayer = PhotonNetwork.player;
						if (killer == null)
						{
							killer = PhotonView.Find(receivedData).transform;
						}
						// The local gameObject is the replacement ragdoll, not the registered actor.
						FlatsOfflineScores.Kill(killer,base.gameObject);
						if (killer.tag == "Player")
						{
							photonPlayer = killer.gameObject.GetPhotonView().owner;
						}
						if (killer.tag == "Player" && (base.gameObject.tag == "Player" || PhotonNetwork.offlineMode))
						{
							ExitGames.Client.Photon.Hashtable hashtable = new ExitGames.Client.Photon.Hashtable();
							int num = (int)photonPlayer.CustomProperties["K"];
							num++;
							hashtable["K"] = num;
							photonPlayer.SetCustomProperties(hashtable);
							int[] array4 = new int[2] { photonPlayer.ID, 0 };
							multiplayer.GetPhotonView().RPC("GetScore", PhotonTargets.All, array4);
						}
						if (userIsPlayer && (killer.tag == "Player" || PhotonNetwork.offlineMode))
						{
							ExitGames.Client.Photon.Hashtable hashtable2 = new ExitGames.Client.Photon.Hashtable();
							int num2 = (int)PhotonNetwork.player.CustomProperties["D"];
							num2++;
							hashtable2["D"] = num2;
							PhotonNetwork.SetPlayerCustomProperties(hashtable2);
							Multiplayer.privateDeathCount++;
						}
						bool victimVIP = Multiplayer.rule == 7 && (userIsPlayer ? myFPSController.vip : GetComponent<AI>() != null && GetComponent<AI>().vip);
						if (Multiplayer.rule <= 2 || victimVIP)
						{
							int num3 = 2;
							if (killer.tag == "Player")
							{
								if (photonPlayer.GetTeam() == PunTeams.Team.red)
								{
									num3 = 0;
								}
								else if (photonPlayer.GetTeam() == PunTeams.Team.blue)
								{
									num3 = 1;
								}
							}
							else if (Multiplayer.rule != 1)
							{
								num3 = ((!(LayerMask.LayerToName(killer.gameObject.layer) == "RedTeam")) ? 1 : 0);
							}
							if ((userIsPlayer && MyView(base.gameObject)) || (!userIsPlayer && MyView(killer.gameObject)))
							{
								int[] array5 = new int[2] { num3, 1 };
								multiplayer.GetPhotonView().RPC("GetTeamScore", PhotonTargets.All, array5);
							}
							if (victimVIP)
							{
								invincibility = true;
								if (!Multiplayer.end)
								{
									multiplayer.GetPhotonView().RPC("VIPRound", PhotonTargets.All);
								}
							}
						}
						if ((userIsPlayer && MyView(base.gameObject)) || (!userIsPlayer && MyView(killer.gameObject)))
						{
							string text2 = "";
							text2 = ((killer.tag == "Player" && base.gameObject.tag == "Player") ? (photonPlayer.NickName + " killed " + base.gameObject.GetPhotonView().owner.NickName) : ((killer.tag == "Player" && base.gameObject.tag != "Player") ? (photonPlayer.NickName + " killed Flatman(Bot)") : ((!(killer.tag != "Player") || !(base.gameObject.tag == "Player")) ? "Flatman(Bot) killed Flatman(Bot)" : ("Flatman(Bot) killed " + base.gameObject.GetPhotonView().owner.NickName))));
							multiplayer.GetPhotonView().RPC("Log", PhotonTargets.All, text2);
						}
					}
				}
				else if (Multiplayer.rule == 8)
				{
					if (Menu.network == 0)
					{
						Debug.Log("Singleplayer");
					}
					else if (Menu.network != 1)
					{
						string text3 = "Killed: " + base.gameObject.GetPhotonView().owner.NickName;
						multiplayer.GetPhotonView().RPC("Log", PhotonTargets.All, text3);
					}
				}
			}
			else if (!MyView(base.gameObject) && Multiplayer.rule == 7 && myFPSController.vip)
			{
				Supershot.PlayKill(effectCamera, ct, gameObject.transform, false, true, base.gameObject.layer);
			}
		}
		if (myFPSController != null)
		{
			if (Menu.gameState == "Singleplayer")
			{
				GameObject gameObject7 = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("Weapons/Weapon" + myFPSController.primaryWeaponIndex), mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity);
				GameObject gameObject8 = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("Weapons/Weapon" + myFPSController.secondaryWeaponIndex), mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity);
				if (myFPSController.primarySightIndex != 0)
				{
					GameObject gameObject9 = FlatsSightTarget.Create("Sights/" + Menu.sightDictionary[myFPSController.primarySightIndex]);
					gameObject9.transform.SetParent(gameObject7.transform.GetChild(2));
					gameObject9.transform.localPosition = Vector3.zero;
					gameObject9.transform.localEulerAngles = new Vector3(-90f, 0f, 0f);
				}
				if (myFPSController.secondarySightIndex != 0)
				{
					GameObject gameObject10 = FlatsSightTarget.Create("Sights/" + Menu.sightDictionary[myFPSController.secondarySightIndex]);
					gameObject10.transform.SetParent(gameObject8.transform.GetChild(2));
					gameObject10.transform.localPosition = Vector3.zero;
					gameObject10.transform.localEulerAngles = new Vector3(-90f, 0f, 0f);
				}
				DroppedGun component5 = gameObject7.GetComponent<DroppedGun>();
				component5.sight = myFPSController.primarySightIndex;
				DroppedGun component6 = gameObject8.GetComponent<DroppedGun>();
				component6.sight = myFPSController.secondarySightIndex;
			}
			else if (Menu.isMaster() && !myFPSController.zombie)
			{
				Gun component7 = myFPSController.primaryWeapon.gameObject.GetComponent<Gun>();
				Gun component8 = myFPSController.primaryWeapons.GetChild(myFPSController.secondaryWeaponIndex).gameObject.GetComponent<Gun>();
				if (Menu.network == 0)
				{
					Debug.Log("Singleplayer");
				}
				else if (Menu.network != 1)
				{
					GameObject go = PhotonNetwork.InstantiateSceneObject("Weapons/Weapon" + myFPSController.primaryWeaponIndex, mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity, 0, null);
					GameObject go2 = PhotonNetwork.InstantiateSceneObject("Weapons/Weapon" + myFPSController.secondaryWeaponIndex, mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity, 0, null);
					go.GetPhotonView().RPC("DropData", PhotonTargets.All, component7.currentAmmo, component7.maxAmmo, myFPSController.primarySightIndex);
					go2.GetPhotonView().RPC("DropData", PhotonTargets.All, component8.currentAmmo, component8.maxAmmo, myFPSController.secondarySightIndex);
				}
			}
		}
		else if (Menu.gameState == "Singleplayer")
		{
			GameObject gameObject11 = UnityEngine.Object.Instantiate(Resources.Load("Weapons/Weapon" + myAI.primaryWeaponIndex), mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity) as GameObject;
			GameObject gameObject12 = UnityEngine.Object.Instantiate(Resources.Load("Weapons/Weapon" + myAI.secondaryWeaponIndex), mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity) as GameObject;
			Vector3 vector = mt.TransformDirection(0f, 0f, 4f);
			Gun component9 = myAI.primaryWeapon.gameObject.GetComponent<Gun>();
			Gun component10 = myAI.primaryWeapons.GetChild(myAI.secondaryWeaponIndex).gameObject.GetComponent<Gun>();
			gameObject11.GetComponent<Rigidbody>().linearVelocity = vector;
			gameObject12.GetComponent<Rigidbody>().linearVelocity = vector * 2f;
			DroppedGun component11 = gameObject11.GetComponent<DroppedGun>();
			component11.currentAmmo = component9.currentAmmo;
			component11.maxAmmo = component9.maxAmmo;
			component11.sight = myAI.primarySightIndex;
			DroppedGun component12 = gameObject12.GetComponent<DroppedGun>();
			component12.currentAmmo = component10.currentAmmo;
			component12.maxAmmo = component10.maxAmmo / 2;
			component12.sight = myAI.secondarySightIndex;
			if (myAI.primarySightIndex != 0)
			{
				GameObject gameObject13 = FlatsSightTarget.Create("Sights/" + Menu.sightDictionary[myAI.primarySightIndex]);
				gameObject13.transform.SetParent(gameObject11.transform.GetChild(2));
				gameObject13.transform.localPosition = Vector3.zero;
				gameObject13.transform.localEulerAngles = new Vector3(-90f, 0f, 0f);
			}
			if (myAI.secondarySightIndex != 0)
			{
				GameObject gameObject14 = FlatsSightTarget.Create("Sights/" + Menu.sightDictionary[myAI.secondarySightIndex]);
				gameObject14.transform.SetParent(gameObject12.transform.GetChild(2));
				gameObject14.transform.localPosition = Vector3.zero;
				gameObject14.transform.localEulerAngles = new Vector3(-90f, 0f, 0f);
			}
		}
		else if (Menu.isMaster())
		{
			Gun component13 = myAI.primaryWeapon.gameObject.GetComponent<Gun>();
			Gun component14 = myAI.primaryWeapons.GetChild(myAI.secondaryWeaponIndex).gameObject.GetComponent<Gun>();
			if (Menu.network == 0)
			{
				Debug.Log("Singleplayer");
			}
			else if (Menu.network != 1)
			{
				GameObject go3 = PhotonNetwork.InstantiateSceneObject("Weapons/Weapon" + myAI.primaryWeaponIndex, mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity, 0, null);
				GameObject go4 = PhotonNetwork.InstantiateSceneObject("Weapons/Weapon" + myAI.secondaryWeaponIndex, mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity, 0, null);
				go3.GetPhotonView().RPC("DropData", PhotonTargets.All, component13.currentAmmo, component13.maxAmmo, myAI.primarySightIndex);
				go4.GetPhotonView().RPC("DropData", PhotonTargets.All, component14.currentAmmo, component14.maxAmmo, myAI.secondarySightIndex);
			}
		}
		if (!userIsPlayer)
		{
			if (Menu.gameState == "Singleplayer" || Multiplayer.rule == 8)
			{
				if (base.gameObject.layer == LayerMask.NameToLayer("BlueTeam"))
				{
					Singleplayer.enemy--;
					if (Singleplayer.rule == 1 || Singleplayer.rule == 3)
					{
						Singleplayer.respawnEnemy--;
					}
				}
				else
				{
					Singleplayer.bot--;
				}
				if (base.gameObject.layer == LayerMask.NameToLayer("BlueTeam") && (Menu.gameState == "Singleplayer" || (killer != null && MyView(killer.gameObject))))
				{
					if (Singleplayer.rule == 0 || Multiplayer.rule == 8)
					{
						Menu.currentSurvivalScore += score;
					}
					else if (Singleplayer.rule == 1)
					{
						Menu.currentAssortmentScore += score / 2;
					}
					else if (Singleplayer.rule == 2 && command != "head")
					{
						Menu.currentHeadshotScore -= 100 * Singleplayer.headshotChain;
						GameObject.Find("SingleplayerController").GetComponent<Singleplayer>().Log("Headshot chain:" + Singleplayer.headshotChain);
						if (Menu.currentHeadshotScore < 0)
						{
							Menu.currentHeadshotScore = 0;
						}
						Singleplayer.headshotChain = 0;
						Singleplayer.chance = false;
					}
				}
			}
			else if (Menu.botCount > 0 && Menu.isMaster())
			{
				Multiplayer.bot--;
			}
		}
		Invoke("Stop", 5f);
	}

	private void Stop()
	{
		UnityEngine.Object.Destroy(base.gameObject);
	}

	public DamageReceiver()
	{
		hitPoints = 500f;
		command = "normal";

	}




}
