using System;
using System.Collections;
using ExitGames.Client.Photon;
using UnityEngine;
using UnityEngine.UI;
public class DamageReceiver : MonoBehaviour
{
	public static bool invincibility;

	/// <summary>QA-29: when the local player's latest timed protection ends (Time.time) and how long it was, written by every timed
	/// source (spawn 3 s, revive 2 s, Guardian Angel); the HUD badge reads the exact time left. Grant extends, never shortens.</summary>
	public static float invincibilityUntil = -10f, invincibilityLength;

	/// <summary>Records a timed protection of <paramref name="seconds"/> starting now (the flag itself stays with its owner).</summary>
	public static void NoteInvincibility(float seconds)
	{
		if (!(seconds > 0f)) return;
		float until = Time.time + seconds;
		if (until > invincibilityUntil) { invincibilityUntil = until; invincibilityLength = seconds; }
	}

	public bool userIsPlayer;

	public float hitPoints;

	public GameObject effectCamera;

	public GameObject deadReplacement;

	public AudioClip damageSE;

	public int score;

	private Transform mt;

	private Transform ct;

	private Transform ui;

	private FPSController myFPSController;

	private AI myAI;

	private GameObject multiplayer;

	private Image damageEffect;

	private Scrollbar healthbar;

	private bool died;
	public bool Dead { get { return died; } }

	// Bullet calls this before reporting damage, so local feedback never waits for the master.
	// QA-05: a hit the enemy cannot take (an invulnerable finale core, a Guardian's last stand) only flashes; a shield bearer hit from the
	// front flinches less with a blue flash; every other hit flashes and flinches (RogueHitReaction merges pellets and bursts).
	public void RogueReactToHit(Transform source, bool headshot)
	{
		if (!RoguelikeMode.Active || userIsPlayer || died || !isActiveAndEnabled) return;
		var reaction = GetComponent<RogueHitReaction>();
		if (reaction == null) reaction = gameObject.AddComponent<RogueHitReaction>();
		Vector3 travel = source != null ? transform.position - source.position : -transform.forward;
		var role = GetComponent<RogueEnemyRole>();
		var affixes = GetComponent<RogueEliteAffixes>();
		if ((role != null && role.Invulnerable) || (affixes != null && affixes.GuardianActive)) { reaction.Blocked(travel); return; }
		// E4 (QA-37): the crosshair hit tick for the local player's own hits, the same marker a drone or a device shows; the enemy plays
		// its own hit sound, so no cue here. A kill turns it red from Die.
		if (LocalPlayerSource(source)) ReportLocalHit(false, headshot);
		if (role != null && role.ShieldFacing(source)) { reaction.Hit(travel, headshot, 0, RogueHitReaction.ShieldedStrength, true); return; }
		reaction.Hit(travel, headshot);
	}

	/// <summary>The source is the local player's own character (solo, or the owner's copy in co-op).</summary>
	private static bool LocalPlayerSource(Transform source)
	{
		if (source == null || source.GetComponent<FPSController>() == null) return false;
		if (Menu.network == 0) return true;
		var view = source.GetComponent<PhotonView>();
		return view != null && view.isMine;
	}

	private static void ReportLocalHit(bool kill, bool headshot)
	{
		try { CombatFeedbackView.ReportHit(kill, headshot, false); }
		catch (Exception e) { Debug.LogException(e); }
		FlatsFeel.LocalHit(kill, headshot);   // Roguelike only (both callers): the headshot ring, the kill sound, the hit-stop
	}

	// QA-05: a teammate's copy shows the hit on that player's body (every client simulates enemy rounds against every player copy). The
	// owner's own first-person body is hidden and gets no camera shake here; a downed or dead player gets nothing; absorbed damage (shield,
	// melee guard) only flashes.
	private void RogueRemotePlayerHit(Transform source, float damage)
	{
		if (!RoguelikeMode.Active || !userIsPlayer || died || !isActiveAndEnabled || MyView(base.gameObject)) return;
		var rp = GetComponent<RoguePlayer>();
		if (rp != null && rp.Downed) return;
		var reaction = GetComponent<RogueHitReaction>();
		if (reaction == null) { reaction = gameObject.AddComponent<RogueHitReaction>(); reaction.PlayerBody = true; }
		Vector3 travel = source != null ? transform.position - source.position : -transform.forward;
		if (damage > 0f) reaction.Hit(travel, false);
		else reaction.Blocked(travel);
	}

	// QA-35: the local player took damage from a known source; the HUD's hit-direction indicator points at it. Self damage (own grenade,
	// gas, falls, kill volumes) has no direction and reports nothing.
	private void ReportDamageDirection(float damage, Transform source)
	{
		if (!userIsPlayer || !(damage > 0f) || source == null || source.root == base.transform.root) return;
		try { DamageDirectionIndicator.Report(source.position, damage); }
		catch (Exception e) { Debug.LogException(e, this); }
		if (RoguelikeMode.Active) FlatsFeel.LocalHurt(damage);
	}

	private string command;

	private Transform shooter;

	private Transform killer;

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
		mt = base.transform;
		if (died) yield break;   // a Die RPC beat Start on this copy (player or enemy): nothing to initialise, Die bound what it needs
		if (userIsPlayer)
		{
			myFPSController = GetComponent<FPSController>();
			if (myFPSController == null)
			{
				// a player object without its controller cannot run the owner loop below; report it once instead of throwing every frame
				Debug.LogError("DamageReceiver: player object '" + base.name + "' has no FPSController", this);
				yield break;
			}
			// the camera only feeds the kill camera; Die falls back to Camera.main when it is missing
			if (myFPSController.myCamera != null)
			{
				ct = myFPSController.myCamera.transform;
			}
		}
		else if (MyView(base.gameObject))
		{
			// its own coroutine: a moment without a main camera (the owner died and the view is switching) must not hold back
			// the health set below
			StartCoroutine(BindOwnedEnemyCamera());
		}
		if (Menu.network != 0)
		{
			multiplayer = GameObject.Find("MultiplayerController");
		}
		while (Multiplayer.end && !(Menu.gameState == "Singleplayer"))
		{
			yield return new WaitForSeconds(0f);
		}
		if (died) yield break;
		if (userIsPlayer && MyView(base.gameObject))
		{
			// the HUD is optional here: without it the player still gets health, spawn protection and regeneration
			GameObject uiObject = GameObject.Find("UI");
			ui = uiObject != null ? uiObject.transform : null;
			Transform effectNode = ui != null ? ui.Find("DamageEffect") : null;
			Transform healthNode = ui != null ? ui.Find("Healthbar") : null;
			damageEffect = effectNode != null ? effectNode.GetComponent<Image>() : null;
			healthbar = healthNode != null ? healthNode.GetComponent<Scrollbar>() : null;
			hitPoints = RogueHooks.PlayerMaxHealth(this, 1000f * (1f + (float)Menu.myCharacter.defense * 0.1f));
			if (damageEffect != null)
			{
				damageEffect.color = new Color(1f, 1f, 1f, 0f);
			}
			if (healthbar != null)
			{
				healthbar.size = 1f;
			}
			invincibility = true;
			NoteInvincibility(3f);
			yield return new WaitForSeconds(3f);
			invincibility = false;
		}
		// Other actors spawning must not end the local player's protection, so the static
		// flag is only cleared by its owners (spawn timer, kill camera, round changes).
		if (!userIsPlayer)
		{
			myAI = GetComponent<AI>();
			if (myAI == null)
			{
				Debug.LogError("DamageReceiver: enemy object '" + base.name + "' has no AI", this);
				yield break;
			}
			hitPoints = RogueHooks.EnemyMaxHealth(this, 1000f * (1f + (float)myAI.stats_Defense * 0.1f) * Flats.Core.EnemyTuning.Health);
		}
		while (true)
		{
			if (userIsPlayer && died)
			{
				break;   // also without a health bar: a dead player neither regenerates nor reads its removed controller
			}
			if (userIsPlayer && MyView(base.gameObject))
			{
				float num = RogueHooks.PlayerMaxHealth(this, 1000f * (1f + (float)Menu.myCharacter.defense * 0.1f));
				if (myFPSController.zombie)
				{
					num = 6000f;
				}
				if (myFPSController.motherZombie)
				{
					num = 10000f;
				}
				if (hitPoints <= num * 0.99f)
				{
					hitPoints += 100f * Time.deltaTime;
					if (myFPSController.zombie)
					{
						hitPoints += 120f * Time.deltaTime;
					}
					if (myFPSController.motherZombie)
					{
						hitPoints += 120f * Time.deltaTime;
					}
				}
				else
				{
					hitPoints = num;
				}
				Color color = default(Color);
				float num2 = hitPoints / num;
				if (hitPoints < num)
				{
					color = new Color(1f, 1f, 1f, 1f - num2);
				}
				if (color.a > 0.9f)
				{
					color = new Color(1f, 1f, 1f, 1f);
				}
				if ((bool)damageEffect)
				{
					damageEffect.color = color;
				}
				if ((bool)healthbar)
				{
					if (died)
					{
						break;
					}
					if (healthbar.gameObject.activeSelf)
					{
						healthbar.size = num2;
					}
				}
			}
			yield return new WaitForSeconds(0f);
		}
		if (healthbar != null)
		{
			healthbar.size = 0f;
		}
	}

	// An owned enemy can spawn while no main camera is enabled (the owner died and the view is switching). The camera only feeds
	// the kill camera, so this waits for one without holding Start back, and stops when the enemy is dead: Die takes Camera.main itself.
	private IEnumerator BindOwnedEnemyCamera()
	{
		Camera main = Camera.main;
		while (main == null)
		{
			yield return null;
			if (died)
			{
				yield break;
			}
			main = Camera.main;
		}
		if (ct == null)
		{
			ct = main.transform.parent;
		}
	}

	// The match controller is bound in Start; a Die RPC can reach a copy before that, and the controller can be gone while a
	// scene closes. Kill logs and scores are optional: without it they are skipped and the death itself continues.
	private PhotonView MultiplayerView()
	{
		if (multiplayer == null && Menu.network != 0)
		{
			multiplayer = GameObject.Find("MultiplayerController");
		}
		PhotonView view = multiplayer != null ? multiplayer.GetPhotonView() : null;
		return view != null ? view : null;
	}

	private static string OwnerName(GameObject go)
	{
		PhotonView view = go != null ? go.GetPhotonView() : null;
		return view != null && view.owner != null ? view.owner.NickName : "Flatman";
	}

	// The local player's ragdoll carries the respawn camera as its fifth child. A HUD or a ragdoll without those parts must not
	// stop Die: the rest of the death (kill log, weapon drop, counters) still has to run.
	private void ShowRespawnView(GameObject corpse)
	{
		if (healthbar != null)
		{
			healthbar.size = 0f;
		}
		if (corpse.transform.childCount <= 4)
		{
			Debug.LogError("DamageReceiver: the dead replacement of '" + base.name + "' has no respawn child", corpse);
			return;
		}
		Transform respawnRoot = corpse.transform.GetChild(4);
		respawnRoot.gameObject.SetActive(true);
		Respawn respawn = respawnRoot.GetComponent<Respawn>();
		if (respawn != null)
		{
			respawn.original = base.gameObject;
		}
		else
		{
			Debug.LogError("DamageReceiver: the respawn child of '" + corpse.name + "' has no Respawn component", corpse);
		}
		if (!Menu.VRmode && respawnRoot.childCount > 0)
		{
			BlurEffect blur = respawnRoot.GetChild(0).GetComponent<BlurEffect>();
			if (blur != null)
			{
				blur.enabled = true;
			}
		}
	}

	// Destroy(myAI) completes at the end of the frame; adding the sink before then
	// would give Photon two methods with the same RPC name.
	private void AddDeadAIRpcSink()
	{
		if (GetComponent<DeadAIRpcSinkInstaller>() == null) base.gameObject.AddComponent<DeadAIRpcSinkInstaller>();
	}

	[PunRPC]
	private void NetworkDamage(int[] receivedData)
	{
		// End-of-match spectator respawns intentionally do not initialize combat UI.
		if (Menu.gameState == "Multiplayer" && Multiplayer.end) return;
		if (died)
		{
			return;
		}
		// Invincibility protects players (spawn, kill camera), as in ApplyDamage; a round change protects everyone.
		if (!base.gameObject.activeSelf || (invincibility && userIsPlayer) || Multiplayer.roundChanging)
		{
			return;
		}
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1)
		{
			// the shooter can have left or been destroyed while this report travelled: the accepted damage still counts,
			// only its attribution is dropped (every use of shooter and killer below allows null)
			PhotonView shooterView = PhotonView.Find(receivedData[2]);
			shooter = shooterView != null ? shooterView.transform : null;
		}
		if ((userIsPlayer && !MyView(base.gameObject)) || hitPoints <= 0f)
		{
			return;
		}
		if (userIsPlayer) ReportDamageDirection(receivedData[0], shooter);   // Classic multiplayer: the victim's owner applies the hit here (QA-35)
		// The shooter's local Bullet already played this hit. Only the remote authority needs a fallback.
		if (RoguelikeMode.Active && receivedData[0] > 0 && (shooter == null || !MyView(shooter.gameObject)))
			RogueReactToHit(shooter, receivedData[1] == 1);
		if (receivedData[1] == 1 && !userIsPlayer && (myAI == null || !myAI.vip) && RoguelikeMode.Coop)   // myAI binds in Start; a hit can arrive earlier
		{
			float headDamage = RogueHooks.ModifyIncomingDamage(this, receivedData[0], shooter);
			hitPoints -= headDamage;
			if (hitPoints > 0f) { killer = shooter; return; }
			command = "head";
			killer = shooter;
			base.gameObject.GetPhotonView().RPC("Die", PhotonTargets.All, receivedData[2]);
			return;
		}
		if (receivedData[1] == 1 && !userIsPlayer && (myAI == null || !myAI.vip) && (Menu.network == 0 || Multiplayer.rule == 8))
		{
			command = "head";
			killer = shooter;
			if (Menu.network == 0)
			{
				Die(receivedData[2]);
			}
			else if (Menu.network != 1)
			{
				base.gameObject.GetPhotonView().RPC("Die", PhotonTargets.All, receivedData[2]);
			}
			return;
		}
		hitPoints -= RoguelikeMode.Active ? RogueHooks.ModifyIncomingDamage(this, receivedData[0], shooter) : receivedData[0];
		if (hitPoints <= 0f)
		{
			command = "normal";
			killer = shooter;
			if (Menu.network == 0)
			{
				Die(receivedData[2]);
			}
			else if (Menu.network != 1)
			{
				base.gameObject.GetPhotonView().RPC("Die", PhotonTargets.All, receivedData[2]);
			}
		}
		// the legacy 1-in-60 "mortal" instant kill is a Classic rule; in the roguelike it killed invulnerable cores and Guardian elites
		else if (!userIsPlayer && (myAI == null || !myAI.vip) && !RoguelikeMode.Active && (Menu.network == 0 || Multiplayer.rule == 8))
		{
			int num = 0;
			num = ((!Singleplayer.chance) ? UnityEngine.Random.Range(0, 60) : UnityEngine.Random.Range(0, 12));
			if (num == 0 && receivedData[1] != -1)
			{
				command = "mortal";
				killer = shooter;
				if (Menu.network == 0)
				{
					Die(receivedData[2]);
				}
				else if (Menu.network != 1)
				{
					base.gameObject.GetPhotonView().RPC("Die", PhotonTargets.All, receivedData[2]);
				}
			}
		}
		else
		{
			killer = shooter;
		}
	}

	/// <summary>Roguelike bleed-out: the authority already ruled the death; no hit, shield, invincibility or downed check applies.</summary>
	public void RogueForceDie()
	{
		if (died || !userIsPlayer) return;
		command = "normal";
		killer = mt;
		if (Menu.network == 0) Die(0);
		else if (Menu.network != 1 && base.gameObject.GetPhotonView().isMine) base.gameObject.GetPhotonView().RPC("Die", PhotonTargets.All, 0);
	}

	public void ApplyDamage(float damage, int headshot, Transform shooter)
	{
		ApplyDamageInternal(damage, headshot, shooter, false);
	}

	public void ApplyBulletDamage(float damage, int headshot, Transform shooter)
	{
		ApplyDamageInternal(damage, headshot, shooter, true);
	}

	private void ApplyDamageInternal(float damage, int headshot, Transform shooter, bool reactionPlayed)
	{
        if (RoguelikeMode.Active) damage = RogueMelee.Incoming(this, damage, shooter, reactionPlayed);
        if (RoguelikeMode.Active && RogueMeleeAuthority.Route(this, damage, headshot, shooter)) return;
		if (Menu.gameState == "Multiplayer" && Multiplayer.end) return;
		// zero, negative (a blast's far edge) or NaN damage is no hit: it must never heal. The static flag protects the local player; in the
		// roguelike a teammate's copy keeps receiving (cosmetic) hits, so its hit feedback does not vanish while this player is protected.
		if ((invincibility && userIsPlayer && (!RoguelikeMode.Active || MyView(base.gameObject))) || !(damage > 0f) || died)
		{
			return;
		}
		if (RoguelikeMode.Active && damage > 0f && !reactionPlayed && (Menu.network == 0 || (shooter != null && MyView(shooter.gameObject))))
			RogueReactToHit(shooter, headshot == 1);
		if (base.GetComponent<AudioSource>().enabled && shooter != null && shooter.gameObject.tag == "Player" && MyView(shooter.gameObject))
		{
			base.GetComponent<AudioSource>().PlayOneShot(damageSE);
		}
		if (RoguelikeMode.Coop && !userIsPlayer && Menu.network != 0 && Menu.network != 1)
		{
			// Roguelike co-op enemies: resolve the hit on the shooter's client (as Classic co-op does) so the kill is
			// instant; the Die RPC carries the shooter so the authority can attribute the bounty. Role rules (shield
			// facing, invulnerable finale cores) come from RogueHooks; invulnerability is replicated by the authority.
			if (hitPoints <= 0f) return;
			float rogueDamage = RogueHooks.ModifyIncomingDamage(this, damage, shooter);
			hitPoints -= rogueDamage;
            if (RoguelikeMode.Active) RogueMeleeAuthority.PublishHit(this, rogueDamage, shooter);
			killer = shooter;
			if (hitPoints > 0f) return;
			command = headshot == 1 ? "head" : "normal";
			var shooterView = shooter != null ? shooter.gameObject.GetPhotonView() : null;
			// packed: shooter viewID * 2 + headshot flag, so every copy (the authority included) settles the same kill the same way
			base.gameObject.GetPhotonView().RPC("Die", PhotonTargets.All, (shooterView != null ? shooterView.viewID : 0) * 2 + (headshot == 1 ? 1 : 0));
			PhotonNetwork.SendOutgoingCommands();   // the kill leaves now, not on the next send tick (F07)
			return;
		}
		if (Menu.network == 0 || Multiplayer.rule == 8 || (RoguelikeMode.Coop && userIsPlayer))
		{
			if (hitPoints <= 0f)
			{
				return;
			}
			// solo, Classic co-op (rule 8) and Roguelike co-op resolve a player's hit on the owner's copy: this is where the local player is hit (QA-35)
			if (userIsPlayer && MyView(base.gameObject)) ReportDamageDirection(damage, shooter);
			// Classic: a headshot flag means a lethal head hit, so the enemy dies at once. The roguelike reports every head hit with the
			// flag and decides lethality below from the damage after mitigation (shield front, Guardian last stand, invulnerable cores),
			// as the co-op path above does; the raw-damage check in Bullet killed through all of them in solo.
			if (headshot == 1 && !userIsPlayer && !RoguelikeMode.Active && !myAI.vip)
			{
				command = "head";
				killer = shooter;
				if (killer != null && killer.tag == "Enemy")
				{
					if (base.gameObject.layer == LayerMask.NameToLayer("BlueTeam"))
					{
						command = "ally";
					}
					else
					{
						command = "normal";
					}
				}
				if (Menu.network == 0)
				{
					Die(0);
				}
				else if (Menu.network != 1)
				{
					base.gameObject.GetPhotonView().RPC("Die", PhotonTargets.All, 0);
				}
				return;
			}
			if (RoguelikeMode.Active) damage = RogueHooks.ModifyIncomingDamage(this, damage, shooter);
			if (RoguelikeMode.Active && userIsPlayer && !MyView(base.gameObject)) RogueRemotePlayerHit(shooter, damage);
			hitPoints -= damage;
            if (RoguelikeMode.Active && Menu.network == 0 && !userIsPlayer) RogueCombatNumber.Show(this, damage, headshot == 1, Flats.Core.Roguelike.DamageKind.Direct, shooter);
			if (!userIsPlayer && myAI != null && myAI.isPatrol && myAI.targets.Count > 0 && myAI.targets[0] != null && !(RoguelikeMode.Active && RogueEnemyStatus.Stunned(myAI)))
			{
				Vector3 normalized = (myAI.targets[0].position - mt.position).normalized;
				Quaternion rotation = Quaternion.LookRotation(normalized);
				rotation.x = 0f;
				rotation.z = 0f;
				mt.rotation = rotation;
			}
			if (hitPoints <= 0f && (Menu.gameState == "Singleplayer" || !userIsPlayer || ((Multiplayer.rule == 8 || RoguelikeMode.Coop) && MyView(base.gameObject))))
			{
				if (userIsPlayer && RoguelikeMode.Active && RogueHooks.TryDown(this)) return;
				command = RoguelikeMode.Active && headshot == 1 && !userIsPlayer ? "head" : "normal";
				killer = shooter;
				if (killer != null && killer.tag == "Enemy")
				{
					if (base.gameObject.layer == LayerMask.NameToLayer("BlueTeam"))
					{
						command = "ally";
					}
					else
					{
						command = "normal";
					}
				}
				if (Menu.network == 0)
				{
					if (!userIsPlayer && myAI != null && myAI.vip)
					{
						command = "vip";
					}
					Die(0);
				}
				else if (Menu.network != 1)
				{
					base.gameObject.GetPhotonView().RPC("Die", PhotonTargets.All, 0);
				}
			}
			else
			{
				// the legacy 1-in-60 "mortal" instant kill is a Classic rule. Solo roguelike runs as Singleplayer.rule 5, so the old
				// rule-2 exemption missed it and the roll killed invulnerable finale cores and Guardian elites.
				if (userIsPlayer || RoguelikeMode.Active || myAI.vip || Singleplayer.rule == 2)
				{
					return;
				}
				int num = 0;
				num = ((!Singleplayer.chance) ? UnityEngine.Random.Range(0, 60) : UnityEngine.Random.Range(0, 12));
				if (num != 0 || headshot == -1)
				{
					return;
				}
				command = "mortal";
				killer = shooter;
				if (killer != null && killer.tag == "Enemy")
				{
					if (base.gameObject.layer == LayerMask.NameToLayer("BlueTeam"))
					{
						command = "ally";
					}
					else
					{
						command = "normal";
					}
				}
				if (Menu.network == 0)
				{
					Die(0);
				}
				else if (Menu.network != 1)
				{
					base.gameObject.GetPhotonView().RPC("Die", PhotonTargets.All, 0);
				}
			}
			return;
		}
		int[] array = new int[3];
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1)
		{
			// Every client simulates every bullet, so each copy of a hit would report it
			// and the target would take the damage once per client. Only the shooter's
			// owner reports (the master client for AI shooters).
			var shooterView = shooter != null ? shooter.gameObject.GetPhotonView() : null;
			if (shooterView == null || !shooterView.isMine)
			{
				return;
			}
			array[0] = (int)damage;
			array[1] = headshot;
			array[2] = shooter.gameObject.GetPhotonView().viewID;
			if (base.gameObject.tag == "Player")
			{
				base.gameObject.GetPhotonView().RPC("NetworkDamage", PhotonTargets.All, array);
			}
			else
			{
				base.gameObject.GetPhotonView().RPC("NetworkDamage", PhotonTargets.MasterClient, array);
			}
		}
	}

	[PunRPC]
	private void Die(int receivedData)
	{
		if (died)
		{
			return;
		}
		died = true;
		// removal is scheduled first: an exception in any later mode hook must not leave the body (and its markers) behind forever
		Invoke("Stop", 5f);
		// every roguelike hook below is guarded: an exception in one must not skip the rest of Die (ragdoll, weapon drop, the
		// enemy counters), which left bodies and markers behind forever
		if (RoguelikeMode.Active)
		{
			try
			{
				var reaction = GetComponent<RogueHitReaction>();
				if (reaction != null) reaction.StopReaction();
				if (!userIsPlayer) RogueHooks.OnEnemyDeathStarted(this);
			}
			catch (Exception e) { Debug.LogException(e, this); }
		}
		if (mt == null) mt = base.transform;   // the Die RPC can reach a network copy before Start ran
		// ... and Start no longer initialises a dead object, so the components it would have bound are bound here: otherwise
		// Destroy(null) below left a live FPSController / AI on the dead object for five seconds, running on deactivated children
		if (userIsPlayer && myFPSController == null) myFPSController = GetComponent<FPSController>();
		if (!userIsPlayer && myAI == null) myAI = GetComponent<AI>();
		if (RoguelikeMode.Coop && !userIsPlayer && receivedData != 0)
		{
			// the announcing copy packed shooter viewID * 2 + headshot flag; the authority pays and attributes from it
			command = (receivedData & 1) == 1 ? "head" : "normal";
			var shooterView = PhotonView.Find(receivedData / 2);
			if (shooterView != null) killer = shooterView.transform;
		}
		if (userIsPlayer && RoguelikeMode.Active)
		{
			try { RogueHooks.OnPlayerDied(this); }
			catch (Exception e) { Debug.LogException(e, this); }
		}
		if (RoguelikeMode.Active && !userIsPlayer) { var cc = GetComponent<CharacterController>(); if (cc != null) cc.enabled = false; }   // the body must not push the ragdoll it is replaced by
		// a shooter that predicted this kill already dropped the ragdoll (RogueKillPrediction); the confirmed death keeps it
		GameObject predictedCorpse = null;
		if (RoguelikeMode.Active && !userIsPlayer)
		{
			try { predictedCorpse = RogueKillPrediction.TakeCorpse(this); }
			catch (Exception e) { Debug.LogException(e, this); predictedCorpse = null; }
		}
		GameObject gameObject = predictedCorpse != null ? predictedCorpse : UnityEngine.Object.Instantiate(deadReplacement, mt.position, mt.rotation) as GameObject;
		if (gameObject == null)
		{
			return;
		}
		if (RoguelikeMode.Active && !userIsPlayer && predictedCorpse == null)
		{
			try { RogueHooks.PoseCorpse(this, gameObject); }
			catch (Exception e) { Debug.LogException(e, this); }
		}
		// the death sound and the body colour are presentation: a ragdoll or a body without those parts still dies in full
		AudioSource corpseAudio = gameObject.GetComponent<AudioSource>();
		if (corpseAudio != null)
		{
			corpseAudio.volume = 0.5f;
			corpseAudio.pitch = 0.75f;
			if (damageSE != null)
			{
				corpseAudio.PlayOneShot(damageSE);
			}
		}
		Renderer sourceRenderer = mt.childCount > 0 ? mt.GetChild(0).GetComponent<Renderer>() : null;
		if (sourceRenderer != null && sourceRenderer.sharedMaterial != null)
		{
			Material bodyMaterial = sourceRenderer.material;
			SkinnedMeshRenderer[] componentsInChildren = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
			SkinnedMeshRenderer[] array = componentsInChildren;
			foreach (SkinnedMeshRenderer skinnedMeshRenderer in array)
			{
				skinnedMeshRenderer.material = bodyMaterial;
			}
		}
		// QA-44: a confirmed enemy death leaves a body that can be carried as a bullet shield (a predicted kill that rolls back never gets here)
		if (RoguelikeMode.Active && !userIsPlayer)
		{
			try { RogueBodyShield.Register(this, gameObject); }
			catch (Exception e) { Debug.LogException(e, this); }
		}
		if (base.gameObject.tag == "Player")
		{
			UnityEngine.Object.Destroy(myFPSController);
			// on the player object itself (it keeps the PhotonView and receives the late RPCs); the local "gameObject" here is the ragdoll,
			// which has no view, so a sink installed there never answered anything
			if (Menu.network != 0 && GetComponent<DeadPlayerRpcSinkInstaller>() == null) base.gameObject.AddComponent<DeadPlayerRpcSinkInstaller>();
		}
		else
		{
			UnityEngine.Object.Destroy(myAI);
			if (Menu.network != 0) AddDeadAIRpcSink();
		}
		UnityEngine.Object.Destroy(GetComponent<CharacterController>());
		for (int j = 0; j < mt.childCount; j++)
		{
			mt.GetChild(j).gameObject.SetActive(false);
		}
		if (ct == null && Camera.main != null && (bool)Camera.main.gameObject)
		{
			ct = Camera.main.transform;
		}
		if (command == "head" && ct != null)
		{
			if (Singleplayer.rule == 0 || Multiplayer.rule == 8)
			{
				score = 500;
			}
			else if (Singleplayer.rule == 2)
			{
				Singleplayer.headshotChain++;
				if (Singleplayer.headshotChain >= 100)
				{
					Menu.currentHeadshotScore += 10000;
				}
				else
				{
					Menu.currentHeadshotScore += 100 * Singleplayer.headshotChain;
				}
				if (Singleplayer.headshotChain > Menu.currentHeadshotChain)
				{
					Menu.currentHeadshotChain = Singleplayer.headshotChain;
					GameObject.Find("SingleplayerController").GetComponent<Singleplayer>().Log("Achieved new headshot record.");
				}
			}
			if (!RoguelikeMode.Active && killer != null && MyView(killer.gameObject))   // no kill-cam interruptions in the roguelike waves; the HUD already reports the bounty
			{
				Supershot.PlayKill(effectCamera, ct, gameObject.transform, true);
			}
		}
		else if (command == "mortal" && ct != null)
		{
			if (!RoguelikeMode.Active && killer != null && MyView(killer.gameObject))
			{
				Supershot.PlayKill(effectCamera, ct, gameObject.transform, false);
			}
		}
		else if (command == "vip" && ct != null)
		{
			if (Menu.network == 0 || !MyView(base.gameObject))
			{
				Supershot.PlayKill(effectCamera, ct, gameObject.transform, false, true);
			}
		}
		else if (command == "ally" && ct != null)
		{
			GameObject.Find("SingleplayerController").GetComponent<Singleplayer>().Log("Ally-bot killed Enemy.");
		}
		else if (command == "normal" && (userIsPlayer || (Menu.network != 0 && Multiplayer.rule != 8 && !RoguelikeMode.Coop)))
		{
			if (Menu.network == 0)
			{
				ShowRespawnView(gameObject);
				UnityEngine.Object.Destroy(gameObject.GetComponent<Destroy>());
				if (Singleplayer.rule == 3 || RoguelikeMode.Solo)
				{
					Debug.Log(RoguelikeMode.Solo ? "Roguelike solo death: the run controller ends the run." : "Respawn for training...");
				}
				else
				{
					GameObject[] array2 = GameObject.FindGameObjectsWithTag("Enemy");
					if (array2 != null)
					{
						GameObject[] array3 = array2;
						foreach (GameObject gameObject5 in array3)
						{
							if (gameObject5.layer == base.gameObject.layer)
							{
								UnityEngine.Object.Destroy(gameObject5);
							}
						}
					}
					GameObject.Find("Menu").BroadcastMessage("GameOver", SendMessageOptions.DontRequireReceiver);
				}
			}
			else if (!userIsPlayer || MyView(base.gameObject))
			{
				// the match controller's view: null when the controller is gone (a scene closing), then logs and scores are skipped
				PhotonView matchView = MultiplayerView();
				if (userIsPlayer)
				{
					ShowRespawnView(gameObject);
				}
				if (killer == mt)
				{
					string text = "";
					if (killer == mt && userIsPlayer)
					{
						if (Menu.network == 0)
						{
							Debug.Log("Singleplayer");
						}
						else if (Menu.network != 1)
						{
							text = "Suicide:" + OwnerName(base.gameObject);
							if (matchView != null)
							{
								matchView.RPC("Log", PhotonTargets.All, text);
							}
							// A VIP lost to a fall or their own grenade still starts the next VIP round.
							if (Multiplayer.rule == 7 && myFPSController != null && myFPSController.vip && !Multiplayer.end)
							{
								invincibility = true;
								if (matchView != null)
								{
									matchView.RPC("VIPRound", PhotonTargets.All);
								}
							}
						}
					}
				}
				else if (Multiplayer.rule != 8 && !RoguelikeMode.Coop)
				{
					if (Menu.network == 0)
					{
						Debug.Log("Singleplayer");
					}
					else if (Menu.network != 1)
					{
						PhotonPlayer photonPlayer = PhotonNetwork.player;
						if (killer == null)
						{
							// the killer's view can be gone (it left or was destroyed while the kill travelled)
							PhotonView killerView = PhotonView.Find(receivedData);
							if (killerView != null)
							{
								killer = killerView.transform;
							}
						}
						bool victimVIP = Multiplayer.rule == 7 && (userIsPlayer ? myFPSController != null && myFPSController.vip : GetComponent<AI>() != null && GetComponent<AI>().vip);
						if (killer == null)
						{
							// nobody to credit: no kill, team score or kill log, but the victim's death still counts and a lost VIP still
							// starts the next round (same senders as the credited path below)
							if (userIsPlayer && PhotonNetwork.offlineMode)
							{
								ExitGames.Client.Photon.Hashtable hashtable3 = new ExitGames.Client.Photon.Hashtable();
								object deaths = PhotonNetwork.player.CustomProperties["D"];
								hashtable3["D"] = (deaths is int ? (int)deaths : 0) + 1;
								PhotonNetwork.SetPlayerCustomProperties(hashtable3);
								Multiplayer.privateDeathCount++;
							}
							if (victimVIP)
							{
								invincibility = true;
								if (!Multiplayer.end && matchView != null)
								{
									matchView.RPC("VIPRound", PhotonTargets.All);
								}
							}
						}
						else
						{
							// The local gameObject is the replacement ragdoll, not the registered actor.
							FlatsOfflineScores.Kill(killer,base.gameObject);
							if (killer.tag == "Player")
							{
								PhotonView killerOwnerView = killer.gameObject.GetPhotonView();
								if (killerOwnerView != null && killerOwnerView.owner != null)
								{
									photonPlayer = killerOwnerView.owner;
								}
							}
							if (killer.tag == "Player" && (base.gameObject.tag == "Player" || PhotonNetwork.offlineMode))
							{
								ExitGames.Client.Photon.Hashtable hashtable = new ExitGames.Client.Photon.Hashtable();
								int num = (int)photonPlayer.CustomProperties["K"];
								num++;
								hashtable["K"] = num;
								photonPlayer.SetCustomProperties(hashtable);
								int[] array4 = new int[2] { photonPlayer.ID, 0 };
								if (matchView != null)
								{
									matchView.RPC("GetScore", PhotonTargets.All, array4);
								}
							}
							if (userIsPlayer && (killer.tag == "Player" || PhotonNetwork.offlineMode))
							{
								ExitGames.Client.Photon.Hashtable hashtable2 = new ExitGames.Client.Photon.Hashtable();
								int num2 = (int)PhotonNetwork.player.CustomProperties["D"];
								num2++;
								hashtable2["D"] = num2;
								PhotonNetwork.SetPlayerCustomProperties(hashtable2);
								Multiplayer.privateDeathCount++;
							}
							if (Multiplayer.rule <= 2 || victimVIP)
							{
								int num3 = 2;
								if (killer.tag == "Player")
								{
									if (photonPlayer.GetTeam() == PunTeams.Team.red)
									{
										num3 = 0;
									}
									else if (photonPlayer.GetTeam() == PunTeams.Team.blue)
									{
										num3 = 1;
									}
								}
								else if (Multiplayer.rule != 1)
								{
									num3 = ((!(LayerMask.LayerToName(killer.gameObject.layer) == "RedTeam")) ? 1 : 0);
								}
								if ((userIsPlayer && MyView(base.gameObject)) || (!userIsPlayer && MyView(killer.gameObject)))
								{
									int[] array5 = new int[2] { num3, 1 };
									if (matchView != null)
									{
										matchView.RPC("GetTeamScore", PhotonTargets.All, array5);
									}
								}
								if (victimVIP)
								{
									invincibility = true;
									if (!Multiplayer.end && matchView != null)
									{
										matchView.RPC("VIPRound", PhotonTargets.All);
									}
								}
							}
							if ((userIsPlayer && MyView(base.gameObject)) || (!userIsPlayer && MyView(killer.gameObject)))
							{
								string text2 = "";
								text2 = ((killer.tag == "Player" && base.gameObject.tag == "Player") ? (photonPlayer.NickName + " killed " + OwnerName(base.gameObject)) : ((killer.tag == "Player" && base.gameObject.tag != "Player") ? (photonPlayer.NickName + " killed Flatman(Bot)") : ((!(killer.tag != "Player") || !(base.gameObject.tag == "Player")) ? "Flatman(Bot) killed Flatman(Bot)" : ("Flatman(Bot) killed " + OwnerName(base.gameObject)))));
								if (matchView != null)
								{
									matchView.RPC("Log", PhotonTargets.All, text2);
								}
							}
						}
					}
				}
				else if (Multiplayer.rule == 8 || RoguelikeMode.Coop)
				{
					if (Menu.network == 0)
					{
						Debug.Log("Singleplayer");
					}
					else if (Menu.network != 1)
					{
						// an optional kill log: a player who already left the room, or a match controller that is gone, only skips
						// the line; the rest of Die (weapon drop, counters) still runs
						PhotonView deadView = base.gameObject.GetPhotonView();
						if (deadView != null && deadView.owner != null && matchView != null)
						{
							matchView.RPC("Log", PhotonTargets.All, "Killed: " + deadView.owner.NickName);
						}
					}
				}
			}
			else if (!MyView(base.gameObject) && Multiplayer.rule == 7 && myFPSController != null && myFPSController.vip)
			{
				Supershot.PlayKill(effectCamera, ct, gameObject.transform, false, true, base.gameObject.layer);
			}
		}
		if (myFPSController != null)
		{
			if (Menu.gameState == "Singleplayer")
			{
				GameObject gameObject7 = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("Weapons/Weapon" + myFPSController.primaryWeaponIndex), mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity);
				GameObject gameObject8 = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("Weapons/Weapon" + myFPSController.secondaryWeaponIndex), mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity);
				if (myFPSController.primarySightIndex != 0)
				{
					GameObject gameObject9 = FlatsSightTarget.Create("Sights/" + Menu.sightDictionary[myFPSController.primarySightIndex]);
					gameObject9.transform.SetParent(gameObject7.transform.GetChild(2));
					gameObject9.transform.localPosition = Vector3.zero;
					gameObject9.transform.localEulerAngles = new Vector3(-90f, 0f, 0f);
				}
				if (myFPSController.secondarySightIndex != 0)
				{
					GameObject gameObject10 = FlatsSightTarget.Create("Sights/" + Menu.sightDictionary[myFPSController.secondarySightIndex]);
					gameObject10.transform.SetParent(gameObject8.transform.GetChild(2));
					gameObject10.transform.localPosition = Vector3.zero;
					gameObject10.transform.localEulerAngles = new Vector3(-90f, 0f, 0f);
				}
				DroppedGun component5 = gameObject7.GetComponent<DroppedGun>();
				component5.sight = myFPSController.primarySightIndex;
				DroppedGun component6 = gameObject8.GetComponent<DroppedGun>();
				component6.sight = myFPSController.secondarySightIndex;
			}
			else if (Menu.isMaster() && !myFPSController.zombie && myFPSController.primaryWeapon != null && myFPSController.primaryWeapons != null)   // a player killed before its weapons were bound drops nothing
			{
				Gun component7 = myFPSController.primaryWeapon.gameObject.GetComponent<Gun>();
				Gun component8 = myFPSController.primaryWeapons.GetChild(myFPSController.secondaryWeaponIndex).gameObject.GetComponent<Gun>();
				if (Menu.network == 0)
				{
					Debug.Log("Singleplayer");
				}
				else if (Menu.network != 1)
				{
					GameObject go = PhotonNetwork.InstantiateSceneObject("Weapons/Weapon" + myFPSController.primaryWeaponIndex, mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity, 0, null);
					GameObject go2 = PhotonNetwork.InstantiateSceneObject("Weapons/Weapon" + myFPSController.secondaryWeaponIndex, mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity, 0, null);
					go.GetPhotonView().RPC("DropData", PhotonTargets.All, component7.currentAmmo, component7.maxAmmo, myFPSController.primarySightIndex);
					go2.GetPhotonView().RPC("DropData", PhotonTargets.All, component8.currentAmmo, component8.maxAmmo, myFPSController.secondarySightIndex);
				}
			}
		}
		else if (Menu.gameState == "Singleplayer" && myAI != null && myAI.primaryWeapon != null)
		{
			GameObject gameObject11 = UnityEngine.Object.Instantiate(Resources.Load("Weapons/Weapon" + myAI.primaryWeaponIndex), mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity) as GameObject;
			GameObject gameObject12 = UnityEngine.Object.Instantiate(Resources.Load("Weapons/Weapon" + myAI.secondaryWeaponIndex), mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity) as GameObject;
			Vector3 vector = mt.TransformDirection(0f, 0f, 4f);
			Gun component9 = myAI.primaryWeapon.gameObject.GetComponent<Gun>();
			Gun component10 = myAI.primaryWeapons.GetChild(myAI.secondaryWeaponIndex).gameObject.GetComponent<Gun>();
			gameObject11.GetComponent<Rigidbody>().linearVelocity = vector;
			gameObject12.GetComponent<Rigidbody>().linearVelocity = vector * 2f;
			DroppedGun component11 = gameObject11.GetComponent<DroppedGun>();
			component11.currentAmmo = component9.currentAmmo;
			component11.maxAmmo = component9.maxAmmo;
			component11.sight = myAI.primarySightIndex;
			DroppedGun component12 = gameObject12.GetComponent<DroppedGun>();
			component12.currentAmmo = component10.currentAmmo;
			component12.maxAmmo = component10.maxAmmo / 2;
			component12.sight = myAI.secondarySightIndex;
			if (myAI.primarySightIndex != 0)
			{
				GameObject gameObject13 = FlatsSightTarget.Create("Sights/" + Menu.sightDictionary[myAI.primarySightIndex]);
				gameObject13.transform.SetParent(gameObject11.transform.GetChild(2));
				gameObject13.transform.localPosition = Vector3.zero;
				gameObject13.transform.localEulerAngles = new Vector3(-90f, 0f, 0f);
			}
			if (myAI.secondarySightIndex != 0)
			{
				GameObject gameObject14 = FlatsSightTarget.Create("Sights/" + Menu.sightDictionary[myAI.secondarySightIndex]);
				gameObject14.transform.SetParent(gameObject12.transform.GetChild(2));
				gameObject14.transform.localPosition = Vector3.zero;
				gameObject14.transform.localEulerAngles = new Vector3(-90f, 0f, 0f);
			}
		}
		else if (Menu.isMaster() && myAI != null && myAI.primaryWeapon != null)   // an enemy killed before its weapons were bound drops nothing
		{
			Gun component13 = myAI.primaryWeapon.gameObject.GetComponent<Gun>();
			Gun component14 = myAI.primaryWeapons.GetChild(myAI.secondaryWeaponIndex).gameObject.GetComponent<Gun>();
			if (Menu.network == 0)
			{
				Debug.Log("Singleplayer");
			}
			else if (Menu.network != 1)
			{
				GameObject go3 = PhotonNetwork.InstantiateSceneObject("Weapons/Weapon" + myAI.primaryWeaponIndex, mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity, 0, null);
				GameObject go4 = PhotonNetwork.InstantiateSceneObject("Weapons/Weapon" + myAI.secondaryWeaponIndex, mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity, 0, null);
				go3.GetPhotonView().RPC("DropData", PhotonTargets.All, component13.currentAmmo, component13.maxAmmo, myAI.primarySightIndex);
				go4.GetPhotonView().RPC("DropData", PhotonTargets.All, component14.currentAmmo, component14.maxAmmo, myAI.secondarySightIndex);
			}
		}
		if (!userIsPlayer)
		{
			if (RoguelikeMode.Active)
			{
				if (LocalPlayerSource(killer)) ReportLocalHit(true, command == "head");   // E4: the local player's kill marker
				{ Renderer feelBody = mt.childCount > 0 ? mt.GetChild(0).GetComponent<Renderer>() : null; FlatsFeel.EnemyDied(mt, feelBody != null && feelBody.sharedMaterial != null ? feelBody.sharedMaterial.color : Color.white, command == "head"); }
				try { RogueHooks.OnEnemyDied(this, killer, command == "head"); }
				catch (Exception e) { Debug.LogException(e, this); }
			}
			if (Menu.gameState == "Singleplayer" || Multiplayer.rule == 8 || RoguelikeMode.Coop)
			{
				if (base.gameObject.layer == LayerMask.NameToLayer("BlueTeam"))
				{
					Singleplayer.enemy--;
					if (Singleplayer.rule == 1 || Singleplayer.rule == 3)
					{
						Singleplayer.respawnEnemy--;
					}
				}
				else
				{
					Singleplayer.bot--;
				}
				if (base.gameObject.layer == LayerMask.NameToLayer("BlueTeam") && (Menu.gameState == "Singleplayer" || (killer != null && MyView(killer.gameObject))))
				{
					if (Singleplayer.rule == 0 || Multiplayer.rule == 8)
					{
						Menu.currentSurvivalScore += score;
					}
					else if (Singleplayer.rule == 1)
					{
						Menu.currentAssortmentScore += score / 2;
					}
					else if (Singleplayer.rule == 2 && command != "head")
					{
						Menu.currentHeadshotScore -= 100 * Singleplayer.headshotChain;
						GameObject.Find("SingleplayerController").GetComponent<Singleplayer>().Log("Headshot chain:" + Singleplayer.headshotChain);
						if (Menu.currentHeadshotScore < 0)
						{
							Menu.currentHeadshotScore = 0;
						}
						Singleplayer.headshotChain = 0;
						Singleplayer.chance = false;
					}
				}
			}
			else if (Menu.botCount > 0 && Menu.isMaster())
			{
				Multiplayer.bot--;
			}
		}
	}

	private void Stop()
	{
		// Roguelike co-op: the authority drops the dead enemy's buffered RPCs so a reused view id never receives them.
		if (RoguelikeMode.Coop && !userIsPlayer && PhotonNetwork.isMasterClient && PhotonNetwork.inRoom)
		{
			var view = GetComponent<PhotonView>();
			if (view != null) PhotonNetwork.RemoveRPCs(view);
		}
		// Networked bots spawned as scene objects (Classic multiplayer): only a network destroy removes the cached instantiation, so a
		// late joiner does not get a ghost. The roguelike clears that cache at death (RoguelikeController.ForgetCachedInstantiate).
		if (!userIsPlayer && !RoguelikeMode.Active && Menu.network == 2 && PhotonNetwork.inRoom && !PhotonNetwork.offlineMode && PhotonNetwork.isMasterClient)
		{
			var sceneView = GetComponent<PhotonView>();
			if (sceneView != null && sceneView.isSceneView && sceneView.instantiationId > 0) { PhotonNetwork.Destroy(base.gameObject); return; }
		}
		UnityEngine.Object.Destroy(base.gameObject);
	}

	public DamageReceiver()
	{
		hitPoints = 500f;
		command = "normal";

	}




}
