using System;
using System.Collections;
using InControl;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Aim, fire, melee, reload, weapon change and grenades.
public partial class FPSController
{
	public void Zoom(bool zoom)
	{
		if (zombie)
		{
			return;
		}
		if (!Aiming && zoom)
		{
			if (primarySightIndex == 0)
			{
				reticle.SetVisible(false);
			}
			startZooming = true;
			aimEyeLocalPosition = mt.InverseTransformPoint(ct.position);
			camAnim.enabled = false;
		}
		else if (Aiming && !zoom)
		{
			if ((bool)sight)
			{
				sight.SetActive(false);
				sight.transform.localScale = new Vector3(1f, 1f, 1f);
			}
			if (reticle != null)
			{
				reticle.SetVisible(true);
			}
			isZoom = false;
			startZooming = false;
		}
	}

	private IEnumerator GrenadeReady()
	{
		yield return new WaitForSeconds(longTapAnim.length + 0.05f);
		if (ltr != null)
		{
			if (Menu.network == 0)
			{
				StartCoroutine("ThrowGrenade");
				UnityEngine.Object.Destroy(ltr.gameObject);
			}
			else if (Menu.network != 1 && base.gameObject.GetPhotonView().isMine)
			{
				base.gameObject.GetPhotonView().RPC("ThrowGrenade", PhotonTargets.All);
				UnityEngine.Object.Destroy(ltr.gameObject);
			}
		}
	}

	[PunRPC]
	private IEnumerator Smash()
	{
		if (zombie)
		{
			Transform closest = null;
			Collider[] colliders = Physics.OverlapSphere(mt.position, 8f, mask);
			if (PhotonNetwork.offlineMode && Multiplayer.rule == 6 && !Multiplayer.end)
			{
				AI nearestBot = null;
				foreach (var collider in colliders)
				{
					var bot = collider.GetComponentInParent<AI>();
					if (bot != null && !bot.zombie && (nearestBot == null || Vector3.Distance(mt.position, bot.transform.position) < Vector3.Distance(mt.position, nearestBot.transform.position))) nearestBot = bot;
				}
				if (nearestBot != null)
				{
					anim.SetBool("ZombieAttack", true);
					nearestBot.SetOfflineZombie(true);
					yield return new WaitForSeconds(0.5f);
					anim.SetBool("ZombieAttack", false);
					yield break;
				}
			}
			if (colliders.Length > 0)
			{
				Collider[] array = colliders;
				foreach (Collider collider in array)
				{
					if (closest == null && (bool)collider.gameObject.GetComponent<FPSController>())
					{
						closest = collider.transform;
					}
					else if (closest != null && Vector3.Distance(mt.position, collider.transform.position) < Vector3.Distance(mt.position, closest.position) && (bool)collider.gameObject.GetComponent<FPSController>())
					{
						closest = collider.transform;
					}
				}
				if (closest != null && closest.gameObject.layer != base.gameObject.layer && (bool)closest.gameObject.GetComponent<DamageReceiver>() && !Multiplayer.end && !closest.gameObject.GetComponent<FPSController>().biten)
				{
					if (Menu.network == 0)
					{
						Debug.Log("Singleplayer");
					}
					else if (Menu.network != 1)
					{
						closest.gameObject.GetPhotonView().RPC("Zombie", PhotonTargets.All, base.gameObject.GetPhotonView().viewID);
					}
					if (MyView(base.gameObject))
					{
						enableControl = false;
						mt.position = closest.position + closest.right * -3.2f + closest.forward * -1.5f;
						mt.eulerAngles = closest.eulerAngles + closest.up * 60f;
					}
				}
			}
			else if (MyView(base.gameObject))
			{
				anim.SetBool("ZombieAttack", true);
				yield return new WaitForSeconds(0.5f);
				anim.SetBool("ZombieAttack", false);
			}
		}
		else
		{
			if (!grabbing && !enableFire)
			{
				yield break;
			}
			if (MyView(base.gameObject) && Aiming)
			{
				Zoom(false);
			}
			enableFire = false;
			float damage = 500f * (1f + (float)Menu.myCharacter.attack * 0.1f);
			if (grabbing)
			{
				damage = 100f * (1f + (float)Menu.myCharacter.attack * 0.1f);
			}
			anim.SetBool("Smash", true);
			yield return new WaitForSeconds(0.15f);
			RaycastHit hit = default(RaycastHit);
			if (Physics.SphereCast(ct.position, 2f, ct.forward, out hit, 3f, mask) && hit.collider.gameObject.layer != base.gameObject.layer && (bool)hit.collider.gameObject.GetComponent<DamageReceiver>())
			{
				hit.collider.gameObject.GetComponent<DamageReceiver>().ApplyDamage(damage, -1, mt);
			}
			if (grabbing)
			{
				LayerMask layerMask = 1 << LayerMask.NameToLayer("Glass");
				if (Physics.SphereCast(ct.position, 2f, ct.forward, out hit, 3f, layerMask) && (bool)hit.collider.gameObject.GetComponent<Glass>())
				{
					hit.collider.gameObject.GetComponent<Glass>().StartCoroutine("Break");
				}
			}
			yield return new WaitForSeconds(0.25f);
			anim.SetBool("Smash", false);
			yield return new WaitForSeconds(0.1f);
			if (!grabbing)
			{
				enableFire = true;
			}
		}
	}

	[PunRPC]
	private IEnumerator Shoot()
	{
		if (zombie)
		{
			anim.SetBool("ZombieAttack", true);
			yield return new WaitForSeconds(0.2f);
			LayerMask glassMask = 1 << LayerMask.NameToLayer("Glass");
			RaycastHit hit = default(RaycastHit);
			if (Physics.SphereCast(ct.position, 2f, ct.forward, out hit, 1f, glassMask))
			{
				hit.collider.gameObject.GetComponent<Glass>().StartCoroutine("Break");
			}
			yield return new WaitForSeconds(0.3f);
			anim.SetBool("ZombieAttack", false);
		}
		else
		{
			if ((currentGun.maxAmmo <= 0 && currentGun.currentAmmo <= 0) || !SessionPlaying)
			{
				yield break;
			}
			enableFire = false;
			if (anim.GetBool("Run"))
			{
				yield return new WaitForSeconds(0.2f);
			}
			InputDevice inputDevice = InputManager.ActiveDevice;
			int currentBurstCount = currentGun.burstCount;
			if (currentGun.currentAmmo <= 0)
			{
				if (Menu.network == 0)
				{
					StartCoroutine("Reload");
				}
				else if (Menu.network != 1 && base.gameObject.GetPhotonView().isMine)
				{
					base.gameObject.GetPhotonView().RPC("Reload", PhotonTargets.All);
				}
				yield break;
			}
			if (currentGun.oneShot)
			{
				GameObject mf = UnityEngine.Object.Instantiate(currentGun.muzzleFlash, GetBulletTrailOrigin(), mt.rotation) as GameObject;
				mf.GetComponent<ParticleSystem>().startColor = mt.GetChild(0).GetComponent<Renderer>().material.color;
				base.GetComponent<AudioSource>().PlayOneShot(currentGun.fireSE);
				for (int i = 0; i < currentGun.burstCount; i++)
				{
					float x = UnityEngine.Random.Range(0f - (100f - currentGun.accuracy), 100f - currentGun.accuracy);
					float y = UnityEngine.Random.Range(0f - (100f - currentGun.accuracy), 100f - currentGun.accuracy);
					Vector3 velocity = ((currentGun.id != 15) ? ct.TransformDirection(x, y, 1500f) : ct.TransformDirection(x, y, 800f));
					Rigidbody rigidbody = UnityEngine.Object.Instantiate(bullet, ct.position + ct.forward, ct.rotation) as Rigidbody;
					Bullet component = rigidbody.GetComponent<Bullet>();
					component.shooter = mt;
					component.grenade = currentGun.grenade;
					if (Multiplayer.rule == 6)
					{
						component.damage = currentGun.damage * 1.5f;
					}
					else
					{
						component.damage = currentGun.damage * (1f + (float)Menu.myCharacter.attack * 0.1f);
					}
					rigidbody.gameObject.layer = base.gameObject.layer + 2;
					rigidbody.linearVelocity = velocity;
					currentGun.currentAmmo--;
					if (currentGun.currentAmmo == 0)
					{
						break;
					}
				}
				if (MyView(base.gameObject))
				{
					inputDevice.Vibrate(0.1f);
				}
				anim.SetInteger("Burst", 1);
				yield return new WaitForSeconds(0.1f);
				anim.SetInteger("Burst", 0);
				yield return new WaitForSeconds(60f / currentGun.rpm - 0.1f);
				enableFire = true;
				if (currentGun.currentAmmo <= 0)
				{
					if (Menu.network == 0)
					{
						StartCoroutine("Reload");
					}
					else if (Menu.network != 1 && base.gameObject.GetPhotonView().isMine)
					{
						base.gameObject.GetPhotonView().RPC("Reload", PhotonTargets.All);
					}
				}
				yield break;
			}
			while (true)
			{
				GameObject mf2 = UnityEngine.Object.Instantiate(currentGun.muzzleFlash, GetBulletTrailOrigin(), mt.rotation) as GameObject;
				mf2.GetComponent<ParticleSystem>().startColor = mt.GetChild(0).GetComponent<Renderer>().material.color;
				base.GetComponent<AudioSource>().PlayOneShot(currentGun.fireSE);
				float ram1 = UnityEngine.Random.Range(0f - (100f - currentGun.accuracy), 100f - currentGun.accuracy);
				float ram2 = UnityEngine.Random.Range(0f - (100f - currentGun.accuracy), 100f - currentGun.accuracy);
				Vector3 dir = ct.TransformDirection(ram1, ram2, 1500f);
				Rigidbody b = UnityEngine.Object.Instantiate(bullet, ct.position + ct.forward, ct.rotation) as Rigidbody;
				Bullet bb = b.GetComponent<Bullet>();
				bb.shooter = mt;
				bb.grenade = currentGun.grenade;
				if (Multiplayer.rule == 6)
				{
					bb.damage = currentGun.damage * 1.5f;
				}
				else
				{
					bb.damage = currentGun.damage * (1f + (float)Menu.myCharacter.attack * 0.1f);
				}
				b.gameObject.layer = base.gameObject.layer + 2;
				b.linearVelocity = dir;
				if (MyView(base.gameObject))
				{
					inputDevice.Vibrate(0.1f);
				}
				anim.SetInteger("Burst", currentBurstCount);
				currentBurstCount--;
				currentGun.currentAmmo--;
				yield return new WaitForSeconds(0.1f);
				if (currentBurstCount == 0 || currentGun.currentAmmo == 0)
				{
					break;
				}
				yield return new WaitForSeconds(60f / currentGun.rpm - 0.1f);
				yield return new WaitForSeconds(0f);
			}
			anim.SetInteger("Burst", 0);
			yield return new WaitForSeconds(60f / currentGun.rpm - 0.1f);
			enableFire = true;
			if (currentGun.currentAmmo <= 0)
			{
				if (Menu.network == 0)
				{
					StartCoroutine("Reload");
				}
				else if (Menu.network != 1 && base.gameObject.GetPhotonView().isMine)
				{
					base.gameObject.GetPhotonView().RPC("Reload", PhotonTargets.All);
				}
			}
		}
	}

	[PunRPC]
	private IEnumerator Reload()
	{
		int current = currentGun.currentAmmo;
		int max = currentGun.maxAmmo;
		int limit = currentGun.limitAmmo;
		if (current >= limit || grabbing || max <= 0 || zombie)
		{
			yield break;
		}
		if (MyView(base.gameObject) && Aiming)
		{
			Zoom(false);
		}
		enableFire = false;
		base.GetComponent<AudioSource>().PlayOneShot(reloadStartSE);
		anim.SetBool("Reload", true);
		yield return new WaitForSeconds(0.1f);
		if (currentGun.handgun)
		{
			yield return new WaitForSeconds(0.05f);
		}
		ikc.leftIK = false;
        var ammunition = Flats.Core.WeaponAmmoPolicy.Reload(current, max, limit);
        current = ammunition.Magazine;
        max = ammunition.Reserve;
		yield return new WaitForSeconds(0.5f + currentGun.reloadTime);
		if (primarySightIndex != 0)
		{
			yield return new WaitForSeconds(0.1f);
		}
		anim.SetBool("Reload", false);
		yield return new WaitForSeconds(0.5f);
		base.GetComponent<AudioSource>().PlayOneShot(reloadEndSE);
		yield return new WaitForSeconds(0.05f);
		if (!currentGun.handgun)
		{
			yield return new WaitForSeconds(0.05f);
		}
		ikc.leftIK = true;
		currentGun.currentAmmo = current;
		currentGun.maxAmmo = max;
		enableFire = true;
		yield return new WaitForSeconds(0.1f);
	}

	[PunRPC]
	private IEnumerator ChangeWeapons()
	{
		if (zombie)
		{
			yield break;
		}
		if (MyView(base.gameObject) && Aiming)
		{
			Zoom(false);
		}
		if (grabbing && grabbedObject != null)
		{
			if (Menu.network == 0)
			{
				int[] receivedData = new int[2] { 1, 0 };
				Grab(receivedData);
			}
			else if (Menu.network != 1)
			{
				int[] array = new int[2]
				{
					1,
					grabbedObject.gameObject.GetPhotonView().viewID
				};
				base.gameObject.GetPhotonView().RPC("Grab", PhotonTargets.AllBuffered, array);
			}
		}
		if (MyView(base.gameObject))
		{
			reticle.SetVisible(false);
		}
		enableFire = false;
		anim.SetBool("Change", true);
		yield return new WaitForSeconds(0.1f);
		ikc.leftIK = false;
		yield return new WaitForSeconds(0.4f);
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
		primaryWeapon = primaryWeapons.GetChild(secondaryWeaponIndex);
		secondaryWeapon = secondaryWeapons.GetChild(primaryWeaponIndex);
		int current = secondaryWeaponIndex;
		secondaryWeaponIndex = primaryWeaponIndex;
		primaryWeaponIndex = current;
		int currentSight = secondarySightIndex;
		secondarySightIndex = primarySightIndex;
		primarySightIndex = currentSight;
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
		primaryWeapon.gameObject.SetActive(true);
		secondaryWeapon.gameObject.SetActive(true);
		yield return new WaitForSeconds(0.05f);
		anim.SetBool("Change", false);
		yield return new WaitForSeconds(0.35f);
		ikc.leftIK = true;
		currentGun = primaryWeapon.GetComponent<Gun>();
		yield return new WaitForSeconds(0.1f);
		enableFire = true;
		if (MyView(base.gameObject))
		{
			reticle.SetVisible(true);
		}
		reloadPressTime = 0f;
	}

	[PunRPC]
	private IEnumerator ThrowGrenade()
	{
		if (zombie)
		{
			yield break;
		}
		base.GetComponent<AudioSource>().PlayOneShot(grenadeSE);
		if (MyView(base.gameObject) && Aiming)
		{
			Zoom(false);
		}
		if (grabbing && grabbedObject != null)
		{
			if (Menu.network == 0)
			{
				int[] receivedData = new int[2] { 1, 0 };
				Grab(receivedData);
			}
			else if (Menu.network != 1)
			{
				int[] array = new int[2]
				{
					1,
					grabbedObject.gameObject.GetPhotonView().viewID
				};
				base.gameObject.GetPhotonView().RPC("Grab", PhotonTargets.AllBuffered, array);
			}
		}
		enableFire = false;
		anim.SetBool("Grenade", true);
		yield return new WaitForSeconds(0.1f);
		ikc.leftIK = false;
		yield return new WaitForSeconds(0.4f);
		float Z = ((!(mct.localEulerAngles.x > 300f)) ? (60f - mct.localEulerAngles.x) : (370f - mct.localEulerAngles.x));
		Vector3 dir = ct.TransformDirection(0f, 0f, Z + 30f);
		Rigidbody b = UnityEngine.Object.Instantiate(grenade, ct.position + ct.forward + ct.right * -0.5f + ct.up, Quaternion.identity) as Rigidbody;
		b.GetComponent<Bullet>().shooter = mt;
		b.gameObject.layer = base.gameObject.layer + 2;
		b.GetComponent<ParticleSystem>().startColor = mt.GetChild(0).GetComponent<Renderer>().material.color;
		b.linearVelocity = dir;
		if (!currentGun.handgun)
		{
			yield return new WaitForSeconds(0.1f);
		}
		yield return new WaitForSeconds(0.3f);
		ikc.leftIK = true;
		anim.SetBool("Grenade", false);
		yield return new WaitForSeconds(0.1f);
		enableFire = true;
	}

	public Vector3 GetBulletTrailOrigin()
	{
		Vector3 muzzle = primaryWeapon.GetChild(1).position;
		if (!MyView(base.gameObject) || gunCam == null || !gunCam.enabled) return muzzle;
		// Weapons and world tracers use different cameras during ADS. Reproject
		// only the visible muzzle; projectile physics keeps the world aim ray.
		Camera worldCamera = ct.GetComponent<Camera>();
		Vector3 viewport = gunCam.WorldToViewportPoint(muzzle);
		if (worldCamera == null || !worldCamera.enabled || viewport.z <= 0f) return muzzle;
		return worldCamera.ViewportToWorldPoint(viewport);
	}

	private void UpdateAimPresentation()
	{
		if (!MyView(base.gameObject) || gunCam == null)
		{
			return;
		}
		// The world camera is also the projectile origin. Only the weapon-view
		// camera approaches the authored sight pose; ADS must not steer that ray.
		// Keep it owned by the camera rig when weapons are disabled or destroyed.
		Transform view = gunCam.transform;
		// Weapon/body clips can animate the eye's parent as well (notably
		// handguns). Preserve eye position relative to the moving player, while
		// input continues to own camera rotation and the authored weapon pose.
		if (startZooming || isZoom) ct.position = mt.TransformPoint(aimEyeLocalPosition);
		else if (!camAnim.enabled)
			ct.localPosition = Vector3.MoveTowards(ct.localPosition, Vector3.zero, Time.deltaTime * 20f);
		if (startZooming)
		{
			enableCamRotate = false;
			Transform anchor = primaryWeapon.GetChild(2);
			view.position = Vector3.MoveTowards(view.position, anchor.position, Time.deltaTime * 20f);
			view.rotation = Quaternion.RotateTowards(view.rotation, anchor.rotation, Time.deltaTime * 20f);
			if (Vector3.Distance(view.position, anchor.position) < 0.05f)
			{
				isZoom = true;
				startZooming = false;
				view.SetPositionAndRotation(anchor.position, anchor.rotation);
				enableCamRotate = true;
				fp.DOFParams.DOFBlurSize = 2f;
			}
		}
		else if (isZoom)
		{
			Transform anchor = primaryWeapon.GetChild(2);
			view.SetPositionAndRotation(anchor.position, anchor.rotation);
		}
		else if (!camAnim.enabled)
		{
			enableCamRotate = false;
			view.position = Vector3.MoveTowards(view.position, ct.position, Time.deltaTime * 20f);
			view.rotation = Quaternion.RotateTowards(view.rotation, ct.rotation, Time.deltaTime * 20f);
			if (Vector3.Distance(view.position, ct.position) < 0.05f && ct.localPosition.sqrMagnitude < 0.0001f)
			{
				ct.localPosition = Vector3.zero;
				view.localRotation = Quaternion.identity;
				view.localPosition = Vector3.zero;
				camAnim.enabled = true;
				fp.DOFParams.DOFBlurSize = 1f;
				enableCamRotate = true;
			}
		}
	}
}
