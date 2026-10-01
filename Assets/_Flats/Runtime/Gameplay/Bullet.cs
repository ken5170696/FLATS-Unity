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
	public Vector3 StartPosition { get { return startPosition; } }

	private Color shooterColor;

	private bool played;

	private VectorLine trail;
	private readonly System.Collections.Generic.List<Vector3> trailPath = new System.Collections.Generic.List<Vector3>();
	private Color32[] trailColors;
	private bool trailOriginPending;

	public bool grenade;

	public bool hand;
	// Roguelike Survival effect chain: 0 direct, 1 chain, 2 homing, 3 explosion, 4 ricochet, 5 penetrate (Flats.Core.Roguelike.DamageKind).
	[System.NonSerialized] public int rogueKind;
	[System.NonSerialized] public int rogueDepth;
	[System.NonSerialized] public string rogueRootShot;
	[System.NonSerialized] public string rogueTrigger;   // Roguelike: the trigger pull this round belongs to (suppression stacks once per pull)
	[System.NonSerialized] public int rogueWeaponModel = -1;       // meta: armory weapon model that fired this round
	[System.NonSerialized] public Flats.Core.Roguelike.FreshRound rogueFreshRound;   // meta: Fresh Magazine token of this round, shared by its pellets (QA-32)
	private float radius;

	private float dist;

	private bool grenadeHit;

	// OnCollisionEnter explodes a launcher round at once while the Start loop can see grenadeHit in the same frame: one blast only.
	private bool exploded;

	// Enemy rounds that strike a head: the gun's headshot bonus, capped low. Player guns run 1.2x-5x and a player head hit on an
	// enemy is an instant kill; a bot sniper at 5x would erase a full-health player, so a bot's head hit is a sting, not a one-shot.
	public const float EnemyHeadshotMulCap = 1.5f;

	// World geometry that shields from a blast. Characters (team layers), bullets, corpses (BulletOnly) and breakable windows
	// (Glass, which a blast shatters) do not; the same mask RogueHooks.Combat uses for kill-explosion and chain line of sight.
	private static int explosionBlockMask = -1;
	private static int ExplosionBlockMask { get { if (explosionBlockMask < 0) explosionBlockMask = LayerMask.GetMask("Default"); return explosionBlockMask; } }

	private void Awake()
	{
		mt = base.transform;
		startPosition = mt.position;
		if (string.IsNullOrEmpty(rogueRootShot)) rogueRootShot = GetInstanceID().ToString();
	}

	private IEnumerator Start()
	{
        if (RoguelikeMode.Active) RogueRangedStatus.Capture(this);
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
				if (exploded)
				{
					yield break;
				}
				if (grenadeHit)
				{
					if (hand)
					{
						yield return new WaitForSeconds(0.5f);
					}
					Explode(base.transform.position, base.transform.position);
					yield break;
				}
				waitTime -= Time.deltaTime;
				if (waitTime < 0f)
				{
					break;
				}
				yield return new WaitForSeconds(0f);
			}
		}
		if (!grenade || !base.GetComponent<Collider>().enabled)
		{
			yield break;
		}
		Explode(base.transform.position, base.transform.position);
	}

	/// <summary>The one blast of a grenade (hand grenade timer, launcher round on impact). Every DamageReceiver in range with a clear
	/// line through world geometry to one of its colliders takes the damage once, falling off with the distance to its nearest such
	/// collider and never below zero. The thrower's attack is already in <see cref="damage"/> (FPSController and AI fire code), so
	/// it is not applied again here: the old blast multiplied by the local Menu.myCharacter.attack, which counted a player's attack
	/// twice and gave enemy grenades the victim machine's attack instead of the thrower's replicated stat tier.</summary>
	private void Explode(Vector3 center, Vector3 sightFrom)
	{
		if (exploded)
		{
			return;
		}
		exploded = true;
		var body = base.GetComponent<Rigidbody>();
		if (body != null) body.linearVelocity = Vector3.zero;
		if (shooter == null)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		GameObject effect = UnityEngine.Object.Instantiate(grenadeHitEffect, center, Quaternion.identity) as GameObject;
		if (effect != null) effect.GetComponent<ParticleSystem>().startColor = shooter.GetChild(0).GetComponent<Renderer>().material.color;
		LayerMask teams = (1 << LayerMask.NameToLayer("RedTeam")) + (1 << LayerMask.NameToLayer("BlueTeam"));
		var nearest = new System.Collections.Generic.Dictionary<DamageReceiver, float>();
		foreach (Collider collider in Physics.OverlapSphere(center, radius, teams))
		{
			if (collider == null) continue;
			DamageReceiver receiver = collider.gameObject.name == "CameraTarget" ? collider.GetComponentInParent<DamageReceiver>() : collider.gameObject.GetComponent<DamageReceiver>();
			if (receiver == null || receiver.Dead) continue;
			if (collider.gameObject.layer == shooter.gameObject.layer && !(FlatsOfflineScores.FreeForAll && collider.transform.root != shooter.root)) continue;
			// the victim's own parts never shield it; anything else on the world mask does
			RaycastHit wall;
			if (Physics.Linecast(sightFrom, collider.bounds.center, out wall, ExplosionBlockMask, QueryTriggerInteraction.Ignore) && wall.collider != null && wall.collider.transform.root != collider.transform.root) continue;
			float distance = Vector3.Distance(center, collider.transform.position);
			float known;
			if (!nearest.TryGetValue(receiver, out known) || distance < known) nearest[receiver] = distance;
		}
		foreach (var pair in nearest)
		{
			if (pair.Key == null) continue;
			float amount = damage - pair.Value * dist;
			if (!(amount > 0f)) continue;   // the edge of the blast deals nothing; a negative amount used to heal
			pair.Key.ApplyDamage(amount, 0, shooter);
			if (RoguelikeMode.Active && !pair.Key.userIsPlayer) RogueRangedStatus.OnHit(this, pair.Key, false);
		}
		UnityEngine.Object.Destroy(base.gameObject);
	}

	/// <summary>Headshot multiplier for an AI round (the shooter's current gun, capped at <see cref="EnemyHeadshotMulCap"/>).</summary>
	private float EnemyHeadshotMul()
	{
		AI ai = shooter != null ? shooter.GetComponent<AI>() : null;
		Gun gun = ai != null && ai.primaryWeapon != null ? ai.primaryWeapon.GetComponent<Gun>() : null;
		// a round still in flight after its shooter died: the lowest bonus any gun has
		return gun != null ? Mathf.Clamp(gun.headshotBonus, 1f, EnemyHeadshotMulCap) : 1.2f;
	}

	private void OnCollisionEnter(Collision col)
	{
        // A shooter's controller may already be removed by the death sequence.
        if (shooter == null) { UnityEngine.Object.Destroy(gameObject); return; }
        if (exploded) return;   // a grenade that already went off only waits for its Destroy
        if (col.transform.root == shooter.root) return;
        FPSController playerShooter = shooter.GetComponent<FPSController>();
		if (trail != null)
		{
			trailPath.Add(mt.position);
		}
		ContactPoint contactPoint = col.contacts[0];
		// QA-44: an enemy round that hits a carried enemy body ends there; the carrier behind it takes nothing from it
		if (RoguelikeMode.Active && playerShooter == null && !grenade && RogueBodyShield.TryAbsorb(this, col))
		{
			base.GetComponent<Collider>().enabled = false;
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		if (RoguelikeMode.Active && playerShooter != null && !grenade && RogueHooks.OnBulletHitWorld(this, col))
		{
			base.GetComponent<Collider>().enabled = false;
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}		if (hand || col.gameObject.layer == LayerMask.NameToLayer("Default") || col.gameObject.layer == LayerMask.NameToLayer("Glass") || col.gameObject.layer == LayerMask.NameToLayer("BulletOnly"))
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
			if (RoguelikeMode.Active && playerShooter != null && !grenade && RogueHooks.TryRicochet(this, col))
			{
				base.GetComponent<Collider>().enabled = false;
				UnityEngine.Object.Destroy(base.gameObject);
				return;
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
				// an AI round on a head collider (CameraTarget carries no DamageReceiver): the character above it takes a moderate
				// head hit, never the instant-kill flag. It goes through ApplyBulletDamage like a body hit, so the victim-owner rules
				// (NetworkDamage to the owner in Classic multiplayer; the owner's copy in co-op) and the Roguelike player intake
				// (downed, shields, damage taken) apply unchanged.
				bool aiHead = col.gameObject.name == "CameraTarget";   // a player's head hit takes the branch below
				DamageReceiver component = aiHead ? col.collider.GetComponentInParent<DamageReceiver>() : col.gameObject.GetComponent<DamageReceiver>();
				if ((bool)component)
				{
					float aiHeadMul = aiHead ? EnemyHeadshotMul() : 1f;
					// Fresh Magazine: the round's first direct enemy hit counts as a headshot (asked only for that hit; pellets share the round)
					bool rogueHead = RoguelikeMode.Active && rogueFreshRound != null && rogueKind == 0 && playerShooter != null && !component.userIsPlayer && RogueMetaRuntime.FreshHeadshot(this, false);
					if (rogueHead && playerShooter.primaryWeapon != null) damage *= Mathf.Min(playerShooter.primaryWeapon.GetComponent<Gun>().headshotBonus, (float)Flats.Core.Roguelike.BuildStats.FreshMagazineMaxMul);   // capped forced headshot
					if (RoguelikeMode.Active && playerShooter != null && !component.userIsPlayer) { damage *= RogueHooks.HitDamageMul(this, rogueHead, contactPoint.point); damage *= RogueHooks.MetaHitMul(this, component, rogueHead, contactPoint.point, damage); damage = RogueHooks.MetaExecute(this, component, damage, rogueHead); }
					if (RoguelikeMode.Active && playerShooter == null && component.userIsPlayer) damage *= RogueHooks.EnemyShotgunRangeMul(this, contactPoint.point);   // QA-49: enemy pellets fall off at range, never gain up close
					if (RoguelikeMode.Active && damage > 0f && (Menu.network == 0 || (shooter != null && shooter.GetComponent<PhotonView>() != null && shooter.GetComponent<PhotonView>().isMine)))
						component.RogueReactToHit(shooter, rogueHead);
					// Roguelike (solo and co-op): a head hit always reports the flag and the receiver decides lethality from the damage
					// after mitigation (shield front, Guardian last stand, invulnerable cores); raw damage used to kill through them in solo
					component.ApplyBulletDamage(damage * aiHeadMul, rogueKind != 0 ? -1 : (rogueHead && (component.hitPoints - damage <= 0f || RoguelikeMode.Active) && component.gameObject.tag == "Enemy" ? 1 : 0), shooter);
					if (RoguelikeMode.Active && playerShooter != null && !component.userIsPlayer) { RogueHooks.OnBulletHitEnemy(this, component, damage, rogueHead); RogueHooks.TryPenetrate(this, col); }
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
				// a natural head hit is the round's first hit too: Fresh Magazine's conversion is spent on it (QA-32)
				if (RoguelikeMode.Active && rogueFreshRound != null && rogueKind == 0 && component2 != null && !component2.userIsPlayer) RogueMetaRuntime.FreshHeadshot(this, true);
				if (RoguelikeMode.Active && component2 != null && !component2.userIsPlayer) { damage *= RogueHooks.HitDamageMul(this, true, contactPoint.point); damage *= RogueHooks.MetaHitMul(this, component2, true, contactPoint.point, damage); damage = RogueHooks.MetaExecute(this, component2, damage, true); }
				if ((bool)component2)
				{
					GameObject gameObject2 = component2.gameObject;
					bool derived = rogueKind != 0;
					if (RoguelikeMode.Active && damage > 0f && (Menu.network == 0 || (shooter != null && shooter.GetComponent<PhotonView>() != null && shooter.GetComponent<PhotonView>().isMine)))
						component2.RogueReactToHit(shooter, !derived);
					// roguelike (solo and co-op): the flag only reports the hit part; the receiver (the master in co-op) decides lethality
					// from its own hit points after mitigation. Classic keeps the instant kill when the raw damage is lethal.
					if ((component2.hitPoints - damage <= 0f || RoguelikeMode.Active) && gameObject2.tag == "Enemy" && !derived)
					{
						component2.ApplyBulletDamage(damage, 1, shooter);
					}
					else
					{
						component2.ApplyBulletDamage(damage, derived ? -1 : 0, shooter);
					}
					if (RoguelikeMode.Active && !component2.userIsPlayer) { RogueHooks.OnBulletHitEnemy(this, component2, damage, !derived); RogueHooks.TryPenetrate(this, col); }
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
			// the contact normal points away from the struck surface: sight lines start just off it, not inside the wall
			Explode(contactPoint.point, contactPoint.point + contactPoint.normal * 0.25f);
		}
	}

	// QA-42: homing steers in the physics step. A round is spawned inside a coroutine, and the physics step moved it 30-60 m before its
	// first Update, so an Update-time steer came too late to bend it onto the target.
	private void FixedUpdate()
	{
		if (RoguelikeMode.Active && shooter != null) RogueHooks.SteerHoming(this, GetComponent<Rigidbody>());
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
