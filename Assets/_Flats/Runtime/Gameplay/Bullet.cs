using System;
using System.Collections;
using UnityEngine;
using Vectrosity;
[DefaultExecutionOrder(100)]
public class Bullet : MonoBehaviour
{
	public float damage;

	public Material trailMaterial;

	[Tooltip("Trail opacity from the tail (0) to the bullet (1).")]
	public AnimationCurve trailAlpha = AnimationCurve.Linear(0f, 0f, 1f, 1f);

	[Tooltip("Evenly spaced segments used to draw the trail fade.")]
	[Range(2, 64)]
	public int trailSegments = 16;

	public UnityEngine.Object hitEffect;

	public UnityEngine.Object grenadeHitEffect;

	public Transform shooter;

	public AudioClip hitWallSE;

	public AudioClip bulletWind;

	private Transform mt;

	private Vector3 startPosition;

	private Color shooterColor;

	private bool played;

	private VectorLine trail;
	private readonly System.Collections.Generic.List<Vector3> trailPath = new System.Collections.Generic.List<Vector3>();
	private Color32[] trailColors;
	private bool trailOriginPending;

	public bool grenade;

	public bool hand;

	private float radius;

	private float dist;

	private bool grenadeHit;

	private void Awake()
	{
		mt = base.transform;
		startPosition = mt.position;
	}

	private IEnumerator Start()
	{
		if (shooter == null)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			yield break;
		}
		if (shooter.gameObject.activeSelf)
		{
			// A dead shooter's CharacterController may already be destroyed.
			var shooterCollider = shooter.GetComponent<Collider>();
			if (shooterCollider != null) Physics.IgnoreCollision(base.GetComponent<Collider>(), shooterCollider);
			if (FlatsOfflineScores.FreeForAll)
				foreach (var ownCollider in shooter.GetComponentsInChildren<Collider>())
					Physics.IgnoreCollision(GetComponent<Collider>(),ownCollider);
		}
		shooterColor = shooter.GetChild(0).GetComponent<Renderer>().material.color;
		trail = new VectorLine("Trail", new Vector3[0], trailMaterial, 2f, LineType.Continuous, Joins.Fill);
		VectorLine.canvas3D.gameObject.layer = LayerMask.NameToLayer("Default");
		if (shooter.tag == "Player")
		{
			trailPath.Add(shooter.GetComponent<FPSController>().primaryWeapon.GetChild(1).position);
			trailOriginPending = !grenade && !hand;
		}
		else
		{
			trailPath.Add(shooter.GetComponent<AI>().primaryWeapon.GetChild(1).position);
		}
		float waitTime = 4f;
		if (grenade)
		{
			while (true)
			{
				if (grenadeHit)
				{
					if (hand)
					{
						yield return new WaitForSeconds(0.5f);
					}
					base.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
					Vector3 explosionPos = base.transform.position;
					Collider[] colliders = Physics.OverlapSphere(layerMask: (LayerMask)((1 << LayerMask.NameToLayer("RedTeam")) + (1 << LayerMask.NameToLayer("BlueTeam"))), position: explosionPos, radius: radius);
					GameObject he = UnityEngine.Object.Instantiate(grenadeHitEffect, explosionPos, Quaternion.identity) as GameObject;
					he.GetComponent<ParticleSystem>().startColor = shooter.GetChild(0).GetComponent<Renderer>().material.color;
					Collider[] array = colliders;
					foreach (Collider collider in array)
					{
						if (Physics.Linecast(explosionPos, collider.transform.position + Vector3.up, 0))
						{
							continue;
						}
						DamageReceiver damageReceiver = ((!(collider.gameObject.name == "CameraTarget")) ? collider.gameObject.GetComponent<DamageReceiver>() : collider.transform.parent.parent.parent.parent.parent.parent.gameObject.GetComponent<DamageReceiver>());
						if ((bool)damageReceiver)
						{
							float num = damage * (1f + (float)Menu.myCharacter.attack * 0.1f) - Vector3.Distance(explosionPos, collider.transform.position) * dist;
							if (collider.gameObject.layer != shooter.gameObject.layer || (FlatsOfflineScores.FreeForAll && collider.transform.root != shooter.root))
							{
								damageReceiver.ApplyDamage(num, 0, shooter);
							}
						}
					}
					UnityEngine.Object.Destroy(base.gameObject);
				}
				else
				{
					waitTime -= Time.deltaTime;
					if (waitTime < 0f)
					{
						break;
					}
				}
				yield return new WaitForSeconds(0f);
			}
		}
		if (!grenade || !base.GetComponent<Collider>().enabled)
		{
			yield break;
		}
		base.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
		Vector3 position = base.transform.position;
		LayerMask layerMask = (1 << LayerMask.NameToLayer("RedTeam")) + (1 << LayerMask.NameToLayer("BlueTeam"));
		Collider[] array2 = Physics.OverlapSphere(position, radius, layerMask);
		GameObject gameObject = UnityEngine.Object.Instantiate(grenadeHitEffect, position, Quaternion.identity) as GameObject;
		gameObject.GetComponent<ParticleSystem>().startColor = shooter.GetChild(0).GetComponent<Renderer>().material.color;
		Collider[] array3 = array2;
		foreach (Collider collider2 in array3)
		{
			if (Physics.Linecast(position, collider2.transform.position + Vector3.up, 0))
			{
				continue;
			}
			DamageReceiver damageReceiver2 = ((!(collider2.gameObject.name == "CameraTarget")) ? collider2.gameObject.GetComponent<DamageReceiver>() : collider2.transform.parent.parent.parent.parent.parent.parent.gameObject.GetComponent<DamageReceiver>());
			if ((bool)damageReceiver2)
			{
				float num2 = damage * (1f + (float)Menu.myCharacter.attack * 0.1f) - Vector3.Distance(position, collider2.transform.position) * dist;
				if (collider2.gameObject.layer != shooter.gameObject.layer || (FlatsOfflineScores.FreeForAll && collider2.transform.root != shooter.root))
				{
					damageReceiver2.ApplyDamage(num2, 0, shooter);
				}
			}
		}
		UnityEngine.Object.Destroy(base.gameObject);
	}

	private void OnCollisionEnter(Collision col)
	{
        // A shooter's controller may already be removed by the death sequence.
        if (shooter == null) { UnityEngine.Object.Destroy(gameObject); return; }
        if (col.transform.root == shooter.root) return;
        FPSController playerShooter = shooter.GetComponent<FPSController>();
		if (trail != null)
		{
			trailPath.Add(mt.position);
		}
		ContactPoint contactPoint = col.contacts[0];
		if (hand || col.gameObject.layer == LayerMask.NameToLayer("Default") || col.gameObject.layer == LayerMask.NameToLayer("Glass") || col.gameObject.layer == LayerMask.NameToLayer("BulletOnly"))
		{
			if (grenade)
			{
				grenadeHit = true;
			}
			GameObject gameObject = UnityEngine.Object.Instantiate(hitEffect, contactPoint.point, Quaternion.identity) as GameObject;
			if (shooter != null)
			{
				gameObject.GetComponent<ParticleSystem>().startColor = shooter.GetChild(0).GetComponent<Renderer>().material.color;
			}
		}
		else
		{
			if ((shooter != null && shooter.gameObject.layer == col.gameObject.layer && !FlatsOfflineScores.FreeForAll) || col.gameObject == null)
			{
				return;
			}
			if (col.gameObject.name != "CameraTarget" || playerShooter == null)
			{
				Vector3 vector = startPosition - contactPoint.point;
				if ((bool)col.transform.root.GetComponent<Collider>())
				{
					col.transform.root.GetComponent<Collider>().BroadcastMessage("EnemyDirection", vector, SendMessageOptions.DontRequireReceiver);
				}
				DamageReceiver component = col.gameObject.GetComponent<DamageReceiver>();
				if ((bool)component)
				{
					component.ApplyDamage(damage, 0, shooter);
				}
				else if (col.gameObject.name == "PhaseSkipper")
				{
					col.collider.BroadcastMessage("ApplyDamage", SendMessageOptions.DontRequireReceiver);
				}
				base.GetComponent<Collider>().enabled = false;
				if (Singleplayer.rule == 2 && shooter.tag == "Player")
				{
					Menu.currentHeadshotScore -= 10;
					if (Menu.currentHeadshotScore < 0)
					{
						Menu.currentHeadshotScore = 0;
					}
				}
			}
			else if (col.gameObject.name == "CameraTarget")
			{
				Vector3 vector2 = startPosition - contactPoint.point;
				if ((bool)col.transform.root.GetComponent<Collider>())
				{
					col.transform.root.GetComponent<Collider>().BroadcastMessage("EnemyDirection", vector2, SendMessageOptions.DontRequireReceiver);
				}
				if (playerShooter.primaryWeapon != null)
                    damage *= playerShooter.primaryWeapon.GetComponent<Gun>().headshotBonus;
				DamageReceiver component2 = col.collider.GetComponentInParent<DamageReceiver>();
				if ((bool)component2)
				{
					GameObject gameObject2 = component2.gameObject;
					if (component2.hitPoints - damage <= 0f && gameObject2.tag == "Enemy")
					{
						component2.ApplyDamage(damage, 1, shooter);
					}
					else
					{
						component2.ApplyDamage(damage, 0, shooter);
					}
				}
				base.GetComponent<Collider>().enabled = false;
			}
		}
		if (!grenade && shooter != null)
		{
			GameObject gameObject3 = UnityEngine.Object.Instantiate(hitEffect, contactPoint.point, Quaternion.identity) as GameObject;
			gameObject3.GetComponent<ParticleSystem>().startColor = shooter.GetChild(0).GetComponent<Renderer>().material.color;
		}
		else
		{
			if (!grenade || hand || !(shooter != null))
			{
				return;
			}
			base.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
			Vector3 position = base.transform.position;
			LayerMask layerMask = (1 << LayerMask.NameToLayer("RedTeam")) + (1 << LayerMask.NameToLayer("BlueTeam"));
			Collider[] array = Physics.OverlapSphere(contactPoint.point, radius, layerMask);
			GameObject gameObject4 = UnityEngine.Object.Instantiate(grenadeHitEffect, contactPoint.point, Quaternion.identity) as GameObject;
			gameObject4.GetComponent<ParticleSystem>().startColor = shooter.GetChild(0).GetComponent<Renderer>().material.color;
			Collider[] array2 = array;
			foreach (Collider collider in array2)
			{
				if (Physics.Linecast(contactPoint.point, collider.transform.position + Vector3.up, 0))
				{
					continue;
				}
				DamageReceiver damageReceiver = ((!(collider.gameObject.name == "CameraTarget")) ? collider.gameObject.GetComponent<DamageReceiver>() : collider.transform.parent.parent.parent.parent.parent.parent.gameObject.GetComponent<DamageReceiver>());
				if ((bool)damageReceiver)
				{
					float num = damage * (1f + (float)Menu.myCharacter.attack * 0.1f) - Vector3.Distance(position, collider.transform.position) * dist;
					if (collider.gameObject.layer != shooter.gameObject.layer)
					{
						damageReceiver.ApplyDamage(num, 0, shooter);
					}
				}
			}
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	private void Update()
	{
		if (shooter == null || Camera.main == null)
		{
			return;
		}
		if (shooter != null && !played && mt != null && Camera.main.gameObject != null)
		{
			bool flag = false;
			if (Menu.network == 0)
			{
				if (shooter.tag == "Enemy")
				{
					flag = true;
				}
			}
			else if (Menu.network != 1 && !shooter.gameObject.GetPhotonView().isMine)
			{
				flag = true;
			}
			if (flag && Vector3.Distance(mt.position, Camera.main.transform.position) < 4f)
			{
				base.GetComponent<AudioSource>().pitch = UnityEngine.Random.Range(0.8f, 1.5f);
				base.GetComponent<AudioSource>().volume = 0.2f;
				base.GetComponent<AudioSource>().PlayOneShot(bulletWind);
				played = true;
			}
		}
	}

	private void LateUpdate()
	{
		if (shooter == null || Camera.main == null) return;
		if (trail != null)
		{
			// Resolve once after the weapon camera reaches this frame's authored
			// pose. Keep old segments fixed in world space after the shot.
			if (trailOriginPending)
			{
				var player = shooter.GetComponent<FPSController>();
				if (player != null) trailPath[0] = player.GetBulletTrailOrigin();
				trailOriginPending = false;
			}
			if ((bool)mt && Time.timeScale != 0f)
			{
				trailPath.Add(mt.position);
			}
			if (trailPath.Count > 8 && !hand)
			{
				trailPath.RemoveAt(trailPath.Count - 2);
			}
			if (Camera.main.cullingMask != 0)
			{
				VectorLine.SetCamera3D(Camera.main);
			}
			ResampleTrail();
			trail.continuousTexture = true;
			trail.drawDepth = 2;
			trail.smoothColor = true;
			trail.SetColors(trailColors);
			trail.Draw3D();
		}
	}

	// The recorded path has one long segment up to the bullet. Redistribute it
	// evenly so the opacity ramp spans the whole visible trail.
	private void ResampleTrail()
	{
		int segments = Mathf.Max(2, trailSegments);
		var points = trail.points3;
		points.Clear();
		float total = 0f;
		for (int i = 1; i < trailPath.Count; i++) total += Vector3.Distance(trailPath[i - 1], trailPath[i]);
		int source = 1;
		float walked = 0f;
		for (int i = 0; i <= segments; i++)
		{
			if (trailPath.Count < 2 || total <= 0f)
			{
				points.Add(trailPath[trailPath.Count - 1]);
				continue;
			}
			float target = total * i / segments;
			while (source < trailPath.Count - 1 && walked + Vector3.Distance(trailPath[source - 1], trailPath[source]) < target)
			{
				walked += Vector3.Distance(trailPath[source - 1], trailPath[source]);
				source++;
			}
			float length = Vector3.Distance(trailPath[source - 1], trailPath[source]);
			float t = length > 0f ? Mathf.Clamp01((target - walked) / length) : 1f;
			points.Add(Vector3.Lerp(trailPath[source - 1], trailPath[source], t));
		}
		if (trailColors == null || trailColors.Length != segments)
		{
			trailColors = new Color32[segments];
		}
		// With smoothColor, segment 0 is flat and segment i blends entry i-1 to i.
		for (int i = 0; i < segments; i++)
		{
			Color c = shooterColor;
			c.a *= Mathf.Clamp01(trailAlpha.Evaluate(i / (segments - 1f)));
			trailColors[i] = c;
		}
	}

	private void OnDestroy()
	{
		VectorLine.Destroy(ref trail);
	}

	public Bullet()
	{
		damage = 20f;
		radius = 15f;
		dist = 30f;

	}




}
