using System;
using ImageTools.Helpers;

namespace ImageTools.IO.Png
{
	internal sealed class PaletteIndexReader : IColorReader
	{
		private int _row;

		private byte[] _palette;

		private byte[] _paletteAlpha;

		public PaletteIndexReader(byte[] palette, byte[] paletteAlpha)
		{
			_palette = palette;
			_paletteAlpha = paletteAlpha;
		}

		public void ReadScanline(byte[] scanline, byte[] pixels, PngHeader header)
		{
			byte[] array = scanline.ToArrayByBitsLength(header.BitDepth);
			int num = 0;
			int num2 = 0;
			if (_paletteAlpha != null && _paletteAlpha.Length > 0)
			{
				for (int i = 0; i < header.Width; i++)
				{
					num2 = array[i];
					num = (_row * header.Width + i) * 4;
					pixels[num] = _palette[num2 * 3];
					pixels[num + 1] = _palette[num2 * 3 + 1];
					pixels[num + 2] = _palette[num2 * 3 + 2];
					pixels[num + 3] = ((_paletteAlpha.Length > num2) ? _paletteAlpha[num2] : byte.MaxValue);
				}
			}
			else
			{
				for (int j = 0; j < header.Width; j++)
				{
					num2 = array[j];
					num = (_row * header.Width + j) * 4;
					pixels[num] = _palette[num2 * 3];
					pixels[num + 1] = _palette[num2 * 3 + 1];
					pixels[num + 2] = _palette[num2 * 3 + 2];
					pixels[num + 3] = byte.MaxValue;
				}
			}
			_row++;
		}


	}
}
