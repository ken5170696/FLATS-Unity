using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class EnemySensor : MonoBehaviour
{
	public Image damageLeft;

	public Image damageRight;

	public Image damageBack;

	public float X;

	public float Z;

	private float rot;

	private Transform mt;

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

	private void Awake()
	{
		if (!MyView(base.gameObject))
		{
			UnityEngine.Object.Destroy(this);
		}
	}

	private void Start()
	{
		mt = base.transform;
		if (!Multiplayer.end || !(Menu.gameState != "Singleplayer"))
		{
			Transform transform = GameObject.Find("UI").transform;
			damageLeft = transform.GetChild(10).GetComponent<Image>();
			damageRight = transform.GetChild(11).GetComponent<Image>();
			damageBack = transform.GetChild(12).GetComponent<Image>();
		}
	}

	private void OnDisable()
	{
		if (damageBack != null && MyView(base.gameObject))
		{
			damageLeft.enabled = false;
			damageRight.enabled = false;
			damageBack.enabled = false;
		}
	}

	private IEnumerator EnemyDirection(Vector3 dir)
	{
		// Spectators spawned after GameOver have no combat HUD bindings.
		if (Menu.gameState == "Multiplayer" && Multiplayer.end) yield break;
		X = dir.normalized.x;
		Z = dir.normalized.z;
		rot = mt.rotation.eulerAngles.y;
		if ((rot > 0f && rot < 45f) || (rot > 315f && rot < 360f))
		{
			if (X < -0.5f)
			{
				damageLeft.enabled = true;
			}
			if (X > 0.5f)
			{
				damageRight.enabled = true;
			}
			if (Mathf.Abs(X) < 0.5f && Z < 0f)
			{
				damageBack.enabled = true;
			}
		}
		else if (rot > 135f && rot < 225f)
		{
			if (X > 0.5f)
			{
				damageLeft.enabled = true;
			}
			if (X < -0.5f)
			{
				damageRight.enabled = true;
			}
			if (Mathf.Abs(X) < 0.5f && Z > 0f)
			{
				damageBack.enabled = true;
			}
		}
		else if (rot > 45f && rot < 135f)
		{
			if (Z > 0.5f)
			{
				damageLeft.enabled = true;
			}
			if (Z < -0.5f)
			{
				damageRight.enabled = true;
			}
			if (Mathf.Abs(Z) < 0.5f && X < 0f)
			{
				damageBack.enabled = true;
			}
		}
		else if (rot > 225f && rot < 315f)
		{
			if (Z < -0.5f)
			{
				damageLeft.enabled = true;
			}
			if (Z > 0.5f)
			{
				damageRight.enabled = true;
			}
			if (Mathf.Abs(Z) < 0.5f && X > 0f)
			{
				damageBack.enabled = true;
			}
		}
		yield return new WaitForSeconds(0.5f);
		if (damageLeft != null) damageLeft.enabled = false;
		if (damageRight != null) damageRight.enabled = false;
		if (damageBack != null) damageBack.enabled = false;
	}

	public EnemySensor()
	{
	}




}
