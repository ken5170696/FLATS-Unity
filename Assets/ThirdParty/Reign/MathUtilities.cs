using System;
using UnityEngine;
namespace Reign
{
	public static class MathUtilities
	{
		public const float Pi2 = (float)Math.PI * 2f;

		public static Vector2 FitInViewIfLarger(float objectWidth, float objectHeight, float viewWidth, float viewHeight)
		{
			Vector2 objectSize = default(Vector2);
			objectSize.x = objectWidth;
			objectSize.y = objectHeight;
			Vector2 viewSize = default(Vector2);
			viewSize.x = viewWidth;
			viewSize.y = viewHeight;
			return FitInViewIfLarger(objectSize, viewSize);
		}

		public static Vector2 FitInViewIfLarger(Vector2 objectSize, Vector2 viewSize)
		{
			if (objectSize.x <= viewSize.x && objectSize.y <= viewSize.y)
			{
				return objectSize;
			}
			return FitInView(objectSize, viewSize);
		}

		public static Vector2 FitInViewIfSmaller(float objectWidth, float objectHeight, float viewWidth, float viewHeight)
		{
			Vector2 objectSize = default(Vector2);
			objectSize.x = objectWidth;
			objectSize.y = objectHeight;
			Vector2 viewSize = default(Vector2);
			viewSize.x = viewWidth;
			viewSize.y = viewHeight;
			return FitInViewIfSmaller(objectSize, viewSize);
		}

		public static Vector2 FitInViewIfSmaller(Vector2 objectSize, Vector2 viewSize)
		{
			if (objectSize.x >= viewSize.x || objectSize.y >= viewSize.y)
			{
				return objectSize;
			}
			return FitInView(objectSize, viewSize);
		}

		public static Vector2 FitInView(float objectWidth, float objectHeight, float viewWidth, float viewHeight)
		{
			Vector2 objectSize = default(Vector2);
			objectSize.x = objectWidth;
			objectSize.y = objectHeight;
			Vector2 viewSize = default(Vector2);
			viewSize.x = viewWidth;
			viewSize.y = viewHeight;
			return FitInView(objectSize, viewSize);
		}

		public static Vector2 FitInView(Vector2 objectSize, Vector2 viewSize)
		{
			float num = objectSize.y / objectSize.x;
			float num2 = viewSize.y / viewSize.x;
			if (num >= num2)
			{
				return new Vector2(objectSize.x / objectSize.y, 1f) * viewSize.y;
			}
			return new Vector2(1f, objectSize.y / objectSize.x) * viewSize.x;
		}

		public static Vector2 ScaleToFitInView(float objectWidth, float objectHeight, float viewWidth, float viewHeight)
		{
			Vector2 objectSize = default(Vector2);
			objectSize.x = objectWidth;
			objectSize.y = objectHeight;
			Vector2 viewSize = default(Vector2);
			viewSize.x = viewWidth;
			viewSize.y = viewHeight;
			return ScaleToFitInView(objectSize, viewSize);
		}

		public static Vector2 ScaleToFitInView(Vector2 objectSize, Vector2 viewSize)
		{
			Vector2 vector = FitInView(objectSize, viewSize);
			objectSize.x /= vector.x;
			objectSize.y /= vector.y;
			return objectSize;
		}

		public static Vector2 FillView(float objectWidth, float objectHeight, float viewWidth, float viewHeight)
		{
			Vector2 objectSize = default(Vector2);
			objectSize.x = objectWidth;
			objectSize.y = objectHeight;
			Vector2 viewSize = default(Vector2);
			viewSize.x = viewWidth;
			viewSize.y = viewHeight;
			return FillView(objectSize, viewSize);
		}

		public static Vector2 FillView(Vector2 objectSize, Vector2 viewSize)
		{
			float num = objectSize.y / objectSize.x;
			float num2 = viewSize.y / viewSize.x;
			if (num <= num2)
			{
				return new Vector2(objectSize.x / objectSize.y, 1f) * viewSize.y;
			}
			return new Vector2(1f, objectSize.y / objectSize.x) * viewSize.x;
		}


	}
}
