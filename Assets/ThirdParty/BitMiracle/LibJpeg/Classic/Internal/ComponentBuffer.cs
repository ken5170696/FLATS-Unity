using System;

namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class ComponentBuffer
	{
		private byte[][] m_buffer;

		private int[] m_funnyIndices;

		private int m_funnyOffset;

		public byte[] this[int i]
		{
			get
			{
				if (m_funnyIndices == null)
				{
					return m_buffer[i];
				}
				return m_buffer[m_funnyIndices[i + m_funnyOffset]];
			}
		}

		public ComponentBuffer()
		{
		}

		public ComponentBuffer(byte[][] buf, int[] funnyIndices, int funnyOffset)
		{
			SetBuffer(buf, funnyIndices, funnyOffset);
		}

		public void SetBuffer(byte[][] buf, int[] funnyIndices, int funnyOffset)
		{
			m_buffer = buf;
			m_funnyIndices = funnyIndices;
			m_funnyOffset = funnyOffset;
		}


	}
}
