using System;
namespace ImageTools.Filtering
{
	public sealed class BilinearResizer : IImageResizer
	{
		public void Resize(ImageBase source, ImageBase target, int width, int height)
		{
			byte[] array = new byte[width * height * 4];
			byte[] sourcePixels = source.Pixels;
			Func<double, double, int, byte> GetColor = (double x, double y, int offset) => sourcePixels[(int)((y * (double)source.PixelWidth + x) * 4.0 + (double)offset)];
			double num = (double)source.PixelWidth / (double)width;
			double num2 = (double)source.PixelHeight / (double)height;
			double l;
			double t;
			double r;
			double b;
			double fractionX;
			double fractionY;
			double oneMinusX;
			double oneMinusY;
			byte c1;
			byte c2;
			byte c3;
			byte c4;
			byte b2;
			byte b3;
			for (int num3 = 0; num3 < height; num3++)
			{
				for (int num4 = 0; num4 < width; num4++)
				{
					int num5 = (num3 * width + num4) * 4;
					l = (int)Math.Floor((double)num4 * num);
					t = (int)Math.Floor((double)num3 * num2);
					r = l + 1.0;
					b = t + 1.0;
					if (r >= (double)source.PixelWidth)
					{
						r = l;
					}
					if (b >= (double)source.PixelHeight)
					{
						b = t;
					}
					fractionX = (double)num4 * num - l;
					fractionY = (double)num3 * num2 - t;
					oneMinusX = 1.0 - fractionX;
					oneMinusY = 1.0 - fractionY;
					Func<int, byte> func = delegate(int offset)
					{
						c1 = GetColor(l, t, offset);
						c2 = GetColor(r, t, offset);
						c3 = GetColor(l, b, offset);
						c4 = GetColor(r, b, offset);
						b2 = (byte)(oneMinusX * (double)(int)c1 + fractionX * (double)(int)c2);
						b3 = (byte)(oneMinusX * (double)(int)c3 + fractionX * (double)(int)c4);
						return (byte)(oneMinusY * (double)(int)b2 + fractionY * (double)(int)b3);
					};
					array[num5] = func(0);
					array[num5 + 1] = func(1);
					array[num5 + 2] = func(2);
					array[num5 + 3] = byte.MaxValue;
				}
			}
			target.SetPixels(width, height, array);
		}

		public BilinearResizer()
		{
		}




	}
}
