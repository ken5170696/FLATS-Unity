using System;

namespace ImageTools.IO.Bmp
{
	internal class BmpFileHeader
	{
		public const int Size = 14;

		public short Type;

		public int FileSize;

		public int Reserved;

		public int Offset;

		public BmpFileHeader()
		{
		}


	}
}
