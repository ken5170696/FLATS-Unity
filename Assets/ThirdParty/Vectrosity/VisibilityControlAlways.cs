using System;
using UnityEngine;
using Vectrosity;
[AddComponentMenu("Vectrosity/VisibilityControlAlways")]
public class VisibilityControlAlways : MonoBehaviour
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

	public VisibilityControlAlways()
	{
		m_destroyed = false;

	}

	public void Setup(VectorLine line)
	{
		VectorManager.VisibilitySetup(base.transform, line, out m_objectNumber);
		VectorManager.DrawArrayLine2(m_objectNumber.i);
		m_vectorLine = line;
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
