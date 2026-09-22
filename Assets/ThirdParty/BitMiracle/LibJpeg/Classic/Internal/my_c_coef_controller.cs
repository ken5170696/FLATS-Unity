using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class my_c_coef_controller : jpeg_c_coef_controller
	{
		private J_BUF_MODE m_passModeSetByLastStartPass;

		private jpeg_compress_struct m_cinfo;

		private int m_iMCU_row_num;

		private int m_mcu_ctr;

		private int m_MCU_vert_offset;

		private int m_MCU_rows_per_iMCU_row;

		private JBLOCK[][] m_MCU_buffer;

		private jvirt_array<JBLOCK>[] m_whole_image;

		public my_c_coef_controller(jpeg_compress_struct cinfo, bool need_full_buffer)
		{
			m_MCU_buffer = new JBLOCK[10][];
			m_whole_image = new jvirt_array<JBLOCK>[10];

			m_cinfo = cinfo;
			if (need_full_buffer)
			{
				for (int i = 0; i < cinfo.m_num_components; i++)
				{
					m_whole_image[i] = jpeg_common_struct.CreateBlocksArray(JpegUtils.jround_up(cinfo.Component_info[i].Width_in_blocks, cinfo.Component_info[i].H_samp_factor), JpegUtils.jround_up(cinfo.Component_info[i].height_in_blocks, cinfo.Component_info[i].V_samp_factor));
					m_whole_image[i].ErrorProcessor = cinfo;
				}
				return;
			}
			JBLOCK[] array = new JBLOCK[10];
			for (int j = 0; j < 10; j++)
			{
				array[j] = new JBLOCK();
			}
			for (int k = 0; k < 10; k++)
			{
				m_MCU_buffer[k] = new JBLOCK[10 - k];
				for (int l = k; l < 10; l++)
				{
					m_MCU_buffer[k][l - k] = array[l];
				}
			}
			m_whole_image[0] = null;
		}

		public virtual void start_pass(J_BUF_MODE pass_mode)
		{
			m_iMCU_row_num = 0;
			start_iMCU_row();
			switch (pass_mode)
			{
			case J_BUF_MODE.JBUF_PASS_THRU:
				if (m_whole_image[0] != null)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_BUFFER_MODE);
				}
				break;
			case J_BUF_MODE.JBUF_SAVE_AND_PASS:
				if (m_whole_image[0] == null)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_BUFFER_MODE);
				}
				break;
			case J_BUF_MODE.JBUF_CRANK_DEST:
				if (m_whole_image[0] == null)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_BUFFER_MODE);
				}
				break;
			default:
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_BUFFER_MODE);
				break;
			}
			m_passModeSetByLastStartPass = pass_mode;
		}

		public virtual bool compress_data(byte[][][] input_buf)
		{
			switch (m_passModeSetByLastStartPass)
			{
			case J_BUF_MODE.JBUF_PASS_THRU:
				return compressDataImpl(input_buf);
			case J_BUF_MODE.JBUF_SAVE_AND_PASS:
				return compressFirstPass(input_buf);
			case J_BUF_MODE.JBUF_CRANK_DEST:
				return compressOutput();
			default:
				return false;
			}
		}

		private bool compressDataImpl(byte[][][] input_buf)
		{
			int num = m_cinfo.m_MCUs_per_row - 1;
			int num2 = m_cinfo.m_total_iMCU_rows - 1;
			for (int i = m_MCU_vert_offset; i < m_MCU_rows_per_iMCU_row; i++)
			{
				for (int j = m_mcu_ctr; j <= num; j++)
				{
					int num3 = 0;
					for (int k = 0; k < m_cinfo.m_comps_in_scan; k++)
					{
						jpeg_component_info jpeg_component_info = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[k]];
						int num4 = ((j < num) ? jpeg_component_info.MCU_width : jpeg_component_info.last_col_width);
						int start_col = j * jpeg_component_info.MCU_sample_width;
						int num5 = i * 8;
						for (int l = 0; l < jpeg_component_info.MCU_height; l++)
						{
							if (m_iMCU_row_num < num2 || i + l < jpeg_component_info.last_row_height)
							{
								m_cinfo.m_fdct.forward_DCT(jpeg_component_info.Quant_tbl_no, input_buf[jpeg_component_info.Component_index], m_MCU_buffer[num3], num5, start_col, num4);
								if (num4 < jpeg_component_info.MCU_width)
								{
									for (int m = 0; m < jpeg_component_info.MCU_width - num4; m++)
									{
										Array.Clear(m_MCU_buffer[num3 + num4][m].data, 0, m_MCU_buffer[num3 + num4][m].data.Length);
									}
									for (int n = num4; n < jpeg_component_info.MCU_width; n++)
									{
										m_MCU_buffer[num3 + n][0][0] = m_MCU_buffer[num3 + n - 1][0][0];
									}
								}
							}
							else
							{
								for (int num6 = 0; num6 < jpeg_component_info.MCU_width; num6++)
								{
									Array.Clear(m_MCU_buffer[num3][num6].data, 0, m_MCU_buffer[num3][num6].data.Length);
								}
								for (int num7 = 0; num7 < jpeg_component_info.MCU_width; num7++)
								{
									m_MCU_buffer[num3 + num7][0][0] = m_MCU_buffer[num3 - 1][0][0];
								}
							}
							num3 += jpeg_component_info.MCU_width;
							num5 += 8;
						}
					}
					if (!m_cinfo.m_entropy.encode_mcu(m_MCU_buffer))
					{
						m_MCU_vert_offset = i;
						m_mcu_ctr = j;
						return false;
					}
				}
				m_mcu_ctr = 0;
			}
			m_iMCU_row_num++;
			start_iMCU_row();
			return true;
		}

		private bool compressFirstPass(byte[][][] input_buf)
		{
			int num = m_cinfo.m_total_iMCU_rows - 1;
			for (int i = 0; i < m_cinfo.m_num_components; i++)
			{
				jpeg_component_info jpeg_component_info = m_cinfo.Component_info[i];
				JBLOCK[][] array = m_whole_image[i].Access(m_iMCU_row_num * jpeg_component_info.V_samp_factor, jpeg_component_info.V_samp_factor);
				int num2;
				if (m_iMCU_row_num < num)
				{
					num2 = jpeg_component_info.V_samp_factor;
				}
				else
				{
					num2 = jpeg_component_info.height_in_blocks % jpeg_component_info.V_samp_factor;
					if (num2 == 0)
					{
						num2 = jpeg_component_info.V_samp_factor;
					}
				}
				int width_in_blocks = jpeg_component_info.Width_in_blocks;
				int h_samp_factor = jpeg_component_info.H_samp_factor;
				int num3 = width_in_blocks % h_samp_factor;
				if (num3 > 0)
				{
					num3 = h_samp_factor - num3;
				}
				for (int j = 0; j < num2; j++)
				{
					m_cinfo.m_fdct.forward_DCT(jpeg_component_info.Quant_tbl_no, input_buf[i], array[j], j * 8, 0, width_in_blocks);
					if (num3 > 0)
					{
						Array.Clear(array[j][width_in_blocks].data, 0, array[j][width_in_blocks].data.Length);
						short value = array[j][width_in_blocks - 1][0];
						for (int k = 0; k < num3; k++)
						{
							array[j][width_in_blocks + k][0] = value;
						}
					}
				}
				if (m_iMCU_row_num != num)
				{
					continue;
				}
				width_in_blocks += num3;
				int num4 = width_in_blocks / h_samp_factor;
				for (int l = num2; l < jpeg_component_info.V_samp_factor; l++)
				{
					for (int m = 0; m < width_in_blocks; m++)
					{
						Array.Clear(array[l][m].data, 0, array[l][m].data.Length);
					}
					int num5 = 0;
					int num6 = 0;
					for (int n = 0; n < num4; n++)
					{
						short value2 = array[l - 1][num6 + h_samp_factor - 1][0];
						for (int num7 = 0; num7 < h_samp_factor; num7++)
						{
							array[l][num5 + num7][0] = value2;
						}
						num5 += h_samp_factor;
						num6 += h_samp_factor;
					}
				}
			}
			return compressOutput();
		}

		private bool compressOutput()
		{
			JBLOCK[][][] array = new JBLOCK[4][][];
			for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
			{
				jpeg_component_info jpeg_component_info = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[i]];
				array[i] = m_whole_image[jpeg_component_info.Component_index].Access(m_iMCU_row_num * jpeg_component_info.V_samp_factor, jpeg_component_info.V_samp_factor);
			}
			for (int j = m_MCU_vert_offset; j < m_MCU_rows_per_iMCU_row; j++)
			{
				for (int k = m_mcu_ctr; k < m_cinfo.m_MCUs_per_row; k++)
				{
					int num = 0;
					for (int l = 0; l < m_cinfo.m_comps_in_scan; l++)
					{
						jpeg_component_info jpeg_component_info2 = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[l]];
						int num2 = k * jpeg_component_info2.MCU_width;
						for (int m = 0; m < jpeg_component_info2.MCU_height; m++)
						{
							for (int n = 0; n < jpeg_component_info2.MCU_width; n++)
							{
								int num3 = array[l][m + j].Length;
								int num4 = num2 + n;
								m_MCU_buffer[num] = new JBLOCK[num3 - num4];
								for (int num5 = num4; num5 < num3; num5++)
								{
									m_MCU_buffer[num][num5 - num4] = array[l][m + j][num5];
								}
								num++;
							}
						}
					}
					if (!m_cinfo.m_entropy.encode_mcu(m_MCU_buffer))
					{
						m_MCU_vert_offset = j;
						m_mcu_ctr = k;
						return false;
					}
				}
				m_mcu_ctr = 0;
			}
			m_iMCU_row_num++;
			start_iMCU_row();
			return true;
		}

		private void start_iMCU_row()
		{
			if (m_cinfo.m_comps_in_scan > 1)
			{
				m_MCU_rows_per_iMCU_row = 1;
			}
			else if (m_iMCU_row_num < m_cinfo.m_total_iMCU_rows - 1)
			{
				m_MCU_rows_per_iMCU_row = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[0]].V_samp_factor;
			}
			else
			{
				m_MCU_rows_per_iMCU_row = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[0]].last_row_height;
			}
			m_mcu_ctr = 0;
			m_MCU_vert_offset = 0;
		}




	}
}
