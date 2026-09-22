using System;
namespace BitMiracle.LibJpeg.Classic
{
	internal class JHUFF_TBL
	{
		private readonly byte[] m_bits;

		private readonly byte[] m_huffval;

		private bool m_sent_table;

		internal byte[] Bits
		{
			get
			{
				return m_bits;
			}
		}

		internal byte[] Huffval
		{
			get
			{
				return m_huffval;
			}
		}

		public bool Sent_table
		{
			get
			{
				return m_sent_table;
			}
			set
			{
				m_sent_table = value;
			}
		}

		internal JHUFF_TBL()
		{
			m_bits = new byte[17];
			m_huffval = new byte[256];

		}




	}
}
