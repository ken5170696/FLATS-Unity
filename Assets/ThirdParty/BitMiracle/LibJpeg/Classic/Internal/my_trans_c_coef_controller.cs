using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class my_trans_c_coef_controller : jpeg_c_coef_controller
	{
		private jpeg_compress_struct m_cinfo;

		private int m_iMCU_row_num;

		private int m_mcu_ctr;

		private int m_MCU_vert_offset;

		private int m_MCU_rows_per_iMCU_row;

		private jvirt_array<JBLOCK>[] m_whole_image;

		private JBLOCK[][] m_dummy_buffer;

		public my_trans_c_coef_controller(jpeg_compress_struct cinfo, jvirt_array<JBLOCK>[] coef_arrays)
		{
			m_dummy_buffer = new JBLOCK[10][];

			m_cinfo = cinfo;
			m_whole_image = coef_arrays;
			JBLOCK[] array = new JBLOCK[10];
			for (int i = 0; i < 10; i++)
			{
				array[i] = new JBLOCK();
			}
			for (int j = 0; j < 10; j++)
			{
				m_dummy_buffer[j] = new JBLOCK[10 - j];
				for (int k = j; k < 10; k++)
				{
					m_dummy_buffer[j][k - j] = array[k];
				}
			}
		}

		public virtual void start_pass(J_BUF_MODE pass_mode)
		{
			if (pass_mode != J_BUF_MODE.JBUF_CRANK_DEST)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_BUFFER_MODE);
			}
			m_iMCU_row_num = 0;
			start_iMCU_row();
		}

		public virtual bool compress_data(byte[][][] input_buf)
		{
			JBLOCK[][][] array = new JBLOCK[4][][];
			for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
			{
				jpeg_component_info jpeg_component_info = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[i]];
				array[i] = m_whole_image[jpeg_component_info.Component_index].Access(m_iMCU_row_num * jpeg_component_info.V_samp_factor, jpeg_component_info.V_samp_factor);
			}
			int num = m_cinfo.m_MCUs_per_row - 1;
			int num2 = m_cinfo.m_total_iMCU_rows - 1;
			JBLOCK[][] array2 = new JBLOCK[10][];
			for (int j = m_MCU_vert_offset; j < m_MCU_rows_per_iMCU_row; j++)
			{
				for (int k = m_mcu_ctr; k < m_cinfo.m_MCUs_per_row; k++)
				{
					int num3 = 0;
					for (int l = 0; l < m_cinfo.m_comps_in_scan; l++)
					{
						jpeg_component_info jpeg_component_info2 = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[l]];
						int num4 = k * jpeg_component_info2.MCU_width;
						int num5 = ((k < num) ? jpeg_component_info2.MCU_width : jpeg_component_info2.last_col_width);
						for (int m = 0; m < jpeg_component_info2.MCU_height; m++)
						{
							int num6 = 0;
							if (m_iMCU_row_num < num2 || m + j < jpeg_component_info2.last_row_height)
							{
								for (num6 = 0; num6 < num5; num6++)
								{
									int num7 = array[l][m + j].Length;
									int num8 = num4 + num6;
									array2[num3] = new JBLOCK[num7 - num8];
									for (int n = num8; n < num7; n++)
									{
										array2[num3][n - num8] = array[l][m + j][n];
									}
									num3++;
								}
							}
							else
							{
								num6 = 0;
							}
							for (; num6 < jpeg_component_info2.MCU_width; num6++)
							{
								array2[num3] = m_dummy_buffer[num3];
								array2[num3][0][0] = array2[num3 - 1][0][0];
								num3++;
							}
						}
					}
					if (!m_cinfo.m_entropy.encode_mcu(array2))
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
