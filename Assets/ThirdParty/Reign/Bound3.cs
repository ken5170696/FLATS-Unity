using UnityEngine;

namespace Reign
{
	public struct Bound3
	{
		public float left;

		public float right;

		public float bottom;

		public float top;

		public float back;

		public float front;

		public static readonly Bound3 zero = default(Bound3);

		public Vector3 Min
		{
			get
			{
				Vector3 result = default(Vector3);
				result.x = left;
				result.y = bottom;
				result.z = back;
				return result;
			}
		}

		public Vector3 Max
		{
			get
			{
				Vector3 result = default(Vector3);
				result.x = right;
				result.y = top;
				result.z = front;
				return result;
			}
		}

		public Bound3(float left, float right, float bottom, float top, float back, float front)
		{
			this.left = left;
			this.right = right;
			this.bottom = bottom;
			this.top = top;
			this.back = back;
			this.front = front;
		}

		public Bound3(Vector3 point)
		{
			left = point.x;
			right = point.x;
			bottom = point.y;
			top = point.y;
			back = point.z;
			front = point.z;
		}

		public static void FromPoints(ref Vector3 point1, ref Vector3 point2, out Bound3 result)
		{
			result.left = point1.x;
			result.right = point1.x;
			result.bottom = point1.y;
			result.top = point1.y;
			result.back = point1.z;
			result.front = point1.z;
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
			if (point2.z < result.back)
			{
				result.back = point2.z;
			}
			if (point2.z > result.front)
			{
				result.front = point2.z;
			}
		}

		public void AddPoint(Vector3 point, float radius)
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
			if (num < bottom)
			{
				bottom = num;
			}
			num = point.y + radius;
			if (num > top)
			{
				top = num;
			}
			num = point.z - radius;
			if (num < back)
			{
				back = num;
			}
			num = point.z + radius;
			if (num > front)
			{
				front = num;
			}
		}

		public bool Intersects(Vector3 point)
		{
			if (point.x >= left && point.x <= right && point.y >= bottom && point.y <= top && point.z >= back)
			{
				return point.z <= front;
			}
			return false;
		}

		public bool Intersects(Vector3 point, float radius)
		{
			if (point.x + radius >= left && point.x - radius <= right && point.y + radius >= bottom && point.y - radius <= top && point.z + radius >= back)
			{
				return point.z - radius <= front;
			}
			return false;
		}
	}
}
