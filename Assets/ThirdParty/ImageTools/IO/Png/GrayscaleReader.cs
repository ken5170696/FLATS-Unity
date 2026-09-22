using System;
using ImageTools.Helpers;

namespace ImageTools.IO.Png
{
	internal sealed class GrayscaleReader : IColorReader
	{
		private int _row;

		private bool _useAlpha;

		public GrayscaleReader(bool useAlpha)
		{
			_useAlpha = useAlpha;
		}

		public void ReadScanline(byte[] scanline, byte[] pixels, PngHeader header)
		{
			int num = 0;
			byte[] array = scanline.ToArrayByBitsLength(header.BitDepth);
			if (_useAlpha)
			{
				for (int i = 0; i < header.Width / 2; i++)
				{
					num = (_row * header.Width + i) * 4;
					pixels[num] = array[i * 2];
					pixels[num + 1] = array[i * 2];
					pixels[num + 2] = array[i * 2];
					pixels[num + 3] = array[i * 2 + 1];
				}
			}
			else
			{
				for (int j = 0; j < header.Width; j++)
				{
					num = (_row * header.Width + j) * 4;
					pixels[num] = array[j];
					pixels[num + 1] = array[j];
					pixels[num + 2] = array[j];
					pixels[num + 3] = byte.MaxValue;
				}
			}
			_row++;
		}


	}
}
