using UnityEngine;
namespace Reign
{
	public static class Vector4Extensions
	{
		public static Vector2 ToVector2(this Vector4 inVec)
		{
			Vector2 result = default(Vector2);
			result.x = inVec.x;
			result.y = inVec.y;
			return result;
		}

		public static Vector3 ToVector3(this Vector4 inVec)
		{
			Vector3 result = default(Vector3);
			result.x = inVec.x;
			result.y = inVec.y;
			result.z = inVec.z;
			return result;
		}


	}
}
