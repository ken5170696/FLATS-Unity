using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class DroppedGun : MonoBehaviour, IPunObservable
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
		// The toss is a random speed: only the copy that simulates this weapon throws it (online: the master's). Every copy used
		// to draw its own speed and run its own physics, so one weapon came to rest in a different place on every screen.
		simulating = Simulates();
		if (simulating)
		{
			body.linearVelocity = base.transform.forward * UnityEngine.Random.Range(1, 50);
		}
		else
		{
			body.isKinematic = true;
			if (!hasPose)
			{
				netPosition = mt.position;
				netRotation = mt.rotation;
			}
			askPoseAt = Time.time + PoseRequestDelay;
		}
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

	// ---- One place for everyone (online play). A ground weapon is a physics object owned by the scene (the master client).
	// The master alone simulates it and sends where it is while it moves (the PhotonView's own stream, unreliable); when it
	// comes to rest the final pose goes out once, reliably, and nothing more is sent until it moves again. Every other copy is
	// kinematic and glides to the pose it was told. A copy that never heard a pose (a player who joined or came back after the
	// weapon settled: the buffered spawn carries only the spawn point) asks the master once. A new master takes the simulation
	// over from where the weapon is drawn. Offline and LAN sessions simulate locally as before.
	private const float PoseRequestDelay = 1f;

	private const float RestSeconds = 0.5f;

	private const float SnapDistance = 30f;

	private Rigidbody body;

	private PhotonView netView;

	private bool simulating = true;

	private bool resting;

	private float stillFor;

	private Vector3 restPosition;

	private Quaternion restRotation = Quaternion.identity;

	private bool hasPose;

	private bool netResting;

	private Vector3 netPosition;

	private Quaternion netRotation;

	private double restStamp = double.MinValue;

	private float askPoseAt = -1f;

	private void Awake()
	{
		body = base.GetComponent<Rigidbody>();
		netView = base.GetComponent<PhotonView>();
		if (netView != null)
		{
			// the same change on every copy, before the first serialization, so the stream layout matches on both ends
			if (netView.ObservedComponents == null)
			{
				netView.ObservedComponents = new List<Component>();
			}
			netView.ObservedComponents.RemoveAll((Component c) => c == null);
			if (!netView.ObservedComponents.Contains(this))
			{
				netView.ObservedComponents.Add(this);
			}
			netView.synchronization = ViewSynchronization.Unreliable;
		}
	}

	private bool Simulates()
	{
		return Menu.network != 2 || netView == null || netView.isMine;
	}

	public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
	{
		if (stream.isWriting)
		{
			if (resting || !simulating)
			{
				return;   // nothing written, nothing sent: a weapon lying still costs no traffic
			}
			stream.SendNext(mt != null ? mt.position : base.transform.position);
			stream.SendNext(mt != null ? mt.rotation : base.transform.rotation);
		}
		else
		{
			Vector3 position = (Vector3)stream.ReceiveNext();
			Quaternion rotation = (Quaternion)stream.ReceiveNext();
			if (info.timestamp <= restStamp)
			{
				return;   // an unreliable sample from before the reliable resting pose arrived late
			}
			netPosition = position;
			netRotation = rotation;
			netResting = false;
			hasPose = true;
		}
	}

	/// <summary>The master's resting pose (reliable): sent to everyone when the weapon settles and to a copy that asked.</summary>
	[PunRPC]
	private void DropPose(Vector3 position, Quaternion rotation, PhotonMessageInfo info)
	{
		if (info.sender == null || !info.sender.IsMasterClient || Simulates())
		{
			return;
		}
		netPosition = position;
		netRotation = rotation;
		netResting = true;
		hasPose = true;
		restStamp = info.timestamp;
	}

	/// <summary>A copy that has no pose yet asks; only the master answers, and only to the player who asked.</summary>
	[PunRPC]
	private void DropPoseRequest(PhotonMessageInfo info)
	{
		if (info.sender == null || !PhotonNetwork.isMasterClient || netView == null)
		{
			return;
		}
		netView.RPC("DropPose", info.sender, base.transform.position, base.transform.rotation);
	}

	private void SyncPose()
	{
		if (body == null || mt == null)
		{
			return;
		}
		bool now = Simulates();
		if (now != simulating)
		{
			// the master changed: the new one simulates from where the weapon is drawn, the old one (if still here) follows
			simulating = now;
			body.isKinematic = !now;
			resting = false;
			stillFor = 0f;
			if (!now)
			{
				netPosition = mt.position;
				netRotation = mt.rotation;
			}
		}
		if (simulating)
		{
			if (Menu.network != 2 || netView == null)
			{
				return;
			}
			if (mt.position.y < -500f && !body.isKinematic)
			{
				// fell out of the map: it stops here instead of being sent forever
				body.linearVelocity = Vector3.zero;
				body.angularVelocity = Vector3.zero;
				body.isKinematic = true;
			}
			bool still = body.isKinematic || body.IsSleeping() || (body.linearVelocity.sqrMagnitude < 0.01f && body.angularVelocity.sqrMagnitude < 0.01f);
			if (resting)
			{
				// pushed again, or still creeping (a weapon rocking or rolling slowly in place passes for still): whenever the
				// pose leaves the one that was sent, the stream resumes and a new resting pose follows
				if ((mt.position - restPosition).sqrMagnitude > 0.0004f || Quaternion.Angle(mt.rotation, restRotation) > 1f)
				{
					resting = false;
					stillFor = 0f;
				}
				return;
			}
			stillFor = still ? stillFor + Time.deltaTime : 0f;
			if (stillFor >= RestSeconds)
			{
				resting = true;
				restPosition = mt.position;
				restRotation = mt.rotation;
				netView.RPC("DropPose", PhotonTargets.Others, mt.position, mt.rotation);
			}
			return;
		}
		if (!hasPose)
		{
			if (askPoseAt >= 0f && Time.time >= askPoseAt && netView != null && netView.viewID != 0 && PhotonNetwork.inRoom)
			{
				askPoseAt = -1f;
				netView.RPC("DropPoseRequest", PhotonTargets.MasterClient);
			}
			return;
		}
		float gap = (netPosition - mt.position).magnitude;
		if (gap > SnapDistance || (netResting && gap < 0.02f))
		{
			mt.SetPositionAndRotation(netPosition, netRotation);
		}
		else
		{
			float k = 1f - Mathf.Exp(-14f * Time.deltaTime);
			mt.SetPositionAndRotation(Vector3.Lerp(mt.position, netPosition, k), Quaternion.Slerp(mt.rotation, netRotation, k));
		}
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
		SyncPose();
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
