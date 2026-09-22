using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class jpeg_downsampler
	{
		private enum downSampleMethod
		{
			fullsize_smooth_downsampler,
			fullsize_downsampler,
			h2v1_downsampler,
			h2v2_smooth_downsampler,
			h2v2_downsampler,
			int_downsampler
		}

		private downSampleMethod[] m_downSamplers;

		private jpeg_compress_struct m_cinfo;

		private bool m_need_context_rows;

		public jpeg_downsampler(jpeg_compress_struct cinfo)
		{
			m_downSamplers = new downSampleMethod[10];

			m_cinfo = cinfo;
			m_need_context_rows = false;
			if (cinfo.m_CCIR601_sampling)
			{
				cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CCIR601_NOTIMPL);
			}
			bool flag = true;
			for (int i = 0; i < cinfo.m_num_components; i++)
			{
				jpeg_component_info jpeg_component_info = cinfo.Component_info[i];
				if (jpeg_component_info.H_samp_factor == cinfo.m_max_h_samp_factor && jpeg_component_info.V_samp_factor == cinfo.m_max_v_samp_factor)
				{
					if (cinfo.m_smoothing_factor != 0)
					{
						m_downSamplers[i] = downSampleMethod.fullsize_smooth_downsampler;
						m_need_context_rows = true;
					}
					else
					{
						m_downSamplers[i] = downSampleMethod.fullsize_downsampler;
					}
				}
				else if (jpeg_component_info.H_samp_factor * 2 == cinfo.m_max_h_samp_factor && jpeg_component_info.V_samp_factor == cinfo.m_max_v_samp_factor)
				{
					flag = false;
					m_downSamplers[i] = downSampleMethod.h2v1_downsampler;
				}
				else if (jpeg_component_info.H_samp_factor * 2 == cinfo.m_max_h_samp_factor && jpeg_component_info.V_samp_factor * 2 == cinfo.m_max_v_samp_factor)
				{
					if (cinfo.m_smoothing_factor != 0)
					{
						m_downSamplers[i] = downSampleMethod.h2v2_smooth_downsampler;
						m_need_context_rows = true;
					}
					else
					{
						m_downSamplers[i] = downSampleMethod.h2v2_downsampler;
					}
				}
				else if (cinfo.m_max_h_samp_factor % jpeg_component_info.H_samp_factor == 0 && cinfo.m_max_v_samp_factor % jpeg_component_info.V_samp_factor == 0)
				{
					flag = false;
					m_downSamplers[i] = downSampleMethod.int_downsampler;
				}
				else
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_FRACT_SAMPLE_NOTIMPL);
				}
			}
			if (cinfo.m_smoothing_factor != 0 && !flag)
			{
				cinfo.TRACEMS(0, J_MESSAGE_CODE.JTRC_SMOOTH_NOTIMPL);
			}
		}

		public void downsample(byte[][][] input_buf, int in_row_index, byte[][][] output_buf, int out_row_group_index)
		{
			for (int i = 0; i < m_cinfo.m_num_components; i++)
			{
				int startOutRow = out_row_group_index * m_cinfo.Component_info[i].V_samp_factor;
				switch (m_downSamplers[i])
				{
				case downSampleMethod.fullsize_smooth_downsampler:
					fullsize_smooth_downsample(i, input_buf[i], in_row_index, output_buf[i], startOutRow);
					break;
				case downSampleMethod.fullsize_downsampler:
					fullsize_downsample(i, input_buf[i], in_row_index, output_buf[i], startOutRow);
					break;
				case downSampleMethod.h2v1_downsampler:
					h2v1_downsample(i, input_buf[i], in_row_index, output_buf[i], startOutRow);
					break;
				case downSampleMethod.h2v2_smooth_downsampler:
					h2v2_smooth_downsample(i, input_buf[i], in_row_index, output_buf[i], startOutRow);
					break;
				case downSampleMethod.h2v2_downsampler:
					h2v2_downsample(i, input_buf[i], in_row_index, output_buf[i], startOutRow);
					break;
				case downSampleMethod.int_downsampler:
					int_downsample(i, input_buf[i], in_row_index, output_buf[i], startOutRow);
					break;
				}
			}
		}

		public bool NeedContextRows()
		{
			return m_need_context_rows;
		}

		private void int_downsample(int componentIndex, byte[][] input_data, int startInputRow, byte[][] output_data, int startOutRow)
		{
			int num = m_cinfo.Component_info[componentIndex].Width_in_blocks * 8;
			int num2 = m_cinfo.m_max_h_samp_factor / m_cinfo.Component_info[componentIndex].H_samp_factor;
			expand_right_edge(input_data, startInputRow, m_cinfo.m_max_v_samp_factor, m_cinfo.m_image_width, num * num2);
			int num3 = m_cinfo.m_max_v_samp_factor / m_cinfo.Component_info[componentIndex].V_samp_factor;
			int num4 = num2 * num3;
			int num5 = num4 / 2;
			int num6 = 0;
			for (int i = 0; i < m_cinfo.Component_info[componentIndex].V_samp_factor; i++)
			{
				int num7 = 0;
				int num8 = 0;
				while (num7 < num)
				{
					int num9 = 0;
					for (int j = 0; j < num3; j++)
					{
						for (int k = 0; k < num2; k++)
						{
							num9 += input_data[startInputRow + num6 + j][num8 + k];
						}
					}
					output_data[startOutRow + i][num7] = (byte)((num9 + num5) / num4);
					num7++;
					num8 += num2;
				}
				num6 += num3;
			}
		}

		private void fullsize_downsample(int componentIndex, byte[][] input_data, int startInputRow, byte[][] output_data, int startOutRow)
		{
			JpegUtils.jcopy_sample_rows(input_data, startInputRow, output_data, startOutRow, m_cinfo.m_max_v_samp_factor, m_cinfo.m_image_width);
			expand_right_edge(output_data, startOutRow, m_cinfo.m_max_v_samp_factor, m_cinfo.m_image_width, m_cinfo.Component_info[componentIndex].Width_in_blocks * 8);
		}

		private void h2v1_downsample(int componentIndex, byte[][] input_data, int startInputRow, byte[][] output_data, int startOutRow)
		{
			int num = m_cinfo.Component_info[componentIndex].Width_in_blocks * 8;
			expand_right_edge(input_data, startInputRow, m_cinfo.m_max_v_samp_factor, m_cinfo.m_image_width, num * 2);
			for (int i = 0; i < m_cinfo.Component_info[componentIndex].V_samp_factor; i++)
			{
				int num2 = 0;
				int num3 = 0;
				for (int j = 0; j < num; j++)
				{
					output_data[startOutRow + i][j] = (byte)(input_data[startInputRow + i][num3] + input_data[startInputRow + i][num3 + 1] + num2 >> 1);
					num2 ^= 1;
					num3 += 2;
				}
			}
		}

		private void h2v2_downsample(int componentIndex, byte[][] input_data, int startInputRow, byte[][] output_data, int startOutRow)
		{
			int num = m_cinfo.Component_info[componentIndex].Width_in_blocks * 8;
			expand_right_edge(input_data, startInputRow, m_cinfo.m_max_v_samp_factor, m_cinfo.m_image_width, num * 2);
			int num2 = 0;
			for (int i = 0; i < m_cinfo.Component_info[componentIndex].V_samp_factor; i++)
			{
				int num3 = 1;
				int num4 = 0;
				for (int j = 0; j < num; j++)
				{
					output_data[startOutRow + i][j] = (byte)(input_data[startInputRow + num2][num4] + input_data[startInputRow + num2][num4 + 1] + input_data[startInputRow + num2 + 1][num4] + input_data[startInputRow + num2 + 1][num4 + 1] + num3 >> 2);
					num3 ^= 3;
					num4 += 2;
				}
				num2 += 2;
			}
		}

		private void h2v2_smooth_downsample(int componentIndex, byte[][] input_data, int startInputRow, byte[][] output_data, int startOutRow)
		{
			int num = m_cinfo.Component_info[componentIndex].Width_in_blocks * 8;
			expand_right_edge(input_data, startInputRow - 1, m_cinfo.m_max_v_samp_factor + 2, m_cinfo.m_image_width, num * 2);
			int num2 = 16384 - m_cinfo.m_smoothing_factor * 80;
			int num3 = m_cinfo.m_smoothing_factor * 16;
			int num4 = 0;
			for (int i = 0; i < m_cinfo.Component_info[componentIndex].V_samp_factor; i++)
			{
				int num5 = 0;
				int num6 = 0;
				int num7 = 0;
				int num8 = 0;
				int num9 = 0;
				int num10 = input_data[startInputRow + num4][num6] + input_data[startInputRow + num4][num6 + 1] + input_data[startInputRow + num4 + 1][num7] + input_data[startInputRow + num4 + 1][num7 + 1];
				int num11 = input_data[startInputRow + num4 - 1][num8] + input_data[startInputRow + num4 - 1][num8 + 1] + input_data[startInputRow + num4 + 2][num9] + input_data[startInputRow + num4 + 2][num9 + 1] + input_data[startInputRow + num4][num6] + input_data[startInputRow + num4][num6 + 2] + input_data[startInputRow + num4 + 1][num7] + input_data[startInputRow + num4 + 1][num7 + 2];
				num11 += num11;
				num11 += input_data[startInputRow + num4 - 1][num8] + input_data[startInputRow + num4 - 1][num8 + 2] + input_data[startInputRow + num4 + 2][num9] + input_data[startInputRow + num4 + 2][num9 + 2];
				num10 = num10 * num2 + num11 * num3;
				output_data[startOutRow + i][num5] = (byte)(num10 + 32768 >> 16);
				num5++;
				num6 += 2;
				num7 += 2;
				num8 += 2;
				num9 += 2;
				for (int num12 = num - 2; num12 > 0; num12--)
				{
					num10 = input_data[startInputRow + num4][num6] + input_data[startInputRow + num4][num6 + 1] + input_data[startInputRow + num4 + 1][num7] + input_data[startInputRow + num4 + 1][num7 + 1];
					num11 = input_data[startInputRow + num4 - 1][num8] + input_data[startInputRow + num4 - 1][num8 + 1] + input_data[startInputRow + num4 + 2][num9] + input_data[startInputRow + num4 + 2][num9 + 1] + input_data[startInputRow + num4][num6 - 1] + input_data[startInputRow + num4][num6 + 2] + input_data[startInputRow + num4 + 1][num7 - 1] + input_data[startInputRow + num4 + 1][num7 + 2];
					num11 += num11;
					num11 += input_data[startInputRow + num4 - 1][num8 - 1] + input_data[startInputRow + num4 - 1][num8 + 2] + input_data[startInputRow + num4 + 2][num9 - 1] + input_data[startInputRow + num4 + 2][num9 + 2];
					num10 = num10 * num2 + num11 * num3;
					output_data[startOutRow + i][num5] = (byte)(num10 + 32768 >> 16);
					num5++;
					num6 += 2;
					num7 += 2;
					num8 += 2;
					num9 += 2;
				}
				num10 = input_data[startInputRow + num4][num6] + input_data[startInputRow + num4][num6 + 1] + input_data[startInputRow + num4 + 1][num7] + input_data[startInputRow + num4 + 1][num7 + 1];
				num11 = input_data[startInputRow + num4 - 1][num8] + input_data[startInputRow + num4 - 1][num8 + 1] + input_data[startInputRow + num4 + 2][num9] + input_data[startInputRow + num4 + 2][num9 + 1] + input_data[startInputRow + num4][num6 - 1] + input_data[startInputRow + num4][num6 + 1] + input_data[startInputRow + num4 + 1][num7 - 1] + input_data[startInputRow + num4 + 1][num7 + 1];
				num11 += num11;
				num11 += input_data[startInputRow + num4 - 1][num8 - 1] + input_data[startInputRow + num4 - 1][num8 + 1] + input_data[startInputRow + num4 + 2][num9 - 1] + input_data[startInputRow + num4 + 2][num9 + 1];
				num10 = num10 * num2 + num11 * num3;
				output_data[startOutRow + i][num5] = (byte)(num10 + 32768 >> 16);
				num4 += 2;
			}
		}

		private void fullsize_smooth_downsample(int componentIndex, byte[][] input_data, int startInputRow, byte[][] output_data, int startOutRow)
		{
			int num = m_cinfo.Component_info[componentIndex].Width_in_blocks * 8;
			expand_right_edge(input_data, startInputRow - 1, m_cinfo.m_max_v_samp_factor + 2, m_cinfo.m_image_width, num);
			int num2 = 65536 - m_cinfo.m_smoothing_factor * 512;
			int num3 = m_cinfo.m_smoothing_factor * 64;
			for (int i = 0; i < m_cinfo.Component_info[componentIndex].V_samp_factor; i++)
			{
				int num4 = 0;
				int num5 = 0;
				int num6 = 0;
				int num7 = 0;
				int num8 = input_data[startInputRow + i - 1][num6] + input_data[startInputRow + i + 1][num7] + input_data[startInputRow + i][num5];
				num6++;
				num7++;
				int num9 = input_data[startInputRow + i][num5];
				num5++;
				int num10 = input_data[startInputRow + i - 1][num6] + input_data[startInputRow + i + 1][num7] + input_data[startInputRow + i][num5];
				int num11 = num8 + (num8 - num9) + num10;
				num9 = num9 * num2 + num11 * num3;
				output_data[startOutRow + i][num4] = (byte)(num9 + 32768 >> 16);
				num4++;
				int num12 = num8;
				num8 = num10;
				for (int num13 = num - 2; num13 > 0; num13--)
				{
					num9 = input_data[startInputRow + i][num5];
					num5++;
					num6++;
					num7++;
					num10 = input_data[startInputRow + i - 1][num6] + input_data[startInputRow + i + 1][num7] + input_data[startInputRow + i][num5];
					num11 = num12 + (num8 - num9) + num10;
					num9 = num9 * num2 + num11 * num3;
					output_data[startOutRow + i][num4] = (byte)(num9 + 32768 >> 16);
					num4++;
					num12 = num8;
					num8 = num10;
				}
				num9 = input_data[startInputRow + i][num5];
				num11 = num12 + (num8 - num9) + num8;
				num9 = num9 * num2 + num11 * num3;
				output_data[startOutRow + i][num4] = (byte)(num9 + 32768 >> 16);
			}
		}

		private static void expand_right_edge(byte[][] image_data, int startInputRow, int num_rows, int input_cols, int output_cols)
		{
			int num = output_cols - input_cols;
			if (num <= 0)
			{
				return;
			}
			for (int i = startInputRow; i < startInputRow + num_rows; i++)
			{
				byte b = image_data[i][input_cols - 1];
				for (int j = 0; j < num; j++)
				{
					image_data[i][input_cols + j] = b;
				}
			}
		}




	}
}
