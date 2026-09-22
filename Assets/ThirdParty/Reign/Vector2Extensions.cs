using System;
using UnityEngine;
namespace Reign
{
	public static class Vector2Extensions
	{
		public static Vector3 ToVector3(this Vector2 inVec)
		{
			Vector3 result = default(Vector3);
			result.x = inVec.x;
			result.y = inVec.y;
			result.z = 0f;
			return result;
		}

		public static Vector3 ToVector3(this Vector2 inVec, float z)
		{
			Vector3 result = default(Vector3);
			result.x = inVec.x;
			result.y = inVec.y;
			result.z = z;
			return result;
		}

		public static void Angle360(ref Vector2 vector, out float result)
		{
			Vector2 normalized = vector.normalized;
			float num = Mathf.Atan2(0f - normalized.y, normalized.x) % ((float)Math.PI * 2f);
			result = ((num < 0f) ? ((float)Math.PI + num + (float)Math.PI) : num);
		}

		public static void Lerp(ref Vector2 p1, ref Vector2 p2, float interpolation, out Vector2 p3)
		{
			p3.x = Mathf.Lerp(p1.x, p2.x, interpolation);
			p3.y = Mathf.Lerp(p1.y, p2.y, interpolation);
		}

		public static Vector2 QuadraticBezierCurve(Vector2 p1, Vector2 p2, Vector2 p3, float interpolation)
		{
			Vector2 vector = default(Vector2);
			vector.x = Mathf.Lerp(p1.x, p2.x, interpolation);
			vector.y = Mathf.Lerp(p1.y, p2.y, interpolation);
			Vector2 vector2 = default(Vector2);
			vector2.x = Mathf.Lerp(p2.x, p3.x, interpolation);
			vector2.y = Mathf.Lerp(p2.y, p3.y, interpolation);
			Vector2 result = default(Vector2);
			result.x = Mathf.Lerp(vector.x, vector2.x, interpolation);
			result.y = Mathf.Lerp(vector.y, vector2.y, interpolation);
			return result;
		}

		public static Vector2 CubicBezierCurve(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float interpolation)
		{
			Vector2 p5 = default(Vector2);
			p5.x = Mathf.Lerp(p1.x, p2.x, interpolation);
			p5.y = Mathf.Lerp(p1.y, p2.y, interpolation);
			Vector2 p6 = default(Vector2);
			p6.x = Mathf.Lerp(p3.x, p4.x, interpolation);
			p6.y = Mathf.Lerp(p3.y, p4.y, interpolation);
			Vector2 p7 = default(Vector2);
			p7.x = Mathf.Lerp(p5.x, p6.x, interpolation);
			p7.y = Mathf.Lerp(p5.y, p6.y, interpolation);
			return QuadraticBezierCurve(p5, p7, p6, interpolation);
		}

		public static Vector2 InersectRay(this Vector2 vector, Vector2 rayOrigin, Vector2 rayDirection)
		{
			return rayDirection * Vector2.Dot(vector - rayOrigin, rayDirection) + rayOrigin;
		}

		public static Vector2 IntersectLine(Vector2 point, Vector2 p1, Vector2 p2)
		{
			Vector2 lhs = point - p1;
			Vector2 normalized = (p2 - p1).normalized;
			return normalized * Vector2.Dot(lhs, normalized) + p1;
		}

		public static Vector2 IntersectLineSegment(this Vector2 vector, Vector2 point1, Vector2 point2)
		{
			Vector2 vector2 = point1 - point2;
			vector2.Normalize();
			Vector2 vector3 = vector2 * Vector2.Dot(vector - point1, vector2) + point1;
			Bound2 result;
			Bound2.FromPoints(ref point1, ref point2, out result);
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

		public static void GetBounds(Vector2[] points, out Vector2 min, out Vector2 max)
		{
			min = points[0];
			max = points[0];
			for (int i = 0; i != points.Length; i++)
			{
				Vector2 vector = points[i];
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
			}
		}

		public static void RotateVectors(Vector2[] vectors, float angle)
		{
			Quaternion quaternion = Quaternion.AngleAxis(angle * 57.29578f, new Vector3(0f, 0f, 1f));
			for (int i = 0; i != vectors.Length; i++)
			{
				vectors[i] = quaternion * vectors[i];
			}
		}

		public static void RotatePoints(Vector2[] points, float angle)
		{
			Vector2 min;
			Vector2 max;
			GetBounds(points, out min, out max);
			Vector2 vector = min + (max - min) * 0.5f;
			Vector3 vector2 = vector.ToVector3();
			Quaternion quaternion = Quaternion.AngleAxis(angle * 57.29578f, new Vector3(0f, 0f, 1f));
			for (int i = 0; i != points.Length; i++)
			{
				points[i] = (quaternion * (points[i] - vector) + vector2).ToVector2();
			}
		}

		public static void FlipPointsX(Vector2[] points)
		{
			Vector2 min;
			Vector2 max;
			GetBounds(points, out min, out max);
			Vector2 vector = min + (max - min) * 0.5f;
			for (int i = 0; i != points.Length; i++)
			{
				points[i].x = 0f - (points[i].x - vector.x) + vector.x;
			}
		}

		public static void FlipPointsY(Vector2[] points)
		{
			Vector2 min;
			Vector2 max;
			GetBounds(points, out min, out max);
			Vector2 vector = min + (max - min) * 0.5f;
			for (int i = 0; i != points.Length; i++)
			{
				points[i].y = 0f - (points[i].y - vector.y) + vector.y;
			}
		}


	}
}
