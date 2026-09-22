using System;

namespace ImageTools.IO
{
	public class UnsupportedImageFormatException : Exception
	{
		public UnsupportedImageFormatException()
		{
		}

		public UnsupportedImageFormatException(string errorMessage)
			: base(errorMessage)
		{
		}

		public UnsupportedImageFormatException(string errorMessage, Exception innerEx)
			: base(errorMessage, innerEx)
		{
		}
	}
}
