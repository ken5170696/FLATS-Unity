using System;
using UnityEngine;
using Vectrosity;
[AddComponentMenu("Vectrosity/VisibilityControl")]
public class VisibilityControl : MonoBehaviour
{
	private RefInt m_objectNumber;

	private VectorLine m_vectorLine;

	private bool m_destroyed;

	public RefInt objectNumber
	{
		get
		{
			return m_objectNumber;
		}
	}

	public VisibilityControl()
	{
		m_destroyed = false;

	}

	public void Setup(VectorLine line, bool makeBounds)
	{
		if (makeBounds)
		{
			VectorManager.SetupBoundsMesh(base.gameObject, line);
		}
		VectorManager.VisibilitySetup(base.transform, line, out m_objectNumber);
		m_vectorLine = line;
	}

	private void OnBecameVisible()
	{
		m_vectorLine.active = true;
		VectorManager.DrawArrayLine2(m_objectNumber.i);
	}

	private void OnBecameInvisible()
	{
		m_vectorLine.active = false;
	}

	private void OnDestroy()
	{
		if (!m_destroyed)
		{
			m_destroyed = true;
			VectorManager.VisibilityRemove(m_objectNumber.i);
			VectorLine.Destroy(ref m_vectorLine);
		}
	}




}
