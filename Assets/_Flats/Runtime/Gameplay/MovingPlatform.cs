using System;
using UnityEngine;
public class MovingPlatform : MonoBehaviour
{
	private Transform mt;

	private void Awake()
	{
		mt = base.transform;
	}

	private void OnTriggerEnter(Collider col)
	{
		if (col.gameObject.tag == "Player")
		{
			if (Menu.network == 0)
			{
				col.transform.SetParent(mt.parent);
			}
			else if (Menu.network != 1 && col.gameObject.GetPhotonView().isMine)
			{
				int[] array = new int[2]
				{
					0,
					col.gameObject.GetPhotonView().viewID
				};
				base.gameObject.GetPhotonView().RPC("NetworkParent", PhotonTargets.All, array);
			}
		}
	}

	private void OnTriggerExit(Collider col)
	{
		if (col.gameObject.tag == "Player")
		{
			if (Menu.network == 0)
			{
				col.transform.SetParent(null);
			}
			else if (Menu.network != 1 && col.gameObject.GetPhotonView().isMine)
			{
				int[] array = new int[2]
				{
					1,
					col.gameObject.GetPhotonView().viewID
				};
				base.gameObject.GetPhotonView().RPC("NetworkParent", PhotonTargets.All, array);
			}
		}
	}

	[PunRPC]
	private void NetworkParent(int[] receivedData)
	{
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1)
		{
			if (receivedData[0] == 0)
			{
				PhotonView.Find(receivedData[1]).transform.SetParent(mt.parent);
			}
			else if (receivedData[0] == 1)
			{
				PhotonView.Find(receivedData[1]).transform.SetParent(null);
			}
		}
	}

	public MovingPlatform()
	{
	}




}
