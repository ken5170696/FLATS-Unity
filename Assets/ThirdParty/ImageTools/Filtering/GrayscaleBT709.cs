using System;

namespace ImageTools.Filtering
{
	public sealed class GrayscaleBT709 : Grayscale
	{
		public GrayscaleBT709()
			: base(0.2125, 0.7154, 0.0721)
		{
		}


	}
}
