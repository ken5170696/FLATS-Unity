using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class phuff_entropy_decoder : jpeg_entropy_decoder
	{
		private class savable_state
		{
			public int EOBRUN;

			public int[] last_dc_val = new int[4];

			public void Assign(savable_state ss)
			{
				EOBRUN = ss.EOBRUN;
				Buffer.BlockCopy(ss.last_dc_val, 0, last_dc_val, 0, last_dc_val.Length * 4);
			}
		}

		private enum MCUDecoder
		{
			mcu_DC_first_decoder,
			mcu_AC_first_decoder,
			mcu_DC_refine_decoder,
			mcu_AC_refine_decoder
		}

		private MCUDecoder m_decoder;

		private bitread_perm_state m_bitstate;

		private savable_state m_saved;

		private int m_restarts_to_go;

		private d_derived_tbl[] m_derived_tbls;

		private d_derived_tbl m_ac_derived_tbl;

		public phuff_entropy_decoder(jpeg_decompress_struct cinfo)
		{
			m_saved = new savable_state();
			m_derived_tbls = new d_derived_tbl[4];

			m_cinfo = cinfo;
			for (int i = 0; i < 4; i++)
			{
				m_derived_tbls[i] = null;
			}
			cinfo.m_coef_bits = new int[cinfo.m_num_components][];
			for (int j = 0; j < cinfo.m_num_components; j++)
			{
				cinfo.m_coef_bits[j] = new int[64];
			}
			for (int k = 0; k < cinfo.m_num_components; k++)
			{
				for (int l = 0; l < 64; l++)
				{
					cinfo.m_coef_bits[k][l] = -1;
				}
			}
		}

		public override void start_pass()
		{
			bool flag = false;
			bool flag2 = m_cinfo.m_Ss == 0;
			if (flag2)
			{
				if (m_cinfo.m_Se != 0)
				{
					flag = true;
				}
			}
			else
			{
				if (m_cinfo.m_Ss > m_cinfo.m_Se || m_cinfo.m_Se >= 64)
				{
					flag = true;
				}
				if (m_cinfo.m_comps_in_scan != 1)
				{
					flag = true;
				}
			}
			if (m_cinfo.m_Ah != 0 && m_cinfo.m_Al != m_cinfo.m_Ah - 1)
			{
				flag = true;
			}
			if (m_cinfo.m_Al > 13)
			{
				flag = true;
			}
			if (flag)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_PROGRESSION, m_cinfo.m_Ss, m_cinfo.m_Se, m_cinfo.m_Ah, m_cinfo.m_Al);
			}
			for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
			{
				int component_index = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[i]].Component_index;
				if (!flag2 && m_cinfo.m_coef_bits[component_index][0] < 0)
				{
					m_cinfo.WARNMS(J_MESSAGE_CODE.JWRN_BOGUS_PROGRESSION, component_index, 0);
				}
				for (int j = m_cinfo.m_Ss; j <= m_cinfo.m_Se; j++)
				{
					int num = m_cinfo.m_coef_bits[component_index][j];
					if (num < 0)
					{
						num = 0;
					}
					if (m_cinfo.m_Ah != num)
					{
						m_cinfo.WARNMS(J_MESSAGE_CODE.JWRN_BOGUS_PROGRESSION, component_index, j);
					}
					m_cinfo.m_coef_bits[component_index][j] = m_cinfo.m_Al;
				}
			}
			if (m_cinfo.m_Ah == 0)
			{
				if (flag2)
				{
					m_decoder = MCUDecoder.mcu_DC_first_decoder;
				}
				else
				{
					m_decoder = MCUDecoder.mcu_AC_first_decoder;
				}
			}
			else if (flag2)
			{
				m_decoder = MCUDecoder.mcu_DC_refine_decoder;
			}
			else
			{
				m_decoder = MCUDecoder.mcu_AC_refine_decoder;
			}
			for (int k = 0; k < m_cinfo.m_comps_in_scan; k++)
			{
				jpeg_component_info jpeg_component_info = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[k]];
				if (flag2)
				{
					if (m_cinfo.m_Ah == 0)
					{
						jpeg_make_d_derived_tbl(true, jpeg_component_info.Dc_tbl_no, ref m_derived_tbls[jpeg_component_info.Dc_tbl_no]);
					}
				}
				else
				{
					jpeg_make_d_derived_tbl(false, jpeg_component_info.Ac_tbl_no, ref m_derived_tbls[jpeg_component_info.Ac_tbl_no]);
					m_ac_derived_tbl = m_derived_tbls[jpeg_component_info.Ac_tbl_no];
				}
				m_saved.last_dc_val[k] = 0;
			}
			m_bitstate.bits_left = 0;
			m_bitstate.get_buffer = 0;
			m_insufficient_data = false;
			m_saved.EOBRUN = 0;
			m_restarts_to_go = m_cinfo.m_restart_interval;
		}

		public override bool decode_mcu(JBLOCK[] MCU_data)
		{
			switch (m_decoder)
			{
			case MCUDecoder.mcu_DC_first_decoder:
				return decode_mcu_DC_first(MCU_data);
			case MCUDecoder.mcu_AC_first_decoder:
				return decode_mcu_AC_first(MCU_data);
			case MCUDecoder.mcu_DC_refine_decoder:
				return decode_mcu_DC_refine(MCU_data);
			case MCUDecoder.mcu_AC_refine_decoder:
				return decode_mcu_AC_refine(MCU_data);
			default:
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
				return false;
			}
		}

		private bool decode_mcu_DC_first(JBLOCK[] MCU_data)
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
					int num = m_cinfo.m_MCU_membership[i];
					int result;
					if (!jpeg_entropy_decoder.HUFF_DECODE(out result, ref br_state, m_derived_tbls[m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[num]].Dc_tbl_no], ref get_buffer, ref bits_left))
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
					result += savable_state.last_dc_val[num];
					savable_state.last_dc_val[num] = result;
					MCU_data[i][0] = (short)(result << m_cinfo.m_Al);
				}
				jpeg_entropy_decoder.BITREAD_SAVE_STATE(ref m_bitstate, get_buffer, bits_left);
				m_saved.Assign(savable_state);
			}
			m_restarts_to_go--;
			return true;
		}

		private bool decode_mcu_AC_first(JBLOCK[] MCU_data)
		{
			if (m_cinfo.m_restart_interval != 0 && m_restarts_to_go == 0 && !process_restart())
			{
				return false;
			}
			if (!m_insufficient_data)
			{
				int num = m_saved.EOBRUN;
				if (num > 0)
				{
					num--;
				}
				else
				{
					bitread_working_state br_state = default(bitread_working_state);
					int get_buffer;
					int bits_left;
					BITREAD_LOAD_STATE(m_bitstate, out get_buffer, out bits_left, ref br_state);
					int num2;
					for (num2 = m_cinfo.m_Ss; num2 <= m_cinfo.m_Se; num2++)
					{
						int result;
						if (!jpeg_entropy_decoder.HUFF_DECODE(out result, ref br_state, m_ac_derived_tbl, ref get_buffer, ref bits_left))
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
							MCU_data[0][JpegUtils.jpeg_natural_order[num2]] = (short)(result << m_cinfo.m_Al);
						}
						else
						{
							if (num3 != 15)
							{
								num = 1 << num3;
								if (num3 != 0)
								{
									if (!jpeg_entropy_decoder.CHECK_BIT_BUFFER(ref br_state, num3, ref get_buffer, ref bits_left))
									{
										return false;
									}
									num3 = jpeg_entropy_decoder.GET_BITS(num3, get_buffer, ref bits_left);
									num += num3;
								}
								num--;
								break;
							}
							num2 += 15;
						}
					}
					jpeg_entropy_decoder.BITREAD_SAVE_STATE(ref m_bitstate, get_buffer, bits_left);
				}
				m_saved.EOBRUN = num;
			}
			m_restarts_to_go--;
			return true;
		}

		private bool decode_mcu_DC_refine(JBLOCK[] MCU_data)
		{
			if (m_cinfo.m_restart_interval != 0 && m_restarts_to_go == 0 && !process_restart())
			{
				return false;
			}
			bitread_working_state br_state = default(bitread_working_state);
			int get_buffer;
			int bits_left;
			BITREAD_LOAD_STATE(m_bitstate, out get_buffer, out bits_left, ref br_state);
			for (int i = 0; i < m_cinfo.m_blocks_in_MCU; i++)
			{
				if (!jpeg_entropy_decoder.CHECK_BIT_BUFFER(ref br_state, 1, ref get_buffer, ref bits_left))
				{
					return false;
				}
				if (jpeg_entropy_decoder.GET_BITS(1, get_buffer, ref bits_left) != 0)
				{
					MCU_data[i][0] |= (short)(1 << m_cinfo.m_Al);
				}
			}
			jpeg_entropy_decoder.BITREAD_SAVE_STATE(ref m_bitstate, get_buffer, bits_left);
			m_restarts_to_go--;
			return true;
		}

		private bool decode_mcu_AC_refine(JBLOCK[] MCU_data)
		{
			int num = 1 << m_cinfo.m_Al;
			int num2 = -1 << m_cinfo.m_Al;
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
				int num3 = m_saved.EOBRUN;
				int num_newnz = 0;
				int[] array = new int[64];
				int i = m_cinfo.m_Ss;
				if (num3 == 0)
				{
					for (; i <= m_cinfo.m_Se; i++)
					{
						int result;
						if (!jpeg_entropy_decoder.HUFF_DECODE(out result, ref br_state, m_ac_derived_tbl, ref get_buffer, ref bits_left))
						{
							undo_decode_mcu_AC_refine(MCU_data, array, num_newnz);
							return false;
						}
						int num4 = result >> 4;
						result &= 0xF;
						if (result != 0)
						{
							if (result != 1)
							{
								m_cinfo.WARNMS(J_MESSAGE_CODE.JWRN_HUFF_BAD_CODE);
							}
							if (!jpeg_entropy_decoder.CHECK_BIT_BUFFER(ref br_state, 1, ref get_buffer, ref bits_left))
							{
								undo_decode_mcu_AC_refine(MCU_data, array, num_newnz);
								return false;
							}
							result = ((jpeg_entropy_decoder.GET_BITS(1, get_buffer, ref bits_left) == 0) ? num2 : num);
						}
						else if (num4 != 15)
						{
							num3 = 1 << num4;
							if (num4 == 0)
							{
								break;
							}
							if (!jpeg_entropy_decoder.CHECK_BIT_BUFFER(ref br_state, num4, ref get_buffer, ref bits_left))
							{
								undo_decode_mcu_AC_refine(MCU_data, array, num_newnz);
								return false;
							}
							num4 = jpeg_entropy_decoder.GET_BITS(num4, get_buffer, ref bits_left);
							num3 += num4;
							break;
						}
						do
						{
							int index = JpegUtils.jpeg_natural_order[i];
							short num5 = MCU_data[0][index];
							if (num5 != 0)
							{
								if (!jpeg_entropy_decoder.CHECK_BIT_BUFFER(ref br_state, 1, ref get_buffer, ref bits_left))
								{
									undo_decode_mcu_AC_refine(MCU_data, array, num_newnz);
									return false;
								}
								if (jpeg_entropy_decoder.GET_BITS(1, get_buffer, ref bits_left) != 0 && (num5 & num) == 0)
								{
									if (num5 >= 0)
									{
										MCU_data[0][index] += (short)num;
									}
									else
									{
										MCU_data[0][index] += (short)num2;
									}
								}
							}
							else if (--num4 < 0)
							{
								break;
							}
							i++;
						}
						while (i <= m_cinfo.m_Se);
						if (result != 0)
						{
							int num6 = JpegUtils.jpeg_natural_order[i];
							MCU_data[0][num6] = (short)result;
							array[num_newnz++] = num6;
						}
					}
				}
				if (num3 > 0)
				{
					for (; i <= m_cinfo.m_Se; i++)
					{
						int index2 = JpegUtils.jpeg_natural_order[i];
						short num7 = MCU_data[0][index2];
						if (num7 == 0)
						{
							continue;
						}
						if (!jpeg_entropy_decoder.CHECK_BIT_BUFFER(ref br_state, 1, ref get_buffer, ref bits_left))
						{
							undo_decode_mcu_AC_refine(MCU_data, array, num_newnz);
							return false;
						}
						if (jpeg_entropy_decoder.GET_BITS(1, get_buffer, ref bits_left) != 0 && (num7 & num) == 0)
						{
							if (num7 >= 0)
							{
								MCU_data[0][index2] += (short)num;
							}
							else
							{
								MCU_data[0][index2] += (short)num2;
							}
						}
					}
					num3--;
				}
				jpeg_entropy_decoder.BITREAD_SAVE_STATE(ref m_bitstate, get_buffer, bits_left);
				m_saved.EOBRUN = num3;
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
			m_saved.EOBRUN = 0;
			m_restarts_to_go = m_cinfo.m_restart_interval;
			if (m_cinfo.m_unread_marker == 0)
			{
				m_insufficient_data = false;
			}
			return true;
		}

		private static void undo_decode_mcu_AC_refine(JBLOCK[] block, int[] newnz_pos, int num_newnz)
		{
			while (num_newnz > 0)
			{
				block[0][newnz_pos[--num_newnz]] = 0;
			}
		}




	}
}
