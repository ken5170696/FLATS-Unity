using UnityEngine;
namespace Reign
{
	public static class Vector3Extensions
	{
		public static Vector2 ToVector2(this Vector3 inVec)
		{
			Vector2 result = default(Vector2);
			result.x = inVec.x;
			result.y = inVec.y;
			return result;
		}

		public static void Lerp(ref Vector3 p1, ref Vector3 p2, float interpolation, out Vector3 p3)
		{
			p3.x = Mathf.Lerp(p1.x, p2.x, interpolation);
			p3.y = Mathf.Lerp(p1.y, p2.y, interpolation);
			p3.z = Mathf.Lerp(p1.z, p2.z, interpolation);
		}

		public static Vector3 QuadraticBezierCurve(Vector3 p1, Vector3 p2, Vector3 p3, float interpolation)
		{
			Vector3 vector = default(Vector3);
			vector.x = Mathf.Lerp(p1.x, p2.x, interpolation);
			vector.y = Mathf.Lerp(p1.y, p2.y, interpolation);
			vector.z = Mathf.Lerp(p1.z, p2.z, interpolation);
			Vector3 vector2 = default(Vector3);
			vector2.x = Mathf.Lerp(p2.x, p3.x, interpolation);
			vector2.y = Mathf.Lerp(p2.y, p3.y, interpolation);
			vector2.z = Mathf.Lerp(p2.z, p3.z, interpolation);
			Vector3 result = default(Vector3);
			result.x = Mathf.Lerp(vector.x, vector2.x, interpolation);
			result.y = Mathf.Lerp(vector.y, vector2.y, interpolation);
			result.z = Mathf.Lerp(vector.z, vector2.z, interpolation);
			return result;
		}

		public static Vector3 CubicBezierCurve(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, float interpolation)
		{
			Vector3 p5 = default(Vector3);
			p5.x = Mathf.Lerp(p1.x, p2.x, interpolation);
			p5.y = Mathf.Lerp(p1.y, p2.y, interpolation);
			p5.z = Mathf.Lerp(p1.z, p2.z, interpolation);
			Vector3 p6 = default(Vector3);
			p6.x = Mathf.Lerp(p3.x, p4.x, interpolation);
			p6.y = Mathf.Lerp(p3.y, p4.y, interpolation);
			p6.z = Mathf.Lerp(p3.z, p4.z, interpolation);
			Vector3 p7 = default(Vector3);
			p7.x = Mathf.Lerp(p5.x, p6.x, interpolation);
			p7.y = Mathf.Lerp(p5.y, p6.y, interpolation);
			p7.z = Mathf.Lerp(p5.z, p6.z, interpolation);
			return QuadraticBezierCurve(p5, p7, p6, interpolation);
		}

		public static Vector3 InersectRay(this Vector3 vector, Vector3 rayOrigin, Vector3 rayDirection)
		{
			return rayDirection * Vector3.Dot(vector - rayOrigin, rayDirection) + rayOrigin;
		}

		public static Vector3 IntersectLine(Vector3 point, Vector3 p1, Vector3 p2)
		{
			Vector3 lhs = point - p1;
			Vector3 normalized = (p2 - p1).normalized;
			return normalized * Vector3.Dot(lhs, normalized) + p1;
		}

		public static Vector3 IntersectLineSegment(this Vector3 vector, Vector3 point1, Vector3 point2)
		{
			Vector3 vector2 = point1 - point2;
			vector2.Normalize();
			Vector3 vector3 = vector2 * Vector3.Dot(vector - point1, vector2) + point1;
			Bound3 result;
			Bound3.FromPoints(ref point1, ref point2, out result);
			if (!result.Intersects(vector3))
			{
				if ((vector - point1).magnitude <= (vector - point2).magnitude)
				{
					return point1;
				}
				return point2;
			}
			return vector3;
		}

		public static void GetBounds(Vector3[] points, out Vector3 min, out Vector3 max)
		{
			min = points[0];
			max = points[0];
			for (int i = 0; i != points.Length; i++)
			{
				Vector3 vector = points[i];
				if (vector.x < min.x)
				{
					min.x = vector.x;
				}
				if (vector.x > max.x)
				{
					max.x = vector.x;
				}
				if (vector.y < min.y)
				{
					min.y = vector.y;
				}
				if (vector.y > max.y)
				{
					max.y = vector.y;
				}
				if (vector.z < min.z)
				{
					min.z = vector.z;
				}
				if (vector.z > max.z)
				{
					max.z = vector.z;
				}
			}
		}

		public static void RotateVectors(Vector3[] vectors, float angle, Vector3 axis)
		{
			Quaternion quaternion = Quaternion.AngleAxis(angle * 57.29578f, axis);
			for (int i = 0; i != vectors.Length; i++)
			{
				vectors[i] = quaternion * vectors[i];
			}
		}

		public static void RotatePoints(Vector3[] points, float angle, Vector3 axis)
		{
			Vector3 min;
			Vector3 max;
			GetBounds(points, out min, out max);
			Vector3 vector = min + (max - min) * 0.5f;
			Quaternion quaternion = Quaternion.AngleAxis(angle * 57.29578f, axis);
			for (int i = 0; i != points.Length; i++)
			{
				points[i] = quaternion * (points[i] - vector) + vector;
			}
		}


	}
}
