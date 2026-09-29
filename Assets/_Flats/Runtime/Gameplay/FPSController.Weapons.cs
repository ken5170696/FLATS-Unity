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
		if (zoom && RoguelikeMode.Active && !RogueHooks.MetaCanAim(this)) return;   // armory weapons that cannot aim
		if (!Aiming && zoom)
		{
			if (primarySightIndex == 0)
			{
				reticle.SetVisible(false);
			}
			startZooming = true;
			aimEyeLocalPosition = mt.InverseTransformPoint(ct.position);
			camAnim.enabled = false;
			// A scope.view module enlarges only the local player's aimed lens image;
			// the presenter restores the sight when aiming ends.
			if (primarySightIndex != 0 && MyView(base.gameObject))
			{
				ScopeViewPresenter.Apply(primaryWeapon.GetChild(2), Menu.sightDictionary[primarySightIndex], gunCam);
			}
		}
		else if (Aiming && !zoom)
		{
			ScopeViewPresenter.Restore();
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
		if (RoguelikeMode.Active && RogueHooks.MeleeBlocked(this))
		{
			yield break;
		}
        if (RoguelikeMode.Active && RogueMelee.Handles(this)) { yield return RogueMelee.Swing(this); yield break; }
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
			enableFire = false; firing = false;
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
				enableFire = true; firing = false;
			}
		}
	}

	[PunRPC]
	private IEnumerator Shoot()
	{
        if (RoguelikeMode.Active && RogueMelee.BlocksFire(this)) yield break;
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
			enableFire = false; firing = true;
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
			float rogueInterval = 1f;
			if (RoguelikeMode.Active) { float rogueWarm = RogueHooks.MetaPreFireDelay(this); if (rogueWarm > 0f) yield return new WaitForSeconds(rogueWarm); }
			if (currentGun.oneShot)
			{
				GameObject mf = UnityEngine.Object.Instantiate(currentGun.muzzleFlash, GetBulletTrailOrigin(), mt.rotation) as GameObject;
				mf.GetComponent<ParticleSystem>().startColor = mt.GetChild(0).GetComponent<Renderer>().material.color;
				base.GetComponent<AudioSource>().PlayOneShot(currentGun.fireSE);
				int pellets = currentGun.burstCount + (RoguelikeMode.Active ? RogueHooks.ExtraPellets(this, currentGun.id) : 0);
				float spreadScale = RoguelikeMode.Active && Aiming ? RogueHooks.AimSpreadMul(this) : 1f;
				for (int i = 0; i < pellets; i++)
				{
					var rogueShot = RoguelikeMode.Active ? RogueHooks.MetaShot(this, Aiming, i == 0) : Flats.Core.Roguelike.ShotModifiers.Neutral;
					if (i == 0) rogueInterval = (float)rogueShot.IntervalMul;
					float x = UnityEngine.Random.Range(0f - (100f - currentGun.accuracy), 100f - currentGun.accuracy) * spreadScale * (float)rogueShot.SpreadMul;
					float y = UnityEngine.Random.Range(0f - (100f - currentGun.accuracy), 100f - currentGun.accuracy) * spreadScale * (float)rogueShot.SpreadMul;
					Vector3 velocity = ((currentGun.id != 15) ? ct.TransformDirection(x, y, 1500f) : ct.TransformDirection(x, y, 800f));
					Rigidbody rigidbody = UnityEngine.Object.Instantiate(bullet, ct.position + ct.forward, ct.rotation) as Rigidbody;
					Bullet component = rigidbody.GetComponent<Bullet>();
					component.shooter = mt;
                    if (RoguelikeMode.Active) RogueRangedStatus.Capture(component);
					component.grenade = currentGun.grenade;
					if (Multiplayer.rule == 6)
					{
						component.damage = currentGun.damage * 1.5f;
					}
					else
					{
						component.damage = currentGun.damage * (1f + (float)Menu.myCharacter.attack * 0.1f) * (RoguelikeMode.Active ? RogueHooks.PlayerDamageMul(this) * (currentGun.grenade ? RogueHooks.GrenadeDamageMul(this) : 1f) : 1f) * (float)rogueShot.DamageMul;
					}
					rigidbody.gameObject.layer = base.gameObject.layer + 2;
					rigidbody.linearVelocity = velocity;
					if (RoguelikeMode.Active) RogueHooks.MetaStampBullet(this, component);
					if (i >= currentGun.burstCount) continue;   // Choke's extra pellet rides on the same shell
					if (!(RoguelikeMode.Active && (RogueHooks.InfiniteAmmo(this) || rogueShot.FreeRound))) currentGun.currentAmmo--;
					if (currentGun.currentAmmo == 0)
					{
						break;
					}
				}
				if (MyView(base.gameObject))
				{
					FlatsGamepad.Vibrate(inputDevice, 0.1f);
				}
				if (RoguelikeMode.Active) { RogueHooks.MetaNoteInterval(this, 60f / currentGun.rpm * rogueInterval); }
				anim.SetInteger("Burst", 1);
				yield return new WaitForSeconds(0.1f * Mathf.Min(1f, rogueInterval));
				anim.SetInteger("Burst", 0);
				yield return new WaitForSeconds(Mathf.Max(0f, 60f / currentGun.rpm * rogueInterval - 0.1f * Mathf.Min(1f, rogueInterval)));
				enableFire = true; firing = false;
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
				var rogueShot = RoguelikeMode.Active ? RogueHooks.MetaShot(this, Aiming, currentBurstCount == currentGun.burstCount) : Flats.Core.Roguelike.ShotModifiers.Neutral;
				rogueInterval = (float)rogueShot.IntervalMul;
				if (RoguelikeMode.Active) RogueHooks.MetaNoteInterval(this, 60f / currentGun.rpm * rogueInterval);
				GameObject mf2 = UnityEngine.Object.Instantiate(currentGun.muzzleFlash, GetBulletTrailOrigin(), mt.rotation) as GameObject;
				mf2.GetComponent<ParticleSystem>().startColor = mt.GetChild(0).GetComponent<Renderer>().material.color;
				base.GetComponent<AudioSource>().PlayOneShot(currentGun.fireSE);
				float aimSpread = RoguelikeMode.Active && Aiming ? RogueHooks.AimSpreadMul(this) : 1f;
				float ram1 = UnityEngine.Random.Range(0f - (100f - currentGun.accuracy), 100f - currentGun.accuracy) * aimSpread * (float)rogueShot.SpreadMul;
				float ram2 = UnityEngine.Random.Range(0f - (100f - currentGun.accuracy), 100f - currentGun.accuracy) * aimSpread * (float)rogueShot.SpreadMul;
				Vector3 dir = ct.TransformDirection(ram1, ram2, 1500f);
				Rigidbody b = UnityEngine.Object.Instantiate(bullet, ct.position + ct.forward, ct.rotation) as Rigidbody;
				Bullet bb = b.GetComponent<Bullet>();
				bb.shooter = mt;
                if (RoguelikeMode.Active) RogueRangedStatus.Capture(bb);
				bb.grenade = currentGun.grenade;
				if (Multiplayer.rule == 6)
				{
					bb.damage = currentGun.damage * 1.5f;
				}
				else
				{
					bb.damage = currentGun.damage * (1f + (float)Menu.myCharacter.attack * 0.1f) * (RoguelikeMode.Active ? RogueHooks.PlayerDamageMul(this) : 1f) * (float)rogueShot.DamageMul;
				}
				b.gameObject.layer = base.gameObject.layer + 2;
				b.linearVelocity = dir;
				if (RoguelikeMode.Active) RogueHooks.MetaStampBullet(this, bb);
				if (MyView(base.gameObject))
				{
					FlatsGamepad.Vibrate(inputDevice, 0.1f);
				}
				anim.SetInteger("Burst", currentBurstCount);
				currentBurstCount--;
				if (!(RoguelikeMode.Active && (RogueHooks.InfiniteAmmo(this) || rogueShot.FreeRound))) currentGun.currentAmmo--;
				yield return new WaitForSeconds(0.1f * Mathf.Min(1f, rogueInterval));
				if (currentBurstCount == 0 || currentGun.currentAmmo == 0)
				{
					break;
				}
				{ float rogueGap = Mathf.Max(0f, 60f / currentGun.rpm * rogueInterval - 0.1f * Mathf.Min(1f, rogueInterval)); if (rogueGap > 0f || !RoguelikeMode.Active) yield return new WaitForSeconds(rogueGap); }
				if (!RoguelikeMode.Active) yield return new WaitForSeconds(0f);
			}
			anim.SetInteger("Burst", 0);
			yield return new WaitForSeconds(Mathf.Max(0f, 60f / currentGun.rpm * rogueInterval - 0.1f * Mathf.Min(1f, rogueInterval)));
			enableFire = true; firing = false;
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
		if (RoguelikeMode.Active) RogueHooks.OnReloadStarted(this, current, limit);
		if (MyView(base.gameObject) && Aiming)
		{
			Zoom(false);
		}
		enableFire = false; firing = false;
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
		yield return new WaitForSeconds((0.5f + currentGun.reloadTime) * (RoguelikeMode.Active ? RogueHooks.ReloadTimeMul(this) : 1f));
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
		if (RoguelikeMode.Active) max = Mathf.Min(currentGun.limitMaxAmmo, max + RogueHooks.ReserveReturnOnReload(this));
		currentGun.currentAmmo = current;
		currentGun.maxAmmo = max;
		if (RoguelikeMode.Active) RogueHooks.MetaReloadCompleted(this, current, currentGun.limitAmmo);
		enableFire = true; firing = false;
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
		enableFire = false; firing = false;
		float rogueSwap = RoguelikeMode.Active ? RogueHooks.MetaSwapTimeMul(this) : 1f;   // Quick Draw, Quick Hands, heavy weapons
		anim.SetBool("Change", true);
		yield return new WaitForSeconds(0.1f);
		ikc.leftIK = false;
		yield return new WaitForSeconds(0.4f * rogueSwap);
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
		yield return new WaitForSeconds(0.35f * rogueSwap);
		ikc.leftIK = true;
		currentGun = primaryWeapon.GetComponent<Gun>();
		yield return new WaitForSeconds(0.1f);
		enableFire = true; firing = false;
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
		enableFire = false; firing = false;
		anim.SetBool("Grenade", true);
		yield return new WaitForSeconds(0.1f);
		ikc.leftIK = false;
		yield return new WaitForSeconds(0.4f);
		float Z = ((!(mct.localEulerAngles.x > 300f)) ? (60f - mct.localEulerAngles.x) : (370f - mct.localEulerAngles.x));
		Vector3 dir = ct.TransformDirection(0f, 0f, Z + 30f);
		Rigidbody b = UnityEngine.Object.Instantiate(grenade, ct.position + ct.forward + ct.right * -0.5f + ct.up, Quaternion.identity) as Rigidbody;
		b.GetComponent<Bullet>().shooter = mt;
		if (RoguelikeMode.Active) b.GetComponent<Bullet>().damage *= RogueHooks.GrenadeDamageMul(this);
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
		enableFire = true; firing = false;
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
			float rogueAds = RoguelikeMode.Active ? RogueHooks.MetaAdsSpeed(this) : 1f;   // aim-in time of skills, weapon and sight
			view.position = Vector3.MoveTowards(view.position, anchor.position, Time.deltaTime * rogueAds * 20f);
			view.rotation = Quaternion.RotateTowards(view.rotation, anchor.rotation, Time.deltaTime * rogueAds * 20f);
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
