using System;
using Photon;
using UnityEngine;
[RequireComponent(typeof(PhotonView))]
public class HighlightOwnedGameObj : Photon.MonoBehaviour
{
	public GameObject PointerPrefab;

	public float Offset;

	private Transform markerTransform;

	private void Update()
	{
		if (base.photonView.isMine)
		{
			if (markerTransform == null)
			{
				GameObject gameObject = (GameObject)UnityEngine.Object.Instantiate(PointerPrefab);
				gameObject.transform.parent = base.gameObject.transform;
				markerTransform = gameObject.transform;
			}
			Vector3 position = base.gameObject.transform.position;
			markerTransform.position = new Vector3(position.x, position.y + Offset, position.z);
			markerTransform.rotation = Quaternion.identity;
		}
		else if (markerTransform != null)
		{
			UnityEngine.Object.Destroy(markerTransform.gameObject);
			markerTransform = null;
		}
	}

	public HighlightOwnedGameObj()
	{
		Offset = 0.5f;

	}




}
