using System;

namespace FluxJpeg.Core.IO
{
	internal class JPEGMarkerFoundException : Exception
	{
		public byte Marker;

		public JPEGMarkerFoundException(byte marker)
		{
			Marker = marker;
		}
	}
}
