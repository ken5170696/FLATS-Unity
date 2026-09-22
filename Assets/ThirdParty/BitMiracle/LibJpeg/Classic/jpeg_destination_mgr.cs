using System;
namespace BitMiracle.LibJpeg.Classic
{
	internal abstract class jpeg_destination_mgr
	{
		private byte[] m_buffer;

		private int m_position;

		private int m_free_in_buffer;

		protected int freeInBuffer
		{
			get
			{
				return m_free_in_buffer;
			}
		}

		public abstract void init_destination();

		public abstract bool empty_output_buffer();

		public abstract void term_destination();

		public virtual bool emit_byte(int val)
		{
			m_buffer[m_position] = (byte)val;
			m_position++;
			if (--m_free_in_buffer == 0 && !empty_output_buffer())
			{
				return false;
			}
			return true;
		}

		protected void initInternalBuffer(byte[] buffer, int offset)
		{
			m_buffer = buffer;
			m_free_in_buffer = buffer.Length - offset;
			m_position = offset;
		}

		protected jpeg_destination_mgr()
		{
		}




	}
}
