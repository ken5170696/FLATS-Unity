using System;
using System.Collections;
using InControl;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Photon RPCs and synchronisation of team, pickups and special modes.
public partial class FPSController
{
	[PunRPC]
	private IEnumerator SyncTeam(int[] receivedData)
	{
		int team = receivedData[0];
		int c = receivedData[1];
		int pw = receivedData[2];
		int sw = receivedData[3];
		int pws = receivedData[4];
		int sws = receivedData[5];
		switch (team)
		{
		case 0:
		{
			SkinnedMeshRenderer[] componentsInChildren2 = GetComponentsInChildren<SkinnedMeshRenderer>();
			SkinnedMeshRenderer[] array2 = componentsInChildren2;
			foreach (SkinnedMeshRenderer skinnedMeshRenderer2 in array2)
			{
				skinnedMeshRenderer2.material.color = ui.GetChild(0).GetChild(5).GetChild(1)
					.GetChild(9)
					.GetComponent<Image>()
					.color;
				skinnedMeshRenderer2.gameObject.layer = 8;
			}
			base.gameObject.layer = 8;
			head.layer = 8;
			mask = 1 << LayerMask.NameToLayer("BlueTeam");
			break;
		}
		case 1:
		{
			SkinnedMeshRenderer[] componentsInChildren3 = GetComponentsInChildren<SkinnedMeshRenderer>();
			SkinnedMeshRenderer[] array3 = componentsInChildren3;
			foreach (SkinnedMeshRenderer skinnedMeshRenderer3 in array3)
			{
				skinnedMeshRenderer3.material.color = ui.GetChild(0).GetChild(5).GetChild(1)
					.GetChild(7)
					.GetComponent<Image>()
					.color;
				skinnedMeshRenderer3.gameObject.layer = 9;
			}
			base.gameObject.layer = 9;
			head.layer = 9;
			mask = 1 << LayerMask.NameToLayer("RedTeam");
			break;
		}
		default:
		{
			SkinnedMeshRenderer[] componentsInChildren = GetComponentsInChildren<SkinnedMeshRenderer>();
			SkinnedMeshRenderer[] array = componentsInChildren;
			foreach (SkinnedMeshRenderer skinnedMeshRenderer in array)
			{
				skinnedMeshRenderer.material.color = ui.GetChild(0).GetChild(5).GetChild(1)
					.GetChild(c)
					.GetComponent<Image>()
					.color;
				if (MyView(base.gameObject) || Multiplayer.rule == 6 || Multiplayer.rule == 8 || RoguelikeMode.Coop)
				{
					skinnedMeshRenderer.gameObject.layer = 8;
					base.gameObject.layer = 8;
					head.layer = 8;
					mask = 1 << LayerMask.NameToLayer("BlueTeam");
				}
				else
				{
					skinnedMeshRenderer.gameObject.layer = 9;
					base.gameObject.layer = 9;
					head.layer = 9;
					mask = 1 << LayerMask.NameToLayer("RedTeam");
				}
			}
			break;
		}
		}
		primaryWeaponIndex = pw;
		secondaryWeaponIndex = sw;
		primaryWeapon = primaryWeapons.GetChild(primaryWeaponIndex);
		secondaryWeapon = secondaryWeapons.GetChild(secondaryWeaponIndex);
		primarySightIndex = pws;
		secondarySightIndex = sws;
		if (primarySightIndex != 0)
		{
			GameObject gameObject = FlatsSightTarget.Create("Sights/" + Menu.sightDictionary[primarySightIndex]);
			gameObject.transform.SetParent(primaryWeapon.GetChild(2));
			gameObject.transform.localPosition = Vector3.zero;
			gameObject.transform.localEulerAngles = new Vector3(-90f, 0f, 0f);
			if (MyView(base.gameObject))
			{
				gameObject.transform.GetChild(0).GetChild(1).gameObject.SetActive(true);
			}
		}
		if (secondarySightIndex != 0)
		{
			GameObject gameObject2 = FlatsSightTarget.Create("Sights/" + Menu.sightDictionary[secondarySightIndex]);
			gameObject2.transform.SetParent(secondaryWeapon.GetChild(2));
			gameObject2.transform.localPosition = Vector3.zero;
			gameObject2.transform.localEulerAngles = new Vector3(-90f, 0f, 0f);
		}
		primaryWeapon.GetComponent<Gun>().currentAmmo = GunInfo.limitAmmo[primaryWeaponIndex];
		primaryWeapon.GetComponent<Gun>().maxAmmo = GunInfo.limitMaxAmmo[primaryWeaponIndex];
		primaryWeapons.GetChild(secondaryWeaponIndex).GetComponent<Gun>().currentAmmo = GunInfo.limitAmmo[secondaryWeaponIndex];
		primaryWeapons.GetChild(secondaryWeaponIndex).GetComponent<Gun>().maxAmmo = GunInfo.limitMaxAmmo[secondaryWeaponIndex];
		primaryWeapon.gameObject.SetActive(true);
		secondaryWeapon.gameObject.SetActive(true);
		currentGun = primaryWeapon.GetComponent<Gun>();
		if (!MyView(base.gameObject))
		{
			Transform pn = (Transform)UnityEngine.Object.Instantiate(myName);
			yield return new WaitForEndOfFrame();
			pn.GetComponent<InformationUI>().target = mt;
			pn.SetParent(ui.GetChild(1), false);
			pn.SetAsLastSibling();
		}
		if (Multiplayer.rule == 6 && zombie)
		{
			primaryWeapon.gameObject.SetActive(false);
			secondaryWeapon.gameObject.SetActive(false);
			ikc.leftIK = false;
			base.gameObject.name = "Zombie";
			SkinnedMeshRenderer[] componentsInChildren4 = GetComponentsInChildren<SkinnedMeshRenderer>();
			SkinnedMeshRenderer[] array4 = componentsInChildren4;
			foreach (SkinnedMeshRenderer skinnedMeshRenderer4 in array4)
			{
				skinnedMeshRenderer4.material.color = new Color(0.5f, 0.5f, 0.5f);
				skinnedMeshRenderer4.gameObject.layer = 9;
				base.gameObject.layer = 9;
				head.layer = 9;
				mask = 1 << LayerMask.NameToLayer("RedTeam");
			}
		}
		if (MyView(base.gameObject) && !zombie && !grabbing) SetBodyRenderLayer(13);
		// SyncTeam is buffered: a player who joins or comes back later runs it with the weapons this character spawned with,
		// while weapon swaps, pickups and purchases since then were sent once and are not in the buffer. That copy showed the
		// spawn weapons for the rest of the life (another gun in the hands than every other screen). So each copy asks the
		// owner once for what it carries now.
		if (Menu.network == 2 && !MyView(base.gameObject) && !zombie)
		{
			PhotonView view = base.gameObject.GetPhotonView();
			if (view != null && view.owner != null)
			{
				view.RPC("WeaponStateRequest", view.owner);
			}
		}
	}

	/// <summary>A copy of this character asks what it carries now; only the owner answers, and only to the player who asked.</summary>
	[PunRPC]
	private void WeaponStateRequest(PhotonMessageInfo info)
	{
		PhotonView view = base.gameObject.GetPhotonView();
		if (info.sender == null || view == null || !view.isMine || view.owner == null || zombie)
		{
			return;
		}
		StartCoroutine(SendWeaponState(view, info.sender));
	}

	private IEnumerator SendWeaponState(PhotonView view, PhotonPlayer to)
	{
		// to everyone (to == null): the owner's own change of what it carries; the rounds follow one frame later, once they are set
		if (to == null)
		{
			yield return null;
		}
		// a swap or an exchange in progress finishes first: its RPC reaches the asker too, and the state sent is the one after it
		float until = Time.time + 4f;
		while (anim != null && anim.GetBool("Change") && Time.time < until)
		{
			yield return null;
		}
		if (view == null || primaryWeapons == null || primaryWeapon == null || secondaryWeaponIndex < 0 || secondaryWeaponIndex >= primaryWeapons.childCount)
		{
			yield break;
		}
		Gun hand = primaryWeapon.GetComponent<Gun>();
		Gun back = primaryWeapons.GetChild(secondaryWeaponIndex).GetComponent<Gun>();
		int[] data = new int[8]
		{
			primaryWeaponIndex, secondaryWeaponIndex, primarySightIndex, secondarySightIndex,
			hand != null ? hand.currentAmmo : 0, hand != null ? hand.maxAmmo : 0, back != null ? back.currentAmmo : 0, back != null ? back.maxAmmo : 0
		};
		if (to == null)
		{
			view.RPC("WeaponState", PhotonTargets.Others, data);
		}
		else
		{
			view.RPC("WeaponState", to, data);
		}
	}

	/// <summary>The owner's answer: the weapon in the hands and the one on the back, their sights and rounds, set on this copy.</summary>
	[PunRPC]
	private IEnumerator WeaponState(int[] data, PhotonMessageInfo info)
	{
		PhotonView view = base.gameObject.GetPhotonView();
		if (data == null || data.Length < 8 || info.sender == null || view == null || view.owner != info.sender || view.isMine || zombie)
		{
			yield break;
		}
		// this copy's own swap or exchange animation (the same RPC the owner just finished) ends first
		float until = Time.time + 4f;
		while (anim != null && anim.GetBool("Change") && Time.time < until)
		{
			yield return null;
		}
		if (primaryWeapons == null || secondaryWeapons == null || primaryWeapon == null || secondaryWeapon == null)
		{
			yield break;
		}
		SetCarriedWeapons(data[0], data[1], data[2], data[3]);
		if (primaryWeaponIndex != data[0] || secondaryWeaponIndex != data[1])
		{
			yield break;
		}
		Gun hand = primaryWeapon.GetComponent<Gun>();
		Gun back = primaryWeapons.GetChild(secondaryWeaponIndex).GetComponent<Gun>();
		if (hand != null)
		{
			hand.currentAmmo = Mathf.Max(0, data[4]);
			hand.maxAmmo = Mathf.Max(0, data[5]);
		}
		if (back != null)
		{
			back.currentAmmo = Mathf.Max(0, data[6]);
			back.maxAmmo = Mathf.Max(0, data[7]);
		}
	}

	/// <summary>Puts the given models and sights in the hands and on the back at once (no animation). False when nothing changed
	/// or the models are not valid.</summary>
	private bool SetCarriedWeapons(int pw, int sw, int pws, int sws)
	{
		if (primaryWeapons == null || secondaryWeapons == null || primaryWeapon == null || secondaryWeapon == null)
		{
			return false;
		}
		if (pw < 0 || pw >= primaryWeapons.childCount || sw < 0 || sw >= secondaryWeapons.childCount || sw >= primaryWeapons.childCount)
		{
			return false;
		}
		if (pw == primaryWeaponIndex && sw == secondaryWeaponIndex && pws == primarySightIndex && sws == secondarySightIndex)
		{
			return false;
		}
		// a weapon hidden for another reason (carrying) stays hidden
		bool handShown = primaryWeapon.gameObject.activeSelf;
		bool backShown = secondaryWeapon.gameObject.activeSelf;
		primaryWeapon.gameObject.SetActive(false);
		secondaryWeapon.gameObject.SetActive(false);
		if (primaryWeapon.GetChild(2).childCount > 0)
		{
			UnityEngine.Object.Destroy(primaryWeapon.GetChild(2).GetChild(0).gameObject);
		}
		if (secondaryWeapon.GetChild(2).childCount > 0)
		{
			UnityEngine.Object.Destroy(secondaryWeapon.GetChild(2).GetChild(0).gameObject);
		}
		primaryWeaponIndex = pw;
		secondaryWeaponIndex = sw;
		primarySightIndex = pws;
		secondarySightIndex = sws;
		primaryWeapon = primaryWeapons.GetChild(primaryWeaponIndex);
		secondaryWeapon = secondaryWeapons.GetChild(secondaryWeaponIndex);
		if (primarySightIndex != 0 && Menu.sightDictionary.ContainsKey(primarySightIndex))
		{
			GameObject gameObject = FlatsSightTarget.Create("Sights/" + Menu.sightDictionary[primarySightIndex]);
			gameObject.transform.SetParent(primaryWeapon.GetChild(2));
			gameObject.transform.localPosition = Vector3.zero;
			gameObject.transform.localEulerAngles = new Vector3(-90f, 0f, 0f);
			if (MyView(base.gameObject))
			{
				gameObject.transform.GetChild(0).GetChild(1).gameObject.SetActive(true);
			}
		}
		if (secondarySightIndex != 0 && Menu.sightDictionary.ContainsKey(secondarySightIndex))
		{
			GameObject gameObject2 = FlatsSightTarget.Create("Sights/" + Menu.sightDictionary[secondarySightIndex]);
			gameObject2.transform.SetParent(secondaryWeapon.GetChild(2));
			gameObject2.transform.localPosition = Vector3.zero;
			gameObject2.transform.localEulerAngles = new Vector3(-90f, 0f, 0f);
		}
		primaryWeapon.gameObject.SetActive(handShown);
		secondaryWeapon.gameObject.SetActive(backShown);
		currentGun = primaryWeapon.GetComponent<Gun>();
		return true;
	}

	/// <summary>Roguelike, owner, at spawn: the character was created with other weapons than the run's build names (it spawned
	/// before the run state was known: a new chapter's map, a rejoin). The build's weapons go into the hands and on the back, and
	/// every other copy is told (WeaponState).</summary>
	public bool RogueCarryBuildWeapons(int primaryModel, int secondaryModel)
	{
		if (zombie || !MyView(base.gameObject) || primaryModel == secondaryModel)
		{
			return false;
		}
		// a sight belongs to the weapon it was on: kept when that model stays in its slot, none otherwise
		int pws = primaryModel == primaryWeaponIndex ? primarySightIndex : primaryModel == secondaryWeaponIndex ? secondarySightIndex : 0;
		int sws = secondaryModel == secondaryWeaponIndex ? secondarySightIndex : secondaryModel == primaryWeaponIndex ? primarySightIndex : 0;
		if (!SetCarriedWeapons(primaryModel, secondaryModel, pws, sws))
		{
			return false;
		}
		PhotonView view = Menu.network == 2 ? base.gameObject.GetPhotonView() : null;
		if (view != null && view.isMine)
		{
			StartCoroutine(SendWeaponState(view, null));
		}
		return true;
	}

	[PunRPC]
	public void Grab(int[] receivedData)
	{
		Transform transform = null;
		if (Menu.network == 0)
		{
			transform = UnityEngine.Object.FindObjectOfType<GrabbedObject>().transform;
		}
		else if (Menu.network != 1)
		{
			transform = PhotonView.Find(receivedData[1]).transform;
		}
		if (receivedData[0] == 0 && transform.parent == null && GrabbedObject.canGrab)
		{
			if (transform.GetComponent<GrabbedObject>().objectType == ObjectType.Flag)
			{
				transform.GetComponent<Collider>().enabled = false;
				transform.SetParent(primaryWeapons.parent);
				primaryWeapon.gameObject.SetActive(false);
				enableFire = false; firing = false;
				grabbing = true;
				transform.GetComponent<Rigidbody>().isKinematic = true;
				transform.localPosition = new Vector3(-0.58f, 0.12f, -0.07f);
				transform.localEulerAngles = new Vector3(0f, 170f, 300f);
				ikc.leftHandObj = transform.GetChild(0);
				anim.SetBool("Flag", true);
				if (Menu.network == 0)
				{
					SetBodyRenderLayer(8);
				}
				else
				{
					if (Menu.network == 1)
					{
						return;
					}
					if (base.gameObject.GetPhotonView().isMine)
					{
						string text = "";
						text = ((base.gameObject.GetPhotonView().owner.GetTeam() != PunTeams.Team.red) ? "Blue team got the flag." : "Red team got the flag.");
						multiplayer.GetPhotonView().RPC("Log", PhotonTargets.All, text);
						if (base.gameObject.GetPhotonView().owner.GetTeam() == PunTeams.Team.red)
						{
							SetBodyRenderLayer(8);
						}
						else
						{
							SetBodyRenderLayer(9);
						}
					}
					Multiplayer.limit += 15;
				}
			}
			else
			{
				if (transform.GetComponent<GrabbedObject>().objectType != ObjectType.Bomb)
				{
					return;
				}
				if (transform.GetComponent<GrabbedObject>().grabbedObjectLayer == base.gameObject.layer)
				{
					if (transform.GetComponent<GrabbedObject>().set)
					{
						return;
					}
					transform.GetComponent<Collider>().enabled = false;
					transform.SetParent(primaryWeapons.parent);
					primaryWeapon.gameObject.SetActive(false);
					enableFire = false; firing = false;
					grabbing = true;
					transform.GetComponent<Rigidbody>().isKinematic = true;
					transform.localPosition = new Vector3(0f, 0.07f, 0.12f);
					transform.localEulerAngles = new Vector3(0f, 45f, 0f);
					ikc.leftHandObj = transform.GetChild(0);
					anim.SetBool("Bomb", true);
					if (Menu.network == 0)
					{
						SetBodyRenderLayer(8);
					}
					else if (Menu.network != 1 && base.gameObject.GetPhotonView().isMine)
					{
						if (base.gameObject.GetPhotonView().owner.GetTeam() == PunTeams.Team.red)
						{
							SetBodyRenderLayer(8);
						}
						else
						{
							SetBodyRenderLayer(9);
						}
					}
				}
				else if (Menu.network == 0)
				{
					Debug.Log("Singleplayer");
				}
				else if (Menu.network != 1)
				{
					transform.gameObject.GetPhotonView().RPC("Destroy", PhotonTargets.MasterClient);
					if (base.gameObject.GetPhotonView().isMine)
					{
						string text2 = "Reset Bomb.";
						multiplayer.GetPhotonView().RPC("Log", PhotonTargets.All, text2);
					}
				}
			}
		}
		else if (receivedData[0] == 1)
		{
			if (Menu.network == 0)
			{
				SetBodyRenderLayer(13);
			}
			else if (Menu.network != 1 && base.gameObject.GetPhotonView().isMine)
			{
				SetBodyRenderLayer(13);
			}
			transform.GetComponent<Collider>().enabled = true;
			transform.SetParent(null);
			grabbedObject = null;
			primaryWeapon.gameObject.SetActive(true);
			enableFire = true; firing = false;
			grabbing = false;
			transform.GetComponent<Rigidbody>().isKinematic = false;
			transform.eulerAngles = Vector3.zero;
			if (transform.GetComponent<GrabbedObject>().objectType == ObjectType.Flag)
			{
				anim.SetBool("Flag", false);
			}
			else if (transform.GetComponent<GrabbedObject>().objectType == ObjectType.Bomb)
			{
				anim.SetBool("Bomb", false);
			}
		}
	}

	[PunRPC]
	private IEnumerator ExchangeWeapons(int[] receivedData)
	{
		if (zombie)
		{
			yield break;
		}
		int[] gunInfo = new int[5];
		// The owner's own count of the weapon it puts down (sent as entries 5 and 6 by TryExchangeGroundWeapon); the master's copy
		// of another player's magazine can differ from the owner's, and the dropped weapon must keep the owner's rounds.
		int droppedCurrent = -1;
		int droppedReserve = -1;
		if (Menu.network == 0)
		{
			gunInfo[0] = receivedData[0];
			gunInfo[1] = receivedData[1];
			gunInfo[2] = receivedData[2];
			gunInfo[3] = receivedData[3];
		}
		else if (Menu.network != 1)
		{
			// One ground weapon, one taker (QA-26). Requests travel through the server, so every copy handles competing requests
			// in the same order: the first claims the weapon on every copy, a later one (another player pressing at the same
			// moment, or a repeat) is refused on every copy, including the master, whose copy of the weapon is already gone.
			// A refused request changes nothing: the player keeps the weapon in hand and drops nothing.
			PhotonView groundView = PhotonView.Find(receivedData[4]);
			DroppedGun component = groundView != null ? groundView.GetComponent<DroppedGun>() : null;
			PhotonView takerView = base.gameObject.GetPhotonView();
			if (component == null || takerView == null || !component.Claim(takerView.viewID))
			{
				yield break;
			}
			gunInfo[0] = component.weaponIndex;
			gunInfo[1] = component.currentAmmo;
			gunInfo[2] = component.maxAmmo;
			gunInfo[3] = component.sight;
			if (receivedData.Length >= 7)
			{
				droppedCurrent = receivedData[5];
				droppedReserve = receivedData[6];
			}
			if (PhotonNetwork.isMasterClient)
			{
				PhotonNetwork.Destroy(groundView.gameObject);
			}
		}
		if (MyView(base.gameObject))
		{
			if (Aiming)
			{
				Zoom(false);
			}
			reticle.SetVisible(false);
		}
		enableFire = false; firing = false;
		anim.SetBool("Change", true);
		yield return new WaitForSeconds(0.2f);
		ikc.leftIK = false;
		yield return new WaitForSeconds(0.8f);
		primaryWeapon.gameObject.SetActive(false);
		if (primarySightIndex != 0 && primaryWeapon.GetChild(2).childCount > 0)
		{
			UnityEngine.Object.Destroy(primaryWeapon.GetChild(2).GetChild(0).gameObject);
		}
		if (Menu.network == 0)
		{
			GameObject newGun = UnityEngine.Object.Instantiate(Resources.Load("Weapons/Weapon" + primaryWeaponIndex), mt.position + Vector3.up * 3f, Quaternion.identity) as GameObject;
			Vector3 velocity = mt.TransformDirection(0f, 0f, 4f);
			newGun.GetComponent<Rigidbody>().linearVelocity = velocity;
			DroppedGun component2 = newGun.GetComponent<DroppedGun>();
			component2.currentAmmo = currentGun.currentAmmo;
			component2.maxAmmo = currentGun.maxAmmo;
			component2.sight = primarySightIndex;
		}
		else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
		{
			GameObject newGun = PhotonNetwork.InstantiateSceneObject("Weapons/Weapon" + primaryWeaponIndex, mt.position + Vector3.up * 3f, Quaternion.identity, 0, null);
			newGun.GetPhotonView().RPC("DropData", PhotonTargets.All, droppedCurrent >= 0 ? droppedCurrent : currentGun.currentAmmo, droppedReserve >= 0 ? droppedReserve : currentGun.maxAmmo, primarySightIndex);
		}
		primaryWeaponIndex = gunInfo[0];
		RogueNoteWeaponTaken(primaryWeaponIndex);   // a new weapon instance: its Fresh Magazine state starts unarmed
		primaryWeapon = primaryWeapons.GetChild(primaryWeaponIndex);
		currentGun = primaryWeapon.GetComponent<Gun>();
		// The taker's own capacity (Roguelike magazine and reserve upgrades differ per player): rounds above the magazine go to
		// the reserve, rounds above both are left out, so a swap never creates a magazine larger than the taker's.
		int takenMagazine = gunInfo[1];
		int takenReserve = gunInfo[2];
		if (currentGun.limitAmmo > 0 && takenMagazine > currentGun.limitAmmo)
		{
			takenReserve += takenMagazine - currentGun.limitAmmo;
			takenMagazine = currentGun.limitAmmo;
		}
		if (currentGun.limitMaxAmmo > 0 && takenReserve > currentGun.limitMaxAmmo)
		{
			takenReserve = currentGun.limitMaxAmmo;
		}
		currentGun.currentAmmo = Mathf.Max(0, takenMagazine);
		currentGun.maxAmmo = Mathf.Max(0, takenReserve);
		primarySightIndex = gunInfo[3];
		if (primarySightIndex != 0)
		{
			GameObject gameObject = FlatsSightTarget.Create("Sights/" + Menu.sightDictionary[primarySightIndex]);
			gameObject.transform.SetParent(primaryWeapon.GetChild(2));
			gameObject.transform.localPosition = Vector3.zero;
			gameObject.transform.localEulerAngles = new Vector3(-90f, 0f, 0f);
			if (MyView(base.gameObject))
			{
				gameObject.transform.GetChild(0).GetChild(1).gameObject.SetActive(true);
			}
		}
		primaryWeapon.gameObject.SetActive(true);
		anim.SetBool("Change", false);
		yield return new WaitForSeconds(0.4f);
		ikc.leftIK = true;
		yield return new WaitForSeconds(0.1f);
		enableFire = true; firing = false;
		if (MyView(base.gameObject))
		{
			reticle.SetVisible(true);
		}
	}

	[PunRPC]
	public IEnumerator VIP()
	{
		vip = true;
		Text phaseText = ui.GetChild(2).GetChild(0).GetComponent<Text>();
		phaseText.enabled = true;
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1)
		{
			if (base.gameObject.GetPhotonView().owner.GetTeam() == PunTeams.Team.red)
			{
				if (base.gameObject.GetPhotonView().isMine)
				{
					phaseText.text = "You are the VIP.";
				}
				else if (PhotonNetwork.player.GetTeam() == PunTeams.Team.red)
				{
					phaseText.text = "Red team's VIP: " + base.gameObject.GetPhotonView().owner.NickName;
				}
				SkinnedMeshRenderer[] componentsInChildren = GetComponentsInChildren<SkinnedMeshRenderer>();
				SkinnedMeshRenderer[] array = componentsInChildren;
				foreach (SkinnedMeshRenderer skinnedMeshRenderer in array)
				{
					Color color = ui.GetChild(0).GetChild(5).GetChild(1)
						.GetChild(9)
						.GetComponent<Image>()
						.color;
					skinnedMeshRenderer.material.color = new Color(color.r * 2f / 3f, color.g * 2f / 3f, color.b * 2f / 3f);
				}
			}
			else if (base.gameObject.GetPhotonView().owner.GetTeam() == PunTeams.Team.blue)
			{
				if (base.gameObject.GetPhotonView().isMine)
				{
					phaseText.text = "You are the VIP.";
				}
				else if (PhotonNetwork.player.GetTeam() == PunTeams.Team.blue)
				{
					phaseText.text = "Blue team's VIP: " + base.gameObject.GetPhotonView().owner.NickName;
				}
				SkinnedMeshRenderer[] componentsInChildren2 = GetComponentsInChildren<SkinnedMeshRenderer>();
				SkinnedMeshRenderer[] array2 = componentsInChildren2;
				foreach (SkinnedMeshRenderer skinnedMeshRenderer2 in array2)
				{
					Color color2 = ui.GetChild(0).GetChild(5).GetChild(1)
						.GetChild(7)
						.GetComponent<Image>()
						.color;
					skinnedMeshRenderer2.material.color = new Color(color2.r * 2f / 3f, color2.g * 2f / 3f, color2.b * 2f / 3f);
				}
			}
		}
		yield return new WaitForSeconds(2f);
		phaseText.text = "";
		phaseText.enabled = false;
	}

	[PunRPC]
	public IEnumerator Zombie(int zombieID)
	{
		if (biten)
		{
			yield break;
		}
		biten = true;
		Multiplayer.limit += 10;
		if (MyView(base.gameObject) && Aiming)
		{
			Zoom(false);
		}
		base.gameObject.layer = LayerMask.NameToLayer("BlueTeam");
		head.layer = LayerMask.NameToLayer("BlueTeam");
		ikc.leftIK = false;
		mask = 1 << LayerMask.NameToLayer("RedTeam");
		primaryWeapons.gameObject.SetActive(false);
		secondaryWeapons.gameObject.SetActive(false);
		anim.SetBool("Reload", false);
		anim.SetBool("Change", false);
		anim.SetBool("Jump", false);
		enableFire = true; firing = false;
		if (zombieID == -1)
		{
			enableCamRotate = false;
			enableControl = false;
			ikc.enabled = false;
			Transform camParent = Camera.main.transform.parent;
			Transform cam = camParent.GetChild(0);
			cam.SetParent(null);
			cam.GetComponent<Animator>().enabled = false;
			cam.position = mt.position + mt.up * 5f + mt.forward * 10f;
			cam.LookAt(mt.position + Vector3.up * 4f);
			anim.SetTrigger("Zombie");
			yield return new WaitForSeconds(1f);
			base.GetComponent<AudioSource>().PlayOneShot(zombieSE);
			SkinnedMeshRenderer[] smrs = GetComponentsInChildren<SkinnedMeshRenderer>();
			try
			{
				SkinnedMeshRenderer[] array = smrs;
				foreach (SkinnedMeshRenderer smr in array)
				{
					while (true)
					{
						float fadeSpeed = Time.unscaledDeltaTime;
						float targetR = Mathf.MoveTowards(smr.material.color.r, 0.5f, fadeSpeed);
						float targetG = Mathf.MoveTowards(smr.material.color.g, 0.5f, fadeSpeed);
						float targetB = Mathf.MoveTowards(smr.material.color.b, 0.5f, fadeSpeed);
						smr.material.color = new Color(targetR, targetG, targetB);
						if (smr.material.color == new Color(0.5f, 0.5f, 0.5f))
						{
							break;
						}
						yield return new WaitForSeconds(0f);
					}
				}
			}
			finally
			{
			}
			yield return new WaitForSeconds(2f);
			cam.SetParent(camParent);
			cam.localPosition = new Vector3(0f, 0f, 0f);
			cam.localEulerAngles = new Vector3(0f, 0f, 0f);
			ikc.leftHandObj = null;
			ikc.enabled = true;
			enableCamRotate = true;
			enableControl = true;
			motherZombie = true;
			if (Menu.network == 0)
			{
				Debug.Log("Singleplayer");
			}
			else if (Menu.network != 1 && base.gameObject.GetPhotonView().isMine)
			{
				string text = base.gameObject.GetPhotonView().owner.NickName + " has become the mother zombie.";
				multiplayer.GetPhotonView().RPC("Log", PhotonTargets.All, text);
			}
		}
		else
		{
			Transform camParent2 = Camera.main.transform.parent;
			Transform cam2 = camParent2.GetChild(0);
			Transform biter = null;
			if (Menu.network == 0)
			{
				Debug.Log("Singleplayer");
			}
			else if (Menu.network != 1)
			{
				biter = PhotonView.Find(zombieID).transform;
			}
			Animator biterAnim = biter.GetComponent<Animator>();
			if (MyView(base.gameObject) || MyView(biter.gameObject))
			{
				DamageReceiver.invincibility = true;
				Menu.canOpen = false;
				enableCamRotate = false;
				enableControl = false;
				ikc.enabled = false;
				cam2.SetParent(null);
				cam2.GetComponent<Animator>().enabled = false;
				cam2.position = mt.position + mt.up * 5f + mt.forward * 10f;
				cam2.LookAt(mt.position + Vector3.up * 4f);
				if (MyView(base.gameObject))
				{
					enableControl = false;
					anim.SetFloat("Vertical", 0f);
					anim.SetFloat("Horizontal", 0f);
				}
				else if (MyView(biter.gameObject))
				{
					biter.GetComponent<CharacterController>().Move(Vector3.zero);
					biter.position = mt.position + mt.right * -3.2f + mt.forward * -1.5f;
					biter.eulerAngles = mt.eulerAngles + mt.up * 60f;
					biterAnim.SetFloat("Vertical", 0f);
					biterAnim.SetFloat("Horizontal", 0f);
				}
			}
			yield return new WaitForSeconds(0.5f);
			biterAnim.SetBool("ZombieAttack", true);
			if (MyView(biter.gameObject))
			{
				biter.position = mt.position + mt.right * -3.2f + mt.forward * -1.5f;
				biter.eulerAngles = mt.eulerAngles + mt.up * 60f;
			}
			yield return new WaitForSeconds(1f);
			anim.SetTrigger("Zombie");
			yield return new WaitForSeconds(1f);
			base.GetComponent<AudioSource>().PlayOneShot(zombieSE);
			SkinnedMeshRenderer[] smrs2 = GetComponentsInChildren<SkinnedMeshRenderer>();
			try
			{
				SkinnedMeshRenderer[] array2 = smrs2;
				foreach (SkinnedMeshRenderer smr2 in array2)
				{
					while (true)
					{
						float fadeSpeed2 = Time.unscaledDeltaTime;
						float targetR2 = Mathf.MoveTowards(smr2.material.color.r, 0.5f, fadeSpeed2);
						float targetG2 = Mathf.MoveTowards(smr2.material.color.g, 0.5f, fadeSpeed2);
						float targetB2 = Mathf.MoveTowards(smr2.material.color.b, 0.5f, fadeSpeed2);
						smr2.material.color = new Color(targetR2, targetG2, targetB2);
						if (smr2.material.color == new Color(0.5f, 0.5f, 0.5f))
						{
							break;
						}
						yield return new WaitForSeconds(0f);
					}
				}
			}
			finally
			{
			}
			yield return new WaitForSeconds(3f);
			biterAnim.SetBool("ZombieAttack", false);
			yield return new WaitForSeconds(1f);
			if (MyView(base.gameObject) || MyView(biter.gameObject))
			{
				cam2.SetParent(camParent2);
				cam2.localPosition = new Vector3(0f, 0f, 0f);
				cam2.localEulerAngles = new Vector3(0f, 0f, 0f);
				ikc.leftHandObj = null;
				ikc.enabled = true;
				enableCamRotate = true;
				enableControl = true;
				DamageReceiver.invincibility = false;
				Menu.canOpen = true;
			}
		}
		base.gameObject.name = "Zombie";
		zombie = true;
		if (motherZombie)
		{
			GetComponent<DamageReceiver>().hitPoints = 10000f;
		}
		else
		{
			GetComponent<DamageReceiver>().hitPoints = 6000f;
		}
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1 && base.gameObject.GetPhotonView().isMine)
		{
			string text2 = base.gameObject.GetPhotonView().owner.NickName + " has become a zombie.";
			multiplayer.GetPhotonView().RPC("Log", PhotonTargets.All, text2);
		}
	}

	// Roguelike: a remote copy's velocity, smoothed over ~0.15 s so network jitter no longer flips its legs between idle and
	// a full stride (F38); the classic path below keeps the legacy 10 u/s threshold.
	private Vector3 rogueRemoteVelocity;

	private IEnumerator SyncAnimation()
	{
		while (true)
		{
			if (!MyView(base.gameObject))
			{
				Vector3 vector = lastPosition;
				if (Menu.network != 1)
				{
					vector = mt.InverseTransformDirection(mt.position - lastPosition) / (RoguelikeMode.Active ? Mathf.Max(Time.deltaTime, 0.0001f) : Time.deltaTime);
				}
				if (anim == null)
				{
					anim = GetComponent<Animator>();
				}
				if (RoguelikeMode.Active)
				{
					rogueRemoteVelocity = Vector3.Lerp(rogueRemoteVelocity, vector, 1f - Mathf.Exp(-Time.deltaTime / 0.15f));
					float sp = rogueRemoteVelocity.magnitude;
					bool moving = enableControl && sp > RogueLocomotion.RemoteMinSpeed && sp < 60f;
					anim.SetFloat("Vertical", moving ? RogueLocomotion.LegParam(rogueRemoteVelocity.z) : 0f, 0.1f, Time.deltaTime);
					anim.SetFloat("Horizontal", moving ? RogueLocomotion.LegParam(rogueRemoteVelocity.x) : 0f, 0.1f, Time.deltaTime);
					if (isGrounded())
					{
						anim.SetBool("Jump", false);   // a landed teammate leaves the airborne pose (this reset was lost in the sprint rework)
						// Presentation only: the owner's sprint state is not replicated, so the run pose follows the smoothed speed,
						// with the threshold raised by the player's own speed multiplier so a fast walk is not shown as a run. No
						// gameplay reads this bool (aiming and firing use the owner's explicit sprint state, QA-21).
						float runSpeed = 20f * Mathf.Max(1f, RogueHooks.MoveSpeedScale(this));
						anim.SetBool("Run", rogueRemoteVelocity.z > runSpeed && !zombie);
					}
					else
					{
						anim.SetBool("Jump", true);
						anim.SetBool("Run", false);
					}
					yield return new WaitForEndOfFrame();
					lastPosition = mt.position;
					yield return new WaitForSeconds(0f);
					continue;
				}
				rogueRemoteVelocity = Vector3.zero;
				float num = 10f;
				if (Menu.network == 0)
				{
					num = 5f;
				}
				if (enableControl && Mathf.Abs(vector.z) > num && Mathf.Abs(vector.z) < 30f)
				{
					anim.SetFloat("Vertical", vector.z);
				}
				else
				{
					anim.SetFloat("Vertical", 0f);
				}
				if (enableControl && Mathf.Abs(vector.x) > num && Mathf.Abs(vector.x) < 30f)
				{
					anim.SetFloat("Horizontal", vector.x);
				}
				else
				{
					anim.SetFloat("Horizontal", 0f);
				}
				if (isGrounded())
				{
					anim.SetBool("Jump", false);
					if (vector.z > 20f && !zombie)
					{
						anim.SetBool("Run", true);
					}
					else
					{
						anim.SetBool("Run", false);
					}
				}
				else
				{
					anim.SetBool("Jump", true);
					anim.SetBool("Run", false);
				}
			}
			yield return new WaitForEndOfFrame();
			lastPosition = mt.position;
			yield return new WaitForSeconds(0f);
		}
	}
}
