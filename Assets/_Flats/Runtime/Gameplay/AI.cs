using UnityEngine.AI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class AI : MonoBehaviour
{
	public float attackRange;

	public float trailTime;

	private float tt;

	private float runAwayDistance;

	public float defaultSpeed;

	public LayerMask mask;

	public AudioClip reloadStartSE;

	public AudioClip reloadEndSE;

	public AudioClip zombieSE;

	public List<Transform> targets;

	public Rigidbody bullet;

	public GameObject head;

	public Transform myName;

	public Transform primaryWeapons;

	public Transform secondaryWeapons;

	public Transform primaryWeapon;

	public Transform secondaryWeapon;

	public int stats_Attack;

	public int stats_Defense;

	public int primaryWeaponIndex;

	public int secondaryWeaponIndex;

	public int primarySightIndex;

	public int secondarySightIndex;

	public int team;

	public bool isPatrol;

	public bool enableFire;

	public bool zombie;

	public bool vip;

	private Gun currentGun;

	private Transform mt;

	private Transform ct;

	private Animator anim;

	private IKController ikc;

	private bool canShoot;

	private bool inSight;

	private bool lessEnemy;

	private Transform[] points;

	private int destPoint;

	private UnityEngine.AI.NavMeshAgent agent;

	private PhotonTransformView ptv;

	private bool attacked;

	private bool alert;

	private Transform closest;

	private Transform closestEnemy;

	private int startEnemyCount;

	private Vector3 lastPosition;

	private Quaternion bodyRot;

	private Quaternion camRot;

	private float rotCount;

	private float stayCount;

	private bool enableBite;

	private Vector3 netPos;

	private Quaternion netRot;

	private Vector3 netVelocity;

	// Roguelike Survival role data (set by the authority before Start; replicated in SyncTeam).
	[System.NonSerialized] public int forcedPrimaryWeapon = -1;
	[System.NonSerialized] public float roleSpeedScale = 1f;
	[System.NonSerialized] public float roleDamageScale = 1f;
	[System.NonSerialized] public float rolePreferredRange;
	[System.NonSerialized] public bool roleFlanker;
	[System.NonSerialized] public int rogueRole = -1;
	[System.NonSerialized] public int rogueInstance;
	[System.NonSerialized] public int rogueElite;

	// Attack and Search reach every copy as RPCs, and Photon starts an RPC's IEnumerator with StartCoroutine(IEnumerator), which
	// StopCoroutine("Attack") cannot stop (X004): the loops stacked and fired extra shots. They are owned here by handle instead;
	// every entry point goes through BeginAttack/BeginSearch, so at most one of each runs.
	private Coroutine attackRoutine;
	private Coroutine searchRoutine;
	private bool attackRunning;
	private bool searchRunning;

	private void Awake()
	{
        if (RoguelikeMode.Active) RogueEnemyStatus.Attach(gameObject);
        if (RoguelikeMode.Coop && Menu.network == 2) RogueEnemyNetSync.Install(gameObject);   // before the first serialization on every copy
        if (RoguelikeMode.Active) { RogueEnemyLinkTraversal.Install(gameObject); RogueEnemyTactics.Install(gameObject); }   // QA-23 drop arcs, QA-31 role movement
		mt = base.transform;
		ct = mt.Find("Camera");
		anim = GetComponent<Animator>();
		// Navigation owns world movement; imported clips otherwise overwrite the agent position.
		anim.applyRootMotion = false;
		ikc = GetComponent<IKController>();
		ptv = GetComponent<PhotonTransformView>();
		agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
		netPos = mt.position;
		netRot = mt.rotation;
		netVelocity = Vector3.zero;
	}

	private IEnumerator Start()
	{
		int network = Menu.network;
		defaultSpeed = agent.speed * Flats.Core.EnemyTuning.Speed * roleSpeedScale;
		agent.speed = defaultSpeed;
		agent.autoBraking = false;
		StartCoroutine("SyncAnimation");
		primaryWeaponIndex = forcedPrimaryWeapon >= 0 ? forcedPrimaryWeapon : UnityEngine.Random.Range(0, 14);
		secondaryWeaponIndex = UnityEngine.Random.Range(0, 16);
		if (secondaryWeaponIndex == primaryWeaponIndex)
		{
			secondaryWeaponIndex = primaryWeaponIndex + 1;
			if (secondaryWeaponIndex == 16)
			{
				secondaryWeaponIndex = 14;
			}
		}
		primarySightIndex = UnityEngine.Random.Range(0, GunInfo.zoom[primaryWeaponIndex] + 1);
		secondarySightIndex = UnityEngine.Random.Range(0, GunInfo.zoom[secondaryWeaponIndex] + 1);
		int[] sendData = new int[10] { team, primaryWeaponIndex, secondaryWeaponIndex, primarySightIndex, secondarySightIndex, stats_Attack, stats_Defense, rogueRole, rogueInstance, rogueElite };
		if (Menu.network == 0)
		{
			StartCoroutine("SyncTeam", sendData);
		}
		else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
		{
			base.gameObject.GetPhotonView().RPC("SyncTeam", PhotonTargets.AllBuffered, sendData);
		}
		GameObject wp = GameObject.Find("WayPoints");
		FlatsOfflineScores.Register(this);
		if (FlatsOfflineScores.FreeForAll)
			mask = ~((1 << LayerMask.NameToLayer("RedTeam")) | (1 << LayerMask.NameToLayer("BlueTeam")) | (1 << LayerMask.NameToLayer("Glass")) | (1 << LayerMask.NameToLayer("BulletOnly")));
		Array.Resize(ref points, wp.transform.childCount);
		for (int i = 0; i < points.Length; i++)
		{
			points[i] = wp.transform.GetChild(i).transform;
		}
		while (targets.Count <= 0)
		{
			CreateList();
			yield return new WaitForSeconds(0.1f);
		}
		if (Menu.isMaster())
		{
			closest = null;
			Transform[] array = points;
			foreach (Transform transform in array)
			{
				if (ClampAngle(Vector3.Angle(mt.forward, mt.position - transform.position)) >= 85f && !Physics.Linecast(mt.position + new Vector3(0f, 1.6f, 0f), transform.position, mask.value))
				{
					if (closest == null)
					{
						closest = transform;
					}
					else if (Vector3.Distance(mt.position, transform.transform.position) <= Vector3.Distance(mt.position, closest.transform.position))
					{
						closest = transform;
					}
				}
			}
			if (closest == null)
			{
				closest = points[0];
			}
			destPoint = Array.IndexOf(points, closest);
			if (destPoint < 0)
			{
				destPoint = 0;
			}
			else if (destPoint >= points.Length)
			{
				destPoint = points.Length - 1;
			}
			if (Menu.network == 0)
			{
				StartCoroutine("SetDestination", points[destPoint].position);
				StartCoroutine("Patrol");
			}
			else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
			{
				StartCoroutine("SetDestination", points[destPoint].position);
				var receiver = GetComponent<DamageReceiver>();
				if (base.gameObject.activeSelf && (receiver == null || !receiver.Dead))
				{
					// A dead enemy never announces a patrol; late RPCs for it land on DeadAIRpcSink.
					base.gameObject.GetPhotonView().RPC("Patrol", PhotonTargets.AllBuffered);
				}
			}
		}
		startEnemyCount = targets.Count;
		while (true)
		{
			if (Menu.isMaster() && targets.Count > 0)
			{
				if (targets.Count != startEnemyCount)
				{
					CreateList();
					yield return new WaitForEndOfFrame();
					startEnemyCount = targets.Count;
				}
				closestEnemy = null;
				foreach (Transform target in targets)
				{
					if (IsValidTarget(target))
					{
						if (closestEnemy == null)
						{
							closestEnemy = target;
						}
						else if (Vector3.Distance(mt.position, target.position) <= Vector3.Distance(mt.position, closestEnemy.position))
						{
							closestEnemy = target;
						}
					}
				}
				if (RoguelikeMode.Active)
				{
					// a planted lure pulls attention; a flanker prefers the player that is NOT the closest when there are several
					var lure = RogueHooks.LureTarget();
					if (lure != null) { targets.Remove(lure); targets.Insert(0, lure); closestEnemy = null; }
					else if (roleFlanker && closestEnemy != null && targets.Count > 1)
					{
						Transform other = null;
						foreach (Transform t in targets) if (IsValidTarget(t) && t != closestEnemy && t.tag == "Player" && (other == null || Vector3.Distance(mt.position, t.position) < Vector3.Distance(mt.position, other.position))) other = t;
						if (other != null) closestEnemy = other;
					}
				}
				if (closestEnemy != null)
				{
					targets.RemoveAt(targets.IndexOf(closestEnemy));
					targets.Insert(0, closestEnemy);
				}
			}
			yield return new WaitForSeconds(1f);
		}
	}

	[PunRPC]
	private IEnumerator SyncTeam(int[] receivedData)
	{
		team = receivedData[0];
		primaryWeaponIndex = receivedData[1];
		secondaryWeaponIndex = receivedData[2];
		int pws = receivedData[3];
		int sws = receivedData[4];
		stats_Attack = receivedData[5];
		stats_Defense = receivedData[6];
		if (receivedData.Length >= 10 && RoguelikeMode.Active) RogueHooks.OnEnemySynced(this, receivedData[7], receivedData[8], receivedData[9]);
		Transform ui = GameObject.Find("UICamera").transform;
		if (team == 0)
		{
			SkinnedMeshRenderer[] componentsInChildren = GetComponentsInChildren<SkinnedMeshRenderer>();
			SkinnedMeshRenderer[] array = componentsInChildren;
			foreach (SkinnedMeshRenderer skinnedMeshRenderer in array)
			{
				skinnedMeshRenderer.material.color = ui.GetChild(0).GetChild(5).GetChild(1)
					.GetChild(9)
					.GetComponent<Image>()
					.color;
				skinnedMeshRenderer.gameObject.layer = 8;
			}
			base.gameObject.layer = 8;
			head.layer = 8;
			mask = ~((1 << LayerMask.NameToLayer("BlueTeam")) | (1 << LayerMask.NameToLayer("Glass")) | (1 << LayerMask.NameToLayer("BulletOnly")));
		}
		else if (team == 1)
		{
			SkinnedMeshRenderer[] componentsInChildren2 = GetComponentsInChildren<SkinnedMeshRenderer>();
			SkinnedMeshRenderer[] array2 = componentsInChildren2;
			foreach (SkinnedMeshRenderer skinnedMeshRenderer2 in array2)
			{
				skinnedMeshRenderer2.material.color = ui.GetChild(0).GetChild(5).GetChild(1)
					.GetChild(7)
					.GetComponent<Image>()
					.color;
				skinnedMeshRenderer2.gameObject.layer = 9;
			}
			base.gameObject.layer = 9;
			head.layer = 9;
			mask = ~((1 << LayerMask.NameToLayer("RedTeam")) | (1 << LayerMask.NameToLayer("Glass")) | (1 << LayerMask.NameToLayer("BulletOnly")));
		}
		primaryWeapon = primaryWeapons.transform.GetChild(primaryWeaponIndex);
		secondaryWeapon = secondaryWeapons.transform.GetChild(secondaryWeaponIndex);
		primarySightIndex = pws;
		secondarySightIndex = sws;
		if (primarySightIndex != 0)
		{
			GameObject gameObject = FlatsSightTarget.Create("Sights/" + Menu.sightDictionary[primarySightIndex]);
			gameObject.transform.SetParent(primaryWeapon.GetChild(2));
			gameObject.transform.localPosition = Vector3.zero;
			gameObject.transform.localEulerAngles = new Vector3(-90f, 0f, 0f);
		}
		if (secondarySightIndex != 0)
		{
			GameObject gameObject2 = FlatsSightTarget.Create("Sights/" + Menu.sightDictionary[secondarySightIndex]);
			gameObject2.transform.SetParent(secondaryWeapon.GetChild(2));
			gameObject2.transform.localPosition = Vector3.zero;
			gameObject2.transform.localEulerAngles = new Vector3(-90f, 0f, 0f);
		}
		primaryWeapon.GetComponent<Gun>().currentAmmo = GunInfo.limitAmmo[primaryWeaponIndex];
		primaryWeapon.GetComponent<Gun>().maxAmmo = GunInfo.limitMaxAmmo[primaryWeaponIndex];
		primaryWeapons.GetChild(secondaryWeaponIndex).GetComponent<Gun>().currentAmmo = GunInfo.limitAmmo[secondaryWeaponIndex];
		primaryWeapons.GetChild(secondaryWeaponIndex).GetComponent<Gun>().maxAmmo = GunInfo.limitMaxAmmo[secondaryWeaponIndex];
		if (stats_Attack == 10 && stats_Defense == 10)
		{
			zombie = true;
			ikc.leftIK = false;
			anim.SetBool("Zombie", true);
			defaultSpeed = 15f;
			base.gameObject.name = "Zombie";
		}
		else
		{
			primaryWeapon.gameObject.SetActive(true);
			secondaryWeapon.gameObject.SetActive(true);
		}
		currentGun = primaryWeapon.GetComponent<Gun>();
		if ((Menu.network == 0 || Multiplayer.rule == 8 || RoguelikeMode.Coop) && base.gameObject.tag == "Enemy")
		{
			int num = 0;
			if (stats_Attack + stats_Defense == 0)
			{
				num = 8;
				trailTime = 10f;
				GetComponent<DamageReceiver>().score = 50;
			}
			else if (stats_Attack + stats_Defense == 2)
			{
				num = 7;
				trailTime = 12f;
				GetComponent<DamageReceiver>().score = 100;
			}
			else if (stats_Attack + stats_Defense == 4)
			{
				num = 4;
				trailTime = 14f;
				GetComponent<DamageReceiver>().score = 150;
			}
			else if (stats_Attack + stats_Defense == 6)
			{
				num = 2;
				trailTime = 16f;
				GetComponent<DamageReceiver>().score = 200;
			}
			else if (stats_Attack + stats_Defense == 8)
			{
				num = 1;
				trailTime = 18f;
				GetComponent<DamageReceiver>().score = 250;
			}
			else if (stats_Attack + stats_Defense == 10)
			{
				num = 0;
				trailTime = 20f;
				GetComponent<DamageReceiver>().score = 300;
			}
			else if (stats_Attack + stats_Defense == 20)
			{
				num = 0;
				trailTime = 50f;
				GetComponent<DamageReceiver>().score = 100;
			}
			SkinnedMeshRenderer[] componentsInChildren3 = GetComponentsInChildren<SkinnedMeshRenderer>();
			SkinnedMeshRenderer[] array3 = componentsInChildren3;
			foreach (SkinnedMeshRenderer skinnedMeshRenderer3 in array3)
			{
				if (num >= 0)
				{
					if (num == Menu.myCharacter.color)
					{
						Color color = ui.GetChild(0).GetChild(5).GetChild(1)
							.GetChild(num)
							.GetComponent<Image>()
							.color;
						skinnedMeshRenderer3.material.color = new Color(color.r * 2f / 3f, color.g * 2f / 3f, color.b * 2f / 3f);
					}
					else
					{
						skinnedMeshRenderer3.material.color = ui.GetChild(0).GetChild(5).GetChild(1)
							.GetChild(num)
							.GetComponent<Image>()
							.color;
					}
				}
				else
				{
					skinnedMeshRenderer3.material.color = Color.black;
				}
				if (zombie)
				{
					skinnedMeshRenderer3.material.color = Color.gray;
				}
				if (Menu.network == 0 && base.gameObject.layer == LayerMask.NameToLayer("RedTeam"))
				{
					skinnedMeshRenderer3.material.color = ui.GetChild(0).GetChild(5).GetChild(1)
						.GetChild(Menu.myCharacter.color)
						.GetComponent<Image>()
						.color;
					head.layer = base.gameObject.layer;
				}
				if (vip)
				{
					skinnedMeshRenderer3.material.color = ui.GetChild(0).GetChild(5).GetChild(1)
						.GetChild(11)
						.GetComponent<Image>()
						.color;
				}
			}
		}
		if (primaryWeaponIndex == 8 || primaryWeaponIndex == 9)
		{
			attackRange = 100f;
		}
		else if (primaryWeaponIndex == 10 || primaryWeaponIndex == 11)
		{
			attackRange = 200f;
		}
		if (zombie)
		{
			agent.speed = 0f;
		}
		if (vip)
		{
			runAwayDistance = 100f;
		}
		Transform pn = (Transform)UnityEngine.Object.Instantiate(myName);
		yield return new WaitForEndOfFrame();
		pn.GetComponent<InformationUI>().target = mt;
		pn.SetParent(ui.GetChild(1), false);
		pn.SetAsLastSibling();
		if (zombie)
		{
			yield return new WaitForSeconds(3f);
			enableBite = true;
			agent.speed = 20f * Flats.Core.EnemyTuning.Speed;
			defaultSpeed = agent.speed;
			attackRange = 7f;
		}
	}

	[PunRPC]
	private void SetDestination(Vector3 destination)
	{
		if (agent.isActiveAndEnabled && agent != null && isGrounded())
		{
			agent.SetDestination(destination);
		}
	}

	[PunRPC]
	private void GotoNextPoint()
	{
		if (points.Length == 0 || !isPatrol)
		{
			return;
		}
		closest = null;
		if (destPoint >= points.Length)
		{
			destPoint = 0;
		}
		int num = destPoint + 1;
		if (num >= points.Length)
		{
			num = 0;
		}
		int num2 = UnityEngine.Random.Range(0, 4);
		if (num2 == 0)
		{
			Transform[] array = points;
			foreach (Transform transform in array)
			{
				if (ClampAngle(Vector3.Angle(mt.forward, mt.position - transform.position)) >= 85f && transform != points[destPoint] && transform != points[num] && !Physics.Linecast(mt.position + new Vector3(0f, 1.6f, 0f), transform.position, mask.value))
				{
					if (closest == null)
					{
						closest = transform;
					}
					else if (Vector3.Distance(mt.position, transform.transform.position) <= Vector3.Distance(mt.position, closest.transform.position) && UnityEngine.Random.Range(0, 4) != 0)
					{
						closest = transform;
					}
				}
			}
		}
		else
		{
			destPoint = num;
		}
		if (num2 == 0 && closest != null)
		{
			destPoint = Array.IndexOf(points, closest);
		}
		else if (num2 == 0 && closest == null)
		{
			destPoint = num;
		}
		if (Menu.isMaster())
		{
			StartCoroutine("SetDestination", points[destPoint].position);
		}
	}

	[PunRPC]
	private IEnumerator Patrol()
	{
		while (true)
		{
            if (RoguelikeMode.Active && RogueEnemyStatus.Stunned(this)) { yield return null; continue; }
			targets.RemoveAll(target => !IsValidTarget(target));
			if (Menu.isMaster())
			{
				if (targets.Count > 0)
				{
					Transform transform = null;
					foreach (Transform target in targets)
					{
						if (target != null)
						{
							if (transform == null)
							{
								transform = target;
							}
							else if (Vector3.Distance(mt.position, target.position) <= Vector3.Distance(mt.position, transform.position))
							{
								transform = target;
							}
						}
					}
					if (transform != null)
					{
						targets.RemoveAt(targets.IndexOf(transform));
						targets.Insert(0, transform);
					}
				}
				if (targets.Count > 0)
				{
					if (isPatrol)
					{
						if (ClampAngle(Vector3.Angle(mt.forward, mt.position - targets[0].position)) >= 85f && CanSeeTarget(targets[0]))
						{
							if (zombie && enableBite)
							{
								if (!alert && Vector3.Distance(targets[0].position, mt.position) < 50f)
								{
									Vector3 vector = mt.position - targets[0].position;
									targets[0].SendMessage("EnemyDirection", vector, SendMessageOptions.DontRequireReceiver);
									alert = true;
								}
								if (Menu.canOpen && IsInRangeOf(targets[0]))
								{
									if (PhotonNetwork.offlineMode && Multiplayer.rule == 6)
									{
										var victim = targets[0];
										var bot = victim.GetComponent<AI>();
										if (bot != null) bot.SetOfflineZombie(true);
										else if (victim.GetComponent<FPSController>() != null && !victim.GetComponent<FPSController>().biten)
											victim.gameObject.GetPhotonView().RPC("Zombie", PhotonTargets.All, gameObject.GetPhotonView().viewID);
										CreateList();
									}
									else if (targets[0].tag == "Player")
									{
										GameObject[] gos = GameObject.FindGameObjectsWithTag("Enemy");
										if (gos != null)
										{
											GameObject[] array = gos;
											foreach (GameObject gameObject in array)
											{
												if (gameObject.layer != base.gameObject.layer)
												{
													UnityEngine.Object.Destroy(gameObject);
												}
											}
										}
										Menu.canOpen = false;
										agent.acceleration = 0f;
										agent.velocity = Vector3.zero;
										if (agent.isOnOffMeshLink && !RogueEnemyLinkTraversal.Manages(this))   // an arc it manages ends on its own (QA-23); a stop mid-link stranded it
										{
											agent.Stop();
										}
										mt.position = targets[0].position + targets[0].right * -3.2f + targets[0].forward * -1.5f;
										mt.eulerAngles = targets[0].eulerAngles + targets[0].up * 60f;
										FPSController fc = targets[0].GetComponent<FPSController>();
										Animator playerAnim = targets[0].GetComponent<Animator>();
										if (fc.isZoom)
										{
											fc.Zoom(false);
										}
										FPSController.enableCamRotate = false;
										FPSController.enableControl = false;
										targets[0].GetComponent<IKController>().enabled = false;
										playerAnim.SetFloat("Vertical", 0f);
										playerAnim.SetFloat("Horizontal", 0f);
										playerAnim.SetBool("Run", false);
										yield return new WaitForEndOfFrame();
										Transform camParent = targets[0].Find("Camera");
										Camera.main.transform.SetParent(null);
										Camera.main.GetComponent<Animator>().enabled = false;
										Camera.main.transform.position = targets[0].position + targets[0].up * 5f + targets[0].forward * 8f;
										Camera.main.transform.LookAt(targets[0].position + Vector3.up * 4f);
										anim.SetBool("ZombieAttack", true);
										yield return new WaitForSeconds(1f);
										fc.primaryWeapon.gameObject.SetActive(false);
										fc.secondaryWeapon.gameObject.SetActive(false);
										playerAnim.SetTrigger("Zombie");
										yield return new WaitForSeconds(1f);
										base.GetComponent<AudioSource>().PlayOneShot(zombieSE);
										SkinnedMeshRenderer[] smrs = targets[0].GetComponentsInChildren<SkinnedMeshRenderer>();
										try
										{
											SkinnedMeshRenderer[] array2 = smrs;
											foreach (SkinnedMeshRenderer smr in array2)
											{
												while (true)
												{
													float fadeSpeed = Time.unscaledDeltaTime;
													float targetR = Mathf.MoveTowards(smr.material.color.r, 0.5f, fadeSpeed);
													float targetG = Mathf.MoveTowards(smr.material.color.g, 0.5f, fadeSpeed);
													float targetB = Mathf.MoveTowards(smr.material.color.b, 0.5f, fadeSpeed);
													smr.material.color = new Color(targetR, targetG, targetB);
													if (smr.material.color == new Color(0.5f, 0.5f, 0.5f))
													{
														break;
													}
													yield return new WaitForSeconds(0f);
												}
											}
										}
										finally
										{
										}
										yield return new WaitForSeconds(1f);
										while (!Camera.main)
										{
											yield return new WaitForSeconds(0.1f);
										}
										Camera.main.transform.SetParent(camParent);
										Camera.main.transform.localPosition = new Vector3(0f, 0f, 0f);
										Camera.main.transform.localEulerAngles = new Vector3(0f, 0f, 0f);
										targets[0].GetComponent<DamageReceiver>().ApplyDamage(9999999f, 0, mt);
										anim.SetBool("ZombieAttack", false);
									}
									else if ((bool)targets[0].GetComponent<DamageReceiver>())
									{
										targets[0].GetComponent<DamageReceiver>().ApplyDamage(10000f, 0, mt);
									}
								}
								else
								{
									inSight = true;
									StartCoroutine("SetDestination", targets[0].position);
								}
							}
							else
							{
								if (agent.isOnOffMeshLink && !RogueEnemyLinkTraversal.Manages(this))   // an arc it manages ends on its own (QA-23); a stop mid-link stranded it
								{
									agent.Stop();
								}
								if (Menu.network == 0)
								{
									BeginAttack();
								}
								else if (Menu.network != 1 && PhotonNetwork.isMasterClient && base.gameObject.activeSelf)
								{
									base.gameObject.GetPhotonView().RPC("Attack", PhotonTargets.All);
								}
							}
						}
						if (!agent.pathPending && agent.isActiveAndEnabled && isGrounded() && agent.remainingDistance <= agent.stoppingDistance && (!agent.hasPath || agent.velocity.sqrMagnitude == 0f))
						{
							if (Menu.network == 0)
							{
								StartCoroutine("GotoNextPoint");
							}
							else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
							{
								base.gameObject.GetPhotonView().RPC("GotoNextPoint", PhotonTargets.All);
							}
						}
					}
					else if (IsInRangeOf(targets[0]) && CanSeeTarget(targets[0]))
					{
						Vector3 normalized;
						if (currentGun.zoom > 1f || currentGun.handgun)
						{
							normalized = (targets[0].position - mt.position).normalized;
						}
						else if (Vector3.Distance(mt.position, targets[0].position) > attackRange / 4f)
						{
							float num = 3.5f - stayCount;
							if (num < 0.2f)
							{
								num = 0.2f;
							}
							normalized = (targets[0].position + new Vector3(UnityEngine.Random.Range(0f - num, num), UnityEngine.Random.Range(0f - num, num), UnityEngine.Random.Range(0f - num, num)) - mt.position).normalized;
						}
						else
						{
							normalized = (targets[0].position - mt.position).normalized;
						}
						if (rotCount > 0.2f || rotCount <= 0f)
						{
							bodyRot = Quaternion.LookRotation(normalized);
							bodyRot.x = 0f;
							bodyRot.z = 0f;
							camRot = Quaternion.LookRotation(normalized);
							camRot.eulerAngles = new Vector3(camRot.eulerAngles.x + 1f, camRot.eulerAngles.y, camRot.eulerAngles.z);
							rotCount = 0f;
						}
						mt.rotation = Quaternion.Slerp(mt.rotation, bodyRot, Time.deltaTime * 20f);
						ct.rotation = Quaternion.Slerp(ct.rotation, camRot, Time.deltaTime * 20f);
						rotCount += Time.deltaTime;
						stayCount += Time.deltaTime / 2f;
						if (ClampAngle(Vector3.Angle(ct.forward, mt.position - targets[0].position)) >= 160f && targets[0].gameObject.activeSelf)
						{
							canShoot = true;
							if (Vector3.Distance(targets[0].position, mt.position) < runAwayDistance)
							{
								closest = null;
								Transform[] array3 = points;
								foreach (Transform transform2 in array3)
								{
									if (ClampAngle(Vector3.Angle(-mt.forward, mt.position - transform2.position)) >= 85f && !Physics.Linecast(mt.position + new Vector3(0f, 1.6f, 0f) - mt.forward * 10f, transform2.position, mask.value))
									{
										if (closest == null)
										{
											closest = transform2;
										}
										else if (Vector3.Distance(mt.position, transform2.transform.position) <= Vector3.Distance(mt.position, closest.transform.position))
										{
											closest = transform2;
										}
									}
								}
								if (closest == null)
								{
									closest = points[0];
								}
								destPoint = Array.IndexOf(points, closest) - 1;
								if (destPoint < 0)
								{
									destPoint = points.Length - 1;
								}
								if (Menu.isMaster())
								{
									StartCoroutine("SetDestination", points[destPoint].position);
								}
							}
						}
						else if (Menu.network == 0)
						{
							BeginSearch();
						}
						else if (Menu.network != 1 && PhotonNetwork.isMasterClient && base.gameObject.activeSelf && !searchRunning)
						{
							base.gameObject.GetPhotonView().RPC("Search", PhotonTargets.All);
						}
					}
					else if (inSight)
					{
						rotCount = 0f;
						stayCount = 0f;
						canShoot = false;
						if (Menu.network == 0)
						{
							BeginSearch();
						}
						else if (Menu.network != 1 && PhotonNetwork.isMasterClient && base.gameObject.activeSelf && !searchRunning)
						{
							base.gameObject.GetPhotonView().RPC("Search", PhotonTargets.All);
						}
					}
					else
					{
						rotCount = 0f;
						stayCount = 0f;
						canShoot = false;
					}
				}
				else
				{
					CreateList();
				}
			}
			yield return new WaitForSeconds(0f);
		}
	}

	private void CreateList()
	{
		targets = new List<Transform>();
		GameObject[] array = GameObject.FindGameObjectsWithTag("Player");
		if (array != null)
		{
			GameObject[] array2 = array;
			foreach (GameObject gameObject in array2)
			{
				if ((gameObject.layer != base.gameObject.layer || FlatsOfflineScores.FreeForAll) && IsValidTarget(gameObject.transform))
				{
					targets.Add(gameObject.transform);
				}
			}
			array = GameObject.FindGameObjectsWithTag("Enemy");
			GameObject[] array3 = array;
			foreach (GameObject gameObject2 in array3)
			{
				if (gameObject2 != base.gameObject && (gameObject2.layer != base.gameObject.layer || FlatsOfflineScores.FreeForAll) && IsValidTarget(gameObject2.transform))
				{
					targets.Add(gameObject2.transform);
				}
			}
		}
		else
		{
			StopAllCoroutines();
			ForgetRoutines();
		}
	}

	/// <summary>
	/// A target this enemy may engage (A6): present and active, not dead (Die deactivates the children and removes the root only
	/// 5 s later) and, in Roguelike, not downed. With every player down the enemy holds its fire instead of shooting the downed.
	/// </summary>
	private static bool IsValidTarget(Transform target)
	{
		if (target == null || !target.gameObject.activeInHierarchy) return false;
		DamageReceiver receiver = target.GetComponent<DamageReceiver>();
		if (receiver != null && receiver.Dead) return false;
		if (RoguelikeMode.Active)
		{
			RoguePlayer player = target.GetComponent<RoguePlayer>();
			if (player != null && player.Downed) return false;
		}
		return true;
	}

	[PunRPC]
	private IEnumerator Attack()
	{
		BeginAttack();
		yield break;
	}

	/// <summary>Enter attack mode. The offline call and the RPC both come here, so a repeated call never starts a second loop.</summary>
	private void BeginAttack()
	{
		inSight = true;
		isPatrol = false;
		if (attackRunning || !base.gameObject.activeInHierarchy) return;
		attackRunning = true;
		Coroutine routine = StartCoroutine(AttackLoop());
		if (attackRunning) attackRoutine = routine;
	}

	/// <summary>Search for a lost target. At most one runs; a repeated call while one runs is ignored.</summary>
	private void BeginSearch()
	{
		if (searchRunning || !base.gameObject.activeInHierarchy) return;
		searchRunning = true;
		Coroutine routine = StartCoroutine(SearchLoop());
		if (searchRunning) searchRoutine = routine;   // a search that ended at once (target already in view) leaves no stale handle
	}

	/// <summary>Stop the attack and search loops that BeginAttack/BeginSearch started.</summary>
	private void StopAttackRoutines()
	{
		bool wasSearching = searchRunning;
		if (attackRoutine != null) StopCoroutine(attackRoutine);
		if (searchRoutine != null) StopCoroutine(searchRoutine);
		ForgetRoutines();
		// a search that ends early restores the walking speed, as one that finishes does
		if (wasSearching && agent != null) agent.speed = defaultSpeed;
	}

	/// <summary>The loops were stopped by other means (StopAllCoroutines, deactivation): clear the handles so new ones can start.</summary>
	private void ForgetRoutines()
	{
		attackRoutine = null;
		searchRoutine = null;
		attackRunning = false;
		searchRunning = false;
	}

	private IEnumerator AttackLoop()
	{
		while (true)
		{
            if (RoguelikeMode.Active && RogueEnemyStatus.Stunned(this)) { yield return null; continue; }
			if (Menu.isMaster())
			{
				if (canShoot && enableFire && targets.Count > 0 && IsValidTarget(targets[0]))
				{
					if (Menu.network == 0)
					{
						StartCoroutine("Shoot");
					}
					else if (Menu.network != 1 && PhotonNetwork.isMasterClient && base.gameObject.activeSelf)
					{
						// Roguelike co-op: every client simulates this burst against its own copy of the players; each copy aims at the
						// target the master chose, where that player really is on that client (F26: the victim's copy used to aim with the
						// lagged replicated rotation, so the host was hit accurately while other players were missed)
						// with the master's current aim error relative to that target, so every client misses and hits alike
						var aimView = RoguelikeMode.Coop ? targets[0].GetComponent<PhotonView>() : null;
						if (aimView != null) base.gameObject.GetPhotonView().RPC("RogueShoot", PhotonTargets.All, aimView.viewID, (Quaternion.Inverse(RogueExactAim(targets[0].position)) * ct.rotation).eulerAngles);
						else base.gameObject.GetPhotonView().RPC("Shoot", PhotonTargets.All);
					}
				}
				yield return new WaitForSeconds(0.5f);
			}
			yield return new WaitForSeconds(0f);
		}
	}

	[PunRPC]
	private IEnumerator Search()
	{
		BeginSearch();
		yield break;
	}

	private IEnumerator SearchLoop()
	{
		targets.RemoveAll(target => !IsValidTarget(target));
		tt = trailTime;
		canShoot = false;
		inSight = false;
		if (agent.isActiveAndEnabled && Menu.isMaster() && targets.Count > 0)
		{
			StartCoroutine("SetDestination", targets[0].position);
		}
		while (true)
		{
            if (RoguelikeMode.Active && RogueEnemyStatus.Stunned(this)) { yield return null; continue; }
			if (Menu.isMaster())
			{
				if (targets.Count <= 0 || targets[0] == null || (CanSeeTarget(targets[0]) && IsInRangeOf(targets[0])))
				{
					break;
				}
				agent.speed = defaultSpeed * 1.5f;
				tt -= 1f;
				Vector3 dir = mt.forward - mt.up * 0.2f;
				Quaternion camRot = Quaternion.LookRotation(dir);
				camRot.eulerAngles = new Vector3(camRot.eulerAngles.x + 1f, camRot.eulerAngles.y, camRot.eulerAngles.z);
				ct.rotation = Quaternion.Slerp(ct.rotation, camRot, Time.deltaTime * 15f);
				yield return new WaitForSeconds(1f);
				if (tt <= 0f)
				{
					closest = null;
					Transform[] array = points;
					foreach (Transform transform in array)
					{
						if (ClampAngle(Vector3.Angle(mt.forward, mt.position - transform.position)) >= 85f && !Physics.Linecast(mt.position + new Vector3(0f, 1.6f, 0f), transform.position, mask.value))
						{
							if (closest == null)
							{
								closest = transform;
							}
							else if (Vector3.Distance(mt.position, transform.transform.position) <= Vector3.Distance(mt.position, closest.transform.position))
							{
								closest = transform;
							}
						}
					}
					if (closest == null)
					{
						closest = points[0];
					}
					destPoint = Array.IndexOf(points, closest) - 1;
					if (destPoint < 0)
					{
						destPoint = points.Length - 1;
					}
					if (Menu.network == 0)
					{
						if (!zombie)
						{
							StartCoroutine("SetDestination", points[destPoint].position);
							StopAttack();
						}
						else
						{
							tt = trailTime;
						}
					}
					else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
					{
						StartCoroutine("SetDestination", points[destPoint].position);
						if (PhotonNetwork.connected)
						{
							base.gameObject.GetPhotonView().RPC("StopAttack", PhotonTargets.All);
						}
					}
				}
			}
			yield return new WaitForSeconds(0f);
		}
		searchRoutine = null;
		searchRunning = false;
		tt = trailTime;
		agent.speed = defaultSpeed;
		inSight = true;
		if (agent.isOnOffMeshLink && !RogueEnemyLinkTraversal.Manages(this))   // an arc it manages ends on its own (QA-23); a stop mid-link stranded it
		{
			agent.Stop();
		}
	}

	/// <summary>One axis of a round's aim error: the gun's legacy spread (100 - accuracy), scaled in the roguelike by the role's aim
	/// relative to the rifleman at the target's distance (QA-31; RogueEnemyRole.RelativeAimSpread). Classic modes are unchanged.</summary>
	private float RogueSpreadSample()
	{
		float spread = 100f - currentGun.accuracy;
		if (RoguelikeMode.Active)
		{
			var role = GetComponent<RogueEnemyRole>();
			Transform aimed = null;
			if (rogueAimViewId >= 0) { var v = PhotonView.Find(rogueAimViewId); if (v != null) aimed = v.transform; }
			if (aimed == null && targets.Count > 0) aimed = targets[0];
			if (role != null) spread *= role.RelativeAimSpread(aimed != null ? Vector3.Distance(mt.position, aimed.position) : 60f);
		}
		return UnityEngine.Random.Range(0f - spread, spread);
	}

	/// <summary>Roguelike role movement (RogueEnemyTactics): the target this enemy is engaging, or null while it patrols or searches.</summary>
	public Transform RogueEngagedTarget { get { return !isPatrol && !searchRunning && targets.Count > 0 && IsValidTarget(targets[0]) ? targets[0] : null; } }

	[System.NonSerialized] int rogueAimViewId = -1;
	[System.NonSerialized] Quaternion rogueAimError = Quaternion.identity;

	[PunRPC]
	private void RogueShoot(int targetViewId, Vector3 aimError)
	{
		rogueAimViewId = targetViewId;
		rogueAimError = Quaternion.Euler(aimError);
		StartCoroutine("Shoot");
	}

	/// <summary>Where this enemy would point its muzzle camera at a target with no error (the master adds its own offset on top).</summary>
	private Quaternion RogueExactAim(Vector3 target)
	{
		Vector3 forward = target - mt.position;
		if (forward.sqrMagnitude < 0.01f) return ct.rotation;
		Quaternion aim = Quaternion.LookRotation(forward);
		aim.eulerAngles = new Vector3(aim.eulerAngles.x + 1f, aim.eulerAngles.y, aim.eulerAngles.z);
		return aim;
	}

	/// <summary>Roguelike co-op, clients other than the master: point the muzzle camera at the master's chosen target as it stands here.</summary>
	private void AimAtRogueTarget()
	{
		if (!RoguelikeMode.Coop || rogueAimViewId < 0 || PhotonNetwork.isMasterClient) return;
		var view = PhotonView.Find(rogueAimViewId);
		if (view == null || !view.gameObject.activeInHierarchy) return;
		ct.rotation = RogueExactAim(view.transform.position) * rogueAimError;   // the master's aim error, applied to where the target is here
	}

	[PunRPC]
	private void StopAttack()
	{
		isPatrol = true;
		inSight = false;
		canShoot = false;
		attacked = false;
		tt = trailTime;
		StopAttackRoutines();
	}

	[PunRPC]
	private IEnumerator Shoot()
	{
        if (RoguelikeMode.Active && RogueEnemyStatus.Stunned(this)) yield break;
        if (RoguelikeMode.Active && RogueKillPrediction.IsPredictedDead(gameObject)) yield break;   // this client already saw the kill (F07)
		agent.speed = defaultSpeed;
		if (currentGun.maxAmmo <= 0 && currentGun.currentAmmo <= 0)
		{
			if (Menu.network == 0)
			{
				StartCoroutine("Reload");
			}
			else if (Menu.network != 1 && PhotonNetwork.isMasterClient && base.gameObject.activeSelf)
			{
				base.gameObject.GetPhotonView().RPC("Reload", PhotonTargets.All);
			}
			yield break;
		}
		enableFire = false;
		if (anim.GetBool("Run"))
		{
			yield return new WaitForSeconds(0.4f);
		}
		Transform firePosition = primaryWeapon.GetChild(1);
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
        if (RoguelikeMode.Active) while (RogueEnemyStatus.Stunned(this)) yield return null;
        if (RoguelikeMode.Active && RogueKillPrediction.IsPredictedDead(gameObject)) { enableFire = true; yield break; }   // the kill was predicted during the wait (X005)
		if (currentGun.oneShot)
		{
			GameObject mf = UnityEngine.Object.Instantiate(currentGun.muzzleFlash, firePosition.position, mt.rotation) as GameObject;
			mf.GetComponent<ParticleSystem>().startColor = mt.GetChild(0).GetComponent<Renderer>().material.color;
			base.GetComponent<AudioSource>().PlayOneShot(currentGun.fireSE);
			for (int i = 0; i < currentGun.burstCount; i++)
			{
				AimAtRogueTarget();
				float x = RogueSpreadSample();
				float y = RogueSpreadSample();
				Vector3 velocity = ((currentGun.id != 15) ? ct.TransformDirection(x, y, 1500f) : ct.TransformDirection(x, y, 800f));
				Rigidbody rigidbody = UnityEngine.Object.Instantiate(bullet, ct.position + ct.forward, ct.rotation) as Rigidbody;
				Bullet component = rigidbody.GetComponent<Bullet>();
				component.shooter = mt;
				if (currentGun.grenade)
				{
					component.grenade = true;
				}
				float num = 1f;
				if (Menu.gameState == "Singleplayer" || Multiplayer.rule == 8 || RoguelikeMode.Coop)
				{
					if (Singleplayer.rule == 0 || Singleplayer.rule == 2 || Multiplayer.rule == 8 || RoguelikeMode.Active)
					{
						num = Flats.Core.EnemyDamageScaling.ForPopulation(RoguelikeMode.Active ? RogueHooks.EnemyPopulation() : Singleplayer.enemy);
					}
					else if (Singleplayer.rule == 1 || Singleplayer.rule == 3)
					{
						num = Flats.Core.EnemyDamageScaling.ForPopulation(Singleplayer.respawnEnemy);
					}
					if (num < 0.5f)
					{
						num = 0.5f;
					}
				}
				component.damage = currentGun.damage * (1f + (float)stats_Attack * 0.1f) * num * Flats.Core.EnemyTuning.Damage * roleDamageScale * (RoguelikeMode.Active ? RogueHooks.EnemyDamageMul() : 1f);
				rigidbody.gameObject.layer = base.gameObject.layer + 2;
				rigidbody.linearVelocity = velocity;
				currentGun.currentAmmo--;
				if (currentGun.currentAmmo == 0)
				{
					break;
				}
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
            if (RoguelikeMode.Active && RogueEnemyStatus.Stunned(this)) { yield return null; continue; }
            if (RoguelikeMode.Active && RogueKillPrediction.IsPredictedDead(gameObject)) break;   // a predicted kill ends the burst on this client (X005)
			GameObject mf2 = UnityEngine.Object.Instantiate(currentGun.muzzleFlash, firePosition.position, mt.rotation) as GameObject;
			mf2.GetComponent<ParticleSystem>().startColor = mt.GetChild(0).GetComponent<Renderer>().material.color;
			base.GetComponent<AudioSource>().PlayOneShot(currentGun.fireSE);
			AimAtRogueTarget();
			float ram1 = RogueSpreadSample();
			float ram2 = RogueSpreadSample();
			Vector3 dir = ct.TransformDirection(ram1, ram2, 1500f);
			Rigidbody b = UnityEngine.Object.Instantiate(bullet, ct.position + ct.forward, ct.rotation) as Rigidbody;
			Bullet bb = b.GetComponent<Bullet>();
			bb.shooter = mt;
			float damagePerEnemy = 1f;
			if (Menu.gameState == "Singleplayer" || Multiplayer.rule == 8 || RoguelikeMode.Coop)
			{
				if (Singleplayer.rule == 0 || Singleplayer.rule == 2 || Multiplayer.rule == 8 || RoguelikeMode.Active)
				{
					damagePerEnemy = Flats.Core.EnemyDamageScaling.ForPopulation(RoguelikeMode.Active ? RogueHooks.EnemyPopulation() : Singleplayer.enemy);
				}
				else if (Singleplayer.rule == 1 || Singleplayer.rule == 3)
				{
					damagePerEnemy = Flats.Core.EnemyDamageScaling.ForPopulation(Singleplayer.respawnEnemy);
				}
				if (damagePerEnemy < 0.5f)
				{
					damagePerEnemy = 0.5f;
				}
			}
			bb.damage = currentGun.damage * (1f + (float)stats_Attack * 0.1f) * damagePerEnemy * Flats.Core.EnemyTuning.Damage * roleDamageScale * (RoguelikeMode.Active ? RogueHooks.EnemyDamageMul() : 1f);
			b.gameObject.layer = base.gameObject.layer + 2;
			b.linearVelocity = dir;
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

	/// <summary>
	/// Hit reaction: turn toward where the attack came from. <paramref name="dir"/> is a direction, not a position: Bullet sends
	/// (shot origin - hit point) and a biting zombie sends (zombie - victim). It used to be treated as a position (X010).
	/// </summary>
	public void EnemyDirection(Vector3 dir)
	{
        if (RoguelikeMode.Active && RogueEnemyStatus.Stunned(this)) return;
		if (!attacked && Menu.isMaster())
		{
			Vector3 forward = dir;
			Vector3 flat = new Vector3(forward.x, 0f, forward.z);
			if (flat.sqrMagnitude < 0.0001f)
			{
				return;
			}
			Quaternion rotation = Quaternion.LookRotation(flat);
			Quaternion rotation2 = Quaternion.LookRotation(forward);
			rotation2.eulerAngles = new Vector3(rotation2.eulerAngles.x + 1f, rotation2.eulerAngles.y, rotation2.eulerAngles.z);
			mt.rotation = rotation;
			ct.rotation = rotation2;
			attacked = true;
		}
	}

	/// <summary>Turn toward a world position (the planted bomb in Singleplayer), with the same rules as <see cref="EnemyDirection"/>.</summary>
	public void FacePosition(Vector3 position)
	{
		EnemyDirection(position - mt.position);
	}

	[PunRPC]
	private IEnumerator Reload()
	{
		int current = currentGun.currentAmmo;
		int max = currentGun.maxAmmo;
		int limit = currentGun.limitAmmo;
		if (current >= limit)
		{
			yield break;
		}
		if (max == 0)
		{
			currentGun.maxAmmo += 30;
			max += 30;
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
		for (int i = 0; i < limit; i++)
		{
			if (max == 0)
			{
				break;
			}
			if (current >= limit)
			{
				break;
			}
			max--;
			current++;
		}
		yield return new WaitForSeconds(0.5f + currentGun.reloadTime);
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

	private void Update()
	{
		if (Menu.isMaster())
		{
			Vector3 velocity = agent.velocity;
			if (Menu.network == 2)
			{
				ptv.SetSynchronizedValues(velocity, Time.deltaTime * 5f);
			}
		}
		if (targets.Count > 0)
		{
			if (!IsValidTarget(targets[0]))
			{
				CreateList();
			}
			if (targets.Count > 0 && Menu.gameState == "Singleplayer" && Singleplayer.enemy <= 2 && !lessEnemy)
			{
				StartCoroutine("SetDestination", targets[0].position);
				lessEnemy = true;
			}
		}
	}

	private IEnumerator SyncAnimation()
	{
		while (true)
		{
			Vector3 vel = lastPosition;
			if (Menu.network != 1)
			{
				vel = mt.InverseTransformDirection(mt.position - lastPosition) / Time.deltaTime;
			}
			if (anim == null)
			{
				anim = GetComponent<Animator>();
			}
			float min = 10f;
			if (Menu.network == 0)
			{
				min = 5f;
			}
			if (Mathf.Abs(vel.z) > min && Mathf.Abs(vel.z) < 30f)
			{
				anim.SetFloat("Vertical", vel.z);
			}
			else
			{
				anim.SetFloat("Vertical", 0f);
			}
			if (Mathf.Abs(vel.x) > min && Mathf.Abs(vel.x) < 30f)
			{
				anim.SetFloat("Horizontal", vel.x);
			}
			else
			{
				anim.SetFloat("Horizontal", 0f);
			}
			if (isGrounded())
			{
				anim.SetBool("Jump", false);
				if (vel.z > 20f && !zombie)
				{
					anim.SetBool("Run", true);
				}
				else
				{
					anim.SetBool("Run", false);
				}
			}
			else
			{
				anim.SetBool("Jump", true);
				anim.SetBool("Run", false);
			}
			yield return new WaitForEndOfFrame();
			lastPosition = mt.position;
			yield return new WaitForSeconds(0f);
		}
	}

	private void OnPhotonPlayerDisconnected(PhotonPlayer pp)
	{
		if (Menu.network != 2)
		{
			return;
		}
		// the same rules as every other rebuild: the other team only, never the dead or downed (this list used to take every
		// player, teammates included, and drop the opposing bots)
		CreateList();
		if (GameObject.FindGameObjectsWithTag("Player").Length == 0)
		{
			StopAllCoroutines();
			ForgetRoutines();
		}
	}

	private void OnDisable()
	{
		// deactivating the GameObject ends every coroutine; stale handles must not block a later Attack or Search
		if (!base.gameObject.activeInHierarchy)
		{
			ForgetRoutines();
		}
	}

	private void OnDestroy()
	{
		StopAllCoroutines();
		ForgetRoutines();
	}

	private bool IsInRangeOf(Transform target)
	{
		if (target == null) return false;
		float num = Vector3.Distance(mt.position, target.position);
		float range = attackRange;
		if (RoguelikeMode.Active)
		{
			range *= RogueHooks.EnemyRangeScale();
			// a marksman that is too close keeps shooting while it backs off (RogueEnemyTactics). This used to report "not in range", which
			// started a Search, and a Search walks TOWARD the target: marksmen closed in instead of keeping their distance (QA-31).
		}
		return num < range;
	}

	private bool CanSeeTarget(Transform target)
	{
		if (target == null) return false;
		if (!Physics.Linecast(mt.position + new Vector3(0f, 3f, 0f), target.position + new Vector3(0f, 6f, 0f), mask.value))
		{
			return true;
		}
		return false;
	}

	private bool isGrounded()
	{
		return Physics.Raycast(mt.position + Vector3.up, -Vector2.up, 2f);
	}

	private static float ClampAngle(float angle)
	{
		if (angle < -360f)
		{
			angle += 360f;
		}
		if (angle > 360f)
		{
			angle -= 360f;
		}
		return angle;
	}

	public void SetOfflineZombie(bool value)
	{
		if (!PhotonNetwork.offlineMode || Multiplayer.rule != 6) return;
		zombie = value;
		enableBite = value;
		team = value ? 1 : 0;
		gameObject.layer = LayerMask.NameToLayer(value ? "BlueTeam" : "RedTeam");
		head.layer = gameObject.layer;
		mask = ~((1 << LayerMask.NameToLayer(value ? "RedTeam" : "BlueTeam")) | (1 << LayerMask.NameToLayer("Glass")) | (1 << LayerMask.NameToLayer("BulletOnly")));
		anim.SetBool("Zombie", value);
		ikc.leftIK = !value;
		if (primaryWeapon != null) primaryWeapon.gameObject.SetActive(!value);
		if (secondaryWeapon != null) secondaryWeapon.gameObject.SetActive(!value);
		attackRange = value ? 7f : 100f;
		defaultSpeed = (value ? 20f : 15f) * Flats.Core.EnemyTuning.Speed;
		agent.speed = defaultSpeed;
		GetComponent<DamageReceiver>().hitPoints = (value ? 6000f : 100f) * Flats.Core.EnemyTuning.Health;
		foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
		{
			renderer.gameObject.layer = gameObject.layer;
			renderer.material.color = value ? Color.gray : Color.red;
		}
		StopAttackRoutines();
		isPatrol = true;
		foreach (var bot in FindObjectsOfType<AI>()) bot.CreateList();
	}

	public void SetOfflineVIP(bool value)
	{
		vip = value;
		runAwayDistance = value ? 100f : 40f;
		Color color = value ? Color.white : (gameObject.layer == LayerMask.NameToLayer("RedTeam") ? Color.red : Color.blue);
		foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.material.color = color;
	}

	public AI()
	{
		attackRange = 200f;
		trailTime = 10f;
		tt = 10f;
		runAwayDistance = 40f;
		mask = -1;
		team = 1;
		isPatrol = true;
		enableFire = true;

	}




}
