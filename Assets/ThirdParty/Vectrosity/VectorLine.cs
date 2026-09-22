using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
namespace Vectrosity
{
	[Serializable]
	public class VectorLine 
	{
		private enum FunctionName
		{
			SetColors,
			SetWidths,
			MakeCurve,
			MakeSpline,
			MakeEllipse
		}

		private UIVertex[] m_UIVertices;

		private UIVertex[] m_capVertices;

		private UIVertex[] m_fillVertices;

		private GameObject m_vectorObject;

		private CanvasRenderer m_canvasRenderer;

		private CanvasRenderer m_capRenderer;

		private CanvasRenderer m_fillRenderer;

		private RectTransform m_rectTransform;

		private bool m_on2DCanvas;

		private int adjustEnd;

		private Color32 m_color;

		private bool m_is2D;

		private List<Vector2> m_points2;

		private List<Vector3> m_points3;

		private int m_pointsCount;

		private Vector3[] m_screenPoints;

		private float[] m_lineWidths;

		private float m_lineWidth;

		private float m_maxWeldDistance;

		private float[] m_distances;

		private string m_name;

		private Material m_material;

		private int m_fillVertexCount;

		private bool m_active;

		private float m_capLength;

		private bool m_smoothWidth;

		private bool m_smoothColor;

		private bool m_continuous;

		private bool m_fillObjectSet;

		private Joins m_joins;

		private bool m_isPoints;

		private bool m_isAutoDrawing;

		private int m_drawStart;

		private int m_drawEnd;

		private int m_endPointsUpdate;

		private bool m_useNormals;

		private bool m_useTangents;

		private bool m_normalsCalculated;

		private bool m_tangentsCalculated;

		private int m_vertexCount;

		private EndCap m_capType;

		private string m_endCap;

		private bool m_continuousTexture;

		private Transform m_drawTransform;

		private bool m_viewportDraw;

		private float m_textureScale;

		private bool m_useTextureScale;

		private float m_textureOffset;

		private bool m_useMatrix;

		private Matrix4x4 m_matrix;

		private bool m_collider;

		private bool m_trigger;

		private PhysicsMaterial2D m_physicsMaterial;

		private Mesh m_mesh;

		private int m_canvasID;

		private static Vector3 v3zero = Vector3.zero;

		private static List<Canvas> m_canvases;

		private static List<Canvas> m_canvases3D;

		private static Material defaultMaterial;

		private static Transform camTransform;

		private static Camera cam3D;

		private static Vector3 oldPosition;

		private static Vector3 oldRotation;

		private static bool lineManagerCreated = false;

		private static LineManager _lineManager;

		private static Dictionary<string, CapInfo> capDictionary;

		private static string[] functionNames = new string[5] { "VectorLine.SetColors: Length of color", "VectorLine.SetWidths: Length of line widths", "MakeCurve", "MakeSpline", "MakeEllipse" };

		private static int endianDiff1;

		private static int endianDiff2;

		private static byte[] byteBlock;

		public RectTransform rectTransform
		{
			get
			{
				if (m_vectorObject != null)
				{
					return m_rectTransform;
				}
				return null;
			}
		}

		public Color32 color
		{
			get
			{
				return m_color;
			}
			set
			{
				m_color = value;
				SetColor(value);
			}
		}

		public bool is2D
		{
			get
			{
				return m_is2D;
			}
		}

		public List<Vector2> points2
		{
			get
			{
				if (!m_is2D)
				{
					Debug.LogError("Line \"" + name + "\" uses points3 rather than points2");
					return null;
				}
				return m_points2;
			}
		}

		public List<Vector3> points3
		{
			get
			{
				if (m_is2D)
				{
					Debug.LogError("Line \"" + name + "\" uses points2 rather than points3");
					return null;
				}
				return m_points3;
			}
		}

		private int pointsCount
		{
			get
			{
				return (!m_is2D) ? m_points3.Count : m_points2.Count;
			}
		}

		public float lineWidth
		{
			get
			{
				return m_lineWidth;
			}
			set
			{
				m_lineWidth = value;
				float num = value * 0.5f;
				for (int i = 0; i < m_lineWidths.Length; i++)
				{
					m_lineWidths[i] = num;
				}
				m_maxWeldDistance = value * 2f * (value * 2f);
			}
		}

		public float maxWeldDistance
		{
			get
			{
				return Mathf.Sqrt(m_maxWeldDistance);
			}
			set
			{
				m_maxWeldDistance = value * value;
			}
		}

		public string name
		{
			get
			{
				return m_name;
			}
			set
			{
				m_name = value;
				if (m_vectorObject != null)
				{
					m_vectorObject.name = value;
				}
				if (m_capRenderer != null)
				{
					m_capRenderer.gameObject.name = value + " cap";
				}
				if (m_fillRenderer != null)
				{
					m_fillRenderer.gameObject.name = value + " fill";
				}
			}
		}

		public Material material
		{
			get
			{
				return m_material;
			}
			set
			{
				m_material = value;
				if (m_vectorObject != null)
				{
					m_canvasRenderer.SetMaterial(m_material, null);
				}
				if (m_fillObjectSet)
				{
					m_fillRenderer.SetMaterial(m_material, null);
				}
			}
		}

		public bool active
		{
			get
			{
				return m_active;
			}
			set
			{
				m_active = value;
				if (m_canvasRenderer != null)
				{
					m_canvasRenderer.SetVertices(m_UIVertices, m_active ? GetVertexCount() : 0);
				}
				if (m_capRenderer != null)
				{
					m_capRenderer.SetVertices(m_capVertices, m_active ? 8 : 0);
				}
				if (m_fillRenderer != null)
				{
					m_fillRenderer.SetVertices(m_fillVertices, m_active ? m_fillVertexCount : 0);
				}
			}
		}

		public float capLength
		{
			get
			{
				return m_capLength;
			}
			set
			{
				if (m_isPoints)
				{
					Debug.LogError("VectorPoints can't use capLength");
				}
				else
				{
					m_capLength = value;
				}
			}
		}

		public bool smoothWidth
		{
			get
			{
				return m_smoothWidth;
			}
			set
			{
				m_smoothWidth = !m_isPoints && value;
			}
		}

		public bool smoothColor
		{
			get
			{
				return m_smoothColor;
			}
			set
			{
				m_smoothColor = !m_isPoints && value;
			}
		}

		public bool continuous
		{
			get
			{
				return m_continuous;
			}
		}

		public Joins joins
		{
			get
			{
				return m_joins;
			}
			set
			{
				if (m_isPoints || (!m_continuous && value == Joins.Fill))
				{
					return;
				}
				m_joins = value;
				if (m_joins == Joins.Fill && !m_fillObjectSet)
				{
					SetupFillObject();
					return;
				}
				if (m_joins == Joins.Fill && m_fillVertices.Length < m_UIVertices.Length)
				{
					Array.Resize(ref m_fillVertices, m_UIVertices.Length);
				}
				if (m_joins != Joins.Fill && m_fillObjectSet)
				{
					m_fillRenderer.SetVertices(m_fillVertices, 0);
				}
				if (m_joins == Joins.Fill && m_fillObjectSet)
				{
					m_fillRenderer.SetVertices(m_fillVertices, m_fillVertexCount);
				}
			}
		}

		public bool isAutoDrawing
		{
			get
			{
				return m_isAutoDrawing;
			}
		}

		public int drawStart
		{
			get
			{
				return m_drawStart;
			}
			set
			{
				if (!m_continuous && (value & 1) != 0)
				{
					value++;
				}
				m_drawStart = Mathf.Clamp(value, 0, pointsCount - 1);
			}
		}

		public int drawEnd
		{
			get
			{
				return m_drawEnd;
			}
			set
			{
				if (!m_continuous && value != 0 && (value & 1) == 0)
				{
					value++;
				}
				m_drawEnd = Mathf.Clamp(value, 0, pointsCount - 1);
			}
		}

		public int endPointsUpdate
		{
			get
			{
				if (m_continuous)
				{
					return m_endPointsUpdate;
				}
				return (m_endPointsUpdate != 0) ? (m_endPointsUpdate + 1) : 0;
			}
			set
			{
				if (!m_continuous && value > 1 && (value & 1) == 0)
				{
					value--;
				}
				m_endPointsUpdate = Mathf.Max(0, value);
			}
		}

		public string endCap
		{
			get
			{
				return m_endCap;
			}
			set
			{
				if (m_isPoints)
				{
					Debug.LogError("VectorPoints can't use end caps");
					return;
				}
				if (value == null || value == "")
				{
					m_endCap = null;
					m_capType = EndCap.None;
					RemoveEndCap();
					return;
				}
				if (capDictionary == null || !capDictionary.ContainsKey(value))
				{
					Debug.LogError("End cap \"" + value + "\" is not set up");
					return;
				}
				m_endCap = value;
				m_capType = capDictionary[value].capType;
				if (m_capType != EndCap.None)
				{
					SetupEndCap();
				}
			}
		}

		public bool continuousTexture
		{
			get
			{
				return m_continuousTexture;
			}
			set
			{
				m_continuousTexture = value;
				if (!value)
				{
					ResetTextureScale();
				}
			}
		}

		public Transform drawTransform
		{
			get
			{
				return m_drawTransform;
			}
			set
			{
				m_drawTransform = value;
			}
		}

		public bool useViewportCoords
		{
			get
			{
				return m_viewportDraw;
			}
			set
			{
				if (m_is2D)
				{
					m_viewportDraw = value;
				}
				else
				{
					Debug.LogWarning("Line must be 2D in order to use viewport coords");
				}
			}
		}

		public float textureScale
		{
			get
			{
				return m_textureScale;
			}
			set
			{
				m_textureScale = value;
				if (m_textureScale == 0f)
				{
					m_useTextureScale = false;
					ResetTextureScale();
				}
				else
				{
					m_useTextureScale = true;
				}
			}
		}

		public float textureOffset
		{
			get
			{
				return m_textureOffset;
			}
			set
			{
				m_textureOffset = value;
				SetTextureScale(true);
			}
		}

		public Matrix4x4 matrix
		{
			get
			{
				return m_matrix;
			}
			set
			{
				m_matrix = value;
				m_useMatrix = m_matrix != Matrix4x4.identity;
			}
		}

		public int drawDepth
		{
			get
			{
				return m_vectorObject.transform.GetSiblingIndex();
			}
			set
			{
				m_vectorObject.transform.SetSiblingIndex(value);
			}
		}

		public bool collider
		{
			get
			{
				return m_collider;
			}
			set
			{
				m_collider = value;
				AddColliderIfNeeded();
				m_vectorObject.GetComponent<Collider2D>().enabled = value;
			}
		}

		public bool trigger
		{
			get
			{
				return m_trigger;
			}
			set
			{
				m_trigger = value;
				if (m_vectorObject.GetComponent<Collider2D>() != null)
				{
					m_vectorObject.GetComponent<Collider2D>().isTrigger = value;
				}
			}
		}

		public PhysicsMaterial2D physicsMaterial
		{
			get
			{
				return m_physicsMaterial;
			}
			set
			{
				AddColliderIfNeeded();
				m_physicsMaterial = value;
				m_vectorObject.GetComponent<Collider2D>().sharedMaterial = value;
			}
		}

		public int canvasID
		{
			get
			{
				return m_canvasID;
			}
			set
			{
				if (value < 0)
				{
					Debug.LogError("CanvasID must be >= 0");
					return;
				}
				if (m_on2DCanvas)
				{
					SetCanvas(value);
					m_vectorObject.transform.SetParent(m_canvases[value].transform, false);
				}
				else
				{
					SetCanvas3D(value);
					m_vectorObject.transform.SetParent(m_canvases3D[value].transform, false);
				}
				m_canvasID = value;
			}
		}

		public static List<Canvas> canvases
		{
			get
			{
				if (m_canvases == null || m_canvases[0] == null)
				{
					SetCanvas(0);
				}
				return m_canvases;
			}
		}

		public static List<Canvas> canvases3D
		{
			get
			{
				if (m_canvases3D == null || m_canvases3D[0] == null)
				{
					SetCanvas3D(0);
				}
				return m_canvases3D;
			}
		}

		public static Canvas canvas
		{
			get
			{
				if (m_canvases == null || m_canvases[0] == null)
				{
					SetCanvas(0);
				}
				return m_canvases[0];
			}
		}

		public static Canvas canvas3D
		{
			get
			{
				if (m_canvases3D == null || m_canvases3D[0] == null)
				{
					SetCanvas3D(0);
				}
				return m_canvases3D[0];
			}
		}

		public static Vector3 camTransformPosition
		{
			get
			{
				return camTransform.position;
			}
		}

		public static bool camTransformExists
		{
			get
			{
				return camTransform != null;
			}
		}

		public static LineManager lineManager
		{
			get
			{
				if (!lineManagerCreated)
				{
					lineManagerCreated = true;
					GameObject gameObject = new GameObject("LineManager");
					_lineManager = gameObject.AddComponent<LineManager>();
					_lineManager.enabled = false;
					UnityEngine.Object.DontDestroyOnLoad(_lineManager);
				}
				return _lineManager;
			}
		}

		public VectorLine(string lineName, Vector3[] linePoints, Material lineMaterial, float width)
		{
			m_active = true;
			m_smoothWidth = false;
			m_smoothColor = false;
			m_fillObjectSet = false;
			m_isAutoDrawing = false;
			m_drawStart = 0;
			m_drawEnd = 0;
			m_useNormals = false;
			m_useTangents = false;
			m_normalsCalculated = false;
			m_tangentsCalculated = false;
			m_capType = EndCap.None;
			m_continuousTexture = false;
			m_useTextureScale = false;
			m_useMatrix = false;
			m_collider = false;
			m_trigger = false;
			m_canvasID = 0;

			m_points3 = new List<Vector3>(linePoints);
			SetupLine(lineName, lineMaterial, width, LineType.Discrete, Joins.None, false, false, m_points3.Count);
		}

		public VectorLine(string lineName, List<Vector3> linePoints, Material lineMaterial, float width)
		{
			m_active = true;
			m_smoothWidth = false;
			m_smoothColor = false;
			m_fillObjectSet = false;
			m_isAutoDrawing = false;
			m_drawStart = 0;
			m_drawEnd = 0;
			m_useNormals = false;
			m_useTangents = false;
			m_normalsCalculated = false;
			m_tangentsCalculated = false;
			m_capType = EndCap.None;
			m_continuousTexture = false;
			m_useTextureScale = false;
			m_useMatrix = false;
			m_collider = false;
			m_trigger = false;
			m_canvasID = 0;

			m_points3 = linePoints;
			SetupLine(lineName, lineMaterial, width, LineType.Discrete, Joins.None, false, false, m_points3.Count);
		}

		public VectorLine(string lineName, Vector3[] linePoints, Material lineMaterial, float width, LineType lineType)
		{
			m_active = true;
			m_smoothWidth = false;
			m_smoothColor = false;
			m_fillObjectSet = false;
			m_isAutoDrawing = false;
			m_drawStart = 0;
			m_drawEnd = 0;
			m_useNormals = false;
			m_useTangents = false;
			m_normalsCalculated = false;
			m_tangentsCalculated = false;
			m_capType = EndCap.None;
			m_continuousTexture = false;
			m_useTextureScale = false;
			m_useMatrix = false;
			m_collider = false;
			m_trigger = false;
			m_canvasID = 0;

			m_points3 = new List<Vector3>(linePoints);
			SetupLine(lineName, lineMaterial, width, lineType, Joins.None, false, false, m_points3.Count);
		}

		public VectorLine(string lineName, List<Vector3> linePoints, Material lineMaterial, float width, LineType lineType)
		{
			m_active = true;
			m_smoothWidth = false;
			m_smoothColor = false;
			m_fillObjectSet = false;
			m_isAutoDrawing = false;
			m_drawStart = 0;
			m_drawEnd = 0;
			m_useNormals = false;
			m_useTangents = false;
			m_normalsCalculated = false;
			m_tangentsCalculated = false;
			m_capType = EndCap.None;
			m_continuousTexture = false;
			m_useTextureScale = false;
			m_useMatrix = false;
			m_collider = false;
			m_trigger = false;
			m_canvasID = 0;

			m_points3 = linePoints;
			SetupLine(lineName, lineMaterial, width, lineType, Joins.None, false, false, m_points3.Count);
		}

		public VectorLine(string lineName, Vector3[] linePoints, Material lineMaterial, float width, LineType lineType, Joins joins)
		{
			m_active = true;
			m_smoothWidth = false;
			m_smoothColor = false;
			m_fillObjectSet = false;
			m_isAutoDrawing = false;
			m_drawStart = 0;
			m_drawEnd = 0;
			m_useNormals = false;
			m_useTangents = false;
			m_normalsCalculated = false;
			m_tangentsCalculated = false;
			m_capType = EndCap.None;
			m_continuousTexture = false;
			m_useTextureScale = false;
			m_useMatrix = false;
			m_collider = false;
			m_trigger = false;
			m_canvasID = 0;

			m_points3 = new List<Vector3>(linePoints);
			SetupLine(lineName, lineMaterial, width, lineType, joins, false, false, m_points3.Count);
		}

		public VectorLine(string lineName, List<Vector3> linePoints, Material lineMaterial, float width, LineType lineType, Joins joins)
		{
			m_active = true;
			m_smoothWidth = false;
			m_smoothColor = false;
			m_fillObjectSet = false;
			m_isAutoDrawing = false;
			m_drawStart = 0;
			m_drawEnd = 0;
			m_useNormals = false;
			m_useTangents = false;
			m_normalsCalculated = false;
			m_tangentsCalculated = false;
			m_capType = EndCap.None;
			m_continuousTexture = false;
			m_useTextureScale = false;
			m_useMatrix = false;
			m_collider = false;
			m_trigger = false;
			m_canvasID = 0;

			m_points3 = linePoints;
			SetupLine(lineName, lineMaterial, width, lineType, joins, false, false, m_points3.Count);
		}

		public VectorLine(string lineName, Vector2[] linePoints, Material lineMaterial, float width)
		{
			m_active = true;
			m_smoothWidth = false;
			m_smoothColor = false;
			m_fillObjectSet = false;
			m_isAutoDrawing = false;
			m_drawStart = 0;
			m_drawEnd = 0;
			m_useNormals = false;
			m_useTangents = false;
			m_normalsCalculated = false;
			m_tangentsCalculated = false;
			m_capType = EndCap.None;
			m_continuousTexture = false;
			m_useTextureScale = false;
			m_useMatrix = false;
			m_collider = false;
			m_trigger = false;
			m_canvasID = 0;

			m_points2 = new List<Vector2>(linePoints);
			SetupLine(lineName, lineMaterial, width, LineType.Discrete, Joins.None, true, false, m_points2.Count);
		}

		public VectorLine(string lineName, List<Vector2> linePoints, Material lineMaterial, float width)
		{
			m_active = true;
			m_smoothWidth = false;
			m_smoothColor = false;
			m_fillObjectSet = false;
			m_isAutoDrawing = false;
			m_drawStart = 0;
			m_drawEnd = 0;
			m_useNormals = false;
			m_useTangents = false;
			m_normalsCalculated = false;
			m_tangentsCalculated = false;
			m_capType = EndCap.None;
			m_continuousTexture = false;
			m_useTextureScale = false;
			m_useMatrix = false;
			m_collider = false;
			m_trigger = false;
			m_canvasID = 0;

			m_points2 = linePoints;
			SetupLine(lineName, lineMaterial, width, LineType.Discrete, Joins.None, true, false, m_points2.Count);
		}

		public VectorLine(string lineName, Vector2[] linePoints, Material lineMaterial, float width, LineType lineType)
		{
			m_active = true;
			m_smoothWidth = false;
			m_smoothColor = false;
			m_fillObjectSet = false;
			m_isAutoDrawing = false;
			m_drawStart = 0;
			m_drawEnd = 0;
			m_useNormals = false;
			m_useTangents = false;
			m_normalsCalculated = false;
			m_tangentsCalculated = false;
			m_capType = EndCap.None;
			m_continuousTexture = false;
			m_useTextureScale = false;
			m_useMatrix = false;
			m_collider = false;
			m_trigger = false;
			m_canvasID = 0;

			m_points2 = new List<Vector2>(linePoints);
			SetupLine(lineName, lineMaterial, width, lineType, Joins.None, true, false, m_points2.Count);
		}

		public VectorLine(string lineName, List<Vector2> linePoints, Material lineMaterial, float width, LineType lineType)
		{
			m_active = true;
			m_smoothWidth = false;
			m_smoothColor = false;
			m_fillObjectSet = false;
			m_isAutoDrawing = false;
			m_drawStart = 0;
			m_drawEnd = 0;
			m_useNormals = false;
			m_useTangents = false;
			m_normalsCalculated = false;
			m_tangentsCalculated = false;
			m_capType = EndCap.None;
			m_continuousTexture = false;
			m_useTextureScale = false;
			m_useMatrix = false;
			m_collider = false;
			m_trigger = false;
			m_canvasID = 0;

			m_points2 = linePoints;
			SetupLine(lineName, lineMaterial, width, lineType, Joins.None, true, false, m_points2.Count);
		}

		public VectorLine(string lineName, Vector2[] linePoints, Material lineMaterial, float width, LineType lineType, Joins joins)
		{
			m_active = true;
			m_smoothWidth = false;
			m_smoothColor = false;
			m_fillObjectSet = false;
			m_isAutoDrawing = false;
			m_drawStart = 0;
			m_drawEnd = 0;
			m_useNormals = false;
			m_useTangents = false;
			m_normalsCalculated = false;
			m_tangentsCalculated = false;
			m_capType = EndCap.None;
			m_continuousTexture = false;
			m_useTextureScale = false;
			m_useMatrix = false;
			m_collider = false;
			m_trigger = false;
			m_canvasID = 0;

			m_points2 = new List<Vector2>(linePoints);
			SetupLine(lineName, lineMaterial, width, lineType, joins, true, false, m_points2.Count);
		}

		public VectorLine(string lineName, List<Vector2> linePoints, Material lineMaterial, float width, LineType lineType, Joins joins)
		{
			m_active = true;
			m_smoothWidth = false;
			m_smoothColor = false;
			m_fillObjectSet = false;
			m_isAutoDrawing = false;
			m_drawStart = 0;
			m_drawEnd = 0;
			m_useNormals = false;
			m_useTangents = false;
			m_normalsCalculated = false;
			m_tangentsCalculated = false;
			m_capType = EndCap.None;
			m_continuousTexture = false;
			m_useTextureScale = false;
			m_useMatrix = false;
			m_collider = false;
			m_trigger = false;
			m_canvasID = 0;

			m_points2 = linePoints;
			SetupLine(lineName, lineMaterial, width, lineType, joins, true, false, m_points2.Count);
		}

		protected VectorLine(bool usePoints, string lineName, Vector3[] linePoints, Material lineMaterial, float width)
		{
			m_active = true;
			m_smoothWidth = false;
			m_smoothColor = false;
			m_fillObjectSet = false;
			m_isAutoDrawing = false;
			m_drawStart = 0;
			m_drawEnd = 0;
			m_useNormals = false;
			m_useTangents = false;
			m_normalsCalculated = false;
			m_tangentsCalculated = false;
			m_capType = EndCap.None;
			m_continuousTexture = false;
			m_useTextureScale = false;
			m_useMatrix = false;
			m_collider = false;
			m_trigger = false;
			m_canvasID = 0;

			m_points3 = new List<Vector3>(linePoints);
			SetupLine(lineName, lineMaterial, width, LineType.Continuous, Joins.None, false, true, m_points3.Count);
		}

		protected VectorLine(bool usePoints, string lineName, List<Vector3> linePoints, Material lineMaterial, float width)
		{
			m_active = true;
			m_smoothWidth = false;
			m_smoothColor = false;
			m_fillObjectSet = false;
			m_isAutoDrawing = false;
			m_drawStart = 0;
			m_drawEnd = 0;
			m_useNormals = false;
			m_useTangents = false;
			m_normalsCalculated = false;
			m_tangentsCalculated = false;
			m_capType = EndCap.None;
			m_continuousTexture = false;
			m_useTextureScale = false;
			m_useMatrix = false;
			m_collider = false;
			m_trigger = false;
			m_canvasID = 0;

			m_points3 = linePoints;
			SetupLine(lineName, lineMaterial, width, LineType.Continuous, Joins.None, false, true, m_points3.Count);
		}

		protected VectorLine(bool usePoints, string lineName, Vector2[] linePoints, Material lineMaterial, float width)
		{
			m_active = true;
			m_smoothWidth = false;
			m_smoothColor = false;
			m_fillObjectSet = false;
			m_isAutoDrawing = false;
			m_drawStart = 0;
			m_drawEnd = 0;
			m_useNormals = false;
			m_useTangents = false;
			m_normalsCalculated = false;
			m_tangentsCalculated = false;
			m_capType = EndCap.None;
			m_continuousTexture = false;
			m_useTextureScale = false;
			m_useMatrix = false;
			m_collider = false;
			m_trigger = false;
			m_canvasID = 0;

			m_points2 = new List<Vector2>(linePoints);
			SetupLine(lineName, lineMaterial, width, LineType.Continuous, Joins.None, true, true, m_points2.Count);
		}

		protected VectorLine(bool usePoints, string lineName, List<Vector2> linePoints, Material lineMaterial, float width)
		{
			m_active = true;
			m_smoothWidth = false;
			m_smoothColor = false;
			m_fillObjectSet = false;
			m_isAutoDrawing = false;
			m_drawStart = 0;
			m_drawEnd = 0;
			m_useNormals = false;
			m_useTangents = false;
			m_normalsCalculated = false;
			m_tangentsCalculated = false;
			m_capType = EndCap.None;
			m_continuousTexture = false;
			m_useTextureScale = false;
			m_useMatrix = false;
			m_collider = false;
			m_trigger = false;
			m_canvasID = 0;

			m_points2 = linePoints;
			SetupLine(lineName, lineMaterial, width, LineType.Continuous, Joins.None, true, true, m_points2.Count);
		}

		public static string Version()
		{
			return "Vectrosity version 4.4";
		}

		private void AddColliderIfNeeded()
		{
			if (m_vectorObject.GetComponent<Collider2D>() == null)
			{
				m_vectorObject.AddComponent((!m_continuous) ? typeof(PolygonCollider2D) : typeof(EdgeCollider2D));
				m_vectorObject.GetComponent<Collider2D>().isTrigger = m_trigger;
			}
		}

		protected void SetupLine(string lineName, Material useMaterial, float width, LineType lineType, Joins joins, bool use2D, bool usePoints, int count)
		{
			m_continuous = lineType == LineType.Continuous;
			m_is2D = use2D;
			m_isPoints = usePoints;
			if (joins == Joins.Fill && !m_continuous)
			{
				Debug.LogError("VectorLine: Must use LineType.Continuous if using Joins.Fill for \"" + lineName + "\"");
				return;
			}
			if ((m_is2D && m_points2 == null) || (!m_is2D && m_points3 == null))
			{
				Debug.LogError("VectorLine: the points array is null for \"" + lineName + "\"");
				return;
			}
			m_pointsCount = count;
			name = lineName;
			if (!CheckPointCount(count))
			{
				return;
			}
			m_maxWeldDistance = width * 2f * (width * 2f);
			m_joins = joins;
			if (useMaterial == null)
			{
				if (defaultMaterial == null)
				{
					defaultMaterial = new Material(Shader.Find("UI/Default"));
				}
				m_material = defaultMaterial;
			}
			else
			{
				m_material = useMaterial;
			}
			if (m_canvases == null || m_canvases[0] == null)
			{
				SetCanvas(0);
			}
			m_vectorObject = new GameObject(name);
			m_vectorObject.transform.SetParent(m_canvases[0].transform, false);
			m_on2DCanvas = true;
			m_canvasRenderer = m_vectorObject.AddComponent<CanvasRenderer>();
			m_canvasRenderer.SetMaterial(m_material, null);
			m_rectTransform = m_vectorObject.AddComponent<RectTransform>();
			SetupTransform(m_rectTransform);
			if (SetVertexCount())
			{
				m_UIVertices = new UIVertex[m_vertexCount];
				SetUVs(0, MaxSegmentIndex());
				color = Color.white;
				m_lineWidths = new float[1];
				m_lineWidths[0] = width * 0.5f;
				m_lineWidth = width;
				if (!m_is2D)
				{
					m_screenPoints = new Vector3[m_vertexCount];
				}
				m_drawStart = 0;
				m_drawEnd = m_pointsCount - 1;
				if (joins == Joins.Fill)
				{
					SetupFillObject();
				}
			}
		}

		private void SetupFillObject()
		{
			m_fillVertices = new UIVertex[m_vertexCount];
			if (m_fillRenderer == null)
			{
				GameObject gameObject = new GameObject(name + " fill");
				m_fillRenderer = gameObject.AddComponent<CanvasRenderer>();
				m_fillRenderer.SetMaterial(m_material, null);
				RectTransform rectTransform = gameObject.AddComponent<RectTransform>();
				SetupTransform(rectTransform);
				gameObject.transform.SetParent(m_vectorObject.transform, false);
			}
			m_fillObjectSet = true;
		}

		private void SetupEndCap()
		{
			if (m_UIVertices != null)
			{
				m_capVertices = new UIVertex[8];
				Color32 color = this.color;
				if (m_UIVertices.Length > 0)
				{
					color = m_UIVertices[m_vertexCount - 1].color;
				}
				for (int i = 0; i < 4; i++)
				{
					m_capVertices[i].color = this.color;
					m_capVertices[i + 4].color = color;
				}
				m_capVertices[0].uv0 = new Vector2(0f, 0.25f);
				m_capVertices[1].uv0 = new Vector2(1f, 0.25f);
				m_capVertices[2].uv0 = new Vector2(1f, 0f);
				m_capVertices[3].uv0 = new Vector2(0f, 0f);
				if (capDictionary[m_endCap].capType == EndCap.Mirror)
				{
					m_capVertices[4].uv0 = new Vector2(1f, 0.25f);
					m_capVertices[5].uv0 = new Vector2(0f, 0.25f);
					m_capVertices[6].uv0 = new Vector2(0f, 0f);
					m_capVertices[7].uv0 = new Vector2(1f, 0f);
				}
				else
				{
					m_capVertices[4].uv0 = new Vector2(0f, 1f);
					m_capVertices[5].uv0 = new Vector2(1f, 1f);
					m_capVertices[6].uv0 = new Vector2(1f, 0.75f);
					m_capVertices[7].uv0 = new Vector2(0f, 0.75f);
				}
				if (m_capRenderer == null)
				{
					GameObject gameObject = new GameObject(name + " cap");
					m_capRenderer = gameObject.AddComponent<CanvasRenderer>();
					m_capRenderer.SetMaterial(capDictionary[m_endCap].material, null);
					RectTransform rectTransform = gameObject.AddComponent<RectTransform>();
					SetupTransform(rectTransform);
					gameObject.transform.SetParent(m_vectorObject.transform, false);
				}
			}
		}

		private bool CheckPointCount(int count)
		{
			if (!m_continuous && count % 2 != 0)
			{
				Debug.LogError("VectorLine: Must have an even points array count for \"" + name + "\" when using LineType.Discrete");
				return false;
			}
			return true;
		}

		private int GetVertexCount()
		{
			int num = m_vertexCount - adjustEnd * 4;
			if (num < 0)
			{
				num = 0;
			}
			return num;
		}

		private static void SetupTransform(RectTransform rectTransform)
		{
			rectTransform.offsetMin = Vector2.zero;
			rectTransform.offsetMax = Vector2.zero;
			rectTransform.anchorMin = Vector2.zero;
			rectTransform.anchorMax = Vector2.zero;
			rectTransform.pivot = Vector2.zero;
			rectTransform.anchoredPosition = Vector2.zero;
		}

		public void Resize(int newCount)
		{
			if (newCount < 0)
			{
				Debug.LogError("VectorLine.Resize: the new count must be >= 0");
			}
			else
			{
				if (!CheckPointCount(newCount))
				{
					return;
				}
				if (m_is2D)
				{
					if (newCount > m_pointsCount)
					{
						for (int i = 0; i < newCount - m_pointsCount; i++)
						{
							m_points2.Add(Vector2.zero);
						}
					}
					else
					{
						m_points2.RemoveRange(newCount, m_pointsCount - newCount);
					}
				}
				else if (newCount > m_pointsCount)
				{
					for (int j = 0; j < newCount - m_pointsCount; j++)
					{
						m_points3.Add(v3zero);
					}
				}
				else
				{
					m_points3.RemoveRange(newCount, m_pointsCount - newCount);
				}
				Resize();
			}
		}

		private void Resize()
		{
			int num = m_pointsCount;
			if (!m_isPoints)
			{
				num = ((!m_continuous) ? (m_pointsCount / 2) : Mathf.Max(0, m_pointsCount - 1));
			}
			bool flag = m_drawEnd == m_pointsCount - 1 || m_drawEnd < 1;
			if (!SetVertexCount())
			{
				return;
			}
			m_pointsCount = pointsCount;
			int num2 = m_UIVertices.Length;
			if (num2 < m_vertexCount)
			{
				if (num2 == 0)
				{
					num2 = 4;
				}
				while (num2 < m_pointsCount)
				{
					num2 *= 2;
				}
				num2 = Mathf.Min(num2, MaxPoints());
				Array.Resize(ref m_UIVertices, num2 * 4);
				if (m_joins == Joins.Fill)
				{
					Array.Resize(ref m_fillVertices, num2 * 4);
				}
				if (!m_is2D)
				{
					Array.Resize(ref m_screenPoints, num2 * 4);
				}
			}
			if (m_lineWidths.Length > 1)
			{
				if (!m_isPoints)
				{
					num2 = ((!m_continuous) ? (num2 / 2) : (num2 - 1));
				}
				if (num2 > m_lineWidths.Length)
				{
					Array.Resize(ref m_lineWidths, num2);
				}
			}
			if (flag)
			{
				m_drawEnd = m_pointsCount - 1;
			}
			m_drawStart = Mathf.Clamp(m_drawStart, 0, m_pointsCount - 1);
			m_drawEnd = Mathf.Clamp(m_drawEnd, 0, m_pointsCount - 1);
			if (m_pointsCount > num)
			{
				SetColor(m_color, num, MaxSegmentIndex());
				SetUVs(num, MaxSegmentIndex());
				if (m_lineWidths.Length > 1)
				{
					SetWidth(m_lineWidth, num, MaxSegmentIndex());
				}
			}
		}

		private void SetUVs(int startIndex, int endIndex)
		{
			int num = startIndex * 4;
			for (int i = startIndex; i < endIndex; i++)
			{
				m_UIVertices[num].uv0 = new Vector2(0f, 1f);
				m_UIVertices[num + 3].uv0 = new Vector2(0f, 0f);
				m_UIVertices[num + 2].uv0 = new Vector2(1f, 0f);
				m_UIVertices[num + 1].uv0 = new Vector2(1f, 1f);
				num += 4;
			}
		}

		private bool SetVertexCount()
		{
			m_vertexCount = Mathf.Max(0, MaxSegmentIndex() * 4);
			if (m_vertexCount > 65534)
			{
				Debug.LogError("VectorLine: exceeded maximum vertex count of 65534 for \"" + name + "\"...use fewer points (maximum is 16383 points for continuous lines and points, and 32767 points for discrete lines)");
				return false;
			}
			return true;
		}

		private int MaxSegmentIndex()
		{
			if (m_isPoints)
			{
				return pointsCount;
			}
			return (!m_continuous) ? (pointsCount / 2) : (pointsCount - 1);
		}

		private int MaxPoints()
		{
			if (m_isPoints || m_continuous)
			{
				return 32767;
			}
			return 16383;
		}

		public void AddNormals()
		{
			m_useNormals = true;
			m_normalsCalculated = false;
		}

		public void AddTangents()
		{
			m_useTangents = true;
			m_tangentsCalculated = false;
		}

		private void CalculateNormals()
		{
			if (m_mesh == null)
			{
				m_mesh = new Mesh();
			}
			Vector3[] array = new Vector3[m_vertexCount];
			for (int i = 0; i < m_vertexCount; i++)
			{
				array[i] = m_UIVertices[i].position;
			}
			m_mesh.vertices = array;
			m_mesh.triangles = GetTriangles();
			m_mesh.RecalculateNormals();
			Vector3[] normals = m_mesh.normals;
			for (int j = 0; j < m_vertexCount; j++)
			{
				m_UIVertices[j].normal = normals[j];
			}
		}

		private void CalculateTangents()
		{
			if (!m_useNormals)
			{
				AddNormals();
				CalculateNormals();
				m_normalsCalculated = true;
			}
			Vector3[] array = new Vector3[m_vertexCount];
			Vector3[] array2 = new Vector3[m_vertexCount];
			int[] triangles = GetTriangles();
			int num = triangles.Length;
			for (int i = 0; i < num; i += 3)
			{
				int num2 = triangles[i];
				int num3 = triangles[i + 1];
				int num4 = triangles[i + 2];
				Vector3 position = m_UIVertices[num2].position;
				Vector3 position2 = m_UIVertices[num3].position;
				Vector3 position3 = m_UIVertices[num4].position;
				Vector2 uv = m_UIVertices[num2].uv0;
				Vector2 uv2 = m_UIVertices[num3].uv0;
				Vector2 uv3 = m_UIVertices[num4].uv0;
				float num5 = position2.x - position.x;
				float num6 = position3.x - position.x;
				float num7 = position2.y - position.y;
				float num8 = position3.y - position.y;
				float num9 = position2.z - position.z;
				float num10 = position3.z - position.z;
				float num11 = uv2.x - uv.x;
				float num12 = uv3.x - uv.x;
				float num13 = uv2.y - uv.y;
				float num14 = uv3.y - uv.y;
				float num15 = 1f / (num11 * num14 - num12 * num13);
				Vector3 vector = new Vector3((num14 * num5 - num13 * num6) * num15, (num14 * num7 - num13 * num8) * num15, (num14 * num9 - num13 * num10) * num15);
				Vector3 vector2 = new Vector3((num11 * num6 - num12 * num5) * num15, (num11 * num8 - num12 * num7) * num15, (num11 * num10 - num12 * num9) * num15);
				array[num2] += vector;
				array[num3] += vector;
				array[num4] += vector;
				array2[num2] += vector2;
				array2[num3] += vector2;
				array2[num4] += vector2;
			}
			for (int j = 0; j < m_vertexCount; j++)
			{
				Vector3 normal = m_UIVertices[j].normal;
				Vector3 vector3 = array[j];
				m_UIVertices[j].tangent = (vector3 - normal * Vector3.Dot(normal, vector3)).normalized;
				m_UIVertices[j].tangent.w = ((!(Vector3.Dot(Vector3.Cross(normal, vector3), array2[j]) < 0f)) ? 1f : (-1f));
			}
		}

		private int[] GetTriangles()
		{
			int[] array = new int[m_vertexCount + m_vertexCount / 2];
			int num = 0;
			for (int i = 0; i < array.Length; i += 6)
			{
				array[i] = num;
				array[i + 1] = num + 1;
				array[i + 2] = num + 3;
				array[i + 3] = num + 2;
				array[i + 4] = num + 3;
				array[i + 5] = num + 1;
				num += 4;
			}
			return array;
		}

		private void RemoveEndCap()
		{
			if (m_capRenderer != null)
			{
				UnityEngine.Object.Destroy(m_capRenderer.gameObject);
			}
		}

		private static void SetCanvas(int id)
		{
			if (m_canvases == null)
			{
				m_canvases = new List<Canvas>();
			}
			for (int i = 0; i < m_canvases.Count; i++)
			{
				if (m_canvases[i] == null)
				{
					m_canvases = new List<Canvas>();
					break;
				}
			}
			while (m_canvases.Count < id + 1)
			{
				GameObject gameObject = new GameObject((m_canvases.Count != 0) ? ("VectorCanvas_" + m_canvases.Count) : "VectorCanvas");
				gameObject.layer = LayerMask.NameToLayer("UI");
				Vector3 position = gameObject.transform.position;
				gameObject.transform.position = position;
				Canvas canvas = gameObject.AddComponent<Canvas>();
				canvas.renderMode = RenderMode.ScreenSpaceOverlay;
				canvas.sortingOrder = 1;
				m_canvases.Add(canvas);
			}
		}

		private static void SetCanvas3D(int id)
		{
			if (!cam3D)
			{
				SetCamera3D();
				if (!cam3D)
				{
					Debug.LogError("No camera available...use VectorLine.SetCamera3D to assign a camera");
					return;
				}
			}
			if (m_canvases3D == null)
			{
				m_canvases3D = new List<Canvas>();
			}
			for (int i = 0; i < m_canvases3D.Count; i++)
			{
				if (m_canvases3D[i] == null)
				{
					m_canvases3D = new List<Canvas>();
					break;
				}
			}
			while (m_canvases3D.Count < id + 1)
			{
				GameObject gameObject = new GameObject((m_canvases3D.Count != 0) ? ("VectorCanvas3D_" + m_canvases3D.Count) : "VectorCanvas3D");
				gameObject.layer = LayerMask.NameToLayer("UI");
				Canvas canvas = gameObject.AddComponent<Canvas>();
				canvas.renderMode = RenderMode.WorldSpace;
				canvas.worldCamera = cam3D;
				RectTransform component = gameObject.GetComponent<RectTransform>();
				SetupTransform(component);
				m_canvases3D.Add(canvas);
			}
		}

		public static void SetCanvasCamera(Camera cam)
		{
			SetCanvasCamera(cam, 0);
		}

		public static void SetCanvasCamera(Camera cam, int id)
		{
			if (id < 0)
			{
				Debug.LogError("VectorLine.SetCanvasCamera: id must be >= 0");
				return;
			}
			if (m_canvases == null || m_canvases.Count < id + 1 || m_canvases[id] == null)
			{
				SetCanvas(id);
			}
			m_canvases[id].renderMode = RenderMode.ScreenSpaceCamera;
			m_canvases[id].worldCamera = cam;
		}

		public static void SetCamera3D()
		{
			if (Camera.main == null)
			{
				Debug.LogError("VectorLine.SetCamera3D: no camera tagged \"Main Camera\" found. Please call SetCamera3D with a specific camera instead.");
			}
			else
			{
				SetCamera3D(Camera.main);
			}
		}

		public static void SetCamera3D(Camera thisCamera)
		{
			camTransform = thisCamera.transform;
			cam3D = thisCamera;
			oldPosition = camTransform.position + Vector3.one;
			oldRotation = camTransform.eulerAngles + Vector3.one;
			if (m_canvases3D != null)
			{
				for (int i = 0; i < m_canvases3D.Count && !(m_canvases3D[i] == null); i++)
				{
					m_canvases3D[i].worldCamera = cam3D;
				}
			}
		}

		public static bool CameraHasMoved()
		{
			return oldPosition != camTransform.position || oldRotation != camTransform.eulerAngles;
		}

		public static void UpdateCameraInfo()
		{
			oldPosition = camTransform.position;
			oldRotation = camTransform.eulerAngles;
		}

		public int GetSegmentNumber()
		{
			if (m_isPoints)
			{
				return pointsCount;
			}
			if (m_continuous)
			{
				return pointsCount - 1;
			}
			return pointsCount / 2;
		}

		private bool WrongArrayLength(int arrayLength, FunctionName functionName)
		{
			if (m_continuous)
			{
				if (arrayLength != pointsCount - 1)
				{
					Debug.LogError(functionNames[(int)functionName] + " array for \"" + name + "\" must be length of points array minus one for a continuous line (one entry per line segment)");
					return true;
				}
			}
			else if (arrayLength != pointsCount / 2)
			{
				Debug.LogError(functionNames[(int)functionName] + " array in \"" + name + "\" must be exactly half the length of points array for a discrete line (one entry per line segment)");
				return true;
			}
			return false;
		}

		private bool CheckArrayLength(FunctionName functionName, int segments, int index)
		{
			if (segments < 1)
			{
				Debug.LogError("VectorLine." + functionNames[(int)functionName] + " needs at least 1 segment");
				return false;
			}
			if (m_isPoints)
			{
				if (index + segments > m_pointsCount)
				{
					if (index == 0)
					{
						Debug.LogError("VectorLine." + functionNames[(int)functionName] + ": The number of segments cannot exceed the number of points in the array for \"" + name + "\"");
						return false;
					}
					Debug.LogError("VectorLine: Calling " + functionNames[(int)functionName] + " with an index of " + index + " would exceed the length of the Vector array for \"" + name + "\"");
					return false;
				}
				return true;
			}
			if (m_continuous)
			{
				if (index + (segments + 1) > m_pointsCount)
				{
					if (index == 0)
					{
						Debug.LogError("VectorLine." + functionNames[(int)functionName] + ": The length of the array for continuous lines needs to be at least the number of segments plus one for \"" + name + "\"");
						return false;
					}
					Debug.LogError("VectorLine: Calling " + functionNames[(int)functionName] + " with an index of " + index + " would exceed the length of the Vector array for \"" + name + "\"");
					return false;
				}
			}
			else if (index + segments * 2 > m_pointsCount)
			{
				if (index == 0)
				{
					Debug.LogError("VectorLine." + functionNames[(int)functionName] + ": The length of the array for discrete lines needs to be at least twice the number of segments for \"" + name + "\"");
					return false;
				}
				Debug.LogError("VectorLine: Calling " + functionNames[(int)functionName] + " with an index of " + index + " would exceed the length of the Vector array for \"" + name + "\"");
				return false;
			}
			return true;
		}

		private void SetEndCapColors()
		{
			if (m_UIVertices.Length < 4)
			{
				return;
			}
			if (m_capType <= EndCap.Mirror)
			{
				int num = ((!m_continuous) ? (m_drawStart * 2) : (m_drawStart * 4));
				for (int i = 0; i < 4; i++)
				{
					m_capVertices[i].color = m_UIVertices[num].color;
				}
			}
			if (m_capType >= EndCap.Both)
			{
				int num2 = m_drawEnd;
				if (m_continuous)
				{
					if (m_drawEnd == pointsCount)
					{
						num2--;
					}
				}
				else if (num2 < pointsCount)
				{
					num2++;
				}
				int num3 = num2 * ((!m_continuous) ? 2 : 4) - 8;
				if (num3 < -4)
				{
					num3 = -4;
				}
				for (int j = 4; j < 8; j++)
				{
					m_capVertices[j].color = m_UIVertices[5 + num3].color;
				}
			}
			m_capRenderer.SetVertices(m_capVertices, m_active ? 8 : 0);
		}

		public void SetColor(Color32 color)
		{
			SetColor(color, 0, pointsCount);
		}

		public void SetColor(Color32 color, int index)
		{
			SetColor(color, index, index);
		}

		public void SetColor(Color32 color, int startIndex, int endIndex)
		{
			int num = MaxSegmentIndex();
			startIndex = Mathf.Clamp(startIndex * 4, 0, num * 4);
			endIndex = Mathf.Clamp((endIndex + 1) * 4, 0, num * 4);
			if (pointsCount != m_pointsCount)
			{
				Resize();
			}
			if (!m_smoothColor)
			{
				for (int i = startIndex; i < endIndex; i++)
				{
					m_UIVertices[i].color = color;
				}
			}
			else
			{
				if (startIndex == 0)
				{
					m_UIVertices[startIndex].color = color;
					m_UIVertices[startIndex + 3].color = color;
				}
				int num2 = m_UIVertices.Length;
				for (int j = startIndex; j < endIndex; j += 4)
				{
					m_UIVertices[j + 1].color = color;
					m_UIVertices[j + 2].color = color;
					if (j + 4 < num2)
					{
						m_UIVertices[j + 4].color = color;
						m_UIVertices[j + 7].color = color;
					}
				}
			}
			if (m_capType != EndCap.None && (startIndex <= 0 || endIndex >= num - 1))
			{
				SetEndCapColors();
			}
			m_canvasRenderer.SetVertices(m_UIVertices, m_active ? GetVertexCount() : 0);
			if (m_joins == Joins.Fill)
			{
				SetFillColors();
			}
		}

		public void SetColors(List<Color32> lineColors)
		{
			SetColors(lineColors.ToArray());
		}

		public void SetColors(Color32[] lineColors)
		{
			if (lineColors == null)
			{
				Debug.LogError("VectorLine.SetColors: lineColors array must not be null");
				return;
			}
			if (pointsCount != m_pointsCount)
			{
				Resize();
			}
			if (!m_isPoints)
			{
				if (WrongArrayLength(lineColors.Length, FunctionName.SetColors))
				{
					return;
				}
			}
			else if (lineColors.Length != pointsCount)
			{
				Debug.LogError("VectorLine.SetColors: Length of lineColors array in \"" + name + "\" must be same length as points array");
				return;
			}
			int start;
			int end;
			SetSegmentStartEnd(out start, out end);
			if (start == 0 && end == 0)
			{
				return;
			}
			int num = start * 4;
			if (m_isPoints)
			{
				end++;
			}
			if (smoothColor)
			{
				m_UIVertices[num].color = lineColors[start];
				m_UIVertices[num + 3].color = lineColors[start];
				m_UIVertices[num + 2].color = lineColors[start];
				m_UIVertices[num + 1].color = lineColors[start];
				num += 4;
				for (int i = start + 1; i < end; i++)
				{
					m_UIVertices[num].color = lineColors[i - 1];
					m_UIVertices[num + 3].color = lineColors[i - 1];
					m_UIVertices[num + 2].color = lineColors[i];
					m_UIVertices[num + 1].color = lineColors[i];
					num += 4;
				}
			}
			else
			{
				for (int j = start; j < end; j++)
				{
					m_UIVertices[num].color = lineColors[j];
					m_UIVertices[num + 1].color = lineColors[j];
					m_UIVertices[num + 2].color = lineColors[j];
					m_UIVertices[num + 3].color = lineColors[j];
					num += 4;
				}
			}
			if (m_capType != EndCap.None)
			{
				SetEndCapColors();
			}
			m_canvasRenderer.SetVertices(m_UIVertices, m_active ? GetVertexCount() : 0);
			if (m_joins == Joins.Fill)
			{
				SetFillColors();
			}
		}

		private void SetSegmentStartEnd(out int start, out int end)
		{
			start = ((!m_continuous) ? (m_drawStart / 2) : m_drawStart);
			end = m_drawEnd;
			if (!m_continuous)
			{
				end = m_drawEnd / 2;
				if (m_drawEnd % 2 != 0)
				{
					end++;
				}
			}
		}

		private void SetFillColors()
		{
			if (m_UIVertices.Length < 8)
			{
				return;
			}
			int end = 0;
			int start;
			SetupDrawStartEnd(out start, out end, false);
			start = Mathf.Max(0, --start);
			if (start != 0 || end != 0)
			{
				bool flag = false;
				if (start != end && ((m_is2D && Approximately(m_points2[start], m_points2[end])) || (!m_is2D && Approximately(m_points3[start], m_points3[end]))))
				{
					flag = true;
				}
				start *= 4;
				end *= 4;
				int num = 0;
				for (int i = start; i < end - 4; i += 4)
				{
					m_fillVertices[num].color = m_UIVertices[i + 3].color;
					m_fillVertices[num + 1].color = m_UIVertices[i + 5].color;
					m_fillVertices[num + 2].color = m_UIVertices[i + 2].color;
					m_fillVertices[num + 3].color = m_UIVertices[i + 4].color;
					num += 4;
				}
				if (flag)
				{
					m_fillVertices[num].color = m_UIVertices[end - 1].color;
					m_fillVertices[num + 1].color = m_UIVertices[start + 1].color;
					m_fillVertices[num + 2].color = m_UIVertices[end - 2].color;
					m_fillVertices[num + 3].color = m_UIVertices[start].color;
				}
				m_fillVertexCount = m_vertexCount - adjustEnd * 4 - ((!flag) ? 4 : 0);
				m_fillRenderer.SetVertices(m_fillVertices, m_active ? m_fillVertexCount : 0);
			}
		}

		public Color GetColor(int index)
		{
			index = index * 4 + 2;
			if (index < 0 || index >= m_vertexCount)
			{
				Debug.LogError("VectorLine.GetColor: index out of range");
				return Color.clear;
			}
			return m_UIVertices[index].color;
		}

		public void SetWidth(float width)
		{
			m_lineWidth = width;
			SetWidth(width, 0, pointsCount);
		}

		public void SetWidth(float width, int index)
		{
			SetWidth(width, index, index);
		}

		public void SetWidth(float width, int startIndex, int endIndex)
		{
			int num = MaxSegmentIndex();
			if (num >= 2 && m_lineWidths.Length == 1)
			{
				Array.Resize(ref m_lineWidths, num);
				for (int i = 0; i < num; i++)
				{
					m_lineWidths[i] = m_lineWidth * 0.5f;
				}
			}
			startIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(num - 1, 0));
			endIndex = Mathf.Clamp(endIndex, 0, Mathf.Max(num - 1, 0));
			for (int j = startIndex; j <= endIndex; j++)
			{
				m_lineWidths[j] = width * 0.5f;
			}
		}

		public void SetWidths(List<float> lineWidths)
		{
			SetWidths(lineWidths.ToArray(), null, lineWidths.Count, true);
		}

		public void SetWidths(List<int> lineWidths)
		{
			SetWidths(null, lineWidths.ToArray(), lineWidths.Count, false);
		}

		public void SetWidths(float[] lineWidths)
		{
			SetWidths(lineWidths, null, lineWidths.Length, true);
		}

		public void SetWidths(int[] lineWidths)
		{
			SetWidths(null, lineWidths, lineWidths.Length, false);
		}

		private void SetWidths(float[] lineWidthsFloat, int[] lineWidthsInt, int arrayLength, bool doFloat)
		{
			if ((doFloat && lineWidthsFloat == null) || (!doFloat && lineWidthsInt == null))
			{
				Debug.LogError("VectorLine.SetWidths: line widths array must not be null");
				return;
			}
			if (pointsCount != m_pointsCount)
			{
				Resize();
			}
			if (m_isPoints)
			{
				if (arrayLength != pointsCount)
				{
					Debug.LogError("VectorLine.SetWidths: line widths array must be the same length as the points array for \"" + name + "\"");
					return;
				}
			}
			else if (WrongArrayLength(arrayLength, FunctionName.SetWidths))
			{
				return;
			}
			if (m_lineWidths.Length != arrayLength)
			{
				Array.Resize(ref m_lineWidths, arrayLength);
			}
			if (doFloat)
			{
				for (int i = 0; i < arrayLength; i++)
				{
					m_lineWidths[i] = lineWidthsFloat[i] * 0.5f;
				}
			}
			else
			{
				for (int j = 0; j < arrayLength; j++)
				{
					m_lineWidths[j] = (float)lineWidthsInt[j] * 0.5f;
				}
			}
		}

		public float GetWidth(int index)
		{
			int num = MaxSegmentIndex();
			if (index < 0 || index >= num)
			{
				Debug.LogError("VectorLine.GetWidth: index out of range...must be >= 0 and < " + num);
				return 0f;
			}
			return m_lineWidths[index] * 2f;
		}

		public static VectorLine SetLine(Color color, params Vector2[] points)
		{
			return SetLine(color, 0f, points);
		}

		public static VectorLine SetLine(Color color, float time, params Vector2[] points)
		{
			if (points.Length < 2)
			{
				Debug.LogError("VectorLine.SetLine needs at least two points");
				return null;
			}
			VectorLine vectorLine = new VectorLine("Line", points, null, 1f, LineType.Continuous, Joins.None);
			vectorLine.color = color;
			if (time > 0f)
			{
				lineManager.DisableLine(vectorLine, time);
			}
			vectorLine.Draw();
			return vectorLine;
		}

		public static VectorLine SetLine(Color color, params Vector3[] points)
		{
			return SetLine(color, 0f, points);
		}

		public static VectorLine SetLine(Color color, float time, params Vector3[] points)
		{
			if (points.Length < 2)
			{
				Debug.LogError("VectorLine.SetLine needs at least two points");
				return null;
			}
			VectorLine vectorLine = new VectorLine("SetLine", points, null, 1f, LineType.Continuous, Joins.None);
			vectorLine.color = color;
			if (time > 0f)
			{
				lineManager.DisableLine(vectorLine, time);
			}
			vectorLine.Draw();
			return vectorLine;
		}

		public static VectorLine SetLine3D(Color color, params Vector3[] points)
		{
			return SetLine3D(color, 0f, points);
		}

		public static VectorLine SetLine3D(Color color, float time, params Vector3[] points)
		{
			if (points.Length < 2)
			{
				Debug.LogError("VectorLine.SetLine3D needs at least two points");
				return null;
			}
			VectorLine vectorLine = new VectorLine("SetLine3D", points, null, 1f, LineType.Continuous, Joins.None);
			vectorLine.color = color;
			vectorLine.Draw3DAuto(time);
			return vectorLine;
		}

		public static VectorLine SetRay(Color color, Vector3 origin, Vector3 direction)
		{
			return SetRay(color, 0f, origin, direction);
		}

		public static VectorLine SetRay(Color color, float time, Vector3 origin, Vector3 direction)
		{
			VectorLine vectorLine = new VectorLine("SetRay", new Vector3[2]
			{
				origin,
				new Ray(origin, direction).GetPoint(direction.magnitude)
			}, null, 1f, LineType.Continuous, Joins.None);
			vectorLine.color = color;
			if (time > 0f)
			{
				lineManager.DisableLine(vectorLine, time);
			}
			vectorLine.Draw();
			return vectorLine;
		}

		public static VectorLine SetRay3D(Color color, Vector3 origin, Vector3 direction)
		{
			return SetRay3D(color, 0f, origin, direction);
		}

		public static VectorLine SetRay3D(Color color, float time, Vector3 origin, Vector3 direction)
		{
			VectorLine vectorLine = new VectorLine("SetRay3D", new Vector3[2]
			{
				origin,
				new Ray(origin, direction).GetPoint(direction.magnitude)
			}, null, 1f, LineType.Continuous, Joins.None);
			vectorLine.color = color;
			vectorLine.Draw3DAuto(time);
			return vectorLine;
		}

		private bool CheckLine(bool draw3D)
		{
			if (m_joins == Joins.Fill)
			{
				DrawFill();
			}
			if (m_capType != EndCap.None)
			{
				DrawEndCap(draw3D);
			}
			if (m_continuousTexture)
			{
				SetContinuousTexture();
			}
			return true;
		}

		private void DrawFill()
		{
			int end = 0;
			int start;
			SetupDrawStartEnd(out start, out end, false);
			bool flag = false;
			if (start != end && ((m_is2D && Approximately(m_points2[start], m_points2[end])) || (!m_is2D && Approximately(m_points3[start], m_points3[end]))))
			{
				flag = true;
			}
			start = Mathf.Max(0, --start);
			start *= 4;
			end *= 4;
			int i;
			for (i = start; i < end - 4; i += 4)
			{
				if (m_UIVertices[i + 4].position.x == m_UIVertices[i + 7].position.x && m_UIVertices[i + 4].position.y == m_fillVertices[i + 7].position.y)
				{
					m_fillVertices[i].position = v3zero;
					m_fillVertices[i + 3].position = v3zero;
					m_fillVertices[i + 2].position = v3zero;
					m_fillVertices[i + 1].position = v3zero;
					i += 4;
				}
				else
				{
					m_fillVertices[i].position = m_UIVertices[i + 1].position;
					m_fillVertices[i + 3].position = m_UIVertices[i + 7].position;
					m_fillVertices[i + 2].position = m_UIVertices[i + 2].position;
					m_fillVertices[i + 1].position = m_UIVertices[i + 4].position;
					m_fillVertices[i].color = m_UIVertices[i + 1].color;
					m_fillVertices[i + 3].color = m_UIVertices[i + 7].color;
					m_fillVertices[i + 2].color = m_UIVertices[i + 2].color;
					m_fillVertices[i + 1].color = m_UIVertices[i + 4].color;
				}
			}
			if (flag && end > start && i < m_fillVertices.Length)
			{
				if (m_UIVertices[start].position.x == m_UIVertices[start + 3].position.x && m_UIVertices[start].position.y == m_UIVertices[start + 3].position.y)
				{
					m_fillVertices[i].position = v3zero;
					m_fillVertices[i + 3].position = v3zero;
					m_fillVertices[i + 2].position = v3zero;
					m_fillVertices[i + 1].position = v3zero;
				}
				else
				{
					m_fillVertices[i].position = m_UIVertices[end - 3].position;
					m_fillVertices[i + 3].position = m_UIVertices[start + 3].position;
					m_fillVertices[i + 2].position = m_UIVertices[end - 2].position;
					m_fillVertices[i + 1].position = m_UIVertices[start].position;
					m_fillVertices[i].color = m_UIVertices[end - 3].color;
					m_fillVertices[i + 3].color = m_UIVertices[start + 3].color;
					m_fillVertices[i + 2].color = m_UIVertices[end - 2].color;
					m_fillVertices[i + 1].color = m_UIVertices[start].color;
				}
			}
			if (m_useNormals)
			{
				for (i = start; i < end - 4; i += 4)
				{
					m_fillVertices[i].normal = m_UIVertices[i + 1].normal;
					m_fillVertices[i + 3].normal = m_UIVertices[i + 7].normal;
					m_fillVertices[i + 2].normal = m_UIVertices[i + 2].normal;
					m_fillVertices[i + 1].normal = m_UIVertices[i + 4].normal;
				}
				if (flag)
				{
					m_fillVertices[i].normal = m_UIVertices[end - 3].normal;
					m_fillVertices[i + 3].normal = m_UIVertices[start + 3].normal;
					m_fillVertices[i + 2].normal = m_UIVertices[end - 2].normal;
					m_fillVertices[i + 1].normal = m_UIVertices[start].normal;
				}
			}
			if (m_useTangents)
			{
				for (i = start; i < end - 4; i += 4)
				{
					m_fillVertices[i].tangent = m_UIVertices[i + 1].tangent;
					m_fillVertices[i + 3].tangent = m_UIVertices[i + 7].tangent;
					m_fillVertices[i + 2].tangent = m_UIVertices[i + 2].tangent;
					m_fillVertices[i + 1].tangent = m_UIVertices[i + 4].tangent;
				}
				if (flag)
				{
					m_fillVertices[i].tangent = m_UIVertices[end - 3].tangent;
					m_fillVertices[i + 3].tangent = m_UIVertices[start + 3].tangent;
					m_fillVertices[i + 2].tangent = m_UIVertices[end - 2].tangent;
					m_fillVertices[i + 1].tangent = m_UIVertices[start].tangent;
				}
			}
			m_fillVertexCount = m_vertexCount - adjustEnd * 4 - ((!flag) ? 4 : 0);
			m_fillRenderer.SetVertices(m_fillVertices, m_active ? m_fillVertexCount : 0);
		}

		private void DrawEndCap(bool draw3D)
		{
			if (m_capType <= EndCap.Mirror)
			{
				int num = m_drawStart * 4;
				int num2 = ((m_lineWidths.Length > 1) ? m_drawStart : 0);
				if (!m_continuous)
				{
					num2 /= 2;
					num /= 2;
				}
				if (!draw3D)
				{
					Vector3 vector = (m_UIVertices[num].position - m_UIVertices[num + 1].position).normalized * m_lineWidths[num2] * 2f * capDictionary[m_endCap].ratio1;
					Vector3 vector2 = vector * capDictionary[m_endCap].offset1;
					m_capVertices[0].position = m_UIVertices[num].position + vector + vector2;
					m_capVertices[3].position = m_UIVertices[num + 3].position + vector + vector2;
					m_UIVertices[num].position += vector2;
					m_UIVertices[num + 3].position += vector2;
				}
				else
				{
					Vector3 vector3 = (m_screenPoints[num] - m_screenPoints[num + 1]).normalized * m_lineWidths[num2] * 2f * capDictionary[m_endCap].ratio1;
					Vector3 vector4 = vector3 * capDictionary[m_endCap].offset1;
					m_capVertices[0].position = cam3D.ScreenToWorldPoint(m_screenPoints[num] + vector3 + vector4);
					m_capVertices[3].position = cam3D.ScreenToWorldPoint(m_screenPoints[num + 3] + vector3 + vector4);
					m_UIVertices[num].position = cam3D.ScreenToWorldPoint(m_screenPoints[num] + vector4);
					m_UIVertices[num + 3].position = cam3D.ScreenToWorldPoint(m_screenPoints[num + 3] + vector4);
				}
				m_capVertices[2].position = m_UIVertices[num + 3].position;
				m_capVertices[1].position = m_UIVertices[num].position;
				if (capDictionary[m_endCap].scale1 != 1f)
				{
					ScaleCapVertices(0, capDictionary[m_endCap].scale1, (m_capVertices[1].position + m_capVertices[2].position) / 2f);
				}
			}
			if (m_capType >= EndCap.Both)
			{
				int num3 = m_drawEnd;
				if (m_continuous)
				{
					if (m_drawEnd == m_pointsCount)
					{
						num3--;
					}
				}
				else if (num3 < m_pointsCount)
				{
					num3++;
				}
				int num4 = num3 * 4;
				int num5 = ((m_lineWidths.Length > 1) ? (num3 - 1) : 0);
				if (num5 < 0)
				{
					num5 = 0;
				}
				if (!m_continuous)
				{
					num5 /= 2;
					num4 /= 2;
				}
				if (num4 < 4)
				{
					num4 = 4;
				}
				if (!draw3D)
				{
					Vector3 vector5 = (m_UIVertices[num4 - 2].position - m_UIVertices[num4 - 1].position).normalized * m_lineWidths[num5] * 2f * capDictionary[m_endCap].ratio2;
					Vector3 vector6 = vector5 * capDictionary[m_endCap].offset2;
					m_capVertices[6].position = m_UIVertices[num4 - 2].position + vector5 + vector6;
					m_capVertices[5].position = m_UIVertices[num4 - 3].position + vector5 + vector6;
					m_UIVertices[num4 - 3].position += vector6;
					m_UIVertices[num4 - 2].position += vector6;
				}
				else
				{
					Vector3 vector7 = (m_screenPoints[num4 - 2] - m_screenPoints[num4 - 1]).normalized * m_lineWidths[num5] * 2f * capDictionary[m_endCap].ratio2;
					Vector3 vector8 = vector7 * capDictionary[m_endCap].offset2;
					m_capVertices[6].position = cam3D.ScreenToWorldPoint(m_screenPoints[num4 - 2] + vector7 + vector8);
					m_capVertices[5].position = cam3D.ScreenToWorldPoint(m_screenPoints[num4 - 3] + vector7 + vector8);
					m_UIVertices[num4 - 3].position = cam3D.ScreenToWorldPoint(m_screenPoints[num4 - 3] + vector8);
					m_UIVertices[num4 - 2].position = cam3D.ScreenToWorldPoint(m_screenPoints[num4 - 2] + vector8);
				}
				m_capVertices[4].position = m_UIVertices[num4 - 3].position;
				m_capVertices[7].position = m_UIVertices[num4 - 2].position;
				if (capDictionary[m_endCap].scale2 != 1f)
				{
					ScaleCapVertices(4, capDictionary[m_endCap].scale2, (m_capVertices[4].position + m_capVertices[7].position) / 2f);
				}
			}
			if (m_drawStart > 0 || m_drawEnd < m_pointsCount)
			{
				SetEndCapColors();
			}
		}

		private void ScaleCapVertices(int offset, float scale, Vector3 center)
		{
			m_capVertices[offset].position = (m_capVertices[offset].position - center) * scale + center;
			m_capVertices[1 + offset].position = (m_capVertices[1 + offset].position - center) * scale + center;
			m_capVertices[2 + offset].position = (m_capVertices[2 + offset].position - center) * scale + center;
			m_capVertices[3 + offset].position = (m_capVertices[3 + offset].position - center) * scale + center;
		}

		private void SetContinuousTexture()
		{
			int num = 0;
			float x = 0f;
			SetDistances();
			int num2 = m_distances.Length - 1;
			float num3 = m_distances[num2];
			for (int i = 0; i < num2; i++)
			{
				m_UIVertices[num].uv0.x = x;
				m_UIVertices[num + 1].uv0.x = x;
				x = 1f / (num3 / m_distances[i + 1]);
				m_UIVertices[num + 2].uv0.x = x;
				m_UIVertices[num + 3].uv0.x = x;
				num += 4;
			}
		}

		private void CheckNormals()
		{
			if (m_useNormals && !m_normalsCalculated)
			{
				CalculateNormals();
				m_normalsCalculated = true;
			}
			if (m_useTangents && !m_tangentsCalculated)
			{
				CalculateTangents();
				m_tangentsCalculated = true;
			}
		}

		private bool UseMatrix(out Matrix4x4 thisMatrix)
		{
			if (m_drawTransform != null)
			{
				thisMatrix = m_drawTransform.localToWorldMatrix;
				return true;
			}
			if (m_useMatrix)
			{
				thisMatrix = m_matrix;
				return true;
			}
			thisMatrix = Matrix4x4.identity;
			return false;
		}

		private bool CheckPointCount()
		{
			if (pointsCount < (m_isPoints ? 1 : 2))
			{
				m_canvasRenderer.SetVertices(m_UIVertices, 0);
				if (m_capType != EndCap.None)
				{
					m_capRenderer.SetVertices(m_capVertices, 0);
				}
				if (m_joins == Joins.Fill)
				{
					m_fillRenderer.SetVertices(m_fillVertices, 0);
				}
				m_pointsCount = pointsCount;
				return false;
			}
			return true;
		}

		private void SetupDrawStartEnd(out int start, out int end, bool clearVertices)
		{
			adjustEnd = 0;
			start = 0;
			end = m_pointsCount - 1;
			if (m_drawStart > 0)
			{
				start = m_drawStart;
				if (clearVertices)
				{
					ZeroVertices(0, start);
				}
			}
			if (m_drawEnd < m_pointsCount - 1)
			{
				end = m_drawEnd;
				if (end < 0)
				{
					end = 0;
				}
				if (!m_continuous && !m_isPoints)
				{
					adjustEnd += (m_pointsCount - end) / 2;
				}
				else
				{
					adjustEnd += m_pointsCount - 1 - end;
				}
			}
			if (m_endPointsUpdate > 0)
			{
				start = Mathf.Max(0, end - m_endPointsUpdate);
			}
		}

		private void ZeroVertices(int startIndex, int endIndex)
		{
			if (m_continuous)
			{
				startIndex *= 4;
				endIndex *= 4;
				if (endIndex > m_vertexCount)
				{
					endIndex -= 4;
				}
				for (int i = startIndex; i < endIndex; i += 4)
				{
					m_UIVertices[i].position = v3zero;
					m_UIVertices[i + 1].position = v3zero;
					m_UIVertices[i + 2].position = v3zero;
					m_UIVertices[i + 3].position = v3zero;
				}
				if (m_joins == Joins.Fill && m_fillVertices != null)
				{
					for (int j = startIndex; j < endIndex; j += 4)
					{
						m_fillVertices[j].position = v3zero;
						m_fillVertices[j + 1].position = v3zero;
						m_fillVertices[j + 2].position = v3zero;
						m_fillVertices[j + 3].position = v3zero;
					}
				}
				return;
			}
			startIndex *= 2;
			endIndex *= 2;
			for (int k = startIndex; k < endIndex; k += 2)
			{
				m_UIVertices[k].position = v3zero;
				m_UIVertices[k + 1].position = v3zero;
			}
			if (m_joins == Joins.Fill)
			{
				for (int l = startIndex; l < endIndex; l += 2)
				{
					m_fillVertices[l].position = v3zero;
					m_fillVertices[l + 1].position = v3zero;
				}
			}
		}

		public void Draw()
		{
			if (!m_active)
			{
				return;
			}
			if (!m_on2DCanvas)
			{
				m_vectorObject.transform.SetParent(m_canvases[m_canvasID].transform, false);
				m_on2DCanvas = true;
			}
			if (!CheckPointCount() || m_lineWidths == null)
			{
				return;
			}
			if (pointsCount != m_pointsCount)
			{
				Resize();
			}
			if (m_isPoints)
			{
				DrawPoints();
				return;
			}
			if (smoothWidth && m_lineWidths.Length == 1 && pointsCount > 2)
			{
				Debug.LogError("VectorLine.Draw called with smooth line widths for \"" + name + "\", but VectorLine.SetWidths has not been used");
				return;
			}
			Matrix4x4 thisMatrix;
			bool useTransformMatrix = UseMatrix(out thisMatrix);
			int start = 0;
			int end = 0;
			SetupDrawStartEnd(out start, out end, true);
			if (m_is2D)
			{
				Line2D(start, end, thisMatrix, useTransformMatrix);
			}
			else
			{
				Line3D(start, end, thisMatrix, useTransformMatrix);
			}
			CheckNormals();
			if (CheckLine(false))
			{
				if (m_useTextureScale)
				{
					SetTextureScale(false);
				}
				m_canvasRenderer.SetVertices(m_UIVertices, m_active ? GetVertexCount() : 0);
				if (m_collider)
				{
					SetCollider(true);
				}
			}
		}

		private void Line2D(int start, int end, Matrix4x4 thisMatrix, bool useTransformMatrix)
		{
			Vector3 vector = v3zero;
			Vector3 vector2 = v3zero;
			Vector3 vector3 = v3zero;
			Vector3 vector4 = v3zero;
			Vector2 vector5 = new Vector2(Screen.width, Screen.height);
			int num = 0;
			int num2 = 0;
			int widthIdx = 0;
			int widthIdxAdd = 0;
			if (m_lineWidths.Length > 1)
			{
				widthIdx = start;
				widthIdxAdd = 1;
			}
			if (m_continuous)
			{
				num = 1;
				num2 = start * 4;
			}
			else
			{
				num = 2;
				widthIdx /= 2;
				num2 = start * 2;
			}
			for (int i = start; i < end; i += num)
			{
				if (useTransformMatrix)
				{
					vector = thisMatrix.MultiplyPoint3x4(m_points2[i]);
					vector2 = thisMatrix.MultiplyPoint3x4(m_points2[i + 1]);
				}
				else
				{
					vector.x = m_points2[i].x;
					vector.y = m_points2[i].y;
					vector2.x = m_points2[i + 1].x;
					vector2.y = m_points2[i + 1].y;
				}
				if (m_viewportDraw)
				{
					vector.x *= vector5.x;
					vector.y *= vector5.y;
					vector2.x *= vector5.x;
					vector2.y *= vector5.y;
				}
				if (vector.x == vector2.x && vector.y == vector2.y)
				{
					SkipQuad(ref num2, ref widthIdx, ref widthIdxAdd);
					continue;
				}
				if (m_capLength == 0f)
				{
					vector4.x = vector2.y - vector.y;
					vector4.y = vector.x - vector2.x;
					float num3 = 1f / (float)Math.Sqrt(vector4.x * vector4.x + vector4.y * vector4.y);
					vector4 *= num3 * m_lineWidths[widthIdx];
					m_UIVertices[num2].position.x = vector.x - vector4.x;
					m_UIVertices[num2].position.y = vector.y - vector4.y;
					m_UIVertices[num2 + 3].position.x = vector.x + vector4.x;
					m_UIVertices[num2 + 3].position.y = vector.y + vector4.y;
					if (smoothWidth && i < end - num)
					{
						vector4.x = vector2.y - vector.y;
						vector4.y = vector.x - vector2.x;
						vector4 *= num3 * m_lineWidths[widthIdx + 1];
					}
				}
				else
				{
					vector4.x = vector2.x - vector.x;
					vector4.y = vector2.y - vector.y;
					vector4 *= 1f / (float)Math.Sqrt(vector4.x * vector4.x + vector4.y * vector4.y);
					vector -= vector4 * m_capLength;
					vector2 += vector4 * m_capLength;
					vector3.x = vector4.y;
					vector3.y = 0f - vector4.x;
					vector4 = vector3 * m_lineWidths[widthIdx];
					m_UIVertices[num2].position.x = vector.x - vector4.x;
					m_UIVertices[num2].position.y = vector.y - vector4.y;
					m_UIVertices[num2 + 3].position.x = vector.x + vector4.x;
					m_UIVertices[num2 + 3].position.y = vector.y + vector4.y;
					if (smoothWidth && i < end - num)
					{
						vector4 = vector3 * m_lineWidths[widthIdx + 1];
					}
				}
				m_UIVertices[num2 + 2].position.x = vector2.x + vector4.x;
				m_UIVertices[num2 + 2].position.y = vector2.y + vector4.y;
				m_UIVertices[num2 + 1].position.x = vector2.x - vector4.x;
				m_UIVertices[num2 + 1].position.y = vector2.y - vector4.y;
				num2 += 4;
				widthIdx += widthIdxAdd;
			}
			if (m_joins != Joins.Weld)
			{
				return;
			}
			if (m_continuous)
			{
				WeldJoins(start * 4 + ((start == 0) ? 4 : 0), end * 4, Approximately(m_points2[0], m_points2[m_pointsCount - 1]));
				return;
			}
			if ((end & 1) == 0)
			{
				end--;
			}
			WeldJoinsDiscrete(start + 1, end, Approximately(m_points2[0], m_points2[m_pointsCount - 1]));
		}

		private void Line3D(int start, int end, Matrix4x4 thisMatrix, bool useTransformMatrix)
		{
			if (!cam3D)
			{
				SetCamera3D();
				if (!cam3D)
				{
					Debug.LogError("No camera available...use VectorLine.SetCamera3D to assign a camera");
					return;
				}
			}
			Vector3 vector = v3zero;
			Vector3 vector2 = v3zero;
			Vector3 vector3 = v3zero;
			float num = 0f;
			int widthIdx = 0;
			int widthIdxAdd = 0;
			if (m_lineWidths.Length > 1)
			{
				widthIdx = start;
				widthIdxAdd = 1;
			}
			int idx = start * 2;
			int num2 = 2;
			if (m_continuous)
			{
				vector2 = ((!useTransformMatrix) ? cam3D.WorldToScreenPoint(m_points3[start]) : cam3D.WorldToScreenPoint(thisMatrix.MultiplyPoint3x4(m_points3[start])));
				idx = start * 4;
				num2 = 1;
			}
			float num3 = Screen.width * 2;
			float num4 = Screen.height * 2;
			for (int i = start; i < end; i += num2)
			{
				if (m_continuous)
				{
					vector.x = vector2.x;
					vector.y = vector2.y;
					vector.z = vector2.z;
					vector2 = ((!useTransformMatrix) ? cam3D.WorldToScreenPoint(m_points3[i + 1]) : cam3D.WorldToScreenPoint(thisMatrix.MultiplyPoint3x4(m_points3[i + 1])));
				}
				else if (useTransformMatrix)
				{
					vector = cam3D.WorldToScreenPoint(thisMatrix.MultiplyPoint3x4(m_points3[i]));
					vector2 = cam3D.WorldToScreenPoint(thisMatrix.MultiplyPoint3x4(m_points3[i + 1]));
				}
				else
				{
					vector = cam3D.WorldToScreenPoint(m_points3[i]);
					vector2 = cam3D.WorldToScreenPoint(m_points3[i + 1]);
				}
				if ((vector.x == vector2.x && vector.y == vector2.y) || (vector.z < 0f && vector2.z < 0f) || (vector.x > num3 && vector.z < 0f) || (vector2.x > num3 && vector2.z < 0f) || (vector.y > num4 && vector.z < 0f) || (vector2.y > num4 && vector2.z < 0f))
				{
					SkipQuad(ref idx, ref widthIdx, ref widthIdxAdd);
					continue;
				}
				vector3.x = vector2.y - vector.y;
				vector3.y = vector.x - vector2.x;
				num = 1f / (float)Math.Sqrt(vector3.x * vector3.x + vector3.y * vector3.y);
				vector3.x *= num * m_lineWidths[widthIdx];
				vector3.y *= num * m_lineWidths[widthIdx];
				m_UIVertices[idx].position.x = vector.x - vector3.x;
				m_UIVertices[idx].position.y = vector.y - vector3.y;
				m_UIVertices[idx + 3].position.x = vector.x + vector3.x;
				m_UIVertices[idx + 3].position.y = vector.y + vector3.y;
				if (smoothWidth && i < end - num2)
				{
					vector3.x = vector2.y - vector.y;
					vector3.y = vector.x - vector2.x;
					vector3.x *= num * m_lineWidths[widthIdx + 1];
					vector3.y *= num * m_lineWidths[widthIdx + 1];
				}
				m_UIVertices[idx + 2].position.x = vector2.x + vector3.x;
				m_UIVertices[idx + 2].position.y = vector2.y + vector3.y;
				m_UIVertices[idx + 1].position.x = vector2.x - vector3.x;
				m_UIVertices[idx + 1].position.y = vector2.y - vector3.y;
				idx += 4;
				widthIdx += widthIdxAdd;
			}
			if (m_joins != Joins.Weld)
			{
				return;
			}
			if (m_continuous)
			{
				WeldJoins(start * 4 + ((start == 0) ? 4 : 0), end * 4, Approximately(m_points3[0], m_points3[m_pointsCount - 1]));
				return;
			}
			if ((end & 1) == 0)
			{
				end--;
			}
			WeldJoinsDiscrete(start + 1, end, Approximately(m_points3[0], m_points3[m_pointsCount - 1]));
		}

		public void Draw3D()
		{
			if (!m_active)
			{
				return;
			}
			if (m_is2D)
			{
				Debug.LogError("VectorLine.Draw3D can only be used with a Vector3 array, which \"" + name + "\" doesn't have");
			}
			else
			{
				if (!CheckPointCount() || m_lineWidths == null)
				{
					return;
				}
				if (m_on2DCanvas)
				{
					SetCanvas3D(m_canvasID);
					m_vectorObject.transform.SetParent(m_canvases3D[m_canvasID].transform, false);
					m_on2DCanvas = false;
				}
				if (pointsCount != m_pointsCount)
				{
					Resize();
				}
				if (m_isPoints)
				{
					DrawPoints3D();
					return;
				}
				if (smoothWidth && m_lineWidths.Length == 1 && m_pointsCount > 2)
				{
					Debug.LogError("VectorLine.Draw3D called with smooth line widths for \"" + name + "\", but VectorLine.SetWidths has not been used");
					return;
				}
				int start = 0;
				int end = 0;
				int num = 0;
				int widthIdx = 0;
				SetupDrawStartEnd(out start, out end, true);
				Matrix4x4 thisMatrix;
				bool flag = UseMatrix(out thisMatrix);
				int num2 = 0;
				int widthIdxAdd = 0;
				if (m_lineWidths.Length > 1)
				{
					widthIdx = start;
					widthIdxAdd = 1;
				}
				if (m_continuous)
				{
					num = 1;
					num2 = start * 4;
				}
				else
				{
					widthIdx /= 2;
					num = 2;
					num2 = start * 2;
				}
				Vector3 vector = v3zero;
				Vector3 vector2 = v3zero;
				Vector3 vector3 = v3zero;
				Vector3 vector4 = v3zero;
				float num3 = Screen.width * 2;
				float num4 = Screen.height * 2;
				for (int i = start; i < end; i += num)
				{
					if (flag)
					{
						vector3 = cam3D.WorldToScreenPoint(thisMatrix.MultiplyPoint3x4(m_points3[i]));
						vector4 = cam3D.WorldToScreenPoint(thisMatrix.MultiplyPoint3x4(m_points3[i + 1]));
					}
					else
					{
						vector3 = cam3D.WorldToScreenPoint(m_points3[i]);
						vector4 = cam3D.WorldToScreenPoint(m_points3[i + 1]);
					}
					if ((vector3.x == vector4.x && vector3.y == vector4.y) || (vector3.z < 0f && vector4.z < 0f) || (vector3.x > num3 && vector3.z < 0f) || (vector4.x > num3 && vector4.z < 0f) || (vector3.y > num4 && vector3.z < 0f) || (vector4.y > num4 && vector4.z < 0f))
					{
						SkipQuad3D(ref num2, ref widthIdx, ref widthIdxAdd);
						continue;
					}
					vector2.x = vector4.y - vector3.y;
					vector2.y = vector3.x - vector4.x;
					vector = vector2 / (float)Math.Sqrt(vector2.x * vector2.x + vector2.y * vector2.y);
					vector2.x = vector.x * m_lineWidths[widthIdx];
					vector2.y = vector.y * m_lineWidths[widthIdx];
					m_screenPoints[num2].x = vector3.x - vector2.x;
					m_screenPoints[num2].y = vector3.y - vector2.y;
					m_screenPoints[num2].z = vector3.z - vector2.z;
					m_screenPoints[num2 + 3].x = vector3.x + vector2.x;
					m_screenPoints[num2 + 3].y = vector3.y + vector2.y;
					m_screenPoints[num2 + 3].z = vector3.z + vector2.z;
					m_UIVertices[num2].position = cam3D.ScreenToWorldPoint(m_screenPoints[num2]);
					m_UIVertices[num2 + 3].position = cam3D.ScreenToWorldPoint(m_screenPoints[num2 + 3]);
					if (smoothWidth && i < end - num)
					{
						vector2.x = vector.x * m_lineWidths[widthIdx + 1];
						vector2.y = vector.y * m_lineWidths[widthIdx + 1];
					}
					m_screenPoints[num2 + 2].x = vector4.x + vector2.x;
					m_screenPoints[num2 + 2].y = vector4.y + vector2.y;
					m_screenPoints[num2 + 2].z = vector4.z + vector2.z;
					m_screenPoints[num2 + 1].x = vector4.x - vector2.x;
					m_screenPoints[num2 + 1].y = vector4.y - vector2.y;
					m_screenPoints[num2 + 1].z = vector4.z - vector2.z;
					m_UIVertices[num2 + 2].position = cam3D.ScreenToWorldPoint(m_screenPoints[num2 + 2]);
					m_UIVertices[num2 + 1].position = cam3D.ScreenToWorldPoint(m_screenPoints[num2 + 1]);
					num2 += 4;
					widthIdx += widthIdxAdd;
				}
				if (m_joins == Joins.Weld)
				{
					if (m_continuous)
					{
						WeldJoins3D(start * 4 + ((start == 0) ? 4 : 0), end * 4, Approximately(m_points3[0], m_points3[m_pointsCount - 1]));
					}
					else
					{
						if ((end & 1) == 0)
						{
							end--;
						}
						WeldJoinsDiscrete3D(start + 1, end, Approximately(m_points3[0], m_points3[m_pointsCount - 1]));
					}
				}
				CheckNormals();
				if (CheckLine(true))
				{
					if (m_useTextureScale)
					{
						SetTextureScale(false);
					}
					m_canvasRenderer.SetVertices(m_UIVertices, m_active ? GetVertexCount() : 0);
					if (m_collider)
					{
						SetCollider(false);
					}
				}
			}
		}

		private void DrawPoints()
		{
			if (!m_is2D && !cam3D)
			{
				SetCamera3D();
				if (!cam3D)
				{
					Debug.LogError("No camera available...use VectorLine.SetCamera3D to assign a camera");
					return;
				}
			}
			Matrix4x4 thisMatrix;
			bool flag = UseMatrix(out thisMatrix);
			int start;
			int end;
			SetupDrawStartEnd(out start, out end, true);
			Vector2 vector = new Vector2(Screen.width, Screen.height);
			int idx = start * 4;
			int widthIdxAdd = ((m_lineWidths.Length > 1) ? 1 : 0);
			int widthIdx = start;
			Vector3 vector2 = new Vector3(m_lineWidths[0], m_lineWidths[0], 0f);
			Vector3 vector3 = new Vector3(0f - m_lineWidths[0], m_lineWidths[0], 0f);
			if (!m_is2D)
			{
				for (int i = start; i <= end; i++)
				{
					Vector3 vector4 = ((!flag) ? cam3D.WorldToScreenPoint(m_points3[i]) : cam3D.WorldToScreenPoint(thisMatrix.MultiplyPoint3x4(m_points3[i])));
					if (vector4.z < 0f)
					{
						SkipQuad(ref idx, ref widthIdx, ref widthIdxAdd);
						continue;
					}
					if (widthIdxAdd != 0)
					{
						vector2.x = (vector2.y = (vector3.y = m_lineWidths[widthIdx]));
						vector3.x = 0f - m_lineWidths[widthIdx];
						widthIdx++;
					}
					m_UIVertices[idx].position.x = vector4.x + vector3.x;
					m_UIVertices[idx].position.y = vector4.y + vector3.y;
					m_UIVertices[idx + 3].position.x = vector4.x - vector2.x;
					m_UIVertices[idx + 3].position.y = vector4.y - vector2.y;
					m_UIVertices[idx + 1].position.x = vector4.x + vector2.x;
					m_UIVertices[idx + 1].position.y = vector4.y + vector2.y;
					m_UIVertices[idx + 2].position.x = vector4.x - vector3.x;
					m_UIVertices[idx + 2].position.y = vector4.y - vector3.y;
					idx += 4;
				}
			}
			else
			{
				for (int j = start; j <= end; j++)
				{
					Vector3 vector4 = ((!flag) ? ((Vector3)m_points2[j]) : thisMatrix.MultiplyPoint3x4(m_points2[j]));
					if (m_viewportDraw)
					{
						vector4.x *= vector.x;
						vector4.y *= vector.y;
					}
					if (widthIdxAdd != 0)
					{
						vector2.x = (vector2.y = (vector3.y = m_lineWidths[widthIdx]));
						vector3.x = 0f - m_lineWidths[widthIdx];
						widthIdx++;
					}
					m_UIVertices[idx].position.x = vector4.x + vector3.x;
					m_UIVertices[idx].position.y = vector4.y + vector3.y;
					m_UIVertices[idx + 3].position.x = vector4.x - vector2.x;
					m_UIVertices[idx + 3].position.y = vector4.y - vector2.y;
					m_UIVertices[idx + 1].position.x = vector4.x + vector2.x;
					m_UIVertices[idx + 1].position.y = vector4.y + vector2.y;
					m_UIVertices[idx + 2].position.x = vector4.x - vector3.x;
					m_UIVertices[idx + 2].position.y = vector4.y - vector3.y;
					idx += 4;
				}
			}
			CheckNormals();
			m_canvasRenderer.SetVertices(m_UIVertices, m_active ? GetVertexCount() : 0);
		}

		private void DrawPoints3D()
		{
			Matrix4x4 thisMatrix;
			bool flag = UseMatrix(out thisMatrix);
			int start = 0;
			int end = 0;
			int widthIdx = 0;
			SetupDrawStartEnd(out start, out end, true);
			int idx = start * 4;
			int widthIdxAdd = 0;
			if (m_lineWidths.Length > 1)
			{
				widthIdx = start;
				widthIdxAdd = 1;
			}
			Vector3 vector = v3zero;
			Vector3 vector2 = v3zero;
			Vector3 vector3 = v3zero;
			for (int i = start; i <= end; i++)
			{
				vector = ((!flag) ? cam3D.WorldToScreenPoint(m_points3[i]) : cam3D.WorldToScreenPoint(thisMatrix.MultiplyPoint3x4(m_points3[i])));
				if (vector.z < 0f)
				{
					SkipQuad(ref idx, ref widthIdx, ref widthIdxAdd);
					continue;
				}
				vector2.x = (vector2.y = (vector3.y = m_lineWidths[widthIdx]));
				vector3.x = 0f - m_lineWidths[widthIdx];
				m_UIVertices[idx].position = cam3D.ScreenToWorldPoint(vector + vector3);
				m_UIVertices[idx + 3].position = cam3D.ScreenToWorldPoint(vector - vector2);
				m_UIVertices[idx + 1].position = cam3D.ScreenToWorldPoint(vector + vector2);
				m_UIVertices[idx + 2].position = cam3D.ScreenToWorldPoint(vector - vector3);
				idx += 4;
				widthIdx += widthIdxAdd;
			}
			CheckNormals();
			m_canvasRenderer.SetVertices(m_UIVertices, m_active ? GetVertexCount() : 0);
		}

		private void SkipQuad(ref int idx, ref int widthIdx, ref int widthIdxAdd)
		{
			m_UIVertices[idx].position = v3zero;
			m_UIVertices[idx + 1].position = v3zero;
			m_UIVertices[idx + 2].position = v3zero;
			m_UIVertices[idx + 3].position = v3zero;
			idx += 4;
			widthIdx += widthIdxAdd;
		}

		private void SkipQuad3D(ref int idx, ref int widthIdx, ref int widthIdxAdd)
		{
			m_UIVertices[idx].position = v3zero;
			m_UIVertices[idx + 1].position = v3zero;
			m_UIVertices[idx + 2].position = v3zero;
			m_UIVertices[idx + 3].position = v3zero;
			m_screenPoints[idx] = v3zero;
			m_screenPoints[idx + 1] = v3zero;
			m_screenPoints[idx + 2] = v3zero;
			m_screenPoints[idx + 3] = v3zero;
			idx += 4;
			widthIdx += widthIdxAdd;
		}

		private void WeldJoins(int start, int end, bool connectFirstAndLast)
		{
			if (connectFirstAndLast)
			{
				SetIntersectionPoint(m_vertexCount - 4, m_vertexCount - 3, 0, 1);
				SetIntersectionPoint(m_vertexCount - 1, m_vertexCount - 2, 3, 2);
			}
			for (int i = start; i < end; i += 4)
			{
				SetIntersectionPoint(i - 4, i - 3, i, i + 1);
				SetIntersectionPoint(i - 1, i - 2, i + 3, i + 2);
			}
		}

		private void WeldJoinsDiscrete(int start, int end, bool connectFirstAndLast)
		{
			if (connectFirstAndLast)
			{
				SetIntersectionPoint(m_vertexCount - 4, m_vertexCount - 3, 0, 1);
				SetIntersectionPoint(m_vertexCount - 1, m_vertexCount - 2, 3, 2);
			}
			int num = (start + 1) / 2 * 4;
			if (m_is2D)
			{
				for (int i = start; i < end; i += 2)
				{
					if (m_points2[i] == m_points2[i + 1])
					{
						SetIntersectionPoint(num - 4, num - 3, num, num + 1);
						SetIntersectionPoint(num - 1, num - 2, num + 3, num + 2);
					}
					num += 4;
				}
				return;
			}
			for (int j = start; j < end; j += 2)
			{
				if (m_points3[j] == m_points3[j + 1])
				{
					SetIntersectionPoint(num - 4, num - 3, num, num + 1);
					SetIntersectionPoint(num - 1, num - 2, num + 3, num + 2);
				}
				num += 4;
			}
		}

		private void SetIntersectionPoint(int p1, int p2, int p3, int p4)
		{
			Vector3 position = m_UIVertices[p1].position;
			Vector3 position2 = m_UIVertices[p2].position;
			Vector3 position3 = m_UIVertices[p3].position;
			Vector3 position4 = m_UIVertices[p4].position;
			if ((position.x == position2.x && position.y == position2.y) || (position3.x == position4.x && position3.y == position4.y))
			{
				return;
			}
			float num = (position4.y - position3.y) * (position2.x - position.x) - (position4.x - position3.x) * (position2.y - position.y);
			if (num > -0.005f && num < 0.005f)
			{
				m_UIVertices[p2].position = (position2 + position3) * 0.5f;
				m_UIVertices[p3].position = m_UIVertices[p2].position;
				return;
			}
			float num2 = ((position4.x - position3.x) * (position.y - position3.y) - (position4.y - position3.y) * (position.x - position3.x)) / num;
			Vector3 vector = new Vector3(position.x + num2 * (position2.x - position.x), position.y + num2 * (position2.y - position.y), position.z);
			if (!((vector - position2).sqrMagnitude > m_maxWeldDistance))
			{
				m_UIVertices[p2].position = vector;
				m_UIVertices[p3].position = vector;
			}
		}

		private void WeldJoins3D(int start, int end, bool connectFirstAndLast)
		{
			if (connectFirstAndLast)
			{
				SetIntersectionPoint3D(m_vertexCount - 4, m_vertexCount - 3, 0, 1);
				SetIntersectionPoint3D(m_vertexCount - 1, m_vertexCount - 2, 3, 2);
			}
			for (int i = start; i < end; i += 4)
			{
				SetIntersectionPoint3D(i - 4, i - 3, i, i + 1);
				SetIntersectionPoint3D(i - 1, i - 2, i + 3, i + 2);
			}
		}

		private void WeldJoinsDiscrete3D(int start, int end, bool connectFirstAndLast)
		{
			if (connectFirstAndLast)
			{
				SetIntersectionPoint3D(m_vertexCount - 4, m_vertexCount - 3, 0, 1);
				SetIntersectionPoint3D(m_vertexCount - 1, m_vertexCount - 2, 3, 2);
			}
			int num = (start + 1) / 2 * 4;
			for (int i = start; i < end; i += 2)
			{
				if (m_points3[i] == m_points3[i + 1])
				{
					SetIntersectionPoint3D(num - 4, num - 3, num, num + 1);
					SetIntersectionPoint3D(num - 1, num - 2, num + 3, num + 2);
				}
				num += 4;
			}
		}

		private void SetIntersectionPoint3D(int p1, int p2, int p3, int p4)
		{
			Vector3 vector = m_screenPoints[p1];
			Vector3 vector2 = m_screenPoints[p2];
			Vector3 vector3 = m_screenPoints[p3];
			Vector3 vector4 = m_screenPoints[p4];
			if ((vector.x == vector2.x && vector.y == vector2.y) || (vector3.x == vector4.x && vector3.y == vector4.y))
			{
				return;
			}
			float num = (vector4.y - vector3.y) * (vector2.x - vector.x) - (vector4.x - vector3.x) * (vector2.y - vector.y);
			if (num > -0.005f && num < 0.005f)
			{
				m_UIVertices[p2].position = cam3D.ScreenToWorldPoint((vector2 + vector3) * 0.5f);
				m_UIVertices[p3].position = m_UIVertices[p2].position;
				return;
			}
			float num2 = ((vector4.x - vector3.x) * (vector.y - vector3.y) - (vector4.y - vector3.y) * (vector.x - vector3.x)) / num;
			Vector3 vector5 = new Vector3(vector.x + num2 * (vector2.x - vector.x), vector.y + num2 * (vector2.y - vector.y), vector.z);
			if (!((vector5 - vector2).sqrMagnitude > m_maxWeldDistance))
			{
				m_UIVertices[p2].position = cam3D.ScreenToWorldPoint(vector5);
				m_UIVertices[p3].position = m_UIVertices[p2].position;
			}
		}

		public static void LineManagerCheckDistance()
		{
			lineManager.StartCheckDistance();
		}

		public static void LineManagerDisable()
		{
			lineManager.DisableIfUnused();
		}

		public static void LineManagerEnable()
		{
			lineManager.EnableIfUsed();
		}

		public void Draw3DAuto()
		{
			Draw3DAuto(0f);
		}

		public void Draw3DAuto(float time)
		{
			if (time < 0f)
			{
				time = 0f;
			}
			lineManager.AddLine(this, m_drawTransform, time);
			m_isAutoDrawing = true;
			Draw3D();
		}

		public void StopDrawing3DAuto()
		{
			lineManager.RemoveLine(this);
			m_isAutoDrawing = false;
		}

		private void SetTextureScale(bool updateUIVertices)
		{
			if (pointsCount != m_pointsCount)
			{
				Resize();
			}
			int num = ((!m_continuous) ? pointsCount : (pointsCount - 1));
			int num2 = (m_continuous ? 1 : 2);
			int num3 = 0;
			int num4 = 0;
			int num5 = ((m_lineWidths.Length != 1) ? 1 : 0);
			float num6 = 1f / m_textureScale;
			bool flag = m_drawTransform != null;
			Matrix4x4 matrix4x = ((!flag) ? Matrix4x4.identity : m_drawTransform.localToWorldMatrix);
			Vector2 vector = Vector2.zero;
			Vector2 vector2 = Vector2.zero;
			Vector2 zero = Vector2.zero;
			float num7 = m_textureOffset;
			float num8 = m_capLength * 2f;
			if (m_is2D)
			{
				for (int i = 0; i < num; i += num2)
				{
					if (!m_viewportDraw)
					{
						if (flag)
						{
							vector = matrix4x.MultiplyPoint3x4(m_points2[i]);
							vector2 = matrix4x.MultiplyPoint3x4(m_points2[i + 1]);
						}
						else
						{
							vector.x = m_points2[i].x;
							vector.y = m_points2[i].y;
							vector2.x = m_points2[i + 1].x;
							vector2.y = m_points2[i + 1].y;
						}
					}
					else if (flag)
					{
						vector = matrix4x.MultiplyPoint3x4(new Vector2(m_points2[i].x * (float)Screen.width, m_points2[i].y * (float)Screen.height));
						vector2 = matrix4x.MultiplyPoint3x4(new Vector2(m_points2[i + 1].x * (float)Screen.width, m_points2[i + 1].y * (float)Screen.height));
					}
					else
					{
						vector = new Vector2(m_points2[i].x * (float)Screen.width, m_points2[i].y * (float)Screen.height);
						vector2 = new Vector2(m_points2[i + 1].x * (float)Screen.width, m_points2[i + 1].y * (float)Screen.height);
					}
					zero.x = vector2.x - vector.x;
					zero.y = vector2.y - vector.y;
					float num9 = num6 / (m_lineWidths[num4] * 2f / ((float)Math.Sqrt(zero.x * zero.x + zero.y * zero.y) + num8));
					m_UIVertices[num3].uv0.x = num7;
					m_UIVertices[num3 + 3].uv0.x = num7;
					m_UIVertices[num3 + 2].uv0.x = num9 + num7;
					m_UIVertices[num3 + 1].uv0.x = num9 + num7;
					num3 += 4;
					num7 = (num7 + num9) % 1f;
					num4 += num5;
				}
			}
			else
			{
				for (int j = 0; j < num; j += num2)
				{
					if (flag)
					{
						vector = cam3D.WorldToScreenPoint(matrix4x.MultiplyPoint3x4(m_points3[j]));
						vector2 = cam3D.WorldToScreenPoint(matrix4x.MultiplyPoint3x4(m_points3[j + 1]));
					}
					else
					{
						vector = cam3D.WorldToScreenPoint(m_points3[j]);
						vector2 = cam3D.WorldToScreenPoint(m_points3[j + 1]);
					}
					zero.x = vector.x - vector2.x;
					zero.y = vector.y - vector2.y;
					float num10 = num6 / (m_lineWidths[num4] * 2f / (float)Math.Sqrt(zero.x * zero.x + zero.y * zero.y));
					m_UIVertices[num3].uv0.x = num7;
					m_UIVertices[num3 + 3].uv0.x = num7;
					m_UIVertices[num3 + 2].uv0.x = num10 + num7;
					m_UIVertices[num3 + 1].uv0.x = num10 + num7;
					num3 += 4;
					num7 = (num7 + num10) % 1f;
					num4 += num5;
				}
			}
			if (updateUIVertices)
			{
				m_canvasRenderer.SetVertices(m_UIVertices, m_active ? GetVertexCount() : 0);
			}
		}

		private void ResetTextureScale()
		{
			for (int i = 0; i < m_vertexCount; i += 4)
			{
				m_UIVertices[i].uv0.x = 0f;
				m_UIVertices[i + 3].uv0.x = 0f;
				m_UIVertices[i + 2].uv0.x = 1f;
				m_UIVertices[i + 1].uv0.x = 1f;
			}
		}

		private void SetCollider(bool convertToWorldSpace)
		{
			if (!cam3D)
			{
				SetCamera3D();
				if (!cam3D)
				{
					Debug.LogError("No camera available...use VectorLine.SetCamera3D to assign a camera");
					return;
				}
			}
			if (cam3D.transform.rotation != Quaternion.identity)
			{
				Debug.LogWarning("The line collider will not be correct if the camera is rotated");
			}
			Vector3 position = new Vector3(0f, 0f, 0f - cam3D.transform.position.z);
			int num = drawStart;
			int num2 = drawEnd;
			if (m_continuous)
			{
				EdgeCollider2D edgeCollider2D = m_vectorObject.GetComponent(typeof(EdgeCollider2D)) as EdgeCollider2D;
				Vector2[] array = new Vector2[(num2 - num) * 4 + 1];
				int num3 = 0;
				int num4 = array.Length - 2;
				if (convertToWorldSpace)
				{
					for (int i = num * 4; i < num2 * 4; i += 4)
					{
						position.x = m_UIVertices[i].position.x;
						position.y = m_UIVertices[i].position.y;
						array[num3] = cam3D.ScreenToWorldPoint(position);
						position.x = m_UIVertices[i + 1].position.x;
						position.y = m_UIVertices[i + 1].position.y;
						array[num3 + 1] = cam3D.ScreenToWorldPoint(position);
						position.x = m_UIVertices[i + 3].position.x;
						position.y = m_UIVertices[i + 3].position.y;
						array[num4] = cam3D.ScreenToWorldPoint(position);
						position.x = m_UIVertices[i + 2].position.x;
						position.y = m_UIVertices[i + 2].position.y;
						array[num4 - 1] = cam3D.ScreenToWorldPoint(position);
						num3 += 2;
						num4 -= 2;
					}
				}
				else
				{
					for (int j = num * 4; j < num2 * 4; j += 4)
					{
						array[num3].x = m_UIVertices[j].position.x;
						array[num3].y = m_UIVertices[j].position.y;
						array[num3 + 1].x = m_UIVertices[j + 1].position.x;
						array[num3 + 1].y = m_UIVertices[j + 1].position.y;
						array[num4].x = m_UIVertices[j + 3].position.x;
						array[num4].y = m_UIVertices[j + 3].position.y;
						array[num4 - 1].x = m_UIVertices[j + 2].position.x;
						array[num4 - 1].y = m_UIVertices[j + 2].position.y;
						num3 += 2;
						num4 -= 2;
					}
				}
				array[array.Length - 1] = array[0];
				edgeCollider2D.points = array;
				return;
			}
			PolygonCollider2D polygonCollider2D = m_vectorObject.GetComponent(typeof(PolygonCollider2D)) as PolygonCollider2D;
			Vector2[] array2 = new Vector2[4];
			polygonCollider2D.pathCount = (num2 - num + 1) / 2;
			int num5 = (num2 + 1) / 2 * 4;
			int num6 = 0;
			if (convertToWorldSpace)
			{
				for (int k = num / 2 * 4; k < num5; k += 4)
				{
					position.x = m_UIVertices[k].position.x;
					position.y = m_UIVertices[k].position.y;
					array2[0] = cam3D.ScreenToWorldPoint(position);
					position.x = m_UIVertices[k + 3].position.x;
					position.y = m_UIVertices[k + 3].position.y;
					array2[1] = cam3D.ScreenToWorldPoint(position);
					position.x = m_UIVertices[k + 2].position.x;
					position.y = m_UIVertices[k + 2].position.y;
					array2[2] = cam3D.ScreenToWorldPoint(position);
					position.x = m_UIVertices[k + 1].position.x;
					position.y = m_UIVertices[k + 1].position.y;
					array2[3] = cam3D.ScreenToWorldPoint(position);
					polygonCollider2D.SetPath(num6++, array2);
				}
			}
			else
			{
				for (int l = num / 2 * 4; l < num5; l += 4)
				{
					array2[0].x = m_UIVertices[l].position.x;
					array2[0].y = m_UIVertices[l].position.y;
					array2[1].x = m_UIVertices[l + 3].position.x;
					array2[1].y = m_UIVertices[l + 3].position.y;
					array2[2].x = m_UIVertices[l + 2].position.x;
					array2[2].y = m_UIVertices[l + 2].position.y;
					array2[3].x = m_UIVertices[l + 1].position.x;
					array2[3].y = m_UIVertices[l + 1].position.y;
					polygonCollider2D.SetPath(num6++, array2);
				}
			}
		}

		public static Vector3[] BytesToVector3Array(byte[] lineBytes)
		{
			if (lineBytes.Length % 12 != 0)
			{
				Debug.LogError("VectorLine.BytesToVector3Array: Incorrect input byte length...must be a multiple of 12");
				return null;
			}
			SetupByteBlock();
			Vector3[] array = new Vector3[lineBytes.Length / 12];
			int num = 0;
			for (int i = 0; i < lineBytes.Length; i += 12)
			{
				array[num++] = new Vector3(ConvertToFloat(lineBytes, i), ConvertToFloat(lineBytes, i + 4), ConvertToFloat(lineBytes, i + 8));
			}
			return array;
		}

		public static Vector2[] BytesToVector2Array(byte[] lineBytes)
		{
			if (lineBytes.Length % 8 != 0)
			{
				Debug.LogError("VectorLine.BytesToVector2Array: Incorrect input byte length...must be a multiple of 8");
				return null;
			}
			SetupByteBlock();
			Vector2[] array = new Vector2[lineBytes.Length / 8];
			int num = 0;
			for (int i = 0; i < lineBytes.Length; i += 8)
			{
				array[num++] = new Vector2(ConvertToFloat(lineBytes, i), ConvertToFloat(lineBytes, i + 4));
			}
			return array;
		}

		private static void SetupByteBlock()
		{
			if (byteBlock == null)
			{
				byteBlock = new byte[4];
			}
			if (BitConverter.IsLittleEndian)
			{
				endianDiff1 = 0;
				endianDiff2 = 0;
			}
			else
			{
				endianDiff1 = 3;
				endianDiff2 = 1;
			}
		}

		private static float ConvertToFloat(byte[] bytes, int i)
		{
			byteBlock[endianDiff1] = bytes[i];
			byteBlock[1 + endianDiff2] = bytes[i + 1];
			byteBlock[2 - endianDiff2] = bytes[i + 2];
			byteBlock[3 - endianDiff1] = bytes[i + 3];
			return BitConverter.ToSingle(byteBlock, 0);
		}

		public static void Destroy(ref VectorLine line)
		{
			DestroyLine(ref line);
		}

		public static void Destroy(VectorLine[] lines)
		{
			for (int i = 0; i < lines.Length; i++)
			{
				DestroyLine(ref lines[i]);
			}
		}

		public static void Destroy(List<VectorLine> lines)
		{
			for (int i = 0; i < lines.Count; i++)
			{
				VectorLine line = lines[i];
				DestroyLine(ref line);
			}
		}

		private static void DestroyLine(ref VectorLine line)
		{
			if (line != null)
			{
				UnityEngine.Object.Destroy(line.m_vectorObject);
				if (line.isAutoDrawing)
				{
					line.StopDrawing3DAuto();
				}
				line = null;
			}
		}

		public static void Destroy(ref VectorPoints line)
		{
			DestroyPoints(ref line);
		}

		public static void Destroy(VectorPoints[] lines)
		{
			for (int i = 0; i < lines.Length; i++)
			{
				DestroyPoints(ref lines[i]);
			}
		}

		public static void Destroy(List<VectorPoints> lines)
		{
			for (int i = 0; i < lines.Count; i++)
			{
				VectorPoints line = lines[i];
				DestroyPoints(ref line);
			}
		}

		private static void DestroyPoints(ref VectorPoints line)
		{
			if (line != null)
			{
				UnityEngine.Object.Destroy(line.m_vectorObject);
				if (line.isAutoDrawing)
				{
					line.StopDrawing3DAuto();
				}
				line = null;
			}
		}

		public static void Destroy(ref VectorLine line, GameObject go)
		{
			Destroy(ref line);
			if (go != null)
			{
				UnityEngine.Object.Destroy(go);
			}
		}

		public static void Destroy(ref VectorPoints line, GameObject go)
		{
			Destroy(ref line);
			if (go != null)
			{
				UnityEngine.Object.Destroy(go);
			}
		}

		public void MakeRect(Rect rect)
		{
			MakeRect(new Vector2(rect.x, rect.y), new Vector2(rect.x + rect.width, rect.y + rect.height), 0);
		}

		public void MakeRect(Rect rect, int index)
		{
			MakeRect(new Vector2(rect.x, rect.y), new Vector2(rect.x + rect.width, rect.y + rect.height), index);
		}

		public void MakeRect(Vector3 bottomLeft, Vector3 topRight)
		{
			MakeRect(bottomLeft, topRight, 0);
		}

		public void MakeRect(Vector3 bottomLeft, Vector3 topRight, int index)
		{
			if (m_continuous)
			{
				if (index + 5 > pointsCount)
				{
					if (index == 0)
					{
						Debug.LogError("VectorLine.MakeRect: The length of the array for continuous lines needs to be at least 5 for \"" + name + "\"");
						return;
					}
					Debug.LogError("Calling VectorLine.MakeRect with an index of " + index + " would exceed the length of the Vector2 array for \"" + name + "\"");
				}
				else if (m_is2D)
				{
					m_points2[index] = new Vector2(bottomLeft.x, bottomLeft.y);
					m_points2[index + 1] = new Vector2(topRight.x, bottomLeft.y);
					m_points2[index + 2] = new Vector2(topRight.x, topRight.y);
					m_points2[index + 3] = new Vector2(bottomLeft.x, topRight.y);
					m_points2[index + 4] = new Vector2(bottomLeft.x, bottomLeft.y);
				}
				else
				{
					m_points3[index] = new Vector3(bottomLeft.x, bottomLeft.y, bottomLeft.z);
					m_points3[index + 1] = new Vector3(topRight.x, bottomLeft.y, bottomLeft.z);
					m_points3[index + 2] = new Vector3(topRight.x, topRight.y, topRight.z);
					m_points3[index + 3] = new Vector3(bottomLeft.x, topRight.y, topRight.z);
					m_points3[index + 4] = new Vector3(bottomLeft.x, bottomLeft.y, bottomLeft.z);
				}
			}
			else if (index + 8 > pointsCount)
			{
				if (index == 0)
				{
					Debug.LogError("VectorLine.MakeRect: The length of the array for discrete lines needs to be at least 8 for \"" + name + "\"");
					return;
				}
				Debug.LogError("Calling VectorLine.MakeRect with an index of " + index + " would exceed the length of the Vector2 array for \"" + name + "\"");
			}
			else if (m_is2D)
			{
				m_points2[index] = new Vector2(bottomLeft.x, bottomLeft.y);
				m_points2[index + 1] = new Vector2(topRight.x, bottomLeft.y);
				m_points2[index + 2] = new Vector2(topRight.x, bottomLeft.y);
				m_points2[index + 3] = new Vector2(topRight.x, topRight.y);
				m_points2[index + 4] = new Vector2(topRight.x, topRight.y);
				m_points2[index + 5] = new Vector2(bottomLeft.x, topRight.y);
				m_points2[index + 6] = new Vector2(bottomLeft.x, topRight.y);
				m_points2[index + 7] = new Vector2(bottomLeft.x, bottomLeft.y);
			}
			else
			{
				m_points3[index] = new Vector3(bottomLeft.x, bottomLeft.y, bottomLeft.z);
				m_points3[index + 1] = new Vector3(topRight.x, bottomLeft.y, bottomLeft.z);
				m_points3[index + 2] = new Vector3(topRight.x, bottomLeft.y, bottomLeft.z);
				m_points3[index + 3] = new Vector3(topRight.x, topRight.y, topRight.z);
				m_points3[index + 4] = new Vector3(topRight.x, topRight.y, topRight.z);
				m_points3[index + 5] = new Vector3(bottomLeft.x, topRight.y, topRight.z);
				m_points3[index + 6] = new Vector3(bottomLeft.x, topRight.y, topRight.z);
				m_points3[index + 7] = new Vector3(bottomLeft.x, bottomLeft.y, bottomLeft.z);
			}
		}

		public void MakeRoundedRect(Rect rect, float cornerRadius, int cornerSegments)
		{
			MakeRoundedRect(new Vector2(rect.x, rect.y), new Vector2(rect.x + rect.width, rect.y + rect.height), cornerRadius, cornerSegments, 0);
		}

		public void MakeRoundedRect(Rect rect, float cornerRadius, int cornerSegments, int index)
		{
			MakeRoundedRect(new Vector2(rect.x, rect.y), new Vector2(rect.x + rect.width, rect.y + rect.height), cornerRadius, cornerSegments, index);
		}

		public void MakeRoundedRect(Vector3 bottomLeft, Vector3 topRight, float cornerRadius, int cornerSegments)
		{
			MakeRoundedRect(bottomLeft, topRight, cornerRadius, cornerSegments, 0);
		}

		public void MakeRoundedRect(Vector3 bottomLeft, Vector3 topRight, float cornerRadius, int cornerSegments, int index)
		{
			if (cornerSegments < 1)
			{
				Debug.LogError("VectorLine.MakeRoundedRect: cornerSegments value must be >= 1");
				return;
			}
			if (index < 0)
			{
				Debug.LogError("VectorLine.MakeRoundedRect: index value must be >= 0");
				return;
			}
			int num = ((!m_continuous) ? (cornerSegments * 8 + 8 + index) : (cornerSegments * 4 + 5 + index));
			if (pointsCount < num)
			{
				Resize(num);
			}
			if (bottomLeft.x > topRight.x)
			{
				Exchange(ref bottomLeft, ref topRight, 0);
			}
			if (bottomLeft.y > topRight.y)
			{
				Exchange(ref bottomLeft, ref topRight, 1);
			}
			bottomLeft += new Vector3(cornerRadius, cornerRadius);
			topRight -= new Vector3(cornerRadius, cornerRadius);
			MakeCircle(bottomLeft, cornerRadius, 4 * cornerSegments, index);
			int num2 = ((!m_continuous) ? (cornerSegments * 2) : (cornerSegments + 1));
			int originalCount = ((!m_continuous) ? (cornerSegments * 2) : cornerSegments);
			if (m_is2D)
			{
				CopyAndAddPoints(num2, originalCount, 3, new Vector2(0f, topRight.y - bottomLeft.y), index);
				CopyAndAddPoints(num2, originalCount, 2, Vector2.zero, index);
				CopyAndAddPoints(num2, originalCount, 1, new Vector2(topRight.x - bottomLeft.x, 0f), index);
				CopyAndAddPoints(num2, originalCount, 0, new Vector2(topRight.x - bottomLeft.x, topRight.y - bottomLeft.y), index);
				if (m_continuous)
				{
					m_points2[num2 * 4 + index] = m_points2[index];
					return;
				}
				m_points2[num2 * 4 + 7 + index] = m_points2[index];
				m_points2[num2 * 3 + 5 + index] = m_points2[num2 * 3 + 6 + index];
				m_points2[num2 * 2 + 3 + index] = m_points2[num2 * 2 + 4 + index];
				m_points2[num2 + 1 + index] = m_points2[num2 + 2 + index];
			}
			else
			{
				CopyAndAddPoints(num2, originalCount, 3, Vector2.zero, index);
				CopyAndAddPoints(num2, originalCount, 2, new Vector2(0f, topRight.y - bottomLeft.y), index);
				CopyAndAddPoints(num2, originalCount, 1, new Vector2(topRight.x - bottomLeft.x, topRight.y - bottomLeft.y), index);
				CopyAndAddPoints(num2, originalCount, 0, new Vector2(topRight.x - bottomLeft.x, 0f), index);
				if (m_continuous)
				{
					m_points3[num2 * 4 + index] = m_points3[index];
					return;
				}
				m_points3[num2 * 4 + 7 + index] = m_points3[index];
				m_points3[num2 * 3 + 5 + index] = m_points3[num2 * 3 + 6 + index];
				m_points3[num2 * 2 + 3 + index] = m_points3[num2 * 2 + 4 + index];
				m_points3[num2 + 1 + index] = m_points3[num2 + 2 + index];
			}
		}

		private void CopyAndAddPoints(int cornerPointCount, int originalCount, int sectionNumber, Vector2 add, int index)
		{
			Vector3 vector = add;
			for (int num = cornerPointCount - 1; num >= 0; num--)
			{
				if (m_continuous)
				{
					if (m_is2D)
					{
						m_points2[cornerPointCount * sectionNumber + num + index] = m_points2[originalCount * sectionNumber + num + index] + add;
					}
					else
					{
						m_points3[cornerPointCount * sectionNumber + num + index] = m_points3[originalCount * sectionNumber + num + index] + vector;
					}
				}
				else if (m_is2D)
				{
					m_points2[cornerPointCount * sectionNumber + sectionNumber * 2 + num + index] = m_points2[originalCount * sectionNumber + num + index] + add;
				}
				else
				{
					m_points3[cornerPointCount * sectionNumber + sectionNumber * 2 + num + index] = m_points3[originalCount * sectionNumber + num + index] + vector;
				}
			}
			if (!m_continuous)
			{
				int num2 = cornerPointCount * (sectionNumber + 1) + sectionNumber * 2 + index;
				if (m_is2D)
				{
					m_points2[num2] = m_points2[num2 - 1];
				}
				else
				{
					m_points3[num2] = m_points3[num2 - 1];
				}
			}
		}

		private void Exchange(ref Vector3 v1, ref Vector3 v2, int i)
		{
			float value = v1[i];
			v1[i] = v2[i];
			v2[i] = value;
		}

		public void MakeCircle(Vector3 origin, float radius)
		{
			MakeEllipse(origin, Vector3.forward, radius, radius, 0f, 0f, GetSegmentNumber(), 0f, 0);
		}

		public void MakeCircle(Vector3 origin, float radius, int segments)
		{
			MakeEllipse(origin, Vector3.forward, radius, radius, 0f, 0f, segments, 0f, 0);
		}

		public void MakeCircle(Vector3 origin, float radius, int segments, float pointRotation)
		{
			MakeEllipse(origin, Vector3.forward, radius, radius, 0f, 0f, segments, pointRotation, 0);
		}

		public void MakeCircle(Vector3 origin, float radius, int segments, int index)
		{
			MakeEllipse(origin, Vector3.forward, radius, radius, 0f, 0f, segments, 0f, index);
		}

		public void MakeCircle(Vector3 origin, float radius, int segments, float pointRotation, int index)
		{
			MakeEllipse(origin, Vector3.forward, radius, radius, 0f, 0f, segments, pointRotation, index);
		}

		public void MakeCircle(Vector3 origin, Vector3 upVector, float radius)
		{
			MakeEllipse(origin, upVector, radius, radius, 0f, 0f, GetSegmentNumber(), 0f, 0);
		}

		public void MakeCircle(Vector3 origin, Vector3 upVector, float radius, int segments)
		{
			MakeEllipse(origin, upVector, radius, radius, 0f, 0f, segments, 0f, 0);
		}

		public void MakeCircle(Vector3 origin, Vector3 upVector, float radius, int segments, float pointRotation)
		{
			MakeEllipse(origin, upVector, radius, radius, 0f, 0f, segments, pointRotation, 0);
		}

		public void MakeCircle(Vector3 origin, Vector3 upVector, float radius, int segments, int index)
		{
			MakeEllipse(origin, upVector, radius, radius, 0f, 0f, segments, 0f, index);
		}

		public void MakeCircle(Vector3 origin, Vector3 upVector, float radius, int segments, float pointRotation, int index)
		{
			MakeEllipse(origin, upVector, radius, radius, 0f, 0f, segments, pointRotation, index);
		}

		public void MakeEllipse(Vector3 origin, float xRadius, float yRadius)
		{
			MakeEllipse(origin, Vector3.forward, xRadius, yRadius, 0f, 0f, GetSegmentNumber(), 0f, 0);
		}

		public void MakeEllipse(Vector3 origin, float xRadius, float yRadius, int segments)
		{
			MakeEllipse(origin, Vector3.forward, xRadius, yRadius, 0f, 0f, segments, 0f, 0);
		}

		public void MakeEllipse(Vector3 origin, float xRadius, float yRadius, int segments, int index)
		{
			MakeEllipse(origin, Vector3.forward, xRadius, yRadius, 0f, 0f, segments, 0f, index);
		}

		public void MakeEllipse(Vector3 origin, float xRadius, float yRadius, int segments, float pointRotation)
		{
			MakeEllipse(origin, Vector3.forward, xRadius, yRadius, 0f, 0f, segments, pointRotation, 0);
		}

		public void MakeEllipse(Vector3 origin, Vector3 upVector, float xRadius, float yRadius)
		{
			MakeEllipse(origin, upVector, xRadius, yRadius, 0f, 0f, GetSegmentNumber(), 0f, 0);
		}

		public void MakeEllipse(Vector3 origin, Vector3 upVector, float xRadius, float yRadius, int segments)
		{
			MakeEllipse(origin, upVector, xRadius, yRadius, 0f, 0f, segments, 0f, 0);
		}

		public void MakeEllipse(Vector3 origin, Vector3 upVector, float xRadius, float yRadius, int segments, int index)
		{
			MakeEllipse(origin, upVector, xRadius, yRadius, 0f, 0f, segments, 0f, index);
		}

		public void MakeEllipse(Vector3 origin, Vector3 upVector, float xRadius, float yRadius, int segments, float pointRotation)
		{
			MakeEllipse(origin, upVector, xRadius, yRadius, 0f, 0f, segments, pointRotation, 0);
		}

		public void MakeEllipse(Vector3 origin, Vector3 upVector, float xRadius, float yRadius, int segments, float pointRotation, int index)
		{
			MakeEllipse(origin, upVector, xRadius, yRadius, 0f, 0f, segments, pointRotation, index);
		}

		public void MakeArc(Vector3 origin, float xRadius, float yRadius, float startDegrees, float endDegrees)
		{
			MakeEllipse(origin, Vector3.forward, xRadius, yRadius, startDegrees, endDegrees, GetSegmentNumber(), 0f, 0);
		}

		public void MakeArc(Vector3 origin, float xRadius, float yRadius, float startDegrees, float endDegrees, int segments)
		{
			MakeEllipse(origin, Vector3.forward, xRadius, yRadius, startDegrees, endDegrees, segments, 0f, 0);
		}

		public void MakeArc(Vector3 origin, float xRadius, float yRadius, float startDegrees, float endDegrees, int segments, int index)
		{
			MakeEllipse(origin, Vector3.forward, xRadius, yRadius, startDegrees, endDegrees, segments, 0f, index);
		}

		public void MakeArc(Vector3 origin, Vector3 upVector, float xRadius, float yRadius, float startDegrees, float endDegrees)
		{
			MakeEllipse(origin, upVector, xRadius, yRadius, startDegrees, endDegrees, GetSegmentNumber(), 0f, 0);
		}

		public void MakeArc(Vector3 origin, Vector3 upVector, float xRadius, float yRadius, float startDegrees, float endDegrees, int segments)
		{
			MakeEllipse(origin, upVector, xRadius, yRadius, startDegrees, endDegrees, segments, 0f, 0);
		}

		public void MakeArc(Vector3 origin, Vector3 upVector, float xRadius, float yRadius, float startDegrees, float endDegrees, int segments, int index)
		{
			MakeEllipse(origin, upVector, xRadius, yRadius, startDegrees, endDegrees, segments, 0f, index);
		}

		private void MakeEllipse(Vector3 origin, Vector3 upVector, float xRadius, float yRadius, float startDegrees, float endDegrees, int segments, float pointRotation, int index)
		{
			if (segments < 3)
			{
				Debug.LogError("VectorLine.MakeEllipse needs at least 3 segments");
			}
			else
			{
				if (!CheckArrayLength(FunctionName.MakeEllipse, segments, index))
				{
					return;
				}
				startDegrees = Mathf.Repeat(startDegrees, 360f);
				endDegrees = Mathf.Repeat(endDegrees, 360f);
				float num;
				float num2;
				if (startDegrees == endDegrees)
				{
					num = 360f;
					num2 = (0f - pointRotation) * ((float)Math.PI / 180f);
				}
				else
				{
					num = ((!(endDegrees > startDegrees)) ? (360f - startDegrees + endDegrees) : (endDegrees - startDegrees));
					num2 = startDegrees * ((float)Math.PI / 180f);
				}
				float num3 = num / (float)segments * ((float)Math.PI / 180f);
				if (m_continuous)
				{
					if (startDegrees != endDegrees)
					{
						segments++;
					}
					int num4 = 0;
					if (m_is2D)
					{
						Vector2 vector = origin;
						for (num4 = 0; num4 < segments; num4++)
						{
							m_points2[index + num4] = vector + new Vector2(0.5f + Mathf.Sin(num2) * xRadius, 0.5f + Mathf.Cos(num2) * yRadius);
							num2 += num3;
						}
						if (!m_isPoints && startDegrees == endDegrees)
						{
							m_points2[index + num4] = m_points2[index + (num4 - segments)];
						}
					}
					else
					{
						Matrix4x4 matrix4x = Matrix4x4.TRS(Vector3.zero, Quaternion.LookRotation(-upVector, upVector), Vector3.one);
						for (num4 = 0; num4 < segments; num4++)
						{
							m_points3[index + num4] = origin + matrix4x.MultiplyPoint3x4(new Vector3(Mathf.Sin(num2) * xRadius, Mathf.Cos(num2) * yRadius, 0f));
							num2 += num3;
						}
						if (!m_isPoints && startDegrees == endDegrees)
						{
							m_points3[index + num4] = m_points3[index + (num4 - segments)];
						}
					}
				}
				else if (m_is2D)
				{
					Vector2 vector2 = origin;
					int num5;
					for (num5 = 0; num5 < segments * 2; num5++)
					{
						m_points2[index + num5] = vector2 + new Vector2(0.5f + Mathf.Sin(num2) * xRadius, 0.5f + Mathf.Cos(num2) * yRadius);
						num2 += num3;
						num5++;
						m_points2[index + num5] = vector2 + new Vector2(0.5f + Mathf.Sin(num2) * xRadius, 0.5f + Mathf.Cos(num2) * yRadius);
					}
				}
				else
				{
					Matrix4x4 matrix4x2 = Matrix4x4.TRS(Vector3.zero, Quaternion.LookRotation(-upVector, upVector), Vector3.one);
					int num6;
					for (num6 = 0; num6 < segments * 2; num6++)
					{
						m_points3[index + num6] = origin + matrix4x2.MultiplyPoint3x4(new Vector3(Mathf.Sin(num2) * xRadius, Mathf.Cos(num2) * yRadius, 0f));
						num2 += num3;
						num6++;
						m_points3[index + num6] = origin + matrix4x2.MultiplyPoint3x4(new Vector3(Mathf.Sin(num2) * xRadius, Mathf.Cos(num2) * yRadius, 0f));
					}
				}
			}
		}

		public void MakeCurve(Vector2[] curvePoints)
		{
			MakeCurve(curvePoints, GetSegmentNumber(), 0);
		}

		public void MakeCurve(Vector2[] curvePoints, int segments)
		{
			MakeCurve(curvePoints, segments, 0);
		}

		public void MakeCurve(Vector2[] curvePoints, int segments, int index)
		{
			if (curvePoints.Length != 4)
			{
				Debug.LogError("VectorLine.MakeCurve needs exactly 4 points in the curve points array");
			}
			else
			{
				MakeCurve(curvePoints[0], curvePoints[1], curvePoints[2], curvePoints[3], segments, index);
			}
		}

		public void MakeCurve(Vector3[] curvePoints)
		{
			MakeCurve(curvePoints, GetSegmentNumber(), 0);
		}

		public void MakeCurve(Vector3[] curvePoints, int segments)
		{
			MakeCurve(curvePoints, segments, 0);
		}

		public void MakeCurve(Vector3[] curvePoints, int segments, int index)
		{
			if (curvePoints.Length != 4)
			{
				Debug.LogError("VectorLine.MakeCurve needs exactly 4 points in the curve points array");
			}
			else
			{
				MakeCurve(curvePoints[0], curvePoints[1], curvePoints[2], curvePoints[3], segments, index);
			}
		}

		public void MakeCurve(Vector3 anchor1, Vector3 control1, Vector3 anchor2, Vector3 control2)
		{
			MakeCurve(anchor1, control1, anchor2, control2, GetSegmentNumber(), 0);
		}

		public void MakeCurve(Vector3 anchor1, Vector3 control1, Vector3 anchor2, Vector3 control2, int segments)
		{
			MakeCurve(anchor1, control1, anchor2, control2, segments, 0);
		}

		public void MakeCurve(Vector3 anchor1, Vector3 control1, Vector3 anchor2, Vector3 control2, int segments, int index)
		{
			if (!CheckArrayLength(FunctionName.MakeCurve, segments, index))
			{
				return;
			}
			if (m_continuous)
			{
				int num = ((!m_isPoints) ? (segments + 1) : segments);
				if (m_is2D)
				{
					Vector2 anchor3 = anchor1;
					Vector2 anchor4 = anchor2;
					Vector2 control3 = control1;
					Vector2 control4 = control2;
					for (int i = 0; i < num; i++)
					{
						m_points2[index + i] = GetBezierPoint(ref anchor3, ref control3, ref anchor4, ref control4, (float)i / (float)segments);
					}
				}
				else
				{
					for (int j = 0; j < num; j++)
					{
						m_points3[index + j] = GetBezierPoint3D(ref anchor1, ref control1, ref anchor2, ref control2, (float)j / (float)segments);
					}
				}
				return;
			}
			int num2 = 0;
			if (m_is2D)
			{
				Vector2 anchor5 = anchor1;
				Vector2 anchor6 = anchor2;
				Vector2 control5 = control1;
				Vector2 control6 = control2;
				for (int k = 0; k < segments; k++)
				{
					m_points2[index + num2++] = GetBezierPoint(ref anchor5, ref control5, ref anchor6, ref control6, (float)k / (float)segments);
					m_points2[index + num2++] = GetBezierPoint(ref anchor5, ref control5, ref anchor6, ref control6, (float)(k + 1) / (float)segments);
				}
			}
			else
			{
				for (int l = 0; l < segments; l++)
				{
					m_points3[index + num2++] = GetBezierPoint3D(ref anchor1, ref control1, ref anchor2, ref control2, (float)l / (float)segments);
					m_points3[index + num2++] = GetBezierPoint3D(ref anchor1, ref control1, ref anchor2, ref control2, (float)(l + 1) / (float)segments);
				}
			}
		}

		private static Vector2 GetBezierPoint(ref Vector2 anchor1, ref Vector2 control1, ref Vector2 anchor2, ref Vector2 control2, float t)
		{
			float num = 3f * (control1.x - anchor1.x);
			float num2 = 3f * (control2.x - control1.x) - num;
			float num3 = anchor2.x - anchor1.x - num - num2;
			float num4 = 3f * (control1.y - anchor1.y);
			float num5 = 3f * (control2.y - control1.y) - num4;
			float num6 = anchor2.y - anchor1.y - num4 - num5;
			return new Vector2(num3 * (t * t * t) + num2 * (t * t) + num * t + anchor1.x, num6 * (t * t * t) + num5 * (t * t) + num4 * t + anchor1.y);
		}

		private static Vector3 GetBezierPoint3D(ref Vector3 anchor1, ref Vector3 control1, ref Vector3 anchor2, ref Vector3 control2, float t)
		{
			float num = 3f * (control1.x - anchor1.x);
			float num2 = 3f * (control2.x - control1.x) - num;
			float num3 = anchor2.x - anchor1.x - num - num2;
			float num4 = 3f * (control1.y - anchor1.y);
			float num5 = 3f * (control2.y - control1.y) - num4;
			float num6 = anchor2.y - anchor1.y - num4 - num5;
			float num7 = 3f * (control1.z - anchor1.z);
			float num8 = 3f * (control2.z - control1.z) - num7;
			float num9 = anchor2.z - anchor1.z - num7 - num8;
			return new Vector3(num3 * (t * t * t) + num2 * (t * t) + num * t + anchor1.x, num6 * (t * t * t) + num5 * (t * t) + num4 * t + anchor1.y, num9 * (t * t * t) + num8 * (t * t) + num7 * t + anchor1.z);
		}

		public void MakeSpline(Vector2[] splinePoints)
		{
			MakeSpline(splinePoints, null, GetSegmentNumber(), 0, false);
		}

		public void MakeSpline(Vector2[] splinePoints, bool loop)
		{
			MakeSpline(splinePoints, null, GetSegmentNumber(), 0, loop);
		}

		public void MakeSpline(Vector2[] splinePoints, int segments)
		{
			MakeSpline(splinePoints, null, segments, 0, false);
		}

		public void MakeSpline(Vector2[] splinePoints, int segments, bool loop)
		{
			MakeSpline(splinePoints, null, segments, 0, loop);
		}

		public void MakeSpline(Vector2[] splinePoints, int segments, int index)
		{
			MakeSpline(splinePoints, null, segments, index, false);
		}

		public void MakeSpline(Vector2[] splinePoints, int segments, int index, bool loop)
		{
			MakeSpline(splinePoints, null, segments, index, loop);
		}

		public void MakeSpline(Vector3[] splinePoints)
		{
			MakeSpline(null, splinePoints, GetSegmentNumber(), 0, false);
		}

		public void MakeSpline(Vector3[] splinePoints, bool loop)
		{
			MakeSpline(null, splinePoints, GetSegmentNumber(), 0, loop);
		}

		public void MakeSpline(Vector3[] splinePoints, int segments)
		{
			MakeSpline(null, splinePoints, segments, 0, false);
		}

		public void MakeSpline(Vector3[] splinePoints, int segments, bool loop)
		{
			MakeSpline(null, splinePoints, segments, 0, loop);
		}

		public void MakeSpline(Vector3[] splinePoints, int segments, int index)
		{
			MakeSpline(null, splinePoints, segments, index, false);
		}

		public void MakeSpline(Vector3[] splinePoints, int segments, int index, bool loop)
		{
			MakeSpline(null, splinePoints, segments, index, loop);
		}

		private void MakeSpline(Vector2[] splinePoints2, Vector3[] splinePoints3, int segments, int index, bool loop)
		{
			int num = ((splinePoints2 == null) ? splinePoints3.Length : splinePoints2.Length);
			if (num < 2)
			{
				Debug.LogError("VectorLine.MakeSpline needs at least 2 spline points");
			}
			else if (splinePoints2 != null && !m_is2D)
			{
				Debug.LogError("VectorLine.MakeSpline was called with a Vector2 spline points array, but the line uses Vector3 points");
			}
			else if (splinePoints3 != null && m_is2D)
			{
				Debug.LogError("VectorLine.MakeSpline was called with a Vector3 spline points array, but the line uses Vector2 points");
			}
			else
			{
				if (!CheckArrayLength(FunctionName.MakeSpline, segments, index))
				{
					return;
				}
				int num2 = index;
				int num3 = ((!loop) ? (num - 1) : num);
				float num4 = 1f / (float)segments * (float)num3;
				float num5 = 0f;
				int num6 = 0;
				int num7 = 0;
				int num8 = 0;
				int i;
				for (i = 0; i < num3; i++)
				{
					num6 = i - 1;
					num7 = i + 1;
					num8 = i + 2;
					if (num6 < 0)
					{
						num6 = (loop ? (num3 - 1) : 0);
					}
					if (loop && num7 > num3 - 1)
					{
						num7 -= num3;
					}
					if (num8 > num3 - 1)
					{
						num8 = ((!loop) ? num3 : (num8 - num3));
					}
					float num9;
					if (m_continuous)
					{
						if (m_is2D)
						{
							for (num9 = num5; num9 <= 1f; num9 += num4)
							{
								m_points2[num2++] = GetSplinePoint(ref splinePoints2[num6], ref splinePoints2[i], ref splinePoints2[num7], ref splinePoints2[num8], num9);
							}
						}
						else
						{
							for (num9 = num5; num9 <= 1f; num9 += num4)
							{
								m_points3[num2++] = GetSplinePoint3D(ref splinePoints3[num6], ref splinePoints3[i], ref splinePoints3[num7], ref splinePoints3[num8], num9);
							}
						}
					}
					else if (m_is2D)
					{
						for (num9 = num5; num9 <= 1f; num9 += num4)
						{
							m_points2[num2++] = GetSplinePoint(ref splinePoints2[num6], ref splinePoints2[i], ref splinePoints2[num7], ref splinePoints2[num8], num9);
							if (num2 > index + 1 && num2 < index + segments * 2)
							{
								m_points2[num2++] = m_points2[num2 - 2];
							}
						}
					}
					else
					{
						for (num9 = num5; num9 <= 1f; num9 += num4)
						{
							m_points3[num2++] = GetSplinePoint3D(ref splinePoints3[num6], ref splinePoints3[i], ref splinePoints3[num7], ref splinePoints3[num8], num9);
							if (num2 > index + 1 && num2 < index + segments * 2)
							{
								m_points3[num2++] = m_points3[num2 - 2];
							}
						}
					}
					num5 = num9 - 1f;
				}
				if ((m_continuous && num2 < index + (segments + 1)) || (!m_continuous && num2 < index + segments * 2))
				{
					if (m_is2D)
					{
						m_points2[num2] = GetSplinePoint(ref splinePoints2[num6], ref splinePoints2[i - 1], ref splinePoints2[num7], ref splinePoints2[num8], 1f);
					}
					else
					{
						m_points3[num2] = GetSplinePoint3D(ref splinePoints3[num6], ref splinePoints3[i - 1], ref splinePoints3[num7], ref splinePoints3[num8], 1f);
					}
				}
			}
		}

		private static Vector2 GetSplinePoint(ref Vector2 p0, ref Vector2 p1, ref Vector2 p2, ref Vector2 p3, float t)
		{
			Vector4 p4 = Vector4.zero;
			Vector4 p5 = Vector4.zero;
			float num = Mathf.Pow(VectorDistanceSquared(ref p0, ref p1), 0.25f);
			float num2 = Mathf.Pow(VectorDistanceSquared(ref p1, ref p2), 0.25f);
			float num3 = Mathf.Pow(VectorDistanceSquared(ref p2, ref p3), 0.25f);
			if (num2 < 0.0001f)
			{
				num2 = 1f;
			}
			if (num < 0.0001f)
			{
				num = num2;
			}
			if (num3 < 0.0001f)
			{
				num3 = num2;
			}
			InitNonuniformCatmullRom(p0.x, p1.x, p2.x, p3.x, num, num2, num3, ref p4);
			InitNonuniformCatmullRom(p0.y, p1.y, p2.y, p3.y, num, num2, num3, ref p5);
			return new Vector2(EvalCubicPoly(ref p4, t), EvalCubicPoly(ref p5, t));
		}

		private static Vector3 GetSplinePoint3D(ref Vector3 p0, ref Vector3 p1, ref Vector3 p2, ref Vector3 p3, float t)
		{
			Vector4 p4 = Vector4.zero;
			Vector4 p5 = Vector4.zero;
			Vector4 p6 = Vector4.zero;
			float num = Mathf.Pow(VectorDistanceSquared(ref p0, ref p1), 0.25f);
			float num2 = Mathf.Pow(VectorDistanceSquared(ref p1, ref p2), 0.25f);
			float num3 = Mathf.Pow(VectorDistanceSquared(ref p2, ref p3), 0.25f);
			if (num2 < 0.0001f)
			{
				num2 = 1f;
			}
			if (num < 0.0001f)
			{
				num = num2;
			}
			if (num3 < 0.0001f)
			{
				num3 = num2;
			}
			InitNonuniformCatmullRom(p0.x, p1.x, p2.x, p3.x, num, num2, num3, ref p4);
			InitNonuniformCatmullRom(p0.y, p1.y, p2.y, p3.y, num, num2, num3, ref p5);
			InitNonuniformCatmullRom(p0.z, p1.z, p2.z, p3.z, num, num2, num3, ref p6);
			return new Vector3(EvalCubicPoly(ref p4, t), EvalCubicPoly(ref p5, t), EvalCubicPoly(ref p6, t));
		}

		private static float VectorDistanceSquared(ref Vector2 p, ref Vector2 q)
		{
			float num = q.x - p.x;
			float num2 = q.y - p.y;
			return num * num + num2 * num2;
		}

		private static float VectorDistanceSquared(ref Vector3 p, ref Vector3 q)
		{
			float num = q.x - p.x;
			float num2 = q.y - p.y;
			float num3 = q.z - p.z;
			return num * num + num2 * num2 + num3 * num3;
		}

		private static void InitNonuniformCatmullRom(float x0, float x1, float x2, float x3, float dt0, float dt1, float dt2, ref Vector4 p)
		{
			float num = ((x1 - x0) / dt0 - (x2 - x0) / (dt0 + dt1) + (x2 - x1) / dt1) * dt1;
			float num2 = ((x2 - x1) / dt1 - (x3 - x1) / (dt1 + dt2) + (x3 - x2) / dt2) * dt1;
			p.x = x1;
			p.y = num;
			p.z = -3f * x1 + 3f * x2 - 2f * num - num2;
			p.w = 2f * x1 - 2f * x2 + num + num2;
		}

		private static float EvalCubicPoly(ref Vector4 p, float t)
		{
			return p.x + p.y * t + p.z * (t * t) + p.w * (t * t * t);
		}

		public void MakeText(string text, Vector3 startPos, float size)
		{
			MakeText(text, startPos, size, 1f, 1.5f, true);
		}

		public void MakeText(string text, Vector3 startPos, float size, bool uppercaseOnly)
		{
			MakeText(text, startPos, size, 1f, 1.5f, uppercaseOnly);
		}

		public void MakeText(string text, Vector3 startPos, float size, float charSpacing, float lineSpacing)
		{
			MakeText(text, startPos, size, charSpacing, lineSpacing, true);
		}

		public void MakeText(string text, Vector3 startPos, float size, float charSpacing, float lineSpacing, bool uppercaseOnly)
		{
			if (m_continuous)
			{
				Debug.LogError("VectorLine.MakeText only works with a discrete line");
				return;
			}
			int num = 0;
			for (int i = 0; i < text.Length; i++)
			{
				int num2 = Convert.ToInt32(text[i]);
				if (num2 < 0 || num2 > 256)
				{
					Debug.LogError("VectorLine.MakeText: Character '" + text[i] + "' is not valid");
					return;
				}
				if (uppercaseOnly && num2 >= 97 && num2 <= 122)
				{
					num2 -= 32;
				}
				if (VectorChar.data[num2] != null)
				{
					num += VectorChar.data[num2].Length;
				}
			}
			if (num != pointsCount)
			{
				Resize(num);
			}
			float num3 = 0f;
			float num4 = 0f;
			int num5 = 0;
			Vector2 vector = new Vector2(size, size);
			for (int j = 0; j < text.Length; j++)
			{
				int num6 = Convert.ToInt32(text[j]);
				switch (num6)
				{
				case 10:
					num4 -= lineSpacing;
					num3 = 0f;
					continue;
				case 32:
					num3 += charSpacing;
					continue;
				}
				if (uppercaseOnly && num6 >= 97 && num6 <= 122)
				{
					num6 -= 32;
				}
				int num7 = 0;
				if (VectorChar.data[num6] != null)
				{
					num7 = VectorChar.data[num6].Length;
					if (m_is2D)
					{
						for (int k = 0; k < num7; k++)
						{
							m_points2[num5++] = Vector2.Scale(VectorChar.data[num6][k] + new Vector2(num3, num4), vector) + (Vector2)startPos;
						}
					}
					else
					{
						for (int l = 0; l < num7; l++)
						{
							m_points3[num5++] = Vector3.Scale((Vector3)VectorChar.data[num6][l] + new Vector3(num3, num4, 0f), vector) + startPos;
						}
					}
					num3 += charSpacing;
				}
				else
				{
					num3 += charSpacing;
				}
			}
		}

		public void MakeWireframe(Mesh mesh)
		{
			if (m_continuous)
			{
				Debug.LogError("VectorLine.MakeWireframe only works with a discrete line");
				return;
			}
			if (m_is2D)
			{
				Debug.LogError("VectorLine.MakeWireframe can only be used with Vector3 points, which \"" + name + "\" doesn't have");
				return;
			}
			if (mesh == null)
			{
				Debug.LogError("VectorLine.MakeWireframe can't use a null mesh");
				return;
			}
			int[] triangles = mesh.triangles;
			Vector3[] vertices = mesh.vertices;
			Dictionary<Vector3Pair, bool> pairs = new Dictionary<Vector3Pair, bool>();
			List<Vector3> list = new List<Vector3>();
			for (int i = 0; i < triangles.Length; i += 3)
			{
				CheckPairPoints(pairs, vertices[triangles[i]], vertices[triangles[i + 1]], list);
				CheckPairPoints(pairs, vertices[triangles[i + 1]], vertices[triangles[i + 2]], list);
				CheckPairPoints(pairs, vertices[triangles[i + 2]], vertices[triangles[i]], list);
			}
			if (list.Count != m_pointsCount)
			{
				Resize(list.Count);
			}
			for (int j = 0; j < m_pointsCount; j++)
			{
				m_points3[j] = list[j];
			}
		}

		private static void CheckPairPoints(Dictionary<Vector3Pair, bool> pairs, Vector3 p1, Vector3 p2, List<Vector3> linePoints)
		{
			Vector3Pair key = new Vector3Pair(p1, p2);
			Vector3Pair key2 = new Vector3Pair(p2, p1);
			if (!pairs.ContainsKey(key) && !pairs.ContainsKey(key2))
			{
				pairs[key] = true;
				pairs[key2] = true;
				linePoints.Add(p1);
				linePoints.Add(p2);
			}
		}

		public void MakeCube(Vector3 position, float xSize, float ySize, float zSize)
		{
			MakeCube(position, xSize, ySize, zSize, 0);
		}

		public void MakeCube(Vector3 position, float xSize, float ySize, float zSize, int index)
		{
			if (m_continuous)
			{
				Debug.LogError("VectorLine.MakeCube only works with a discrete line");
				return;
			}
			if (m_is2D)
			{
				Debug.LogError("VectorLine.MakeCube can only be used with Vector3 points, which \"" + name + "\" doesn't have");
				return;
			}
			if (index + 24 > m_pointsCount)
			{
				if (index == 0)
				{
					Debug.LogError("VectorLine.MakeCube: The number of Vector3 points needs to be at least 24 for \"" + name + "\"");
					return;
				}
				Debug.LogError("Calling VectorLine.MakeCube with an index of " + index + " would exceed the length of the Vector3 points for \"" + name + "\"");
				return;
			}
			xSize /= 2f;
			ySize /= 2f;
			zSize /= 2f;
			m_points3[index] = position + new Vector3(0f - xSize, ySize, 0f - zSize);
			m_points3[index + 1] = position + new Vector3(xSize, ySize, 0f - zSize);
			m_points3[index + 2] = position + new Vector3(xSize, ySize, 0f - zSize);
			m_points3[index + 3] = position + new Vector3(xSize, ySize, zSize);
			m_points3[index + 4] = position + new Vector3(xSize, ySize, zSize);
			m_points3[index + 5] = position + new Vector3(0f - xSize, ySize, zSize);
			m_points3[index + 6] = position + new Vector3(0f - xSize, ySize, zSize);
			m_points3[index + 7] = position + new Vector3(0f - xSize, ySize, 0f - zSize);
			m_points3[index + 8] = position + new Vector3(0f - xSize, 0f - ySize, 0f - zSize);
			m_points3[index + 9] = position + new Vector3(0f - xSize, ySize, 0f - zSize);
			m_points3[index + 10] = position + new Vector3(xSize, 0f - ySize, 0f - zSize);
			m_points3[index + 11] = position + new Vector3(xSize, ySize, 0f - zSize);
			m_points3[index + 12] = position + new Vector3(0f - xSize, 0f - ySize, zSize);
			m_points3[index + 13] = position + new Vector3(0f - xSize, ySize, zSize);
			m_points3[index + 14] = position + new Vector3(xSize, 0f - ySize, zSize);
			m_points3[index + 15] = position + new Vector3(xSize, ySize, zSize);
			m_points3[index + 16] = position + new Vector3(0f - xSize, 0f - ySize, 0f - zSize);
			m_points3[index + 17] = position + new Vector3(xSize, 0f - ySize, 0f - zSize);
			m_points3[index + 18] = position + new Vector3(xSize, 0f - ySize, 0f - zSize);
			m_points3[index + 19] = position + new Vector3(xSize, 0f - ySize, zSize);
			m_points3[index + 20] = position + new Vector3(xSize, 0f - ySize, zSize);
			m_points3[index + 21] = position + new Vector3(0f - xSize, 0f - ySize, zSize);
			m_points3[index + 22] = position + new Vector3(0f - xSize, 0f - ySize, zSize);
			m_points3[index + 23] = position + new Vector3(0f - xSize, 0f - ySize, 0f - zSize);
		}

		public void SetDistances()
		{
			if (m_distances == null || m_distances.Length != ((!m_continuous) ? (m_pointsCount / 2 + 1) : m_pointsCount))
			{
				m_distances = new float[(!m_continuous) ? (m_pointsCount / 2 + 1) : m_pointsCount];
			}
			double num = 0.0;
			int num2 = pointsCount - 1;
			if (m_points3 != null)
			{
				if (m_continuous)
				{
					for (int i = 0; i < num2; i++)
					{
						Vector3 vector = m_points3[i] - m_points3[i + 1];
						num += Math.Sqrt(vector.x * vector.x + vector.y * vector.y + vector.z * vector.z);
						m_distances[i + 1] = (float)num;
					}
					return;
				}
				int num3 = 1;
				for (int j = 0; j < num2; j += 2)
				{
					Vector3 vector2 = m_points3[j] - m_points3[j + 1];
					num += Math.Sqrt(vector2.x * vector2.x + vector2.y * vector2.y + vector2.z * vector2.z);
					m_distances[num3++] = (float)num;
				}
			}
			else if (m_continuous)
			{
				for (int k = 0; k < num2; k++)
				{
					Vector2 vector3 = m_points2[k] - m_points2[k + 1];
					num += Math.Sqrt(vector3.x * vector3.x + vector3.y * vector3.y);
					m_distances[k + 1] = (float)num;
				}
			}
			else
			{
				int num4 = 1;
				for (int l = 0; l < num2; l += 2)
				{
					Vector2 vector4 = m_points2[l] - m_points2[l + 1];
					num += Math.Sqrt(vector4.x * vector4.x + vector4.y * vector4.y);
					m_distances[num4++] = (float)num;
				}
			}
		}

		public float GetLength()
		{
			if (m_distances == null || m_distances.Length != ((!m_continuous) ? (pointsCount / 2 + 1) : pointsCount))
			{
				SetDistances();
			}
			return m_distances[m_distances.Length - 1];
		}

		public Vector2 GetPoint01(float distance)
		{
			int index;
			return GetPoint(Mathf.Lerp(0f, GetLength(), distance), out index);
		}

		public Vector2 GetPoint01(float distance, out int index)
		{
			return GetPoint(Mathf.Lerp(0f, GetLength(), distance), out index);
		}

		public Vector2 GetPoint(float distance)
		{
			int index;
			return GetPoint(distance, out index);
		}

		public Vector2 GetPoint(float distance, out int index)
		{
			if (!m_is2D)
			{
				Debug.LogError("VectorLine.GetPoint only works with Vector2 points");
				index = 0;
				return Vector2.zero;
			}
			SetDistanceIndex(out index, distance);
			Vector2 result = ((!m_continuous) ? Vector2.Lerp(m_points2[(index - 1) * 2], m_points2[(index - 1) * 2 + 1], Mathf.InverseLerp(m_distances[index - 1], m_distances[index], distance)) : Vector2.Lerp(m_points2[index - 1], m_points2[index], Mathf.InverseLerp(m_distances[index - 1], m_distances[index], distance)));
			if ((bool)m_drawTransform)
			{
				result += new Vector2(m_drawTransform.position.x, m_drawTransform.position.y);
			}
			index--;
			return result;
		}

		public Vector3 GetPoint3D01(float distance)
		{
			int index;
			return GetPoint3D(Mathf.Lerp(0f, GetLength(), distance), out index);
		}

		public Vector3 GetPoint3D01(float distance, out int index)
		{
			return GetPoint3D(Mathf.Lerp(0f, GetLength(), distance), out index);
		}

		public Vector3 GetPoint3D(float distance)
		{
			int index;
			return GetPoint3D(distance, out index);
		}

		public Vector3 GetPoint3D(float distance, out int index)
		{
			if (m_is2D)
			{
				Debug.LogError("VectorLine.GetPoint3D only works with Vector3 points");
				index = 0;
				return Vector3.zero;
			}
			SetDistanceIndex(out index, distance);
			Vector3 result = ((!m_continuous) ? Vector3.Lerp(m_points3[(index - 1) * 2], m_points3[(index - 1) * 2 + 1], Mathf.InverseLerp(m_distances[index - 1], m_distances[index], distance)) : Vector3.Lerp(m_points3[index - 1], m_points3[index], Mathf.InverseLerp(m_distances[index - 1], m_distances[index], distance)));
			if ((bool)m_drawTransform)
			{
				result += m_drawTransform.position;
			}
			index--;
			return result;
		}

		private void SetDistanceIndex(out int i, float distance)
		{
			if (m_distances == null)
			{
				SetDistances();
			}
			i = m_drawStart + 1;
			if (!m_continuous)
			{
				i = (i + 1) / 2;
			}
			if (i >= m_distances.Length)
			{
				i = m_distances.Length - 1;
			}
			int num = m_drawEnd;
			if (!m_continuous)
			{
				num = (num + 1) / 2;
			}
			while (distance > m_distances[i] && i < num)
			{
				i++;
			}
		}

		public static void SetEndCap(string name, EndCap capType)
		{
			SetEndCap(name, capType, (Material)null, 0f, 0f, 1f, 1f, (Texture2D[])null);
		}

		public static void SetEndCap(string name, EndCap capType, Material material, params Texture2D[] textures)
		{
			SetEndCap(name, capType, material, 0f, 0f, 1f, 1f, textures);
		}

		public static void SetEndCap(string name, EndCap capType, Material material, float offset, params Texture2D[] textures)
		{
			SetEndCap(name, capType, material, offset, offset, 1f, 1f, textures);
		}

		public static void SetEndCap(string name, EndCap capType, Material material, float offsetFront, float offsetBack, params Texture2D[] textures)
		{
			SetEndCap(name, capType, material, offsetFront, offsetBack, 1f, 1f, textures);
		}

		public static void SetEndCap(string name, EndCap capType, Material material, float offsetFront, float offsetBack, float scaleFront, float scaleBack, params Texture2D[] textures)
		{
			if (capDictionary == null)
			{
				capDictionary = new Dictionary<string, CapInfo>();
			}
			if (name == null || name == "")
			{
				Debug.LogError("VectorLine: must supply a name for SetEndCap");
				return;
			}
			if (capDictionary.ContainsKey(name) && capType != EndCap.None)
			{
				Debug.LogError("VectorLine: end cap \"" + name + "\" has already been set up");
				return;
			}
			if (capType == EndCap.Both)
			{
				if (textures.Length < 2)
				{
					Debug.LogError("VectorLine: must supply two textures when using SetEndCap with EndCap.Both");
					return;
				}
				if (textures[0].width != textures[1].width || textures[0].height != textures[1].height)
				{
					Debug.LogError("VectorLine: when using SetEndCap with EndCap.Both, both textures must have the same width and height");
					return;
				}
			}
			if ((capType == EndCap.Front || capType == EndCap.Back || capType == EndCap.Mirror) && textures.Length < 1)
			{
				Debug.LogError("VectorLine: must supply a texture when using SetEndCap with EndCap.Front, EndCap.Back, or EndCap.Mirror");
				return;
			}
			if (capType == EndCap.None)
			{
				if (capDictionary.ContainsKey(name))
				{
					RemoveEndCap(name);
				}
				return;
			}
			if (material == null)
			{
				Debug.LogError("VectorLine: must supply a material when using SetEndCap with any EndCap type except EndCap.None");
				return;
			}
			if (!material.HasProperty("_MainTex"))
			{
				Debug.LogError("VectorLine: the material supplied when using SetEndCap must contain a shader that has a \"_MainTex\" property");
				return;
			}
			int width = textures[0].width;
			int height = textures[0].height;
			float num = 0f;
			float ratio = 0f;
			Color[] colors = null;
			Color[] colors2 = null;
			switch (capType)
			{
			case EndCap.Front:
				colors = textures[0].GetPixels();
				colors2 = new Color[width * height];
				num = (float)textures[0].width / (float)textures[0].height;
				break;
			case EndCap.Back:
				colors = new Color[width * height];
				colors2 = textures[0].GetPixels();
				ratio = (float)textures[0].width / (float)textures[0].height;
				break;
			case EndCap.Both:
				colors = textures[0].GetPixels();
				colors2 = textures[1].GetPixels();
				num = (float)textures[0].width / (float)textures[0].height;
				ratio = (float)textures[1].width / (float)textures[1].height;
				break;
			case EndCap.Mirror:
				colors = textures[0].GetPixels();
				colors2 = new Color[width * height];
				num = (float)textures[0].width / (float)textures[0].height;
				ratio = num;
				break;
			}
			Texture2D texture2D = new Texture2D(width, height * 4, TextureFormat.ARGB32, false);
			texture2D.wrapMode = TextureWrapMode.Clamp;
			texture2D.filterMode = textures[0].filterMode;
			texture2D.SetPixels(0, 0, width, height, colors);
			texture2D.SetPixels(0, height * 3, width, height, colors2);
			texture2D.SetPixels(0, height, width, height * 2, new Color[width * (height * 2)]);
			texture2D.Apply(false, true);
			Material material2 = (Material)UnityEngine.Object.Instantiate(material);
			material2.name = material.name + " EndCap";
			material2.mainTexture = texture2D;
			capDictionary.Add(name, new CapInfo(capType, material2, texture2D, num, ratio, offsetFront, offsetBack, scaleFront, scaleBack));
		}

		public static void RemoveEndCap(string name)
		{
			if (!capDictionary.ContainsKey(name))
			{
				Debug.LogError("VectorLine: RemoveEndCap: \"" + name + "\" has not been set up");
				return;
			}
			UnityEngine.Object.Destroy(capDictionary[name].texture);
			UnityEngine.Object.Destroy(capDictionary[name].material);
			capDictionary.Remove(name);
		}

		public bool Selected(Vector2 p)
		{
			int index;
			return Selected(p, 0, 0, out index, cam3D);
		}

		public bool Selected(Vector2 p, out int index)
		{
			return Selected(p, 0, 0, out index, cam3D);
		}

		public bool Selected(Vector2 p, int extraDistance, out int index)
		{
			return Selected(p, extraDistance, 0, out index, cam3D);
		}

		public bool Selected(Vector2 p, int extraDistance, int extraLength, out int index)
		{
			return Selected(p, extraDistance, extraLength, out index, cam3D);
		}

		public bool Selected(Vector2 p, Camera cam)
		{
			int index;
			return Selected(p, 0, 0, out index, cam);
		}

		public bool Selected(Vector2 p, out int index, Camera cam)
		{
			return Selected(p, 0, 0, out index, cam);
		}

		public bool Selected(Vector2 p, int extraDistance, out int index, Camera cam)
		{
			return Selected(p, extraDistance, 0, out index, cam);
		}

		public bool Selected(Vector2 p, int extraDistance, int extraLength, out int index, Camera cam)
		{
			if (cam == null)
			{
				SetCamera3D();
				if (!cam3D)
				{
					Debug.LogError("VectorLine.Selected: camera cannot be null. If there is no camera tagged \"MainCamera\", supply one manually");
					index = 0;
					return false;
				}
				cam = cam3D;
			}
			int num = ((m_lineWidths.Length != 1) ? 1 : 0);
			int num2 = ((!m_continuous) ? (m_drawStart / 2 - num) : (m_drawStart - num));
			if (m_lineWidths.Length == 1)
			{
				num = 0;
				num2 = 0;
			}
			else
			{
				num = 1;
			}
			int num3 = m_drawEnd;
			bool flag = m_drawTransform != null;
			Matrix4x4 matrix4x = ((!flag) ? Matrix4x4.identity : m_drawTransform.localToWorldMatrix);
			Vector2 vector = new Vector2(Screen.width, Screen.height);
			if (m_isPoints)
			{
				if (num3 == pointsCount)
				{
					num3--;
				}
				if (m_is2D)
				{
					for (int i = m_drawStart; i <= num3; i++)
					{
						num2 += num;
						float num4 = m_lineWidths[num2] + (float)extraDistance;
						Vector2 vector2 = ((!flag) ? m_points2[i] : ((Vector2)matrix4x.MultiplyPoint3x4(m_points2[i])));
						if (m_viewportDraw)
						{
							vector2.x *= vector.x;
							vector2.y *= vector.y;
						}
						if (p.x >= vector2.x - num4 && p.x <= vector2.x + num4 && p.y >= vector2.y - num4 && p.y <= vector2.y + num4)
						{
							index = i;
							return true;
						}
					}
					index = -1;
					return false;
				}
				for (int j = m_drawStart; j <= num3; j++)
				{
					num2 += num;
					float num5 = m_lineWidths[num2] + (float)extraDistance;
					Vector2 vector2 = ((!flag) ? cam.WorldToScreenPoint(m_points3[j]) : cam.WorldToScreenPoint(matrix4x.MultiplyPoint3x4(m_points3[j])));
					if (p.x >= vector2.x - num5 && p.x <= vector2.x + num5 && p.y >= vector2.y - num5 && p.y <= vector2.y + num5)
					{
						index = j;
						return true;
					}
				}
				index = -1;
				return false;
			}
			float num6 = 0f;
			int num7 = (m_continuous ? 1 : 2);
			Vector2 zero = Vector2.zero;
			if (m_continuous && m_drawEnd == pointsCount)
			{
				num3--;
			}
			Vector2 vector3 = default(Vector2);
			Vector2 vector4 = default(Vector2);
			if (m_is2D)
			{
				for (int k = m_drawStart; k < num3; k += num7)
				{
					num2 += num;
					if (flag)
					{
						vector3 = matrix4x.MultiplyPoint3x4(m_points2[k]);
						vector4 = matrix4x.MultiplyPoint3x4(m_points2[k + 1]);
					}
					else
					{
						vector3.x = m_points2[k].x;
						vector3.y = m_points2[k].y;
						vector4.x = m_points2[k + 1].x;
						vector4.y = m_points2[k + 1].y;
					}
					if (m_viewportDraw)
					{
						vector3.x *= vector.x;
						vector3.y *= vector.y;
						vector4.x *= vector.x;
						vector4.y *= vector.y;
					}
					if (extraLength > 0)
					{
						zero = (vector3 - vector4).normalized * extraLength;
						vector3.x += zero.x;
						vector3.y += zero.y;
						vector4.x -= zero.x;
						vector4.y -= zero.y;
					}
					num6 = Vector2.Dot(p - vector3, vector4 - vector3) / (vector4 - vector3).sqrMagnitude;
					if (!(num6 < 0f) && !(num6 > 1f) && (p - (vector3 + num6 * (vector4 - vector3))).sqrMagnitude <= (m_lineWidths[num2] + (float)extraDistance) * (m_lineWidths[num2] + (float)extraDistance))
					{
						index = ((!m_continuous) ? (k / 2) : k);
						return true;
					}
				}
				index = -1;
				return false;
			}
			Vector3 vector5 = v3zero;
			for (int l = m_drawStart; l < num3; l += num7)
			{
				num2 += num;
				Vector3 vector6;
				if (flag)
				{
					vector6 = cam.WorldToScreenPoint(matrix4x.MultiplyPoint3x4(m_points3[l]));
					vector5 = cam.WorldToScreenPoint(matrix4x.MultiplyPoint3x4(m_points3[l + 1]));
				}
				else
				{
					vector6 = cam.WorldToScreenPoint(m_points3[l]);
					vector5 = cam.WorldToScreenPoint(m_points3[l + 1]);
				}
				if (vector6.z < 0f || vector5.z < 0f)
				{
					continue;
				}
				vector3.x = (int)vector6.x;
				vector4.x = (int)vector5.x;
				vector3.y = (int)vector6.y;
				vector4.y = (int)vector5.y;
				if (vector3.x != vector4.x || vector3.y != vector4.y)
				{
					if (extraLength > 0)
					{
						zero = (vector3 - vector4).normalized * extraLength;
						vector3.x += zero.x;
						vector3.y += zero.y;
						vector4.x -= zero.x;
						vector4.y -= zero.y;
					}
					num6 = Vector2.Dot(p - vector3, vector4 - vector3) / (vector4 - vector3).sqrMagnitude;
					if (!(num6 < 0f) && !(num6 > 1f) && (p - (vector3 + num6 * (vector4 - vector3))).sqrMagnitude <= (m_lineWidths[num2] + (float)extraDistance) * (m_lineWidths[num2] + (float)extraDistance))
					{
						index = ((!m_continuous) ? (l / 2) : l);
						return true;
					}
				}
			}
			index = -1;
			return false;
		}

		private bool Approximately(Vector2 p1, Vector2 p2)
		{
			return Approximately(p1.x, p2.x) && Approximately(p1.y, p2.y);
		}

		private bool Approximately(Vector3 p1, Vector3 p2)
		{
			return Approximately(p1.x, p2.x) && Approximately(p1.y, p2.y) && Approximately(p1.z, p2.z);
		}

		private bool Approximately(float a, float b)
		{
			return Mathf.Round(a * 100f) / 100f == Mathf.Round(b * 100f) / 100f;
		}





	}
}
