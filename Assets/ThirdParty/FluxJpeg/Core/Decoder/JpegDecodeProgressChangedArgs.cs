using System;

namespace FluxJpeg.Core.Decoder
{
	internal class JpegDecodeProgressChangedArgs : EventArgs
	{
		public bool SizeReady;

		public int Width;

		public int Height;

		public long ReadPosition;

		public double DecodeProgress;

		public JpegDecodeProgressChangedArgs()
		{
		}


	}
}
