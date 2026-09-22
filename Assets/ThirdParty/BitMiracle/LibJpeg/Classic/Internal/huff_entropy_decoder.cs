using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class huff_entropy_decoder : jpeg_entropy_decoder
	{
		private class savable_state
		{
			public int[] last_dc_val = new int[4];

			public void Assign(savable_state ss)
			{
				Buffer.BlockCopy(ss.last_dc_val, 0, last_dc_val, 0, last_dc_val.Length * 4);
			}
		}

		private bitread_perm_state m_bitstate;

		private savable_state m_saved;

		private int m_restarts_to_go;

		private d_derived_tbl[] m_dc_derived_tbls;

		private d_derived_tbl[] m_ac_derived_tbls;

		private d_derived_tbl[] m_dc_cur_tbls;

		private d_derived_tbl[] m_ac_cur_tbls;

		private bool[] m_dc_needed;

		private bool[] m_ac_needed;

		public huff_entropy_decoder(jpeg_decompress_struct cinfo)
		{
			m_saved = new savable_state();
			m_dc_derived_tbls = new d_derived_tbl[4];
			m_ac_derived_tbls = new d_derived_tbl[4];
			m_dc_cur_tbls = new d_derived_tbl[10];
			m_ac_cur_tbls = new d_derived_tbl[10];
			m_dc_needed = new bool[10];
			m_ac_needed = new bool[10];

			m_cinfo = cinfo;
			for (int i = 0; i < 4; i++)
			{
				m_dc_derived_tbls[i] = (m_ac_derived_tbls[i] = null);
			}
		}

		public override void start_pass()
		{
			if (m_cinfo.m_Ss != 0 || m_cinfo.m_Se != 63 || m_cinfo.m_Ah != 0 || m_cinfo.m_Al != 0)
			{
				m_cinfo.WARNMS(J_MESSAGE_CODE.JWRN_NOT_SEQUENTIAL);
			}
			for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
			{
				jpeg_component_info jpeg_component_info = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[i]];
				int dc_tbl_no = jpeg_component_info.Dc_tbl_no;
				int ac_tbl_no = jpeg_component_info.Ac_tbl_no;
				jpeg_make_d_derived_tbl(true, dc_tbl_no, ref m_dc_derived_tbls[dc_tbl_no]);
				jpeg_make_d_derived_tbl(false, ac_tbl_no, ref m_ac_derived_tbls[ac_tbl_no]);
				m_saved.last_dc_val[i] = 0;
			}
			for (int j = 0; j < m_cinfo.m_blocks_in_MCU; j++)
			{
				int num = m_cinfo.m_MCU_membership[j];
				jpeg_component_info jpeg_component_info2 = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[num]];
				m_dc_cur_tbls[j] = m_dc_derived_tbls[jpeg_component_info2.Dc_tbl_no];
				m_ac_cur_tbls[j] = m_ac_derived_tbls[jpeg_component_info2.Ac_tbl_no];
				if (jpeg_component_info2.component_needed)
				{
					m_dc_needed[j] = true;
					m_ac_needed[j] = jpeg_component_info2.DCT_scaled_size > 1;
				}
				else
				{
					m_dc_needed[j] = (m_ac_needed[j] = false);
				}
			}
			m_bitstate.bits_left = 0;
			m_bitstate.get_buffer = 0;
			m_insufficient_data = false;
			m_restarts_to_go = m_cinfo.m_restart_interval;
		}

		public override bool decode_mcu(JBLOCK[] MCU_data)
		{
			if (m_cinfo.m_restart_interval != 0 && m_restarts_to_go == 0 && !process_restart())
			{
				return false;
			}
			if (!m_insufficient_data)
			{
				bitread_working_state br_state = default(bitread_working_state);
				int get_buffer;
				int bits_left;
				BITREAD_LOAD_STATE(m_bitstate, out get_buffer, out bits_left, ref br_state);
				savable_state savable_state = new savable_state();
				savable_state.Assign(m_saved);
				for (int i = 0; i < m_cinfo.m_blocks_in_MCU; i++)
				{
					int result;
					if (!jpeg_entropy_decoder.HUFF_DECODE(out result, ref br_state, m_dc_cur_tbls[i], ref get_buffer, ref bits_left))
					{
						return false;
					}
					if (result != 0)
					{
						if (!jpeg_entropy_decoder.CHECK_BIT_BUFFER(ref br_state, result, ref get_buffer, ref bits_left))
						{
							return false;
						}
						int x = jpeg_entropy_decoder.GET_BITS(result, get_buffer, ref bits_left);
						result = jpeg_entropy_decoder.HUFF_EXTEND(x, result);
					}
					if (m_dc_needed[i])
					{
						int num = m_cinfo.m_MCU_membership[i];
						result += savable_state.last_dc_val[num];
						savable_state.last_dc_val[num] = result;
						MCU_data[i][0] = (short)result;
					}
					if (m_ac_needed[i])
					{
						int num2;
						for (num2 = 1; num2 < 64; num2++)
						{
							if (!jpeg_entropy_decoder.HUFF_DECODE(out result, ref br_state, m_ac_cur_tbls[i], ref get_buffer, ref bits_left))
							{
								return false;
							}
							int num3 = result >> 4;
							result &= 0xF;
							if (result != 0)
							{
								num2 += num3;
								if (!jpeg_entropy_decoder.CHECK_BIT_BUFFER(ref br_state, result, ref get_buffer, ref bits_left))
								{
									return false;
								}
								num3 = jpeg_entropy_decoder.GET_BITS(result, get_buffer, ref bits_left);
								result = jpeg_entropy_decoder.HUFF_EXTEND(num3, result);
								MCU_data[i][JpegUtils.jpeg_natural_order[num2]] = (short)result;
							}
							else
							{
								if (num3 != 15)
								{
									break;
								}
								num2 += 15;
							}
						}
						continue;
					}
					int num4;
					for (num4 = 1; num4 < 64; num4++)
					{
						if (!jpeg_entropy_decoder.HUFF_DECODE(out result, ref br_state, m_ac_cur_tbls[i], ref get_buffer, ref bits_left))
						{
							return false;
						}
						int num5 = result >> 4;
						result &= 0xF;
						if (result != 0)
						{
							num4 += num5;
							if (!jpeg_entropy_decoder.CHECK_BIT_BUFFER(ref br_state, result, ref get_buffer, ref bits_left))
							{
								return false;
							}
							jpeg_entropy_decoder.DROP_BITS(result, ref bits_left);
						}
						else
						{
							if (num5 != 15)
							{
								break;
							}
							num4 += 15;
						}
					}
				}
				jpeg_entropy_decoder.BITREAD_SAVE_STATE(ref m_bitstate, get_buffer, bits_left);
				m_saved.Assign(savable_state);
			}
			m_restarts_to_go--;
			return true;
		}

		private bool process_restart()
		{
			m_cinfo.m_marker.SkipBytes(m_bitstate.bits_left / 8);
			m_bitstate.bits_left = 0;
			if (!m_cinfo.m_marker.read_restart_marker())
			{
				return false;
			}
			for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
			{
				m_saved.last_dc_val[i] = 0;
			}
			m_restarts_to_go = m_cinfo.m_restart_interval;
			if (m_cinfo.m_unread_marker == 0)
			{
				m_insufficient_data = false;
			}
			return true;
		}




	}
}
