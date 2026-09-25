using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Vectrosity;
public class Supershot : MonoBehaviour
{
	public AnimationClip supershotAnimation;

	public bool headshot;

	public bool ally;

	public bool vip;

	public int vipLayer;

	private int ram;

	private float animLength;

	private bool stopAnim;

	private Canvas ui;

	private Canvas sight;

	private Transform mt;

	private Transform myCanvas;

	private Transform player;

	private Text myText;

	private Animator anim;

	private float slowFactor;

	private float newTimeScale;

	private float speed;

	private LayerMask savedLayerMask;

	// Awake runs inside Instantiate, so PlayKill sets the mode before creating the effect.
	private static bool nextNoticeOnly;

	// Kill cinematic turned off: only the text notice plays. Time, cameras, input and
	// the pause menu are untouched.
	private bool noticeOnly;

	// Only the latest notice stays on screen during quick successive kills.
	private static Supershot activeNotice;

	// Headshot and mortal-shot kills honour the player's Kill Cinematic setting.
	public static void PlayKill(UnityEngine.Object prefab, Transform view, Transform target, bool headshot)
	{
		nextNoticeOnly = !FlatsControls.KillCinematic;
		GameObject effect;
		try
		{
			effect = UnityEngine.Object.Instantiate(prefab, view.position, view.rotation) as GameObject;
		}
		finally
		{
			nextNoticeOnly = false;
		}
		Supershot component = effect.GetComponent<Supershot>();
		component.headshot = headshot;
		component.StartCoroutine("StartEffect", target);
	}

	private void Awake()
	{
		if (!Menu.canOpen)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		noticeOnly = nextNoticeOnly;
		if (!noticeOnly)
		{
			Menu.canOpen = false;
		}
		speed = Time.deltaTime;
		mt = base.transform;
		mt.position = Camera.main.transform.position;
		myCanvas = mt.GetChild(0);
		myText = myCanvas.GetChild(0).GetComponent<Text>();
		anim = myCanvas.GetComponent<Animator>();
		player = Camera.main.transform.root;
		Transform transform = GameObject.Find("UICamera").transform;
		ui = transform.GetChild(1).GetComponent<Canvas>();
		sight = transform.GetChild(3).GetComponent<Canvas>();
		myCanvas.GetComponent<Canvas>().worldCamera = transform.GetComponent<Camera>();
		myCanvas.position = transform.position;
		myCanvas.rotation = transform.rotation;
		if (noticeOnly)
		{
			GetComponent<Camera>().enabled = false;
			Singleplayer.chance = true;
			return;
		}
		Camera.main.GetComponent<FXAA>().enabled = false;
		Camera.main.GetComponent<AmplifyMotionEffect>().enabled = false;
		Camera.main.GetComponent<FxPro>().enabled = false;
		Camera.main.GetComponent<EdgeDetectEffectNormals>().enabled = false;
		Camera.main.GetComponent<CC_Grayscale>().enabled = false;
		GetComponent<FxPro>().enabled = FPSController.dof;
		GetComponent<EdgeDetectEffectNormals>().enabled = FPSController.edgeRendering;
		GetComponent<CC_Grayscale>().enabled = FPSController.saturationFilter;
		Singleplayer.chance = true;
	}

	private void Start()
	{
	}

	public IEnumerator StartEffect(Transform target)
	{
		if (noticeOnly && (bool)ui)
		{
			// The ragdoll knock-back belongs to the kill, not to the camera.
			target.LookAt(player);
			target.GetChild(3).GetChild(0).GetComponent<Rigidbody>().AddForce(target.forward * -UnityEngine.Random.Range(10000, 20000) - target.up * 10000f);
			if (activeNotice != null && activeNotice != this)
			{
				activeNotice.EndEffect();
			}
			activeNotice = this;
			// The HUD stays visible, so the notice draws above it.
			myCanvas.GetComponent<Canvas>().sortingOrder = ui.sortingOrder + 1;
			myText.text = KillText();
			myCanvas.SetParent(null);
			myCanvas.position -= myCanvas.right * 5f;
			anim.Play("Supershot");
			yield break;
		}
		if ((bool)ui)
		{
			if (VRController.device == "cardboard")
			{
				savedLayerMask = Camera.main.cullingMask;
			}
			else if (VRController.device == "oculus")
			{
				savedLayerMask = Camera.main.transform.GetChild(1).GetChild(0).GetComponent<Camera>().cullingMask;
			}
			newTimeScale = Time.timeScale / slowFactor;
			FPSController.enableCamRotate = false;
			DamageReceiver.invincibility = true;
			GetComponent<FxPro>().enabled = FPSController.dof;
			if (FPSController.dof)
			{
				GetComponent<FxPro>().DOFParams.Target = target;
			}
			target.LookAt(player);
			target.GetChild(3).GetChild(0).GetComponent<Rigidbody>().AddForce(target.forward * -UnityEngine.Random.Range(10000, 20000) - target.up * 10000f);
			if (Input.GetJoystickNames().Length == 0 && !Input.mousePresent)
			{
				ETCInput.SetControlActivated("Joystick", false);
				ETCInput.SetControlVisible("Reload", false);
				ETCInput.SetControlActivated("Reload", false);
				ETCInput.SetControlVisible("Jump", false);
				ETCInput.SetControlActivated("Jump", false);
				ETCInput.SetControlVisible("Fire", false);
				ETCInput.SetControlActivated("Fire", false);
				ETCInput.SetControlVisible("Zoom", false);
				ETCInput.SetControlActivated("Zoom", false);
				ETCInput.ResetAxis("Horizontal");
				ETCInput.ResetAxis("Vertical");
			}
			ui.enabled = false;
			sight.enabled = false;
			Camera.main.clearFlags = CameraClearFlags.Depth;
			base.GetComponent<Camera>().clearFlags = CameraClearFlags.Skybox;
			if (VRController.device == "cardboard")
			{
				Camera.main.cullingMask = 0;
				if (!Menu.VRmode)
				{
				}
			}
			else
			{
				bool flag = VRController.device == "oculus";
			}
			Camera.main.transform.GetChild(0).gameObject.SetActive(false);
			RaycastHit ray = default(RaycastHit);
			float targetToRight = 20f;
			float targetToLeft = 20f;
			if (Physics.Raycast(target.position + target.right, target.right, out ray, 100f))
			{
				targetToRight = Vector3.Distance(target.position, ray.point);
			}
			if (Physics.Raycast(target.position - target.right, -target.right, out ray, 100f))
			{
				targetToLeft = Vector3.Distance(target.position, ray.point);
			}
			if (targetToRight > targetToLeft)
			{
				ram = 0;
			}
			else if (targetToRight < targetToLeft)
			{
				ram = 1;
			}
			else
			{
				ram = UnityEngine.Random.Range(0, 2);
			}
			yield return new WaitForSeconds(0.15f);
			Time.timeScale = newTimeScale;
			Time.fixedDeltaTime /= slowFactor;
			Time.maximumDeltaTime /= slowFactor;
			myText.text = KillText();
			myCanvas.SetParent(null);
			if (ally)
			{
				mt.LookAt(target);
			}
			if (ram == 0)
			{
				myCanvas.position -= myCanvas.right * 5f;
			}
			else
			{
				myCanvas.position += myCanvas.right * 5f;
			}
			anim.Play("Supershot");
			while (true)
			{
				if (ram == 0)
				{
					mt.position = Vector3.Lerp(mt.position, target.TransformPoint(Vector3.up + Vector3.right), 6f * speed);
				}
				else
				{
					mt.position = Vector3.Lerp(mt.position, target.TransformPoint(Vector3.up + Vector3.left), 6f * speed);
				}
				if (!(Vector3.Distance(mt.position, target.position) < 12f))
				{
					yield return new WaitForSeconds(0f);
					continue;
				}
				break;
			}
		}
		else
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	private string KillText()
	{
		if (Multiplayer.rule == 7)
		{
			return vipLayer == LayerMask.NameToLayer("RedTeam") ? "Blue team killed VIP!" : "Red team killed VIP!";
		}
		if (vip)
		{
			return "VIP was killed!";
		}
		if (ally)
		{
			return "Team kill...";
		}
		if (headshot && Singleplayer.rule == 2)
		{
			return "Headshot " + Singleplayer.headshotChain + "x";
		}
		if (headshot && Singleplayer.enemy != 0)
		{
			return "Headshot";
		}
		return "Mortalshot";
	}

	public void EndEffect()
	{
		if (noticeOnly)
		{
			if (activeNotice == this)
			{
				activeNotice = null;
			}
			stopAnim = true;
			if (myCanvas != null)
			{
				UnityEngine.Object.Destroy(myCanvas.gameObject);
			}
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		GetComponent<FxPro>().enabled = false;
		Time.timeScale = 1f;
		Time.fixedDeltaTime *= slowFactor;
		Time.maximumDeltaTime *= slowFactor;
		base.GetComponent<Camera>().clearFlags = CameraClearFlags.Depth;
		Camera.main.clearFlags = CameraClearFlags.Skybox;
		if (VRController.device == "cardboard")
		{
			Camera.main.cullingMask = savedLayerMask;
			if (!Menu.VRmode)
			{
			}
		}
		else
		{
			bool flag = VRController.device == "oculus";
		}
		Camera.main.transform.GetChild(0).gameObject.SetActive(true);
		DamageReceiver.invincibility = false;
		Menu.canOpen = true;
		if (Input.GetJoystickNames().Length == 0 && !Input.mousePresent)
		{
			ETCInput.SetControlActivated("Joystick", true);
			ETCInput.SetControlVisible("Reload", true);
			ETCInput.SetControlActivated("Reload", true);
			ETCInput.SetControlVisible("Jump", true);
			ETCInput.SetControlActivated("Jump", true);
			ETCInput.SetControlVisible("Fire", true);
			ETCInput.SetControlActivated("Fire", true);
			ETCInput.SetControlVisible("Zoom", true);
			ETCInput.SetControlActivated("Zoom", true);
		}
		FPSController.enableCamRotate = true;
		ui.enabled = true;
		sight.enabled = true;
		if (vip)
		{
			Singleplayer.cleared = true;
		}
		Camera.main.GetComponent<FxPro>().enabled = FPSController.dof;
		Camera.main.GetComponent<AmplifyMotionEffect>().enabled = FPSController.motionBlur;
		Camera.main.GetComponent<FXAA>().enabled = FPSController.aa;
		Camera.main.GetComponent<EdgeDetectEffectNormals>().enabled = FPSController.edgeRendering;
		Camera.main.GetComponent<CC_Grayscale>().enabled = FPSController.saturationFilter;
		UnityEngine.Object.Destroy(myCanvas.gameObject);
		UnityEngine.Object.Destroy(base.gameObject);
	}

	private void LateUpdate()
	{
		if (!noticeOnly)
		{
			VectorLine.SetCamera3D(base.gameObject.GetComponent<Camera>());
		}
		if (stopAnim)
		{
			return;
		}
		animLength += Time.unscaledDeltaTime;
		if (animLength >= supershotAnimation.length * 8.75f / 10f)
		{
			if (ram == 0)
			{
				myCanvas.position -= myCanvas.right * 500f * Time.unscaledDeltaTime;
			}
			else
			{
				myCanvas.position += myCanvas.right * 500f * Time.unscaledDeltaTime;
			}
		}
		if (animLength >= supershotAnimation.length)
		{
			EndEffect();
			stopAnim = true;
		}
	}

	public Supershot()
	{
		slowFactor = 16f;
		newTimeScale = 0.0625f;

	}




}
