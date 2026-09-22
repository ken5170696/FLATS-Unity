using System;

namespace ImageTools.IO.Gif
{
	internal sealed class GifLogicalScreenDescriptor
	{
		public short Width;

		public short Height;

		public byte Background;

		public bool GlobalColorTableFlag;

		public int GlobalColorTableSize;

		public GifLogicalScreenDescriptor()
		{
		}


	}
}
