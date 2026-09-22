using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class Gun : MonoBehaviour
{
	public int id;

	public int currentAmmo;

	public int maxAmmo;

	public int limitAmmo;

	public int limitMaxAmmo;

	public int burstCount;

	public float damage;

	public float rpm;

	public float accuracy;

	public float reloadTime;

	public float zoom;

	public float headshotBonus;

	public bool oneShot;

	public bool handgun;

	public bool grenade;

	public AudioClip fireSE;

	public UnityEngine.Object muzzleFlash;

	private Transform mtRoot;

	private Text ammoCount;

	private Animator anim;

	private IKController ikc;

	private bool userIsPlayer;

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

	private void Start()
	{
		mtRoot = base.transform.parent.parent.parent.parent.parent.parent.parent.parent.parent.parent.parent;
		anim = mtRoot.GetComponent<Animator>();
		ikc = mtRoot.GetComponent<IKController>();
		id = base.transform.GetSiblingIndex();
		limitAmmo = GunInfo.limitAmmo[id];
		limitMaxAmmo = GunInfo.limitMaxAmmo[id];
		burstCount = GunInfo.burstCount[id];
		damage = GunInfo.damage[id];
		rpm = GunInfo.rpm[id];
		accuracy = GunInfo.accuracy[id];
		reloadTime = GunInfo.reloadTime[id];
		zoom = GunInfo.zoom[id];
		headshotBonus = GunInfo.headshotBonus[id];
		oneShot = GunInfo.oneShot[id];
		handgun = GunInfo.handgun[id];
		grenade = GunInfo.grenade[id];
		if (mtRoot.tag == "Player")
		{
			if (Multiplayer.end && Menu.gameState != "Singleplayer")
			{
				return;
			}
			if (MyView(mtRoot.gameObject))
			{
				userIsPlayer = true;
				ammoCount = GameObject.Find("AmmoCount").GetComponent<Text>();
				StartCoroutine("AmmoCount");
				base.gameObject.layer = LayerMask.NameToLayer("IgnoreObject");
				if (base.transform.GetChild(2).childCount != 0)
				{
					base.transform.GetChild(2).GetChild(0).GetChild(0)
						.gameObject.layer = LayerMask.NameToLayer("IgnoreObject");
					base.transform.GetChild(2).GetChild(0).GetChild(0)
						.GetChild(0)
						.gameObject.SetActive(true);
				}
			}
		}
		ikc.leftHandObj = base.transform.GetChild(0);
		if (handgun)
		{
			anim.SetBool("Handgun", true);
		}
	}

	private void OnEnable()
	{
		if (!mtRoot)
		{
			return;
		}
		ikc.leftHandObj = base.transform.GetChild(0);
		if (userIsPlayer && MyView(mtRoot.gameObject))
		{
			StartCoroutine("AmmoCount");
			base.gameObject.layer = LayerMask.NameToLayer("IgnoreObject");
			if (base.transform.GetChild(2).childCount != 0)
			{
				base.transform.GetChild(2).GetChild(0).GetChild(0)
					.gameObject.layer = LayerMask.NameToLayer("IgnoreObject");
				base.transform.GetChild(2).GetChild(0).GetChild(0)
					.GetChild(0)
					.gameObject.SetActive(true);
			}
		}
		if (handgun)
		{
			anim.SetBool("Handgun", true);
		}
	}

	private void OnDisable()
	{
		base.gameObject.layer = 0;
		if (base.transform.GetChild(2).childCount != 0)
		{
			base.transform.GetChild(2).GetChild(0).GetChild(0)
				.gameObject.layer = 0;
			base.transform.GetChild(2).GetChild(0).GetChild(0)
				.GetChild(0)
				.gameObject.SetActive(false);
		}
		if ((bool)anim && handgun)
		{
			anim.SetBool("Handgun", false);
		}
		if (userIsPlayer && MyView(mtRoot.gameObject))
		{
			StopCoroutine("AmmoCount");
		}
	}

	private IEnumerator AmmoCount()
	{
		while (true)
		{
			if ((bool)ammoCount)
			{
				ammoCount.text = currentAmmo + "/" + maxAmmo;
			}
			yield return new WaitForSeconds(60f / (rpm * 2f));
		}
	}

	public Gun()
	{
	}




}
