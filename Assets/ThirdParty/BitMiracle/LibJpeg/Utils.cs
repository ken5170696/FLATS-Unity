using System;
using System.IO;
namespace BitMiracle.LibJpeg
{
	internal class Utils
	{
		public static MemoryStream CopyStream(Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException("stream");
			}
			long position = stream.Position;
			stream.Seek(0L, SeekOrigin.Begin);
			MemoryStream memoryStream = new MemoryStream((int)stream.Length);
			byte[] buffer = new byte[2048];
			int num;
			do
			{
				num = stream.Read(buffer, 0, 2048);
				memoryStream.Write(buffer, 0, num);
			}
			while (num >= 2048);
			stream.Seek(position, SeekOrigin.Begin);
			return memoryStream;
		}

		public static void CMYK2RGB(byte c, byte m, byte y, byte k, out byte red, out byte green, out byte blue)
		{
			float num = (float)(int)c / 255f;
			float num2 = (float)(int)m / 255f;
			float num3 = (float)(int)y / 255f;
			float num4 = (float)(int)k / 255f;
			float num5 = num * (1f - num4) + num4;
			float num6 = num2 * (1f - num4) + num4;
			float num7 = num3 * (1f - num4) + num4;
			num5 = (1f - num5) * 255f + 0.5f;
			num6 = (1f - num6) * 255f + 0.5f;
			num7 = (1f - num7) * 255f + 0.5f;
			red = (byte)(num5 * 255f);
			green = (byte)(num6 * 255f);
			blue = (byte)(num7 * 255f);
		}

		public Utils()
		{
		}




	}
}
