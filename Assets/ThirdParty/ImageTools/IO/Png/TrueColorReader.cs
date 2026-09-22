using System;
using ImageTools.Helpers;

namespace ImageTools.IO.Png
{
	internal sealed class TrueColorReader : IColorReader
	{
		private int _row;

		private bool _useAlpha;

		public TrueColorReader(bool useAlpha)
		{
			_useAlpha = useAlpha;
		}

		public void ReadScanline(byte[] scanline, byte[] pixels, PngHeader header)
		{
			int num = 0;
			byte[] array = scanline.ToArrayByBitsLength(header.BitDepth);
			if (_useAlpha)
			{
				Array.Copy(array, 0, pixels, _row * header.Width * 4, array.Length);
			}
			else
			{
				for (int i = 0; i < array.Length / 3; i++)
				{
					num = (_row * header.Width + i) * 4;
					pixels[num] = array[i * 3];
					pixels[num + 1] = array[i * 3 + 1];
					pixels[num + 2] = array[i * 3 + 2];
					pixels[num + 3] = byte.MaxValue;
				}
			}
			_row++;
		}


	}
}
