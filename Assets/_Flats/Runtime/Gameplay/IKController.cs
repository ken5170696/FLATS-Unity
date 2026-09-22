using System;
using UnityEngine;
[RequireComponent(typeof(Animator))]
public class IKController : MonoBehaviour
{
	protected Animator animator;

	public bool leftIK;

	public Transform leftHandObj;

	public float degree;

	private Transform mt;

	private Transform chest;

	private Transform ct;

	private Transform ctt;

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
			chest.RotateAround(chest.position, ct.right, degree);
			ct.position = Vector3.Lerp(ct.position, ctt.position, 1f);
		}
		else
		{
			ct.localEulerAngles = new Vector3(degree, 0f, 0f);
			chest.RotateAround(chest.position, ct.right, degree);
			ct.position = Vector3.Lerp(ct.position, ctt.position, 1f);
		}
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
