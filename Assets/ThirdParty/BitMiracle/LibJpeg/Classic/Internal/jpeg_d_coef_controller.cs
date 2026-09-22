using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class jpeg_d_coef_controller
	{
		private enum DecompressorType
		{
			Ordinary,
			Smooth,
			OnePass
		}

		private const int SAVED_COEFS = 6;

		private const int Q01_POS = 1;

		private const int Q10_POS = 8;

		private const int Q20_POS = 16;

		private const int Q11_POS = 9;

		private const int Q02_POS = 2;

		private jpeg_decompress_struct m_cinfo;

		private bool m_useDummyConsumeData;

		private DecompressorType m_decompressor;

		private int m_MCU_ctr;

		private int m_MCU_vert_offset;

		private int m_MCU_rows_per_iMCU_row;

		private JBLOCK[] m_MCU_buffer;

		private jvirt_array<JBLOCK>[] m_whole_image;

		private jvirt_array<JBLOCK>[] m_coef_arrays;

		private int[] m_coef_bits_latch;

		private int m_coef_bits_savedOffset;

		public jpeg_d_coef_controller(jpeg_decompress_struct cinfo, bool need_full_buffer)
		{
			m_MCU_buffer = new JBLOCK[10];
			m_whole_image = new jvirt_array<JBLOCK>[10];

			m_cinfo = cinfo;
			if (need_full_buffer)
			{
				for (int i = 0; i < cinfo.m_num_components; i++)
				{
					m_whole_image[i] = jpeg_common_struct.CreateBlocksArray(JpegUtils.jround_up(cinfo.Comp_info[i].Width_in_blocks, cinfo.Comp_info[i].H_samp_factor), JpegUtils.jround_up(cinfo.Comp_info[i].height_in_blocks, cinfo.Comp_info[i].V_samp_factor));
					m_whole_image[i].ErrorProcessor = cinfo;
				}
				m_useDummyConsumeData = false;
				m_decompressor = DecompressorType.Ordinary;
				m_coef_arrays = m_whole_image;
				return;
			}
			JBLOCK[] array = new JBLOCK[10];
			for (int j = 0; j < 10; j++)
			{
				array[j] = new JBLOCK();
				for (int k = 0; k < array[j].data.Length; k++)
				{
					array[j].data[k] = -12851;
				}
				m_MCU_buffer[j] = array[j];
			}
			m_useDummyConsumeData = true;
			m_decompressor = DecompressorType.OnePass;
			m_coef_arrays = null;
		}

		public void start_input_pass()
		{
			m_cinfo.m_input_iMCU_row = 0;
			start_iMCU_row();
		}

		public ReadResult consume_data()
		{
			if (m_useDummyConsumeData)
			{
				return ReadResult.JPEG_SUSPENDED;
			}
			JBLOCK[][][] array = new JBLOCK[4][][];
			for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
			{
				jpeg_component_info jpeg_component_info = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[i]];
				array[i] = m_whole_image[jpeg_component_info.Component_index].Access(m_cinfo.m_input_iMCU_row * jpeg_component_info.V_samp_factor, jpeg_component_info.V_samp_factor);
			}
			for (int j = m_MCU_vert_offset; j < m_MCU_rows_per_iMCU_row; j++)
			{
				for (int k = m_MCU_ctr; k < m_cinfo.m_MCUs_per_row; k++)
				{
					int num = 0;
					for (int l = 0; l < m_cinfo.m_comps_in_scan; l++)
					{
						jpeg_component_info jpeg_component_info2 = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[l]];
						int num2 = k * jpeg_component_info2.MCU_width;
						for (int m = 0; m < jpeg_component_info2.MCU_height; m++)
						{
							for (int n = 0; n < jpeg_component_info2.MCU_width; n++)
							{
								m_MCU_buffer[num] = array[l][m + j][num2 + n];
								num++;
							}
						}
					}
					if (!m_cinfo.m_entropy.decode_mcu(m_MCU_buffer))
					{
						m_MCU_vert_offset = j;
						m_MCU_ctr = k;
						return ReadResult.JPEG_SUSPENDED;
					}
				}
				m_MCU_ctr = 0;
			}
			m_cinfo.m_input_iMCU_row++;
			if (m_cinfo.m_input_iMCU_row < m_cinfo.m_total_iMCU_rows)
			{
				start_iMCU_row();
				return ReadResult.JPEG_ROW_COMPLETED;
			}
			m_cinfo.m_inputctl.finish_input_pass();
			return ReadResult.JPEG_SCAN_COMPLETED;
		}

		public void start_output_pass()
		{
			if (m_coef_arrays != null)
			{
				if (m_cinfo.m_do_block_smoothing && smoothing_ok())
				{
					m_decompressor = DecompressorType.Smooth;
				}
				else
				{
					m_decompressor = DecompressorType.Ordinary;
				}
			}
			m_cinfo.m_output_iMCU_row = 0;
		}

		public ReadResult decompress_data(ComponentBuffer[] output_buf)
		{
			switch (m_decompressor)
			{
			case DecompressorType.Ordinary:
				return decompress_data_ordinary(output_buf);
			case DecompressorType.Smooth:
				return decompress_smooth_data(output_buf);
			case DecompressorType.OnePass:
				return decompress_onepass(output_buf);
			default:
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
				return ReadResult.JPEG_SUSPENDED;
			}
		}

		public jvirt_array<JBLOCK>[] GetCoefArrays()
		{
			return m_coef_arrays;
		}

		private ReadResult decompress_onepass(ComponentBuffer[] output_buf)
		{
			int num = m_cinfo.m_MCUs_per_row - 1;
			int num2 = m_cinfo.m_total_iMCU_rows - 1;
			for (int i = m_MCU_vert_offset; i < m_MCU_rows_per_iMCU_row; i++)
			{
				for (int j = m_MCU_ctr; j <= num; j++)
				{
					for (int k = 0; k < m_cinfo.m_blocks_in_MCU; k++)
					{
						Array.Clear(m_MCU_buffer[k].data, 0, m_MCU_buffer[k].data.Length);
					}
					if (!m_cinfo.m_entropy.decode_mcu(m_MCU_buffer))
					{
						m_MCU_vert_offset = i;
						m_MCU_ctr = j;
						return ReadResult.JPEG_SUSPENDED;
					}
					int num3 = 0;
					for (int l = 0; l < m_cinfo.m_comps_in_scan; l++)
					{
						jpeg_component_info jpeg_component_info = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[l]];
						if (!jpeg_component_info.component_needed)
						{
							num3 += jpeg_component_info.MCU_blocks;
							continue;
						}
						int num4 = ((j < num) ? jpeg_component_info.MCU_width : jpeg_component_info.last_col_width);
						int num5 = i * jpeg_component_info.DCT_scaled_size;
						int num6 = j * jpeg_component_info.MCU_sample_width;
						for (int m = 0; m < jpeg_component_info.MCU_height; m++)
						{
							if (m_cinfo.m_input_iMCU_row < num2 || i + m < jpeg_component_info.last_row_height)
							{
								int num7 = num6;
								for (int n = 0; n < num4; n++)
								{
									m_cinfo.m_idct.inverse(jpeg_component_info.Component_index, m_MCU_buffer[num3 + n].data, output_buf[jpeg_component_info.Component_index], num5, num7);
									num7 += jpeg_component_info.DCT_scaled_size;
								}
							}
							num3 += jpeg_component_info.MCU_width;
							num5 += jpeg_component_info.DCT_scaled_size;
						}
					}
				}
				m_MCU_ctr = 0;
			}
			m_cinfo.m_output_iMCU_row++;
			m_cinfo.m_input_iMCU_row++;
			if (m_cinfo.m_input_iMCU_row < m_cinfo.m_total_iMCU_rows)
			{
				start_iMCU_row();
				return ReadResult.JPEG_ROW_COMPLETED;
			}
			m_cinfo.m_inputctl.finish_input_pass();
			return ReadResult.JPEG_SCAN_COMPLETED;
		}

		private ReadResult decompress_data_ordinary(ComponentBuffer[] output_buf)
		{
			while (m_cinfo.m_input_scan_number < m_cinfo.m_output_scan_number || (m_cinfo.m_input_scan_number == m_cinfo.m_output_scan_number && m_cinfo.m_input_iMCU_row <= m_cinfo.m_output_iMCU_row))
			{
				if (m_cinfo.m_inputctl.consume_input() == ReadResult.JPEG_SUSPENDED)
				{
					return ReadResult.JPEG_SUSPENDED;
				}
			}
			int num = m_cinfo.m_total_iMCU_rows - 1;
			for (int i = 0; i < m_cinfo.m_num_components; i++)
			{
				jpeg_component_info jpeg_component_info = m_cinfo.Comp_info[i];
				if (!jpeg_component_info.component_needed)
				{
					continue;
				}
				JBLOCK[][] array = m_whole_image[i].Access(m_cinfo.m_output_iMCU_row * jpeg_component_info.V_samp_factor, jpeg_component_info.V_samp_factor);
				int num2;
				if (m_cinfo.m_output_iMCU_row < num)
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
				int num3 = 0;
				for (int j = 0; j < num2; j++)
				{
					int num4 = 0;
					for (int k = 0; k < jpeg_component_info.Width_in_blocks; k++)
					{
						m_cinfo.m_idct.inverse(jpeg_component_info.Component_index, array[j][k].data, output_buf[i], num3, num4);
						num4 += jpeg_component_info.DCT_scaled_size;
					}
					num3 += jpeg_component_info.DCT_scaled_size;
				}
			}
			m_cinfo.m_output_iMCU_row++;
			if (m_cinfo.m_output_iMCU_row < m_cinfo.m_total_iMCU_rows)
			{
				return ReadResult.JPEG_ROW_COMPLETED;
			}
			return ReadResult.JPEG_SCAN_COMPLETED;
		}

		private ReadResult decompress_smooth_data(ComponentBuffer[] output_buf)
		{
			while (m_cinfo.m_input_scan_number <= m_cinfo.m_output_scan_number && !m_cinfo.m_inputctl.EOIReached())
			{
				if (m_cinfo.m_input_scan_number == m_cinfo.m_output_scan_number)
				{
					int num = ((m_cinfo.m_Ss == 0) ? 1 : 0);
					if (m_cinfo.m_input_iMCU_row > m_cinfo.m_output_iMCU_row + num)
					{
						break;
					}
				}
				if (m_cinfo.m_inputctl.consume_input() == ReadResult.JPEG_SUSPENDED)
				{
					return ReadResult.JPEG_SUSPENDED;
				}
			}
			int num2 = m_cinfo.m_total_iMCU_rows - 1;
			for (int i = 0; i < m_cinfo.m_num_components; i++)
			{
				jpeg_component_info jpeg_component_info = m_cinfo.Comp_info[i];
				if (!jpeg_component_info.component_needed)
				{
					continue;
				}
				int num3;
				bool flag;
				int num4;
				if (m_cinfo.m_output_iMCU_row < num2)
				{
					num3 = jpeg_component_info.V_samp_factor;
					num4 = num3 * 2;
					flag = false;
				}
				else
				{
					num3 = jpeg_component_info.height_in_blocks % jpeg_component_info.V_samp_factor;
					if (num3 == 0)
					{
						num3 = jpeg_component_info.V_samp_factor;
					}
					num4 = num3;
					flag = true;
				}
				JBLOCK[][] array = null;
				int num5 = 0;
				bool flag2;
				if (m_cinfo.m_output_iMCU_row > 0)
				{
					num4 += jpeg_component_info.V_samp_factor;
					array = m_whole_image[i].Access((m_cinfo.m_output_iMCU_row - 1) * jpeg_component_info.V_samp_factor, num4);
					num5 = jpeg_component_info.V_samp_factor;
					flag2 = false;
				}
				else
				{
					array = m_whole_image[i].Access(0, num4);
					flag2 = true;
				}
				int num6 = i * 6;
				int num7 = jpeg_component_info.quant_table.quantval[0];
				int num8 = jpeg_component_info.quant_table.quantval[1];
				int num9 = jpeg_component_info.quant_table.quantval[8];
				int num10 = jpeg_component_info.quant_table.quantval[16];
				int num11 = jpeg_component_info.quant_table.quantval[9];
				int num12 = jpeg_component_info.quant_table.quantval[2];
				int num13 = i;
				for (int j = 0; j < num3; j++)
				{
					int num14 = num5 + j;
					int num15 = 0;
					num15 = ((!flag2 || j != 0) ? (num14 - 1) : num14);
					int num16 = 0;
					num16 = ((!flag || j != num3 - 1) ? (num14 + 1) : num14);
					int num17 = array[num15][0][0];
					int num18 = num17;
					int num19 = num17;
					int num20 = array[num14][0][0];
					int num21 = num20;
					int num22 = num20;
					int num23 = array[num16][0][0];
					int num24 = num23;
					int num25 = num23;
					int num26 = 0;
					int num27 = jpeg_component_info.Width_in_blocks - 1;
					for (int k = 0; k <= num27; k++)
					{
						JBLOCK jBLOCK = new JBLOCK();
						Buffer.BlockCopy(array[num14][0].data, 0, jBLOCK.data, 0, jBLOCK.data.Length * 2);
						if (k < num27)
						{
							num19 = array[num15][1][0];
							num22 = array[num14][1][0];
							num25 = array[num16][1][0];
						}
						int num28 = m_coef_bits_latch[m_coef_bits_savedOffset + num6 + 1];
						if (num28 != 0 && jBLOCK[1] == 0)
						{
							int num29 = 36 * num7 * (num20 - num22);
							int num30;
							if (num29 >= 0)
							{
								num30 = ((num8 << 7) + num29) / (num8 << 8);
								if (num28 > 0 && num30 >= 1 << num28)
								{
									num30 = (1 << num28) - 1;
								}
							}
							else
							{
								num30 = ((num8 << 7) - num29) / (num8 << 8);
								if (num28 > 0 && num30 >= 1 << num28)
								{
									num30 = (1 << num28) - 1;
								}
								num30 = -num30;
							}
							jBLOCK[1] = (short)num30;
						}
						num28 = m_coef_bits_latch[m_coef_bits_savedOffset + num6 + 2];
						if (num28 != 0 && jBLOCK[8] == 0)
						{
							int num31 = 36 * num7 * (num18 - num24);
							int num32;
							if (num31 >= 0)
							{
								num32 = ((num9 << 7) + num31) / (num9 << 8);
								if (num28 > 0 && num32 >= 1 << num28)
								{
									num32 = (1 << num28) - 1;
								}
							}
							else
							{
								num32 = ((num9 << 7) - num31) / (num9 << 8);
								if (num28 > 0 && num32 >= 1 << num28)
								{
									num32 = (1 << num28) - 1;
								}
								num32 = -num32;
							}
							jBLOCK[8] = (short)num32;
						}
						num28 = m_coef_bits_latch[m_coef_bits_savedOffset + num6 + 3];
						if (num28 != 0 && jBLOCK[16] == 0)
						{
							int num33 = 9 * num7 * (num18 + num24 - 2 * num21);
							int num34;
							if (num33 >= 0)
							{
								num34 = ((num10 << 7) + num33) / (num10 << 8);
								if (num28 > 0 && num34 >= 1 << num28)
								{
									num34 = (1 << num28) - 1;
								}
							}
							else
							{
								num34 = ((num10 << 7) - num33) / (num10 << 8);
								if (num28 > 0 && num34 >= 1 << num28)
								{
									num34 = (1 << num28) - 1;
								}
								num34 = -num34;
							}
							jBLOCK[16] = (short)num34;
						}
						num28 = m_coef_bits_latch[m_coef_bits_savedOffset + num6 + 4];
						if (num28 != 0 && jBLOCK[9] == 0)
						{
							int num35 = 5 * num7 * (num17 - num19 - num23 + num25);
							int num36;
							if (num35 >= 0)
							{
								num36 = ((num11 << 7) + num35) / (num11 << 8);
								if (num28 > 0 && num36 >= 1 << num28)
								{
									num36 = (1 << num28) - 1;
								}
							}
							else
							{
								num36 = ((num11 << 7) - num35) / (num11 << 8);
								if (num28 > 0 && num36 >= 1 << num28)
								{
									num36 = (1 << num28) - 1;
								}
								num36 = -num36;
							}
							jBLOCK[9] = (short)num36;
						}
						num28 = m_coef_bits_latch[m_coef_bits_savedOffset + num6 + 5];
						if (num28 != 0 && jBLOCK[2] == 0)
						{
							int num37 = 9 * num7 * (num20 + num22 - 2 * num21);
							int num38;
							if (num37 >= 0)
							{
								num38 = ((num12 << 7) + num37) / (num12 << 8);
								if (num28 > 0 && num38 >= 1 << num28)
								{
									num38 = (1 << num28) - 1;
								}
							}
							else
							{
								num38 = ((num12 << 7) - num37) / (num12 << 8);
								if (num28 > 0 && num38 >= 1 << num28)
								{
									num38 = (1 << num28) - 1;
								}
								num38 = -num38;
							}
							jBLOCK[2] = (short)num38;
						}
						m_cinfo.m_idct.inverse(jpeg_component_info.Component_index, jBLOCK.data, output_buf[num13], 0, num26);
						num17 = num18;
						num18 = num19;
						num20 = num21;
						num21 = num22;
						num23 = num24;
						num24 = num25;
						num14++;
						num15++;
						num16++;
						num26 += jpeg_component_info.DCT_scaled_size;
					}
					num13 += jpeg_component_info.DCT_scaled_size;
				}
			}
			m_cinfo.m_output_iMCU_row++;
			if (m_cinfo.m_output_iMCU_row < m_cinfo.m_total_iMCU_rows)
			{
				return ReadResult.JPEG_ROW_COMPLETED;
			}
			return ReadResult.JPEG_SCAN_COMPLETED;
		}

		private bool smoothing_ok()
		{
			if (!m_cinfo.m_progressive_mode || m_cinfo.m_coef_bits == null)
			{
				return false;
			}
			if (m_coef_bits_latch == null)
			{
				m_coef_bits_latch = new int[m_cinfo.m_num_components * 6];
				m_coef_bits_savedOffset = 0;
			}
			bool result = false;
			for (int i = 0; i < m_cinfo.m_num_components; i++)
			{
				JQUANT_TBL quant_table = m_cinfo.Comp_info[i].quant_table;
				if (quant_table == null)
				{
					return false;
				}
				if (quant_table.quantval[0] == 0 || quant_table.quantval[1] == 0 || quant_table.quantval[8] == 0 || quant_table.quantval[16] == 0 || quant_table.quantval[9] == 0 || quant_table.quantval[2] == 0)
				{
					return false;
				}
				if (m_cinfo.m_coef_bits[i][0] < 0)
				{
					return false;
				}
				for (int j = 1; j <= 5; j++)
				{
					m_coef_bits_latch[m_coef_bits_savedOffset + j] = m_cinfo.m_coef_bits[i][j];
					if (m_cinfo.m_coef_bits[i][j] != 0)
					{
						result = true;
					}
				}
				m_coef_bits_savedOffset += 6;
			}
			return result;
		}

		private void start_iMCU_row()
		{
			if (m_cinfo.m_comps_in_scan > 1)
			{
				m_MCU_rows_per_iMCU_row = 1;
			}
			else
			{
				jpeg_component_info jpeg_component_info = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[0]];
				if (m_cinfo.m_input_iMCU_row < m_cinfo.m_total_iMCU_rows - 1)
				{
					m_MCU_rows_per_iMCU_row = jpeg_component_info.V_samp_factor;
				}
				else
				{
					m_MCU_rows_per_iMCU_row = jpeg_component_info.last_row_height;
				}
			}
			m_MCU_ctr = 0;
			m_MCU_vert_offset = 0;
		}




	}
}
