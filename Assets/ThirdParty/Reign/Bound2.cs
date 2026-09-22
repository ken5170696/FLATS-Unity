using UnityEngine;

namespace Reign
{
	public struct Bound2
	{
		public float left;

		public float right;

		public float bottom;

		public float top;

		public static readonly Bound2 zero = default(Bound2);

		public Vector2 Min
		{
			get
			{
				Vector2 result = default(Vector2);
				result.x = left;
				result.y = bottom;
				return result;
			}
		}

		public Vector2 Max
		{
			get
			{
				Vector2 result = default(Vector2);
				result.x = right;
				result.y = top;
				return result;
			}
		}

		public Bound2(float left, float right, float bottom, float top)
		{
			this.left = left;
			this.right = right;
			this.bottom = bottom;
			this.top = top;
		}

		public Bound2(Vector2 point)
		{
			left = point.x;
			right = point.x;
			bottom = point.y;
			top = point.y;
		}

		public Rect ToRect()
		{
			return new Rect(left, top, right - left, bottom - top);
		}

		public static void FromPoints(ref Vector2 point1, ref Vector2 point2, out Bound2 result)
		{
			result.left = point1.x;
			result.right = point1.x;
			result.bottom = point1.y;
			result.top = point1.y;
			if (point2.x < result.left)
			{
				result.left = point2.x;
			}
			if (point2.x > result.right)
			{
				result.right = point2.x;
			}
			if (point2.y < result.bottom)
			{
				result.bottom = point2.y;
			}
			if (point2.y > result.top)
			{
				result.top = point2.y;
			}
		}

		public void AddPoint(Vector2 point, float radius)
		{
			float num = point.x - radius;
			if (num < left)
			{
				left = num;
			}
			num = point.x + radius;
			if (num > right)
			{
				right = num;
			}
			num = point.y - radius;
			if (num < top)
			{
				top = num;
			}
			num = point.y + radius;
			if (num > bottom)
			{
				bottom = num;
			}
		}

		public void AddRect(Rect rect)
		{
			if (rect.xMin < left)
			{
				left = rect.xMin;
			}
			if (rect.xMax > right)
			{
				right = rect.xMax;
			}
			if (rect.yMin < top)
			{
				top = rect.yMin;
			}
			if (rect.yMax > bottom)
			{
				bottom = rect.yMax;
			}
		}

		public bool Intersects(Vector2 point)
		{
			if (point.x >= left && point.x <= right && point.y >= bottom)
			{
				return point.y <= top;
			}
			return false;
		}

		public bool Intersects(Vector2 point, float radius)
		{
			if (point.x + radius >= left && point.x - radius <= right && point.y + radius >= bottom)
			{
				return point.y - radius <= top;
			}
			return false;
		}
	}
}
