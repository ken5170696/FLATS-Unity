using System;
using UnityEngine;
[AddComponentMenu("Photon Networking/Photon Rigidbody View")]
[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Rigidbody))]
public class PhotonRigidbodyView : MonoBehaviour, IPunObservable
{
	[SerializeField]
	protected bool m_SynchronizeVelocity;

	[SerializeField]
	protected bool m_SynchronizeAngularVelocity;

	private Rigidbody m_Body;

	private void Awake()
	{
		m_Body = GetComponent<Rigidbody>();
	}

	public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
	{
		if (stream.isWriting)
		{
			if (m_SynchronizeVelocity)
			{
				stream.SendNext(m_Body.linearVelocity);
			}
			if (m_SynchronizeAngularVelocity)
			{
				stream.SendNext(m_Body.angularVelocity);
			}
		}
		else
		{
			if (m_SynchronizeVelocity)
			{
				m_Body.linearVelocity = (Vector3)stream.ReceiveNext();
			}
			if (m_SynchronizeAngularVelocity)
			{
				m_Body.angularVelocity = (Vector3)stream.ReceiveNext();
			}
		}
	}

	public PhotonRigidbodyView()
	{
		m_SynchronizeVelocity = true;
		m_SynchronizeAngularVelocity = true;

	}




}
