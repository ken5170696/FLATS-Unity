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
			CompactIndicators();
		}
	}

	// The authored hit-direction strips are 800 units wide (a full half of the 800-unit canvas) and the rear strip 450 tall, so a
	// hit from the side washed over half the view. Narrower strips still read as a direction from the edge; sized once per session.
	static bool compacted;
	void CompactIndicators()
	{
		if (compacted) return;
		compacted = true;
		Resize(damageLeft, new Vector2(460f, 0f), new Vector2(230f, 0f));
		Resize(damageRight, new Vector2(460f, 0f), new Vector2(-230f, 0f));
		Resize(damageBack, new Vector2(0f, 260f), new Vector2(0f, 130f));
	}
	static void Resize(Image image, Vector2 size, Vector2 position)
	{
		if (image == null) return;
		var rect = image.rectTransform;
		rect.sizeDelta = size; rect.anchoredPosition = position;
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

	private static int feedbackOverlay = -1;
	/// <summary>The combat feedback overlay prefab exists in this build (checked once).</summary>
	private static bool FeedbackOverlayPresent
	{
		get
		{
			if (feedbackOverlay < 0) feedbackOverlay = Resources.Load<GameObject>(CombatFeedbackView.ResourcePath) != null ? 1 : 0;
			return feedbackOverlay == 1;
		}
	}

	private IEnumerator EnemyDirection(Vector3 dir)
	{
		// Spectators spawned after GameOver have no combat HUD bindings.
		if (Menu.gameState == "Multiplayer" && Multiplayer.end) yield break;
		// the combat feedback overlay draws the hit direction as arcs around the crosshair (DamageDirectionIndicator, QA-35); the old
		// left/right/back gradients would show the same hit twice, so they remain only as the fallback when that prefab is missing
		if (FeedbackOverlayPresent) yield break;
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
