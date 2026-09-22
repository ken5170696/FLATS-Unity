using System;
using UnityEngine;
[AddComponentMenu("Photon Networking/Photon Rigidbody 2D View")]
[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Rigidbody2D))]
public class PhotonRigidbody2DView : MonoBehaviour, IPunObservable
{
	[SerializeField]
	protected bool m_SynchronizeVelocity;

	[SerializeField]
	protected bool m_SynchronizeAngularVelocity;

	private Rigidbody2D m_Body;

	private void Awake()
	{
		m_Body = GetComponent<Rigidbody2D>();
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
				m_Body.linearVelocity = (Vector2)stream.ReceiveNext();
			}
			if (m_SynchronizeAngularVelocity)
			{
				m_Body.angularVelocity = (float)stream.ReceiveNext();
			}
		}
	}

	public PhotonRigidbody2DView()
	{
		m_SynchronizeVelocity = true;
		m_SynchronizeAngularVelocity = true;

	}




}
