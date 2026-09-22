using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class FPSPlayerControl : MonoBehaviour
{
	public AudioClip gunSound;

	public AudioClip reload;

	public AudioClip needReload;

	public ParticleSystem shellParticle;

	public GameObject muzzleEffect;

	public GameObject impactEffect;

	public Text armoText;

	private bool inJump;

	private float jumpStart;

	private bool inFire;

	private bool inReload;

	private Animator anim;

	private int armoCount;

	private void Awake()
	{
		anim = GetComponentInChildren<Animator>();
	}

	private void Update()
	{
		if (ETCInput.GetButton("Fire") && !inFire && armoCount > 0 && !inReload)
		{
			inFire = true;
			anim.SetBool("Shoot", true);
			InvokeRepeating("GunFire", 0.12f, 0.12f);
			GunFire();
		}
		if (ETCInput.GetButtonDown("Fire") && armoCount == 0 && !inReload)
		{
			base.GetComponent<AudioSource>().PlayOneShot(needReload);
		}
		if (ETCInput.GetButtonUp("Fire"))
		{
			anim.SetBool("Shoot", false);
			muzzleEffect.SetActive(false);
			inFire = false;
			CancelInvoke();
		}
		if (ETCInput.GetButtonDown("Reload"))
		{
			inReload = true;
			base.GetComponent<AudioSource>().PlayOneShot(reload);
			anim.SetBool("Reload", true);
			StartCoroutine(Reload());
		}
		if (ETCInput.GetButtonDown("Back"))
		{
			base.transform.Rotate(Vector3.up * 180f);
		}
		if (ETCInput.GetButtonDown("Jump"))
		{
			inJump = true;
			jumpStart = base.transform.position.y;
		}
		if (inJump && base.transform.position.y - jumpStart < 3f)
		{
			GetComponent<CharacterController>().Move(Vector3.up * 0.5f);
		}
		else
		{
			inJump = false;
		}
		armoText.text = armoCount.ToString();
	}

	public void MoveStart()
	{
		anim.SetBool("Move", true);
	}

	public void MoveStop()
	{
		anim.SetBool("Move", false);
	}

	public void GunFire()
	{
		if (armoCount > 0)
		{
			muzzleEffect.transform.Rotate(Vector3.forward * UnityEngine.Random.Range(0f, 360f));
			muzzleEffect.transform.localScale = new Vector3(UnityEngine.Random.Range(0.1f, 0.2f), UnityEngine.Random.Range(0.1f, 0.2f), 1f);
			muzzleEffect.SetActive(true);
			StartCoroutine(Flash());
			base.GetComponent<AudioSource>().PlayOneShot(gunSound);
			shellParticle.Emit(1);
			Vector3 position = new Vector3(Screen.width / 2, Screen.height / 2, 0f);
			position += new Vector3(UnityEngine.Random.Range(-10, 10), UnityEngine.Random.Range(-10, 10), 0f);
			Ray ray = Camera.main.ScreenPointToRay(position);
			RaycastHit[] array = Physics.RaycastAll(ray);
			if (array.Length > 0)
			{
				UnityEngine.Object.Instantiate(impactEffect, array[0].point - array[0].normal * -0.2f, Quaternion.identity);
			}
		}
		else
		{
			anim.SetBool("Shoot", false);
			muzzleEffect.SetActive(false);
			inFire = false;
		}
		armoCount--;
		if (armoCount < 0)
		{
			armoCount = 0;
		}
	}

	public void TouchPadSwipe(bool value)
	{
		ETCInput.SetControlSwipeIn("FreeLookTouchPad", value);
	}

	private IEnumerator Flash()
	{
		yield return new WaitForSeconds(0.08f);
		muzzleEffect.SetActive(false);
	}

	private IEnumerator Reload()
	{
		yield return new WaitForSeconds(0.5f);
		armoCount = 30;
		inReload = false;
		anim.SetBool("Reload", false);
	}

	public FPSPlayerControl()
	{
		armoCount = 30;

	}




}
