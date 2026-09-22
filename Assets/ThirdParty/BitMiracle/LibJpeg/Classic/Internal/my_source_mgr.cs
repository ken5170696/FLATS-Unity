using System;
using System.IO;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class my_source_mgr : jpeg_source_mgr
	{
		private const int INPUT_BUF_SIZE = 4096;

		private jpeg_decompress_struct m_cinfo;

		private Stream m_infile;

		private byte[] m_buffer;

		private bool m_start_of_file;

		public my_source_mgr(jpeg_decompress_struct cinfo)
		{
			m_cinfo = cinfo;
			m_buffer = new byte[4096];
		}

		public void Attach(Stream infile)
		{
			m_infile = infile;
			m_infile.Seek(0L, SeekOrigin.Begin);
			initInternalBuffer(null, 0);
		}

		public override void init_source()
		{
			m_start_of_file = true;
		}

		public override bool fill_input_buffer()
		{
			int num = m_infile.Read(m_buffer, 0, 4096);
			if (num <= 0)
			{
				if (m_start_of_file)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_INPUT_EMPTY);
				}
				m_cinfo.WARNMS(J_MESSAGE_CODE.JWRN_JPEG_EOF);
				m_buffer[0] = byte.MaxValue;
				m_buffer[1] = 217;
				num = 2;
			}
			initInternalBuffer(m_buffer, num);
			m_start_of_file = false;
			return true;
		}




	}
}
