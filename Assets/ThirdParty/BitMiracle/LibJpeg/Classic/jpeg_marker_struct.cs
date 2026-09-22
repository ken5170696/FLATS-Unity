using System;
namespace BitMiracle.LibJpeg.Classic
{
	internal class jpeg_marker_struct
	{
		private byte m_marker;

		private int m_originalLength;

		private byte[] m_data;

		public byte Marker
		{
			get
			{
				return m_marker;
			}
		}

		public int OriginalLength
		{
			get
			{
				return m_originalLength;
			}
		}

		public byte[] Data
		{
			get
			{
				return m_data;
			}
		}

		internal jpeg_marker_struct(byte marker, int originalDataLength, int lengthLimit)
		{
			m_marker = marker;
			m_originalLength = originalDataLength;
			m_data = new byte[lengthLimit];
		}




	}
}
