using System;
namespace BitMiracle.LibJpeg.Classic
{
	internal class jvirt_array<T>
	{
		internal delegate T[][] Allocator(int width, int height);

		private jpeg_common_struct m_cinfo;

		private T[][] m_buffer;

		public jpeg_common_struct ErrorProcessor
		{
			get
			{
				return m_cinfo;
			}
			set
			{
				m_cinfo = value;
			}
		}

		internal jvirt_array(int width, int height, Allocator allocator)
		{
			m_cinfo = null;
			m_buffer = allocator(width, height);
		}

		public T[][] Access(int startRow, int numberOfRows)
		{
			if (startRow + numberOfRows > m_buffer.Length)
			{
				if (m_cinfo == null)
				{
					throw new InvalidOperationException("Bogus virtual array access");
				}
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_VIRTUAL_ACCESS);
			}
			T[][] array = new T[numberOfRows][];
			for (int i = 0; i < numberOfRows; i++)
			{
				array[i] = m_buffer[startRow + i];
			}
			return array;
		}




	}
}
