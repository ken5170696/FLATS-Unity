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
		HideOffer();
	}

	private void OnDisable()
	{
		HideOffer();
	}

	// ---- What touching this weapon offers the local player (QA-26). In Roguelike a weapon is told apart by its model and its
	// sight: armory numbers belong to the holder's loadout for a model, so the model and the sight are what taking a weapon
	// changes. Classic modes keep the model alone (the legacy rule), so only the first and last cases occur there.
	//  - Same model and sight as a carried weapon: its rounds top up that weapon's reserve and it is used up, as before.
	//  - Same model as the weapon in hand, another sight: an exchange in the same slot is offered; the reserve is topped up from
	//    it first with only the rounds needed, and the rest stay in it.
	//  - Same model as the holstered weapon, another sight: that reserve is topped up the same way. Two copies of one model
	//    cannot be carried (they share one magazine), so the exchange is offered once that weapon is in hand.
	//  - Any other model: an exchange is offered, as before.
	// The offer follows the Roguelike action rule every frame (FPSController.MayPickUpWeapon): it disappears while carrying,
	// down, in a menu, dashing or while Interact belongs to a Roguelike target, and returns when that ends.
	private static DroppedGun showing;

	private bool inside;

	private bool consumed;

	private bool requested;

	private int claimedBy;

	/// <summary>Every copy, in the order the server delivered the requests: true for the first taker only (FPSController.ExchangeWeapons).</summary>
	public bool Claim(int takerViewId)
	{
		if (claimedBy != 0 || consumed)
		{
			return false;
		}
		claimedBy = takerViewId;
		HideOffer();
		return true;
	}

	/// <summary>The local player asked for this weapon; the offer stays hidden until the request is settled.</summary>
	public void NoteRequested()
	{
		requested = true;
		HideOffer();
	}

	private void OnTriggerEnter(Collider col)
	{
		if (!(col.tag == "Player") || !MyView(col.gameObject))
		{
			return;
		}
		FPSController candidate = col.gameObject.GetComponent<FPSController>();
		if (candidate == null)
		{
			return;
		}
		player = col.transform;
		fc = candidate;
		inside = true;
		Evaluate();
	}

	private void Evaluate()
	{
		if (!inside || consumed || requested || claimedBy != 0 || fc == null || fc.primaryWeapon == null || fc.primaryWeapons == null)
		{
			HideOffer();
			return;
		}
		pw = fc.primaryWeapon.GetComponent<Gun>();
		sw = fc.primaryWeapons.GetChild(fc.secondaryWeaponIndex).GetComponent<Gun>();
		if (pw == null || sw == null)
		{
			HideOffer();
			return;
		}
		bool samePrimary = fc.primaryWeaponIndex == weaponIndex;
		bool sameSecondary = !samePrimary && fc.secondaryWeaponIndex == weaponIndex;
		bool rogue = RoguelikeMode.Active;
		bool primaryIdentical = samePrimary && (!rogue || sight == fc.primarySightIndex);
		bool secondaryIdentical = sameSecondary && (!rogue || sight == fc.secondarySightIndex);
		if (primaryIdentical)
		{
			if (pw.maxAmmo != pw.limitMaxAmmo)
			{
				TakeAll(pw);
			}
			else
			{
				HideOffer();
			}
			return;
		}
		if (secondaryIdentical)
		{
			if (sw.maxAmmo != sw.limitMaxAmmo)
			{
				TakeAll(sw);
			}
			else
			{
				HideOffer();
			}
			return;
		}
		if (samePrimary)
		{
			TakeNeeded(pw);
		}
		else if (sameSecondary)
		{
			TakeNeeded(sw);
			HideOffer();
			return;
		}
		if (!fc.zombie && fc.MayPickUpWeapon)
		{
			ShowOffer();
		}
		else
		{
			HideOffer();
		}
	}

	// The legacy refill: every round goes to the reserve (up to its limit) and the weapon is used up.
	private void TakeAll(Gun gun)
	{
		consumed = true;
		HideOffer();
		base.GetComponent<AudioSource>().PlayOneShot(getAmmo);
		gun.maxAmmo += currentAmmo + maxAmmo;
		if (gun.maxAmmo > gun.limitMaxAmmo)
		{
			gun.maxAmmo = gun.limitMaxAmmo;
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

	// A weapon worth keeping on the ground (another sight): only the rounds the reserve lacks are taken, reserve first, and every
	// copy learns what is left through DropData, so nobody takes the same rounds twice.
	private void TakeNeeded(Gun gun)
	{
		int need = gun.limitMaxAmmo - gun.maxAmmo;
		int available = Mathf.Max(0, currentAmmo) + Mathf.Max(0, maxAmmo);
		if (need <= 0 || available <= 0)
		{
			return;
		}
		int take = Mathf.Min(need, available);
		gun.maxAmmo += take;
		int fromReserve = Mathf.Min(take, Mathf.Max(0, maxAmmo));
		maxAmmo = Mathf.Max(0, maxAmmo) - fromReserve;
		currentAmmo = Mathf.Max(0, currentAmmo) - (take - fromReserve);
		base.GetComponent<AudioSource>().PlayOneShot(getAmmo);
		if (Menu.network != 0 && Menu.network != 1)
		{
			PhotonView view = base.gameObject.GetPhotonView();
			if (view != null)
			{
				view.RPC("DropData", PhotonTargets.Others, currentAmmo, maxAmmo, sight);
			}
		}
	}

	private void ShowOffer()
	{
		if (fc != null && fc.droppedGun != base.transform)
		{
			fc.droppedGun = base.transform;
		}
		if (ready && showing == this)
		{
			return;
		}
		ready = true;
		if (message == null)
		{
			return;
		}
		currentGunImage.sprite = weaponTextures[fc.primaryWeaponIndex];
		thisGunImage.sprite = weaponTextures[weaponIndex];
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
		showing = this;
	}

	private void HideOffer()
	{
		ready = false;
		if (fc != null && fc.droppedGun == base.transform)
		{
			fc.droppedGun = null;
		}
		if (showing == this)
		{
			showing = null;
			if ((bool)message)
			{
				message.GetChild(1).gameObject.SetActive(false);
			}
		}
	}

	private void Update()
	{
		if (inside)
		{
			if (fc == null)
			{
				inside = false;
				HideOffer();
			}
			else
			{
				// Re-read each frame: a weapon switch, a spent reserve, a carry or a menu changes what this weapon offers.
				Evaluate();
			}
		}
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
			inside = false;
			HideOffer();
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
			// Roguelike magazines and reserves grow with the loadout and upgrades: a larger count is the dropper's real count, not
			// corrupt data, so it is kept (the taker's own capacity applies on pickup, FPSController.ExchangeWeapons).
			int magazineCap = RoguelikeMode.Active ? Mathf.Max(limitAmmo, GunInfo.limitAmmo[weaponIndex]) * 10 : limitAmmo;
			int reserveCap = RoguelikeMode.Active ? Mathf.Max(limitMaxAmmo, GunInfo.limitMaxAmmo[weaponIndex]) * 10 : limitMaxAmmo;
			if (currentAmmo < 0 || currentAmmo > magazineCap)
			{
				currentAmmo = GunInfo.limitAmmo[weaponIndex];
			}
			if (maxAmmo < 0 || maxAmmo > reserveCap)
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
