using System;

namespace ImageTools.IO.Gif
{
	internal sealed class GifImageDescriptor
	{
		public short Left;

		public short Top;

		public short Width;

		public short Height;

		public bool LocalColorTableFlag;

		public int LocalColorTableSize;

		public bool InterlaceFlag;

		public GifImageDescriptor()
		{
		}


	}
}
