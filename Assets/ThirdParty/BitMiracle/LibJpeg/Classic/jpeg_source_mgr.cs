using System;
namespace BitMiracle.LibJpeg.Classic
{
	internal abstract class jpeg_source_mgr
	{
		private byte[] m_next_input_byte;

		private int m_bytes_in_buffer;

		private int m_position;

		public abstract void init_source();

		public abstract bool fill_input_buffer();

		protected void initInternalBuffer(byte[] buffer, int size)
		{
			m_bytes_in_buffer = size;
			m_next_input_byte = buffer;
			m_position = 0;
		}

		public virtual void skip_input_data(int num_bytes)
		{
			if (num_bytes > 0)
			{
				while (num_bytes > m_bytes_in_buffer)
				{
					num_bytes -= m_bytes_in_buffer;
					fill_input_buffer();
				}
				m_position += num_bytes;
				m_bytes_in_buffer -= num_bytes;
			}
		}

		public virtual bool resync_to_restart(jpeg_decompress_struct cinfo, int desired)
		{
			cinfo.WARNMS(J_MESSAGE_CODE.JWRN_MUST_RESYNC, cinfo.m_unread_marker, desired);
			int num = 1;
			while (true)
			{
				num = ((cinfo.m_unread_marker < 192) ? 2 : ((cinfo.m_unread_marker >= 208 && cinfo.m_unread_marker <= 215) ? ((cinfo.m_unread_marker != 208 + ((desired + 1) & 7) && cinfo.m_unread_marker != 208 + ((desired + 2) & 7)) ? ((cinfo.m_unread_marker != 208 + ((desired - 1) & 7) && cinfo.m_unread_marker != 208 + ((desired - 2) & 7)) ? 1 : 2) : 3) : 3));
				cinfo.TRACEMS(4, J_MESSAGE_CODE.JTRC_RECOVERY_ACTION, cinfo.m_unread_marker, num);
				switch (num)
				{
				case 1:
					cinfo.m_unread_marker = 0;
					return true;
				case 2:
					if (!cinfo.m_marker.next_marker())
					{
						return false;
					}
					break;
				case 3:
					return true;
				}
			}
		}

		public virtual void term_source()
		{
		}

		public virtual bool GetTwoBytes(out int V)
		{
			if (!MakeByteAvailable())
			{
				V = 0;
				return false;
			}
			m_bytes_in_buffer--;
			V = m_next_input_byte[m_position] << 8;
			m_position++;
			if (!MakeByteAvailable())
			{
				return false;
			}
			m_bytes_in_buffer--;
			V += m_next_input_byte[m_position];
			m_position++;
			return true;
		}

		public virtual bool GetByte(out int V)
		{
			if (!MakeByteAvailable())
			{
				V = 0;
				return false;
			}
			m_bytes_in_buffer--;
			V = m_next_input_byte[m_position];
			m_position++;
			return true;
		}

		public virtual int GetBytes(byte[] dest, int amount)
		{
			int num = amount;
			if (num > m_bytes_in_buffer)
			{
				num = m_bytes_in_buffer;
			}
			for (int i = 0; i < num; i++)
			{
				dest[i] = m_next_input_byte[m_position];
				m_position++;
				m_bytes_in_buffer--;
			}
			return num;
		}

		public virtual bool MakeByteAvailable()
		{
			if (m_bytes_in_buffer == 0 && !fill_input_buffer())
			{
				return false;
			}
			return true;
		}

		protected jpeg_source_mgr()
		{
		}




	}
}
