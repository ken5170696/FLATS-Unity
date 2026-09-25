using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class DroppedGun : MonoBehaviour
{
	public int weaponIndex;

	public int currentAmmo;

	public int limitAmmo;

	public int maxAmmo;

	public int limitMaxAmmo;

	public int sight;

	public bool dontDestroy;

	public AudioClip getAmmo;

	public Sprite[] weaponTextures;

	public bool ready;

	private Transform message;

	private Transform player;

	private FPSController fc;

	private Gun pw;

	private Gun sw;

	private Image currentGunImage;

	private Image thisGunImage;

	private Transform mt;

	private Transform ct;

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
		int network = Menu.network;
		mt = base.transform;
		base.GetComponent<Rigidbody>().linearVelocity = base.transform.forward * UnityEngine.Random.Range(1, 50);
		message = GameObject.Find("Message").transform;
		currentGunImage = message.GetChild(1).GetChild(0).GetComponent<Image>();
		thisGunImage = message.GetChild(1).GetChild(1).GetComponent<Image>();
		if (sight != 0)
		{
			GameObject gameObject = FlatsSightTarget.Create("Sights/" + Menu.sightDictionary[sight]);
			gameObject.transform.SetParent(base.transform.GetChild(2));
			gameObject.transform.localPosition = Vector3.zero;
			gameObject.transform.localEulerAngles = new Vector3(-90f, 0f, 0f);
		}
		if (!dontDestroy)
		{
			yield return new WaitForSeconds(30f);
			if (Menu.network == 0)
			{
				UnityEngine.Object.Destroy(base.gameObject);
			}
			else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
			{
				PhotonNetwork.Destroy(base.gameObject);
			}
		}
	}

	private void OnDestroy()
	{
		if ((bool)message)
		{
			message.GetChild(1).gameObject.SetActive(false);
		}
	}

	private void OnDisable()
	{
		ready = false;
		if ((bool)message)
		{
			message.GetChild(1).gameObject.SetActive(false);
		}
	}

	private void OnTriggerEnter(Collider col)
	{
		if (!(col.tag == "Player") || ready || !MyView(col.gameObject))
		{
			return;
		}
		player = col.transform;
		fc = player.gameObject.GetComponent<FPSController>();
		pw = fc.primaryWeapon.GetComponent<Gun>();
		sw = fc.primaryWeapons.GetChild(fc.secondaryWeaponIndex).GetComponent<Gun>();
		currentGunImage.sprite = weaponTextures[fc.primaryWeaponIndex];
		thisGunImage.sprite = weaponTextures[weaponIndex];
		if (fc.primaryWeaponIndex == weaponIndex && pw.maxAmmo != pw.limitMaxAmmo)
		{
			base.GetComponent<AudioSource>().PlayOneShot(getAmmo);
			pw.maxAmmo += currentAmmo + maxAmmo;
			if (pw.maxAmmo > pw.limitMaxAmmo)
			{
				pw.maxAmmo = pw.limitMaxAmmo;
			}
			if (Menu.network == 0)
			{
				UnityEngine.Object.Destroy(base.gameObject);
			}
			else if (Menu.network != 1)
			{
				base.gameObject.GetPhotonView().RPC("Destroy", PhotonTargets.All);
			}
		}
		else if (fc.secondaryWeaponIndex == weaponIndex && sw.maxAmmo != sw.limitMaxAmmo)
		{
			base.GetComponent<AudioSource>().PlayOneShot(getAmmo);
			sw.maxAmmo += currentAmmo + maxAmmo;
			if (sw.maxAmmo > sw.limitMaxAmmo)
			{
				sw.maxAmmo = sw.limitMaxAmmo;
			}
			if (Menu.network == 0)
			{
				UnityEngine.Object.Destroy(base.gameObject);
			}
			else if (Menu.network != 1)
			{
				base.gameObject.GetPhotonView().RPC("Destroy", PhotonTargets.All);
			}
		}
		else if (!fc.zombie && fc.primaryWeaponIndex != weaponIndex && fc.secondaryWeaponIndex != weaponIndex)
		{
			ready = true;
			fc.droppedGun = base.transform;
			if (Input.GetJoystickNames().Length > 0)
			{
				message.GetChild(1).GetChild(3).GetComponent<Text>()
					.text = "Exchange weapon: {control:Interact}.";
			}
			else if (!Application.isMobilePlatform && Input.mousePresent)
			{
				message.GetChild(1).GetChild(3).GetComponent<Text>()
					.text = "Exchange weapon: {control:Interact}.";
			}
			else
			{
				// Mobile browsers can report a mouse; touch players use the HUD Swap button.
				message.GetChild(1).GetChild(3).GetComponent<Text>()
					.text = "Tap Swap to exchange";
			}
			message.GetChild(1).gameObject.SetActive(true);
		}
	}

	private void Update()
	{
		if ((bool)ct)
		{
			if (Vector3.Distance(mt.position, ct.position) > 120f)
			{
				base.GetComponent<Renderer>().enabled = false;
			}
			else
			{
				base.GetComponent<Renderer>().enabled = true;
			}
		}
		else if ((bool)Camera.main)
		{
			ct = Camera.main.transform;
		}
	}

	private void OnTriggerExit(Collider col)
	{
		if (col.tag == "Player" && MyView(col.gameObject))
		{
			ready = false;
			fc.droppedGun = null;
			if ((bool)message)
			{
				message.GetChild(1).gameObject.SetActive(false);
			}
		}
	}

	[PunRPC]
	private void DropData(int currentAmmoData, int maxAmmoData, int sightData)
	{
		if (Menu.network != 0)
		{
			currentAmmo = currentAmmoData;
			maxAmmo = maxAmmoData;
			sight = sightData;
			if (currentAmmo == -1 && maxAmmo == -1)
			{
				dontDestroy = true;
			}
			if (currentAmmo < 0 || currentAmmo > limitAmmo)
			{
				currentAmmo = GunInfo.limitAmmo[weaponIndex];
			}
			if (maxAmmo < 0 || maxAmmo > limitMaxAmmo)
			{
				maxAmmo = GunInfo.limitMaxAmmo[weaponIndex];
			}
		}
	}

	[PunRPC]
	private void Destroy()
	{
		if (Menu.network == 0)
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
		else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
		{
			PhotonNetwork.Destroy(base.gameObject);
		}
	}

	public DroppedGun()
	{
	}




}
