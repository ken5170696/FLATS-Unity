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
	private bool CanStartAim => (enableFire || firing) && !anim.GetBool("Run");

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

	private bool padSprint;

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
        if (menu != null) menu.BindGameplay(this);
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
				int[] array2 = new int[6]
				{
					num,
					Menu.myCharacter.color,
					Menu.myCharacter.primaryWeapon,
					Menu.myCharacter.secondaryWeapon,
					Menu.myCharacter.sightList[Menu.myCharacter.primaryWeapon],
					Menu.myCharacter.sightList[Menu.myCharacter.secondaryWeapon]
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

	private void ApplyLook(Flats.Core.LookInput input, float x, float y)
	{
		var look = Flats.Core.LookRotationPolicy.Evaluate(input, x, y, isZoom ? FlatsControls.AimSensitivity(sensitivity) : sensitivity, invertY,
			isZoom, isZoom ? currentGun.zoom : 1f, headTracking, VRController.device == "cardboard",
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
			DroppedGun component2 = droppedGun.GetComponent<DroppedGun>();
			if (component2.ready)
			{
				if (SessionNetworkMode == 0)
				{
					int[] receivedData6 = new int[5] { component2.weaponIndex, component2.currentAmmo, component2.maxAmmo, component2.sight, 0 };
					StartCoroutine(ExchangeWeapons(receivedData6));
					UnityEngine.Object.Destroy(droppedGun.gameObject);
				}
				else if (SessionNetworkMode != 1 && base.gameObject.GetPhotonView().isMine)
				{
					int[] array8 = new int[5]
					{
						component2.weaponIndex,
						component2.currentAmmo,
						component2.maxAmmo,
						component2.sight,
						droppedGun.gameObject.GetPhotonView().viewID
					};
					base.gameObject.GetPhotonView().RPC("ExchangeWeapons", PhotonTargets.All, array8);
				}
				droppedGun = null;
			}
		}
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
            }
            else if (touchControl && !overrideInputDevice)
            {
                num = ETCInput.GetAxis("Vertical");
				num2 = ETCInput.GetAxis("Horizontal");
				if (ETCInput.GetButton("Jump"))
				{
					jumpPressTime += 1f * Time.deltaTime;
					if (num > 0f && Mathf.Abs(num2) < 0.5f && jumpPressTime > holdTime && isGrounded())
					{
						num *= 1.5f;
						num2 /= 2f;
					}
				}
				else if (ETCInput.GetButtonUp("Jump"))
				{
					if (jumpPressTime <= holdTime && !jumping)
					{
						if (isGrounded() && !Physics.Raycast(mct.position, Vector2.up, 2f))
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
					if (reloadPressTime > holdTime && (enableFire || grabbing))
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
					if (reloadPressTime <= holdTime && enableFire)
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
					if (zoomPressTime > holdTime && grabbedObject == null && enableFire && !grabbing)
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
					RaycastHit hitInfo = default(RaycastHit);
					if (Physics.SphereCast(ct.position, 2f, ct.forward, out hitInfo, 3f, mask))
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
					else if (enableFire)
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
				else if (FlatsControls.KeyboardOrMouseKeyHeld() || new Vector2(Input.GetAxisRaw("mouse x"), Input.GetAxisRaw("mouse y")).sqrMagnitude > 9f) preferGamepad = false;
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
						// works) and ends when forward input stops or the player aims.
						if (FlatsControls.PadState("Jump", 1) && !jumping && isGrounded() && !Physics.Raycast(mct.position, Vector2.up, 2f))
						{
							Y = mt.position.y;
							jumping = true;
						}
						if (FlatsControls.PadState("Sprint", 1))
						{
							padSprint = !padSprint;
						}
						if (num <= 0.2f || isZoom)
						{
							padSprint = false;
						}
						if (num > 0f && Mathf.Abs(num2) < 0.5f && isGrounded() && (padSprint || FlatsControls.PadState("Sprint")))
						{
							num *= 1.5f;
							num2 /= 2f;
						}
						if (FlatsControls.PadState("Fire", 0))
						{
							RaycastHit hitInfo2 = default(RaycastHit);
							if (Physics.SphereCast(ct.position, 2f, ct.forward, out hitInfo2, 3f, mask))
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
							else if (enableFire)
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
						if (FlatsControls.PadState("Reload", 1) && (enableFire || grabbing))
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
									DroppedGun component = droppedGun.GetComponent<DroppedGun>();
									if (component.ready)
									{
										if (Menu.network == 0)
										{
											int[] receivedData2 = new int[5] { component.weaponIndex, component.currentAmmo, component.maxAmmo, component.sight, 0 };
											StartCoroutine(ExchangeWeapons(receivedData2));
											UnityEngine.Object.Destroy(droppedGun.gameObject);
										}
										else if (Menu.network != 1 && base.gameObject.GetPhotonView().isMine)
										{
											int[] array3 = new int[5]
											{
												component.weaponIndex,
												component.currentAmmo,
												component.maxAmmo,
												component.sight,
												droppedGun.gameObject.GetPhotonView().viewID
											};
											base.gameObject.GetPhotonView().RPC("ExchangeWeapons", PhotonTargets.All, array3);
										}
										droppedGun = null;
									}
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
								if (enableFire || grabbing)
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
						if (FlatsControls.PadState("Grenade", 1) && grabbedObject == null && enableFire && !grabbing)
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
					if (num > 0f && Mathf.Abs(num2) < 0.5f && isGrounded() && input.Sprint)
					{
						num *= 1.5f;
						num2 /= 2f;
					}
					if (enableCamRotate)
					{
						ApplyLook(Flats.Core.LookInput.Mouse, input.LookX, input.LookY);
					}
					if (input.Fire)
					{
						RaycastHit hitInfo3 = default(RaycastHit);
						if (Physics.SphereCast(ct.position, 2f, ct.forward, out hitInfo3, 3f, mask))
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
						else if (enableFire)
						{
							actions.Dispatch(Flats.Core.PlayerAction.Shoot);
						}
					}
					if ((input.Reload) && enableFire)
					{
						actions.Dispatch(Flats.Core.PlayerAction.Reload);
					}
					if (input.ChangeWeapon && (enableFire || grabbing))
					{
						actions.Dispatch(Flats.Core.PlayerAction.ChangeWeapons);
					}
					if (input.Grenade && grabbedObject == null && enableFire && !grabbing)
					{
						actions.Dispatch(Flats.Core.PlayerAction.ThrowGrenade);
					}
					if (input.Jump && !jumping && isGrounded() && !Physics.Raycast(mct.position, Vector2.up, 2f))
					{
						Y = mt.position.y;
						jumping = true;
					}
					if (input.Interact)
					{
						TryInteract();
					}
					if (FlatsControls.HoldToAim)
					{
						// Holding re-enters aim once a reload, sprint or weapon change ends.
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
			if (!GameplayActive)
			{
				num = 0f;
				num2 = 0f;
			}
			ferrisWheelFollower.BeforeMove(cc, mt);
			movedWithGravity = Flats.Gameplay.PlayerMovementMotor.Move(cc, mt, jumping, zombie, num, num2, Time.deltaTime);
			if (jumping)
			{
				if (mt.parent == null)
				{
					cc.Move(mt.up * 12f * Time.deltaTime);
					if (!isGrounded())
					{
						isJump = true;
					}
					if (isGrounded() && isJump)
					{
						jumping = false;
					}
					if (mt.position.y >= Y + 5f || Physics.Raycast(mct.position, Vector2.up, 2f))
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
				anim.SetFloat("Vertical", speed.z);
				anim.SetFloat("Horizontal", speed.x);
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
					if (speed.z > 20f && enableFire)
					{
						if (!isZoom && enableCamRotate)
						{
							anim.SetBool("Run", true);
						}
						if (Aiming)
						{
							Zoom(false);
						}
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
					camAnim.SetFloat("Speed", 0f);
				}
			}
			else
			{
				camAnim = ct.GetComponent<Animator>();
			}
			if (!jumping && !movedWithGravity)
			{
				cc.Move(Vector3.down * Time.deltaTime * 9.81f);
			}
			if (Menu.changedSettings)
			{
				if (Menu.VRmode)
				{
					am.enabled = false;
					fp.enabled = false;
					ed.enabled = false;
					cg.enabled = false;
					QualitySettings.antiAliasing = 0;
				}
				else
				{
					am.enabled = motionBlur;
					fp.enabled = dof;
					fxaa.enabled = aa;
					ed.enabled = edgeRendering;
					cg.enabled = saturationFilter;
				}
				Menu.changedSettings = false;
			}
		}
		else if (MyView(base.gameObject) && !enableControl)
		{
			Vector3 zero = Vector3.zero;
			cc.Move(zero);
			ptv.SetSynchronizedValues(zero, 0f);
		}
	}

	private void OnAnimatorMove()
	{
		// Input owns planar movement and yaw. Applying the exported locomotion
		// root translation as well introduces frame-dependent reverse impulses.
		// Animator's built-in controller integration also supplied gravity. Keep
		// that controller gravity without importing the clips' planar impulses.
		if (anim != null && cc != null && MyView(base.gameObject) && !movedWithGravity)
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
