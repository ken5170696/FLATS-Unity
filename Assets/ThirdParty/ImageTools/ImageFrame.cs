using System;
using System.Diagnostics.Contracts.Reign;

namespace ImageTools
{
	public class ImageFrame : ImageBase
	{
		public ImageFrame(ImageFrame other)
			: base(other)
		{
			Contract.Requires<ArgumentNullException>(other != null, "Other image cannot be null.");
			Contract.Requires<ArgumentException>(other.IsFilled, "Other image has not been loaded.");
		}

		public ImageFrame()
		{
		}


	}
}
