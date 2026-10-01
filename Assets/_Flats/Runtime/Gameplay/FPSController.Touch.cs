using System;
using System.Collections;
using InControl;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Mobile touch gestures (EasyTouch).
public partial class FPSController
{
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
		// Roguelike: Fire never becomes a melee swing near an object (X5); melee has its own input.
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
		else if (enableFire && RogueAllows(RogueAction.Fire))
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
		if (ETCInput.GetButton("Fire") || ETCInput.GetButton("Reload") || ETCInput.GetButton("Jump") || ETCInput.GetButton("Zoom") || (interactButton != null && ETCInput.GetButton("Interact")))
		{
			return;
		}
		if (grabbedObject == null && enableFire && !isZoom && !grabbing && droppedGun == null && tapFiring && RogueAllows(RogueAction.Grenade))
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
			// the one exchange path (action rule, server-ordered request, the owner's own ammo count; QA-14, QA-26)
			TryExchangeGroundWeapon();
		}
	}
}
