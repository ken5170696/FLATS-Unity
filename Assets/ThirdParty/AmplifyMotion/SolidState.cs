using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
namespace AmplifyMotion
{
	internal class SolidState : MotionState
	{
		private MeshRenderer m_meshRenderer;

		private Matrix3x4 m_prevLocalToWorld;

		private Matrix3x4 m_currLocalToWorld;

		private Mesh m_mesh;

		private MaterialDesc[] m_sharedMaterials;

		public bool m_moved;

		private bool m_wasVisible;

		private static HashSet<AmplifyMotionObjectBase> m_uniqueWarnings = new HashSet<AmplifyMotionObjectBase>();

		public SolidState(AmplifyMotionCamera owner, AmplifyMotionObjectBase obj)
			: base(owner, obj)
		{
			m_meshRenderer = m_obj.GetComponent<MeshRenderer>();
		}

		private void IssueError(string message)
		{
			if (!m_uniqueWarnings.Contains(m_obj))
			{
				Debug.LogWarning(message);
				m_uniqueWarnings.Add(m_obj);
			}
			m_error = true;
		}

		internal override void Initialize()
		{
			MeshFilter component = m_obj.GetComponent<MeshFilter>();
			if (component == null || component.sharedMesh == null)
			{
				IssueError("[AmplifyMotion] Invalid MeshFilter/Mesh in object " + m_obj.name + ". Skipping.");
				return;
			}
			base.Initialize();
			m_mesh = component.sharedMesh;
			m_sharedMaterials = ProcessSharedMaterials(m_meshRenderer.sharedMaterials);
			m_wasVisible = false;
		}

		internal override void UpdateTransform(bool starting)
		{
			if (!m_initialized)
			{
				Initialize();
				return;
			}
			if (!starting && m_wasVisible)
			{
				m_prevLocalToWorld = m_currLocalToWorld;
			}
			m_currLocalToWorld = m_transform.localToWorldMatrix;
			m_moved = true;
			if (!m_owner.Overlay)
			{
				m_moved = starting || MotionState.MatrixChanged(m_currLocalToWorld, m_prevLocalToWorld);
			}
			if (starting || !m_wasVisible)
			{
				m_prevLocalToWorld = m_currLocalToWorld;
			}
			m_wasVisible = m_meshRenderer.isVisible;
		}

		internal override void RenderVectors(Camera camera, float scale, Quality quality)
		{
			if (!m_initialized || m_error || !m_meshRenderer.isVisible)
			{
				return;
			}
			bool flag = ((int)m_owner.Instance.CullingMask & (1 << m_obj.gameObject.layer)) != 0;
			if (flag && (!flag || !m_moved))
			{
				return;
			}
			int num = (flag ? m_owner.Instance.GenerateObjectId(m_obj.gameObject) : 255);
			Matrix4x4 mat = ((!m_obj.FixedStep) ? (m_owner.PrevViewProjMatrixRT * (Matrix4x4)m_prevLocalToWorld) : (m_owner.PrevViewProjMatrixRT * (Matrix4x4)m_currLocalToWorld));
			Shader.SetGlobalMatrix("_AM_MATRIX_PREV_MVP", mat);
			Shader.SetGlobalFloat("_AM_OBJECT_ID", (float)num * 0.003921569f);
			Shader.SetGlobalFloat("_AM_MOTION_SCALE", flag ? scale : 0f);
			int num2 = ((quality != Quality.Mobile) ? 2 : 0);
			for (int i = 0; i < m_sharedMaterials.Length; i++)
			{
				MaterialDesc materialDesc = m_sharedMaterials[i];
				int pass = num2 + (materialDesc.coverage ? 1 : 0);
				if (materialDesc.coverage)
				{
					m_owner.Instance.SolidVectorsMaterial.mainTexture = materialDesc.material.mainTexture;
					if (materialDesc.cutoff)
					{
						m_owner.Instance.SolidVectorsMaterial.SetFloat("_Cutoff", materialDesc.material.GetFloat("_Cutoff"));
					}
				}
				if (m_owner.Instance.SolidVectorsMaterial.SetPass(pass))
				{
					Graphics.DrawMeshNow(m_mesh, m_transform.localToWorldMatrix, i);
				}
			}
		}




	}
}
