using System;
using UnityEngine;
[RequireComponent(typeof(Animator))]
public class IKController : MonoBehaviour
{
	protected Animator animator;

	public bool leftIK;

	public Transform leftHandObj;

	public float degree;

	[Tooltip("Look pitch (degrees) taken by the chest bone alone, as before full vertical look. Pitch beyond it is shared by the lower and upper spine so the arms and weapon still follow the camera without folding one joint.")]
	public float chestPitchLimit = 50f;

	[Range(0f, 1f)]
	[Tooltip("Share of the pitch beyond chestPitchLimit taken by the lower spine; the upper spine takes the rest.")]
	public float lowerSpineShare = 0.5f;

	[Tooltip("Seconds for a remote copy's shown body pitch to follow the replicated pitch (network steps are smoothed; aim uses the raw value).")]
	public float remotePitchSmoothing = 0.08f;

	private Transform mt;

	private Transform chest;

	private Transform lowerSpine;

	private Transform upperSpine;

	private Transform ct;

	private Transform ctt;

	private float shownPitch;

	private bool hasShownPitch;

	private RoguePlayer roguePlayer;

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

	private void OnAnimatorIK()
	{
		if (!animator)
		{
			return;
		}
		if (leftIK)
		{
			if (leftHandObj != null)
			{
				animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1f);
				animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1f);
				animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandObj.position);
				animator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandObj.rotation);
			}
		}
		else
		{
			animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
			animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
		}
	}

	private void Start()
	{
		mt = base.transform;
		animator = GetComponent<Animator>();
		chest = mt.Find("Armature/mixamorig_Hips/mixamorig_Spine/mixamorig_Spine1").transform;
		ctt = chest.GetChild(0).Find("CameraTarget").transform;
		lowerSpine = chest.parent;
		upperSpine = ctt.parent;
		// Roguelike adds RoguePlayer in FPSController.Awake, before this Start; Classic players and enemies have none.
		roguePlayer = GetComponent<RoguePlayer>();
		if ((bool)GetComponent<FPSController>())
		{
			ct = GetComponent<FPSController>().myCamera.transform;
		}
		else
		{
			ct = mt.Find("Camera").transform;
		}
	}

	private void LateUpdate()
	{
		if (MyView(base.gameObject))
		{
			degree = ct.localEulerAngles.x;
			PoseSpine(Flats.Core.LookRotationPolicy.SignedPitch(degree));
			ct.position = Vector3.Lerp(ct.position, ctt.position, 1f);
		}
		else
		{
			// The camera (the aim and the shot direction of this copy) keeps the replicated pitch as it arrives; only the shown
			// body pitch is smoothed between network steps, through the shorter way round 0/360.
			ct.localEulerAngles = new Vector3(degree, 0f, 0f);
			float target = Flats.Core.LookRotationPolicy.SignedPitch(degree);
			if (!hasShownPitch || remotePitchSmoothing <= 0f)
			{
				shownPitch = target;
				hasShownPitch = true;
			}
			else
			{
				shownPitch = Mathf.LerpAngle(shownPitch, target, 1f - Mathf.Exp(-Time.deltaTime / remotePitchSmoothing));
			}
			PoseSpine(Flats.Core.LookRotationPolicy.SignedPitch(shownPitch));
			ct.position = Vector3.Lerp(ct.position, ctt.position, 1f);
		}
	}

	// Full vertical look (QA-07). Up to chestPitchLimit the chest turns alone, exactly as before. Beyond it the lower spine and the
	// upper spine share the rest, about the same camera axis, so the upper body (arms, weapon, camera target) still turns by the
	// whole pitch: the weapon stays on the camera's line while no single joint folds past its old limit. A downed player keeps
	// the old chest-only pose (the prone body already leans).
	private void PoseSpine(float pitch)
	{
		Vector3 axis = ct.right;
		float core = Mathf.Clamp(pitch, 0f - chestPitchLimit, chestPitchLimit);
		float extra = pitch - core;
		if (extra == 0f || lowerSpine == null || upperSpine == null || upperSpine == chest)
		{
			chest.RotateAround(chest.position, axis, pitch);
			return;
		}
		if (roguePlayer != null && roguePlayer.Downed)
		{
			chest.RotateAround(chest.position, axis, core);
			return;
		}
		float lower = extra * lowerSpineShare;
		lowerSpine.RotateAround(lowerSpine.position, axis, lower);
		chest.RotateAround(chest.position, axis, core);
		upperSpine.RotateAround(upperSpine.position, axis, extra - lower);
	}

	private void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
	{
		if (Menu.network == 2)
		{
			if (stream.isWriting)
			{
				stream.SendNext(degree);
			}
			else
			{
				degree = (float)stream.ReceiveNext();
			}
		}
	}

	public IKController()
	{
	}




}
