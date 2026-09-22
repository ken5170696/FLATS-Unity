using System;
using System.Collections;
using UnityEngine;
public class AutoAim : MonoBehaviour
{
	public LayerMask enemyLayer;

	public LayerMask block;

	private Transform mt;

	private Transform ct;

	private FPSController fc;

	private bool aiming;

	private float range;

	private float waitTime;

	private IEnumerator Start()
	{
		if (Menu.network == 1)
		{
			yield break;
		}
		if (Menu.network == 2)
		{
			if (!base.gameObject.GetPhotonView().isMine)
			{
				UnityEngine.Object.Destroy(this);
			}
			yield break;
		}
		mt = base.transform;
		ct = Camera.main.transform;
		fc = GetComponent<FPSController>();
		block = 1;
		if (base.gameObject.layer == 8)
		{
			enemyLayer = 512;
		}
		else if (base.gameObject.layer == 9)
		{
			enemyLayer = 256;
		}
		while (true)
		{
			if (Input.mousePresent)
			{
				waitTime = 0.05f;
			}
			else if (Input.GetJoystickNames().Length > 0)
			{
				waitTime = 0.08f;
			}
			else
			{
				waitTime = 0.1f;
			}
			if (ct != null && FPSController.autoAim && FPSController.enableControl && !Menu.VRmode && !fc.isZoom)
			{
				RaycastHit hitInfo = default(RaycastHit);
				if (Physics.Raycast(ct.position, ct.forward, out hitInfo, range, enemyLayer) && !Physics.Linecast(ct.position, hitInfo.collider.transform.position, block) && hitInfo.collider.gameObject.layer != 0 && !aiming)
				{
					aiming = true;
					StartCoroutine("Aim", hitInfo.collider.transform);
				}
			}
			yield return new WaitForSeconds(0f);
		}
	}

	private IEnumerator Aim(Transform target)
	{
		FPSController.enableCamRotate = false;
		Invoke("Stop", waitTime);
		while (aiming && FPSController.enableControl && !(target == null) && ct.GetComponent<Camera>().fieldOfView == 60f)
		{
			Quaternion rot = Quaternion.LookRotation(target.position - ct.position);
			rot.x = 0f;
			rot.z = 0f;
			mt.rotation = Quaternion.Slerp(mt.rotation, rot, Time.deltaTime * waitTime * 100f);
			yield return new WaitForSeconds(0f);
		}
	}

	private void Stop()
	{
		aiming = false;
		if (Menu.canOpen)
		{
			FPSController.enableCamRotate = true;
		}
	}

	public AutoAim()
	{
		range = 150f;

	}




}
