using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ExitGames.Client.Photon;
using InControl;
using Reign;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public partial class Menu : MonoBehaviour
{
	public bool resetData;

	public static int network = 0; private static readonly bool offlineNotifications = false;

	private static InterstitialAd ad;

	private static Ad adForWin;

	public static bool created = false;

	private Transform mt;

	private bool fliping;

	public ETCJoystick stick;

	public AudioClip pressSE;

	public AudioClip cancelSE;

	public Renderer backgroundRenderer;
	private Material runtimeBackgroundMaterial;
    private Material runtimeMainUI, runtimeSelected;
    private Material originalMainUI, originalSelected;
    private Graphic[] themedGraphics;

	public Material mainUI;

	public Material selected;

	public GameObject backButton;

	public GameObject uploadButton;

	public GameObject quitButton;

	public GameObject update;

	public GameObject[] specialButtons;

	public Text news;

	public Text purchaseButton;

	public Text resultIndex;

	public Text voteMap;

	public Image[] buttons;

	public Sprite[] images;

	public Sprite[] weapons;

	public AnimationClip fade;

	public Texture2D defaultIcon;

	public Transform leaderboardScroll;

	public GameObject leaderboardContent;

	public GameObject leaderboardLoading;

	public dreamloLeaderBoard dl;

	public Transform multiplayerResult;

	public Transform singleplayerResult;

	public Transform multiplayerList;

	public Transform multiplayerResultList;

	public GameObject playerButton;

	public GameObject result;

	public GameObject errorMessage;

	public GameObject confirm;

	public GameObject aboutUs;

	public GameObject notification;

	public Toggle stayRoom;

	public Button startNow;

	public static bool waitBackground = false;

	private GameObject myButton;

	private Animator anim;

	private Text[] bt;

	private int page;

	public static string current = "Main";

	public static GameObject currentDetail;

	public static string gameState = "Main";

	public static string version = "";

	private string multiScore;

	private string singleScore;

	private string totalScore;

	private Vector2 fireButtonPosition;

	private Vector2 reloadButtonPosition;

	private Vector2 actionButtonPosition;

	private Vector2 grenadeButtonPosition;

	private Dictionary<int, string> aaText;

	private Dictionary<int, string> dofText;

	private Dictionary<int, string> motionBlurText;

	private Dictionary<int, string> edgeRenderingText;

	private Dictionary<int, string> saturationFilterText;

	private Dictionary<int, string> sensitivityText;

	private Dictionary<int, string> handednessText;

	private Dictionary<int, string> yAxisText;

	private Dictionary<int, string> autoAimText;

	private Dictionary<int, string> tapFiringText;

	private Dictionary<int, string> resolutionText;

	private Dictionary<int, string> headRotationText;

	private Dictionary<int, string> notificationText;

	private Dictionary<int, string> batteryText;

	private int stage;

	public static int currentSurvivalScore = 0;

	public static int currentSurvivalPhase = 0;

	public static int currentAssortmentScore = 0;

	public static int currentAssortmentPhase = 0;

	public static int currentHeadshotScore = 0;

	public static int currentHeadshotChain = 0;

	public static bool changedSettings = false;

	public static bool canOpen = true;

	public static bool skipTitle = false;

	public static bool adFree = false;

	private GameObject ambient;

	private AudioSource bgm1;

	private AudioSource bgm2;

	private Dictionary<int, string> ruleTitleText;

	private Dictionary<int, string> ruleExpText;

	private Dictionary<string, string> objectiveText;

	private Dictionary<int, string> stageName;

	private Dictionary<string, int> stageText;

	public static int rule = 0;

	public static int objective = 0;

	public static int playerCount = 0;

	public static int botCount = 0;

	private float savedTimeScale;

	private int savedWeapon;

	public static Dictionary<int, string> sightDictionary = new Dictionary<int, string>();

	private List<Map> vote;

	private bool voted;

	private bool wasInRoom;

	private int syncedPlayer;

	public Transform chat;

	public static bool customControlEnabled = false;

	public static bool backWithCancel = false;

	private bool resetCustomizing;

	private float resetTime;

	public static Dictionary<string, string> customControl = new Dictionary<string, string>();

	private StandaloneInputModule standaloneModule;

	private InControlInputModule inControlModule;

	public static bool VRmode = false;

	private int currentCullingMask;

	private string syncData;

	private bool syncing;

	public static bool startNowPressed = false;

	public static int startNowPlayer = 0;

	public InputField invitationRoomName;

	public GameObject roomCreation;

	public GameObject pleaseWait;

	public GameObject ipButton;

	private Text[] roomTexts;

	private string url;

	private bool fireTV;

	public Sprite flatsLogo;

	public static Character myCharacter;

	public static Current myCurrent;

	public static Settings mySettings;

	public static string oldCharacter;

	public static string oldCurrent;

	public static string oldSettings;

	public List<int> invitedRules;

	public int currentInvitedRule;

	public static bool gettingRoomList = false;

	public RoomInfo[] roomList;

	public float accepting;

	private bool preCheckToStayRoom;

	private float framerateLimit;

	private float deltaTime;

	private int framerateAlert;

	private bool framerateAlertIsEnabled;

	private float picking;

	public static List<PlayerInfo> localNetworkPlayerList = new List<PlayerInfo>();

	public List<string> playerListForCheck;

    internal Material ResolveThemeMaterial(Material material)
    {
        if (originalMainUI != null && material == originalMainUI) return runtimeMainUI;
        if (originalSelected != null && material == originalSelected) return runtimeSelected;
        return material;
    }

    private void BindThemeMaterials()
    {
        // Button clips assign shared asset references after Awake. Resolve them
        // before Canvas rebuild so normal and highlighted states use the theme.
        if (themedGraphics == null) return;
        foreach (var graphic in themedGraphics)
        {
            if (graphic == null) continue;
            var material = ResolveThemeMaterial(graphic.material);
            if (graphic.material != material) graphic.material = material;
        }
    }
	private void OnDestroy()
	{
        Canvas.preWillRenderCanvases -= BindThemeMaterials;
		if (runtimeBackgroundMaterial != null) Destroy(runtimeBackgroundMaterial);
        if (runtimeMainUI != null) Destroy(runtimeMainUI);
        if (runtimeSelected != null) Destroy(runtimeSelected);
	}

	private void Awake()
	{
		// The authored menu clips are quiet and the legacy source was at half
		// volume. Keep this gain on the UI source, separate from weapon audio.
		AudioSource menuSound = GetComponent<AudioSource>();
		menuSound.volume = 1f;
		menuSound.spatialBlend = 0f;
		// Keep animated material references on this menu's owned theme instances.
		if (backgroundRenderer != null) runtimeBackgroundMaterial = backgroundRenderer.material;
        originalMainUI=mainUI; originalSelected=selected;
        if(originalMainUI!=null)mainUI=runtimeMainUI=new Material(originalMainUI);
        if(originalSelected!=null)selected=runtimeSelected=new Material(originalSelected);
        themedGraphics=transform.root.GetComponentsInChildren<Graphic>(true);
        BindThemeMaterials();
        Canvas.preWillRenderCanvases += BindThemeMaterials;
        foreach(var input in GetComponentsInChildren<UnityEngine.UI.InputField>(true))
        {
			// Legacy prefabs lost their InputField text references during import.
			// Without a textComponent, desktop keyboard editing cannot activate.
			if (input.textComponent == null)
			{
				Transform textChild = input.transform.Find("Text");
				if (textChild != null) input.textComponent = textChild.GetComponent<Text>();
			}
			if (input.placeholder == null)
			{
				Transform placeholderChild = input.transform.Find("Placeholder");
				if (placeholderChild != null) input.placeholder = placeholderChild.GetComponent<Graphic>();
			}
			if (input.targetGraphic == null) input.targetGraphic = input.GetComponent<Graphic>();
            if(input.onEndEdit.GetPersistentEventCount()!=0)continue;
            if(input.name=="NameInput")input.onEndEdit.AddListener(NameInput);
            if(input.name=="Comment" && input.transform.parent.name=="Profile")input.onEndEdit.AddListener(CommentInput);
        }
		if (!FlatsLocalProfile.Prepare()) { enabled = false; return; }
		if (resetData)
		{
			FlatsStorageNotice.Show("The legacy resetData flag is enabled. Automatic data deletion was blocked.", true);
			enabled = false; return;
		}
		mt = base.transform;
		InitializeSaveTransfer();
		anim = GetComponent<Animator>();
		Time.timeScale = 1f;
		Application.targetFrameRate = 60;
		Screen.sleepTimeout = -1;
		backWithCancel = false;
		if (Application.loadedLevel == 0)
		{
			mt.parent.GetChild(1).gameObject.SetActive(false);
		}
		if (FlatsPreferences.HasKey("version"))
		{
			version = FlatsPreferences.GetString("version");
		}
		else
		{
			version = "";
		}
		Debug.Log("version:" + version);
		string text = "5.4.1";
		if (version != text)
		{
			if (version == "" || int.Parse(version.Substring(0, 1)) < 5)
			{
				string text3;
				int num;
				int num2;
				int num3;
				if (FlatsPreferences.HasKey("playerid"))
				{
					string text2 = ((!FlatsPreferences.HasKey("playername")) ? "" : FlatsPreferences.GetString("playername"));
					text3 = text2.Replace("$", "");
					if (FlatsPreferences.HasKey("kill"))
					{
						num = FlatsPreferences.GetInt("kill");
						if (num > 2)
						{
							num -= num / 2;
						}
						if (num > 300)
						{
							num = 300;
						}
					}
					else
					{
						num = 0;
					}
					if (FlatsPreferences.HasKey("playername"))
					{
						num2 = FlatsPreferences.GetInt("death");
						if (num2 > 2)
						{
							num2 -= num2 / 2;
						}
						if (num2 > 300)
						{
							num2 = 300;
						}
					}
					else
					{
						num2 = 0;
					}
					if (FlatsPreferences.HasKey("bestscore"))
					{
						num3 = FlatsPreferences.GetInt("bestscore");
						if (num3 > 100000)
						{
							num3 = 100000;
						}
					}
					else
					{
						num3 = 0;
					}
					// Retain legacy keys after migration, including unknown user settings.
				}
				else
				{
					text3 = "Flatman";
					num = 0;
					num2 = 0;
					num3 = 0;
				}
				myCharacter = new Character();
				myCharacter.id = StringUtils.GeneratePassword(10);
				myCharacter.name = text3;
				myCharacter.comment = "No comment.";
				myCharacter.color = 9;
				myCharacter.kill = num;
				myCharacter.death = num2;
				myCharacter.survivalScore = num3;
				myCharacter.assortmentScore = 0;
				myCharacter.headshotScore = 0;
				myCharacter.primaryWeapon = 4;
				myCharacter.secondaryWeapon = 8;
				myCharacter.attack = 5;
				myCharacter.defense = 5;
				myCharacter.sightList = new List<int>
				{
					0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
					4, 5, 0, 0, 0, 0
				};
				mySettings = new Settings();
				mySettings.sound_bgm = 5;
				mySettings.sound_all = 5;
				mySettings.graphics_aa = 0;
				mySettings.graphics_dof = 0;
				mySettings.graphics_motionBlur = 0;
				mySettings.graphics_edgeRendering = 0;
				mySettings.graphics_saturationFilter = 0;
				mySettings.control_sensitivity = 1;
				mySettings.control_handedness = 0;
				mySettings.control_yAxis = 0;
				mySettings.control_autoAim = 1;
				mySettings.control_tapFiring = 0;
				mySettings.vr_resolution = 1;
				mySettings.vr_eyeDistance = 0;
				mySettings.vr_headRotation = 1;
				mySettings.extra_batterySaver = 0;
				mySettings.extra_notification = 0;
				myCurrent = new Current();
				myCurrent.survival_Score = 0;
				myCurrent.survival_Phase = 0;
				myCurrent.assortment_Score = 0;
				myCurrent.assortment_Phase = 0;
				myCurrent.headshot_Score = 0;
				myCurrent.headshot_Chain = 0;
				adFree = false;
				version = text;
				SaveDataController.Save();
				Debug.Log("This is the first play.");
				update.transform.GetChild(1).GetComponent<Text>().text = "Flats version 5";
				update.transform.GetChild(2).GetComponent<Text>().text = "Welcome to Flats.\n\nFlats is a simple cross-platform FPS.\nYou can play single & multiplayer mode.";
				update.transform.GetChild(4).GetComponent<Text>().text = "- Added new game mode and new system.\n- Added LAN multiplayer mode for Android and iOS devices.\n- Now available on Windows 8.1 or later.\n\nNote for updaters from version 4:Your score is taken over,\nbut singleplayer score is limited to 100000\nalso kill and death are halved and limited to 300.";
			}
			else
			{
				if (version.Contains("5.0") || version.Contains("5.1") || version.Contains("5.2"))
				{
					Debug.Log("Old save data has been detected, converting to new save data...");
					string text4 = FlatsPreferences.GetString("character");
					string text5 = FlatsPreferences.GetString("settings");
					string text6 = FlatsPreferences.GetString("current");
					string[] array = text4.Split(new string[1] { "$" }, StringSplitOptions.None);
					myCharacter = new Character();
					myCharacter.id = array[0];
					myCharacter.name = array[1];
					myCharacter.comment = array[2];
					myCharacter.color = IntParseFast(array[3]);
					myCharacter.kill = IntParseFast(array[4]);
					myCharacter.death = IntParseFast(array[5]);
					myCharacter.survivalScore = IntParseFast(array[6]);
					myCharacter.assortmentScore = IntParseFast(array[7]);
					myCharacter.headshotScore = IntParseFast(array[8]);
					myCharacter.primaryWeapon = IntParseFast(array[9]);
					myCharacter.secondaryWeapon = IntParseFast(array[10]);
					myCharacter.attack = IntParseFast(array[11]);
					myCharacter.defense = IntParseFast(array[12]);
					myCharacter.sightList = new List<int>
					{
						0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
						3, 4, 0, 0, 0, 0
					};
					string[] array2 = text5.Split(new string[1] { "$" }, StringSplitOptions.None);
					mySettings = new Settings();
					mySettings.sound_bgm = IntParseFast(array2[0]);
					mySettings.sound_all = IntParseFast(array2[1]);
					mySettings.graphics_aa = IntParseFast(array2[2]);
					mySettings.graphics_dof = IntParseFast(array2[3]);
					mySettings.graphics_motionBlur = IntParseFast(array2[4]);
					mySettings.graphics_edgeRendering = IntParseFast(array2[5]);
					mySettings.graphics_saturationFilter = IntParseFast(array2[6]);
					mySettings.control_sensitivity = IntParseFast(array2[7]);
					mySettings.control_handedness = IntParseFast(array2[8]);
					mySettings.control_yAxis = IntParseFast(array2[9]);
					mySettings.control_autoAim = IntParseFast(array2[10]);
					mySettings.control_tapFiring = IntParseFast(array2[11]);
					mySettings.vr_resolution = IntParseFast(array2[12]);
					mySettings.vr_eyeDistance = IntParseFast(array2[13]);
					mySettings.vr_headRotation = IntParseFast(array2[14]);
					mySettings.extra_batterySaver = IntParseFast(array2[15]);
					mySettings.extra_notification = IntParseFast(array2[16]);
					string[] array3 = text6.Split(new string[1] { "$" }, StringSplitOptions.None);
					myCurrent = new Current();
					myCurrent.survival_Score = IntParseFast(array3[0]);
					myCurrent.survival_Phase = IntParseFast(array3[1]);
					myCurrent.assortment_Score = IntParseFast(array3[2]);
					myCurrent.assortment_Phase = IntParseFast(array3[3]);
					myCurrent.headshot_Score = IntParseFast(array3[4]);
					myCurrent.headshot_Chain = IntParseFast(array3[5]);
					SaveDataController.Save();
				}
				update.transform.GetChild(1).GetComponent<Text>().text = "Update Version " + text;
				update.transform.GetChild(2).GetComponent<Text>().text = "Bug fixes and adjustment.";
				update.transform.GetChild(4).GetComponent<Text>().text = "- Linux: Import old save now opens a file path dialog.\n- Exported saves can be imported again.";
			}
			version = text;
			FlatsPreferences.SetString("version", version);
			FlatsPreferences.Save();
			anim.SetBool("Update", true);
			Debug.Log("Updated version:" + version);
		}
		else if (Input.GetJoystickNames().Length > 0)
		{
			EventSystem.current.firstSelectedGameObject = buttons[0].transform.parent.gameObject;
		}
		Array.Resize(ref bt, 6);
		for (int i = 0; i < buttons.Length; i++)
		{
			bt[i] = buttons[i].transform.parent.GetChild(1).GetComponent<Text>();
            // Main sprite rectangles and padding now match the original 512px assets.
            // Preserve the authored Image RectTransform instead of a tight-crop workaround.
		}
		ambient = GameObject.Find("Ambient");
		AudioSource[] components = ambient.GetComponents<AudioSource>();
		bgm1 = components[0];
		bgm2 = components[1];
		stick.transform.SetAsFirstSibling();
	}

	private void OnJoinedLobby()
	{
		if (!(current != "Multiplayer") || !(current != "Matching"))
		{
			return;
		}
		Debug.Log("Joined lobby");
		invitedRules = new List<int>();
		if (roomList == null || roomList.Length <= 0)
		{
			return;
		}
		for (int i = 0; i < roomList.Length; i++)
		{
			if (roomList[i].IsOpen)
			{
				int item = (int)roomList[i].CustomProperties["R"];
				invitedRules.Add(item);
			}
		}
	}

	private void OnReceivedRoomListUpdate()
	{
		if (!PhotonNetwork.inRoom && current != "Multiplayer" && current != "Matching")
		{
			roomList = PhotonNetwork.GetRoomList();
			if (mySettings.extra_notification == 1)
			{
				PhotonNetwork.Disconnect();
			}
		}
	}

	private IEnumerator ReceiveInvitation()
	{
		while (true)
		{
			yield return new WaitForSeconds(10f);
			if (offlineNotifications && network == 0 && canOpen && !waitBackground && current != "Matching")
			{
				if (PhotonNetwork.inRoom)
				{
					Debug.Log("You are in matchmaking, stop receiving invitation.");
				}
				else
				{
					gettingRoomList = true;
					Connect();
					Debug.Log("Called Connect() to get room list.");
				}
				yield return new WaitForSeconds(10f);
				if (!PhotonNetwork.connected && invitedRules.Count > 0)
				{
					int r = (currentInvitedRule = invitedRules[UnityEngine.Random.Range(0, invitedRules.Count)]);
					Debug.Log("Send invitation.");
					if (Input.GetJoystickNames().Length > 0)
					{
						notification.transform.GetChild(0).GetChild(1).GetComponent<Text>()
							.text = ruleTitleText[r] + "\nHold reload button to join";
					}
					else if (Input.mousePresent)
					{
						notification.transform.GetChild(0).GetChild(1).GetComponent<Text>()
							.text = ruleTitleText[r] + "\nPress enter key to join";
					}
					else
					{
						notification.transform.GetChild(0).GetChild(1).GetComponent<Text>()
							.text = ruleTitleText[r] + "\nTap here to join";
					}
					Animator notificationAnim = notification.GetComponent<Animator>();
					notification.gameObject.SetActive(true);
					notificationAnim.Play("Invitation_On");
					yield return new WaitForSeconds(4f);
					notificationAnim.Play("Invitation_Off");
					yield return new WaitForSeconds(1f);
					notification.gameObject.SetActive(false);
				}
			}
			yield return new WaitForSeconds(0f);
		}
	}

	public void AcceptInvitation()
	{
		rule = currentInvitedRule;
		StopCoroutine("ReceiveInvitation");
		if (current == "Playing")
		{
			OpenMenu();
		}
		StartCoroutine("JoinFromInvitation");
	}

	private IEnumerator JoinFromInvitation()
	{
        while (!Flats.Modules.BuiltinModules.Instance.Center.Ready) yield return null;
		stayRoom.gameObject.SetActive(true);
		ipButton.SetActive(false);
		roomTexts[0].text = ruleTitleText[rule];
		roomTexts[1].text = ruleExpText[rule];
		roomTexts[2].text = "Objective: " + objectiveText[rule + "-" + objective];
		roomTexts[3].text = "Player Count: " + playerCount;
		roomTexts[4].text = "Searching for a room...";
		myButton = (GameObject)UnityEngine.Object.Instantiate(playerButton);
		myButton.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(myCharacter.color)
			.GetComponent<Image>()
			.color;
		myButton.transform.GetChild(0).GetComponent<Image>().sprite = mt.GetChild(5).GetChild(0).GetChild(0)
			.GetChild(0)
			.GetComponent<Image>()
			.sprite;
		myButton.transform.GetChild(1).GetComponent<Text>().text = myCharacter.name;
		DetailInformation di = myButton.GetComponent<DetailInformation>();
		di.canvas = mt;
		di.backgroundColor = myButton.GetComponent<Image>().color;
		di.comment = myCharacter.comment;
		di.kill = myCharacter.kill;
		di.death = myCharacter.death;
		myButton.transform.SetParent(multiplayerList, false);
		Texture2D icon = new Texture2D(128, 128)
		{
			filterMode = FilterMode.Bilinear
		};
		byte[] bytes = System.IO.File.ReadAllBytes((FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath) + "/Flats_UserIcon.png");
		icon.LoadImage(bytes);
		ExitGames.Client.Photon.Hashtable playerProps = new ExitGames.Client.Photon.Hashtable();
		playerProps["K"] = myCharacter.kill;
		playerProps["D"] = myCharacter.death;
		playerProps["TC"] = myCharacter.color;
		playerProps["C"] = myCharacter.comment;
		playerProps["I"] = bytes;
        PublishRoomModules(playerProps);
		PhotonNetwork.SetPlayerCustomProperties(playerProps);
		PhotonNetwork.player.NickName = myCharacter.name;
		anim.SetBool("Fade", true);
		yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
		if (currentDetail != null)
		{
			currentDetail.SetActive(false);
		}
		quitButton.SetActive(false);
		anim.SetBool("Matching", true);
		anim.SetBool("Detail", false);
		anim.SetTrigger("SkipToMatching");
		current = "Matching";
		if (!PhotonNetwork.connected)
		{
			Connect();
		}
		if (PhotonNetwork.inRoom)
		{
			while (PhotonNetwork.inRoom)
			{
				yield return new WaitForSeconds(0f);
			}
		}
		while (!PhotonNetwork.connectedAndReady)
		{
			yield return new WaitForSeconds(0f);
		}
		ExitGames.Client.Photon.Hashtable customProps = new ExitGames.Client.Photon.Hashtable();
		if (rule != 0)
		{
			customProps["R"] = rule;
		}
		PhotonNetwork.JoinRandomRoom(customProps, 0);
		preCheckToStayRoom = true;
	}

	public void OnDrag(int btn)
	{
		string text = "";
		switch (btn)
		{
		case 1:
			text = "Fire Button";
			break;
		case 2:
			text = "Reload Button";
			break;
		case 3:
			text = "Action Button";
			break;
		case 4:
			text = "Grenade&Aim Button";
			break;
		}
		RectTransform rectTransform = currentDetail.transform.GetChild(1).GetChild(1).GetChild(btn)
			.rectTransform();
		rectTransform.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
		currentDetail.transform.GetChild(1).GetChild(0).GetChild(2)
			.GetComponent<Text>()
			.text = text + "  X:" + rectTransform.anchoredPosition.x.ToString("F0") + " Y:" + rectTransform.anchoredPosition.y.ToString("F0");
		picking += 1f;
		Debug.Log("Picking..." + UnityEngine.Random.Range(0, 10));
	}

	public void OnClickedUp(int btn)
	{
		if (picking < 1f)
		{
			RectTransform rectTransform = currentDetail.transform.GetChild(1).GetChild(1).GetChild(btn)
				.rectTransform();
			if (rectTransform.localScale.x == 1f)
			{
				rectTransform.localScale = new Vector3(1.5f, 1.5f, rectTransform.localScale.z);
			}
			else if (rectTransform.localScale.x == 1.5f)
			{
				rectTransform.localScale = new Vector3(2f, 2f, rectTransform.localScale.z);
			}
			else
			{
				rectTransform.localScale = new Vector3(1f, 1f, rectTransform.localScale.z);
			}
		}
		else
		{
			Debug.Log("Just dragged...");
		}
		picking = 0f;
	}

	private void Connect()
    {
        PhotonNetwork.automaticallySyncScene = false;
        PhotonNetwork.BackgroundTimeout = 60f;
        if (!FlatsPhotonConfiguration.Apply(out multiplayerFailure)) return;
        PhotonNetwork.offlineMode = false;
        if (!PhotonNetwork.ConnectUsingSettings(version.Substring(0, 3)))
            multiplayerFailure = "Photon rejected the connection request: " + PhotonNetwork.connectionStateDetailed;
    }

	private static bool tutorialLaunchConsumed;

	private IEnumerator Start()
	{
        StartCoroutine(ShowPendingModuleRejection());
		SocialDesc desc = new SocialDesc
		{
			BB10_ShareSelectorUI = null,
			BB10_ShareSelectorTitle = null,
			BB10_CloseButton = null,
			BB10_ShareSelectorBBM = null,
			BB10_ShareSelectorFacebook = null,
			BB10_ShareSelectorTwitter = null
		};
		SocialManager.Init(desc);
		if (VRController.device == "oculus")
		{
			quitButton.transform.GetChild(0).gameObject.SetActive(false);
		}
		stick.transform.SetAsFirstSibling();
		standaloneModule = EventSystem.current.gameObject.GetComponent<StandaloneInputModule>();
		inControlModule = EventSystem.current.gameObject.GetComponent<InControlInputModule>();
        // Unity 2020's StandaloneInputModule already handles touch. The legacy
        // forced TouchInputModule steals activation between mouse down and up.
        foreach (var legacyTouch in EventSystem.current.GetComponents<TouchInputModule>()) legacyTouch.enabled = false;
        inControlModule.submitButton = InControlInputModule.Button.Action1;
        inControlModule.cancelButton = InControlInputModule.Button.Action2;
        // UI animation must not apply character root motion to RectTransform rows.
        foreach (var uiAnimator in GetComponentsInChildren<Animator>(true)) uiAnimator.applyRootMotion = false;
		quitButton.transform.GetChild(0).gameObject.SetActive(false);
		SaveDataController.Load();
		int wait = 0;
		while (myCharacter == null || myCurrent == null)
		{
			wait++;
			if (wait > 1000)
			{
				Debug.Log("No save data!!");
				LoadOfflineScene(0);
			}
			yield return new WaitForSeconds(0f);
		}
		if (adForWin != null)
		{
			adForWin.Visible = false;
		}
		version = FlatsPreferences.GetString("version");
		currentSurvivalScore = myCurrent.survival_Score;
		currentSurvivalPhase = myCurrent.survival_Phase;
		currentAssortmentScore = myCurrent.assortment_Score;
		currentAssortmentPhase = myCurrent.assortment_Phase;
		currentHeadshotScore = myCurrent.headshot_Score;
		currentHeadshotChain = myCurrent.headshot_Chain;
		if (myCharacter.attack + myCharacter.defense > 10)
		{
			myCharacter.attack = 0;
			myCharacter.defense = 0;
		}
		if (!System.IO.File.Exists((FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath) + "/Flats_UserIcon.png"))
		{
			if (System.IO.File.Exists((FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath) + "/UserIcon.png"))
			{
				System.IO.File.Delete((FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath) + "/UserIcon.png");
			}
			byte[] bytes = defaultIcon.EncodeToPNG();
			System.IO.File.WriteAllBytes((FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath) + "/Flats_UserIcon.png", bytes);
		}
		Texture2D icon = new Texture2D(128, 128)
		{
			filterMode = FilterMode.Bilinear
		};
		byte[] bytes2 = System.IO.File.ReadAllBytes((FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath) + "/Flats_UserIcon.png");
		icon.LoadImage(bytes2);
		if (icon.width < 128 || icon.height < 128)
		{
			byte[] bytes3 = defaultIcon.EncodeToPNG();
			System.IO.File.WriteAllBytes((FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath) + "/Flats_UserIcon.png", bytes3);
			bytes2 = System.IO.File.ReadAllBytes((FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath) + "/Flats_UserIcon.png");
			icon.LoadImage(bytes2);
		}
		mt.GetChild(5).GetChild(0).GetChild(0)
			.GetChild(0)
			.GetComponent<Image>()
			.sprite = Sprite.Create(icon, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f));
		mt.GetChild(5).GetChild(0).GetChild(1)
			.GetComponent<InputField>()
			.text = myCharacter.name;
		mt.GetChild(5).GetChild(0).GetChild(2)
			.GetComponent<InputField>()
			.text = myCharacter.comment;
		if (myCharacter.death == 0)
		{
			multiScore = ((float)myCharacter.kill).ToString("F2");
		}
		else
		{
			multiScore = ((float)myCharacter.kill / (float)myCharacter.death).ToString("F2");
		}
		singleScore = ((float)((myCharacter.survivalScore + myCharacter.assortmentScore + myCharacter.headshotScore) / 3)).ToString("F0");
		totalScore = ((float.Parse(multiScore) + 1f) * float.Parse(singleScore) / 2f).ToString("F0");
		mt.GetChild(5).GetChild(2).GetChild(1)
			.GetComponent<Text>()
			.text = totalScore;
		mt.GetChild(5).GetChild(2).GetChild(3)
			.GetComponent<Text>()
			.text = multiScore;
		mt.GetChild(5).GetChild(2).GetChild(5)
			.GetComponent<Text>()
			.text = myCharacter.kill.ToString();
		mt.GetChild(5).GetChild(2).GetChild(7)
			.GetComponent<Text>()
			.text = myCharacter.death.ToString();
		mt.GetChild(5).GetChild(2).GetChild(9)
			.GetComponent<Text>()
			.text = singleScore;
		mt.GetChild(5).GetChild(2).GetChild(11)
			.GetComponent<Text>()
			.text = myCharacter.survivalScore.ToString();
		mt.GetChild(5).GetChild(2).GetChild(13)
			.GetComponent<Text>()
			.text = myCharacter.assortmentScore.ToString();
		mt.GetChild(5).GetChild(2).GetChild(15)
			.GetComponent<Text>()
			.text = myCharacter.headshotScore.ToString();
		mt.GetChild(5).GetChild(3).GetChild(0)
			.GetChild(1)
			.GetComponent<Image>()
			.sprite = mt.GetChild(5).GetChild(3).GetChild(2)
			.GetChild(myCharacter.primaryWeapon)
			.GetChild(0)
			.GetComponent<Image>()
			.sprite;
		mt.GetChild(5).GetChild(3).GetChild(0)
			.GetChild(2)
			.GetComponent<Text>()
			.text = mt.GetChild(5).GetChild(3).GetChild(2)
			.GetChild(myCharacter.primaryWeapon)
			.GetChild(1)
			.GetComponent<Text>()
			.text;
		mt.GetChild(5).GetChild(3).GetChild(1)
			.GetChild(1)
			.GetComponent<Image>()
			.sprite = mt.GetChild(5).GetChild(3).GetChild(2)
			.GetChild(myCharacter.secondaryWeapon)
			.GetChild(0)
			.GetComponent<Image>()
			.sprite;
		mt.GetChild(5).GetChild(3).GetChild(1)
			.GetChild(2)
			.GetComponent<Text>()
			.text = mt.GetChild(5).GetChild(3).GetChild(2)
			.GetChild(myCharacter.secondaryWeapon)
			.GetChild(1)
			.GetComponent<Text>()
			.text;
		sightDictionary[0] = "none";
		sightDictionary[1] = "reflex sight";
		sightDictionary[2] = "2x sight";
		sightDictionary[3] = "4x sight";
		sightDictionary[4] = "6x sight";
		sightDictionary[5] = "8x sight";
		mt.GetChild(5).GetChild(4).GetChild(1)
			.GetComponent<Text>()
			.text = myCharacter.attack + myCharacter.defense + "/10";
		mt.GetChild(5).GetChild(4).GetChild(2)
			.GetChild(1)
			.GetComponent<Text>()
			.text = myCharacter.attack.ToString();
		mt.GetChild(5).GetChild(4).GetChild(3)
			.GetChild(1)
			.GetComponent<Text>()
			.text = myCharacter.defense.ToString();
		mt.GetChild(5).GetChild(5).GetChild(0)
			.GetComponent<Text>()
			.text = "ID:" + myCharacter.id;
		bgm1.volume = (float)mySettings.sound_bgm / 10f;
		if (Singleplayer.chance)
		{
			bgm2.volume = (float)mySettings.sound_bgm / 10f;
		}
		else
		{
			bgm2.volume = 0f;
		}
		mt.GetChild(6).GetChild(0).GetChild(0)
			.GetChild(1)
			.GetComponent<Text>()
			.text = mySettings.sound_bgm.ToString();
		if (current == "Playing")
		{
			AudioListener.volume = (float)mySettings.sound_all / 10f;
		}
		else
		{
			AudioListener.volume = (float)mySettings.sound_all / 20f;
		}
		mt.GetChild(6).GetChild(0).GetChild(1)
			.GetChild(1)
			.GetComponent<Text>()
			.text = mySettings.sound_all.ToString();
		aaText[0] = "OFF";
		aaText[1] = "ON";
		FPSController.aa = IntToBool(mySettings.graphics_aa);
		mt.GetChild(6).GetChild(1).GetChild(0)
			.GetChild(1)
			.GetComponent<Text>()
			.text = aaText[mySettings.graphics_aa];
		dofText[0] = "OFF";
		dofText[1] = "ON";
		FPSController.dof = IntToBool(mySettings.graphics_dof);
		mt.GetChild(6).GetChild(1).GetChild(1)
			.GetChild(1)
			.GetComponent<Text>()
			.text = dofText[mySettings.graphics_dof];
		motionBlurText[0] = "OFF";
		motionBlurText[1] = "ON";
		FPSController.motionBlur = IntToBool(mySettings.graphics_motionBlur);
		mt.GetChild(6).GetChild(1).GetChild(2)
			.GetChild(1)
			.GetComponent<Text>()
			.text = motionBlurText[mySettings.graphics_motionBlur];
		edgeRenderingText[0] = "OFF";
		edgeRenderingText[1] = "ON";
		FPSController.edgeRendering = IntToBool(mySettings.graphics_edgeRendering);
		mt.GetChild(6).GetChild(1).GetChild(3)
			.GetChild(1)
			.GetComponent<Text>()
			.text = edgeRenderingText[mySettings.graphics_edgeRendering];
		saturationFilterText[0] = "OFF";
		saturationFilterText[1] = "ON";
		FPSController.saturationFilter = IntToBool(mySettings.graphics_saturationFilter);
		mt.GetChild(6).GetChild(1).GetChild(4)
			.GetChild(1)
			.GetComponent<Text>()
			.text = saturationFilterText[mySettings.graphics_saturationFilter];
		sensitivityText[0] = "Low";
		sensitivityText[1] = "Normal";
		sensitivityText[2] = "High";
		FPSController.sensitivity = mySettings.control_sensitivity + 1;
		mt.GetChild(6).GetChild(2).GetChild(0)
			.GetChild(1)
			.GetComponent<Text>()
			.text = sensitivityText[mySettings.control_sensitivity];
		handednessText[0] = "Right";
		handednessText[1] = "Left";
		Transform ui = mt.parent.GetChild(1);
		if (mySettings.control_handedness == 0)
		{
			stick.joystickArea = ETCJoystick.JoystickArea.Left;
			ui.GetChild(1).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterRight;
			ui.GetChild(2).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterRight;
			ui.GetChild(3).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterRight;
			ui.GetChild(4).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterRight;
			ui.GetChild(5).rectTransform().anchoredPosition3D = new Vector3(0f - Mathf.Abs(ui.GetChild(5).rectTransform().anchoredPosition3D.x), ui.GetChild(5).rectTransform().anchoredPosition3D.y, ui.GetChild(5).rectTransform().anchoredPosition3D.z);
			ui.GetChild(6).rectTransform().anchoredPosition3D = new Vector3(0f - Mathf.Abs(ui.GetChild(6).rectTransform().anchoredPosition3D.x), ui.GetChild(6).rectTransform().anchoredPosition3D.y, ui.GetChild(6).rectTransform().anchoredPosition3D.z);
		}
		else
		{
			stick.joystickArea = ETCJoystick.JoystickArea.Right;
			ui.GetChild(1).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterLeft;
			ui.GetChild(2).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterLeft;
			ui.GetChild(3).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterLeft;
			ui.GetChild(4).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterLeft;
			ui.GetChild(5).rectTransform().anchoredPosition3D = new Vector3(Mathf.Abs(ui.GetChild(5).rectTransform().anchoredPosition3D.x), ui.GetChild(5).rectTransform().anchoredPosition3D.y, ui.GetChild(5).rectTransform().anchoredPosition3D.z);
			ui.GetChild(6).rectTransform().anchoredPosition3D = new Vector3(Mathf.Abs(ui.GetChild(6).rectTransform().anchoredPosition3D.x), ui.GetChild(6).rectTransform().anchoredPosition3D.y, ui.GetChild(6).rectTransform().anchoredPosition3D.z);
		}
		FPSController.handedness = mySettings.control_handedness;
		mt.GetChild(6).GetChild(2).GetChild(1)
			.GetChild(1)
			.GetComponent<Text>()
			.text = handednessText[mySettings.control_handedness];
		yAxisText[0] = "Regular";
		yAxisText[1] = "Inverted";
		FPSController.invertY = IntToBool(mySettings.control_yAxis);
		mt.GetChild(6).GetChild(2).GetChild(2)
			.GetChild(1)
			.GetComponent<Text>()
			.text = yAxisText[mySettings.control_yAxis];
		autoAimText[0] = "OFF";
		autoAimText[1] = "ON";
		FPSController.autoAim = IntToBool(mySettings.control_autoAim);
		mt.GetChild(6).GetChild(2).GetChild(3)
			.GetChild(1)
			.GetComponent<Text>()
			.text = autoAimText[mySettings.control_autoAim];
		tapFiringText[0] = "OFF";
		tapFiringText[1] = "ON";
		FPSController.tapFiring = IntToBool(mySettings.control_tapFiring);
		mt.GetChild(6).GetChild(2).GetChild(4)
			.GetChild(1)
			.GetComponent<Text>()
			.text = tapFiringText[mySettings.control_tapFiring];
		resolutionText[0] = "Low";
		resolutionText[1] = "Normal";
		resolutionText[2] = "High";
		if (mySettings.vr_resolution == 2)
		{
			if (VRmode && !(VRController.device == "cardboard") && !(VRController.device == "oculus"))
			{
			}
		}
		else if (mySettings.vr_resolution == 1)
		{
			if (VRmode && !(VRController.device == "cardboard") && !(VRController.device == "oculus"))
			{
			}
		}
		else if (VRmode && !(VRController.device == "cardboard"))
		{
			bool flag = VRController.device == "oculus";
		}
		mt.GetChild(6).GetChild(3).GetChild(0)
			.GetChild(1)
			.GetComponent<Text>()
			.text = resolutionText[mySettings.vr_resolution];
		string eyeDistanceText = ((mySettings.vr_eyeDistance != 0) ? ("+" + (float)mySettings.vr_eyeDistance * 0.5f) : "Default");
		VRController.offset = (float)mySettings.vr_eyeDistance * 0.5f;
		mt.GetChild(6).GetChild(3).GetChild(1)
			.GetChild(1)
			.GetComponent<Text>()
			.text = eyeDistanceText;
		headRotationText[0] = "OFF";
		headRotationText[1] = "ON";
		mt.GetChild(6).GetChild(3).GetChild(2)
			.GetChild(1)
			.GetComponent<Text>()
			.text = headRotationText[mySettings.vr_headRotation];
		batteryText[0] = "OFF";
		batteryText[1] = "ON";
		Application.targetFrameRate = 60 - 30 * mySettings.extra_batterySaver;
		mt.GetChild(6).GetChild(5).GetChild(0)
			.GetChild(1)
			.GetComponent<Text>()
			.text = batteryText[mySettings.extra_batterySaver];
		notificationText[0] = "OFF";
		notificationText[1] = "ON";
		mt.GetChild(6).GetChild(5).GetChild(1)
			.GetChild(1)
			.GetComponent<Text>()
			.text = notificationText[mySettings.extra_notification];
		fireButtonPosition = new Vector2(Mathf.Abs(mt.parent.GetChild(1).GetChild(1).rectTransform()
			.anchoredPosition.x), mt.parent.GetChild(1).GetChild(1).rectTransform()
			.anchoredPosition.y);
		reloadButtonPosition = new Vector2(Mathf.Abs(mt.parent.GetChild(1).GetChild(2).rectTransform()
			.anchoredPosition.x), mt.parent.GetChild(1).GetChild(2).rectTransform()
			.anchoredPosition.y);
		actionButtonPosition = new Vector2(Mathf.Abs(mt.parent.GetChild(1).GetChild(3).rectTransform()
			.anchoredPosition.x), mt.parent.GetChild(1).GetChild(3).rectTransform()
			.anchoredPosition.y);
		grenadeButtonPosition = new Vector2(Mathf.Abs(mt.parent.GetChild(1).GetChild(4).rectTransform()
			.anchoredPosition.x), mt.parent.GetChild(1).GetChild(4).rectTransform()
			.anchoredPosition.y);
		if (FlatsPreferences.HasKey("touchmapping"))
		{
			string text = FlatsPreferences.GetString("touchmapping");
			string[] array = text.Split(new string[1] { "$" }, StringSplitOptions.None);
			mt.parent.GetChild(1).GetChild(1).GetComponent<ETCButton>()
				.anchorOffet = new Vector2(float.Parse(array[0]), float.Parse(array[1]));
			mt.parent.GetChild(1).GetChild(2).GetComponent<ETCButton>()
				.anchorOffet = new Vector2(float.Parse(array[2]), float.Parse(array[3]));
			mt.parent.GetChild(1).GetChild(3).GetComponent<ETCButton>()
				.anchorOffet = new Vector2(float.Parse(array[4]), float.Parse(array[5]));
			mt.parent.GetChild(1).GetChild(4).GetComponent<ETCButton>()
				.anchorOffet = new Vector2(float.Parse(array[6]), float.Parse(array[7]));
			if (array.Length > 8)
			{
				mt.parent.GetChild(1).GetChild(1).rectTransform()
					.localScale = new Vector3(float.Parse(array[8]), float.Parse(array[8]), mt.parent.GetChild(1).GetChild(1).rectTransform()
					.localScale.z);
				mt.parent.GetChild(1).GetChild(2).rectTransform()
					.localScale = new Vector3(float.Parse(array[9]), float.Parse(array[9]), mt.parent.GetChild(1).GetChild(2).rectTransform()
					.localScale.z);
				mt.parent.GetChild(1).GetChild(3).rectTransform()
					.localScale = new Vector3(float.Parse(array[10]), float.Parse(array[10]), mt.parent.GetChild(1).GetChild(3).rectTransform()
					.localScale.z);
				mt.parent.GetChild(1).GetChild(4).rectTransform()
					.localScale = new Vector3(float.Parse(array[11]), float.Parse(array[11]), mt.parent.GetChild(1).GetChild(4).rectTransform()
					.localScale.z);
			}
			Debug.Log("Loaded touch mapping:" + text);
		}
		if (FlatsPreferences.HasKey("controllermapping") && Input.GetJoystickNames().Length > 0)
		{
			string text2 = FlatsPreferences.GetString("controllermapping");
			string[] array2 = text2.Split(new string[1] { "$" }, StringSplitOptions.None);
			if (Input.GetJoystickNames()[0] == array2[0])
			{
				customControl["ControllerName"] = array2[0];
				customControl["Jump"] = "joystick 1 " + array2[1];
				customControl["Pick"] = "joystick 1 " + array2[2];
				customControl["Reload"] = "joystick 1 " + array2[3];
				customControl["Change"] = "joystick 1 " + array2[4];
				customControl["Zoom"] = "joystick 1 " + array2[5];
				customControl["Fire"] = "joystick 1 " + array2[6];
				customControlEnabled = true;
			}
			else
			{
				ShowConfirm("Detected another controller.", "Your custom button mapping has been deleted.", null, "OK", null);
				FlatsPreferences.DeleteKey("controllermapping");
				FlatsPreferences.Save();
				customControlEnabled = false;
			}
		}
		else
		{
			customControlEnabled = false;
		}
		mt.GetChild(7).GetChild(0).GetChild(3)
			.GetComponent<Text>()
			.text = totalScore;
		if (Input.GetJoystickNames().Length > 0)
		{
			if (customControlEnabled)
			{
				standaloneModule.submitButton = customControl["Jump"];
				standaloneModule.cancelButton = customControl["Pick"];
				standaloneModule.enabled = false;
				inControlModule.enabled = true;
			}
			else
			{
				standaloneModule.submitButton = "Submit";
				standaloneModule.cancelButton = "Cancel";
				standaloneModule.enabled = true;
				inControlModule.enabled = false;
			}
		}
		else
		{
			standaloneModule.enabled = true;
			inControlModule.enabled = false;
			EventSystem.current.SetSelectedGameObject(null);
		}
        // Original shared materials retained their theme across scenes. Restore
        // owned instances here, after Start has loaded the saved character.
        Color savedTheme = mt.GetChild(5).GetChild(1).GetChild(myCharacter.color).GetComponent<Image>().color;
        mainUI.color = MainThemeColor(savedTheme);
        selected.color = savedTheme;
		if (Application.loadedLevel == 0)
		{
			gameState = "Main";
			current = "Main";
			if (!skipTitle)
			{
				backgroundRenderer.sharedMaterial.color = new Color(0f, 0f, 0f, 1f);
				mainUI.color = new Color(0.5f, 0.5f, 0.5f, mainUI.color.a);
				if (Application.platform == RuntimePlatform.MetroPlayerX86)
				{
					mt.GetChild(1).GetComponent<Image>().enabled = false;
					mt.GetChild(1).GetChild(0).GetComponent<Text>()
						.enabled = false;
				}
				yield return new WaitForSeconds(0.2f);
				anim.SetBool("Title", true);
			}
			else
			{
				StartCoroutine("BackgroundColor", "SkippedTitle");
			}
			if (Input.mousePresent)
			{
				Screen.lockCursor = false;
				UnityEngine.Cursor.visible = true;
			}
		}
		else
		{
			current = "Playing";
			anim.SetBool("Fade", false);
			if (Input.mousePresent)
			{
				Screen.lockCursor = true;
				UnityEngine.Cursor.visible = false;
			}
		}
		if (offlineNotifications && network == 0 && !waitBackground)
		{
			StartCoroutine("ReceiveInvitation");
		}
		if (gameState == "Multiplayer")
		{
			bt[0].text = "Resume";
			bt[1].text = "Singleplayer";
		}
		else if (gameState == "Singleplayer")
		{
			bt[0].text = "Multiplayer";
			bt[1].text = "Resume";
		}
		else
		{
			bt[0].text = "Multiplayer";
			bt[1].text = "Singleplayer";
		}
		bt[2].text = "Character";
		bt[3].text = "Settings";
		bt[4].text = "Leaderboard";
		bt[5].text = "Information";
		buttons[0].sprite = images[0];
		buttons[1].sprite = images[1];
		buttons[2].sprite = images[2];
		buttons[3].sprite = images[3];
		buttons[4].sprite = images[4];
		buttons[5].sprite = images[5];
		canOpen = true;
		skipTitle = false;
		for (int i = 0; i < 5; i++)
		{
			roomTexts[i] = mt.GetChild(9).GetChild(0).GetChild(i)
				.GetComponent<Text>();
		}
		ruleTitleText = new Dictionary<int, string>();
		ruleTitleText[0] = "Any";
		ruleTitleText[1] = "Deathmatch";
		ruleTitleText[2] = "Team Deathmatch";
		ruleTitleText[3] = "Territory";
		ruleTitleText[4] = "Capture the Flag";
		ruleTitleText[5] = "Blow up the Base";
		ruleTitleText[6] = "Zombie";
		ruleTitleText[7] = "VIP";
		ruleTitleText[8] = "Co-op Survival";
		ruleExpText = new Dictionary<int, string>();
		ruleExpText[0] = "Searching...";
		ruleExpText[1] = "Kill other players.";
		ruleExpText[2] = "Kill the other team's players.";
		ruleExpText[3] = "Dominate the area.";
		ruleExpText[4] = "Capture the other team's flag\nand bring it back to my team's base.";
		ruleExpText[5] = "Blow up the other team's base.\nSeparated Offense and Defense.\n\nOffense: Set a bomb on the base.\nDefense: Defend my team's base.";
		ruleExpText[6] = "A zombie makes another zombie.";
		ruleExpText[7] = "Defend your team's VIP and kill the other team's VIP.";
		ruleExpText[8] = "Survive with your co-players.\nYour record is saved as the record of\nsingleplayer mode.\n\nNote: Started from phase 1\nand current score will be reset.";
		objectiveText = new Dictionary<string, string>();
		objectiveText["0-0"] = "Any";
		objectiveText["0-1"] = "Any";
		objectiveText["0-2"] = "Any";
		objectiveText["0-3"] = "Any";
		objectiveText["1-0"] = "Any";
		objectiveText["1-1"] = "3 Kills";
		objectiveText["1-2"] = "5 Kills";
		objectiveText["1-3"] = "10 Kills";
		objectiveText["2-0"] = "Any";
		objectiveText["2-1"] = "5 Kills";
		objectiveText["2-2"] = "10 Kills";
		objectiveText["2-3"] = "15 Kills";
		objectiveText["3-0"] = "Any";
		objectiveText["3-1"] = "30 Seconds";
		objectiveText["3-2"] = "60 Seconds";
		objectiveText["3-3"] = "90 Seconds";
		objectiveText["4-0"] = "Any";
		objectiveText["4-1"] = "1 Flag";
		objectiveText["4-2"] = "2 Flags";
		objectiveText["4-3"] = "3 Flags";
		objectiveText["5-0"] = "Any";
		objectiveText["5-1"] = "1 Round";
		objectiveText["5-2"] = "2 Rounds";
		objectiveText["5-3"] = "3 Rounds";
		objectiveText["6-0"] = "Any";
		objectiveText["6-1"] = "1 Round";
		objectiveText["6-2"] = "2 Rounds";
		objectiveText["6-3"] = "3 Rounds";
		objectiveText["7-0"] = "Any";
		objectiveText["7-1"] = "1 Round";
		objectiveText["7-2"] = "2 Rounds";
		objectiveText["7-3"] = "3 Rounds";
		objectiveText["8-0"] = "--";
		objectiveText["8-1"] = "--";
		objectiveText["8-2"] = "--";
		objectiveText["8-3"] = "--";
		stageName = new Dictionary<int, string>();
		stageName[0] = "Flat City";
		stageName[1] = "Urban Park";
		stageName[2] = "Beachside Town";
		stageName[3] = "Department Store";
		stageName[4] = "Warehouse";
		stageName[5] = "Night Land";
		stageText = new Dictionary<string, int>();
		stageText[stageName[0]] = 0;
		stageText[stageName[1]] = 1;
		stageText[stageName[2]] = 2;
		stageText[stageName[3]] = 3;
		stageText[stageName[4]] = 4;
		stageText[stageName[5]] = 5;
		if (PhotonNetwork.inRoom && gameState != "Multiplayer")
		{
			rule = (int)PhotonNetwork.room.CustomProperties["R"];
			objective = (int)PhotonNetwork.room.CustomProperties["O"];
			playerCount = PhotonNetwork.room.MaxPlayers;
			roomTexts[0].text = ruleTitleText[rule];
			roomTexts[1].text = ruleExpText[rule];
			roomTexts[2].text = "Objective: " + objectiveText[rule + "-" + objective];
			roomTexts[3].text = "Player Count: " + playerCount;
			roomTexts[4].text = "Matchmaking... Wait or press Start Now.";
			PhotonPlayer[] playerList = PhotonNetwork.playerList;
			foreach (PhotonPlayer photonPlayer in playerList)
			{
				byte[] data = (byte[])photonPlayer.CustomProperties["I"];
				Texture2D texture2D = new Texture2D(128, 128);
				texture2D.LoadImage(data);
				GameObject gameObject = (GameObject)UnityEngine.Object.Instantiate(playerButton);
				if (rule == 1 || rule == 6 || rule == 8)
				{
					int index = (int)photonPlayer.CustomProperties["TC"];
					gameObject.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(index)
						.GetComponent<Image>()
						.color;
				}
				else if (photonPlayer.GetTeam() == PunTeams.Team.red)
				{
					gameObject.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(9)
						.GetComponent<Image>()
						.color;
				}
				else
				{
					gameObject.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(7)
						.GetComponent<Image>()
						.color;
				}
				gameObject.transform.GetChild(0).GetComponent<Image>().sprite = Sprite.Create(texture2D, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f));
				gameObject.transform.GetChild(1).GetComponent<Text>().text = photonPlayer.NickName;
				DetailInformation component = gameObject.GetComponent<DetailInformation>();
				component.canvas = base.transform;
				component.backgroundColor = gameObject.GetComponent<Image>().color;
				component.comment = (string)photonPlayer.CustomProperties["C"];
				component.kill = (int)photonPlayer.CustomProperties["K"];
				component.death = (int)photonPlayer.CustomProperties["D"];
				component.id = photonPlayer.ID;
				gameObject.transform.SetParent(multiplayerList, false);
				if (photonPlayer.GetTeam() == PunTeams.Team.red)
				{
					gameObject.transform.SetAsFirstSibling();
				}
				if (photonPlayer == PhotonNetwork.player && myButton == null)
				{
					myButton = gameObject;
				}
			}
		}
		else
		{
			waitBackground = false;
		}
		if (waitBackground)
		{
			stayRoom.isOn = true;
			stayRoom.interactable = true;
			startNow.interactable = true;
			int num = playerCount;
			if (num % 2 == 1)
			{
				num++;
			}
			if (num <= 2)
			{
				num = 4;
			}
			if (playerCount <= 2 || rule == 1 || rule == 6 || rule == 8)
			{
				startNow.transform.GetChild(0).GetComponent<Text>().text = "Start Now! " + startNowPlayer + "/" + num / 2;
			}
			else
			{
				startNow.transform.GetChild(0).GetComponent<Text>().text = "Add bot and Start Now! " + startNowPlayer + "/" + num / 2;
			}
		}
		if (!VRmode && GetComponent<FlatsDesktopSettings>() == null)
			gameObject.AddComponent<FlatsDesktopSettings>().Initialize(mt.GetChild(6).GetChild(3));
		if (gameState == "Main")
		{
			yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(5f));
			// Consume once per process, after saves, settings and UI have initialized.
			// Returning from Tutorial must remain at the normal main menu.
			if (!tutorialLaunchConsumed && Array.IndexOf(Environment.GetCommandLineArgs(), "-flats-tutorial") >= 0)
			{
				tutorialLaunchConsumed = true;
				network = 0;
				Singleplayer.rule = 4;
				gameState = "Singleplayer";
				LoadOfflineScene("Tutorial");
				yield break;
			}
			ambient.GetComponent<AudioSource>().Play();
			if (current == "Main")
			{
				quitButton.SetActive(true);
			}
			yield break;
		}
		while (!Camera.main)
		{
			yield return new WaitForSeconds(0f);
		}
		changedSettings = true;
		yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(1f));
		mt.parent.GetChild(1).GetComponent<Canvas>().enabled = true;
		Debug.Log("Player Spawn!");
		StartCoroutine("BackgroundColor", "FadeOut");
	

    }

	private void Update()
	{
		InputDevice activeDevice = InputManager.ActiveDevice;
		if (current != "Modules" && !fliping && !backWithCancel && (Input.GetKeyUp(KeyCode.Escape) || activeDevice.CommandWasPressed || (current != "Main" && current != "Playing" && !TouchScreenKeyboard.visible && !Keyboard.isOpen && ((!customControlEnabled && activeDevice.Action2.WasPressed) || (customControlEnabled && Input.GetButtonDown(customControl["Pick"]))))) && canOpen && !confirm.activeSelf && (current == "Playing" || backButton.activeSelf || current == "Main"))
		{
			Fade(-1);
		}
		if (!canOpen)
		{
			anim.enabled = false;
		}
		else
		{
			anim.enabled = true;
		}
		if (customControlEnabled && !resetCustomizing && Input.GetButton(customControl["Jump"]) && Input.GetButton(customControl["Pick"]) && Input.GetButton(customControl["Reload"]) && Input.GetButton(customControl["Change"]))
		{
			resetTime += Time.unscaledDeltaTime;
			if (resetTime > 5f)
			{
				resetCustomizing = true;
				resetTime = 0f;
				FlatsPreferences.DeleteKey("controllermapping");
				FlatsPreferences.Save();
				customControlEnabled = false;
				standaloneModule.enabled = false;
				inControlModule.enabled = true;
				standaloneModule.submitButton = "Submit";
				standaloneModule.cancelButton = "Cancel";
				ShowConfirm("Disabled custom mapping", "Your custom controller mapping was disabled.", ResetCustomMapping, "OK", null);
				Selectable component = confirm.transform.GetChild(3).GetComponent<Selectable>();
				component.Select();
			}
		}
		if (mySettings != null && (mySettings.graphics_aa != 0 || mySettings.graphics_dof != 0 || mySettings.graphics_motionBlur != 0 || mySettings.graphics_saturationFilter != 0 || mySettings.graphics_edgeRendering != 0))
		{
			deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
			float num = 1f / deltaTime;
			if (num < framerateLimit && current != "Settings")
			{
				framerateAlert++;
			}
			else
			{
				framerateAlert = 0;
			}
			if (framerateAlert >= 30)
			{
				if (mySettings.graphics_aa != 0)
				{
					mySettings.graphics_aa = 0;
					FPSController.aa = IntToBool(mySettings.graphics_aa);
					mt.GetChild(6).GetChild(1).GetChild(0)
						.GetChild(1)
						.GetComponent<Text>()
						.text = aaText[mySettings.graphics_aa];
				}
				if (mySettings.graphics_dof != 0)
				{
					mySettings.graphics_dof = 0;
					FPSController.dof = IntToBool(mySettings.graphics_dof);
					mt.GetChild(6).GetChild(1).GetChild(1)
						.GetChild(1)
						.GetComponent<Text>()
						.text = dofText[mySettings.graphics_dof];
				}
				if (mySettings.graphics_motionBlur != 0)
				{
					mySettings.graphics_motionBlur = 0;
					FPSController.motionBlur = IntToBool(mySettings.graphics_motionBlur);
					mt.GetChild(6).GetChild(1).GetChild(2)
						.GetChild(1)
						.GetComponent<Text>()
						.text = motionBlurText[mySettings.graphics_motionBlur];
				}
				if (mySettings.graphics_edgeRendering != 0)
				{
					mySettings.graphics_edgeRendering = 0;
					FPSController.edgeRendering = IntToBool(mySettings.graphics_edgeRendering);
					mt.GetChild(6).GetChild(1).GetChild(3)
						.GetChild(1)
						.GetComponent<Text>()
						.text = edgeRenderingText[mySettings.graphics_edgeRendering];
				}
				if (mySettings.graphics_saturationFilter != 0)
				{
					mySettings.graphics_saturationFilter = 0;
					FPSController.saturationFilter = IntToBool(mySettings.graphics_saturationFilter);
					mt.GetChild(6).GetChild(1).GetChild(4)
						.GetChild(1)
						.GetComponent<Text>()
						.text = saturationFilterText[mySettings.graphics_saturationFilter];
				}
				SaveDataController.Save();
				changedSettings = true;
				ShowConfirm("Extremely low framerate!", "All graphics settings have been disabled.", FramerateAlertIsChecked, "OK", null);
				framerateAlertIsEnabled = true;
			}
		}
		if (notification.gameObject.activeSelf && ((!customControlEnabled && activeDevice.Action3.IsPressed) || (customControlEnabled && Input.GetButton(customControl["Reload"]))))
		{
			accepting += Time.unscaledDeltaTime;
			if (accepting > 1f && canOpen)
			{
				Debug.Log("Accepted invitation from controller button.");
				StartCoroutine("AcceptInvitation");
				mt.parent.GetChild(5).GetComponent<Animator>().Play("Invitation_Off");
				accepting = 0f;
			}
		}
		else
		{
			accepting = 0f;
		}
		if (current == "Map" && Input.GetJoystickNames().Length > 0)
		{
			Image[] array = buttons;
			foreach (Image image in array)
			{
				RectTransform component2 = image.transform.parent.GetComponent<RectTransform>();
				if (EventSystem.current.currentSelectedGameObject == image.transform.parent.gameObject)
				{
					component2.sizeDelta = new Vector2(210f, 130f);
				}
				else
				{
					component2.sizeDelta = new Vector2(200f, 120f);
				}
			}
		}
		if (current == "Playing" && canOpen && anim.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.None"))
		{
			anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
		}
		else
		{
			anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
		}
	}

	private void FramerateAlertIsChecked(bool result)
	{
		framerateAlertIsEnabled = false;
	}

	private void ResetCustomMapping(bool result)
	{
		resetCustomizing = false;
	}

	private void LateUpdate()
	{
        CheckMultiplayerDeadline();
		if ((current == "Playing" && !framerateAlertIsEnabled) || VRmode || standaloneModule == null || inControlModule == null)
		{
			return;
		}
		if (current != "Playing" && (EventSystem.current.currentSelectedGameObject == null || !EventSystem.current.currentSelectedGameObject.activeInHierarchy))
		{
			if (Input.GetJoystickNames().Length > 0)
			{
				if (customControlEnabled)
				{
					standaloneModule.enabled = true;
					inControlModule.enabled = false;
					standaloneModule.submitButton = customControl["Jump"];
					standaloneModule.cancelButton = customControl["Pick"];
				}
				else
				{
					standaloneModule.enabled = false;
					inControlModule.enabled = true;
				}
				if (errorMessage.activeSelf)
				{
					Selectable component = errorMessage.transform.GetChild(2).GetComponent<Selectable>();
					component.Select();
				}
				else if (confirm.activeSelf)
				{
					Selectable selectable = ((!confirm.transform.GetChild(5).gameObject.activeSelf) ? confirm.transform.GetChild(3).GetComponent<Selectable>() : confirm.transform.GetChild(5).GetComponent<Selectable>());
					selectable.Select();
				}
				else if (roomCreation.activeSelf)
				{
					Selectable component2 = roomCreation.transform.GetChild(3).GetComponent<Selectable>();
					component2.Select();
				}
				else if (current == "Main" || (current == "Map" && !voted))
				{
					EventSystem.current.SetSelectedGameObject(buttons[0].transform.parent.gameObject);
				}
				else if (backButton.activeSelf)
				{
					EventSystem.current.SetSelectedGameObject(backButton);
				}
			}
			else if ((bool)standaloneModule)
			{
				standaloneModule.enabled = true;
				inControlModule.enabled = false;
			}
		}
		else if (framerateAlertIsEnabled)
		{
			Selectable selectable2 = ((!confirm.transform.GetChild(5).gameObject.activeSelf) ? confirm.transform.GetChild(3).GetComponent<Selectable>() : confirm.transform.GetChild(5).GetComponent<Selectable>());
			selectable2.Select();
		}
		if (update.activeSelf)
		{
			EventSystem.current.firstSelectedGameObject = update.transform.GetChild(5).gameObject;
			EventSystem.current.SetSelectedGameObject(update.transform.GetChild(5).gameObject);
			EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>().Select();
		}
	}

	private void BackToMainMenu()
	{
		if (gameState == "Multiplayer")
		{
			bt[0].text = "Resume";
			bt[1].text = "Singleplayer";
		}
		else if (gameState == "Singleplayer")
		{
			bt[0].text = "Multiplayer";
			bt[1].text = "Resume";
		}
		else
		{
			bt[0].text = "Multiplayer";
			bt[1].text = "Singleplayer";
		}
		if (waitBackground)
		{
			bt[0].text = "Matchmaking...";
		}
		bt[2].text = "Character";
		bt[3].text = "Settings";
		bt[4].text = "Leaderboard";
		bt[5].text = "Information";
		buttons[0].sprite = images[0];
		buttons[1].sprite = images[1];
		buttons[2].sprite = images[2];
		buttons[3].sprite = images[3];
		buttons[4].sprite = images[4];
		buttons[5].sprite = images[5];
		anim.SetBool("Fade", false);
		current = "Main";
	}

	public void OpenMenu()
	{
		anim.SetTrigger("OpenMenu");
		current = "Main";
		AudioListener.volume /= 2f;
		StartCoroutine("BackgroundColor", "OpenMenu");
		mt.parent.GetChild(1).GetComponent<Canvas>().enabled = false;
		mt.parent.GetChild(2).GetComponent<Canvas>().enabled = false;
		if (!VRmode)
		{
			FPSController.enableCamRotate = false;
		}
		if (gameState != "Multiplayer")
		{
			if (Singleplayer.rule == 1 && Singleplayer.currentAssortmentRule == 4)
			{
				GameObject grabbedObject = GameObject.Find("BlueTeamBase").GetComponent<TeamBase>().grabbedObject;
				if (grabbedObject != null && grabbedObject != null)
				{
					grabbedObject.transform.GetChild(1).gameObject.SetActive(false);
					grabbedObject.transform.GetChild(2).gameObject.SetActive(false);
				}
			}
			savedTimeScale = Time.timeScale;
			Time.timeScale = 0f;
		}
		if (!Input.mousePresent && Input.GetJoystickNames().Length == 0)
		{
			EasyTouch.SetEnabled(false);
			ETCInput.SetControlActivated("Joystick", false);
			ETCInput.ResetAxis("Horizontal");
			ETCInput.ResetAxis("Vertical");
		}
		Selectable component = buttons[0].transform.parent.GetComponent<Selectable>();
		if (Input.GetJoystickNames().Length > 0)
		{
			component.Select();
		}
		if (Input.mousePresent)
		{
			Screen.lockCursor = false;
			UnityEngine.Cursor.visible = true;
		}
		if (!VRmode)
		{
			return;
		}
		if (Camera.main.gameObject != null)
		{
			mt.position = Camera.main.transform.position + Camera.main.transform.forward * 2.1f;
			mt.eulerAngles = new Vector3(Camera.main.transform.eulerAngles.x, Camera.main.transform.eulerAngles.y, 0f);
		}
		if ((bool)Camera.main.transform.GetChild(0).GetComponent<Camera>())
		{
			GameObject gameObject = Camera.main.transform.root.gameObject;
			if (gameObject.tag != "Player")
			{
				gameObject = Camera.main.transform.Find("Player").gameObject;
			}
			gameObject.GetComponent<IKController>().enabled = false;
			Camera.main.transform.GetChild(0).gameObject.SetActive(false);
		}
	}

	public void CloseMenu()
	{
		anim.SetTrigger("CloseMenu");
		anim.SetBool("Fade", false);
		current = "Playing";
		AudioListener.volume *= 2f;
		StartCoroutine("BackgroundColor", "CloseMenu");
		Time.timeScale = savedTimeScale;
		FPSController.enableCamRotate = true;
		EventSystem.current.SetSelectedGameObject(null);
		mt.parent.GetChild(1).GetComponent<Canvas>().enabled = true;
		mt.parent.GetChild(2).GetComponent<Canvas>().enabled = true;
		if (Singleplayer.rule == 1 && Singleplayer.currentAssortmentRule == 4)
		{
			GameObject grabbedObject = GameObject.Find("BlueTeamBase").GetComponent<TeamBase>().grabbedObject;
			if (grabbedObject != null)
			{
				grabbedObject.transform.GetChild(1).gameObject.SetActive(true);
				grabbedObject.transform.GetChild(2).gameObject.SetActive(true);
			}
		}
		if (Input.mousePresent)
		{
			Screen.lockCursor = true;
			UnityEngine.Cursor.visible = false;
		}
		if (!Input.mousePresent && Input.GetJoystickNames().Length == 0)
		{
			EasyTouch.SetEnabled(true);
			ETCInput.SetControlActivated("Joystick", true);
		}
		ETCInput.ResetAxis("Vertical");
		ETCInput.ResetAxis("Horizontal");
		if (VRmode)
		{
			GameObject gameObject = Camera.main.transform.root.gameObject;
			if (gameObject.tag != "Player")
			{
				gameObject = Camera.main.transform.Find("Player").gameObject;
			}
			gameObject.GetComponent<IKController>().enabled = true;
			Camera.main.transform.GetChild(0).gameObject.SetActive(true);
			Camera.main.BroadcastMessage("UpdateStereoValues", SendMessageOptions.DontRequireReceiver);
		}
	}

	public void PlayMenuSound(AudioClip clip)
	{
		if (clip != null) GetComponent<AudioSource>().PlayOneShot(clip, 3f);
	}

	public void Fade(int button)
	{
        if (HandleModNavigation(button)) return;
        if (button == -1 && current == "Multiplayer" && anim.GetBool("RoomCreation"))
        {
            SetRoomCreationVisible(false); anim.SetBool("Detail", true);
            anim.Play("Detail Fade In", 0, 0f);
            if (currentDetail != null) currentDetail.SetActive(true);
            backButton.SetActive(true); fliping = false; return;
        }
        if (button == -1 && multiplayerConnecting)
        { ++multiplayerOperation; multiplayerFailure = "Connection cancelled"; PhotonNetwork.Disconnect(); }
        Debug.Log("FLATS_MENU_ACTION current=" + current + " button=" + button);
        if (current == "Main" && button == 90 && Array.IndexOf(Environment.GetCommandLineArgs(), "-flats-bot-sandbox") >= 0)
        { rule = 1; objective = 1; botCount = 3; current = "OfflineMatch"; backButton.SetActive(true); RefreshOfflineMatch(); return; }
        if (current == "OfflineMatch")
        {
            if (!fliping) StartCoroutine(OfflineMatchMenu(button));
            return;
        }

		if (button <= 5 && button >= -1 && current != "Playing" && (current == "Main" || current == "Multiplayer" || current == "Singleplayer" || current == "Character" || current == "Settings") && current != "Map" && (!(current == "Singleplayer") || button != 5) && (!(gameState == "Main") || !(current == "Main") || button != -1))
		{
			anim.SetBool("Fade", true);
		}
		if (button != -1 || current != "Playing" || canOpen)
		{
			StartCoroutine("MenuController", button);
		}
		if (button == -1)
		{
			PlayMenuSound(cancelSE);
		}
		else
		{
			PlayMenuSound(pressSE);
		}
	}

    private static void DetachSceneTargets()
    {
        foreach (var camera in Resources.FindObjectsOfTypeAll<Camera>())
            if (camera != null && camera.gameObject.scene.IsValid()) camera.targetTexture = null;
        RenderTexture.active = null;
    }
    private static void LoadOfflineScene(string scene) { DetachSceneTargets(); UnityEngine.SceneManagement.SceneManager.LoadScene(scene); }
    private static void LoadOfflineScene(int scene) { DetachSceneTargets(); UnityEngine.SceneManagement.SceneManager.LoadScene(scene); }
    private int offlineMap;
    private bool startingOfflineMatch;
    private static readonly string[] OfflineMaps = { "FlatCity", "UrbanPark", "BeachsideTown", "DepartmentStore", "Warehouse", "NightLand" };

    private void RefreshOfflineMatch()
    {
        bt[0].text = ruleTitleText[rule];
        bt[1].text = "Objective: " + objectiveText[rule + "-" + objective];
        bt[2].text = "Bots: " + botCount;
        bt[3].text = stageName[offlineMap];
        bt[4].text = "Start Offline Match";
        bt[5].text = "Back";
        buttons[0].sprite = images[36]; buttons[1].sprite = images[19];
        buttons[2].sprite = images[38]; buttons[3].sprite = images[12 + offlineMap];
        buttons[4].sprite = images[18]; buttons[5].sprite = images[41];
    }

    private IEnumerator OfflineMatchMenu(int button)
    {
        if (button == -1 || button == 5)
        {
            current = "Main"; BackToMainMenu(); backButton.SetActive(gameState != "Main");
            yield break;
        }
        if (button == 0) rule = rule % 8 + 1;
        if (button == 1) objective = objective % 3 + 1;
        if (button == 2) botCount = botCount >= 9 ? 1 : botCount + 2;
        if (button == 3) offlineMap = (offlineMap + 1) % OfflineMaps.Length;
        RefreshOfflineMatch();
        if (button != 4) yield break;
        fliping = true;
        startingOfflineMatch = true;
        if (PhotonNetwork.inRoom) PhotonNetwork.LeaveRoom();
        if (!PhotonNetwork.offlineMode) { PhotonNetwork.Disconnect(); PhotonNetwork.offlineMode = true; }
        playerCount = 1; // Bots do not send human-player synchronization receipts.
        PhotonNetwork.player.NickName = myCharacter.name;
        PhotonNetwork.SetPlayerCustomProperties(new ExitGames.Client.Photon.Hashtable {
            { "K", 0 }, { "D", 0 }, { "TC", myCharacter.color }, { "C", myCharacter.comment },
            { "I", System.IO.File.ReadAllBytes((FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath) + "/Flats_UserIcon.png") }
        });
        PhotonNetwork.CreateRoom("Flats Local Bots", new RoomOptions {
            MaxPlayers = 1, IsVisible = false,
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable { { "R", rule }, { "O", objective } }
        }, null);
        if (!PhotonNetwork.inRoom)
        {
            startingOfflineMatch = false; fliping = false;
            ShowConfirm("Local match", "The offline room could not be created. Please try again.", null, "OK", null);
            yield break;
        }
        PhotonNetwork.player.SetTeam(rule == 1 || rule == 6 ? PunTeams.Team.none : PunTeams.Team.red);
        network = 2; waitBackground = false; gameState = "Multiplayer";
        current = "Playing"; Time.timeScale = 1f;
        backButton.SetActive(false);
        Debug.Log("FLATS_LOCAL_MATCH rule=" + rule + " bots=" + botCount + " map=" + OfflineMaps[offlineMap]);
        LoadOfflineScene(OfflineMaps[offlineMap]);
    }

	public IEnumerator MenuController(int button)
	{
        if (current == "Multiplayer" && (button == 3 || button == 10 || button == 11 || button == 15))
        {
            if (button == 11 && string.IsNullOrWhiteSpace(invitationRoomName.text))
            { ShowConfirm("Invitation Match", "Enter a room name.", null, "OK", null); yield break; }
            yield return StartCoroutine(EnsureMultiplayerConnection());
            if (!multiplayerReady) { fliping = false; anim.SetBool("Fade", false); yield break; }
            pendingRoomDeadline = Time.realtimeSinceStartup + 25f;
        }

		if (current != "Playing")
		{
			fliping = true;
		}
		if (current == "Playing")
		{
			if (button == -1)
			{
				OpenMenu();
			}
		}
		else if (current == "Main")
		{
			switch (button)
			{
			case -1:
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
				if (gameState != "Main")
				{
					CloseMenu();
				}
				else
				{
					ShowConfirm("Quit Application", "Quit Flats.", Quit, "OK", "Cancel");
				}
				break;
			case 0:
			{
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
				if (gameState == "Multiplayer")
				{
					CloseMenu();
					break;
				}
				if (waitBackground)
				{
					anim.SetBool("Matching", true);
					backButton.SetActive(true);
					anim.SetTrigger("SkipToMatching");
					current = "Matching";
					break;
				}
				bt[0].text = "Open Match";
				bt[1].text = "Invitation Match";
				bt[2].text = "Local Match";
				bt[3].text = "Chat Room";
				bt[4].text = "Server Region";
				bt[5].text = "Online Version: " + version.Substring(0, 3);
				buttons[0].sprite = images[36];
				buttons[1].sprite = images[37];
				buttons[2].sprite = images[38];
				buttons[3].sprite = images[39];
				buttons[4].sprite = images[40];
				buttons[5].sprite = images[41];
				backButton.SetActive(true);
				EventSystem.current.SetSelectedGameObject(null);
				anim.SetBool("Fade", false);
				current = "Multiplayer";
				Texture2D texture2D = new Texture2D(128, 128);
				texture2D.filterMode = FilterMode.Bilinear;
				byte[] array = System.IO.File.ReadAllBytes((FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath) + "/Flats_UserIcon.png");
				texture2D.LoadImage(array);
				ExitGames.Client.Photon.Hashtable hashtable = new ExitGames.Client.Photon.Hashtable();
				hashtable["K"] = myCharacter.kill;
				hashtable["D"] = myCharacter.death;
				hashtable["TC"] = myCharacter.color;
				hashtable["C"] = myCharacter.comment;
				hashtable["I"] = array;
                PublishRoomModules(hashtable);
				PhotonNetwork.SetPlayerCustomProperties(hashtable);
				PhotonNetwork.player.NickName = myCharacter.name;
				break;
			}
			case 1:
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
				if (gameState == "Singleplayer")
				{
					CloseMenu();
					break;
				}
				bt[0].text = "Survival";
				bt[1].text = "Assortment";
				bt[2].text = "Headshot Challenge";
				bt[3].text = "Training";
				bt[4].text = "Tutorial";
				bt[5].text = "Stage Select";
				buttons[0].sprite = images[18];
				buttons[1].sprite = images[19];
				buttons[2].sprite = images[20];
				buttons[3].sprite = images[21];
				buttons[4].sprite = images[22];
				buttons[5].sprite = images[23];
				backButton.SetActive(true);
				EventSystem.current.SetSelectedGameObject(null);
				anim.SetBool("Fade", false);
				current = "Singleplayer";
				break;
			case 2:
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
				bt[0].text = "Profile";
				bt[1].text = "Color";
				bt[2].text = "Score";
				bt[3].text = "Weapons";
				bt[4].text = "Stats";
				bt[5].text = "Sync";
				buttons[0].sprite = images[24];
				buttons[1].sprite = images[25];
				buttons[2].sprite = images[26];
				buttons[3].sprite = images[27];
				buttons[4].sprite = images[28];
				buttons[5].sprite = images[29];
				backButton.SetActive(true);
				EventSystem.current.SetSelectedGameObject(null);
				anim.SetBool("Fade", false);
				current = "Character";
				break;
			case 3:
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
				bt[0].text = "Sound";
				bt[1].text = "Graphics";
				bt[2].text = "Control";
				bt[3].text = VRmode ? "VR Image" : "Display";
				bt[4].text = "Button Mapping";
				bt[5].text = "Extra Settings";
				buttons[0].sprite = images[30];
				buttons[1].sprite = images[31];
				buttons[2].sprite = images[32];
				buttons[3].sprite = images[33];
				buttons[4].sprite = images[34];
				buttons[5].sprite = images[35];
				backButton.SetActive(true);
				EventSystem.current.SetSelectedGameObject(null);
				anim.SetBool("Fade", false);
				current = "Settings";
				break;
			case 4:
				leaderboardLoading.SetActive(true);
				StartCoroutine("Leaderboard", false);
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
				currentDetail = mt.GetChild(7).GetChild(0).gameObject;
				currentDetail.SetActive(true);
				backButton.SetActive(true);
				anim.SetBool("Detail", true);
				current = "Leaderboard";
				break;
			case 5:
				StartCoroutine("Information");
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
				currentDetail = mt.GetChild(8).GetChild(0).gameObject;
				currentDetail.SetActive(true);
				backButton.SetActive(true);
				anim.SetBool("Detail", true);
				current = "Information";
				break;
			case -2:
				if (VRController.device == "cardboard")
				{
					ShowConfirm("Cardboard mode", "Use gamepads to play VR mode.", EnableVR, "Enable", "Disable");
				}
				break;
			case -3:
				ShowConfirm("Reset", "Exit from current game and reboot.", Reset, "OK", "Cancel");
				break;
			case -4:
				ShowConfirm("Quit Application", "Quit Flats.", Quit, "OK", "Cancel");
				break;
			}
			if (button >= 0)
			{
				page = button + 3;
			}
		}
		else if (current == "Multiplayer" || current == "Singleplayer" || current == "Character" || current == "Settings" || current == "Leaderboard" || current == "Information")
		{
			switch (button)
			{
			case -1:
				if (currentDetail == null && current != "Leaderboard" && current != "Information")
				{
					backButton.SetActive(false);
					yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
					EventSystem.current.SetSelectedGameObject(null);
					bool returnToPlay = gameState == "Main" && (current == "Singleplayer" || current == "Multiplayer");
					BackToMainMenu();
                    if (returnToPlay) { current="Play"; backButton.SetActive(true); RefreshPlayTiles(); EventSystem.current.SetSelectedGameObject(buttons[0].transform.parent.gameObject); }
					break;
				}
				if (current == "Multiplayer")
				{
					if (currentDetail.name == "ChatRoom")
					{
						if (PhotonNetwork.inRoom)
						{
							base.gameObject.GetPhotonView().RPC("Chat", PhotonTargets.MasterClient, PhotonNetwork.player.NickName + " left chat.");
						}
						foreach (Transform item in currentDetail.transform.GetChild(4))
						{
							UnityEngine.Object.Destroy(item.gameObject);
						}
						currentDetail.transform.GetChild(2).GetComponent<InputField>().text = "";
						currentDetail.transform.GetChild(3).GetComponent<Button>().interactable = false;
						currentDetail.transform.GetChild(5).gameObject.SetActive(true);
						PhotonNetwork.LeaveRoom();
					}
				}
				else if (current == "Character")
				{
					SaveDataController.Save();
					if (FlatsLocalProfile.LastSaveSucceeded) MonoBehaviour.print("Character data has been saved.");
				}
				else if (current == "Settings")
				{
					if (currentDetail.name == "ButtonMapping" && Input.GetJoystickNames().Length == 0)
					{
						Vector2[] array2 = new Vector2[4];
						float[] array3 = new float[4];
						for (int i = 1; i < 5; i++)
						{
							array2[i - 1] = new Vector2(Mathf.Abs(currentDetail.transform.GetChild(1).GetChild(1).GetChild(i)
								.rectTransform()
								.anchoredPosition.x) - mt.parent.GetChild(1).GetChild(i).rectTransform()
								.sizeDelta.x / 2f, currentDetail.transform.GetChild(1).GetChild(1).GetChild(i)
								.rectTransform()
								.anchoredPosition.y);
							mt.parent.GetChild(1).GetChild(i).GetComponent<ETCButton>()
								.anchorOffet = array2[i - 1];
							Vector3 localScale = currentDetail.transform.GetChild(1).GetChild(1).GetChild(i)
								.rectTransform()
								.localScale;
							array3[i - 1] = localScale.x;
							mt.parent.GetChild(1).GetChild(i).rectTransform()
								.localScale = new Vector3(localScale.x, localScale.y, localScale.z);
						}
						FlatsPreferences.SetString("touchmapping", array2[0].x.ToString("F0") + "$" + array2[0].y.ToString("F0") + "$" + array2[1].x.ToString("F0") + "$" + array2[1].y.ToString("F0") + "$" + array2[2].x.ToString("F0") + "$" + array2[2].y.ToString("F0") + "$" + array2[3].x.ToString("F0") + "$" + array2[3].y.ToString("F0") + "$" + array3[0] + "$" + array3[1] + "$" + array3[2] + "$" + array3[3]);
						FlatsPreferences.Save();
						Debug.Log("Touch mapping has been saved.");
					}
					SaveDataController.Save();
					changedSettings = true;
					if (FlatsLocalProfile.LastSaveSucceeded) MonoBehaviour.print("Settings data has been saved.");
				}
				else if (current == "Leaderboard" || current == "Information")
				{
					current = "Main";
					backButton.SetActive(false);
					EventSystem.current.SetSelectedGameObject(null);
				}
				uploadButton.SetActive(false);
				leaderboardLoading.SetActive(false);
				anim.SetBool("Detail", false);
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
				if (currentDetail != null)
				{
					currentDetail.SetActive(false);
				}
				currentDetail = null;
				anim.SetBool("Fade", false);
				break;
			case 0:
			case 1:
			case 2:
			case 3:
			case 4:
			case 5:
			{
				if (current == "Multiplayer")
				{
					if (button == 0)
					{
						rule = 0;
						objective = 0;
						playerCount = 0;
						botCount = 0;
						mt.GetChild(3).GetChild(0).GetChild(0)
							.GetChild(1)
							.GetComponent<Text>()
							.text = ruleTitleText[rule];
						mt.GetChild(3).GetChild(0).GetChild(1)
							.GetChild(1)
							.GetComponent<Text>()
							.text = objectiveText[rule + "-" + objective];
						mt.GetChild(3).GetChild(0).GetChild(2)
							.GetChild(1)
							.GetComponent<Text>()
							.text = "Any";
					}
					if (button == 1)
					{
						rule = 0;
						objective = 0;
						playerCount = 0;
						botCount = 0;
					}
					if (button == 2)
					{
						ShowConfirm("This function is not available", "Sorry, you can't use this function\non Windows devices.", null, "OK", null);
					}
					if (button == 3)
					{
						foreach (Transform item2 in chat.GetChild(4))
						{
							UnityEngine.Object.Destroy(item2.gameObject);
						}
						if (!PhotonNetwork.connected)
						{
							Connect();
						}
						if (PhotonNetwork.inRoom)
						{
							while (PhotonNetwork.inRoom)
							{
								yield return new WaitForSeconds(0f);
							}
						}
						while (!PhotonNetwork.connectedAndReady)
						{
							yield return new WaitForSeconds(0f);
						}
						ExitGames.Client.Photon.Hashtable customProps = new ExitGames.Client.Photon.Hashtable();
						customProps["R"] = -1;
						PhotonNetwork.JoinOrCreateRoom("ChatRoom", new RoomOptions
						{
							IsVisible = false,
							CustomRoomProperties = customProps,
							CustomRoomPropertiesForLobby = new string[1] { "R" }
						}, null);
					}
					int num2 = button;
				}
				if (current != "Singleplayer")
				{
					if (current == "Multiplayer" && button == 2)
					{
						anim.SetBool("Detail", false);
						yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
						backButton.SetActive(true);
						anim.SetBool("Fade", false);
					}
					else
					{
						yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
						currentDetail = mt.GetChild(page).GetChild(button).gameObject;
						currentDetail.SetActive(true);
						RefreshRegionLabel();
						backButton.SetActive(true);
						anim.SetBool("Detail", true);
					}
				}
				if (!(current == "Settings") || button != 4)
				{
					break;
				}
				if (Input.mousePresent || Input.GetJoystickNames().Length > 0)
				{
					currentDetail.GetComponent<Image>().enabled = true;
					currentDetail.transform.GetChild(0).gameObject.SetActive(true);
					break;
				}
				currentDetail.GetComponent<Image>().enabled = false;
				currentDetail.transform.GetChild(1).gameObject.SetActive(true);
				currentDetail.transform.GetChild(1).GetChild(0).GetChild(2)
					.GetComponent<Text>()
					.text = "";
				RectTransform rectTransform = currentDetail.transform.GetChild(1).GetChild(0).rectTransform();
				RectTransform rectTransform2 = currentDetail.transform.GetChild(1).GetChild(1).rectTransform();
				if (mySettings.control_handedness == 0)
				{
					rectTransform.anchoredPosition = new Vector2(0f - Mathf.Abs(rectTransform.anchoredPosition.x), rectTransform.anchoredPosition.y);
					rectTransform2.anchoredPosition = new Vector2(Mathf.Abs(rectTransform2.anchoredPosition.x), rectTransform2.anchoredPosition.y);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
						.rectTransform()
						.anchorMin = new Vector2(1f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
						.rectTransform()
						.anchorMax = new Vector2(1f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
						.rectTransform()
						.anchorMin = new Vector2(1f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
						.rectTransform()
						.anchorMax = new Vector2(1f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
						.rectTransform()
						.anchorMin = new Vector2(1f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
						.rectTransform()
						.anchorMax = new Vector2(1f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
						.rectTransform()
						.anchorMin = new Vector2(1f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
						.rectTransform()
						.anchorMax = new Vector2(1f, 0.5f);
				}
				else
				{
					rectTransform.anchoredPosition = new Vector2(Mathf.Abs(rectTransform.anchoredPosition.x), rectTransform.anchoredPosition.y);
					rectTransform2.anchoredPosition = new Vector2(0f - Mathf.Abs(rectTransform2.anchoredPosition.x), rectTransform2.anchoredPosition.y);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
						.rectTransform()
						.anchorMin = new Vector2(0f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
						.rectTransform()
						.anchorMax = new Vector2(0f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
						.rectTransform()
						.anchorMin = new Vector2(0f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
						.rectTransform()
						.anchorMax = new Vector2(0f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
						.rectTransform()
						.anchorMin = new Vector2(0f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
						.rectTransform()
						.anchorMax = new Vector2(0f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
						.rectTransform()
						.anchorMin = new Vector2(0f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
						.rectTransform()
						.anchorMax = new Vector2(0f, 0.5f);
				}
				currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
					.rectTransform()
					.anchoredPosition = mt.parent.GetChild(1).GetChild(1).rectTransform()
					.anchoredPosition;
				currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
					.rectTransform()
					.anchoredPosition = mt.parent.GetChild(1).GetChild(2).rectTransform()
					.anchoredPosition;
				currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
					.rectTransform()
					.anchoredPosition = mt.parent.GetChild(1).GetChild(3).rectTransform()
					.anchoredPosition;
				currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
					.rectTransform()
					.anchoredPosition = mt.parent.GetChild(1).GetChild(4).rectTransform()
					.anchoredPosition;
				currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
					.rectTransform()
					.localScale = mt.parent.GetChild(1).GetChild(1).rectTransform()
					.localScale;
				currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
					.rectTransform()
					.localScale = mt.parent.GetChild(1).GetChild(2).rectTransform()
					.localScale;
				currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
					.rectTransform()
					.localScale = mt.parent.GetChild(1).GetChild(3).rectTransform()
					.localScale;
				currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
					.rectTransform()
					.localScale = mt.parent.GetChild(1).GetChild(4).rectTransform()
					.localScale;
				break;
			}
			}
			if (current == "Multiplayer")
			{
				switch (button)
				{
				case 10:
				{
					stayRoom.gameObject.SetActive(true);
					ipButton.SetActive(false);
					roomTexts[0].text = ruleTitleText[rule];
					roomTexts[1].text = ruleExpText[rule];
					roomTexts[2].text = "Objective: " + objectiveText[rule + "-" + objective];
					roomTexts[3].text = "Player Count: " + playerCount;
					roomTexts[4].text = "Searching for a room...";
					myButton = (GameObject)UnityEngine.Object.Instantiate(playerButton);
					myButton.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(myCharacter.color)
						.GetComponent<Image>()
						.color;
					myButton.transform.GetChild(0).GetComponent<Image>().sprite = mt.GetChild(5).GetChild(0).GetChild(0)
						.GetChild(0)
						.GetComponent<Image>()
						.sprite;
					myButton.transform.GetChild(1).GetComponent<Text>().text = PhotonNetwork.player.NickName;
					DetailInformation di2 = myButton.GetComponent<DetailInformation>();
					di2.canvas = mt;
					di2.backgroundColor = myButton.GetComponent<Image>().color;
					di2.comment = myCharacter.comment;
					di2.kill = myCharacter.kill;
					di2.death = myCharacter.death;
					myButton.transform.SetParent(multiplayerList, false);
					anim.SetBool("Detail", false);
					yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
					if (currentDetail != null)
					{
						currentDetail.SetActive(false);
					}
					anim.SetBool("Matching", true);
					if (!PhotonNetwork.connected)
					{
						Connect();
					}
					if (PhotonNetwork.inRoom)
					{
						while (PhotonNetwork.inRoom)
						{
							yield return new WaitForSeconds(0f);
						}
					}
					while (!PhotonNetwork.connectedAndReady)
					{
						yield return new WaitForSeconds(0f);
					}
					ExitGames.Client.Photon.Hashtable customProps3 = new ExitGames.Client.Photon.Hashtable();
					if (rule != 0)
					{
						customProps3["R"] = rule;
					}
					if (objective != 0)
					{
						customProps3["O"] = objective;
					}
					PhotonNetwork.JoinRandomRoom(customProps3, (byte)playerCount);
					break;
				}
				case 11:
					stayRoom.gameObject.SetActive(true);
					ipButton.SetActive(false);
					if (!(invitationRoomName.text != ""))
					{
						break;
					}
					anim.SetBool("Detail", false);
					backButton.SetActive(false);
					yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
					if (currentDetail != null)
					{
						currentDetail.SetActive(false);
					}
					pleaseWait.SetActive(true);
					if (!PhotonNetwork.connected)
					{
						Connect();
					}
					if (PhotonNetwork.inRoom)
					{
						while (PhotonNetwork.inRoom)
						{
							yield return new WaitForSeconds(0f);
						}
					}
					while (!PhotonNetwork.connectedAndReady)
					{
						yield return new WaitForSeconds(0f);
					}
					PhotonNetwork.JoinRoom(invitationRoomName.text);
					break;
				case 15:
					if (currentDetail.name == "InvitationMatch")
					{
						roomTexts[0].text = ruleTitleText[rule];
						roomTexts[1].text = ruleExpText[rule];
						roomTexts[2].text = "Objective: " + objectiveText[rule + "-" + objective];
						roomTexts[3].text = "Player Count: " + playerCount;
						roomTexts[4].text = "Matchmaking... Wait or press Start Now.";
						myButton = (GameObject)UnityEngine.Object.Instantiate(playerButton);
						myButton.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(myCharacter.color)
							.GetComponent<Image>()
							.color;
						myButton.transform.GetChild(0).GetComponent<Image>().sprite = mt.GetChild(5).GetChild(0).GetChild(0)
							.GetChild(0)
							.GetComponent<Image>()
							.sprite;
						myButton.transform.GetChild(1).GetComponent<Text>().text = PhotonNetwork.player.NickName;
						DetailInformation di = myButton.GetComponent<DetailInformation>();
						di.canvas = mt;
						di.backgroundColor = myButton.GetComponent<Image>().color;
						di.comment = myCharacter.comment;
						di.kill = myCharacter.kill;
						di.death = myCharacter.death;
						myButton.transform.SetParent(multiplayerList, false);
						SetRoomCreationVisible(false);
						backButton.SetActive(false);
						yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
						pleaseWait.SetActive(true);
						ExitGames.Client.Photon.Hashtable customProps2 = new ExitGames.Client.Photon.Hashtable();
						customProps2["R"] = rule;
						customProps2["O"] = objective;
                        PublishRoomModules(customProps2);
						RoomOptions options = new RoomOptions
						{
							MaxPlayers = (byte)playerCount,
							CustomRoomProperties = customProps2,
							CustomRoomPropertiesForLobby = new string[2] { "R", "O" },
							IsVisible = false
						};
						string roomName = invitationRoomName.text;
						PhotonNetwork.CreateRoom(roomName, options, null);
					}
					else if (!(currentDetail.name == "LocalMatch"))
					{
					}
					break;
				case 16:
				{
					string text = PhotonNetwork.player.NickName + ":" + mt.GetChild(3).GetChild(3).GetChild(2)
						.GetComponent<InputField>()
						.text;
					base.gameObject.GetPhotonView().RPC("Chat", PhotonTargets.MasterClient, text);
					mt.GetChild(3).GetChild(3).GetChild(2)
						.GetComponent<InputField>()
						.text = "";
					break;
				}
				}
			}
			else if (current == "Singleplayer")
			{
				if (button >= 0 && button != 5)
				{
					backButton.SetActive(false);
					if (!waitBackground && PhotonNetwork.connected)
					{
						PhotonNetwork.Disconnect();
					}
					anim.SetBool("Detail", false);
					yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
					StartCoroutine("BackgroundColor", "FadeIn");
					yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(2f));
					if (button != 4)
					{
						if (stage == -1)
						{
							stage = UnityEngine.Random.Range(2, 8);
						}
						else
						{
							stage += 2;
						}
					}
				}
				else if (button != 5)
				{
					stage = -1;
				}
				switch (button)
				{
				case 0:
					Singleplayer.rule = 0;
					LoadOfflineScene(stage);
					gameState = "Singleplayer";
					break;
				case 1:
					Singleplayer.rule = 1;
					LoadOfflineScene(stage);
					gameState = "Singleplayer";
					break;
				case 2:
					Singleplayer.rule = 2;
					LoadOfflineScene(stage);
					gameState = "Singleplayer";
					break;
				case 3:
					Singleplayer.rule = 3;
					LoadOfflineScene(stage);
					gameState = "Singleplayer";
					break;
				case 4:
					Singleplayer.rule = 4;
					LoadOfflineScene("Tutorial");
					gameState = "Singleplayer";
					break;
				case 5:
					stage++;
					if (stage > 5)
					{
						stage = 0;
					}
					else if (stage < 0)
					{
						stage = 5;
					}
					buttons[5].sprite = images[stage + 12];
					bt[5].text = stageName[stage];
					break;
				}
			}
			else if (current == "Character")
			{
				switch (button)
				{
				case 10:
					StreamManager.LoadFileDialog(FolderLocations.Pictures, new string[3] { ".png", ".jpg", ".jpeg" }, imageLoadedCallback);
					break;
				case 11:
					myCharacter.color = IntParseFast(EventSystem.current.currentSelectedGameObject.name.Replace("Color", ""));
					StartCoroutine("BackgroundColor", "Change");
					if (Application.loadedLevel == 0)
					{
						Color color = mt.GetChild(5).GetChild(1).GetChild(myCharacter.color)
							.GetComponent<Image>()
							.color;
						ParticleSystem particleSystem = GameObject.Find("BackgroundParticle").GetComponent<ParticleSystem>();
						particleSystem.startColor = color;
					}
					break;
				case 12:
				{
					int primaryWeapon = myCharacter.primaryWeapon;
					Transform child = mt.GetChild(5).GetChild(3).GetChild(3)
						.GetChild(0);
					child.GetChild(0).GetComponent<Image>().sprite = mt.GetChild(5).GetChild(3).GetChild(2)
						.GetChild(primaryWeapon)
						.GetChild(0)
						.GetComponent<Image>()
						.sprite;
					child.GetChild(1).GetComponent<Text>().text = mt.GetChild(5).GetChild(3).GetChild(2)
						.GetChild(primaryWeapon)
						.GetChild(1)
						.GetComponent<Text>()
						.text;
					if (child.GetChild(1).GetComponent<Text>().text.Contains("Shotgun"))
					{
						child.GetChild(2).GetComponent<Text>().text = GunInfo.damage[primaryWeapon] + " x " + GunInfo.burstCount[primaryWeapon];
					}
					else if (child.GetChild(1).GetComponent<Text>().text.Contains("Grenade"))
					{
						child.GetChild(2).GetComponent<Text>().text = GunInfo.damage[primaryWeapon] + " + explosion";
					}
					else
					{
						child.GetChild(2).GetComponent<Text>().text = GunInfo.damage[primaryWeapon].ToString();
					}
					child.GetChild(3).GetComponent<Text>().text = GunInfo.rpm[primaryWeapon].ToString();
					child.GetChild(4).GetComponent<Text>().text = GunInfo.limitAmmo[primaryWeapon].ToString();
					child.GetChild(5).GetComponent<Text>().text = GunInfo.limitMaxAmmo[primaryWeapon].ToString();
					child.GetChild(6).GetComponent<Text>().text = GunInfo.accuracy[primaryWeapon] + "%";
					child.GetChild(7).GetComponent<Text>().text = GunInfo.reloadTime[primaryWeapon] + 1.2f + "sec";
					child.GetChild(8).GetComponent<Text>().text = GunInfo.headshotBonus[primaryWeapon] + "x";
					child.GetChild(9).GetComponent<Text>().text = sightDictionary[myCharacter.sightList[myCharacter.primaryWeapon]];
					if (myCharacter.sightList[myCharacter.primaryWeapon] == 0)
					{
						child.GetChild(13).GetChild(0).GetComponent<Text>()
							.text = "Sight: " + sightDictionary[myCharacter.sightList[myCharacter.primaryWeapon]] + "\n(reload speed bonus)";
					}
					else
					{
						child.GetChild(13).GetChild(0).GetComponent<Text>()
							.text = "Sight: " + sightDictionary[myCharacter.sightList[myCharacter.primaryWeapon]];
					}
					child.parent.gameObject.SetActive(true);
					EventSystem.current.SetSelectedGameObject(child.GetChild(11).gameObject);
					savedWeapon = primaryWeapon;
					break;
				}
				case 13:
				{
					int secondaryWeapon = myCharacter.secondaryWeapon;
					Transform child3 = mt.GetChild(5).GetChild(3).GetChild(3)
						.GetChild(0);
					child3.GetChild(0).GetComponent<Image>().sprite = mt.GetChild(5).GetChild(3).GetChild(2)
						.GetChild(secondaryWeapon)
						.GetChild(0)
						.GetComponent<Image>()
						.sprite;
					child3.GetChild(1).GetComponent<Text>().text = mt.GetChild(5).GetChild(3).GetChild(2)
						.GetChild(secondaryWeapon)
						.GetChild(1)
						.GetComponent<Text>()
						.text;
					if (child3.GetChild(1).GetComponent<Text>().text.Contains("Shotgun"))
					{
						child3.GetChild(2).GetComponent<Text>().text = GunInfo.damage[secondaryWeapon] + " x " + GunInfo.burstCount[secondaryWeapon];
					}
					else if (child3.GetChild(1).GetComponent<Text>().text.Contains("Grenade"))
					{
						child3.GetChild(2).GetComponent<Text>().text = GunInfo.damage[secondaryWeapon] + " + explosion";
					}
					else
					{
						child3.GetChild(2).GetComponent<Text>().text = GunInfo.damage[secondaryWeapon].ToString();
					}
					child3.GetChild(3).GetComponent<Text>().text = GunInfo.rpm[secondaryWeapon].ToString();
					child3.GetChild(4).GetComponent<Text>().text = GunInfo.limitAmmo[secondaryWeapon].ToString();
					child3.GetChild(5).GetComponent<Text>().text = GunInfo.limitMaxAmmo[secondaryWeapon].ToString();
					child3.GetChild(6).GetComponent<Text>().text = GunInfo.accuracy[secondaryWeapon] + "%";
					child3.GetChild(7).GetComponent<Text>().text = GunInfo.reloadTime[secondaryWeapon] + 1.2f + "sec";
					child3.GetChild(8).GetComponent<Text>().text = GunInfo.headshotBonus[secondaryWeapon] + "x";
					child3.GetChild(9).GetComponent<Text>().text = sightDictionary[myCharacter.sightList[myCharacter.secondaryWeapon]];
					if (myCharacter.sightList[myCharacter.secondaryWeapon] == 0)
					{
						child3.GetChild(13).GetChild(0).GetComponent<Text>()
							.text = "Sight: " + sightDictionary[myCharacter.sightList[myCharacter.secondaryWeapon]] + "\n(reload speed bonus)";
					}
					else
					{
						child3.GetChild(13).GetChild(0).GetComponent<Text>()
							.text = "Sight: " + sightDictionary[myCharacter.sightList[myCharacter.secondaryWeapon]];
					}
					child3.parent.gameObject.SetActive(true);
					EventSystem.current.SetSelectedGameObject(child3.GetChild(11).gameObject);
					savedWeapon = secondaryWeapon;
					break;
				}
				case 14:
				{
					int num = IntParseFast(EventSystem.current.currentSelectedGameObject.name.Replace("Weapon", ""));
					Debug.Log("picked weapon:" + num);
					Transform child2 = mt.GetChild(5).GetChild(3).GetChild(3)
						.GetChild(0);
					child2.GetChild(0).GetComponent<Image>().sprite = mt.GetChild(5).GetChild(3).GetChild(2)
						.GetChild(num)
						.GetChild(0)
						.GetComponent<Image>()
						.sprite;
					child2.GetChild(1).GetComponent<Text>().text = mt.GetChild(5).GetChild(3).GetChild(2)
						.GetChild(num)
						.GetChild(1)
						.GetComponent<Text>()
						.text;
					if (child2.GetChild(1).GetComponent<Text>().text.Contains("Shotgun"))
					{
						child2.GetChild(2).GetComponent<Text>().text = GunInfo.damage[num] + " x " + GunInfo.burstCount[num];
					}
					else if (child2.GetChild(1).GetComponent<Text>().text.Contains("Grenade"))
					{
						child2.GetChild(2).GetComponent<Text>().text = GunInfo.damage[num] + " + explosion";
					}
					else
					{
						child2.GetChild(2).GetComponent<Text>().text = GunInfo.damage[num].ToString();
					}
					child2.GetChild(3).GetComponent<Text>().text = GunInfo.rpm[num].ToString();
					child2.GetChild(4).GetComponent<Text>().text = GunInfo.limitAmmo[num].ToString();
					child2.GetChild(5).GetComponent<Text>().text = GunInfo.limitMaxAmmo[num].ToString();
					child2.GetChild(6).GetComponent<Text>().text = GunInfo.accuracy[num] + "%";
					child2.GetChild(7).GetComponent<Text>().text = GunInfo.reloadTime[num] + 2f + "sec";
					child2.GetChild(8).GetComponent<Text>().text = GunInfo.headshotBonus[num] + "x";
					child2.GetChild(9).GetComponent<Text>().text = sightDictionary[myCharacter.sightList[num]];
					if (myCharacter.sightList[num] == 0)
					{
						child2.GetChild(13).GetChild(0).GetComponent<Text>()
							.text = "Sight: " + sightDictionary[myCharacter.sightList[num]] + "\n(reload speed bonus)";
					}
					else
					{
						child2.GetChild(13).GetChild(0).GetComponent<Text>()
							.text = "Sight: " + sightDictionary[myCharacter.sightList[num]];
					}
					child2.parent.gameObject.SetActive(true);
					EventSystem.current.SetSelectedGameObject(child2.GetChild(11).gameObject);
					savedWeapon = num;
					break;
				}
				case 15:
					if (myCharacter.secondaryWeapon == savedWeapon)
					{
						myCharacter.secondaryWeapon = myCharacter.primaryWeapon;
						mt.GetChild(5).GetChild(3).GetChild(1)
							.GetChild(1)
							.GetComponent<Image>()
							.sprite = mt.GetChild(5).GetChild(3).GetChild(2)
							.GetChild(myCharacter.secondaryWeapon)
							.GetChild(0)
							.GetComponent<Image>()
							.sprite;
						mt.GetChild(5).GetChild(3).GetChild(1)
							.GetChild(2)
							.GetComponent<Text>()
							.text = mt.GetChild(5).GetChild(3).GetChild(2)
							.GetChild(myCharacter.secondaryWeapon)
							.GetChild(1)
							.GetComponent<Text>()
							.text;
					}
					myCharacter.primaryWeapon = savedWeapon;
					mt.GetChild(5).GetChild(3).GetChild(0)
						.GetChild(1)
						.GetComponent<Image>()
						.sprite = mt.GetChild(5).GetChild(3).GetChild(2)
						.GetChild(myCharacter.primaryWeapon)
						.GetChild(0)
						.GetComponent<Image>()
						.sprite;
					mt.GetChild(5).GetChild(3).GetChild(0)
						.GetChild(2)
						.GetComponent<Text>()
						.text = mt.GetChild(5).GetChild(3).GetChild(2)
						.GetChild(myCharacter.primaryWeapon)
						.GetChild(1)
						.GetComponent<Text>()
						.text;
					break;
				case 16:
					if (myCharacter.primaryWeapon == savedWeapon)
					{
						myCharacter.primaryWeapon = myCharacter.secondaryWeapon;
						mt.GetChild(5).GetChild(3).GetChild(0)
							.GetChild(1)
							.GetComponent<Image>()
							.sprite = mt.GetChild(5).GetChild(3).GetChild(2)
							.GetChild(myCharacter.primaryWeapon)
							.GetChild(0)
							.GetComponent<Image>()
							.sprite;
						mt.GetChild(5).GetChild(3).GetChild(0)
							.GetChild(2)
							.GetComponent<Text>()
							.text = mt.GetChild(5).GetChild(3).GetChild(2)
							.GetChild(myCharacter.primaryWeapon)
							.GetChild(1)
							.GetComponent<Text>()
							.text;
					}
					myCharacter.secondaryWeapon = savedWeapon;
					mt.GetChild(5).GetChild(3).GetChild(1)
						.GetChild(1)
						.GetComponent<Image>()
						.sprite = mt.GetChild(5).GetChild(3).GetChild(2)
						.GetChild(myCharacter.secondaryWeapon)
						.GetChild(0)
						.GetComponent<Image>()
						.sprite;
					mt.GetChild(5).GetChild(3).GetChild(1)
						.GetChild(2)
						.GetComponent<Text>()
						.text = mt.GetChild(5).GetChild(3).GetChild(2)
						.GetChild(myCharacter.secondaryWeapon)
						.GetChild(1)
						.GetComponent<Text>()
						.text;
					break;
				case 17:
					if (myCharacter.sightList[savedWeapon] < GunInfo.zoom[savedWeapon])
					{
						myCharacter.sightList[savedWeapon] = myCharacter.sightList[savedWeapon] + 1;
					}
					else
					{
						myCharacter.sightList[savedWeapon] = 0;
					}
					if (myCharacter.sightList[savedWeapon] == 0)
					{
						mt.GetChild(5).GetChild(3).GetChild(3)
							.GetChild(0)
							.GetChild(13)
							.GetChild(0)
							.GetComponent<Text>()
							.text = sightDictionary[myCharacter.sightList[savedWeapon]] + "\n(reload speed bonus)";
					}
					else
					{
						mt.GetChild(5).GetChild(3).GetChild(3)
							.GetChild(0)
							.GetChild(13)
							.GetChild(0)
							.GetComponent<Text>()
							.text = sightDictionary[myCharacter.sightList[savedWeapon]];
					}
					mt.GetChild(5).GetChild(3).GetChild(3)
						.GetChild(0)
						.GetChild(9)
						.GetComponent<Text>()
						.text = sightDictionary[myCharacter.sightList[savedWeapon]];
					break;
				case 18:
					EventSystem.current.SetSelectedGameObject(mt.GetChild(5).GetChild(3).GetChild(2)
						.GetChild(savedWeapon)
						.gameObject);
						mt.GetChild(5).GetChild(3).GetChild(3)
							.gameObject.SetActive(false);
						break;
					default:
						switch (button)
						{
						case 18:
						{
							if (network == 1 || network == 2 || waitBackground)
							{
								ShowConfirm("Quit multiplayer mode or matchmaking!", "You can't use this function while playing multiplayer mode.", null, "OK", null);
								break;
							}
							WWW www = new WWW(string.Concat(str3: currentDetail.transform.GetChild(2).GetComponent<InputField>().text, str0: "http://dreamlo.com/lb/", str1: dl.publicCode, str2: "/pipe-get/user-"));
							yield return www;
							if (!www.isDone)
							{
								break;
							}
							if (!string.IsNullOrEmpty(www.error))
							{
								Debug.Log("Sync data does not exist.");
								break;
							}
							string[] array5 = www.text.Split(new char[1] { '|' }, StringSplitOptions.None);
							dreamloLeaderBoard.Score score = new dreamloLeaderBoard.Score
							{
								playerName = array5[0],
								score = 0,
								seconds = 0,
								shortText = "",
								dateString = ""
							};
							if (array5.Length > 1)
							{
								score.score = int.Parse(array5[1]);
							}
							if (array5.Length > 2)
							{
								score.seconds = int.Parse(array5[2]);
							}
							if (array5.Length > 3)
							{
								score.shortText = array5[3];
							}
							if (array5.Length > 4)
							{
								score.dateString = array5[4];
							}
							if (array5.Length > 3)
							{
								string id = score.playerName.Replace("user-", "");
								myCharacter.id = id;
								string[] array6 = score.shortText.Split(new string[1] { "$" }, StringSplitOptions.None);
								if (array6.Length >= 6)
								{
									myCharacter.kill = IntParseFast(array6[1]);
									myCharacter.death = IntParseFast(array6[2]);
									myCharacter.survivalScore = IntParseFast(array6[3]);
									myCharacter.assortmentScore = IntParseFast(array6[4]);
									myCharacter.headshotScore = IntParseFast(array6[5]);
									SaveDataController.Save();
									ShowConfirm("Sync succeeded.", "You have to reboot Flats.", Reset, "OK", null);
								}
								else
								{
									ShowConfirm("Sync failed.", "Something wrong with your data...", null, "OK", null);
								}
							}
							else
							{
								ShowConfirm("Sync failed.", "There isn't your data on leaderboard.", null, "OK", null);
							}
							break;
						}
						case 19:
						{
							if (network == 1 || network == 2 || waitBackground)
							{
								ShowConfirm("Quit multiplayer mode or matchmaking!", "You can't use this function while playing multiplayer mode.", null, "OK", null);
								break;
							}
							pleaseWait.SetActive(true);
							LocalNetwork ln = GetComponent<LocalNetwork>();
							ln.masterIP = "Searching...";
							ln.StartReceivingDataForSync();
							float trial = 0f;
							while (true)
							{
								trial += Time.unscaledDeltaTime;
								if (trial >= 3f || ln.masterIP != "Searching...")
								{
									break;
								}
								yield return new WaitForSeconds(0f);
							}
							ln.StopAllCoroutines();
							ln.CloseReceiver();
							pleaseWait.SetActive(false);
							if (ln.masterIP == "Searching...")
							{
								Debug.Log("There is no sender, be a sender.");
								syncing = true;
								ln.StartCoroutine("StartSendingDataForSync");
								ShowConfirm("Sending my data...", "Sending my data (ID:" + myCharacter.id + ")\nand waiting for a receiver...\nYou must close this after syncing.", SyncDataConfirm, "Close", null);
							}
							else
							{
								syncing = false;
								syncData = ln.masterIP;
								string[] array4 = syncData.Split(new string[1] { "$" }, StringSplitOptions.None);
								string text2 = array4[0];
								ShowConfirm("Received data!", "Received data from ID:" + text2 + "\nOverwrite your current data\nand reboot Flats.", SyncDataConfirm, "Overwrite", "Cancel");
							}
							break;
						}
						}
						break;
					}
					InputDevice inputDevice = InputManager.ActiveDevice;
					if (mt.GetChild(5).GetChild(3).GetChild(3)
						.gameObject.activeSelf && (Input.GetKeyUp(KeyCode.Escape) || inputDevice.CommandWasPressed || (!customControlEnabled && inputDevice.Action2.WasPressed) || (customControlEnabled && Input.GetButtonDown(customControl["Pick"]))))
					{
						mt.GetChild(5).GetChild(3).GetChild(3)
							.gameObject.SetActive(false);
						backButton.SetActive(true);
						MonoBehaviour.print("Back with B");
					}
				}
				else if (current == "Settings")
				{
					if (button == 10)
					{
						Debug.Log("Touch button mapping has been reset.");
						currentDetail.transform.GetChild(1).GetChild(0).GetChild(2)
							.GetComponent<Text>()
							.text = "";
						if (mySettings.control_handedness == 0)
						{
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
								.rectTransform()
								.anchorMin = new Vector2(1f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
								.rectTransform()
								.anchorMax = new Vector2(1f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
								.rectTransform()
								.anchorMin = new Vector2(1f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
								.rectTransform()
								.anchorMax = new Vector2(1f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
								.rectTransform()
								.anchorMin = new Vector2(1f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
								.rectTransform()
								.anchorMax = new Vector2(1f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
								.rectTransform()
								.anchorMin = new Vector2(1f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
								.rectTransform()
								.anchorMax = new Vector2(1f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
								.rectTransform()
								.anchoredPosition = new Vector2(0f - fireButtonPosition.x, fireButtonPosition.y);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
								.rectTransform()
								.anchoredPosition = new Vector2(0f - reloadButtonPosition.x, reloadButtonPosition.y);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
								.rectTransform()
								.anchoredPosition = new Vector2(0f - actionButtonPosition.x, actionButtonPosition.y);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
								.rectTransform()
								.anchoredPosition = new Vector2(0f - grenadeButtonPosition.x, grenadeButtonPosition.y);
						}
						else
						{
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
								.rectTransform()
								.anchorMin = new Vector2(0f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
								.rectTransform()
								.anchorMax = new Vector2(0f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
								.rectTransform()
								.anchorMin = new Vector2(0f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
								.rectTransform()
								.anchorMax = new Vector2(0f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
								.rectTransform()
								.anchorMin = new Vector2(0f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
								.rectTransform()
								.anchorMax = new Vector2(0f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
								.rectTransform()
								.anchorMin = new Vector2(0f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
								.rectTransform()
								.anchorMax = new Vector2(0f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
								.rectTransform()
								.anchoredPosition = fireButtonPosition;
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
								.rectTransform()
								.anchoredPosition = reloadButtonPosition;
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
								.rectTransform()
								.anchoredPosition = actionButtonPosition;
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
								.rectTransform()
								.anchoredPosition = grenadeButtonPosition;
						}
						currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
							.rectTransform()
							.localScale = new Vector3(1f, 1f, currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
							.rectTransform()
							.localScale.z);
						currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
							.rectTransform()
							.localScale = new Vector3(1f, 1f, currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
							.rectTransform()
							.localScale.z);
						currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
							.rectTransform()
							.localScale = new Vector3(1f, 1f, currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
							.rectTransform()
							.localScale.z);
						currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
							.rectTransform()
							.localScale = new Vector3(1f, 1f, currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
							.rectTransform()
							.localScale.z);
					}
				}
				else if (current == "Leaderboard")
				{
					if (button == 10)
					{
						leaderboardLoading.SetActive(true);
						StartCoroutine("Leaderboard", true);
					}
				}
				else if (current == "Information")
				{
					switch (button)
					{
					case 10:
						if (!VRmode)
						{
							string text3 = "Android: bit.ly/1Dn3fpL";
							string text4 = "iOS: apple.co/1Ke5yO2";
							string text5 = "Windows: bit.ly/1W0SGjR";
							string desc = ((Application.platform == RuntimePlatform.Android) ? ("#Flats \n" + text3 + "\n" + text4 + "\n" + text5 + "\n\n") : ((Application.platform != RuntimePlatform.IPhonePlayer) ? ("#Flats \n" + text5 + "\n" + text3 + "\n" + text4 + "\n\n") : ("#Flats \n" + text4 + "\n" + text3 + "\n" + text5 + "\n\n")));
							byte[] data = flatsLogo.texture.EncodeToPNG();
							string sharePath = System.IO.Path.Combine((FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath),"Flats-Share.png");
							System.IO.File.WriteAllBytes(sharePath,data);
							GUIUtility.systemCopyBuffer = "Flats - offline desktop edition";
							ShowConfirm("Share saved", "Text copied to clipboard. Image saved to:\n"+sharePath, null, "OK", null);
						}
						else
						{
							ShowConfirm("This option is unavailable.", "Sorry, currently this option is not supported in VR mode.", null, "OK", null);
						}
						break;
					case 11:
					{
						aboutUs.SetActive(true);
						Selectable component = aboutUs.transform.GetChild(4).GetComponent<Selectable>();
						if (Input.GetJoystickNames().Length > 0)
						{
							component.Select();
						}
						break;
					}
					case 12:
						if (VRController.device == "oculus")
						{
							ShowConfirm("Not available", "Sorry, currently not available.", null, "OK", null);
							break;
						}
						if (fireTV)
						{
							ShowConfirm("Not available from TV", "Sorry, currently this option is unavalable.", null, "OK", null);
							break;
						}
						if (adFree)
						{
							GetComponent<InAppPurchase>().Buy("beer");
						}
						else
						{
							GetComponent<InAppPurchase>().Buy("adremover");
						}
						Debug.Log("Opening in-app purchase...");
						break;
					case 13:
					{
						MarketingDesc marketingDesc = new MarketingDesc();
						marketingDesc.Editor_URL = "http://foliagegames.com/";
						marketingDesc.Win8_PackageFamilyName = "FoliageGamesLLC.Flats_arh4z6sc73q8a";
						marketingDesc.WP8_AppID = "9wzdncrdh8vm";
						marketingDesc.iOS_AppID = "833603987";
						marketingDesc.BB10_AppID = "";
						marketingDesc.Android_MarketingStore = MarketingStores.GooglePlay;
						marketingDesc.Android_GooglePlay_BundleID = "com.foliagegames.flats";
						marketingDesc.Android_Amazon_BundleID = "com.foliagegames.flats";
						marketingDesc.Android_Samsung_BundleID = "com.foliagegames.flats";
						MarketingManager.OpenStoreForReview(marketingDesc);
						break;
					}
					case 14:
						Application.OpenURL("http://foliagegames.com");
						break;
					case 15:
						Application.OpenURL("https://www.facebook.com/foliagegames");
						break;
					case 16:
						Application.OpenURL("https://twitter.com/foliagegames");
						break;
					case 17:
						Application.OpenURL("https://plus.google.com/107882397860279823163");
						break;
					}
				}
			}
			else if (current == "Matching")
			{
				switch (button)
				{
				case -1:
					anim.SetBool("Matching", false);
					yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
					if (stayRoom.isOn)
					{
						waitBackground = true;
						backButton.SetActive(false);
						BackToMainMenu();
						break;
					}
					bt[0].text = "Open Match";
					bt[1].text = "Invitation Match";
					bt[2].text = "Local Match";
					bt[3].text = "Chat Room";
					bt[4].text = "Server Region";
					bt[5].text = "Online Version: " + version.Substring(0, 3);
					buttons[0].sprite = images[36];
					buttons[1].sprite = images[37];
					buttons[2].sprite = images[38];
					buttons[3].sprite = images[39];
					buttons[4].sprite = images[40];
					buttons[5].sprite = images[41];
					waitBackground = false;
					stayRoom.interactable = false;
					startNow.interactable = false;
					UnityEngine.Object.Destroy(myButton);
					wasInRoom = false;
					PhotonNetwork.Disconnect();
					currentDetail = null;
					anim.SetBool("Detail", false);
					anim.SetBool("Fade", false);
					current = "Multiplayer";
					break;
				case 10:
					if (!startNowPressed)
					{
						base.gameObject.GetPhotonView().RPC("StartNow", PhotonTargets.AllBuffered);
						startNowPressed = true;
					}
					break;
				case 12:
					if (!VRmode)
					{
						if (!PhotonNetwork.inRoom)
						{
						}
					}
					else
					{
						ShowConfirm("This option is unavailable.", "Sorry, currently this option is not supported in VR mode.", null, "OK", null);
					}
					break;
				}
			}
			else if (current == "Map")
			{
				if (!voted)
				{
					base.gameObject.GetPhotonView().RPC("VoteMap", PhotonTargets.AllBuffered, button);
					voted = true;
				}
			}
			else if (current == "Result" && button == -1)
			{
				backButton.SetActive(false);
				Time.timeScale = 1f;
				if (gameState == "Multiplayer")
				{
					wasInRoom = false;
					PhotonNetwork.Disconnect();
				}
				if (adForWin != null && adForWin.Visible)
				{
					adForWin.Visible = false;
				}
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
				anim.SetBool("Fade", false);
				mt.GetChild(10).gameObject.SetActive(false);
				StartCoroutine("BackgroundColor", "FadeIn");
			}
			if (current == "Main")
			{
				quitButton.SetActive(true);
			}
			else
			{
				quitButton.SetActive(false);
			}
			if (currentDetail != null)
			{
				Debug.Log("current:" + current + " currentDetail:" + currentDetail.name);
			}
			else
			{
				Debug.Log("current:" + current + " currentDetail: null");
			}
			fliping = false;
		}

		private void SyncDataConfirm(bool result)
		{
			LocalNetwork component = GetComponent<LocalNetwork>();
			if (result)
			{
				if (syncing)
				{
					component.StopAllCoroutines();
					component.CloseSender();
					syncing = false;
					syncData = "";
				}
				else if (syncData != "" && syncData != "Searching...")
				{
					string[] array = syncData.Split(new string[1] { "$" }, StringSplitOptions.None);
					myCharacter.id = array[0];
					myCharacter.kill = IntParseFast(array[1]);
					myCharacter.death = IntParseFast(array[2]);
					myCharacter.survivalScore = IntParseFast(array[3]);
					myCharacter.assortmentScore = IntParseFast(array[4]);
					myCharacter.headshotScore = IntParseFast(array[5]);
					SaveDataController.Save();
					LoadOfflineScene(0);
				}
				else
				{
					Debug.Log("Synced but no data, something wrong!");
					LoadOfflineScene(0);
				}
			}
			else
			{
				component.StopAllCoroutines();
				component.CloseSender();
				syncing = false;
				syncData = "";
			}
		}

		[PunRPC]
		private void StartNow()
		{
			startNowPlayer++;
			Debug.Log("Current start now player: " + startNowPlayer);
			int num = playerCount;
			if (num % 2 == 1)
			{
				num++;
			}
			if (num <= 2)
			{
				num = 4;
			}
			if (playerCount <= 2 || rule == 1 || rule == 6 || rule == 8)
			{
				startNow.transform.GetChild(0).GetComponent<Text>().text = "Start Now! " + startNowPlayer + "/" + num / 2;
			}
			else
			{
				startNow.transform.GetChild(0).GetComponent<Text>().text = "Add bot and Start Now! " + startNowPlayer + "/" + num / 2;
			}
			if (startNowPlayer >= num / 2)
			{
				Debug.Log("More than half players want to start now, so it's going to begin.");
				botCount = playerCount - PhotonNetwork.room.PlayerCount;
				playerCount = PhotonNetwork.room.PlayerCount;
				if (rule == 1 || rule == 6 || rule == 8)
				{
					botCount = 0;
				}
				StartCoroutine("Ready");
			}
		}

		[PunRPC]
		private void Chat(string t)
		{
			PhotonNetwork.InstantiateSceneObject(data: new object[1] { t }, prefabName: "ChatText", position: Vector3.zero, rotation: Quaternion.identity, group: 0);
			if (chat.GetChild(4).childCount >= 18)
			{
				PhotonNetwork.Destroy(chat.GetChild(4).GetChild(0).gameObject);
			}
		}

		private void Reset(bool result)
		{
			if (result)
			{
                bool disconnect = gameState == "Multiplayer";
                gameState = "Main";
                wasInRoom = false;
				if (disconnect)
				{
					PhotonNetwork.Disconnect();
				}
				skipTitle = true;
				gameState = "Main";
				LoadOfflineScene(0);
			}
		}

		private void Quit(bool result)
		{
			if (result)
			{
				Application.Quit();
			}
		}

		private IEnumerator Ready()
		{
			MonoBehaviour.print("Start syncing...");
			roomTexts[4].text = "Syncing... up to a minute.";
			PhotonNetwork.room.IsOpen = false;
			PhotonNetwork.room.IsVisible = false;
			if (waitBackground && current != "Matching")
			{
				ShowConfirm("Multiplayer Ready", "If you are playing singleplayer, current score will be saved.", null, "OK", null);
				Time.timeScale = 0f;
				if (gameState == "Singleplayer")
				{
					myCurrent.survival_Score = currentSurvivalScore;
					myCurrent.survival_Phase = currentSurvivalPhase;
					myCurrent.assortment_Score = currentAssortmentScore;
					myCurrent.assortment_Phase = currentAssortmentPhase;
					myCurrent.headshot_Score = currentHeadshotScore;
					myCurrent.headshot_Chain = currentHeadshotChain;
					SaveDataController.Save();
				}
				waitBackground = false;
			}
			else
			{
				backButton.SetActive(false);
				startNow.interactable = false;
				Debug.Log("Stopped start now function.");
			}
			syncedPlayer = 0;
			yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(5f));
			base.gameObject.GetPhotonView().RPC("Sync", PhotonTargets.AllBuffered);
			while (true)
			{
				if (PhotonNetwork.isMasterClient && syncedPlayer >= PhotonNetwork.room.PlayerCount)
				{
					Debug.Log("I'm the master");
					base.gameObject.GetPhotonView().RPC("DecideMap", PhotonTargets.AllBuffered);
					break;
				}
				if (!(gameState == "Multiplayer"))
				{
					yield return new WaitForSeconds(0f);
					continue;
				}
				break;
			}
		}

		[PunRPC]
		private void Sync()
		{
			syncedPlayer++;
		}

		[PunRPC]
		private IEnumerator DecideMap()
		{
			if (current != "Matching")
			{
				backButton.SetActive(false);
				anim.SetTrigger("SkipToMatching");
				current = "Matching";
			}
			if (rule != 1 && rule != 6 && rule != 8)
			{
				roomTexts[4].text = "Adjusting team members...";
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
			}
			if (PhotonNetwork.isMasterClient && PunTeams.PlayersPerTeam[PunTeams.Team.red].Count != PunTeams.PlayersPerTeam[PunTeams.Team.blue].Count)
			{
				Debug.Log("Adjusting team count...");
				if (PunTeams.PlayersPerTeam[PunTeams.Team.red].Count < PunTeams.PlayersPerTeam[PunTeams.Team.blue].Count)
				{
					PhotonPlayer player = PunTeams.PlayersPerTeam[PunTeams.Team.blue][0];
					player.SetTeam(PunTeams.Team.red);
				}
				else
				{
					PhotonPlayer player2 = PunTeams.PlayersPerTeam[PunTeams.Team.red][0];
					player2.SetTeam(PunTeams.Team.blue);
				}
			}
			waitBackground = false;
			anim.SetBool("Matching", false);
			yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
			anim.SetBool("Fade", false);
			current = "Map";
			gameState = "Multiplayer";
			voteMap.text = "Vote map";
			voteMap.gameObject.SetActive(true);
			int time = 10;
			while (time > 0)
			{
				voteMap.text = "Vote map " + time;
				bt[0].text = stageName[0] + " : " + vote[0].mapValue;
				bt[1].text = stageName[1] + " : " + vote[1].mapValue;
				bt[2].text = stageName[2] + " : " + vote[2].mapValue;
				bt[3].text = stageName[3] + " : " + vote[3].mapValue;
				bt[4].text = stageName[4] + " : " + vote[4].mapValue;
				bt[5].text = stageName[5] + " : " + vote[5].mapValue;
				buttons[0].sprite = images[12];
				buttons[1].sprite = images[13];
				buttons[2].sprite = images[14];
				buttons[3].sprite = images[15];
				buttons[4].sprite = images[16];
				buttons[5].sprite = images[17];
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(1f));
				time--;
				if (time <= 0)
				{
					break;
				}
				yield return new WaitForSeconds(0f);
			}
			anim.SetBool("Fade", true);
			yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
			voteMap.gameObject.SetActive(false);
			network = 2;
			Debug.Log("Network mode:" + network);
			while (!PhotonNetwork.isMasterClient)
			{
				yield return new WaitForSeconds(0f);
			}
			vote.Sort((Map x, Map y) => y.mapValue.CompareTo(x.mapValue));
			int num;
			if (vote[0].mapValue == 0)
			{
				num = vote[UnityEngine.Random.Range(0, 6)].mapKey + 2;
			}
			else
			{
				int num2 = 0;
				for (int num3 = 1; num3 < vote.Count; num3++)
				{
					if (vote[num3].mapValue == vote[0].mapValue)
					{
						num2 = num3;
					}
				}
				num = ((num2 != 0) ? (vote[UnityEngine.Random.Range(0, num2 + 1)].mapKey + 2) : (vote[0].mapKey + 2));
			}
			base.gameObject.GetPhotonView().RPC("LoadMap", PhotonTargets.AllBuffered, num);
		}

		[PunRPC]
		private void VoteMap(int map)
		{
			vote[map].mapValue++;
		}

		[PunRPC]
		private IEnumerator LoadMap(int map)
		{
			StartCoroutine("BackgroundColor", "FadeIn");
			yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(2f));
			LoadOfflineScene(map);
		}

		private void OnJoinedRoom()
		{
            pendingRoomDeadline = 0;
            if (startingOfflineMatch) return;
            if (!CheckRoomModules()) return;
			Debug.Log("Joined!");
			if ((int)PhotonNetwork.room.CustomProperties["R"] == -1)
			{
				chat.GetChild(5).gameObject.SetActive(false);
				chat.GetChild(3).GetComponent<Button>().interactable = true;
				base.gameObject.GetPhotonView().RPC("Chat", PhotonTargets.MasterClient, PhotonNetwork.player.NickName + " joined chat.");
				return;
			}
			wasInRoom = true;
			pleaseWait.SetActive(false);
			backButton.SetActive(true);
			anim.SetBool("Matching", true);
			current = "Matching";
			stayRoom.interactable = true;
			startNow.interactable = true;
			rule = (int)PhotonNetwork.room.CustomProperties["R"];
			objective = (int)PhotonNetwork.room.CustomProperties["O"];
			playerCount = PhotonNetwork.room.MaxPlayers;
			roomTexts[0].text = ruleTitleText[rule];
			roomTexts[1].text = ruleExpText[rule];
			roomTexts[2].text = "Objective: " + objectiveText[rule + "-" + objective];
			roomTexts[3].text = "Player Count: " + playerCount;
			roomTexts[4].text = "Matchmaking... Wait or press Start Now.";
			int num = playerCount;
			if (num % 2 == 1)
			{
				num++;
			}
			if (num <= 2)
			{
				num = 4;
			}
			if (playerCount <= 2 || rule == 1 || rule == 6 || rule == 8)
			{
				startNow.transform.GetChild(0).GetComponent<Text>().text = "Start Now! " + startNowPlayer + "/" + num / 2;
			}
			else
			{
				startNow.transform.GetChild(0).GetComponent<Text>().text = "Add bot and Start Now! " + startNowPlayer + "/" + num / 2;
			}
			if (myButton == null)
			{
				myButton = (GameObject)UnityEngine.Object.Instantiate(playerButton);
				myButton.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(myCharacter.color)
					.GetComponent<Image>()
					.color;
				myButton.transform.GetChild(0).GetComponent<Image>().sprite = mt.GetChild(5).GetChild(0).GetChild(0)
					.GetChild(0)
					.GetComponent<Image>()
					.sprite;
				myButton.transform.GetChild(1).GetComponent<Text>().text = PhotonNetwork.player.NickName;
				DetailInformation component = myButton.GetComponent<DetailInformation>();
				component.canvas = mt;
				component.backgroundColor = myButton.GetComponent<Image>().color;
				component.comment = myCharacter.comment;
				component.kill = myCharacter.kill;
				component.death = myCharacter.death;
				myButton.transform.SetParent(multiplayerList, false);
			}
			if (rule == 1 || rule == 6 || rule == 8)
			{
				PhotonNetwork.player.SetTeam(PunTeams.Team.none);
			}
			else if ((bool)myButton)
			{
				if (PunTeams.PlayersPerTeam[PunTeams.Team.red].Count <= PunTeams.PlayersPerTeam[PunTeams.Team.blue].Count)
				{
					PhotonNetwork.player.SetTeam(PunTeams.Team.red);
					myButton.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(9)
						.GetComponent<Image>()
						.color;
				}
				else
				{
					PhotonNetwork.player.SetTeam(PunTeams.Team.blue);
					myButton.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(7)
						.GetComponent<Image>()
						.color;
				}
				if (preCheckToStayRoom)
				{
					stayRoom.isOn = true;
				}
			}
			else
			{
				stayRoom.interactable = false;
				startNow.interactable = false;
				PhotonNetwork.Disconnect();
			}
			MonoBehaviour.print("Red Team:" + PunTeams.PlayersPerTeam[PunTeams.Team.red].Count + " Blue Team:" + PunTeams.PlayersPerTeam[PunTeams.Team.blue].Count);
			PhotonPlayer[] otherPlayers = PhotonNetwork.otherPlayers;
			foreach (PhotonPlayer photonPlayer in otherPlayers)
			{
				byte[] data = (byte[])photonPlayer.CustomProperties["I"];
				Texture2D texture2D = new Texture2D(128, 128);
				texture2D.LoadImage(data);
				GameObject gameObject = (GameObject)UnityEngine.Object.Instantiate(playerButton);
				if (rule == 1 || rule == 6 || rule == 8)
				{
					int index = (int)photonPlayer.CustomProperties["TC"];
					gameObject.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(index)
						.GetComponent<Image>()
						.color;
				}
				else if (photonPlayer.GetTeam() == PunTeams.Team.red)
				{
					gameObject.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(9)
						.GetComponent<Image>()
						.color;
				}
				else
				{
					gameObject.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(7)
						.GetComponent<Image>()
						.color;
				}
				gameObject.transform.GetChild(0).GetComponent<Image>().sprite = Sprite.Create(texture2D, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f));
				gameObject.transform.GetChild(1).GetComponent<Text>().text = photonPlayer.NickName;
				DetailInformation component2 = gameObject.GetComponent<DetailInformation>();
				component2.canvas = base.transform;
				component2.backgroundColor = gameObject.GetComponent<Image>().color;
				component2.comment = (string)photonPlayer.CustomProperties["C"];
				component2.kill = (int)photonPlayer.CustomProperties["K"];
				component2.death = (int)photonPlayer.CustomProperties["D"];
				component2.id = photonPlayer.ID;
				gameObject.transform.SetParent(multiplayerList, false);
				if (photonPlayer.GetTeam() == PunTeams.Team.red)
				{
					gameObject.transform.SetAsFirstSibling();
				}
			}
			if (PhotonNetwork.room.PlayerCount >= PhotonNetwork.room.MaxPlayers)
			{
				PhotonNetwork.SetMasterClient(PhotonNetwork.player);
				StartCoroutine("Ready");
			}
		}

		private void OnPhotonPlayerConnected(PhotonPlayer newPlayer)
		{
            if (PhotonNetwork.isMasterClient && !CheckPeerModules(newPlayer)) return;
			if ((int)PhotonNetwork.room.CustomProperties["R"] == -1)
			{
				chat.GetChild(5).gameObject.SetActive(false);
				chat.GetChild(3).GetComponent<Button>().interactable = true;
				return;
			}
			Debug.Log("Someone joined!");
			byte[] data = (byte[])newPlayer.CustomProperties["I"];
			Texture2D texture2D = new Texture2D(128, 128);
			texture2D.LoadImage(data);
			GameObject gameObject = (GameObject)UnityEngine.Object.Instantiate(playerButton);
			if (rule == 1 || rule == 6 || rule == 8)
			{
				int index = (int)newPlayer.CustomProperties["TC"];
				gameObject.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(index)
					.GetComponent<Image>()
					.color;
			}
			else if (newPlayer.GetTeam() == PunTeams.Team.red)
			{
				gameObject.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(9)
					.GetComponent<Image>()
					.color;
			}
			else
			{
				gameObject.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(7)
					.GetComponent<Image>()
					.color;
			}
			gameObject.transform.GetChild(0).GetComponent<Image>().sprite = Sprite.Create(texture2D, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f));
			gameObject.transform.GetChild(1).GetComponent<Text>().text = newPlayer.NickName;
			DetailInformation component = gameObject.GetComponent<DetailInformation>();
			component.canvas = base.transform;
			component.backgroundColor = gameObject.GetComponent<Image>().color;
			component.comment = (string)newPlayer.CustomProperties["C"];
			component.kill = (int)newPlayer.CustomProperties["K"];
			component.death = (int)newPlayer.CustomProperties["D"];
			component.id = newPlayer.ID;
			gameObject.transform.SetParent(multiplayerList, false);
			if (newPlayer.GetTeam() == PunTeams.Team.red)
			{
				gameObject.transform.SetAsFirstSibling();
			}
			if (PhotonNetwork.room.PlayerCount >= PhotonNetwork.room.MaxPlayers)
			{
				PhotonNetwork.SetMasterClient(newPlayer);
				StartCoroutine("Ready");
			}
		}

		private void OnPhotonPlayerDisconnected(PhotonPlayer otherPlayer)
		{
			foreach (Transform multiplayer in multiplayerList)
			{
				if (multiplayer.GetComponent<DetailInformation>().id == otherPlayer.ID)
				{
					UnityEngine.Object.Destroy(multiplayer.gameObject);
				}
			}
			PunTeams.PlayersPerTeam[otherPlayer.GetTeam()].Remove(otherPlayer);
			otherPlayer.CustomProperties["team"] = (byte)PunTeams.Team.none;
		}

		private void OnPhotonCreateRoomFailed(object[] codeAndMsg)
		{
            pendingRoomDeadline = 0;
			pleaseWait.SetActive(false);
			errorMessage.SetActive(true);
			Selectable component = errorMessage.transform.GetChild(2).GetComponent<Selectable>();
			if (Input.GetJoystickNames().Length > 0)
			{
				component.Select();
			}
		}

		private void OnPhotonRandomJoinFailed(object[] codeAndMsg)
		{
			Debug.Log("There is no room that matches your conditions. Created a new room instead.");
			if (rule == 0)
			{
				rule = UnityEngine.Random.Range(1, 8);
			}
			if (objective == 0)
			{
				objective = 1;
			}
			if (playerCount == 0)
			{
				playerCount = 4;
			}
			roomTexts[0].text = ruleTitleText[rule];
			roomTexts[1].text = ruleExpText[rule];
			roomTexts[2].text = "Objective: " + objectiveText[rule + "-" + objective];
			roomTexts[3].text = "Player Count: " + playerCount;
			roomTexts[4].text = "Matchmaking... Wait or press Start Now.";
			ExitGames.Client.Photon.Hashtable hashtable = new ExitGames.Client.Photon.Hashtable();
			hashtable["R"] = rule;
			hashtable["O"] = objective;
            PublishRoomModules(hashtable);
			RoomOptions roomOptions = new RoomOptions();
			roomOptions.MaxPlayers = (byte)playerCount;
			roomOptions.CustomRoomProperties = hashtable;
			roomOptions.CustomRoomPropertiesForLobby = new string[2] { "R", "O" };
			string roomName = "pub-" + StringUtils.GeneratePassword(8);
			PhotonNetwork.CreateRoom(roomName, roomOptions, null);
		}

		private void OnPhotonJoinRoomFailed(object[] codeAndMsg)
		{
            pendingRoomDeadline = 0;
            if (currentDetail == null) return;
            backButton.SetActive(true);
			if (currentDetail.name != "ChatRoom")
			{
				Debug.Log("There is no room that matches your room name.");
				pleaseWait.SetActive(false);
				rule = 1;
				objective = 1;
				playerCount = 4;
				roomCreation.transform.GetChild(0).GetChild(1).GetComponent<Text>()
					.text = ruleTitleText[rule];
				roomCreation.transform.GetChild(1).GetChild(1).GetComponent<Text>()
					.text = objectiveText[rule + "-" + objective];
				roomCreation.transform.GetChild(2).GetChild(1).GetComponent<Text>()
					.text = playerCount.ToString();
				SetRoomCreationVisible(true);
				Selectable component = roomCreation.transform.GetChild(3).GetComponent<Selectable>();
				if (Input.GetJoystickNames().Length > 0)
				{
					component.Select();
				}
			}
		}

		private void OnDisconnectedFromPhoton()
		{
			if (gettingRoomList)
			{
				Debug.Log("I got the room list, disconnected from Photon.");
				gettingRoomList = false;
				return;
			}
			if (wasInRoom)
			{
				errorMessage.SetActive(true);
				Selectable component = errorMessage.transform.GetChild(2).GetComponent<Selectable>();
				if (Input.GetJoystickNames().Length > 0)
				{
					component.Select();
				}
			}
			if (network == 2)
			{
				network = 0;
			}
			startNowPlayer = 0;
			startNowPressed = false;
			wasInRoom = false;
			waitBackground = false;
			stayRoom.isOn = false;
			stayRoom.interactable = false;
			startNow.interactable = false;
            PhotonNetwork.player.CustomProperties["team"] = (byte)PunTeams.Team.none;
			if (multiplayerList.childCount > 0)
			{
				foreach (Transform multiplayer in multiplayerList)
				{
					UnityEngine.Object.Destroy(multiplayer.gameObject);
				}
			}
			if (gameState == "Multiplayer" && current != "Singleplayer" && current != "Result")
			{
				LoadOfflineScene(0);
			}
		}

		private void OnFailedToConnectToPhoton(DisconnectCause cause)
		{
            multiplayerFailure = "Photon connection failed: " + cause;
            if (multiplayerConnecting) return;
			if (!gettingRoomList)
			{
				errorMessage.SetActive(true);
				pleaseWait.SetActive(false);
				Selectable component = errorMessage.transform.GetChild(2).GetComponent<Selectable>();
				if (Input.GetJoystickNames().Length > 0)
				{
					component.Select();
				}
			}
		}

		private bool multiplayerWasSuspended;
		private void OnApplicationPause(bool pause)
		{
			// Unity also sends an initial false callback to newly loaded behaviours.
			// It is not a resume and must never disconnect a newly started match.
			if (!Application.isMobilePlatform) return;
			if (pause) { multiplayerWasSuspended = true; return; }
			if (multiplayerWasSuspended)
			{
				multiplayerWasSuspended = false;
				if (gameState == "Multiplayer")
				{
					PhotonNetwork.Disconnect();
				}
				else if (PhotonNetwork.inRoom)
				{
					wasInRoom = true;
				}
				else
				{
					wasInRoom = false;
				}
			}
		}

		private void OnApplicationQuit()
		{
			waitBackground = false;
			stayRoom.isOn = false;
		}

		private IEnumerator Leaderboard(bool upload)
		{
			foreach (Transform item in leaderboardScroll.GetChild(0))
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
			if (myCharacter != null)
			{
				if (myCharacter.name == "" || myCharacter.name == null)
				{
					myCharacter.name = "No Name";
				}
				float kd = myCharacter.death == 0 ? myCharacter.kill : (float)myCharacter.kill / myCharacter.death;
				int average = (myCharacter.survivalScore + myCharacter.assortmentScore + myCharacter.headshotScore) / 3;
				int num = Mathf.RoundToInt((kd + 1f) * average / 2f);
                totalScore = num.ToString();
                mt.GetChild(7).GetChild(0).GetChild(3).GetComponent<Text>().text = totalScore;
				string shortText = myCharacter.name + "$" + myCharacter.kill + "$" + myCharacter.death + "$" + myCharacter.survivalScore + "$" + myCharacter.assortmentScore + "$" + myCharacter.headshotScore;
				dl.AddScore("user-" + myCharacter.id, num, 0, shortText);
			}
			dl.LoadScores();
			if (!upload)
			{
				uploadButton.SetActive(false);
			}
			List<dreamloLeaderBoard.Score> playerList = new List<dreamloLeaderBoard.Score>();
			int maxToDisplay = 20;
			int count = 0;
			yield return null;
			playerList = dl.ToListHighToLow();
			foreach (dreamloLeaderBoard.Score item2 in playerList)
			{
				GameObject gameObject = (GameObject)UnityEngine.Object.Instantiate(leaderboardContent);
				gameObject.transform.SetParent(leaderboardScroll.GetChild(0), false);
				Text component = gameObject.transform.GetChild(0).GetComponent<Text>();
				Text component2 = gameObject.transform.GetChild(1).GetComponent<Text>();
				Text component3 = gameObject.transform.GetChild(2).GetComponent<Text>();
				if (item2.shortText != "")
				{
					count++;
					component.text = count.ToString();
					string[] array = item2.shortText.Split(new string[1] { "$" }, StringSplitOptions.None);
					component2.text = array[0].Replace("+", " ");
					int score2 = item2.score;
					component3.text = score2.ToString();
				}
				if (count >= maxToDisplay || count >= playerList.Count)
				{
					break;
				}
			}
			leaderboardLoading.SetActive(false);
		}

        private IEnumerator Information()
        {
            purchaseButton.text = "Store purchases unavailable";
#if UNITY_WEBGL && !UNITY_EDITOR
            news.text = "FLATS Web\nOnline play uses the site's configured Photon service.\nBuilt-in crosshair settings and data presets are supported.\nDownloaded DLL mods require desktop Mono.\nBrowser saves may be cleared by the browser.\n\nOriginal game: © Foliage Games LLC";
#else
            news.text = "Windows reconstruction\nScores and settings are saved on this PC.\nOnline multiplayer uses your configured Photon app.\nStore purchase verification is unavailable.\n\nOriginal game: © Foliage Games LLC";
#endif
            yield break;
        }

		private void imageLoadedCallback(Stream stream, bool succeeded)
		{
			if (!succeeded)
			{
				if (stream != null)
				{
					stream.Dispose();
				}
				return;
			}
			try
			{
				byte[] array = new byte[stream.Length];
				stream.Read(array, 0, array.Length);
				Texture2D texture2D = new Texture2D(128, 128);
				texture2D.LoadImage(array);
				texture2D.Apply();
				if (texture2D.width < 128 || texture2D.height < 128)
				{
					ShowConfirm("Image size error", "Image scale must be larger than 128x128.", null, "OK", null);
					return;
				}
				array = texture2D.EncodeToPNG();
				System.IO.File.WriteAllBytes((FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath) + "/Flats_UserIcon.png", array);
				mt.GetChild(5).GetChild(0).GetChild(0)
					.GetChild(0)
					.GetComponent<Image>()
					.sprite = Sprite.Create(texture2D, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f));
			}
			catch (Exception ex)
			{
				Debug.Log(ex.Message);
			}
			finally
			{
				if (stream != null)
				{
					stream.Dispose();
				}
			}
		}

        private Color MainThemeColor(Color color)
        {
            return new Color(color.r * .5f + .25f, color.g * .5f + .25f, color.b * .5f + .25f, mainUI.color.a);
        }
		public IEnumerator BackgroundColor(string command)
		{
			float alpha = 0.2f;
			Color backgroundThemeColor = mt.GetChild(5).GetChild(1).GetChild(myCharacter.color)
				.GetComponent<Image>()
				.color;
			Color mainColor = MainThemeColor(backgroundThemeColor);
			selected.color = backgroundThemeColor;
			switch (command)
			{
			case "OpenMenu":
				backgroundRenderer.sharedMaterial.color = new Color(0f, 0f, 0f, 0f);
				break;
			case "CloseMenu":
				backgroundRenderer.sharedMaterial.color = new Color(0f, 0f, 0f, alpha);
				break;
			case "FadeOut":
			case "SkippedTitle":
				backgroundRenderer.sharedMaterial.color = new Color(backgroundThemeColor.r, backgroundThemeColor.g, backgroundThemeColor.b, 1f);
				break;
			case "Title":
				backgroundRenderer.sharedMaterial.color = new Color(0f, 0f, 0f, 1f);
				break;
			case "Respawn":
				mt.parent.GetChild(1).Find("Mask").GetComponent<Image>()
					.enabled = true;
				break;
			default:
				backgroundRenderer.sharedMaterial.color = new Color(0f, 0f, 0f, alpha);
				break;
			}
			if (command == "Title" || command == "SkippedTitle")
			{
				ParticleSystem bp = GameObject.Find("BackgroundParticle").GetComponent<ParticleSystem>();
				bp.startColor = backgroundThemeColor;
				bp.Play();
				if (command == "SkippedTitle")
				{
					yield return new WaitForSeconds(0.2f);
				}
			}
			if (VRmode && command != "Respawn")
			{
				mt.parent.GetChild(1).Find("Mask").GetComponent<Image>()
					.enabled = false;
			}
			while (true)
			{
				backgroundRenderer.enabled = true;
				switch (command)
				{
				case "OpenMenu":
				{
					float unscaledDeltaTime = Time.unscaledDeltaTime;
					float a3 = Mathf.MoveTowards(backgroundRenderer.sharedMaterial.color.a, alpha, unscaledDeltaTime);
					backgroundRenderer.sharedMaterial.color = new Color(0f, 0f, 0f, a3);
					if (backgroundRenderer.sharedMaterial.color.a == alpha)
					{
						yield break;
					}
					goto default;
				}
				case "CloseMenu":
				{
					float unscaledDeltaTime3 = Time.unscaledDeltaTime;
					float a6 = Mathf.MoveTowards(backgroundRenderer.sharedMaterial.color.a, 0f, unscaledDeltaTime3);
					backgroundRenderer.sharedMaterial.color = new Color(0f, 0f, 0f, a6);
					if (backgroundRenderer.sharedMaterial.color.a == 0f)
					{
						backgroundRenderer.enabled = false;
						yield break;
					}
					goto default;
				}
				case "Change":
				{
					float unscaledDeltaTime2 = Time.unscaledDeltaTime;
					float r3 = Mathf.MoveTowards(mainUI.color.r, mainColor.r, unscaledDeltaTime2);
					float g3 = Mathf.MoveTowards(mainUI.color.g, mainColor.g, unscaledDeltaTime2);
					float b3 = Mathf.MoveTowards(mainUI.color.b, mainColor.b, unscaledDeltaTime2);
					mainUI.color = new Color(r3, g3, b3, mainUI.color.a);
					if (mainUI.color == mainColor)
					{
						yield break;
					}
					goto default;
				}
				case "FadeIn":
				{
					float maxDelta5 = Time.deltaTime;
					float r4 = Mathf.MoveTowards(backgroundRenderer.sharedMaterial.color.r, backgroundThemeColor.r, maxDelta5);
					float g4 = Mathf.MoveTowards(backgroundRenderer.sharedMaterial.color.g, backgroundThemeColor.g, maxDelta5);
					float b4 = Mathf.MoveTowards(backgroundRenderer.sharedMaterial.color.b, backgroundThemeColor.b, maxDelta5);
					float a7 = Mathf.MoveTowards(backgroundRenderer.sharedMaterial.color.a, 1f, maxDelta5);
					backgroundRenderer.sharedMaterial.color = new Color(r4, g4, b4, a7);
					if (backgroundRenderer.sharedMaterial.color == new Color(backgroundThemeColor.r, backgroundThemeColor.g, backgroundThemeColor.b, 1f))
					{
						if (current == "Result")
						{
							gameState = "Main";
							current = "Main";
							LoadOfflineScene(0);
						}
						yield break;
					}
					goto default;
				}
				case "FadeOut":
				{
					float maxDelta4 = Time.deltaTime;
					float a5 = Mathf.MoveTowards(backgroundRenderer.sharedMaterial.color.a, 0f, maxDelta4);
					backgroundRenderer.sharedMaterial.color = new Color(backgroundRenderer.sharedMaterial.color.r, backgroundRenderer.sharedMaterial.color.g, backgroundRenderer.sharedMaterial.color.b, a5);
					if (backgroundRenderer.sharedMaterial.color.a == 0f)
					{
						mt.parent.GetChild(1).Find("Mask").GetComponent<Image>()
							.enabled = false;
						backgroundRenderer.enabled = false;
						yield break;
					}
					goto default;
				}
				case "Respawn":
				{
					float maxDelta3 = Time.deltaTime;
					float a4 = Mathf.MoveTowards(backgroundRenderer.sharedMaterial.color.a, 1f, maxDelta3);
					backgroundRenderer.sharedMaterial.color = new Color(backgroundThemeColor.r, backgroundThemeColor.g, backgroundThemeColor.b, a4);
					if (backgroundRenderer.sharedMaterial.color.a == 1f)
					{
						yield break;
					}
					goto default;
				}
				case "Title":
				{
					float maxDelta2 = Time.deltaTime;
					float a2 = Mathf.MoveTowards(backgroundRenderer.sharedMaterial.color.a, alpha, maxDelta2);
					if (backgroundRenderer.sharedMaterial.color.a != alpha)
					{
						backgroundRenderer.sharedMaterial.color = new Color(0f, 0f, 0f, a2);
					}
					else
					{
						float r2 = Mathf.MoveTowards(mainUI.color.r, mainColor.r, maxDelta2);
						float g2 = Mathf.MoveTowards(mainUI.color.g, mainColor.g, maxDelta2);
						float b2 = Mathf.MoveTowards(mainUI.color.b, mainColor.b, maxDelta2);
						mainUI.color = new Color(r2, g2, b2, mainUI.color.a);
						if (mainUI.color == mainColor)
						{
							yield break;
						}
					}
					goto default;
				}
				case "SkippedTitle":
				{
					float maxDelta = Time.deltaTime;
					float r = Mathf.MoveTowards(backgroundRenderer.sharedMaterial.color.r, 0f, maxDelta);
					float g = Mathf.MoveTowards(backgroundRenderer.sharedMaterial.color.g, 0f, maxDelta);
					float b = Mathf.MoveTowards(backgroundRenderer.sharedMaterial.color.b, 0f, maxDelta);
					float a = Mathf.MoveTowards(backgroundRenderer.sharedMaterial.color.a, alpha, maxDelta);
					backgroundRenderer.sharedMaterial.color = new Color(r, g, b, a);
					if (backgroundRenderer.sharedMaterial.color == new Color(0f, 0f, 0f, alpha))
					{
						anim.SetTrigger("OpenMenu");
						yield break;
					}
					goto default;
				}
				default:
					yield return new WaitForSeconds(0f);
					break;
				}
			}
		}

		public void ReadyForAd()
		{ /* Advertising disabled in the standalone offline recovery. */ }

		private void adCreatedCallback(bool succeeded)
		{
			if (succeeded && current == "Result")
			{
				adForWin.Visible = true;
			}
		}

		private static void eventCallback(AdEvents adEvent, string eventMessage)
		{
		}

		private IEnumerator GameOver()
		{
			if (gameState == "Singleplayer" || (gameState == "Multiplayer" && Multiplayer.rule == 8))
			{
				GameObject[] array = GameObject.FindGameObjectsWithTag("Enemy");
				GameObject[] array2 = array;
				foreach (GameObject gameObject in array2)
				{
					if ((bool)gameObject.GetComponent<AI>())
					{
						gameObject.GetComponent<AI>().StopAllCoroutines();
					}
				}
			}
			canOpen = false;
			EasyTouch.SetEnabled(false);
			stick.transform.parent.gameObject.SetActive(false);
			Text phaseText = GameObject.Find("Message").transform.GetChild(0).GetComponent<Text>();
			phaseText.enabled = true;
			phaseText.text = "Game Over";
			if (gameState == "Singleplayer")
			{
				if (Singleplayer.rule == 0)
				{
					myCurrent.survival_Score = 0;
					myCurrent.survival_Phase = 0;
				}
				else if (Singleplayer.rule == 1)
				{
					myCurrent.assortment_Score = 0;
					myCurrent.assortment_Phase = 0;
				}
				else if (Singleplayer.rule == 2)
				{
					myCurrent.headshot_Score = 0;
					myCurrent.headshot_Chain = 0;
				}
				SaveDataController.Save();
			}
			yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(1.5f));
			yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(1f));
			Time.timeScale = 0f;
			if (gameState == "Multiplayer" && Multiplayer.rule != 8)
			{
				List<GameObject> list = new List<GameObject>();
				if (network == 1)
				{
					foreach (PlayerInfo localNetworkPlayer in localNetworkPlayerList)
					{
						GameObject gameObject2 = (GameObject)UnityEngine.Object.Instantiate(result);
						if (Multiplayer.rule == 1 || Multiplayer.rule == 6)
						{
							int color = localNetworkPlayer.color;
							gameObject2.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(color)
								.GetComponent<Image>()
								.color;
						}
						else if (localNetworkPlayer.team == "red")
						{
							gameObject2.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(9)
								.GetComponent<Image>()
								.color;
						}
						else
						{
							gameObject2.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(7)
								.GetComponent<Image>()
								.color;
						}
						gameObject2.transform.GetChild(1).GetComponent<Text>().text = localNetworkPlayer.name;
						int kill = localNetworkPlayer.kill;
						int death = localNetworkPlayer.death;
						gameObject2.transform.GetChild(2).GetComponent<Text>().text = kill.ToString();
						gameObject2.transform.GetChild(3).GetComponent<Text>().text = death.ToString();
						if (Multiplayer.rule >= 3)
						{
							gameObject2.transform.GetChild(4).GetComponent<Text>().text = "--";
						}
						else if (death == 0)
						{
							gameObject2.transform.GetChild(4).GetComponent<Text>().text = ((float)kill).ToString("F2");
						}
						else
						{
							gameObject2.transform.GetChild(4).GetComponent<Text>().text = ((float)kill / (float)death).ToString("F2");
						}
						gameObject2.transform.SetParent(multiplayerResultList, false);
						list.Add(gameObject2);
					}
				}
				else
				{
					PhotonPlayer[] playerList = PhotonNetwork.playerList;
					foreach (PhotonPlayer photonPlayer in playerList)
					{
						GameObject gameObject3 = (GameObject)UnityEngine.Object.Instantiate(result);
						if (Multiplayer.rule == 1 || Multiplayer.rule == 6)
						{
							int index = (int)photonPlayer.CustomProperties["TC"];
							gameObject3.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(index)
								.GetComponent<Image>()
								.color;
						}
						else if (photonPlayer.GetTeam() == PunTeams.Team.red)
						{
							gameObject3.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(9)
								.GetComponent<Image>()
								.color;
						}
						else
						{
							gameObject3.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(7)
								.GetComponent<Image>()
								.color;
						}
						gameObject3.transform.GetChild(1).GetComponent<Text>().text = photonPlayer.NickName;
						int num = (int)photonPlayer.CustomProperties["K"];
						int num2 = (int)photonPlayer.CustomProperties["D"];
						gameObject3.transform.GetChild(2).GetComponent<Text>().text = num.ToString();
						gameObject3.transform.GetChild(3).GetComponent<Text>().text = num2.ToString();
						if (Multiplayer.rule >= 3)
						{
							gameObject3.transform.GetChild(4).GetComponent<Text>().text = "--";
						}
						else if (num2 == 0)
						{
							gameObject3.transform.GetChild(4).GetComponent<Text>().text = ((float)num).ToString("F2");
						}
						else
						{
							gameObject3.transform.GetChild(4).GetComponent<Text>().text = ((float)num / (float)num2).ToString("F2");
						}
						gameObject3.transform.SetParent(multiplayerResultList, false);
						list.Add(gameObject3);
						if (Multiplayer.rule <= 2 && photonPlayer.IsLocal)
						{
							myCharacter.kill += num;
							myCharacter.death += num2;
							SaveDataController.Save();
						}
					}
				}
				if (Multiplayer.rule < 3)
				{
					if (PhotonNetwork.offlineMode)
						foreach (var botScore in FlatsOfflineScores.Bots)
						{
							var row=(GameObject)UnityEngine.Object.Instantiate(result);
							row.GetComponent<Image>().color=mt.GetChild(5).GetChild(1).GetChild(botScore.team==0?9:7).GetComponent<Image>().color;
							row.transform.GetChild(1).GetComponent<Text>().text=botScore.name;
							row.transform.GetChild(2).GetComponent<Text>().text=botScore.kills.ToString();
							row.transform.GetChild(3).GetComponent<Text>().text=botScore.deaths.ToString();
							row.transform.GetChild(4).GetComponent<Text>().text=((float)botScore.kills/Mathf.Max(1,botScore.deaths)).ToString("F2");
							row.transform.SetParent(multiplayerResultList,false);list.Add(row);
						}
					list.Sort((GameObject x, GameObject y) => float.Parse(y.transform.GetChild(2).GetComponent<Text>().text).CompareTo(float.Parse(x.transform.GetChild(2).GetComponent<Text>().text)));
				}
				int rs = Multiplayer.redTeamScore;
				int bs = Multiplayer.blueTeamScore;
				if (Multiplayer.rule != 1 && Multiplayer.rule != 6)
				{
					if (rs >= bs)
					{
						for (int num3 = list.Count - 1; num3 > -1; num3--)
						{
							if (list[num3].GetComponent<Image>().color == mt.GetChild(5).GetChild(1).GetChild(9)
								.GetComponent<Image>()
								.color)
							{
								list[num3].transform.SetAsFirstSibling();
							}
						}
					}
					else
					{
						for (int num4 = list.Count - 1; num4 > -1; num4--)
						{
							if (list[num4].GetComponent<Image>().color == mt.GetChild(5).GetChild(1).GetChild(7)
								.GetComponent<Image>()
								.color)
							{
								list[num4].transform.SetAsFirstSibling();
							}
						}
					}
					if (rs > bs)
					{
						resultIndex.text = "Winner:Red Team";
					}
					else if (bs > rs)
					{
						resultIndex.text = "Winner:Blue Team";
					}
					else
					{
						resultIndex.text = "Draw";
					}
				}
				else if (rule == 6)
				{
					for (int num5 = list.Count - 1; num5 > -1; num5--)
					{
						list[num5].transform.SetAsFirstSibling();
					}
					resultIndex.text = "Result";
					if (Multiplayer.rule == 6)
					{
						if (rs > bs)
						{
							resultIndex.text = "Winner:Survivors";
						}
						else if (bs > rs)
						{
							resultIndex.text = "Winner:Zombies";
						}
						else
						{
							resultIndex.text = "Draw";
						}
					}
				}
				else
				{
					for (int num6 = list.Count - 1; num6 > -1; num6--)
					{
						list[num6].transform.SetAsFirstSibling();
					}
				}
				for (int num7 = 0; num7 < multiplayerResultList.childCount; num7++)
				{
					multiplayerResultList.GetChild(num7).GetChild(0).GetComponent<Text>()
						.text = (num7 + 1).ToString();
				}
				if (Multiplayer.rule != 1 && Multiplayer.rule != 6)
				{
					GameObject gameObject4 = (GameObject)UnityEngine.Object.Instantiate(result);
					gameObject4.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(9)
						.GetComponent<Image>()
						.color;
					for (int num8 = 0; num8 < gameObject4.transform.childCount; num8++)
					{
						switch (num8)
						{
						case 1:
							gameObject4.transform.GetChild(num8).GetComponent<Text>().text = "Red Team";
							break;
						case 4:
							gameObject4.transform.GetChild(num8).GetComponent<Text>().text = rs.ToString();
							break;
						default:
							gameObject4.transform.GetChild(num8).GetComponent<Text>().text = "";
							break;
						}
					}
					GameObject gameObject5 = (GameObject)UnityEngine.Object.Instantiate(result);
					gameObject5.GetComponent<Image>().color = mt.GetChild(5).GetChild(1).GetChild(7)
						.GetComponent<Image>()
						.color;
					for (int num9 = 0; num9 < gameObject5.transform.childCount; num9++)
					{
						switch (num9)
						{
						case 1:
							gameObject5.transform.GetChild(num9).GetComponent<Text>().text = "Blue Team";
							break;
						case 4:
							gameObject5.transform.GetChild(num9).GetComponent<Text>().text = bs.ToString();
							break;
						default:
							gameObject5.transform.GetChild(num9).GetComponent<Text>().text = "";
							break;
						}
					}
					gameObject4.transform.SetParent(multiplayerResultList, false);
					gameObject5.transform.SetParent(multiplayerResultList, false);
					if (rs >= bs)
					{
						gameObject4.transform.SetAsFirstSibling();
						int siblingIndex = 0;
						for (int num10 = 0; num10 < multiplayerResultList.childCount; num10++)
						{
							if (multiplayerResultList.GetChild(num10).GetComponent<Image>().color == mt.GetChild(5).GetChild(1).GetChild(7)
								.GetComponent<Image>()
								.color)
							{
								siblingIndex = num10;
								break;
							}
						}
						gameObject5.transform.SetSiblingIndex(siblingIndex);
					}
					else
					{
						gameObject5.transform.SetAsFirstSibling();
						int siblingIndex2 = 0;
						for (int num11 = 0; num11 < multiplayerResultList.childCount; num11++)
						{
							if (multiplayerResultList.GetChild(num11).GetComponent<Image>().color == mt.GetChild(5).GetChild(1).GetChild(9)
								.GetComponent<Image>()
								.color)
							{
								siblingIndex2 = num11;
								break;
							}
						}
						gameObject4.transform.SetSiblingIndex(siblingIndex2);
					}
				}
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(2f));
				if (Input.mousePresent)
				{
					Screen.lockCursor = false;
					UnityEngine.Cursor.visible = true;
				}
				AudioSource[] sources = ambient.GetComponents<AudioSource>();
				AudioSource[] array3 = sources;
				foreach (AudioSource audioSource in array3)
				{
					audioSource.Stop();
				}
				phaseText.text = "";
				phaseText.enabled = false;
				anim.Play("Multiplayer Result");
				current = "Result";
				canOpen = true;
				skipTitle = true;
				backButton.SetActive(true);
			}
			else if (gameState == "Singleplayer" || Multiplayer.rule == 8)
			{
				int myScore = 0;
				if (Singleplayer.rule == 0 || Multiplayer.rule == 8)
				{
					resultIndex.text = "Result";
					singleplayerResult.GetChild(0).GetComponent<Text>().text = "Score: " + currentSurvivalScore + "\nDied at Phase " + currentSurvivalPhase;
					myScore = currentSurvivalScore;
				}
				else if (Singleplayer.rule == 1)
				{
					resultIndex.text = "Result";
					singleplayerResult.GetChild(0).GetComponent<Text>().text = "Score: " + currentAssortmentScore + "\nDied at Phase " + currentAssortmentPhase;
					myScore = currentAssortmentScore;
				}
				else if (Singleplayer.rule == 2)
				{
					resultIndex.text = "Result";
					singleplayerResult.GetChild(0).GetComponent<Text>().text = "Score: " + currentHeadshotScore + "\nMax Headshot Chain: " + currentHeadshotChain;
					myScore = currentHeadshotScore;
				}
				string comment = ((myScore < 5000) ? "Beginner" : ((myScore < 10000) ? "Good Shooter" : ((myScore < 20000) ? "Survivor" : ((myScore < 50000) ? "Tough Guy" : ((myScore < 80000) ? "Gun Devil" : ((myScore < 100000) ? "Ninja" : ((myScore < 150000) ? "Crazy Killer" : ((myScore < 200000) ? "FPS Zombie" : ((myScore >= 300000) ? "Day Dreamer" : "Fribbler")))))))));
				string highscored = "";
				if (Singleplayer.rule == 0 && myScore > myCharacter.survivalScore)
				{
					myCharacter.survivalScore = myScore;
					highscored = "Highscore!!\n\n";
				}
				else if (Singleplayer.rule == 1 && myScore > myCharacter.assortmentScore)
				{
					myCharacter.assortmentScore = myScore;
					highscored = "Highscore!!\n\n";
				}
				else if (Singleplayer.rule == 2 && myScore > myCharacter.headshotScore)
				{
					myCharacter.headshotScore = myScore;
					highscored = "Highscore!!\n\n";
				}
				if (highscored != "")
				{
					SaveDataController.Save();
				}
				singleplayerResult.GetChild(1).GetComponent<Text>().text = highscored + "Your level is...\n" + comment;
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(2f));
				StartCoroutine("BackgroundColor", "OpenMenu");
				if (Input.mousePresent)
				{
					Screen.lockCursor = false;
					UnityEngine.Cursor.visible = true;
				}
				AudioSource[] sources2 = ambient.GetComponents<AudioSource>();
				AudioSource[] array4 = sources2;
				foreach (AudioSource audioSource2 in array4)
				{
					audioSource2.Stop();
				}
				phaseText.text = "";
				phaseText.enabled = false;
				anim.Play("Singleplayer Result");
				current = "Result";
				canOpen = true;
				skipTitle = true;
				backButton.SetActive(true);
			}
			if (VRmode)
			{
				Debug.Log("VR dead.");
				base.transform.parent.GetChild(6).gameObject.SetActive(false);
				Time.timeScale = 0f;
				Vector3 deadCamPos = Camera.main.transform.position + Vector3.up * 4f;
				Camera.main.gameObject.SetActive(false);
				GameObject deadVrCam = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("DeadVRCamera"));
				deadVrCam.transform.position = deadCamPos;
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(0.1f));
				mt.parent.localPosition = deadVrCam.transform.position + deadVrCam.transform.forward * 2.1f;
				mt.parent.eulerAngles = new Vector3(deadVrCam.transform.eulerAngles.x, deadVrCam.transform.eulerAngles.y, 0f);
				mt.GetComponent<Canvas>().worldCamera = deadVrCam.GetComponent<Camera>();
			}
			if (!adFree && VRController.device == "cardboard")
			{
				ReadyForAd();
			}
		}

		private void EnableVR(bool result)
		{
			if (result)
			{
				VRmode = true;
				FPSController.enableCamRotate = true;
				return;
			}
			VRmode = false;
			if (Application.loadedLevel != 0)
			{
				GameObject gameObject = Camera.main.transform.root.gameObject;
				if (gameObject.tag != "Player")
				{
					gameObject = Camera.main.transform.Find("Player").gameObject;
				}
				gameObject.GetComponent<IKController>().enabled = true;
				Camera.main.transform.GetChild(0).gameObject.SetActive(true);
				Camera.main.BroadcastMessage("UpdateStereoValues", SendMessageOptions.DontRequireReceiver);
				FPSController.enableCamRotate = false;
			}
		}

        private Flats.UI.ConfirmationPresenter confirmationPresenter;
        private Flats.UI.ConfirmationPresenter Confirmation
        {
            get { return confirmationPresenter ?? (confirmationPresenter = new Flats.UI.ConfirmationPresenter(confirm)); }
        }
        // Retained public entry points for persistent UnityEvents and existing callers.
        public void ShowConfirm(string title, string message, UnityAction<bool> action, string positiveBtnText, string negativeBtnText)
        {
            Confirmation.ShowConfirm(mt.GetChild(0).GetComponent<Image>().color, title, message, action, positiveBtnText, negativeBtnText);
        }
        public void OnClickedConfirm() { Confirmation.OnClickedConfirm(); }

		public void NameInput(string newName)
		{
			newName = newName.Replace("$", "");
			newName = newName.Replace("|", "");
			newName = newName.Replace("*", "");
			newName = newName.Replace("/", "");
			myCharacter.name = newName;
			Debug.Log("New name: " + myCharacter.name);
		}

		public void CommentInput(string newComment)
		{
			newComment = newComment.Replace("$", "");
			newComment = newComment.Replace("|", "");
			newComment = newComment.Replace("*", "");
			newComment = newComment.Replace("/", "");
			myCharacter.comment = newComment;
			Debug.Log("New comment: " + myCharacter.comment);
		}

		public void PlusMinus()
		{
			Transform parent = EventSystem.current.currentSelectedGameObject.transform.parent;
			int num = ((EventSystem.current.currentSelectedGameObject.name == "Plus") ? 1 : (-1));
			if (parent.name.StartsWith("Desktop")) { GetComponent<FlatsDesktopSettings>().Change(parent,num); return; }
			if (parent.name == "Rule")
			{
				rule += num;
				if (parent.parent.name == "RoomCreation")
				{
					if (rule < 1)
					{
						rule = ruleTitleText.Count - 1;
					}
					else if (rule > ruleTitleText.Count - 1)
					{
						rule = 1;
					}
				}
				else if (rule < 0)
				{
					rule = ruleTitleText.Count - 1;
				}
				else if (rule > ruleTitleText.Count - 1)
				{
					rule = 0;
				}
				parent.GetChild(1).GetComponent<Text>().text = ruleTitleText[rule];
				if (parent.parent.name == "RoomCreation")
				{
					objective = 1;
				}
				else
				{
					objective = 0;
				}
				parent.parent.GetChild(1).GetChild(1).GetComponent<Text>()
					.text = objectiveText[rule + "-" + objective];
				if (parent.parent.name == "RoomCreation")
				{
					playerCount = 4;
					parent.parent.GetChild(2).GetChild(1).GetComponent<Text>()
						.text = "4";
				}
				else
				{
					playerCount = 0;
					parent.parent.GetChild(2).GetChild(1).GetComponent<Text>()
						.text = "Any";
				}
			}
			else if (parent.name == "Objective")
			{
				if (rule < 1)
				{
					objective = 0;
				}
				else
				{
					objective += num;
					if (parent.parent.name == "RoomCreation")
					{
						if (objective < 1)
						{
							objective = 3;
						}
						else if (objective > 3)
						{
							objective = 1;
						}
					}
					else if (objective < 0)
					{
						objective = 3;
					}
					else if (objective > 3)
					{
						objective = 0;
					}
				}
				parent.GetChild(1).GetComponent<Text>().text = objectiveText[rule + "-" + objective];
			}
			else if (parent.name == "PlayerCount")
			{
				playerCount += num;
				if (playerCount == 1)
				{
					if (num < 0)
					{
						playerCount = 0;
					}
					else
					{
						playerCount = 2;
					}
				}
				if (parent.parent.name == "RoomCreation")
				{
					if (playerCount < 2)
					{
						playerCount = 8;
					}
					else if (playerCount > 8)
					{
						playerCount = 2;
					}
				}
				else if (playerCount < 0)
				{
					playerCount = 8;
				}
				else if (playerCount > 8)
				{
					playerCount = 0;
				}
				if (rule != 1 && rule != 6 && rule != 8)
				{
					if (num < 0)
					{
						if (playerCount == 3)
						{
							playerCount = 2;
						}
						else if (playerCount == 5)
						{
							playerCount = 4;
						}
						else if (playerCount == 7)
						{
							playerCount = 6;
						}
					}
					else if (playerCount == 3)
					{
						playerCount = 4;
					}
					else if (playerCount == 5)
					{
						playerCount = 6;
					}
					else if (playerCount == 7)
					{
						playerCount = 8;
					}
				}
				if (playerCount == 0)
				{
					parent.GetChild(1).GetComponent<Text>().text = "Any";
				}
				else
				{
					parent.GetChild(1).GetComponent<Text>().text = playerCount.ToString();
				}
			}
			else if (parent.name == "Region")
			{
                if (PhotonNetwork.inRoom)
                { ShowConfirm("Server Region", "Leave the current room before changing region.", null, "OK", null); return; }
				if (PhotonNetwork.PhotonServerSettings.PreferredRegion == CloudRegionCode.us)
				{
					if (num > 0)
					{
						PhotonNetwork.PhotonServerSettings.PreferredRegion = CloudRegionCode.eu;
					}
					else
					{
						PhotonNetwork.PhotonServerSettings.PreferredRegion = CloudRegionCode.asia;
					}
				}
				else if (PhotonNetwork.PhotonServerSettings.PreferredRegion == CloudRegionCode.eu)
				{
					if (num > 0)
					{
						PhotonNetwork.PhotonServerSettings.PreferredRegion = CloudRegionCode.asia;
					}
					else
					{
						PhotonNetwork.PhotonServerSettings.PreferredRegion = CloudRegionCode.us;
					}
				}
				else if (PhotonNetwork.PhotonServerSettings.PreferredRegion == CloudRegionCode.asia)
				{
					if (num > 0)
					{
						PhotonNetwork.PhotonServerSettings.PreferredRegion = CloudRegionCode.us;
					}
					else
					{
						PhotonNetwork.PhotonServerSettings.PreferredRegion = CloudRegionCode.eu;
					}
				}
				parent.GetChild(1).GetComponent<Text>().text = PhotonNetwork.PhotonServerSettings.PreferredRegion.ToString().ToUpper();
                if (PhotonNetwork.connected) PhotonNetwork.Disconnect();
			}
			else if (parent.name == "Attack")
			{
				if ((num < 0 && myCharacter.attack > 0) || (num > 0 && myCharacter.attack + myCharacter.defense < 10))
				{
					myCharacter.attack += num;
				}
				parent.GetChild(1).GetComponent<Text>().text = myCharacter.attack.ToString();
				parent.parent.GetChild(1).GetComponent<Text>().text = myCharacter.attack + myCharacter.defense + "/10";
			}
			else if (parent.name == "Defense")
			{
				if ((num < 0 && myCharacter.defense > 0) || (num > 0 && myCharacter.attack + myCharacter.defense < 10))
				{
					myCharacter.defense += num;
				}
				parent.GetChild(1).GetComponent<Text>().text = myCharacter.defense.ToString();
				parent.parent.GetChild(1).GetComponent<Text>().text = myCharacter.attack + myCharacter.defense + "/10";
			}
			else if (parent.name == "Volume-BGM")
			{
				if ((num < 0 && mySettings.sound_bgm > 0) || (num > 0 && mySettings.sound_bgm < 10))
				{
					mySettings.sound_bgm += num;
				}
				bgm1.volume = (float)mySettings.sound_bgm / 10f;
				if (Singleplayer.chance)
				{
					bgm2.volume = (float)mySettings.sound_bgm / 10f;
				}
				else
				{
					bgm2.volume = 0f;
				}
				parent.GetChild(1).GetComponent<Text>().text = mySettings.sound_bgm.ToString();
			}
			else if (parent.name == "Volume-All")
			{
				if ((num < 0 && mySettings.sound_all > 0) || (num > 0 && mySettings.sound_all < 10))
				{
					mySettings.sound_all += num;
				}
				AudioListener.volume = (float)mySettings.sound_all / 10f;
				parent.GetChild(1).GetComponent<Text>().text = mySettings.sound_all.ToString();
			}
			else if (parent.name == "Anti-Aliasing")
			{
				if (mySettings.graphics_aa == 0)
				{
					mySettings.graphics_aa = 1;
				}
				else
				{
					mySettings.graphics_aa = 0;
				}
				FPSController.aa = IntToBool(mySettings.graphics_aa);
				parent.GetChild(1).GetComponent<Text>().text = aaText[mySettings.graphics_aa];
			}
			else if (parent.name == "DepthOfField")
			{
				if (mySettings.graphics_dof == 0)
				{
					mySettings.graphics_dof = 1;
				}
				else
				{
					mySettings.graphics_dof = 0;
				}
				FPSController.dof = IntToBool(mySettings.graphics_dof);
				parent.GetChild(1).GetComponent<Text>().text = dofText[mySettings.graphics_dof];
			}
			else if (parent.name == "MotionBlur")
			{
				if (mySettings.graphics_motionBlur == 0)
				{
					mySettings.graphics_motionBlur = 1;
				}
				else
				{
					mySettings.graphics_motionBlur = 0;
				}
				FPSController.motionBlur = IntToBool(mySettings.graphics_motionBlur);
				parent.GetChild(1).GetComponent<Text>().text = motionBlurText[mySettings.graphics_motionBlur];
			}
			else if (parent.name == "EdgeRendering")
			{
				if (mySettings.graphics_edgeRendering == 0)
				{
					mySettings.graphics_edgeRendering = 1;
				}
				else
				{
					mySettings.graphics_edgeRendering = 0;
				}
				FPSController.edgeRendering = IntToBool(mySettings.graphics_edgeRendering);
				parent.GetChild(1).GetComponent<Text>().text = edgeRenderingText[mySettings.graphics_edgeRendering];
			}
			else if (parent.name == "SaturationFilter")
			{
				if (mySettings.graphics_saturationFilter == 0)
				{
					mySettings.graphics_saturationFilter = 1;
				}
				else
				{
					mySettings.graphics_saturationFilter = 0;
				}
				FPSController.saturationFilter = IntToBool(mySettings.graphics_saturationFilter);
				parent.GetChild(1).GetComponent<Text>().text = saturationFilterText[mySettings.graphics_saturationFilter];
			}
			else if (parent.name == "CameraSensitivity")
			{
				mySettings.control_sensitivity += num;
				if (mySettings.control_sensitivity > 2)
				{
					mySettings.control_sensitivity = 0;
				}
				else if (mySettings.control_sensitivity < 0)
				{
					mySettings.control_sensitivity = 2;
				}
				FPSController.sensitivity = mySettings.control_sensitivity + 1;
				parent.GetChild(1).GetComponent<Text>().text = sensitivityText[mySettings.control_sensitivity];
			}
			else if (parent.name == "Handedness")
			{
				mySettings.control_handedness += num;
				if (mySettings.control_handedness > 1)
				{
					mySettings.control_handedness = 0;
				}
				else if (mySettings.control_handedness < 0)
				{
					mySettings.control_handedness = 1;
				}
				Transform child = mt.parent.GetChild(1);
				if (mySettings.control_handedness == 0)
				{
					stick.joystickArea = ETCJoystick.JoystickArea.Left;
					child.GetChild(1).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterRight;
					child.GetChild(2).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterRight;
					child.GetChild(3).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterRight;
					child.GetChild(4).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterRight;
					child.GetChild(5).rectTransform().anchoredPosition3D = new Vector3(0f - Mathf.Abs(child.GetChild(5).rectTransform().anchoredPosition3D.x), child.GetChild(5).rectTransform().anchoredPosition3D.y, child.GetChild(5).rectTransform().anchoredPosition3D.z);
					child.GetChild(6).rectTransform().anchoredPosition3D = new Vector3(0f - Mathf.Abs(child.GetChild(6).rectTransform().anchoredPosition3D.x), child.GetChild(6).rectTransform().anchoredPosition3D.y, child.GetChild(6).rectTransform().anchoredPosition3D.z);
				}
				else
				{
					stick.joystickArea = ETCJoystick.JoystickArea.Right;
					child.GetChild(1).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterLeft;
					child.GetChild(2).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterLeft;
					child.GetChild(3).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterLeft;
					child.GetChild(4).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterLeft;
					child.GetChild(5).rectTransform().anchoredPosition3D = new Vector3(Mathf.Abs(child.GetChild(5).rectTransform().anchoredPosition3D.x), child.GetChild(5).rectTransform().anchoredPosition3D.y, child.GetChild(5).rectTransform().anchoredPosition3D.z);
					child.GetChild(6).rectTransform().anchoredPosition3D = new Vector3(Mathf.Abs(child.GetChild(6).rectTransform().anchoredPosition3D.x), child.GetChild(6).rectTransform().anchoredPosition3D.y, child.GetChild(6).rectTransform().anchoredPosition3D.z);
				}
				FPSController.handedness = mySettings.control_handedness;
				parent.GetChild(1).GetComponent<Text>().text = handednessText[mySettings.control_handedness];
			}
			else if (parent.name == "Y-Axis")
			{
				if (mySettings.control_yAxis == 0)
				{
					mySettings.control_yAxis = 1;
				}
				else
				{
					mySettings.control_yAxis = 0;
				}
				FPSController.invertY = IntToBool(mySettings.control_yAxis);
				parent.GetChild(1).GetComponent<Text>().text = yAxisText[mySettings.control_yAxis];
			}
			else if (parent.name == "AutoAim")
			{
				if (mySettings.control_autoAim == 0)
				{
					mySettings.control_autoAim = 1;
				}
				else
				{
					mySettings.control_autoAim = 0;
				}
				FPSController.autoAim = IntToBool(mySettings.control_autoAim);
				parent.GetChild(1).GetComponent<Text>().text = autoAimText[mySettings.control_autoAim];
			}
			else if (parent.name == "TapFiring")
			{
				if (mySettings.control_tapFiring == 0)
				{
					mySettings.control_tapFiring = 1;
				}
				else
				{
					mySettings.control_tapFiring = 0;
				}
				FPSController.tapFiring = IntToBool(mySettings.control_tapFiring);
				parent.GetChild(1).GetComponent<Text>().text = tapFiringText[mySettings.control_tapFiring];
			}
			else if (parent.name == "Resolution")
			{
				mySettings.vr_resolution += num;
				if (mySettings.vr_resolution > 2)
				{
					mySettings.vr_resolution = 0;
				}
				else if (mySettings.vr_resolution < 0)
				{
					mySettings.vr_resolution = 2;
				}
				if (mySettings.vr_resolution == 2)
				{
					if (VRmode && !(VRController.device == "cardboard") && !(VRController.device == "oculus"))
					{
					}
				}
				else if (mySettings.vr_resolution == 1)
				{
					if (VRmode && !(VRController.device == "cardboard") && !(VRController.device == "oculus"))
					{
					}
				}
				else if (VRmode && !(VRController.device == "cardboard"))
				{
					bool flag = VRController.device == "oculus";
				}
				parent.GetChild(1).GetComponent<Text>().text = resolutionText[mySettings.vr_resolution];
			}
			else if (parent.name == "EyeDistance")
			{
				mySettings.vr_eyeDistance += num;
				if (mySettings.vr_eyeDistance > 2)
				{
					mySettings.vr_eyeDistance = 0;
				}
				else if (mySettings.vr_eyeDistance < 0)
				{
					mySettings.vr_eyeDistance = 2;
				}
				string text = ((mySettings.vr_eyeDistance != 0) ? ("+" + (float)mySettings.vr_eyeDistance * 0.5f) : "Default");
				VRController.offset = (float)mySettings.vr_eyeDistance * 0.5f;
				parent.GetChild(1).GetComponent<Text>().text = text;
			}
			else if (parent.name == "HeadRotation")
			{
				if (mySettings.vr_headRotation == 0)
				{
					mySettings.vr_headRotation = 1;
				}
				else
				{
					mySettings.vr_headRotation = 0;
				}
				parent.GetChild(1).GetComponent<Text>().text = headRotationText[mySettings.vr_headRotation];
			}
			else if (parent.name == "BatterySaver")
			{
				if (mySettings.extra_batterySaver == 0)
				{
					mySettings.extra_batterySaver = 1;
				}
				else
				{
					mySettings.extra_batterySaver = 0;
				}
				Application.targetFrameRate = 60 - 30 * mySettings.extra_batterySaver;
				parent.GetChild(1).GetComponent<Text>().text = batteryText[mySettings.extra_batterySaver];
			}
			else if (parent.name == "Notification")
			{
				if (mySettings.extra_notification == 0)
				{
					mySettings.extra_notification = 1;
				}
				else
				{
					mySettings.extra_notification = 0;
				}
				parent.GetChild(1).GetComponent<Text>().text = notificationText[mySettings.extra_notification];
			}
		}

		public static int IntParseFast(string value)
		{
			int num = 0;
			foreach (char c in value)
			{
				num = 10 * num + (c - 48);
			}
			return num;
		}

		private static bool IntToBool(int num)
		{
			if (num == 1)
			{
				return true;
			}
			return false;
		}

		public static bool isMaster()
		{
			if (network == 0)
			{
				return true;
			}
			if (network == 1)
			{
				return false;
			}
			if (network == 2)
			{
				if (PhotonNetwork.isMasterClient)
				{
					return true;
				}
				return false;
			}
			return false;
		}

		public Menu()
		{
			aaText = new Dictionary<int, string>();
			dofText = new Dictionary<int, string>();
			motionBlurText = new Dictionary<int, string>();
			edgeRenderingText = new Dictionary<int, string>();
			saturationFilterText = new Dictionary<int, string>();
			sensitivityText = new Dictionary<int, string>();
			handednessText = new Dictionary<int, string>();
			yAxisText = new Dictionary<int, string>();
			autoAimText = new Dictionary<int, string>();
			tapFiringText = new Dictionary<int, string>();
			resolutionText = new Dictionary<int, string>();
			headRotationText = new Dictionary<int, string>();
			notificationText = new Dictionary<int, string>();
			batteryText = new Dictionary<int, string>();
			stage = -1;
			savedTimeScale = 1f;
			vote = new List<Map>();
            // Remote votes can arrive while DecideMap is still yielding for
            // the local fade. RPC data must exist before that UI coroutine.
            for (int i = 0; i < 6; i++)
                vote.Add(new Map { mapKey = i });
			syncData = "";
			roomTexts = new Text[5];
			url = "https://dl.dropboxusercontent.com/s/ahx0zuddx9t4gre/News.txt";
			invitedRules = new List<int>();
			roomList = new RoomInfo[0];
			framerateLimit = 20f;
			playerListForCheck = new List<string>();

		}

		[CompilerGenerated]
		private static int _003CDecideMap_003Eb__27(Map x, Map y)
		{
			return y.mapValue.CompareTo(x.mapValue);
		}

		[CompilerGenerated]
		private static int _003CGameOver_003Eb__3c(GameObject x, GameObject y)
		{
			return float.Parse(y.transform.GetChild(2).GetComponent<Text>().text).CompareTo(float.Parse(x.transform.GetChild(2).GetComponent<Text>().text));
		}




	}
