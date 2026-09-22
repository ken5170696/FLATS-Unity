using System;

namespace ImageTools.IO.Bmp
{
	internal class BmpInfoHeader
	{
		public const int Size = 40;

		public int HeaderSize;

		public int Width;

		public int Height;

		public short Planes;

		public short BitsPerPixel;

		public BmpCompression Compression;

		public int ImageSize;

		public int XPelsPerMeter;

		public int YPelsPerMeter;

		public int ClrUsed;

		public int ClrImportant;

		public BmpInfoHeader()
		{
		}


	}
}
