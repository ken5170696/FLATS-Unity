using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal abstract class jpeg_entropy_encoder
	{
		protected class c_derived_tbl
		{
			public int[] ehufco = new int[256];

			public char[] ehufsi = new char[256];
		}

		protected static int MAX_HUFFMAN_COEF_BITS = 10;

		private static int MAX_CLEN = 32;

		protected jpeg_compress_struct m_cinfo;

		public abstract void start_pass(bool gather_statistics);

		public abstract bool encode_mcu(JBLOCK[][] MCU_data);

		public abstract void finish_pass();

		protected void jpeg_make_c_derived_tbl(bool isDC, int tblno, ref c_derived_tbl dtbl)
		{
			if (tblno < 0 || tblno >= 4)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_HUFF_TABLE, tblno);
			}
			JHUFF_TBL jHUFF_TBL = (isDC ? m_cinfo.m_dc_huff_tbl_ptrs[tblno] : m_cinfo.m_ac_huff_tbl_ptrs[tblno]);
			if (jHUFF_TBL == null)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_HUFF_TABLE, tblno);
			}
			if (dtbl == null)
			{
				dtbl = new c_derived_tbl();
			}
			int num = 0;
			char[] array = new char[257];
			for (int i = 1; i <= 16; i++)
			{
				int num2 = jHUFF_TBL.Bits[i];
				if (num2 < 0 || num + num2 > 256)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_HUFF_TABLE);
				}
				while (num2-- != 0)
				{
					array[num++] = (char)i;
				}
			}
			array[num] = '\0';
			int num3 = num;
			int num4 = 0;
			int num5 = array[0];
			num = 0;
			int[] array2 = new int[257];
			while (array[num] != 0)
			{
				while (array[num] == num5)
				{
					array2[num++] = num4;
					num4++;
				}
				if (num4 >= 1 << num5)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_HUFF_TABLE);
				}
				num4 <<= 1;
				num5++;
			}
			Array.Clear(dtbl.ehufsi, 0, dtbl.ehufsi.Length);
			int num6 = (isDC ? 15 : 255);
			for (num = 0; num < num3; num++)
			{
				int num7 = jHUFF_TBL.Huffval[num];
				if (num7 < 0 || num7 > num6 || dtbl.ehufsi[num7] != 0)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_HUFF_TABLE);
				}
				dtbl.ehufco[num7] = array2[num];
				dtbl.ehufsi[num7] = array[num];
			}
		}

		protected void jpeg_gen_optimal_table(JHUFF_TBL htbl, long[] freq)
		{
			byte[] array = new byte[MAX_CLEN + 1];
			int[] array2 = new int[257];
			int[] array3 = new int[257];
			int i;
			for (i = 0; i < 257; i++)
			{
				array3[i] = -1;
			}
			freq[256] = 1L;
			while (true)
			{
				int num = -1;
				long num2 = 1000000000L;
				for (i = 0; i <= 256; i++)
				{
					if (freq[i] != 0 && freq[i] <= num2)
					{
						num2 = freq[i];
						num = i;
					}
				}
				int num3 = -1;
				num2 = 1000000000L;
				for (i = 0; i <= 256; i++)
				{
					if (freq[i] != 0 && freq[i] <= num2 && i != num)
					{
						num2 = freq[i];
						num3 = i;
					}
				}
				if (num3 < 0)
				{
					break;
				}
				freq[num] += freq[num3];
				freq[num3] = 0L;
				array2[num]++;
				while (array3[num] >= 0)
				{
					num = array3[num];
					array2[num]++;
				}
				array3[num] = num3;
				array2[num3]++;
				while (array3[num3] >= 0)
				{
					num3 = array3[num3];
					array2[num3]++;
				}
			}
			for (i = 0; i <= 256; i++)
			{
				if (array2[i] != 0)
				{
					if (array2[i] > MAX_CLEN)
					{
						m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_HUFF_CLEN_OVERFLOW);
					}
					array[array2[i]]++;
				}
			}
			for (i = MAX_CLEN; i > 16; i--)
			{
				while (array[i] > 0)
				{
					int num4 = i - 2;
					while (array[num4] == 0)
					{
						num4--;
					}
					array[i] -= 2;
					array[i - 1]++;
					array[num4 + 1] += 2;
					array[num4]--;
				}
			}
			while (array[i] == 0)
			{
				i--;
			}
			array[i]--;
			Buffer.BlockCopy(array, 0, htbl.Bits, 0, htbl.Bits.Length);
			int num5 = 0;
			for (i = 1; i <= MAX_CLEN; i++)
			{
				for (int num4 = 0; num4 <= 255; num4++)
				{
					if (array2[num4] == i)
					{
						htbl.Huffval[num5] = (byte)num4;
						num5++;
					}
				}
			}
			htbl.Sent_table = false;
		}

		protected jpeg_entropy_encoder()
		{
		}




	}
}
