using System;

namespace ImageTools.IO.Png
{
	internal sealed class PngChunk
	{
		public int Length;

		public string Type;

		public byte[] Data;

		public uint Crc;

		public PngChunk()
		{
		}


	}
}
