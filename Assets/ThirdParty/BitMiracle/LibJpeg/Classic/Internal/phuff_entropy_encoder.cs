using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class phuff_entropy_encoder : jpeg_entropy_encoder
	{
		private enum MCUEncoder
		{
			mcu_DC_first_encoder,
			mcu_AC_first_encoder,
			mcu_DC_refine_encoder,
			mcu_AC_refine_encoder
		}

		private const int MAX_CORR_BITS = 1000;

		private MCUEncoder m_MCUEncoder;

		private bool m_gather_statistics;

		private int m_put_buffer;

		private int m_put_bits;

		private int[] m_last_dc_val;

		private int m_ac_tbl_no;

		private int m_EOBRUN;

		private int m_BE;

		private char[] m_bit_buffer;

		private int m_restarts_to_go;

		private int m_next_restart_num;

		private c_derived_tbl[] m_derived_tbls;

		private long[][] m_count_ptrs;

		public phuff_entropy_encoder(jpeg_compress_struct cinfo)
		{
			m_last_dc_val = new int[4];
			m_derived_tbls = new c_derived_tbl[4];
			m_count_ptrs = new long[4][];

			m_cinfo = cinfo;
			for (int i = 0; i < 4; i++)
			{
				m_derived_tbls[i] = null;
				m_count_ptrs[i] = null;
			}
		}

		public override void start_pass(bool gather_statistics)
		{
			m_gather_statistics = gather_statistics;
			bool flag = m_cinfo.m_Ss == 0;
			if (m_cinfo.m_Ah == 0)
			{
				if (flag)
				{
					m_MCUEncoder = MCUEncoder.mcu_DC_first_encoder;
				}
				else
				{
					m_MCUEncoder = MCUEncoder.mcu_AC_first_encoder;
				}
			}
			else if (flag)
			{
				m_MCUEncoder = MCUEncoder.mcu_DC_refine_encoder;
			}
			else
			{
				m_MCUEncoder = MCUEncoder.mcu_AC_refine_encoder;
				if (m_bit_buffer == null)
				{
					m_bit_buffer = new char[1000];
				}
			}
			for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
			{
				jpeg_component_info jpeg_component_info = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[i]];
				m_last_dc_val[i] = 0;
				int num;
				if (flag)
				{
					if (m_cinfo.m_Ah != 0)
					{
						continue;
					}
					num = jpeg_component_info.Dc_tbl_no;
				}
				else
				{
					m_ac_tbl_no = jpeg_component_info.Ac_tbl_no;
					num = jpeg_component_info.Ac_tbl_no;
				}
				if (m_gather_statistics)
				{
					if (num < 0 || num >= 4)
					{
						m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_HUFF_TABLE, num);
					}
					if (m_count_ptrs[num] == null)
					{
						m_count_ptrs[num] = new long[257];
					}
					Array.Clear(m_count_ptrs[num], 0, 257);
				}
				else
				{
					jpeg_make_c_derived_tbl(flag, num, ref m_derived_tbls[num]);
				}
			}
			m_EOBRUN = 0;
			m_BE = 0;
			m_put_buffer = 0;
			m_put_bits = 0;
			m_restarts_to_go = m_cinfo.m_restart_interval;
			m_next_restart_num = 0;
		}

		public override bool encode_mcu(JBLOCK[][] MCU_data)
		{
			switch (m_MCUEncoder)
			{
			case MCUEncoder.mcu_DC_first_encoder:
				return encode_mcu_DC_first(MCU_data);
			case MCUEncoder.mcu_AC_first_encoder:
				return encode_mcu_AC_first(MCU_data);
			case MCUEncoder.mcu_DC_refine_encoder:
				return encode_mcu_DC_refine(MCU_data);
			case MCUEncoder.mcu_AC_refine_encoder:
				return encode_mcu_AC_refine(MCU_data);
			default:
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
				return false;
			}
		}

		public override void finish_pass()
		{
			if (m_gather_statistics)
			{
				finish_pass_gather_phuff();
			}
			else
			{
				finish_pass_phuff();
			}
		}

		private bool encode_mcu_DC_first(JBLOCK[][] MCU_data)
		{
			if (m_cinfo.m_restart_interval != 0 && m_restarts_to_go == 0)
			{
				emit_restart(m_next_restart_num);
			}
			for (int i = 0; i < m_cinfo.m_blocks_in_MCU; i++)
			{
				int num = IRIGHT_SHIFT(MCU_data[i][0][0], m_cinfo.m_Al);
				int num2 = m_cinfo.m_MCU_membership[i];
				int num3 = num - m_last_dc_val[num2];
				m_last_dc_val[num2] = num;
				num = num3;
				if (num3 < 0)
				{
					num3 = -num3;
					num--;
				}
				int num4 = 0;
				while (num3 != 0)
				{
					num4++;
					num3 >>= 1;
				}
				if (num4 > jpeg_entropy_encoder.MAX_HUFFMAN_COEF_BITS + 1)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_DCT_COEF);
				}
				emit_symbol(m_cinfo.Component_info[m_cinfo.m_cur_comp_info[num2]].Dc_tbl_no, num4);
				if (num4 != 0)
				{
					emit_bits(num, num4);
				}
			}
			if (m_cinfo.m_restart_interval != 0)
			{
				if (m_restarts_to_go == 0)
				{
					m_restarts_to_go = m_cinfo.m_restart_interval;
					m_next_restart_num++;
					m_next_restart_num &= 7;
				}
				m_restarts_to_go--;
			}
			return true;
		}

		private bool encode_mcu_AC_first(JBLOCK[][] MCU_data)
		{
			if (m_cinfo.m_restart_interval != 0 && m_restarts_to_go == 0)
			{
				emit_restart(m_next_restart_num);
			}
			int num = 0;
			for (int i = m_cinfo.m_Ss; i <= m_cinfo.m_Se; i++)
			{
				int num2 = MCU_data[0][0][JpegUtils.jpeg_natural_order[i]];
				if (num2 == 0)
				{
					num++;
					continue;
				}
				int code;
				if (num2 < 0)
				{
					num2 = -num2;
					num2 >>= m_cinfo.m_Al;
					code = ~num2;
				}
				else
				{
					num2 >>= m_cinfo.m_Al;
					code = num2;
				}
				if (num2 == 0)
				{
					num++;
					continue;
				}
				if (m_EOBRUN > 0)
				{
					emit_eobrun();
				}
				while (num > 15)
				{
					emit_symbol(m_ac_tbl_no, 240);
					num -= 16;
				}
				int num3 = 1;
				while ((num2 >>= 1) != 0)
				{
					num3++;
				}
				if (num3 > jpeg_entropy_encoder.MAX_HUFFMAN_COEF_BITS)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_DCT_COEF);
				}
				emit_symbol(m_ac_tbl_no, (num << 4) + num3);
				emit_bits(code, num3);
				num = 0;
			}
			if (num > 0)
			{
				m_EOBRUN++;
				if (m_EOBRUN == 32767)
				{
					emit_eobrun();
				}
			}
			if (m_cinfo.m_restart_interval != 0)
			{
				if (m_restarts_to_go == 0)
				{
					m_restarts_to_go = m_cinfo.m_restart_interval;
					m_next_restart_num++;
					m_next_restart_num &= 7;
				}
				m_restarts_to_go--;
			}
			return true;
		}

		private bool encode_mcu_DC_refine(JBLOCK[][] MCU_data)
		{
			if (m_cinfo.m_restart_interval != 0 && m_restarts_to_go == 0)
			{
				emit_restart(m_next_restart_num);
			}
			for (int i = 0; i < m_cinfo.m_blocks_in_MCU; i++)
			{
				int num = MCU_data[i][0][0];
				emit_bits(num >> m_cinfo.m_Al, 1);
			}
			if (m_cinfo.m_restart_interval != 0)
			{
				if (m_restarts_to_go == 0)
				{
					m_restarts_to_go = m_cinfo.m_restart_interval;
					m_next_restart_num++;
					m_next_restart_num &= 7;
				}
				m_restarts_to_go--;
			}
			return true;
		}

		private bool encode_mcu_AC_refine(JBLOCK[][] MCU_data)
		{
			if (m_cinfo.m_restart_interval != 0 && m_restarts_to_go == 0)
			{
				emit_restart(m_next_restart_num);
			}
			int num = 0;
			int[] array = new int[64];
			for (int i = m_cinfo.m_Ss; i <= m_cinfo.m_Se; i++)
			{
				int num2 = MCU_data[0][0][JpegUtils.jpeg_natural_order[i]];
				if (num2 < 0)
				{
					num2 = -num2;
				}
				if ((array[i] = num2 >> m_cinfo.m_Al) == 1)
				{
					num = i;
				}
			}
			int num3 = 0;
			int num4 = 0;
			int num5 = m_BE;
			for (int j = m_cinfo.m_Ss; j <= m_cinfo.m_Se; j++)
			{
				int num6 = array[j];
				if (num6 == 0)
				{
					num3++;
					continue;
				}
				while (num3 > 15 && j <= num)
				{
					emit_eobrun();
					emit_symbol(m_ac_tbl_no, 240);
					num3 -= 16;
					emit_buffered_bits(num5, num4);
					num5 = 0;
					num4 = 0;
				}
				if (num6 > 1)
				{
					m_bit_buffer[num5 + num4] = (char)(num6 & 1);
					num4++;
					continue;
				}
				emit_eobrun();
				emit_symbol(m_ac_tbl_no, (num3 << 4) + 1);
				num6 = ((MCU_data[0][0][JpegUtils.jpeg_natural_order[j]] >= 0) ? 1 : 0);
				emit_bits(num6, 1);
				emit_buffered_bits(num5, num4);
				num5 = 0;
				num4 = 0;
				num3 = 0;
			}
			if (num3 > 0 || num4 > 0)
			{
				m_EOBRUN++;
				m_BE += num4;
				if (m_EOBRUN == 32767 || m_BE > 937)
				{
					emit_eobrun();
				}
			}
			if (m_cinfo.m_restart_interval != 0)
			{
				if (m_restarts_to_go == 0)
				{
					m_restarts_to_go = m_cinfo.m_restart_interval;
					m_next_restart_num++;
					m_next_restart_num &= 7;
				}
				m_restarts_to_go--;
			}
			return true;
		}

		private void finish_pass_phuff()
		{
			emit_eobrun();
			flush_bits();
		}

		private void finish_pass_gather_phuff()
		{
			emit_eobrun();
			bool[] array = new bool[4];
			bool flag = m_cinfo.m_Ss == 0;
			for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
			{
				jpeg_component_info jpeg_component_info = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[i]];
				int num = jpeg_component_info.Ac_tbl_no;
				if (flag)
				{
					if (m_cinfo.m_Ah != 0)
					{
						continue;
					}
					num = jpeg_component_info.Dc_tbl_no;
				}
				if (array[num])
				{
					continue;
				}
				JHUFF_TBL jHUFF_TBL = null;
				if (flag)
				{
					if (m_cinfo.m_dc_huff_tbl_ptrs[num] == null)
					{
						m_cinfo.m_dc_huff_tbl_ptrs[num] = new JHUFF_TBL();
					}
					jHUFF_TBL = m_cinfo.m_dc_huff_tbl_ptrs[num];
				}
				else
				{
					if (m_cinfo.m_ac_huff_tbl_ptrs[num] == null)
					{
						m_cinfo.m_ac_huff_tbl_ptrs[num] = new JHUFF_TBL();
					}
					jHUFF_TBL = m_cinfo.m_ac_huff_tbl_ptrs[num];
				}
				jpeg_gen_optimal_table(jHUFF_TBL, m_count_ptrs[num]);
				array[num] = true;
			}
		}

		private void emit_byte(int val)
		{
			m_cinfo.m_dest.emit_byte(val);
		}

		private void emit_bits(int code, int size)
		{
			int num = code;
			if (size == 0)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_HUFF_MISSING_CODE);
			}
			if (m_gather_statistics)
			{
				return;
			}
			num &= (1 << size) - 1;
			m_put_bits += size;
			num <<= 24 - m_put_bits;
			num |= m_put_buffer;
			while (m_put_bits >= 8)
			{
				int num2 = (num >> 16) & 0xFF;
				emit_byte(num2);
				if (num2 == 255)
				{
					emit_byte(0);
				}
				num <<= 8;
				m_put_bits -= 8;
			}
			m_put_buffer = num;
		}

		private void flush_bits()
		{
			emit_bits(127, 7);
			m_put_buffer = 0;
			m_put_bits = 0;
		}

		private void emit_symbol(int tbl_no, int symbol)
		{
			if (m_gather_statistics)
			{
				m_count_ptrs[tbl_no][symbol]++;
			}
			else
			{
				emit_bits(m_derived_tbls[tbl_no].ehufco[symbol], m_derived_tbls[tbl_no].ehufsi[symbol]);
			}
		}

		private void emit_buffered_bits(int offset, int nbits)
		{
			if (!m_gather_statistics)
			{
				for (int i = 0; i < nbits; i++)
				{
					emit_bits(m_bit_buffer[offset + i], 1);
				}
			}
		}

		private void emit_eobrun()
		{
			if (m_EOBRUN > 0)
			{
				int num = m_EOBRUN;
				int num2 = 0;
				while ((num >>= 1) != 0)
				{
					num2++;
				}
				if (num2 > 14)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_HUFF_MISSING_CODE);
				}
				emit_symbol(m_ac_tbl_no, num2 << 4);
				if (num2 != 0)
				{
					emit_bits(m_EOBRUN, num2);
				}
				m_EOBRUN = 0;
				emit_buffered_bits(0, m_BE);
				m_BE = 0;
			}
		}

		private void emit_restart(int restart_num)
		{
			emit_eobrun();
			if (!m_gather_statistics)
			{
				flush_bits();
				emit_byte(255);
				emit_byte(208 + restart_num);
			}
			if (m_cinfo.m_Ss == 0)
			{
				for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
				{
					m_last_dc_val[i] = 0;
				}
			}
			else
			{
				m_EOBRUN = 0;
				m_BE = 0;
			}
		}

		private static int IRIGHT_SHIFT(int x, int shft)
		{
			return x >> shft;
		}




	}
}
