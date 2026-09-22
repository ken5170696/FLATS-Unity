using System;

namespace ImageTools
{
	public class ImageFormatException : Exception
	{
		public ImageFormatException()
		{
		}

		public ImageFormatException(string errorMessage)
			: base(errorMessage)
		{
		}

		public ImageFormatException(string errorMessage, Exception innerEx)
			: base(errorMessage, innerEx)
		{
		}
	}
}
