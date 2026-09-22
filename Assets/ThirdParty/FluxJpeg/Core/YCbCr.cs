using System;
namespace FluxJpeg.Core
{
	internal class YCbCr
	{
		public static void toRGB(ref byte c1, ref byte c2, ref byte c3)
		{
			double num = (int)c1;
			double num2 = (double)(int)c2 - 128.0;
			double num3 = (double)(int)c3 - 128.0;
			double num4 = num + 1.402 * num3;
			double num5 = num - 0.34414 * num2 - 0.71414 * num3;
			double num6 = num + 1.772 * num2;
			c1 = (byte)((num4 > 255.0) ? byte.MaxValue : ((!(num4 < 0.0)) ? ((byte)num4) : (byte)0));
			c2 = (byte)((num5 > 255.0) ? byte.MaxValue : ((!(num5 < 0.0)) ? ((byte)num5) : (byte)0));
			c3 = (byte)((num6 > 255.0) ? byte.MaxValue : ((!(num6 < 0.0)) ? ((byte)num6) : (byte)0));
		}

		public static void fromRGB(ref byte c1, ref byte c2, ref byte c3)
		{
			double num = (int)c1;
			double num2 = (int)c2;
			double num3 = (int)c3;
			c1 = (byte)(0.299 * num + 0.587 * num2 + 0.114 * num3);
			c2 = (byte)(-0.16874 * num - 0.33126 * num2 + 0.5 * num3 + 128.0);
			c3 = (byte)(0.5 * num - 0.41869 * num2 - 0.08131 * num3 + 128.0);
		}

		public static float[] fromRGB(float[] data)
		{
			return new float[3]
			{
				(float)(0.299 * (double)data[0] + 0.587 * (double)data[1] + 0.114 * (double)data[2]),
				128f + (float)(-0.16874 * (double)data[0] - 0.33126 * (double)data[1] + 0.5 * (double)data[2]),
				128f + (float)(0.5 * (double)data[0] - 0.41869 * (double)data[1] - 0.08131 * (double)data[2])
			};
		}

		public YCbCr()
		{
		}




	}
}
