using System;
using System.Collections;
using UnityEngine;
public class SceneGun : MonoBehaviour
{
	public int index;

	private Transform mt;

	private GameObject dropped;

	private int gunIndex;

	private int count;

	private IEnumerator Start()
	{
		mt = base.transform;
		if (index == -1)
		{
			gunIndex = UnityEngine.Random.Range(0, 15);
		}
		else
		{
			gunIndex = index;
		}
		if (Menu.network == 0)
		{
			dropped = UnityEngine.Object.Instantiate(Resources.Load("Weapons/Weapon" + gunIndex), mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity) as GameObject;
			DroppedGun component = dropped.GetComponent<DroppedGun>();
			component.dontDestroy = true;
			component.currentAmmo = GunInfo.limitAmmo[gunIndex];
			component.maxAmmo = GunInfo.limitMaxAmmo[gunIndex];
			component.sight = UnityEngine.Random.Range(0, GunInfo.zoom[gunIndex] + 1);
		}
		else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
		{
			dropped = PhotonNetwork.InstantiateSceneObject("Weapons/Weapon" + gunIndex, mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity, 0, null);
			dropped.GetPhotonView().RPC("DropData", PhotonTargets.AllBuffered, -1, -1, UnityEngine.Random.Range(0, GunInfo.zoom[gunIndex] + 1));
		}
		while (true)
		{
			if (Menu.isMaster() && dropped == null)
			{
				count++;
				if (count >= 30)
				{
					if (index == -1)
					{
						gunIndex = UnityEngine.Random.Range(0, 15);
					}
					if (Menu.network == 0)
					{
						dropped = UnityEngine.Object.Instantiate(Resources.Load("Weapons/Weapon" + gunIndex), mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity) as GameObject;
						DroppedGun component2 = dropped.GetComponent<DroppedGun>();
						component2.dontDestroy = true;
						component2.currentAmmo = GunInfo.limitAmmo[gunIndex];
						component2.maxAmmo = GunInfo.limitMaxAmmo[gunIndex];
						component2.sight = UnityEngine.Random.Range(0, GunInfo.zoom[gunIndex] + 1);
					}
					else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
					{
						dropped = PhotonNetwork.InstantiateSceneObject("Weapons/Weapon" + gunIndex, mt.position + Vector3.up * 3f + Vector3.forward * 2f, Quaternion.identity, 0, null);
						dropped.GetPhotonView().RPC("DropData", PhotonTargets.All, -1, -1, UnityEngine.Random.Range(0, GunInfo.zoom[gunIndex] + 1));
						dropped.GetComponent<DroppedGun>().dontDestroy = true;
					}
					count = 0;
				}
			}
			yield return new WaitForSeconds(1f);
		}
	}

	public SceneGun()
	{
		index = -1;

	}




}
