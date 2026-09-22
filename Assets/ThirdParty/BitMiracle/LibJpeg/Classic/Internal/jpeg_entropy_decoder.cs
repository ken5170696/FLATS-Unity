using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal abstract class jpeg_entropy_decoder
	{
		protected const int BIT_BUF_SIZE = 32;

		protected const int MIN_GET_BITS = 25;

		private static int[] extend_test = new int[16]
		{
			0, 1, 2, 4, 8, 16, 32, 64, 128, 256,
			512, 1024, 2048, 4096, 8192, 16384
		};

		private static int[] extend_offset = new int[16]
		{
			0, -1, -3, -7, -15, -31, -63, -127, -255, -511,
			-1023, -2047, -4095, -8191, -16383, -32767
		};

		protected jpeg_decompress_struct m_cinfo;

		protected bool m_insufficient_data;

		public abstract void start_pass();

		public abstract bool decode_mcu(JBLOCK[] MCU_data);

		protected static int HUFF_EXTEND(int x, int s)
		{
			if (x >= extend_test[s])
			{
				return x;
			}
			return x + extend_offset[s];
		}

		protected void BITREAD_LOAD_STATE(bitread_perm_state bitstate, out int get_buffer, out int bits_left, ref bitread_working_state br_state)
		{
			br_state.cinfo = m_cinfo;
			get_buffer = bitstate.get_buffer;
			bits_left = bitstate.bits_left;
		}

		protected static void BITREAD_SAVE_STATE(ref bitread_perm_state bitstate, int get_buffer, int bits_left)
		{
			bitstate.get_buffer = get_buffer;
			bitstate.bits_left = bits_left;
		}

		protected void jpeg_make_d_derived_tbl(bool isDC, int tblno, ref d_derived_tbl dtbl)
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
				dtbl = new d_derived_tbl();
			}
			dtbl.pub = jHUFF_TBL;
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
			int[] array2 = new int[257];
			num = 0;
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
			num = 0;
			for (int j = 1; j <= 16; j++)
			{
				if (jHUFF_TBL.Bits[j] != 0)
				{
					dtbl.valoffset[j] = num - array2[num];
					num += jHUFF_TBL.Bits[j];
					dtbl.maxcode[j] = array2[num - 1];
				}
				else
				{
					dtbl.maxcode[j] = -1;
				}
			}
			dtbl.maxcode[17] = 1048575;
			Array.Clear(dtbl.look_nbits, 0, dtbl.look_nbits.Length);
			num = 0;
			for (int k = 1; k <= 8; k++)
			{
				int num6 = 1;
				while (num6 <= jHUFF_TBL.Bits[k])
				{
					int num7 = array2[num] << 8 - k;
					for (int num8 = 1 << 8 - k; num8 > 0; num8--)
					{
						dtbl.look_nbits[num7] = k;
						dtbl.look_sym[num7] = jHUFF_TBL.Huffval[num];
						num7++;
					}
					num6++;
					num++;
				}
			}
			if (!isDC)
			{
				return;
			}
			for (int l = 0; l < num3; l++)
			{
				int num9 = jHUFF_TBL.Huffval[l];
				if (num9 < 0 || num9 > 15)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_HUFF_TABLE);
				}
			}
		}

		protected static bool CHECK_BIT_BUFFER(ref bitread_working_state state, int nbits, ref int get_buffer, ref int bits_left)
		{
			if (bits_left < nbits)
			{
				if (!jpeg_fill_bit_buffer(ref state, get_buffer, bits_left, nbits))
				{
					return false;
				}
				get_buffer = state.get_buffer;
				bits_left = state.bits_left;
			}
			return true;
		}

		protected static int GET_BITS(int nbits, int get_buffer, ref int bits_left)
		{
			return (get_buffer >> (bits_left -= nbits)) & ((1 << nbits) - 1);
		}

		protected static int PEEK_BITS(int nbits, int get_buffer, int bits_left)
		{
			return (get_buffer >> bits_left - nbits) & ((1 << nbits) - 1);
		}

		protected static void DROP_BITS(int nbits, ref int bits_left)
		{
			bits_left -= nbits;
		}

		protected static bool jpeg_fill_bit_buffer(ref bitread_working_state state, int get_buffer, int bits_left, int nbits)
		{
			bool flag = false;
			if (state.cinfo.m_unread_marker == 0)
			{
				while (bits_left < 25)
				{
					int V;
					state.cinfo.m_src.GetByte(out V);
					if (V == 255)
					{
						do
						{
							state.cinfo.m_src.GetByte(out V);
						}
						while (V == 255);
						if (V != 0)
						{
							state.cinfo.m_unread_marker = V;
							flag = true;
							break;
						}
						V = 255;
					}
					get_buffer = (get_buffer << 8) | V;
					bits_left += 8;
				}
			}
			else
			{
				flag = true;
			}
			if (flag && nbits > bits_left)
			{
				if (!state.cinfo.m_entropy.m_insufficient_data)
				{
					state.cinfo.WARNMS(J_MESSAGE_CODE.JWRN_HIT_MARKER);
					state.cinfo.m_entropy.m_insufficient_data = true;
				}
				get_buffer <<= 25 - bits_left;
				bits_left = 25;
			}
			state.get_buffer = get_buffer;
			state.bits_left = bits_left;
			return true;
		}

		protected static bool HUFF_DECODE(out int result, ref bitread_working_state state, d_derived_tbl htbl, ref int get_buffer, ref int bits_left)
		{
			int min_bits = 0;
			bool flag = false;
			if (bits_left < 8)
			{
				if (!jpeg_fill_bit_buffer(ref state, get_buffer, bits_left, 0))
				{
					result = -1;
					return false;
				}
				get_buffer = state.get_buffer;
				bits_left = state.bits_left;
				if (bits_left < 8)
				{
					min_bits = 1;
					flag = true;
				}
			}
			if (!flag)
			{
				int num = PEEK_BITS(8, get_buffer, bits_left);
				if ((min_bits = htbl.look_nbits[num]) != 0)
				{
					DROP_BITS(min_bits, ref bits_left);
					result = htbl.look_sym[num];
					return true;
				}
				min_bits = 9;
			}
			result = jpeg_huff_decode(ref state, get_buffer, bits_left, htbl, min_bits);
			if (result < 0)
			{
				return false;
			}
			get_buffer = state.get_buffer;
			bits_left = state.bits_left;
			return true;
		}

		protected static int jpeg_huff_decode(ref bitread_working_state state, int get_buffer, int bits_left, d_derived_tbl htbl, int min_bits)
		{
			int i = min_bits;
			if (!CHECK_BIT_BUFFER(ref state, i, ref get_buffer, ref bits_left))
			{
				return -1;
			}
			int num;
			for (num = GET_BITS(i, get_buffer, ref bits_left); num > htbl.maxcode[i]; i++)
			{
				num <<= 1;
				if (!CHECK_BIT_BUFFER(ref state, 1, ref get_buffer, ref bits_left))
				{
					return -1;
				}
				num |= GET_BITS(1, get_buffer, ref bits_left);
			}
			state.get_buffer = get_buffer;
			state.bits_left = bits_left;
			if (i > 16)
			{
				state.cinfo.WARNMS(J_MESSAGE_CODE.JWRN_HUFF_BAD_CODE);
				return 0;
			}
			return htbl.pub.Huffval[num + htbl.valoffset[i]];
		}

		protected jpeg_entropy_decoder()
		{
		}




	}
}
