using System;

namespace ImageTools.IO.Png
{
	internal sealed class PngHeader
	{
		public int Width;

		public int Height;

		public byte BitDepth;

		public byte ColorType;

		public byte CompressionMethod;

		public byte FilterMethod;

		public byte InterlaceMethod;

		public PngHeader()
		{
		}


	}
}
