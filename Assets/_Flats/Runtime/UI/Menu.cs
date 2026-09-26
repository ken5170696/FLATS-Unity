using System;
using System.Linq;
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
    // Compatibility boundary while legacy modes still write these public fields.
    private sealed class GameplaySession : Flats.Core.IGameSessionContext
    {
        public bool IsPlaying { get { return current == "Playing"; } }
        public int NetworkMode { get { return network; } }
    }
    private readonly Flats.Core.IGameSessionContext gameplaySession = new GameplaySession();
    private readonly Flats.Core.PauseNavigationSession pauseNavigation = new Flats.Core.PauseNavigationSession();

    public void BindGameplay(FPSController player)
    {
        player.ConfigureGameplay(gameplaySession, new Flats.Gameplay.UnityDesktopPlayerInput(), false);
    }

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
	private bool returningToMenu;

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
		if (Current == this) Current = null;
        if (captureAction != null) FlatsControls.Capturing = false;
        FlatsLocalization.Changed -= RefreshPersonalRows;
        if (localDiscovery != null) localDiscovery.Stop();
        Canvas.preWillRenderCanvases -= BindThemeMaterials;
		if (runtimeBackgroundMaterial != null) Destroy(runtimeBackgroundMaterial);
        if (runtimeMainUI != null) Destroy(runtimeMainUI);
        if (runtimeSelected != null) Destroy(runtimeSelected);
	}

	private void Awake()
	{
		Current = this;
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
        var syncExplanation = mt.Find("Character/Sync/Explanation");
        if (syncExplanation != null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            syncExplanation.GetComponent<Text>().text = "Save files: Export on source; Import old save here.\nPreview before replacing the current save.\n\nBrowsers cannot use UDP LAN Sync.\nUse save files to transfer your progress.\n\nLegacy cloud restoration is unavailable.\nThe ID/Receive entry cannot recover old cloud data.";
#else
            syncExplanation.GetComponent<Text>().text = "Save files: Export on source; Import old save here.\nPreview before replacing the current save.\n\nLAN Sync (native builds, same network):\nOpen source first, then receiver; confirm scores.\nOnly ID + scores transfer, not gameplay.\n\nLegacy cloud restoration is unavailable.\nThe ID/Receive entry cannot recover old cloud data.";
#endif
        }
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
		string text = Flats.Modules.ModRules.GameVersion;
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
				update.transform.GetChild(1).GetComponent<Text>().text = "FLATS " + text + " preview";
				update.transform.GetChild(2).GetComponent<Text>().text = "Welcome to FLATS.\nSingleplayer and Photon online play.\nOnline play requires an internet connection.";
				update.transform.GetChild(4).GetComponent<Text>().text = "- Separate aim sensitivity; Kill Cinematic switch.\n- Controller: common FPS layout, steady look speed.\n- Settings scroll; all key bindings on one page.\n- Mobile: Swap button; settings while spectating.\n- Bullet tracers fade in toward the bullet.\n\nPlatform validation: see release notes.";
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
				update.transform.GetChild(2).GetComponent<Text>().text = "Controls, settings and mobile play updates.";
				update.transform.GetChild(4).GetComponent<Text>().text = "- Separate aim sensitivity; Kill Cinematic switch.\n- Controller: common FPS layout, steady look speed.\n- Settings scroll; all key bindings on one page.\n- Mobile: Swap button; settings while spectating.\n- Bullet tracers fade in toward the bullet.\n\nPlatform validation: see release notes.";
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
			bt[i] = tileArtwork.labels[i];
            // Main sprite rectangles and padding now match the original 512px assets.
            // Preserve the authored Image RectTransform instead of a tight-crop workaround.
		}
		ambient = GameObject.Find("Ambient");
		AudioSource[] components = ambient.GetComponents<AudioSource>();
		bgm1 = components[0];
		bgm2 = components[1];
		stick.transform.SetAsFirstSibling();
	}

	private static bool tutorialLaunchConsumed;

	private IEnumerator Start()
	{
        InitializeLanguageButton();
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
		// FlatsUserIcon restores the default avatar when the file is missing, unreadable or
		// too small, so the character screen and room entry never throw on a cleaned folder.
		Texture2D icon = new Texture2D(128, 128)
		{
			filterMode = FilterMode.Bilinear
		};
		icon.LoadImage(FlatsUserIcon.Read(defaultIcon));
		characterScreen.GetChild(0).GetChild(0)
			.GetChild(0)
			.GetComponent<Image>()
			.sprite = Sprite.Create(icon, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f));
		characterScreen.GetChild(0).GetChild(1)
			.GetComponent<InputField>()
			.text = myCharacter.name;
		characterScreen.GetChild(0).GetChild(2)
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
		characterScreen.GetChild(2).GetChild(1)
			.GetComponent<Text>()
			.text = totalScore;
		characterScreen.GetChild(2).GetChild(3)
			.GetComponent<Text>()
			.text = multiScore;
		characterScreen.GetChild(2).GetChild(5)
			.GetComponent<Text>()
			.text = myCharacter.kill.ToString();
		characterScreen.GetChild(2).GetChild(7)
			.GetComponent<Text>()
			.text = myCharacter.death.ToString();
		characterScreen.GetChild(2).GetChild(9)
			.GetComponent<Text>()
			.text = singleScore;
		characterScreen.GetChild(2).GetChild(11)
			.GetComponent<Text>()
			.text = myCharacter.survivalScore.ToString();
		characterScreen.GetChild(2).GetChild(13)
			.GetComponent<Text>()
			.text = myCharacter.assortmentScore.ToString();
		characterScreen.GetChild(2).GetChild(15)
			.GetComponent<Text>()
			.text = myCharacter.headshotScore.ToString();
		characterScreen.GetChild(3).GetChild(0)
			.GetChild(1)
			.GetComponent<Image>()
			.sprite = characterScreen.GetChild(3).GetChild(2)
			.GetChild(myCharacter.primaryWeapon)
			.GetChild(0)
			.GetComponent<Image>()
			.sprite;
		characterScreen.GetChild(3).GetChild(0)
			.GetChild(2)
			.GetComponent<Text>()
			.text = characterScreen.GetChild(3).GetChild(2)
			.GetChild(myCharacter.primaryWeapon)
			.GetChild(1)
			.GetComponent<Text>()
			.text;
		characterScreen.GetChild(3).GetChild(1)
			.GetChild(1)
			.GetComponent<Image>()
			.sprite = characterScreen.GetChild(3).GetChild(2)
			.GetChild(myCharacter.secondaryWeapon)
			.GetChild(0)
			.GetComponent<Image>()
			.sprite;
		characterScreen.GetChild(3).GetChild(1)
			.GetChild(2)
			.GetComponent<Text>()
			.text = characterScreen.GetChild(3).GetChild(2)
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
		characterScreen.GetChild(4).GetChild(1)
			.GetComponent<Text>()
			.text = myCharacter.attack + myCharacter.defense + "/10";
		characterScreen.GetChild(4).GetChild(2)
			.GetChild(1)
			.GetComponent<Text>()
			.text = myCharacter.attack.ToString();
		characterScreen.GetChild(4).GetChild(3)
			.GetChild(1)
			.GetComponent<Text>()
			.text = myCharacter.defense.ToString();
		characterScreen.GetChild(5).GetChild(0)
			.GetComponent<Text>()
			.text = "ID:" + myCharacter.id;
        InitializeVolumeSliders();
        InitializeControls();
		aaText[0] = "OFF";
		aaText[1] = "ON";
		FPSController.aa = IntToBool(mySettings.graphics_aa);
		SettingValue(1, "Anti-Aliasing").text = aaText[mySettings.graphics_aa];
		dofText[0] = "OFF";
		dofText[1] = "ON";
		FPSController.dof = IntToBool(mySettings.graphics_dof);
		SettingValue(1, "DepthOfField").text = dofText[mySettings.graphics_dof];
		motionBlurText[0] = "OFF";
		motionBlurText[1] = "ON";
		FPSController.motionBlur = IntToBool(mySettings.graphics_motionBlur);
		SettingValue(1, "MotionBlur").text = motionBlurText[mySettings.graphics_motionBlur];
		edgeRenderingText[0] = "OFF";
		edgeRenderingText[1] = "ON";
		FPSController.edgeRendering = IntToBool(mySettings.graphics_edgeRendering);
		SettingValue(1, "EdgeRendering").text = edgeRenderingText[mySettings.graphics_edgeRendering];
		saturationFilterText[0] = "OFF";
		saturationFilterText[1] = "ON";
		FPSController.saturationFilter = IntToBool(mySettings.graphics_saturationFilter);
		SettingValue(1, "SaturationFilter").text = saturationFilterText[mySettings.graphics_saturationFilter];
		sensitivityText[0] = "Low";
		sensitivityText[1] = "Normal";
		sensitivityText[2] = "High";
		FPSController.sensitivity = mySettings.control_sensitivity + 1;
		SettingValue(2, "CameraSensitivity").text = sensitivityText[mySettings.control_sensitivity];
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
			SetInteractAnchor(ui, ETCBase.RectAnchor.CenterRight);
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
			SetInteractAnchor(ui, ETCBase.RectAnchor.CenterLeft);
			ui.GetChild(5).rectTransform().anchoredPosition3D = new Vector3(Mathf.Abs(ui.GetChild(5).rectTransform().anchoredPosition3D.x), ui.GetChild(5).rectTransform().anchoredPosition3D.y, ui.GetChild(5).rectTransform().anchoredPosition3D.z);
			ui.GetChild(6).rectTransform().anchoredPosition3D = new Vector3(Mathf.Abs(ui.GetChild(6).rectTransform().anchoredPosition3D.x), ui.GetChild(6).rectTransform().anchoredPosition3D.y, ui.GetChild(6).rectTransform().anchoredPosition3D.z);
		}
		FPSController.handedness = mySettings.control_handedness;
		SettingValue(2, "Handedness").text = handednessText[mySettings.control_handedness];
		yAxisText[0] = "Regular";
		yAxisText[1] = "Inverted";
		FPSController.invertY = IntToBool(mySettings.control_yAxis);
		SettingValue(2, "Y-Axis").text = yAxisText[mySettings.control_yAxis];
		autoAimText[0] = "OFF";
		autoAimText[1] = "ON";
		FPSController.autoAim = IntToBool(mySettings.control_autoAim);
		SettingValue(2, "AutoAim").text = autoAimText[mySettings.control_autoAim];
		tapFiringText[0] = "OFF";
		tapFiringText[1] = "ON";
		FPSController.tapFiring = IntToBool(mySettings.control_tapFiring);
		SettingValue(2, "TapFiring").text = tapFiringText[mySettings.control_tapFiring];
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
		SettingValue(3, "Resolution").text = resolutionText[mySettings.vr_resolution];
		string eyeDistanceText = ((mySettings.vr_eyeDistance != 0) ? ("+" + (float)mySettings.vr_eyeDistance * 0.5f) : "Default");
		VRController.offset = (float)mySettings.vr_eyeDistance * 0.5f;
		SettingValue(3, "EyeDistance").text = eyeDistanceText;
		headRotationText[0] = "OFF";
		headRotationText[1] = "ON";
		SettingValue(3, "HeadRotation").text = headRotationText[mySettings.vr_headRotation];
		batteryText[0] = "OFF";
		batteryText[1] = "ON";
		Application.targetFrameRate = 60 - 30 * mySettings.extra_batterySaver;
		SettingValue(5, "BatterySaver").text = batteryText[mySettings.extra_batterySaver];
		notificationText[0] = "OFF";
		notificationText[1] = "ON";
		SettingValue(5, "Notification").text = notificationText[mySettings.extra_notification];
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
			if (TryParseTouchMapping(text, out float[] array))
			{
				for (int i = 1; i <= 4; i++)
				{
					mt.parent.GetChild(1).GetChild(i).GetComponent<ETCButton>()
						.anchorOffet = new Vector2(array[(i - 1) * 2], array[(i - 1) * 2 + 1]);
					if (array.Length > 8)
					{
						RectTransform button = mt.parent.GetChild(1).GetChild(i).rectTransform();
						button.localScale = new Vector3(array[7 + i], array[7 + i], button.localScale.z);
					}
				}
				// Older versions wrote the current culture's decimal separator; keep one format.
				string normalized = FormatTouchMapping(array);
				if (normalized != text)
				{
					FlatsPreferences.SetString("touchmapping", normalized);
					FlatsPreferences.Save();
				}
				Debug.Log("Loaded touch mapping:" + normalized);
			}
			else
			{
				Debug.LogWarning("Ignored an unreadable touch mapping: " + text);
			}
		}
		if (FlatsPreferences.HasKey("controllermapping") && Input.GetJoystickNames().Length > 0)
		{
			string text2 = FlatsPreferences.GetString("controllermapping");
			string[] array2 = text2.Split(new string[1] { "$" }, StringSplitOptions.None);
			if (array2.Length == 7 && Input.GetJoystickNames()[0] == array2[0])
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
				customControlEnabled = false;
			}
		}
		else
		{
			customControlEnabled = false;
		}
		leaderboardScreen.GetChild(0).GetChild(3)
			.GetComponent<Text>()
			.text = totalScore;
		if (Input.GetJoystickNames().Length > 0)
		{
			standaloneModule.submitButton = "Submit";
                standaloneModule.cancelButton = "Cancel";
                standaloneModule.enabled = false;
                inControlModule.enabled = true;
		}
		else
		{
			standaloneModule.enabled = true;
			inControlModule.enabled = false;
			EventSystem.current.SetSelectedGameObject(null);
		}
        // Original shared materials retained their theme across scenes. Restore
        // owned instances here, after Start has loaded the saved character.
        Color savedTheme = characterScreen.GetChild(1).GetChild(myCharacter.color).GetComponent<Image>().color;
        mainUI.color = MainThemeColor(savedTheme);
        selected.color = savedTheme;
		if (Application.loadedLevel == 0)
		{
			gameState = "Main";
			current = "Main";
			returningToMenu = skipTitle;
			if (!skipTitle)
			{
				backgroundRenderer.sharedMaterial.color = new Color(0f, 0f, 0f, 1f);
				mainUI.color = new Color(0.5f, 0.5f, 0.5f, mainUI.color.a);
				yield return new WaitForSeconds(0.2f);
				anim.SetBool("Title", true);
			}
			else
			{
				StartCoroutine("BackgroundColor", "SkippedTitle");
			}
			if (!Application.isMobilePlatform && Input.mousePresent)
			{
				Screen.lockCursor = false;
				UnityEngine.Cursor.visible = true;
			}
		}
		else
		{
			current = "Playing";
			anim.SetBool("Fade", false);
			if (!Application.isMobilePlatform && Input.mousePresent)
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
			roomTexts[i] = matchingScreen.GetChild(0).GetChild(i)
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
					gameObject.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(index)
						.GetComponent<Image>()
						.color;
				}
				else if (photonPlayer.GetTeam() == PunTeams.Team.red)
				{
					gameObject.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(9)
						.GetComponent<Image>()
						.color;
				}
				else
				{
					gameObject.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(7)
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
		if (!Application.isMobilePlatform && !VRmode && GetComponent<FlatsDesktopSettings>() == null)
			gameObject.AddComponent<FlatsDesktopSettings>().Initialize(settingsScreen.GetChild(3));
		if (gameState == "Main")
		{
			// The original starts the menu music after the 5 s title. Returning from a
			// match skips the title, so the music starts almost at once instead of
			// leaving the menu silent for another 5 s.
			yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(returningToMenu ? 0.5f : 5f));
			// Consume once per process, after saves, settings and UI have initialized.
			// Returning from Tutorial must remain at the normal main menu.
			if (!tutorialLaunchConsumed && Array.IndexOf(Environment.GetCommandLineArgs(), "-flats-tutorial") >= 0)
			{
				tutorialLaunchConsumed = true;
				network = 0;
				Singleplayer.rule = 4;
				gameState = "Singleplayer"; Singleplayer.ResetSharedMatchState();
				LoadOfflineScene("Tutorial");
				yield break;
			}
			ambient.GetComponent<AudioSource>().Play();
			mainMenuInitialized = true;
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
        if (TickBindingCapture()) return;
        TickLocalMatch();
		InputDevice activeDevice = InputManager.ActiveDevice;
        if (multiplayerConnecting && (Input.GetKeyUp(KeyCode.Escape) || activeDevice.CommandWasPressed ||
            activeDevice.Action2.WasPressed))
        {
            Fade(-1);
            return;
        }
		// B or Esc answers an open confirmation dialog with its cancel choice (or the
		// only button of an alert), as controller players expect.
		if (confirm.activeSelf && canOpen && (Input.GetKeyUp(KeyCode.Escape) || InputManager.Devices.Any(device => device.Action2.WasPressed)))
		{
			var dialog = confirm.GetComponent<ConfirmationDialogView>();
			Button choice = dialog.alert.gameObject.activeInHierarchy ? dialog.alert : dialog.negative;
			if (choice != null && choice.gameObject.activeInHierarchy && choice.IsInteractable())
			{
				choice.onClick.Invoke();
				return;
			}
		}
		if (current != "Modules" && !fliping && !backWithCancel && (Input.GetKeyUp(KeyCode.Escape) || activeDevice.CommandWasPressed || ((current != "Main" || pauseNavigation.IsOpen) && current != "Playing" && !TouchScreenKeyboard.visible && !Keyboard.isOpen && activeDevice.Action2.WasPressed)) && canOpen && !confirm.activeSelf && !update.activeSelf && (current == "Playing" || backButton.activeSelf || current == "Main" || (localMatchPanel != null && localMatchPanel.activeSelf)))
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
					SettingValue(1, "Anti-Aliasing").text = aaText[mySettings.graphics_aa];
				}
				if (mySettings.graphics_dof != 0)
				{
					mySettings.graphics_dof = 0;
					FPSController.dof = IntToBool(mySettings.graphics_dof);
					SettingValue(1, "DepthOfField").text = dofText[mySettings.graphics_dof];
				}
				if (mySettings.graphics_motionBlur != 0)
				{
					mySettings.graphics_motionBlur = 0;
					FPSController.motionBlur = IntToBool(mySettings.graphics_motionBlur);
					SettingValue(1, "MotionBlur").text = motionBlurText[mySettings.graphics_motionBlur];
				}
				if (mySettings.graphics_edgeRendering != 0)
				{
					mySettings.graphics_edgeRendering = 0;
					FPSController.edgeRendering = IntToBool(mySettings.graphics_edgeRendering);
					SettingValue(1, "EdgeRendering").text = edgeRenderingText[mySettings.graphics_edgeRendering];
				}
				if (mySettings.graphics_saturationFilter != 0)
				{
					mySettings.graphics_saturationFilter = 0;
					FPSController.saturationFilter = IntToBool(mySettings.graphics_saturationFilter);
					SettingValue(1, "SaturationFilter").text = saturationFilterText[mySettings.graphics_saturationFilter];
				}
				SaveDataController.Save();
				changedSettings = true;
				ShowConfirm("Extremely low framerate!", "All graphics settings have been disabled.", FramerateAlertIsChecked, "OK", null);
				framerateAlertIsEnabled = true;
			}
		}
		if (notification.gameObject.activeSelf && FlatsControls.PadState("Reload"))
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

	// Last main-menu tile that had focus, so controller focus comes back to it after a
	// dialog, a pointer click or a return from another page.
	private GameObject lastMainTile;
	// The Settings categories reuse the same tiles, so they are remembered separately.
	private GameObject lastSettingsTile;

	private void RememberMainTile()
	{
		var selected = EventSystem.current.currentSelectedGameObject;
		if (selected == null || buttons == null) return;
		bool main = current == "Main", settings = current == "Settings" && currentDetail == null;
		if (!main && !settings) return;
		foreach (Image tile in buttons)
		{
			if (tile != null && tile.transform.parent.gameObject == selected)
			{
				if (main) lastMainTile = selected; else lastSettingsTile = selected;
				return;
			}
		}
	}

	private GameObject FirstVisibleTile()
	{
		if (buttons == null) return null;
		foreach (Image tile in buttons)
		{
			if (tile == null) continue;
			var selectable = tile.transform.parent.GetComponent<Selectable>();
			if (selectable != null && selectable.gameObject.activeInHierarchy && selectable.IsInteractable()) return selectable.gameObject;
		}
		return null;
	}

	private GameObject MainTileToRestore()
	{
		if (lastMainTile != null)
		{
			// While the menu returns, the tiles are briefly hidden or not interactable;
			// wait for the remembered tile instead of settling on the first one.
			var selectable = lastMainTile.GetComponent<Selectable>();
			return lastMainTile.activeInHierarchy && selectable != null && selectable.IsInteractable() ? lastMainTile : null;
		}
		var first = buttons[0].transform.parent.gameObject;
		return first.activeInHierarchy ? first : null;
	}

	private void FramerateAlertIsChecked(bool result)
	{
		framerateAlertIsEnabled = false;
	}

	private void LateUpdate()
	{
        RefreshControlTile();
        if (FlatsControls.Capturing || releasePending || Time.frameCount <= suppressControlFrame) return;
        RefreshLanguageButton();
        CheckMultiplayerDeadline();
		if ((current == "Playing" && !framerateAlertIsEnabled) || VRmode || standaloneModule == null || inControlModule == null)
		{
			return;
		}
		RememberMainTile();
		// Pick the input module from the device in use every frame, not only when focus is
		// empty: a controller press restores focus first, which used to leave the pointer
		// module active for controller navigation.
		bool controllerNavigation = current != "Playing" && Input.GetJoystickNames().Length > 0 && !PointerFocusPolicy.PointerActive;
		if (current != "Playing" && inControlModule.enabled != controllerNavigation)
		{
			standaloneModule.submitButton = "Submit";
			standaloneModule.cancelButton = "Cancel";
			standaloneModule.enabled = !controllerNavigation;
			inControlModule.enabled = controllerNavigation;
		}
		// The pointer module handles the mouse; a resting cursor must not add a second,
		// hover highlight next to controller focus.
		inControlModule.allowMouseInput = false;
		if (current != "Playing" && (EventSystem.current.currentSelectedGameObject == null || !EventSystem.current.currentSelectedGameObject.activeInHierarchy))
		{
			if (controllerNavigation)
			{
				if (errorMessage.activeSelf)
				{
					Selectable component = errorMessage.transform.GetChild(2).GetComponent<Selectable>();
					component.Select();
				}
				else if (confirm.activeSelf)
				{
					Selectable selectable = (confirm.GetComponent<ConfirmationDialogView>().alert.gameObject.activeSelf ? confirm.GetComponent<ConfirmationDialogView>().alert : confirm.GetComponent<ConfirmationDialogView>().negative);
					selectable.Select();
				}
				else if (roomCreation.activeSelf)
				{
					Selectable component2 = roomCreation.transform.GetChild(3).GetComponent<Selectable>();
					component2.Select();
				}
				else if (current == "Main" || (current == "Map" && !voted))
				{
					// Return to the tile the player was on, not always the first one.
					EventSystem.current.SetSelectedGameObject(MainTileToRestore());
				}
				else if (current == "Settings" && currentDetail == null && lastSettingsTile != null && lastSettingsTile.activeInHierarchy && lastSettingsTile.GetComponent<Selectable>().IsInteractable())
				{
					// Back from a Settings page returns to that page's category.
					EventSystem.current.SetSelectedGameObject(lastSettingsTile);
				}
				else if (FirstVisibleTile() != null)
				{
					// Pages built from the menu tiles (Play, Singleplayer and so on) start on
					// their first tile, not on Back, so A does not leave the page.
					EventSystem.current.SetSelectedGameObject(FirstVisibleTile());
				}
				else if (backButton.activeSelf && current != "Play" && current != "Singleplayer" && current != "Multiplayer" && !(current == "Settings" && currentDetail == null))
				{
					// Tile pages wait for their tiles to appear rather than settling on Back.
					EventSystem.current.SetSelectedGameObject(backButton);
				}
			}
		}
		else if (framerateAlertIsEnabled)
		{
			Selectable selectable2 = (confirm.GetComponent<ConfirmationDialogView>().alert.gameObject.activeSelf ? confirm.GetComponent<ConfirmationDialogView>().alert : confirm.GetComponent<ConfirmationDialogView>().negative);
			selectable2.Select();
		}
		if (update.activeSelf)
		{
			EventSystem.current.firstSelectedGameObject = update.transform.GetChild(5).gameObject;
			EventSystem.current.SetSelectedGameObject(update.transform.GetChild(5).gameObject);
			EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>().Select();
		}
	}

    private void ApplyListenerVolume()
    {
        AudioListener.volume = Flats.Core.PauseNavigationSession.ListenerVolume(mySettings.sound_all / 10f, current == "Playing");
    }

	public void PlayMenuSound(AudioClip clip)
	{
		if (clip != null) GetComponent<AudioSource>().PlayOneShot(clip, 3f);
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
            { "I", FlatsUserIcon.Read(defaultIcon) }
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

		// Touch layout: four button offsets (x, y) and, since a later version, four scales.
		public static bool TryParseTouchMapping(string text, out float[] values)
		{
			values = null;
			string[] parts = (text ?? "").Split('$');
			if (parts.Length != 8 && parts.Length != 12)
			{
				return false;
			}
			var parsed = new float[parts.Length];
			for (int i = 0; i < parts.Length; i++)
			{
				if (!float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed[i]) &&
					!float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out parsed[i]))
				{
					return false;
				}
				if (float.IsNaN(parsed[i]) || float.IsInfinity(parsed[i]))
				{
					return false;
				}
			}
			values = parsed;
			return true;
		}

		public static string FormatTouchMapping(float[] values)
		{
			return string.Join("$", values.Select(value => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
		}

		[PunRPC]
		private void StartNow()
		{
            if (readyStarted || !PhotonNetwork.inRoom) return;
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
                ResetMatchReadiness();
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

        private bool readyStarted;
        private int readyOperation;

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

        private Color MainThemeColor(Color color)
        {
            return new Color(color.r * .5f + .25f, color.g * .5f + .25f, color.b * .5f + .25f, mainUI.color.a);
        }
		public IEnumerator BackgroundColor(string command)
		{
			float alpha = 0.2f;
			Color backgroundThemeColor = characterScreen.GetChild(1).GetChild(myCharacter.color)
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
				backgroundRenderer.sharedMaterial.color = new Color(0f, 0f, 0f, 1f);
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
					float a7 = Mathf.MoveTowards(backgroundRenderer.sharedMaterial.color.a, 1f, maxDelta5);
					backgroundRenderer.sharedMaterial.color = new Color(0f, 0f, 0f, a7);
					if (a7 == 1f)
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

		private void EnableVR(bool result)
		{
			if (result)
			{
				VRmode = true;
				FPSController.enableCamRotate = true;
				GetComponent<FlatsDesktopSettings>()?.ShowDesktopRows(false);
				return;
			}
			VRmode = false;
			GetComponent<FlatsDesktopSettings>()?.ShowDesktopRows(true);
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
            Confirmation.ShowConfirm(backControl.GetComponent<Image>().color, title, message, action, positiveBtnText, negativeBtnText);
        }
        public void OnClickedConfirm() { Confirmation.OnClickedConfirm(); }

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
