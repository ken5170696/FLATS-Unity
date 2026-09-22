using System;
using System.Collections;
using UnityEngine;
using Vectrosity;
[AddComponentMenu("Vectrosity/VisibilityControlStatic")]
public class VisibilityControlStatic : MonoBehaviour
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

	public VisibilityControlStatic()
	{
		m_destroyed = false;

	}

	public void Setup(VectorLine line, bool makeBounds)
	{
		if (makeBounds)
		{
			VectorManager.SetupBoundsMesh(base.gameObject, line);
		}
		Matrix4x4 localToWorldMatrix = base.transform.localToWorldMatrix;
		for (int i = 0; i < line.points3.Count; i++)
		{
			line.points3[i] = localToWorldMatrix.MultiplyPoint3x4(line.points3[i]);
		}
		m_vectorLine = line;
		VectorManager.VisibilityStaticSetup(line, out m_objectNumber);
		StartCoroutine(WaitCheck());
	}

	private IEnumerator WaitCheck()
	{
		VectorManager.DrawArrayLine(m_objectNumber.i);
		yield return null;
		if (!GetComponent<Renderer>().isVisible)
		{
			m_vectorLine.active = false;
		}
	}

	private void OnBecameVisible()
	{
		m_vectorLine.active = true;
		VectorManager.DrawArrayLine(m_objectNumber.i);
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
			VectorManager.VisibilityStaticRemove(m_objectNumber.i);
			VectorLine.Destroy(ref m_vectorLine);
		}
	}




}
