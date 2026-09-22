using System;
namespace ImageTools.Filtering
{
	public sealed class NearestNeighborResizer : IImageResizer
	{
		public void Resize(ImageBase source, ImageBase target, int width, int height)
		{
			byte[] array = new byte[width * height * 4];
			double num = (double)source.PixelWidth / (double)width;
			double num2 = (double)source.PixelHeight / (double)height;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			byte[] pixels = source.Pixels;
			for (int i = 0; i < height; i++)
			{
				num3 = 4 * width * i;
				num5 = 4 * source.PixelWidth * (int)((double)i * num2);
				for (int j = 0; j < width; j++)
				{
					num4 = num3 + 4 * j;
					num6 = num5 + 4 * (int)((double)j * num);
					array[num4] = pixels[num6];
					array[num4 + 1] = pixels[num6 + 1];
					array[num4 + 2] = pixels[num6 + 2];
					array[num4 + 3] = pixels[num6 + 3];
				}
			}
			target.SetPixels(width, height, array);
		}

		public NearestNeighborResizer()
		{
		}




	}
}
