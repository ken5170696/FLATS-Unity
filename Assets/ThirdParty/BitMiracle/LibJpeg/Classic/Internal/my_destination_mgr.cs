using System;
using System.IO;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class my_destination_mgr : jpeg_destination_mgr
	{
		private const int OUTPUT_BUF_SIZE = 4096;

		private jpeg_compress_struct m_cinfo;

		private Stream m_outfile;

		private byte[] m_buffer;

		public my_destination_mgr(jpeg_compress_struct cinfo, Stream alreadyOpenFile)
		{
			m_cinfo = cinfo;
			m_outfile = alreadyOpenFile;
		}

		public override void init_destination()
		{
			m_buffer = new byte[4096];
			initInternalBuffer(m_buffer, 0);
		}

		public override bool empty_output_buffer()
		{
			writeBuffer(m_buffer.Length);
			initInternalBuffer(m_buffer, 0);
			return true;
		}

		public override void term_destination()
		{
			int num = m_buffer.Length - base.freeInBuffer;
			if (num > 0)
			{
				writeBuffer(num);
			}
			m_outfile.Flush();
		}

		private void writeBuffer(int dataCount)
		{
			try
			{
				m_outfile.Write(m_buffer, 0, dataCount);
			}
			catch (IOException ex)
			{
				m_cinfo.TRACEMS(0, J_MESSAGE_CODE.JERR_FILE_WRITE, ex.Message);
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_FILE_WRITE);
			}
			catch (NotSupportedException ex2)
			{
				m_cinfo.TRACEMS(0, J_MESSAGE_CODE.JERR_FILE_WRITE, ex2.Message);
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_FILE_WRITE);
			}
			catch (ObjectDisposedException ex3)
			{
				m_cinfo.TRACEMS(0, J_MESSAGE_CODE.JERR_FILE_WRITE, ex3.Message);
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_FILE_WRITE);
			}
		}




	}
}
