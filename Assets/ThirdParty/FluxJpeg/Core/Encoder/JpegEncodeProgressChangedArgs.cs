using System;

namespace FluxJpeg.Core.Encoder
{
	internal class JpegEncodeProgressChangedArgs : EventArgs
	{
		public double EncodeProgress;

		public JpegEncodeProgressChangedArgs()
		{
		}


	}
}
