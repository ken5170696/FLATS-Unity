using System.IO;
using UnityEngine;
namespace Reign
{
	public static class StreamExtensions
	{
		public static void WriteVector(this BinaryWriter writer, Vector2 value)
		{
			writer.Write(value.x);
			writer.Write(value.y);
		}

		public static void WriteVector(this BinaryWriter writer, Vector3 value)
		{
			writer.Write(value.x);
			writer.Write(value.y);
			writer.Write(value.z);
		}

		public static void WriteVector(this BinaryWriter writer, Vector4 value)
		{
			writer.Write(value.x);
			writer.Write(value.y);
			writer.Write(value.z);
			writer.Write(value.w);
		}

		public static Vector2 ReadVector2(this BinaryReader reader)
		{
			return new Vector2(reader.ReadSingle(), reader.ReadSingle());
		}

		public static Vector3 ReadVector3(this BinaryReader reader)
		{
			return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
		}

		public static Vector4 ReadVector4(this BinaryReader reader)
		{
			return new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
		}

		public static void WriteMatrix(this BinaryWriter writer, Matrix4x4 value)
		{
			writer.WriteVector(new Vector4(value.m00, value.m01, value.m02, value.m03));
			writer.WriteVector(new Vector4(value.m10, value.m11, value.m12, value.m13));
			writer.WriteVector(new Vector4(value.m20, value.m21, value.m22, value.m23));
			writer.WriteVector(new Vector4(value.m30, value.m31, value.m32, value.m33));
		}

		public static Matrix4x4 ReadMatrix4(this BinaryReader reader)
		{
			Vector4 vector = reader.ReadVector4();
			Vector4 vector2 = reader.ReadVector4();
			Vector4 vector3 = reader.ReadVector4();
			Vector4 vector4 = reader.ReadVector4();
			return new Matrix4x4
			{
				m00 = vector.x,
				m01 = vector.y,
				m02 = vector.z,
				m03 = vector.w,
				m10 = vector2.x,
				m11 = vector2.y,
				m12 = vector2.z,
				m13 = vector2.w,
				m20 = vector3.x,
				m21 = vector3.y,
				m22 = vector3.z,
				m23 = vector3.w,
				m30 = vector4.x,
				m31 = vector4.y,
				m32 = vector4.z,
				m33 = vector4.w
			};
		}


	}
}
