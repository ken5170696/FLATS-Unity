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
				if (MyView(base.gameObject) || Multiplayer.rule == 6 || Multiplayer.rule == 8)
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
				enableFire = false;
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
					enableFire = false;
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
			enableFire = true;
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
		if (MyView(base.gameObject))
		{
			if (Aiming)
			{
				Zoom(false);
			}
			reticle.SetVisible(false);
		}
		int[] gunInfo = new int[5];
		if (Menu.network == 0)
		{
			gunInfo[0] = receivedData[0];
			gunInfo[1] = receivedData[1];
			gunInfo[2] = receivedData[2];
			gunInfo[3] = receivedData[3];
		}
		else if (Menu.network != 1)
		{
			GameObject gun = PhotonView.Find(receivedData[4]).gameObject;
			DroppedGun component = gun.GetComponent<DroppedGun>();
			gunInfo[0] = component.weaponIndex;
			gunInfo[1] = component.currentAmmo;
			gunInfo[2] = component.maxAmmo;
			gunInfo[3] = component.sight;
			if (PhotonNetwork.isMasterClient)
			{
				PhotonNetwork.Destroy(gun);
			}
		}
		enableFire = false;
		anim.SetBool("Change", true);
		yield return new WaitForSeconds(0.2f);
		ikc.leftIK = false;
		yield return new WaitForSeconds(0.8f);
		primaryWeapon.gameObject.SetActive(false);
		if (primarySightIndex != 0)
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
			newGun.GetPhotonView().RPC("DropData", PhotonTargets.All, currentGun.currentAmmo, currentGun.maxAmmo, primarySightIndex);
		}
		primaryWeaponIndex = gunInfo[0];
		primaryWeapon = primaryWeapons.GetChild(primaryWeaponIndex);
		currentGun = primaryWeapon.GetComponent<Gun>();
		currentGun.currentAmmo = gunInfo[1];
		currentGun.maxAmmo = gunInfo[2];
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
		enableFire = true;
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
		enableFire = true;
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

	private IEnumerator SyncAnimation()
	{
		while (true)
		{
			if (!MyView(base.gameObject))
			{
				Vector3 vector = lastPosition;
				if (Menu.network != 1)
				{
					vector = mt.InverseTransformDirection(mt.position - lastPosition) / Time.deltaTime;
				}
				if (anim == null)
				{
					anim = GetComponent<Animator>();
				}
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
