using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class huff_entropy_encoder : jpeg_entropy_encoder
	{
		private class savable_state
		{
			public int put_buffer;

			public int put_bits;

			public int[] last_dc_val = new int[4];
		}

		private bool m_gather_statistics;

		private savable_state m_saved;

		private int m_restarts_to_go;

		private int m_next_restart_num;

		private c_derived_tbl[] m_dc_derived_tbls;

		private c_derived_tbl[] m_ac_derived_tbls;

		private long[][] m_dc_count_ptrs;

		private long[][] m_ac_count_ptrs;

		public huff_entropy_encoder(jpeg_compress_struct cinfo)
		{
			m_saved = new savable_state();
			m_dc_derived_tbls = new c_derived_tbl[4];
			m_ac_derived_tbls = new c_derived_tbl[4];
			m_dc_count_ptrs = new long[4][];
			m_ac_count_ptrs = new long[4][];

			m_cinfo = cinfo;
			for (int i = 0; i < 4; i++)
			{
				m_dc_derived_tbls[i] = (m_ac_derived_tbls[i] = null);
				m_dc_count_ptrs[i] = (m_ac_count_ptrs[i] = null);
			}
		}

		public override void start_pass(bool gather_statistics)
		{
			m_gather_statistics = gather_statistics;
			for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
			{
				int dc_tbl_no = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[i]].Dc_tbl_no;
				int ac_tbl_no = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[i]].Ac_tbl_no;
				if (m_gather_statistics)
				{
					if (dc_tbl_no < 0 || dc_tbl_no >= 4)
					{
						m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_HUFF_TABLE, dc_tbl_no);
					}
					if (ac_tbl_no < 0 || ac_tbl_no >= 4)
					{
						m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_HUFF_TABLE, ac_tbl_no);
					}
					if (m_dc_count_ptrs[dc_tbl_no] == null)
					{
						m_dc_count_ptrs[dc_tbl_no] = new long[257];
					}
					Array.Clear(m_dc_count_ptrs[dc_tbl_no], 0, m_dc_count_ptrs[dc_tbl_no].Length);
					if (m_ac_count_ptrs[ac_tbl_no] == null)
					{
						m_ac_count_ptrs[ac_tbl_no] = new long[257];
					}
					Array.Clear(m_ac_count_ptrs[ac_tbl_no], 0, m_ac_count_ptrs[ac_tbl_no].Length);
				}
				else
				{
					jpeg_make_c_derived_tbl(true, dc_tbl_no, ref m_dc_derived_tbls[dc_tbl_no]);
					jpeg_make_c_derived_tbl(false, ac_tbl_no, ref m_ac_derived_tbls[ac_tbl_no]);
				}
				m_saved.last_dc_val[i] = 0;
			}
			m_saved.put_buffer = 0;
			m_saved.put_bits = 0;
			m_restarts_to_go = m_cinfo.m_restart_interval;
			m_next_restart_num = 0;
		}

		public override bool encode_mcu(JBLOCK[][] MCU_data)
		{
			if (m_gather_statistics)
			{
				return encode_mcu_gather(MCU_data);
			}
			return encode_mcu_huff(MCU_data);
		}

		public override void finish_pass()
		{
			if (m_gather_statistics)
			{
				finish_pass_gather();
			}
			else
			{
				finish_pass_huff();
			}
		}

		private bool encode_mcu_huff(JBLOCK[][] MCU_data)
		{
			savable_state saved = m_saved;
			if (m_cinfo.m_restart_interval != 0 && m_restarts_to_go == 0 && !emit_restart(saved, m_next_restart_num))
			{
				return false;
			}
			for (int i = 0; i < m_cinfo.m_blocks_in_MCU; i++)
			{
				int num = m_cinfo.m_MCU_membership[i];
				if (!encode_one_block(saved, MCU_data[i][0].data, saved.last_dc_val[num], m_dc_derived_tbls[m_cinfo.Component_info[m_cinfo.m_cur_comp_info[num]].Dc_tbl_no], m_ac_derived_tbls[m_cinfo.Component_info[m_cinfo.m_cur_comp_info[num]].Ac_tbl_no]))
				{
					return false;
				}
				saved.last_dc_val[num] = MCU_data[i][0][0];
			}
			m_saved = saved;
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

		private void finish_pass_huff()
		{
			savable_state saved = m_saved;
			if (!flush_bits(saved))
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CANT_SUSPEND);
			}
			m_saved = saved;
		}

		private bool encode_mcu_gather(JBLOCK[][] MCU_data)
		{
			if (m_cinfo.m_restart_interval != 0)
			{
				if (m_restarts_to_go == 0)
				{
					for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
					{
						m_saved.last_dc_val[i] = 0;
					}
					m_restarts_to_go = m_cinfo.m_restart_interval;
				}
				m_restarts_to_go--;
			}
			for (int j = 0; j < m_cinfo.m_blocks_in_MCU; j++)
			{
				int num = m_cinfo.m_MCU_membership[j];
				htest_one_block(MCU_data[j][0].data, m_saved.last_dc_val[num], m_dc_count_ptrs[m_cinfo.Component_info[m_cinfo.m_cur_comp_info[num]].Dc_tbl_no], m_ac_count_ptrs[m_cinfo.Component_info[m_cinfo.m_cur_comp_info[num]].Ac_tbl_no]);
				m_saved.last_dc_val[num] = MCU_data[j][0][0];
			}
			return true;
		}

		private void finish_pass_gather()
		{
			bool[] array = new bool[4];
			bool[] array2 = new bool[4];
			for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
			{
				int dc_tbl_no = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[i]].Dc_tbl_no;
				if (!array[dc_tbl_no])
				{
					if (m_cinfo.m_dc_huff_tbl_ptrs[dc_tbl_no] == null)
					{
						m_cinfo.m_dc_huff_tbl_ptrs[dc_tbl_no] = new JHUFF_TBL();
					}
					jpeg_gen_optimal_table(m_cinfo.m_dc_huff_tbl_ptrs[dc_tbl_no], m_dc_count_ptrs[dc_tbl_no]);
					array[dc_tbl_no] = true;
				}
				int ac_tbl_no = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[i]].Ac_tbl_no;
				if (!array2[ac_tbl_no])
				{
					if (m_cinfo.m_ac_huff_tbl_ptrs[ac_tbl_no] == null)
					{
						m_cinfo.m_ac_huff_tbl_ptrs[ac_tbl_no] = new JHUFF_TBL();
					}
					jpeg_gen_optimal_table(m_cinfo.m_ac_huff_tbl_ptrs[ac_tbl_no], m_ac_count_ptrs[ac_tbl_no]);
					array2[ac_tbl_no] = true;
				}
			}
		}

		private bool encode_one_block(savable_state state, short[] block, int last_dc_val, c_derived_tbl dctbl, c_derived_tbl actbl)
		{
			int num = block[0] - last_dc_val;
			int num2 = num;
			if (num < 0)
			{
				num = -num;
				num2--;
			}
			int num3 = 0;
			while (num != 0)
			{
				num3++;
				num >>= 1;
			}
			if (num3 > jpeg_entropy_encoder.MAX_HUFFMAN_COEF_BITS + 1)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_DCT_COEF);
			}
			if (!emit_bits(state, dctbl.ehufco[num3], dctbl.ehufsi[num3]))
			{
				return false;
			}
			if (num3 != 0 && !emit_bits(state, num2, num3))
			{
				return false;
			}
			int num4 = 0;
			for (int i = 1; i < 64; i++)
			{
				num = block[JpegUtils.jpeg_natural_order[i]];
				if (num == 0)
				{
					num4++;
					continue;
				}
				while (num4 > 15)
				{
					if (!emit_bits(state, actbl.ehufco[240], actbl.ehufsi[240]))
					{
						return false;
					}
					num4 -= 16;
				}
				num2 = num;
				if (num < 0)
				{
					num = -num;
					num2--;
				}
				num3 = 1;
				while ((num >>= 1) != 0)
				{
					num3++;
				}
				if (num3 > jpeg_entropy_encoder.MAX_HUFFMAN_COEF_BITS)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_DCT_COEF);
				}
				int num5 = (num4 << 4) + num3;
				if (!emit_bits(state, actbl.ehufco[num5], actbl.ehufsi[num5]))
				{
					return false;
				}
				if (!emit_bits(state, num2, num3))
				{
					return false;
				}
				num4 = 0;
			}
			if (num4 > 0 && !emit_bits(state, actbl.ehufco[0], actbl.ehufsi[0]))
			{
				return false;
			}
			return true;
		}

		private void htest_one_block(short[] block, int last_dc_val, long[] dc_counts, long[] ac_counts)
		{
			int num = block[0] - last_dc_val;
			if (num < 0)
			{
				num = -num;
			}
			int num2 = 0;
			while (num != 0)
			{
				num2++;
				num >>= 1;
			}
			if (num2 > jpeg_entropy_encoder.MAX_HUFFMAN_COEF_BITS + 1)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_DCT_COEF);
			}
			dc_counts[num2]++;
			int num3 = 0;
			for (int i = 1; i < 64; i++)
			{
				num = block[JpegUtils.jpeg_natural_order[i]];
				if (num == 0)
				{
					num3++;
					continue;
				}
				while (num3 > 15)
				{
					ac_counts[240]++;
					num3 -= 16;
				}
				if (num < 0)
				{
					num = -num;
				}
				num2 = 1;
				while ((num >>= 1) != 0)
				{
					num2++;
				}
				if (num2 > jpeg_entropy_encoder.MAX_HUFFMAN_COEF_BITS)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_DCT_COEF);
				}
				ac_counts[(num3 << 4) + num2]++;
				num3 = 0;
			}
			if (num3 > 0)
			{
				ac_counts[0]++;
			}
		}

		private bool emit_byte(int val)
		{
			return m_cinfo.m_dest.emit_byte(val);
		}

		private bool emit_bits(savable_state state, int code, int size)
		{
			int num = code;
			int put_bits = state.put_bits;
			if (size == 0)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_HUFF_MISSING_CODE);
			}
			num &= (1 << size) - 1;
			put_bits += size;
			num <<= 24 - put_bits;
			num |= state.put_buffer;
			while (put_bits >= 8)
			{
				int num2 = (num >> 16) & 0xFF;
				if (!emit_byte(num2))
				{
					return false;
				}
				if (num2 == 255 && !emit_byte(0))
				{
					return false;
				}
				num <<= 8;
				put_bits -= 8;
			}
			state.put_buffer = num;
			state.put_bits = put_bits;
			return true;
		}

		private bool flush_bits(savable_state state)
		{
			if (!emit_bits(state, 127, 7))
			{
				return false;
			}
			state.put_buffer = 0;
			state.put_bits = 0;
			return true;
		}

		private bool emit_restart(savable_state state, int restart_num)
		{
			if (!flush_bits(state))
			{
				return false;
			}
			if (!emit_byte(255))
			{
				return false;
			}
			if (!emit_byte(208 + restart_num))
			{
				return false;
			}
			for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
			{
				state.last_dc_val[i] = 0;
			}
			return true;
		}




	}
}
