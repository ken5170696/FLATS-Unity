using System;
using System.Collections;
using InControl;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public partial class FPSController : MonoBehaviour
{
    private Flats.Core.IGameSessionContext session;
    private Flats.Core.IPlayerInputSource desktopInput = new Flats.Gameplay.UnityDesktopPlayerInput();
    private bool overrideInputDevice;
    private Flats.Core.IPlayerActionDispatcher actions;
    private int SessionNetworkMode { get { return session != null ? session.NetworkMode : 0; } }
    private bool SessionPlaying { get { return session != null && session.IsPlaying; } }
    private bool GameplayActive { get { return testMode || SessionPlaying; } }

    // Composition and private validation runners use the same product input boundary.
    public void ConfigureGameplay(Flats.Core.IGameSessionContext context, Flats.Core.IPlayerInputSource input, bool overrideDevice = true)
    {
        ConfigureGameplay(context,input,new Flats.Gameplay.PhotonPlayerActionDispatcher(this,context),overrideDevice);
    }
    public void ConfigureGameplay(Flats.Core.IGameSessionContext context, Flats.Core.IPlayerInputSource input, Flats.Core.IPlayerActionDispatcher dispatcher, bool overrideDevice = true)
    {
        session = context ?? throw new ArgumentNullException(nameof(context));
        desktopInput = input ?? throw new ArgumentNullException(nameof(input));
        actions = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        overrideInputDevice = overrideDevice;
    }

	public bool zombie;

	public bool motherZombie;

	public bool biten;

	public bool vip;

	public bool testMode;

	public int testPrimary;

	public GameObject myCamera;

	private Camera gunCam;

	public int primaryWeaponIndex;

	public int secondaryWeaponIndex;

	public int primarySightIndex;

	public int secondarySightIndex;

	public Transform primaryWeapon;

	public Transform secondaryWeapon;

	public GameObject head;

	public Rigidbody bullet;

	public Rigidbody grenade;

	public Transform longTapRing;

	public bool isZoom;

	private Gun currentGun;

	private bool startZooming;

	// Aim-in takes a few frames (startZooming) before isZoom is set. Cancelling and
	// toggling must treat that transition as aiming, or reload, weapon change,
	// grenade or sprint started mid-transition would finish aimed afterwards.
	private bool Aiming => isZoom || startZooming;
	// True only during Shoot's cooldown. Aiming may start or stop while the fire
	// button is held; reload, weapon change, grenades and melee still block it.
	private bool firing;
	// Sprinting is an explicit state (UpdateSprint), never read back from speed: speed skills make walking faster than the
	// old 20 u/s "running" threshold without sprinting (QA-21). Aiming wins over sprinting (QA-04); a dash or a menu refuses it.
	private bool CanStartAim => enableFire || firing;

	private Vector3 aimEyeLocalPosition;

	private Transform ui;

	// Contextual touch button (GameplayHUD/Interact); shown only when something can be picked up.
	private ETCButton interactButton;

	private Text interactLabel;

	private Flats.UI.ICrosshairVisibility reticle;
	private bool preferGamepad = true;

	private GameObject sight;

	public Transform primaryWeapons;

	public Transform secondaryWeapons;

	public AudioClip reloadStartSE;

	public AudioClip reloadEndSE;

	public AudioClip grenadeSE;

	public AudioClip zombieSE;

	public AnimationClip longTapAnim;

	public Transform myName;

	private Animator anim;

	private Animator camAnim;

	private Transform mt;

	private Transform mct;

	private Transform ct;

	private CharacterController cc;
	private readonly Flats.Gameplay.FerrisWheelFollower ferrisWheelFollower = new Flats.Gameplay.FerrisWheelFollower();

	private IKController ikc;

	private AmplifyMotionEffect am;

	private FxPro fp;

	private FXAA fxaa;

	private EdgeDetectEffectNormals ed;

	private CC_Grayscale cg;

	public float reloadPressTime;

	public float jumpPressTime;

	public float zoomPressTime;

	public float pickPressTime;

	private bool picking;

	public static float holdTime = 0.2f;

	// ---- Sprint (QA-21, QA-04, QA-02). Rules, owner only:
	//  - Keyboard and mouse follow FlatsControls.ToggleSprint: Hold (default, the original behaviour) sprints while the key is
	//    held; Toggle starts with a press and stops with the next press. A controller keeps its own rule: a click toggles and
	//    holding also sprints. Touch sprints while Jump is held.
	//  - Sprint needs forward input (little strafe) and ground contact; stopping forward input ends a toggled sprint.
	//  - Aiming wins: starting to aim ends the sprint at once (normal aimed movement, no fire delay). Held sprint resumes when
	//    the aim ends while the key is still held; a toggled sprint stays off until pressed again. A sprint press ends a
	//    toggled aim (never an aim that is being held).
	//  - Firing ends the sprint: the weapon comes up for SprintOutSeconds before that first shot is sent (the owner waits, so
	//    every copy fires together). Held sprint resumes SprintAfterShotSeconds after the trigger is let go; toggled stays off.
	//  - A reload keeps the sprint speed (the run pose waits for the weapon). A Roguelike dash suspends it (toggled: ends).
	//  - Hard stops (carry start, going down, a pause, menu or modal, a suspended control) end it; a held key must be released
	//    and pressed again. Death, respawn and a scene change create a new controller, so nothing carries over.
	private bool sprinting;

	private bool sprintLatched;

	private bool sprintHeldLast;

	private bool sprintNeedsRelease;

	private float sprintBlockedUntil = -1f;

	private float sprintOutUntil = -1f;

	private int sprintOutShot;

	private const float SprintOutSeconds = 0.2f;

	private const float SprintAfterShotSeconds = 0.35f;

	/// <summary>The owner's movement uses the sprint speed this frame.</summary>
	public bool Sprinting => sprinting;

	private float rogueLift;

	private float rogueFall;

	private float rogueJumpPrevY;

	// Roguelike owns the local player's vertical speed: a ballistic jump and fall instead of a constant push fighting the controller's
	// accumulated gravity (a jump cut at a mis-read apex, and a double jump cancelled by the fall speed it started in).
	private float rogueVy;

	private bool rogueJumpStart;

	private const float RogueGravity = 30f;

	private const float RogueJumpHeight = 5f;

	private const float RogueTerminalFall = 55f;

	private bool RogueVertical => RoguelikeMode.Active && mt != null && mt.parent == null;

	public static int sensitivity = 5;

	public static bool edgeRendering = false;

	public static bool saturationFilter = false;

	public static bool aa = false;

	public static bool motionBlur = false;

	public static bool dof = false;

	public static int handedness = 0;

	public static bool invertY = false;

	public static bool autoAim = false;

	public static bool tapFiring = false;

	public static bool enableCamRotate = false;

	public static bool enableControl = false;

	public bool enableFire;

	public bool touchControl;

	private GameObject multiplayer;

	private Vector3 lastPosition;

	private PhotonTransformView ptv;

	public Transform droppedGun;

	public Transform grabbedObject;

	public bool grabbing;

	public LayerMask mask;

	private Transform ltr;

	private bool jumping;

	private bool isJump;

	private bool movedWithGravity;

	private float Y;

	public static int headTracking = 0;

	public static Vector3 headRotation;

	public static float savedFOV = 0f;

	private Vector3 netPos;

	private Quaternion netRot;

	private Vector3 netVelocity;

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
		mt = base.transform;
		mct = myCamera.transform;
		ct = myCamera.transform.GetChild(0);
		cc = GetComponent<CharacterController>();
		anim = GetComponent<Animator>();
		ikc = GetComponent<IKController>();
		ptv = GetComponent<PhotonTransformView>();
		ui = GameObject.Find("UICamera").transform;
		if (MyView(base.gameObject))
		{
			var interact = ui.GetChild(1).Find("Interact");
			interactButton = interact != null ? interact.GetComponent<ETCButton>() : null;
			interactLabel = interact != null ? interact.GetComponentInChildren<Text>(true) : null;
			ShowInteractButton(false, true);
			// The press event is used rather than polling: EasyTouch may advance Down to
			// Press before this controller's Update reads it.
			if (interactButton != null)
			{
				interactButton.onDown.AddListener(OnInteractPressed);
			}
		}
        // Reuse the existing scene UI binding, without another global service lookup.
        var menu = ui.GetComponentInChildren<Menu>(true);
        if (menu != null) { menu.BindGameplay(this); RogueHooks.OnPlayerStarted(this); }
        else Debug.LogError("Player requires a Menu gameplay session binding.", this);
		netPos = mt.position;
		netRot = mt.rotation;
		netVelocity = Vector3.zero;
		StartCoroutine("SyncAnimation");
	}

	[PunRPC]
	private void StartZombie()
	{
		anim.SetTrigger("Zombie");
		zombie = true;
	}

	private void Start()
	{
		if (MyView(base.gameObject))
		{
			camAnim = ct.GetComponent<Animator>();
			gunCam = ct.GetChild(0).GetComponent<Camera>();
			am = ct.GetComponent<AmplifyMotionEffect>();
			fp = ct.GetComponent<FxPro>();
			fxaa = ct.GetComponent<FXAA>();
			ed = ct.GetComponent<EdgeDetectEffectNormals>();
			cg = ct.GetComponent<CC_Grayscale>();
			var presenter = ui.GetChild(1).GetChild(7).GetComponent<Flats.UI.LocalCrosshairPresenter>();
            presenter.BindLocalOwner(this);
            reticle = presenter;
			sight = GameObject.Find("Sight").transform.GetChild(0).gameObject;
			multiplayer = GameObject.Find("MultiplayerController");
			int num = 0;
			if (Menu.network == 0)
			{
				// The pickup lesson requires a rifle the student does not already
				// carry. Keep the course loadout independent of the saved loadout;
				// otherwise its required sniper is silently consumed as ammunition.
				bool tutorialCourse = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Tutorial";
				if (tutorialCourse)
				{
					primaryWeaponIndex = 4;
				}
				else if (testMode)
				{
					primaryWeaponIndex = testPrimary;
				}
				else
				{
					primaryWeaponIndex = Menu.myCharacter.primaryWeapon;
				}
				secondaryWeaponIndex = tutorialCourse ? 8 : Menu.myCharacter.secondaryWeapon;
				primaryWeapon = primaryWeapons.GetChild(primaryWeaponIndex);
				secondaryWeapon = secondaryWeapons.GetChild(secondaryWeaponIndex);
				primarySightIndex = tutorialCourse ? 0 : Menu.myCharacter.sightList[primaryWeaponIndex];
				secondarySightIndex = tutorialCourse ? 0 : Menu.myCharacter.sightList[secondaryWeaponIndex];
				if (RoguelikeMode.Active) { RogueHooks.MetaStartLoadout(ref primaryWeaponIndex, ref secondaryWeaponIndex, ref primarySightIndex, ref secondarySightIndex); primaryWeapon = primaryWeapons.GetChild(primaryWeaponIndex); secondaryWeapon = secondaryWeapons.GetChild(secondaryWeaponIndex); }
				if (primarySightIndex != 0)
				{
					GameObject gameObject = FlatsSightTarget.Create("Sights/" + Menu.sightDictionary[primarySightIndex]);
					gameObject.transform.SetParent(primaryWeapon.GetChild(2));
					gameObject.transform.localPosition = Vector3.zero;
					gameObject.transform.localEulerAngles = new Vector3(-90f, 0f, 0f);
					gameObject.transform.GetChild(0).GetChild(1).gameObject.SetActive(true);
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
				primaryWeapon.gameObject.SetActive(true);
				secondaryWeapon.gameObject.SetActive(true);
				currentGun = primaryWeapon.GetComponent<Gun>();
				SkinnedMeshRenderer[] componentsInChildren = GetComponentsInChildren<SkinnedMeshRenderer>();
				SkinnedMeshRenderer[] array = componentsInChildren;
				foreach (SkinnedMeshRenderer skinnedMeshRenderer in array)
				{
					skinnedMeshRenderer.material.color = ui.GetChild(0).GetChild(5).GetChild(1)
						.GetChild(Menu.myCharacter.color)
						.GetComponent<Image>()
						.color;
					skinnedMeshRenderer.gameObject.layer = 8;
					base.gameObject.layer = 8;
					head.layer = 8;
				}
				mask = 1 << LayerMask.NameToLayer("BlueTeam");
			}
			else if (Menu.network != 1)
			{
				num = ((PhotonNetwork.player.GetTeam() != PunTeams.Team.red) ? ((PhotonNetwork.player.GetTeam() == PunTeams.Team.blue) ? 1 : 2) : 0);
				int rogueP = Menu.myCharacter.primaryWeapon, rogueS = Menu.myCharacter.secondaryWeapon, roguePs = Menu.myCharacter.sightList[rogueP], rogueSs = Menu.myCharacter.sightList[rogueS];
				if (RoguelikeMode.Active) RogueHooks.MetaStartLoadout(ref rogueP, ref rogueS, ref roguePs, ref rogueSs);
				int[] array2 = new int[6]
				{
					num,
					Menu.myCharacter.color,
					rogueP,
					rogueS,
					roguePs,
					rogueSs
				};
				base.gameObject.GetPhotonView().RPC("SyncTeam", PhotonTargets.AllBuffered, array2);
			}
			Menu.changedSettings = true;
			EasyTouch.SetEnableAutoSelect(false);
			EasyTouch.SetLongTapTime(0.3f);
			EasyTouch.SetMinPinchLength(20f);
			EasyTouch.SetDoubleTapTime(0.15f);
			EasyTouch.SetUICompatibily(false);
			if ((!Application.isMobilePlatform && Input.mousePresent) || Input.GetJoystickNames().Length > 0)
			{
				touchControl = true;
			}
			else
			{
				touchControl = false;
			}
			reticle.SetVisible(true);
			sight.SetActive(false);
			SetBodyRenderLayer(13);
			// QA-01: the local body sits on layer 13, which world lights exclude; a shadows-only proxy on the default layer casts its shadow
			PlayerShadowProxy.Attach(gameObject);
			savedFOV = 0f;
			enableCamRotate = true;
			enableControl = true;
			enableFire = true; firing = false;
		}
		else
		{
			myCamera.SetActive(false);
		}
		GC.Collect();
	}

	// LOD changes must not make the local body reappear in world/scope views.
	// Only mesh renderer objects change layer; team colliders keep their masks.
	private void SetBodyRenderLayer(int layer)
	{
		var group = GetComponent<LODGroup>();
		if (group == null)
		{
			mt.GetChild(0).gameObject.layer = layer;
			return;
		}
		foreach (var lod in group.GetLODs())
			foreach (var renderer in lod.renderers)
				if (renderer != null) renderer.gameObject.layer = layer;
	}

	private void OnEnable()
	{
		EasyTouch.On_SimpleTap += On_SimpleTap;
		EasyTouch.On_Swipe += On_Swipe;
		EasyTouch.On_LongTapStart += On_LongTapStart;
		EasyTouch.On_LongTapEnd += On_LongTapEnd;
	}

	private void OnDisable()
	{
		ShowInteractButton(false);
		EasyTouch.On_SimpleTap -= On_SimpleTap;
		EasyTouch.On_Swipe -= On_Swipe;
		EasyTouch.On_LongTapStart -= On_LongTapStart;
		EasyTouch.On_LongTapEnd -= On_LongTapEnd;
		if (Aiming)
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
	}

	private void OnDestroy()
	{
		if (interactButton != null)
		{
			interactButton.onDown.RemoveListener(OnInteractPressed);
		}
		EasyTouch.On_SimpleTap -= On_SimpleTap;
		EasyTouch.On_Swipe -= On_Swipe;
		EasyTouch.On_LongTapStart -= On_LongTapStart;
		EasyTouch.On_LongTapEnd -= On_LongTapEnd;
	}

	// Feel layer (Roguelike): the pitch pivot recoil nudges, and what footsteps read. The look itself stays owned by ApplyLook.
	internal Transform FeelLookPivot => mct;
	internal bool FeelGrounded => cc != null && mt != null && isGrounded();
	internal bool FeelControllable => enableControl && enableCamRotate;

	private void ApplyLook(Flats.Core.LookInput input, float x, float y)
	{
		var look = Flats.Core.LookRotationPolicy.Evaluate(input, x, y, isZoom ? FlatsControls.AimSensitivity(sensitivity) : sensitivity, invertY,
			isZoom, isZoom ? currentGun.zoom * ScopeViewPresenter.LookScale : 1f, headTracking, VRController.device == "cardboard",
			headRotation.x, headRotation.y, SessionPlaying, testMode,
			mct.localEulerAngles.x, mct.localEulerAngles.y);
		mt.eulerAngles += new Vector3(0f, look.BodyYaw, 0f);
		mct.localEulerAngles = new Vector3(look.CameraPitch, look.CameraYaw, 0f);
	}

	private bool CanInteract
	{
		get
		{
			if (grabbedObject != null && (enableFire || grabbing))
			{
				return true;
			}
			return droppedGun != null && enableFire && droppedGun.GetComponent<DroppedGun>().ready;
		}
	}

	private void OnInteractPressed()
	{
		if (enabled && enableControl && touchControl && !overrideInputDevice && GameplayActive && CanInteract)
		{
			TryInteract();
		}
	}

	private void ShowInteractButton(bool show, bool force = false)
	{
		if (interactButton == null || (!force && interactButton.visible == show))
		{
			return;
		}
		interactButton.visible = show;
		interactButton.activated = show;
		for (int i = 0; i < interactButton.transform.childCount; i++)
		{
			interactButton.transform.GetChild(i).gameObject.SetActive(show);
		}
	}

	// Grab or drop an object, or exchange the weapon on the floor. Shared by the
	// desktop Interact binding and the contextual touch Interact button.
	private void TryInteract()
	{
		if (grabbedObject != null && enableFire)
		{
			if (SessionNetworkMode == 0)
			{
				int[] array5 = new int[2];
				int[] receivedData4 = array5;
				Grab(receivedData4);
			}
			else if (SessionNetworkMode != 1)
			{
				int[] array6 = new int[2]
				{
					0,
					grabbedObject.gameObject.GetPhotonView().viewID
				};
				base.gameObject.GetPhotonView().RPC("Grab", PhotonTargets.AllBuffered, array6);
			}
		}
		else if (grabbing && grabbedObject != null)
		{
			if (SessionNetworkMode == 0)
			{
				int[] receivedData5 = new int[2] { 1, 0 };
				Grab(receivedData5);
			}
			else if (SessionNetworkMode != 1)
			{
				int[] array7 = new int[2]
				{
					1,
					grabbedObject.gameObject.GetPhotonView().viewID
				};
				base.gameObject.GetPhotonView().RPC("Grab", PhotonTargets.AllBuffered, array7);
			}
		}
		else if (droppedGun != null && enableFire)
		{
			TryExchangeGroundWeapon();
		}
	}

	/// <summary>Roguelike action rule for taking a ground weapon (always true in Classic modes); DroppedGun asks it before offering.</summary>
	public bool MayPickUpWeapon => !RoguelikeMode.Active || RogueActionGate.Allows(this, RogueAction.PickupWeapon);

	// Exchange the weapon in hand for the ground weapon the local player stands at: desktop Interact, the touch Interact button
	// and the controller's held Change all come here. The Roguelike rule is asked first (QA-14): carrying, down, a menu, a melee
	// swing, a dash or an Interact that belongs to a Roguelike target refuse it. The request carries the owner's own count of the
	// weapon it puts down (entries 5 and 6), and travels through the server so every copy orders competing requests the same way
	// (QA-26; ExchangeWeapons lets only the first taker have the weapon).
	private bool TryExchangeGroundWeapon()
	{
		if (droppedGun == null || !enableFire)
		{
			return false;
		}
		DroppedGun ground = droppedGun.GetComponent<DroppedGun>();
		if (ground == null || !ground.ready || !MayPickUpWeapon)
		{
			return false;
		}
		if (SessionNetworkMode == 0)
		{
			int[] receivedData = new int[5] { ground.weaponIndex, ground.currentAmmo, ground.maxAmmo, ground.sight, 0 };
			StartCoroutine(ExchangeWeapons(receivedData));
			UnityEngine.Object.Destroy(droppedGun.gameObject);
		}
		else if (SessionNetworkMode != 1 && base.gameObject.GetPhotonView().isMine)
		{
			PhotonView groundView = droppedGun.gameObject.GetPhotonView();
			if (groundView == null)
			{
				return false;
			}
			int[] request = new int[7]
			{
				ground.weaponIndex,
				ground.currentAmmo,
				ground.maxAmmo,
				ground.sight,
				groundView.viewID,
				currentGun != null ? currentGun.currentAmmo : -1,
				currentGun != null ? currentGun.maxAmmo : -1
			};
			base.gameObject.GetPhotonView().RPC("ExchangeWeapons", PhotonTargets.AllViaServer, request);
			ground.NoteRequested();
		}
		droppedGun = null;
		return true;
	}

	private void Update()
	{
		movedWithGravity = false;
		if (!enableControl)
		{
			// Cutscenes (for example the zombie bite) suspend control; the button returns afterwards.
			ShowInteractButton(false);
		}
		if (MyView(base.gameObject) && enableControl)
		{
            // Consume edge actions even while paused; never replay a queued press on resume.
            var desktopSample = desktopInput.Sample();
			if ((!Application.isMobilePlatform && Input.mousePresent) || Input.GetJoystickNames().Length > 0)
			{
				if (touchControl)
				{
					if (Input.GetJoystickNames().Length > 0)
					{
						Debug.Log("Gamepad Control");
						EventSystem.current.gameObject.GetComponent<StandaloneInputModule>().enabled = false;
                        EventSystem.current.gameObject.GetComponent<InControlInputModule>().enabled = true;
					}
					else
					{
						Debug.Log("Mouse Control");
					}
					EasyTouch.SetEnabled(false);
					ETCInput.SetControlActivated("Joystick", false);
					ETCInput.ResetAxis("Horizontal");
					ETCInput.ResetAxis("Vertical");
					ETCInput.SetControlVisible("Reload", false);
					ETCInput.SetControlActivated("Reload", false);
					ETCInput.SetControlVisible("Jump", false);
					ETCInput.SetControlActivated("Jump", false);
					ETCInput.SetControlVisible("Fire", false);
					ETCInput.SetControlActivated("Fire", false);
					ETCInput.SetControlVisible("Zoom", false);
					ETCInput.SetControlActivated("Zoom", false);
					ui.GetChild(1).GetChild(1).GetChild(0)
						.GetComponent<Image>()
						.enabled = false;
					ui.GetChild(1).GetChild(2).GetChild(0)
						.GetComponent<Image>()
						.enabled = false;
					ui.GetChild(1).GetChild(3).GetChild(0)
						.GetComponent<Image>()
						.enabled = false;
					ui.GetChild(1).GetChild(4).GetChild(0)
						.GetComponent<Image>()
						.enabled = false;
					touchControl = false;
				}
			}
			else if (!touchControl)
			{
				Debug.Log("Touch Control");
				EasyTouch.SetEnabled(true);
				ETCInput.SetControlActivated("Joystick", true);
				ETCInput.SetControlVisible("Reload", true);
				ETCInput.SetControlActivated("Reload", true);
				ETCInput.SetControlVisible("Jump", true);
				ETCInput.SetControlActivated("Jump", true);
				ETCInput.SetControlVisible("Fire", true);
				ETCInput.SetControlActivated("Fire", true);
				ETCInput.SetControlVisible("Zoom", true);
				ETCInput.SetControlActivated("Zoom", true);
				ui.GetChild(1).GetChild(1).GetChild(0)
					.GetComponent<Image>()
					.enabled = true;
				ui.GetChild(1).GetChild(2).GetChild(0)
					.GetComponent<Image>()
					.enabled = true;
				ui.GetChild(1).GetChild(3).GetChild(0)
					.GetComponent<Image>()
					.enabled = true;
				ui.GetChild(1).GetChild(4).GetChild(0)
					.GetComponent<Image>()
					.enabled = true;
				EventSystem.current.gameObject.GetComponent<StandaloneInputModule>().enabled = true;
				EventSystem.current.gameObject.GetComponent<InControlInputModule>().enabled = false;
				touchControl = true;
			}
			float num;
			float num2;
			// This frame's sprint, fire and held-aim inputs, resolved by UpdateSprint after the device branch (mode: 0 hold,
			// 1 toggle, 2 controller click-toggle that also sprints while held).
			bool sprintHeld = false, sprintPressed = false, fireHeld = false, aimHeld = false;
			int sprintMode = 0;
			FlushSprintOutShot();
			bool canInteract = touchControl && !overrideInputDevice && GameplayActive && CanInteract;
			ShowInteractButton(canInteract);
			if (canInteract && interactLabel != null)
			{
				string label = grabbing ? "Drop" : (grabbedObject != null ? "Pick up" : "Swap");
				if (interactLabel.text != label)
				{
					interactLabel.text = label;
				}
			}
            if (!GameplayActive)
            {
                num = num2 = 0f;
                jumpPressTime = reloadPressTime = zoomPressTime = pickPressTime = 0f;
                picking = false;
                // Only whether a sprint button is still held, so a sprint key let go in a menu may start a sprint at once afterwards.
                sprintHeld = desktopSample.Sprint || (!touchControl && FlatsControls.PadState("Sprint"));
                sprintHeldLast = desktopSample.Sprint;
            }
            else if (touchControl && !overrideInputDevice)
            {
                num = ETCInput.GetAxis("Vertical");
				num2 = ETCInput.GetAxis("Horizontal");
				if (ETCInput.GetButton("Jump"))
				{
					jumpPressTime += 1f * Time.deltaTime;
					// Holding Jump sprints; UpdateSprint applies it with the shared sprint rules.
					sprintHeld = jumpPressTime > holdTime;
				}
				else if (ETCInput.GetButtonUp("Jump"))
				{
					if (jumpPressTime <= holdTime && !jumping)
					{
						if (isGrounded() && !RogueJumpBlocked && !Physics.Raycast(mct.position, Vector2.up, 2f))
						{
							Y = mt.position.y;
							jumping = true;
						}
						jumpPressTime = 0f;
					}
					else
					{
						jumpPressTime = 0f;
					}
				}
				if (ETCInput.GetButton("Reload"))
				{
					reloadPressTime += 1f * Time.deltaTime;
					if (reloadPressTime > holdTime && (enableFire || grabbing) && RogueAllows(RogueAction.SwitchWeapon))
					{
						if (Menu.network == 0)
						{
							StartCoroutine("ChangeWeapons");
						}
						else if (Menu.network != 1)
						{
							base.gameObject.GetPhotonView().RPC("ChangeWeapons", PhotonTargets.All);
						}
					}
				}
				else if (ETCInput.GetButtonUp("Reload"))
				{
					if (reloadPressTime <= holdTime && enableFire && RogueAllows(RogueAction.Reload))
					{
						if (Menu.network == 0)
						{
							StartCoroutine("Reload");
						}
						else if (Menu.network != 1)
						{
							base.gameObject.GetPhotonView().RPC("Reload", PhotonTargets.All);
						}
						reloadPressTime = 0f;
					}
					else
					{
						reloadPressTime = 0f;
					}
				}
				if (ETCInput.GetButton("Zoom"))
				{
					zoomPressTime += 1f * Time.deltaTime;
					if (zoomPressTime > holdTime && grabbedObject == null && enableFire && !grabbing && RogueAllows(RogueAction.Grenade))
					{
						if (Menu.network == 0)
						{
							StartCoroutine("ThrowGrenade");
						}
						else if (Menu.network != 1)
						{
							base.gameObject.GetPhotonView().RPC("ThrowGrenade", PhotonTargets.All);
						}
					}
				}
				else if (ETCInput.GetButtonUp("Zoom"))
				{
					if (zoomPressTime <= holdTime)
					{
						if (!Aiming && CanStartAim)
						{
							Zoom(true);
						}
						else if (Aiming)
						{
							Zoom(false);
						}
						zoomPressTime = 0f;
					}
					else
					{
						zoomPressTime = 0f;
					}
				}
				if (ETCInput.GetButton("Fire"))
				{
					fireHeld = true;
					RaycastHit hitInfo = default(RaycastHit);
					if (!RoguelikeMode.Active && Physics.SphereCast(ct.position, 2f, ct.forward, out hitInfo, 3f, mask))
					{
						if (hitInfo.collider.gameObject.layer != base.gameObject.layer)
						{
							if (Menu.network == 0)
							{
								StartCoroutine("Smash");
							}
							else if (Menu.network != 1)
							{
								base.gameObject.GetPhotonView().RPC("Smash", PhotonTargets.All);
							}
						}
					}
					else if (grabbing)
					{
						if (Menu.network == 0)
						{
							StartCoroutine("Smash");
						}
						else if (Menu.network != 1)
						{
							base.gameObject.GetPhotonView().RPC("Smash", PhotonTargets.All);
						}
					}
					else if (enableFire && RogueAllows(RogueAction.Fire) && SprintOutReady(1))
					{
						if (Menu.network == 0)
						{
							StartCoroutine("Shoot");
						}
						else if (Menu.network != 1)
						{
							base.gameObject.GetPhotonView().RPC("Shoot", PhotonTargets.All);
						}
					}
				}
			}
			else
			{
				InputDevice activeDevice = InputManager.ActiveDevice;
				FlatsGamepad.EnsureApplied(activeDevice);
				bool padUsed = FlatsControls.AnyPadButtonHeld(activeDevice) || Mathf.Abs(activeDevice.LeftStickX)>0.1f || Mathf.Abs(activeDevice.LeftStickY)>0.1f || Mathf.Abs(activeDevice.RightStickX)>0.1f || Mathf.Abs(activeDevice.RightStickY)>0.1f || activeDevice.LeftTrigger>0.1f || activeDevice.RightTrigger>0.1f;
				if (padUsed) preferGamepad = true;
				// Only a deliberate mouse move (over 3 pixels in a frame) hands control back; jitter of a resting
				// mouse must not switch the controller off between stick inputs.
				// Phones simulate the mouse from touches, which must not take control from the pad.
				else if (!Application.isMobilePlatform && (FlatsControls.KeyboardOrMouseKeyHeld() || new Vector2(Input.GetAxisRaw("mouse x"), Input.GetAxisRaw("mouse y")).sqrMagnitude > 9f)) preferGamepad = false;
				FlatsControls.UsingGamepad = !overrideInputDevice && preferGamepad && activeDevice.Name != "None";
                if (!overrideInputDevice && preferGamepad && Input.GetJoystickNames().Length > 0 && activeDevice.Name != "None")
				{
					num = activeDevice.LeftStickY;
					num2 = activeDevice.LeftStickX;
					if (enableCamRotate)
					{
						var look = FlatsGamepad.Look(new Vector2(activeDevice.RightStickX, activeDevice.RightStickY), Time.deltaTime, isZoom);
						ApplyLook(Flats.Core.LookInput.Gamepad, look.x, look.y);
					}
					if (SessionPlaying)
					{
						// Controller jump fires on press. Sprint toggles with a click (holding also
						// works) and ends when forward input stops, the player aims or fires (UpdateSprint).
						if (FlatsControls.PadState("Jump", 1) && !jumping && isGrounded() && !RogueJumpBlocked && !Physics.Raycast(mct.position, Vector2.up, 2f))
						{
							Y = mt.position.y;
							jumping = true;
						}
						sprintMode = 2;
						sprintHeld = FlatsControls.PadState("Sprint");
						sprintPressed = FlatsControls.PadState("Sprint", 1);
						aimHeld = FlatsControls.PadState("Aim", 0);
						if (FlatsControls.PadState("Fire", 0))
						{
							fireHeld = true;
							RaycastHit hitInfo2 = default(RaycastHit);
							if (!RoguelikeMode.Active && Physics.SphereCast(ct.position, 2f, ct.forward, out hitInfo2, 3f, mask))
							{
								if (hitInfo2.collider.gameObject.layer != base.gameObject.layer)
								{
									if (Menu.network == 0)
									{
										StartCoroutine("Smash");
									}
									else if (Menu.network != 1)
									{
										base.gameObject.GetPhotonView().RPC("Smash", PhotonTargets.All);
									}
								}
							}
							else if (grabbing)
							{
								if (Menu.network == 0)
								{
									StartCoroutine("Smash");
								}
								else if (Menu.network != 1)
								{
									base.gameObject.GetPhotonView().RPC("Smash", PhotonTargets.All);
								}
							}
							else if (enableFire && RogueAllows(RogueAction.Fire) && SprintOutReady(1))
							{
								if (Menu.network == 0)
								{
									StartCoroutine("Shoot");
								}
								else if (Menu.network != 1)
								{
									base.gameObject.GetPhotonView().RPC("Shoot", PhotonTargets.All);
								}
							}
						}
						if (FlatsControls.PadState("Reload", 1) && (enableFire || grabbing) && RogueAllows(RogueAction.Reload))
						{
							if (Menu.network == 0)
							{
								StartCoroutine("Reload");
							}
							else if (Menu.network != 1)
							{
								base.gameObject.GetPhotonView().RPC("Reload", PhotonTargets.All);
							}
						}
						if (FlatsControls.PadState("Change", 0))
						{
							pickPressTime += 1f * Time.deltaTime;
							if (pickPressTime > holdTime && !picking)
							{
								picking = true;
								if (grabbedObject != null && enableFire)
								{
									if (Menu.network == 0)
									{
										int[] array = new int[2];
										int[] receivedData = array;
										Grab(receivedData);
									}
									else if (Menu.network != 1)
									{
										int[] array2 = new int[2]
										{
											0,
											grabbedObject.gameObject.GetPhotonView().viewID
										};
										base.gameObject.GetPhotonView().RPC("Grab", PhotonTargets.AllBuffered, array2);
									}
								}
								else if (droppedGun != null && enableFire)
								{
									TryExchangeGroundWeapon();
								}
								else if (grabbing && grabbedObject != null)
								{
									if (Menu.network == 0)
									{
										int[] receivedData3 = new int[2] { 1, 0 };
										Grab(receivedData3);
									}
									else if (Menu.network != 1)
									{
										int[] array4 = new int[2]
										{
											1,
											grabbedObject.gameObject.GetPhotonView().viewID
										};
										base.gameObject.GetPhotonView().RPC("Grab", PhotonTargets.AllBuffered, array4);
									}
								}
							}
						}
						if (FlatsControls.PadState("Change", 2))
						{
							if (pickPressTime <= holdTime)
							{
								if ((enableFire || grabbing) && RogueAllows(RogueAction.SwitchWeapon))
								{
									if (Menu.network == 0)
									{
										StartCoroutine("ChangeWeapons");
									}
									else if (Menu.network != 1)
									{
										base.gameObject.GetPhotonView().RPC("ChangeWeapons", PhotonTargets.All);
									}
								}
								pickPressTime = 0f;
							}
							else
							{
								pickPressTime = 0f;
							}
							picking = false;
						}
						if (FlatsControls.PadState("Aim", 0) && grabbedObject == null && !grabbing && !Aiming && CanStartAim)
						{
							Zoom(true);
						}
						if (FlatsControls.PadState("Aim", 2) && grabbedObject == null && !grabbing && Aiming)
						{
							Zoom(false);
						}
						if (FlatsControls.PadState("Grenade", 1) && grabbedObject == null && enableFire && !grabbing && RogueAllows(RogueAction.Grenade))
						{
							if (Menu.network == 0)
							{
								StartCoroutine("ThrowGrenade");
							}
							else if (Menu.network != 1)
							{
								base.gameObject.GetPhotonView().RPC("ThrowGrenade", PhotonTargets.All);
							}
						}
						if (FlatsControls.PadState("Scope", 1) && Menu.canOpen)
						{
							if (Aiming)
							{
								Zoom(false);
							}
							else if (CanStartAim)
							{
								Zoom(true);
							}
						}
					}
				}
				else
				{
                    var input = desktopSample;
					num = input.Forward;
					num2 = input.Right;
					sprintMode = FlatsControls.ToggleSprint ? 1 : 0;
					sprintHeld = input.Sprint;
					sprintPressed = input.Sprint && !sprintHeldLast;
					sprintHeldLast = input.Sprint;
					fireHeld = input.Fire;
					aimHeld = FlatsControls.HoldToAim && input.AimHeld;
					if (enableCamRotate)
					{
						ApplyLook(Flats.Core.LookInput.Mouse, input.LookX, input.LookY);
					}
					if (input.Fire)
					{
						RaycastHit hitInfo3 = default(RaycastHit);
						if (!RoguelikeMode.Active && Physics.SphereCast(ct.position, 2f, ct.forward, out hitInfo3, 3f, mask))
						{
							if (hitInfo3.collider.gameObject.layer != base.gameObject.layer)
							{
								actions.Dispatch(Flats.Core.PlayerAction.Smash);
							}
						}
						else if (grabbing)
						{
							actions.Dispatch(Flats.Core.PlayerAction.Smash);
						}
						else if (enableFire && !(RoguelikeMode.Active && RogueHooks.CarryingBlocksFire(this)) && RogueAllows(RogueAction.Fire) && SprintOutReady(2))
						{
							actions.Dispatch(Flats.Core.PlayerAction.Shoot);
						}
					}
					if ((input.Reload) && enableFire && RogueAllows(RogueAction.Reload))
					{
						actions.Dispatch(Flats.Core.PlayerAction.Reload);
					}
					if (input.ChangeWeapon && (enableFire || grabbing) && RogueAllows(RogueAction.SwitchWeapon))
					{
						actions.Dispatch(Flats.Core.PlayerAction.ChangeWeapons);
					}
					if (input.Grenade && grabbedObject == null && enableFire && !grabbing && RogueAllows(RogueAction.Grenade))
					{
						actions.Dispatch(Flats.Core.PlayerAction.ThrowGrenade);
					}
					if (input.Jump && !RogueJumpBlocked && !Physics.Raycast(mct.position, Vector2.up, 2f)
						&& ((!jumping && isGrounded()) || (RogueVertical && !cc.isGrounded && RogueHooks.AllowAirJump(this))))
					{
						// the air jump (Double Jump) is taken while rising or falling; it restarts the ballistic rise from where the player is
						Y = mt.position.y;
						jumping = true;
						rogueJumpStart = true;
					}
					if (input.Interact)
					{
						TryInteract();
					}
					if (FlatsControls.HoldToAim)
					{
						// Holding re-enters aim once a reload, weapon change or dash ends; it also wins over a sprint.
						if (input.AimHeld && !Aiming && CanStartAim)
						{
							Zoom(true);
						}
						else if (!input.AimHeld && Aiming)
						{
							Zoom(false);
						}
					}
					else if (input.ToggleZoom)
					{
						if (Aiming)
						{
							Zoom(false);
						}
						else if (CanStartAim)
						{
							Zoom(true);
						}
					}
				}
			}
			UpdateSprint(ref num, ref num2, sprintHeld, sprintPressed, sprintMode, fireHeld, aimHeld);
			if (!GameplayActive)
			{
				num = 0f;
				num2 = 0f;
			}
			ferrisWheelFollower.BeforeMove(cc, mt);
			movedWithGravity = Flats.Gameplay.PlayerMovementMotor.Move(cc, mt, jumping, zombie, num, num2, Time.deltaTime, RoguelikeMode.Active ? RogueHooks.MoveSpeedScale(this) : 1f);
			if (RogueVertical)
			{
				RogueVerticalMove();
			}
			else if (jumping)
			{
				if (mt.parent == null)
				{
					// Roguelike jump-height bonuses raise the push speed (rise grows with its square) so the target height is reachable.
					float jumpMul = RoguelikeMode.Active ? RogueHooks.JumpHeightMul(this) : 1f;
					// Descending since the previous frame means the apex has passed (the controller gravity acts between frames).
					bool pastApex = isJump && mt.position.y <= rogueJumpPrevY;
					rogueJumpPrevY = mt.position.y;
					cc.Move(mt.up * 12f * (RoguelikeMode.Active ? Mathf.Sqrt(jumpMul) : 1f) * Time.deltaTime);
					if (!isGrounded())
					{
						isJump = true;
					}
					if (isGrounded() && isJump)
					{
						jumping = false;
					}
					if (mt.position.y >= Y + 5f * jumpMul || Physics.Raycast(mct.position, Vector2.up, 2f))
					{
						jumping = false;
					}
					// The push ends at the apex. Before, a jump that fell short of the target height (a Roguelike bonus, a ledge
					// under the rising player) kept pushing up for the whole fall, so a jump off a roof floated down.
					if (RoguelikeMode.Active && pastApex)
					{
						jumping = false;
					}
				}
				else
				{
					isJump = false;
					jumping = false;
				}
			}
			else if (!jumping && !isGrounded() && !movedWithGravity)
			{
				cc.Move(mt.up * 10f * Time.deltaTime);
			}
			else
			{
				jumping = false;
				isJump = false;
			}
			Vector3 speed = mt.InverseTransformDirection(cc.velocity);
			if (isGrounded())
			{
				anim.SetFloat("Vertical", RoguelikeMode.Active ? RogueLocomotion.LegParam(speed.z) : speed.z);
				anim.SetFloat("Horizontal", RoguelikeMode.Active ? RogueLocomotion.LegParam(speed.x) : speed.x);
			}
			else
			{
				anim.SetFloat("Vertical", 0f);
				anim.SetFloat("Horizontal", 0f);
			}
			ptv.SetSynchronizedValues(speed, 0f);
			if (isZoom)
			{
				anim.SetBool("Zoom", true);
			}
			else
			{
				anim.SetBool("Zoom", false);
			}
			if ((bool)camAnim)
			{
				if (isGrounded())
				{
					if (!Menu.VRmode)
					{
						camAnim.SetFloat("Speed", Mathf.Abs(num) + Mathf.Abs(num2));
					}
					anim.SetBool("Jump", false);
					// The run pose follows the sprint state, never the speed (QA-21): speed skills and a dash move faster
					// than the old 20 u/s threshold without sprinting, and aiming or a shot never waits for the run pose.
					anim.SetBool("Run", sprinting && enableFire && !Aiming && enableCamRotate && speed.z > 1f);
				}
				else
				{
					anim.SetBool("Jump", true);
					anim.SetBool("Run", false);
					camAnim.SetFloat("Speed", 0f);
				}
			}
			else
			{
				camAnim = ct.GetComponent<Animator>();
			}
			if (RogueVertical)
			{
				rogueLift = 0f; rogueFall = 0f;
			}
			else if (!jumping && !movedWithGravity)
			{
				cc.Move(Vector3.down * Time.deltaTime * 9.81f);
				// The fall itself comes from the controller gravity applied in OnAnimatorMove (SimpleMove accumulates it); this constant
				// term only cancels the legacy upward move above. Scaling the constant made a Roguelike low-gravity zone lift players
				// 1.4 m before an unchanged fall (F23). Low gravity instead adds a lift that grows with the time spent airborne.
				float gravityScale = RoguelikeMode.Active ? RogueHooks.GravityScale(this) : 1f;
				if (gravityScale < 1f && !isGrounded()) { rogueLift += 9.81f * (1f - gravityScale) * Time.deltaTime; cc.Move(Vector3.up * rogueLift * Time.deltaTime); }
				else rogueLift = 0f;
				// Characters are about 6 m tall, so real-world gravity makes a rooftop drop feel floaty (80 m took 4.1 s).
				// Roguelike descents (never the rise of a jump, never inside a low-gravity zone) fall with extra gravity.
				if (RoguelikeMode.Active && gravityScale >= 1f && !isGrounded() && cc.velocity.y < -0.5f) { rogueFall += 9.81f * (RogueHooks.FallGravityMul - 1f) * Time.deltaTime; cc.Move(Vector3.down * rogueFall * Time.deltaTime); }
				else rogueFall = 0f;
			}
			else { rogueLift = 0f; rogueFall = 0f; }
			if (Menu.changedSettings)
			{
				// The catch-up path (spawn, respawn, scene start). The option handlers also call
				// ApplyCameraEffects directly, so a change shows while this branch is not running.
				ApplyCameraEffects(am, fp, fxaa, ed, cg);
				Menu.changedSettings = false;
			}
		}
		else if (MyView(base.gameObject) && !enableControl)
		{
			Vector3 zero = Vector3.zero;
			cc.Move(zero);
			ptv.SetSynchronizedValues(zero, 0f);
			StopSprint(true);
		}
	}

	// Applies the graphics toggles (anti-aliasing, depth of field, motion blur, edge rendering, saturation
	// filter) to the player's view camera now. Update only consumes Menu.changedSettings while the local
	// player has control, so a change made while downed, in a cutscene or in a kill cinematic used to
	// wait until control returned. A camera without the effect components (menu, spectator) is left alone.
	public static void ApplyCameraEffects(Camera cam)
	{
		if (cam == null)
		{
			return;
		}
		ApplyCameraEffects(cam.GetComponent<AmplifyMotionEffect>(), cam.GetComponent<FxPro>(), cam.GetComponent<FXAA>(), cam.GetComponent<EdgeDetectEffectNormals>(), cam.GetComponent<CC_Grayscale>());
	}

	private static void ApplyCameraEffects(AmplifyMotionEffect motion, FxPro fx, FXAA antiAliasing, EdgeDetectEffectNormals edge, CC_Grayscale gray)
	{
		// The kill cinematic turned the view camera's effects off on purpose and draws through its own
		// camera; it takes the new values there and restores the view camera itself when it ends.
		if (Supershot.ReapplyEffectSettings())
		{
			return;
		}
		if (Menu.VRmode)
		{
			if (motion != null) motion.enabled = false;
			if (fx != null) fx.enabled = false;
			if (edge != null) edge.enabled = false;
			if (gray != null) gray.enabled = false;
			if (motion != null || fx != null || edge != null || gray != null) QualitySettings.antiAliasing = 0;
			return;
		}
		if (motion != null) motion.enabled = motionBlur;
		if (fx != null) fx.enabled = dof;
		if (antiAliasing != null) antiAliasing.enabled = aa;
		if (edge != null) edge.enabled = edgeRendering;
		// A downed Roguelike player keeps the forced grayscale; RogueDownedPresentation reads the
		// setting every frame and restores it when the player is back up.
		if (gray != null) gray.enabled = saturationFilter;
	}

	// ---- Sprint state (rules at the fields above)
	private RoguePlayer roguePlayer;

	private bool RogueDashing
	{
		get
		{
			if (roguePlayer == null)
			{
				roguePlayer = GetComponent<RoguePlayer>();
			}
			return roguePlayer != null && roguePlayer.Dashing;
		}
	}

	/// <summary>Ends the owner's sprint now. hard: carry start, going down, menus; a held sprint key must be released and pressed again.</summary>
	public void StopSprint(bool hard)
	{
		sprinting = false;
		sprintLatched = false;
		if (hard)
		{
			sprintNeedsRelease = true;
			sprintOutShot = 0;
			if (anim != null)
			{
				anim.SetBool("Run", false);
			}
		}
	}

	private void UpdateSprint(ref float forward, ref float right, bool held, bool pressed, int mode, bool fireHeld, bool aimHeld)
	{
		if (!held)
		{
			sprintNeedsRelease = false;
		}
		if (!GameplayActive || RogueJumpBlocked || !RogueAllows(RogueAction.Sprint))
		{
			// A hard stop: a key still held has to be let go first; one let go meanwhile starts a sprint as soon as it is pressed.
			StopSprint(true);
			sprintNeedsRelease = held;
			return;
		}
		if (pressed && (mode != 0 || !sprintNeedsRelease))
		{
			if (mode != 0)
			{
				sprintLatched = !sprintLatched;
			}
			// The newest press wins over a toggled aim; an aim button that is being held keeps priority.
			if ((mode == 0 || sprintLatched) && Aiming && !aimHeld && !fireHeld)
			{
				Zoom(false);
			}
		}
		// A held trigger (on a weapon that is ready or firing) keeps the sprint off, and so does the moment after it is let go;
		// a long cooldown (a sniper rifle's bolt) alone does not.
		if (fireHeld && (enableFire || firing) && !grabbing && RogueAllows(RogueAction.Fire))
		{
			sprintBlockedUntil = Time.time + SprintAfterShotSeconds;
		}
		bool shooting = sprintOutShot != 0 || Time.time < sprintBlockedUntil;
		bool suppressed = Aiming || shooting || (RoguelikeMode.Active && RogueDashing);
		// A toggled sprint ends when forward input stops (the controller keeps its old 0.2 dead band) or when it is suppressed;
		// a held one only pauses.
		if (forward <= (mode == 2 ? 0.2f : 0f) || (suppressed && mode != 0))
		{
			sprintLatched = false;
		}
		bool wants = sprintLatched || (mode != 1 && held && !sprintNeedsRelease);
		sprinting = wants && !suppressed && forward > 0f && Mathf.Abs(right) < 0.5f && isGrounded();
		if (sprinting)
		{
			forward *= 1.5f;
			right /= 2f;
		}
	}

	// A shot from a sprint (QA-04): the sprint ends at once and the weapon comes up for SprintOutSeconds, then that one shot is
	// sent by FlushSprintOutShot even if the button was only tapped. The owner waits before sending, so every copy fires at the
	// same moment. source: 1 the legacy send (StartCoroutine or the Shoot RPC), 2 the gameplay action dispatcher (desktop).
	// True when the shot may be sent now.
	private bool SprintOutReady(int source)
	{
		if (zombie)
		{
			return true;
		}
		if (sprinting)
		{
			StopSprint(false);
			sprintOutUntil = Time.time + SprintOutSeconds;
			sprintOutShot = source;
			return false;
		}
		return sprintOutShot == 0;
	}

	private void FlushSprintOutShot()
	{
		if (sprintOutShot == 0 || Time.time < sprintOutUntil)
		{
			return;
		}
		int source = sprintOutShot;
		sprintOutShot = 0;
		sprintBlockedUntil = Time.time + SprintAfterShotSeconds;
		if (!GameplayActive || !enableFire || grabbing || zombie || !RogueAllows(RogueAction.Fire))
		{
			return;
		}
		if (source == 2 && actions != null)
		{
			actions.Dispatch(Flats.Core.PlayerAction.Shoot);
		}
		else if (Menu.network == 0)
		{
			StartCoroutine("Shoot");
		}
		else if (Menu.network != 1)
		{
			base.gameObject.GetPhotonView().RPC("Shoot", PhotonTargets.All);
		}
	}

	private void OnAnimatorMove()
	{
		// Input owns planar movement and yaw. Applying the exported locomotion
		// root translation as well introduces frame-dependent reverse impulses.
		// Animator's built-in controller integration also supplied gravity. Keep
		// that controller gravity without importing the clips' planar impulses.
		if (anim != null && cc != null && MyView(base.gameObject) && !movedWithGravity && !RogueVertical)
			cc.SimpleMove(Vector3.zero);
		if (cc != null && mt != null && MyView(base.gameObject)) ferrisWheelFollower.AfterMove(cc, mt);
	}

	private void LateUpdate()
	{
		// Script import order 50 runs after IKController has posed the chest
		// and camera rig, and before scopes (75) and visible tracers (100).
		// Sampling the sight before IK moves it breaks ADS when looking up/down.
		UpdateAimPresentation();
	}

	// Roguelike: a downed player stays on the ground until revived (no jump, no sprint; it crawls).
	private bool RogueJumpBlocked => RoguelikeMode.Active && RogueHooks.JumpBlocked(this);

	/// <summary>Roguelike vertical motion for the local player: jump speed from the target height, one gravity for the rise and the
	/// fall (scaled by low-gravity zones), a terminal speed, and a landing that clears the jump. The motor's ground snap owns grounded frames.</summary>
	private void RogueVerticalMove()
	{
		float dt = Time.deltaTime;
		float g = RogueGravity * RogueHooks.GravityScale(this);
		if (rogueJumpStart || (jumping && !isJump))   // the keyboard/pad/touch jump paths all set jumping from the ground
		{
			rogueJumpStart = false;
			rogueVy = Mathf.Sqrt(2f * RogueGravity * RogueJumpHeight * RogueHooks.JumpHeightMul(this));
			isJump = true;
		}
		else if (movedWithGravity)
		{
			// standing or walking on ground: the motor already pressed the player onto it
			rogueVy = 0f; jumping = false; isJump = false;
			return;
		}
		rogueVy = Mathf.Max(rogueVy - g * dt, -RogueTerminalFall);
		if (rogueVy > 0f && (cc.collisionFlags & CollisionFlags.Above) != 0) rogueVy = 0f;   // head hit a ceiling
		cc.Move(Vector3.up * (rogueVy * dt));
		if (cc.isGrounded && rogueVy <= 0f)
		{
			rogueVy = -2f;   // keeps contact on slopes and steps until the motor's snap takes over
			jumping = false; isJump = false;
		}
		else jumping = rogueVy > 0f;   // the motor skips its ground snap only while rising
	}

	private bool isGrounded()
	{
		// A capsule can rest on a stair edge outside the centre ray. Preserve
		// actual controller ground contact so a supported player can jump again.
		return Flats.Gameplay.PlayerMovementMotor.IsGrounded(cc, mt);
	}

	private void OnApplicationPause(bool pause)
	{
		if (Application.isMobilePlatform && Menu.gameState == "Multiplayer" && pause && MyView(base.gameObject))
		{
			if (Menu.network == 0)
			{
				Debug.Log("Singleplayer");
			}
			else if (Menu.network != 1)
			{
				PhotonNetwork.Disconnect();
			}
			Application.LoadLevel(0);
		}
	}

	public FPSController()
	{
		touchControl = true;

	}




}
