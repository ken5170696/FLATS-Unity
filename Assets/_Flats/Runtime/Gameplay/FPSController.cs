using System;
using System.Collections;
using InControl;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public class FPSController : MonoBehaviour
{
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

	private Transform ui;

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
		int network = Menu.network;
		int num2 = 1;
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
			if (Input.mousePresent || Input.GetJoystickNames().Length > 0)
			{
				touchControl = true;
			}
			else
			{
				touchControl = false;
			}
			reticle.SetVisible(true);
			sight.SetActive(false);
			mt.GetChild(0).gameObject.layer = 13;
			savedFOV = 0f;
			enableCamRotate = true;
			enableControl = true;
			enableFire = true;
		}
		else
		{
			myCamera.SetActive(false);
		}
		GC.Collect();
	}

	[PunRPC]
	private IEnumerator SyncTeam(int[] receivedData)
	{
		int team = receivedData[0];
		int c = receivedData[1];
		int pw = receivedData[2];
		int sw = receivedData[3];
		int pws = receivedData[4];
		int sws = receivedData[5];
		switch (team)
		{
		case 0:
		{
			SkinnedMeshRenderer[] componentsInChildren2 = GetComponentsInChildren<SkinnedMeshRenderer>();
			SkinnedMeshRenderer[] array2 = componentsInChildren2;
			foreach (SkinnedMeshRenderer skinnedMeshRenderer2 in array2)
			{
				skinnedMeshRenderer2.material.color = ui.GetChild(0).GetChild(5).GetChild(1)
					.GetChild(9)
					.GetComponent<Image>()
					.color;
				skinnedMeshRenderer2.gameObject.layer = 8;
			}
			base.gameObject.layer = 8;
			head.layer = 8;
			mask = 1 << LayerMask.NameToLayer("BlueTeam");
			break;
		}
		case 1:
		{
			SkinnedMeshRenderer[] componentsInChildren3 = GetComponentsInChildren<SkinnedMeshRenderer>();
			SkinnedMeshRenderer[] array3 = componentsInChildren3;
			foreach (SkinnedMeshRenderer skinnedMeshRenderer3 in array3)
			{
				skinnedMeshRenderer3.material.color = ui.GetChild(0).GetChild(5).GetChild(1)
					.GetChild(7)
					.GetComponent<Image>()
					.color;
				skinnedMeshRenderer3.gameObject.layer = 9;
			}
			base.gameObject.layer = 9;
			head.layer = 9;
			mask = 1 << LayerMask.NameToLayer("RedTeam");
			break;
		}
		default:
		{
			SkinnedMeshRenderer[] componentsInChildren = GetComponentsInChildren<SkinnedMeshRenderer>();
			SkinnedMeshRenderer[] array = componentsInChildren;
			foreach (SkinnedMeshRenderer skinnedMeshRenderer in array)
			{
				skinnedMeshRenderer.material.color = ui.GetChild(0).GetChild(5).GetChild(1)
					.GetChild(c)
					.GetComponent<Image>()
					.color;
				if (MyView(base.gameObject) || Multiplayer.rule == 6 || Multiplayer.rule == 8)
				{
					skinnedMeshRenderer.gameObject.layer = 8;
					base.gameObject.layer = 8;
					head.layer = 8;
					mask = 1 << LayerMask.NameToLayer("BlueTeam");
				}
				else
				{
					skinnedMeshRenderer.gameObject.layer = 9;
					base.gameObject.layer = 9;
					head.layer = 9;
					mask = 1 << LayerMask.NameToLayer("RedTeam");
				}
			}
			break;
		}
		}
		primaryWeaponIndex = pw;
		secondaryWeaponIndex = sw;
		primaryWeapon = primaryWeapons.GetChild(primaryWeaponIndex);
		secondaryWeapon = secondaryWeapons.GetChild(secondaryWeaponIndex);
		primarySightIndex = pws;
		secondarySightIndex = sws;
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
		primaryWeapon.GetComponent<Gun>().currentAmmo = GunInfo.limitAmmo[primaryWeaponIndex];
		primaryWeapon.GetComponent<Gun>().maxAmmo = GunInfo.limitMaxAmmo[primaryWeaponIndex];
		primaryWeapons.GetChild(secondaryWeaponIndex).GetComponent<Gun>().currentAmmo = GunInfo.limitAmmo[secondaryWeaponIndex];
		primaryWeapons.GetChild(secondaryWeaponIndex).GetComponent<Gun>().maxAmmo = GunInfo.limitMaxAmmo[secondaryWeaponIndex];
		primaryWeapon.gameObject.SetActive(true);
		secondaryWeapon.gameObject.SetActive(true);
		currentGun = primaryWeapon.GetComponent<Gun>();
		if (!MyView(base.gameObject))
		{
			Transform pn = (Transform)UnityEngine.Object.Instantiate(myName);
			yield return new WaitForEndOfFrame();
			pn.GetComponent<InformationUI>().target = mt;
			pn.SetParent(ui.GetChild(1), false);
			pn.SetAsLastSibling();
		}
		if (Multiplayer.rule == 6 && zombie)
		{
			primaryWeapon.gameObject.SetActive(false);
			secondaryWeapon.gameObject.SetActive(false);
			ikc.leftIK = false;
			base.gameObject.name = "Zombie";
			SkinnedMeshRenderer[] componentsInChildren4 = GetComponentsInChildren<SkinnedMeshRenderer>();
			SkinnedMeshRenderer[] array4 = componentsInChildren4;
			foreach (SkinnedMeshRenderer skinnedMeshRenderer4 in array4)
			{
				skinnedMeshRenderer4.material.color = new Color(0.5f, 0.5f, 0.5f);
				skinnedMeshRenderer4.gameObject.layer = 9;
				base.gameObject.layer = 9;
				head.layer = 9;
				mask = 1 << LayerMask.NameToLayer("RedTeam");
			}
		}
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
		EasyTouch.On_SimpleTap -= On_SimpleTap;
		EasyTouch.On_Swipe -= On_Swipe;
		EasyTouch.On_LongTapStart -= On_LongTapStart;
		EasyTouch.On_LongTapEnd -= On_LongTapEnd;
		if (isZoom)
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
		EasyTouch.On_SimpleTap -= On_SimpleTap;
		EasyTouch.On_Swipe -= On_Swipe;
		EasyTouch.On_LongTapStart -= On_LongTapStart;
		EasyTouch.On_LongTapEnd -= On_LongTapEnd;
	}

	private void ApplyLook(Flats.Core.LookInput input, float x, float y)
	{
		var look = Flats.Core.LookRotationPolicy.Evaluate(input, x, y, sensitivity, invertY,
			isZoom, isZoom ? currentGun.zoom : 1f, headTracking, VRController.device == "cardboard",
			headRotation.x, headRotation.y, Menu.current == "Playing", testMode,
			mct.localEulerAngles.x, mct.localEulerAngles.y);
		mt.eulerAngles += new Vector3(0f, look.BodyYaw, 0f);
		mct.localEulerAngles = new Vector3(look.CameraPitch, look.CameraYaw, 0f);
	}

	private void On_Swipe(Gesture gesture)
	{
		if (enableCamRotate && MyView(base.gameObject) && (handedness != 0 || !(gesture.startPosition.x < (float)(Screen.width / 2))) && (handedness != 1 || !(gesture.startPosition.x > (float)(Screen.width / 2))))
		{
			ApplyLook(Flats.Core.LookInput.Touch, gesture.deltaPosition.x, gesture.deltaPosition.y);
		}
	}

	private void On_SimpleTap(Gesture gesture)
	{
		if (!tapFiring || !MyView(base.gameObject) || gesture.isOverGui)
		{
			return;
		}
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

	private void On_LongTapEnd(Gesture gesture)
	{
		if (ltr != null)
		{
			UnityEngine.Object.Destroy(ltr.gameObject);
		}
	}

	private void On_LongTapStart(Gesture gesture)
	{
		if (ETCInput.GetButton("Fire") || ETCInput.GetButton("Reload") || ETCInput.GetButton("Jump") || ETCInput.GetButton("Zoom"))
		{
			return;
		}
		if (grabbedObject == null && enableFire && !isZoom && !grabbing && droppedGun == null && tapFiring)
		{
			ltr = UnityEngine.Object.Instantiate(longTapRing, Vector3.zero, Quaternion.identity) as Transform;
			ltr.GetChild(0).position = gesture.position;
			StartCoroutine("GrenadeReady");
		}
		else if (grabbing && grabbedObject != null)
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
		else if (grabbedObject != null && enableFire)
		{
			if (Menu.network == 0)
			{
				int[] array2 = new int[2];
				int[] receivedData2 = array2;
				Grab(receivedData2);
			}
			else if (Menu.network != 1)
			{
				int[] array3 = new int[2]
				{
					0,
					grabbedObject.gameObject.GetPhotonView().viewID
				};
				base.gameObject.GetPhotonView().RPC("Grab", PhotonTargets.AllBuffered, array3);
			}
		}
		else
		{
			if (!(droppedGun != null) || !enableFire)
			{
				return;
			}
			DroppedGun component = droppedGun.GetComponent<DroppedGun>();
			if (component.ready)
			{
				if (Menu.network == 0)
				{
					int[] receivedData3 = new int[5] { component.weaponIndex, component.currentAmmo, component.maxAmmo, component.sight, 0 };
					StartCoroutine(ExchangeWeapons(receivedData3));
					UnityEngine.Object.Destroy(droppedGun.gameObject);
				}
				else if (Menu.network != 1 && base.gameObject.GetPhotonView().isMine)
				{
					int[] array4 = new int[5]
					{
						component.weaponIndex,
						component.currentAmmo,
						component.maxAmmo,
						component.sight,
						droppedGun.gameObject.GetPhotonView().viewID
					};
					base.gameObject.GetPhotonView().RPC("ExchangeWeapons", PhotonTargets.All, array4);
				}
				droppedGun = null;
			}
		}
	}

	public void Zoom(bool zoom)
	{
		if (zombie)
		{
			return;
		}
		if (!isZoom && zoom)
		{
			if (primarySightIndex == 0)
			{
				reticle.SetVisible(false);
			}
			startZooming = true;
		}
		else if (isZoom && !zoom)
		{
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
	public void Grab(int[] receivedData)
	{
		Transform transform = null;
		if (Menu.network == 0)
		{
			transform = UnityEngine.Object.FindObjectOfType<GrabbedObject>().transform;
		}
		else if (Menu.network != 1)
		{
			transform = PhotonView.Find(receivedData[1]).transform;
		}
		if (receivedData[0] == 0 && transform.parent == null && GrabbedObject.canGrab)
		{
			if (transform.GetComponent<GrabbedObject>().objectType == ObjectType.Flag)
			{
				transform.GetComponent<Collider>().enabled = false;
				transform.SetParent(primaryWeapons.parent);
				primaryWeapon.gameObject.SetActive(false);
				enableFire = false;
				grabbing = true;
				transform.GetComponent<Rigidbody>().isKinematic = true;
				transform.localPosition = new Vector3(-0.58f, 0.12f, -0.07f);
				transform.localEulerAngles = new Vector3(0f, 170f, 300f);
				ikc.leftHandObj = transform.GetChild(0);
				anim.SetBool("Flag", true);
				if (Menu.network == 0)
				{
					mt.GetChild(0).gameObject.layer = 8;
				}
				else
				{
					if (Menu.network == 1)
					{
						return;
					}
					if (base.gameObject.GetPhotonView().isMine)
					{
						string text = "";
						text = ((base.gameObject.GetPhotonView().owner.GetTeam() != PunTeams.Team.red) ? "Blue team got the flag." : "Red team got the flag.");
						multiplayer.GetPhotonView().RPC("Log", PhotonTargets.All, text);
						if (base.gameObject.GetPhotonView().owner.GetTeam() == PunTeams.Team.red)
						{
							mt.GetChild(0).gameObject.layer = 8;
						}
						else
						{
							mt.GetChild(0).gameObject.layer = 9;
						}
					}
					Multiplayer.limit += 15;
				}
			}
			else
			{
				if (transform.GetComponent<GrabbedObject>().objectType != ObjectType.Bomb)
				{
					return;
				}
				if (transform.GetComponent<GrabbedObject>().grabbedObjectLayer == base.gameObject.layer)
				{
					if (transform.GetComponent<GrabbedObject>().set)
					{
						return;
					}
					transform.GetComponent<Collider>().enabled = false;
					transform.SetParent(primaryWeapons.parent);
					primaryWeapon.gameObject.SetActive(false);
					enableFire = false;
					grabbing = true;
					transform.GetComponent<Rigidbody>().isKinematic = true;
					transform.localPosition = new Vector3(0f, 0.07f, 0.12f);
					transform.localEulerAngles = new Vector3(0f, 45f, 0f);
					ikc.leftHandObj = transform.GetChild(0);
					anim.SetBool("Bomb", true);
					if (Menu.network == 0)
					{
						mt.GetChild(0).gameObject.layer = 8;
					}
					else if (Menu.network != 1 && base.gameObject.GetPhotonView().isMine)
					{
						if (base.gameObject.GetPhotonView().owner.GetTeam() == PunTeams.Team.red)
						{
							mt.GetChild(0).gameObject.layer = 8;
						}
						else
						{
							mt.GetChild(0).gameObject.layer = 9;
						}
					}
				}
				else if (Menu.network == 0)
				{
					Debug.Log("Singleplayer");
				}
				else if (Menu.network != 1)
				{
					transform.gameObject.GetPhotonView().RPC("Destroy", PhotonTargets.MasterClient);
					if (base.gameObject.GetPhotonView().isMine)
					{
						string text2 = "Reset Bomb.";
						multiplayer.GetPhotonView().RPC("Log", PhotonTargets.All, text2);
					}
				}
			}
		}
		else if (receivedData[0] == 1)
		{
			if (Menu.network == 0)
			{
				mt.GetChild(0).gameObject.layer = 13;
			}
			else if (Menu.network != 1 && base.gameObject.GetPhotonView().isMine)
			{
				mt.GetChild(0).gameObject.layer = 13;
			}
			transform.GetComponent<Collider>().enabled = true;
			transform.SetParent(null);
			grabbedObject = null;
			primaryWeapon.gameObject.SetActive(true);
			enableFire = true;
			grabbing = false;
			transform.GetComponent<Rigidbody>().isKinematic = false;
			transform.eulerAngles = Vector3.zero;
			if (transform.GetComponent<GrabbedObject>().objectType == ObjectType.Flag)
			{
				anim.SetBool("Flag", false);
			}
			else if (transform.GetComponent<GrabbedObject>().objectType == ObjectType.Bomb)
			{
				anim.SetBool("Bomb", false);
			}
		}
	}

	[PunRPC]
	private IEnumerator ExchangeWeapons(int[] receivedData)
	{
		if (zombie)
		{
			yield break;
		}
		if (MyView(base.gameObject))
		{
			if (isZoom)
			{
				Zoom(false);
			}
			reticle.SetVisible(false);
		}
		int[] gunInfo = new int[5];
		new GameObject();
		if (Menu.network == 0)
		{
			gunInfo[0] = receivedData[0];
			gunInfo[1] = receivedData[1];
			gunInfo[2] = receivedData[2];
			gunInfo[3] = receivedData[3];
		}
		else if (Menu.network != 1)
		{
			GameObject gun = PhotonView.Find(receivedData[4]).gameObject;
			DroppedGun component = gun.GetComponent<DroppedGun>();
			gunInfo[0] = component.weaponIndex;
			gunInfo[1] = component.currentAmmo;
			gunInfo[2] = component.maxAmmo;
			gunInfo[3] = component.sight;
			if (PhotonNetwork.isMasterClient)
			{
				PhotonNetwork.Destroy(gun);
			}
		}
		enableFire = false;
		anim.SetBool("Change", true);
		yield return new WaitForSeconds(0.2f);
		ikc.leftIK = false;
		yield return new WaitForSeconds(0.8f);
		primaryWeapon.gameObject.SetActive(false);
		if (primarySightIndex != 0)
		{
			UnityEngine.Object.Destroy(primaryWeapon.GetChild(2).GetChild(0).gameObject);
		}
		new GameObject();
		if (Menu.network == 0)
		{
			GameObject newGun = UnityEngine.Object.Instantiate(Resources.Load("Weapons/Weapon" + primaryWeaponIndex), mt.position + Vector3.up * 3f, Quaternion.identity) as GameObject;
			Vector3 velocity = mt.TransformDirection(0f, 0f, 4f);
			newGun.GetComponent<Rigidbody>().linearVelocity = velocity;
			DroppedGun component2 = newGun.GetComponent<DroppedGun>();
			component2.currentAmmo = currentGun.currentAmmo;
			component2.maxAmmo = currentGun.maxAmmo;
			component2.sight = primarySightIndex;
		}
		else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
		{
			GameObject newGun = PhotonNetwork.InstantiateSceneObject("Weapons/Weapon" + primaryWeaponIndex, mt.position + Vector3.up * 3f, Quaternion.identity, 0, null);
			newGun.GetPhotonView().RPC("DropData", PhotonTargets.All, currentGun.currentAmmo, currentGun.maxAmmo, primarySightIndex);
		}
		primaryWeaponIndex = gunInfo[0];
		primaryWeapon = primaryWeapons.GetChild(primaryWeaponIndex);
		currentGun = primaryWeapon.GetComponent<Gun>();
		currentGun.currentAmmo = gunInfo[1];
		currentGun.maxAmmo = gunInfo[2];
		primarySightIndex = gunInfo[3];
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
		primaryWeapon.gameObject.SetActive(true);
		anim.SetBool("Change", false);
		yield return new WaitForSeconds(0.4f);
		ikc.leftIK = true;
		yield return new WaitForSeconds(0.1f);
		enableFire = true;
		if (MyView(base.gameObject))
		{
			reticle.SetVisible(true);
		}
	}

	[PunRPC]
	public IEnumerator VIP()
	{
		vip = true;
		Text phaseText = ui.GetChild(2).GetChild(0).GetComponent<Text>();
		phaseText.enabled = true;
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1)
		{
			if (base.gameObject.GetPhotonView().owner.GetTeam() == PunTeams.Team.red)
			{
				if (base.gameObject.GetPhotonView().isMine)
				{
					phaseText.text = "You are the VIP.";
				}
				else if (PhotonNetwork.player.GetTeam() == PunTeams.Team.red)
				{
					phaseText.text = "Red team's VIP: " + base.gameObject.GetPhotonView().owner.NickName;
				}
				SkinnedMeshRenderer[] componentsInChildren = GetComponentsInChildren<SkinnedMeshRenderer>();
				SkinnedMeshRenderer[] array = componentsInChildren;
				foreach (SkinnedMeshRenderer skinnedMeshRenderer in array)
				{
					Color color = ui.GetChild(0).GetChild(5).GetChild(1)
						.GetChild(9)
						.GetComponent<Image>()
						.color;
					skinnedMeshRenderer.material.color = new Color(color.r * 2f / 3f, color.g * 2f / 3f, color.b * 2f / 3f);
				}
			}
			else if (base.gameObject.GetPhotonView().owner.GetTeam() == PunTeams.Team.blue)
			{
				if (base.gameObject.GetPhotonView().isMine)
				{
					phaseText.text = "You are the VIP.";
				}
				else if (PhotonNetwork.player.GetTeam() == PunTeams.Team.blue)
				{
					phaseText.text = "Blue team's VIP: " + base.gameObject.GetPhotonView().owner.NickName;
				}
				SkinnedMeshRenderer[] componentsInChildren2 = GetComponentsInChildren<SkinnedMeshRenderer>();
				SkinnedMeshRenderer[] array2 = componentsInChildren2;
				foreach (SkinnedMeshRenderer skinnedMeshRenderer2 in array2)
				{
					Color color2 = ui.GetChild(0).GetChild(5).GetChild(1)
						.GetChild(7)
						.GetComponent<Image>()
						.color;
					skinnedMeshRenderer2.material.color = new Color(color2.r * 2f / 3f, color2.g * 2f / 3f, color2.b * 2f / 3f);
				}
			}
		}
		yield return new WaitForSeconds(2f);
		phaseText.text = "";
		phaseText.enabled = false;
	}

	[PunRPC]
	public IEnumerator Zombie(int zombieID)
	{
		if (biten)
		{
			yield break;
		}
		biten = true;
		Multiplayer.limit += 10;
		if (MyView(base.gameObject) && isZoom)
		{
			Zoom(false);
		}
		base.gameObject.layer = LayerMask.NameToLayer("BlueTeam");
		head.layer = LayerMask.NameToLayer("BlueTeam");
		ikc.leftIK = false;
		mask = 1 << LayerMask.NameToLayer("RedTeam");
		primaryWeapons.gameObject.SetActive(false);
		secondaryWeapons.gameObject.SetActive(false);
		anim.SetBool("Reload", false);
		anim.SetBool("Change", false);
		anim.SetBool("Jump", false);
		enableFire = true;
		if (zombieID == -1)
		{
			enableCamRotate = false;
			enableControl = false;
			ikc.enabled = false;
			Transform camParent = Camera.main.transform.parent;
			Transform cam = camParent.GetChild(0);
			cam.SetParent(null);
			cam.GetComponent<Animator>().enabled = false;
			cam.position = mt.position + mt.up * 5f + mt.forward * 10f;
			cam.LookAt(mt.position + Vector3.up * 4f);
			anim.SetTrigger("Zombie");
			yield return new WaitForSeconds(1f);
			base.GetComponent<AudioSource>().PlayOneShot(zombieSE);
			SkinnedMeshRenderer[] smrs = GetComponentsInChildren<SkinnedMeshRenderer>();
			try
			{
				SkinnedMeshRenderer[] array = smrs;
				foreach (SkinnedMeshRenderer smr in array)
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
			yield return new WaitForSeconds(2f);
			cam.SetParent(camParent);
			cam.localPosition = new Vector3(0f, 0f, 0f);
			cam.localEulerAngles = new Vector3(0f, 0f, 0f);
			ikc.leftHandObj = null;
			ikc.enabled = true;
			enableCamRotate = true;
			enableControl = true;
			motherZombie = true;
			if (Menu.network == 0)
			{
				Debug.Log("Singleplayer");
			}
			else if (Menu.network != 1 && base.gameObject.GetPhotonView().isMine)
			{
				string text = base.gameObject.GetPhotonView().owner.NickName + " has become the mother zombie.";
				multiplayer.GetPhotonView().RPC("Log", PhotonTargets.All, text);
			}
		}
		else
		{
			Transform camParent2 = Camera.main.transform.parent;
			Transform cam2 = camParent2.GetChild(0);
			Transform biter = null;
			if (Menu.network == 0)
			{
				Debug.Log("Singleplayer");
			}
			else if (Menu.network != 1)
			{
				biter = PhotonView.Find(zombieID).transform;
			}
			Animator biterAnim = biter.GetComponent<Animator>();
			if (MyView(base.gameObject) || MyView(biter.gameObject))
			{
				DamageReceiver.invincibility = true;
				Menu.canOpen = false;
				enableCamRotate = false;
				enableControl = false;
				ikc.enabled = false;
				cam2.SetParent(null);
				cam2.GetComponent<Animator>().enabled = false;
				cam2.position = mt.position + mt.up * 5f + mt.forward * 10f;
				cam2.LookAt(mt.position + Vector3.up * 4f);
				if (MyView(base.gameObject))
				{
					enableControl = false;
					anim.SetFloat("Vertical", 0f);
					anim.SetFloat("Horizontal", 0f);
				}
				else if (MyView(biter.gameObject))
				{
					biter.GetComponent<CharacterController>().Move(Vector3.zero);
					biter.position = mt.position + mt.right * -3.2f + mt.forward * -1.5f;
					biter.eulerAngles = mt.eulerAngles + mt.up * 60f;
					biterAnim.SetFloat("Vertical", 0f);
					biterAnim.SetFloat("Horizontal", 0f);
				}
			}
			yield return new WaitForSeconds(0.5f);
			biterAnim.SetBool("ZombieAttack", true);
			if (MyView(biter.gameObject))
			{
				biter.position = mt.position + mt.right * -3.2f + mt.forward * -1.5f;
				biter.eulerAngles = mt.eulerAngles + mt.up * 60f;
			}
			yield return new WaitForSeconds(1f);
			anim.SetTrigger("Zombie");
			yield return new WaitForSeconds(1f);
			base.GetComponent<AudioSource>().PlayOneShot(zombieSE);
			SkinnedMeshRenderer[] smrs2 = GetComponentsInChildren<SkinnedMeshRenderer>();
			try
			{
				SkinnedMeshRenderer[] array2 = smrs2;
				foreach (SkinnedMeshRenderer smr2 in array2)
				{
					while (true)
					{
						float fadeSpeed2 = Time.unscaledDeltaTime;
						float targetR2 = Mathf.MoveTowards(smr2.material.color.r, 0.5f, fadeSpeed2);
						float targetG2 = Mathf.MoveTowards(smr2.material.color.g, 0.5f, fadeSpeed2);
						float targetB2 = Mathf.MoveTowards(smr2.material.color.b, 0.5f, fadeSpeed2);
						smr2.material.color = new Color(targetR2, targetG2, targetB2);
						if (smr2.material.color == new Color(0.5f, 0.5f, 0.5f))
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
			yield return new WaitForSeconds(3f);
			biterAnim.SetBool("ZombieAttack", false);
			yield return new WaitForSeconds(1f);
			if (MyView(base.gameObject) || MyView(biter.gameObject))
			{
				cam2.SetParent(camParent2);
				cam2.localPosition = new Vector3(0f, 0f, 0f);
				cam2.localEulerAngles = new Vector3(0f, 0f, 0f);
				ikc.leftHandObj = null;
				ikc.enabled = true;
				enableCamRotate = true;
				enableControl = true;
				DamageReceiver.invincibility = false;
				Menu.canOpen = true;
			}
		}
		base.gameObject.name = "Zombie";
		zombie = true;
		if (motherZombie)
		{
			GetComponent<DamageReceiver>().hitPoints = 10000f;
		}
		else
		{
			GetComponent<DamageReceiver>().hitPoints = 6000f;
		}
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1 && base.gameObject.GetPhotonView().isMine)
		{
			string text2 = base.gameObject.GetPhotonView().owner.NickName + " has become a zombie.";
			multiplayer.GetPhotonView().RPC("Log", PhotonTargets.All, text2);
		}
	}

	[PunRPC]
	private IEnumerator Smash()
	{
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
			if (MyView(base.gameObject) && isZoom)
			{
				Zoom(false);
			}
			enableFire = false;
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
				enableFire = true;
			}
		}
	}

	[PunRPC]
	private IEnumerator Shoot()
	{
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
			if ((currentGun.maxAmmo <= 0 && currentGun.currentAmmo <= 0) || !(Menu.current == "Playing"))
			{
				yield break;
			}
			enableFire = false;
			if (anim.GetBool("Run"))
			{
				yield return new WaitForSeconds(0.2f);
			}
			InputDevice inputDevice = InputManager.ActiveDevice;
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
			if (currentGun.oneShot)
			{
				GameObject mf = UnityEngine.Object.Instantiate(currentGun.muzzleFlash, firePosition.position, mt.rotation) as GameObject;
				mf.GetComponent<ParticleSystem>().startColor = mt.GetChild(0).GetComponent<Renderer>().material.color;
				base.GetComponent<AudioSource>().PlayOneShot(currentGun.fireSE);
				for (int i = 0; i < currentGun.burstCount; i++)
				{
					float x = UnityEngine.Random.Range(0f - (100f - currentGun.accuracy), 100f - currentGun.accuracy);
					float y = UnityEngine.Random.Range(0f - (100f - currentGun.accuracy), 100f - currentGun.accuracy);
					Vector3 velocity = ((currentGun.id != 15) ? ct.TransformDirection(x, y, 1500f) : ct.TransformDirection(x, y, 800f));
					Rigidbody rigidbody = UnityEngine.Object.Instantiate(bullet, ct.position + ct.forward, ct.rotation) as Rigidbody;
					Bullet component = rigidbody.GetComponent<Bullet>();
					component.shooter = mt;
					component.grenade = currentGun.grenade;
					if (Multiplayer.rule == 6)
					{
						component.damage = currentGun.damage * 1.5f;
					}
					else
					{
						component.damage = currentGun.damage * (1f + (float)Menu.myCharacter.attack * 0.1f);
					}
					rigidbody.gameObject.layer = base.gameObject.layer + 2;
					rigidbody.linearVelocity = velocity;
					currentGun.currentAmmo--;
					if (currentGun.currentAmmo == 0)
					{
						break;
					}
				}
				if (MyView(base.gameObject))
				{
					inputDevice.Vibrate(0.1f);
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
				GameObject mf2 = UnityEngine.Object.Instantiate(currentGun.muzzleFlash, firePosition.position, mt.rotation) as GameObject;
				mf2.GetComponent<ParticleSystem>().startColor = mt.GetChild(0).GetComponent<Renderer>().material.color;
				base.GetComponent<AudioSource>().PlayOneShot(currentGun.fireSE);
				float ram1 = UnityEngine.Random.Range(0f - (100f - currentGun.accuracy), 100f - currentGun.accuracy);
				float ram2 = UnityEngine.Random.Range(0f - (100f - currentGun.accuracy), 100f - currentGun.accuracy);
				Vector3 dir = ct.TransformDirection(ram1, ram2, 1500f);
				Rigidbody b = UnityEngine.Object.Instantiate(bullet, ct.position + ct.forward, ct.rotation) as Rigidbody;
				Bullet bb = b.GetComponent<Bullet>();
				bb.shooter = mt;
				bb.grenade = currentGun.grenade;
				if (Multiplayer.rule == 6)
				{
					bb.damage = currentGun.damage * 1.5f;
				}
				else
				{
					bb.damage = currentGun.damage * (1f + (float)Menu.myCharacter.attack * 0.1f);
				}
				b.gameObject.layer = base.gameObject.layer + 2;
				b.linearVelocity = dir;
				if (MyView(base.gameObject))
				{
					inputDevice.Vibrate(0.1f);
				}
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
		if (MyView(base.gameObject) && isZoom)
		{
			Zoom(false);
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
		currentGun.currentAmmo = current;
		currentGun.maxAmmo = max;
		enableFire = true;
		yield return new WaitForSeconds(0.1f);
	}

	[PunRPC]
	private IEnumerator ChangeWeapons()
	{
		if (zombie)
		{
			yield break;
		}
		if (MyView(base.gameObject) && isZoom)
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
		enableFire = false;
		anim.SetBool("Change", true);
		yield return new WaitForSeconds(0.1f);
		ikc.leftIK = false;
		yield return new WaitForSeconds(0.4f);
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
		yield return new WaitForSeconds(0.35f);
		ikc.leftIK = true;
		currentGun = primaryWeapon.GetComponent<Gun>();
		yield return new WaitForSeconds(0.1f);
		enableFire = true;
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
		if (MyView(base.gameObject) && isZoom)
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
		enableFire = false;
		anim.SetBool("Grenade", true);
		yield return new WaitForSeconds(0.1f);
		ikc.leftIK = false;
		yield return new WaitForSeconds(0.4f);
		float Z = ((!(mct.localEulerAngles.x > 300f)) ? (60f - mct.localEulerAngles.x) : (370f - mct.localEulerAngles.x));
		Vector3 dir = ct.TransformDirection(0f, 0f, Z + 30f);
		Rigidbody b = UnityEngine.Object.Instantiate(grenade, ct.position + ct.forward + ct.right * -0.5f + ct.up, Quaternion.identity) as Rigidbody;
		b.GetComponent<Bullet>().shooter = mt;
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
		enableFire = true;
	}

	private void Update()
	{
		movedWithGravity = false;
		if (MyView(base.gameObject) && enableControl)
		{
			if (Input.mousePresent || Input.GetJoystickNames().Length > 0)
			{
				if (touchControl)
				{
					if (Input.GetJoystickNames().Length > 0)
					{
						Debug.Log("Gamepad Control");
						if (Menu.customControlEnabled)
						{
							EventSystem.current.gameObject.GetComponent<StandaloneInputModule>().enabled = true;
							EventSystem.current.gameObject.GetComponent<InControlInputModule>().enabled = false;
						}
						else
						{
							EventSystem.current.gameObject.GetComponent<StandaloneInputModule>().enabled = false;
							EventSystem.current.gameObject.GetComponent<InControlInputModule>().enabled = true;
						}
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
			if (touchControl)
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
						if (!isZoom && enableFire && !anim.GetBool("Run"))
						{
							Zoom(true);
						}
						else if (isZoom)
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
				bool padUsed = activeDevice.AnyButtonIsPressed || Mathf.Abs(activeDevice.LeftStickX)>0.1f || Mathf.Abs(activeDevice.LeftStickY)>0.1f || Mathf.Abs(activeDevice.RightStickX)>0.1f || Mathf.Abs(activeDevice.RightStickY)>0.1f || activeDevice.LeftTrigger>0.1f || activeDevice.RightTrigger>0.1f;
				if (padUsed) preferGamepad = true;
				else if (Input.anyKey || Mathf.Abs(Input.GetAxisRaw("mouse x"))>0.01f || Mathf.Abs(Input.GetAxisRaw("mouse y"))>0.01f) preferGamepad = false;
				if (preferGamepad && Input.GetJoystickNames().Length > 0 && activeDevice.Name != "None")
				{
					num = activeDevice.LeftStickY;
					num2 = activeDevice.LeftStickX;
					if (enableCamRotate)
					{
						ApplyLook(Flats.Core.LookInput.Gamepad, activeDevice.RightStickX, activeDevice.RightStickY);
					}
					if (Menu.current == "Playing")
					{
						if ((num > 0f && Mathf.Abs(num2) < 0.5f && isGrounded() && !Menu.customControlEnabled && activeDevice.Action1.IsPressed) || (Menu.customControlEnabled && Input.GetButton(Menu.customControl["Jump"])))
						{
							jumpPressTime += 1f * Time.deltaTime;
							if (jumpPressTime > holdTime && num > 0f)
							{
								num *= 1.5f;
								num2 /= 2f;
							}
						}
						else if ((!Menu.customControlEnabled && activeDevice.Action1.WasReleased) || (Menu.customControlEnabled && Input.GetButtonUp(Menu.customControl["Jump"])))
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
						if (num > 0f && Mathf.Abs(num2) < 0.5f && isGrounded() && !Menu.customControlEnabled && activeDevice.LeftStickButton.IsPressed && !activeDevice.Action1.IsPressed)
						{
							num *= 1.5f;
							num2 /= 2f;
						}
						if ((!Menu.customControlEnabled && (activeDevice.RightTrigger.IsPressed || activeDevice.RightBumper.IsPressed)) || (Menu.customControlEnabled && ((Menu.customControl["Fire"].Contains("analog") && Input.GetAxis(Menu.customControl["Fire"]) > 0.8f) || (Menu.customControl["Fire"].Contains("button") && Input.GetButton(Menu.customControl["Fire"])))))
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
						if (((!Menu.customControlEnabled && activeDevice.Action3.WasPressed) || (Menu.customControlEnabled && Input.GetButtonDown(Menu.customControl["Reload"]))) && (enableFire || grabbing))
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
						if ((!Menu.customControlEnabled && activeDevice.Action4.IsPressed) || (Menu.customControlEnabled && Input.GetButton(Menu.customControl["Change"])))
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
						if ((!Menu.customControlEnabled && activeDevice.Action4.WasReleased) || (Menu.customControlEnabled && Input.GetButtonUp(Menu.customControl["Change"])))
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
						if (((!Menu.customControlEnabled && (activeDevice.LeftTrigger.IsPressed || activeDevice.LeftBumper.IsPressed)) || (Menu.customControlEnabled && ((Menu.customControl["Zoom"].Contains("analog") && Input.GetAxis(Menu.customControl["Zoom"]) > 0.8f) || (Menu.customControl["Zoom"].Contains("button") && Input.GetButton(Menu.customControl["Zoom"]))))) && grabbedObject == null && enableFire && !grabbing && !isZoom && enableFire && !anim.GetBool("Run"))
						{
							Zoom(true);
						}
						if (((!Menu.customControlEnabled && (activeDevice.LeftTrigger.WasReleased || activeDevice.LeftBumper.WasReleased)) || (Menu.customControlEnabled && ((Menu.customControl["Zoom"].Contains("analog") && Input.GetAxis(Menu.customControl["Zoom"]) < 0.8f) || (Menu.customControl["Zoom"].Contains("button") && Input.GetButtonUp(Menu.customControl["Zoom"]))))) && grabbedObject == null && !grabbing && isZoom)
						{
							Zoom(false);
						}
						if (((!Menu.customControlEnabled && activeDevice.Action2.WasPressed) || (Menu.customControlEnabled && Input.GetButtonDown(Menu.customControl["Pick"]))) && grabbedObject == null && enableFire && !grabbing)
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
						if (((!Menu.customControlEnabled && (activeDevice.RightStickButton.WasPressed || activeDevice.DPadUp.WasPressed)) || (Menu.customControlEnabled && Input.GetButtonDown(Menu.customControl["Zoom"]))) && Menu.canOpen)
						{
							if (isZoom)
							{
								Zoom(false);
							}
							else if (enableFire && !anim.GetBool("Run"))
							{
								Zoom(true);
							}
						}
					}
				}
				else
				{
					num = Input.GetAxis("Vertical");
					num2 = Input.GetAxis("Horizontal");
					if (num > 0f && Mathf.Abs(num2) < 0.5f && isGrounded() && Input.GetKey(KeyCode.LeftShift))
					{
						num *= 1.5f;
						num2 /= 2f;
					}
					if (enableCamRotate)
					{
						ApplyLook(Flats.Core.LookInput.Mouse, Input.GetAxisRaw("mouse x"), Input.GetAxisRaw("mouse y"));
					}
					if (Input.GetMouseButton(0))
					{
						RaycastHit hitInfo3 = default(RaycastHit);
						if (Physics.SphereCast(ct.position, 2f, ct.forward, out hitInfo3, 3f, mask))
						{
							if (hitInfo3.collider.gameObject.layer != base.gameObject.layer)
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
					if ((Input.GetKeyDown("r")) && enableFire)
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
					if (Input.GetKeyDown("e") && (enableFire || grabbing))
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
					if (Input.GetKeyDown("g") && grabbedObject == null && enableFire && !grabbing)
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
					if (Input.GetKeyDown("space") && !jumping && isGrounded() && !Physics.Raycast(mct.position, Vector2.up, 2f))
					{
						Y = mt.position.y;
						jumping = true;
					}
					if (Input.GetKeyDown("q"))
					{
						if (grabbedObject != null && enableFire)
						{
							if (Menu.network == 0)
							{
								int[] array5 = new int[2];
								int[] receivedData4 = array5;
								Grab(receivedData4);
							}
							else if (Menu.network != 1)
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
							if (Menu.network == 0)
							{
								int[] receivedData5 = new int[2] { 1, 0 };
								Grab(receivedData5);
							}
							else if (Menu.network != 1)
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
								if (Menu.network == 0)
								{
									int[] receivedData6 = new int[5] { component2.weaponIndex, component2.currentAmmo, component2.maxAmmo, component2.sight, 0 };
									StartCoroutine(ExchangeWeapons(receivedData6));
									UnityEngine.Object.Destroy(droppedGun.gameObject);
								}
								else if (Menu.network != 1 && base.gameObject.GetPhotonView().isMine)
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
					if (Input.GetKeyDown(KeyCode.Mouse1))
					{
						if (isZoom)
						{
							Zoom(false);
						}
						else if (enableFire && !anim.GetBool("Run"))
						{
							Zoom(true);
						}
					}
				}
			}
			if (Menu.current != "Playing" && !testMode)
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
						if (isZoom)
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

	private void FixedUpdate()
	{
		if (!MyView(base.gameObject))
		{
			return;
		}
		if (startZooming)
		{
			enableCamRotate = false;
			ct.position = Vector3.MoveTowards(ct.position, primaryWeapon.GetChild(2).position, Time.deltaTime * 20f);
			ct.eulerAngles = Vector3.MoveTowards(ct.eulerAngles, primaryWeapon.GetChild(2).eulerAngles, Time.deltaTime * 20f);
			if (Vector3.Distance(ct.transform.position, primaryWeapon.GetChild(2).position) < 0.05f)
			{
				isZoom = true;
				startZooming = false;
				ct.SetParent(primaryWeapon.GetChild(2));
				ct.localEulerAngles = new Vector3(0f, 0f, 0f);
				ct.localPosition = new Vector3(0f, 0f, 0f);
				camAnim.enabled = false;
				enableCamRotate = true;
				fp.DOFParams.DOFBlurSize = 2f;
			}
		}
		else if (!isZoom && ct.parent != mct && ct.parent != null)
		{
			enableCamRotate = false;
			ct.position = Vector3.MoveTowards(ct.position, mct.position, Time.deltaTime * 20f);
			ct.rotation = Quaternion.RotateTowards(ct.rotation, mct.rotation, Time.deltaTime * 20f);
			if (Vector3.Distance(ct.transform.position, mct.position) < 0.05f)
			{
				ct.SetParent(mct);
				ct.localEulerAngles = new Vector3(0f, 0f, 0f);
				ct.localPosition = new Vector3(0f, 0f, 0f);
				camAnim.enabled = true;
				fp.DOFParams.DOFBlurSize = 1f;
				enableCamRotate = true;
			}
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
		if (!MyView(base.gameObject))
		{
			int network = Menu.network;
			int num = 1;
		}
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

	private IEnumerator SyncAnimation()
	{
		while (true)
		{
			if (!MyView(base.gameObject))
			{
				Vector3 vector = lastPosition;
				if (Menu.network != 1)
				{
					vector = mt.InverseTransformDirection(mt.position - lastPosition) / Time.deltaTime;
				}
				if (anim == null)
				{
					anim = GetComponent<Animator>();
				}
				float num = 10f;
				if (Menu.network == 0)
				{
					num = 5f;
				}
				if (enableControl && Mathf.Abs(vector.z) > num && Mathf.Abs(vector.z) < 30f)
				{
					anim.SetFloat("Vertical", vector.z);
				}
				else
				{
					anim.SetFloat("Vertical", 0f);
				}
				if (enableControl && Mathf.Abs(vector.x) > num && Mathf.Abs(vector.x) < 30f)
				{
					anim.SetFloat("Horizontal", vector.x);
				}
				else
				{
					anim.SetFloat("Horizontal", 0f);
				}
				if (isGrounded())
				{
					anim.SetBool("Jump", false);
					if (vector.z > 20f && !zombie)
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
			}
			yield return new WaitForEndOfFrame();
			lastPosition = mt.position;
			yield return new WaitForSeconds(0f);
		}
	}

	public FPSController()
	{
		touchControl = true;

	}




}
