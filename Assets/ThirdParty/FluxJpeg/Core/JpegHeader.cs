using System;
using System.Text;
namespace FluxJpeg.Core
{
	internal class JpegHeader
	{
		public byte Marker;

		public byte[] Data;

		internal bool IsJFIF;

		public new string ToString
		{
			get
			{
				return Encoding.UTF8.GetString(Data, 0, Data.Length);
			}
		}

		public JpegHeader()
		{
		}




	}
}
