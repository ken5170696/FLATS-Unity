using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class my_upsampler : jpeg_upsampler
	{
		private enum ComponentUpsampler
		{
			noop_upsampler,
			fullsize_upsampler,
			h2v1_fancy_upsampler,
			h2v1_upsampler,
			h2v2_fancy_upsampler,
			h2v2_upsampler,
			int_upsampler
		}

		private jpeg_decompress_struct m_cinfo;

		private ComponentBuffer[] m_color_buf;

		private int[] m_perComponentOffsets;

		private ComponentUpsampler[] m_upsampleMethods;

		private int m_currentComponent;

		private int m_upsampleRowOffset;

		private int m_next_row_out;

		private int m_rows_to_go;

		private int[] m_rowgroup_height;

		private byte[] m_h_expand;

		private byte[] m_v_expand;

		public my_upsampler(jpeg_decompress_struct cinfo)
		{
			m_color_buf = new ComponentBuffer[10];
			m_perComponentOffsets = new int[10];
			m_upsampleMethods = new ComponentUpsampler[10];
			m_rowgroup_height = new int[10];
			m_h_expand = new byte[10];
			m_v_expand = new byte[10];

			m_cinfo = cinfo;
			m_need_context_rows = false;
			if (cinfo.m_CCIR601_sampling)
			{
				cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CCIR601_NOTIMPL);
			}
			bool flag = cinfo.m_do_fancy_upsampling && cinfo.m_min_DCT_scaled_size > 1;
			for (int i = 0; i < cinfo.m_num_components; i++)
			{
				jpeg_component_info jpeg_component_info = cinfo.Comp_info[i];
				int num = jpeg_component_info.H_samp_factor * jpeg_component_info.DCT_scaled_size / cinfo.m_min_DCT_scaled_size;
				int num2 = jpeg_component_info.V_samp_factor * jpeg_component_info.DCT_scaled_size / cinfo.m_min_DCT_scaled_size;
				int max_h_samp_factor = cinfo.m_max_h_samp_factor;
				int max_v_samp_factor = cinfo.m_max_v_samp_factor;
				m_rowgroup_height[i] = num2;
				bool flag2 = true;
				if (!jpeg_component_info.component_needed)
				{
					m_upsampleMethods[i] = ComponentUpsampler.noop_upsampler;
					flag2 = false;
				}
				else if (num == max_h_samp_factor && num2 == max_v_samp_factor)
				{
					m_upsampleMethods[i] = ComponentUpsampler.fullsize_upsampler;
					flag2 = false;
				}
				else if (num * 2 == max_h_samp_factor && num2 == max_v_samp_factor)
				{
					if (flag && jpeg_component_info.downsampled_width > 2)
					{
						m_upsampleMethods[i] = ComponentUpsampler.h2v1_fancy_upsampler;
					}
					else
					{
						m_upsampleMethods[i] = ComponentUpsampler.h2v1_upsampler;
					}
				}
				else if (num * 2 == max_h_samp_factor && num2 * 2 == max_v_samp_factor)
				{
					if (flag && jpeg_component_info.downsampled_width > 2)
					{
						m_upsampleMethods[i] = ComponentUpsampler.h2v2_fancy_upsampler;
						m_need_context_rows = true;
					}
					else
					{
						m_upsampleMethods[i] = ComponentUpsampler.h2v2_upsampler;
					}
				}
				else if (max_h_samp_factor % num == 0 && max_v_samp_factor % num2 == 0)
				{
					m_upsampleMethods[i] = ComponentUpsampler.int_upsampler;
					m_h_expand[i] = (byte)(max_h_samp_factor / num);
					m_v_expand[i] = (byte)(max_v_samp_factor / num2);
				}
				else
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_FRACT_SAMPLE_NOTIMPL);
				}
				if (flag2)
				{
					ComponentBuffer componentBuffer = new ComponentBuffer();
					componentBuffer.SetBuffer(jpeg_common_struct.AllocJpegSamples(JpegUtils.jround_up(cinfo.m_output_width, cinfo.m_max_h_samp_factor), cinfo.m_max_v_samp_factor), null, 0);
					m_color_buf[i] = componentBuffer;
				}
			}
		}

		public override void start_pass()
		{
			m_next_row_out = m_cinfo.m_max_v_samp_factor;
			m_rows_to_go = m_cinfo.m_output_height;
		}

		public override void upsample(ComponentBuffer[] input_buf, ref int in_row_group_ctr, int in_row_groups_avail, byte[][] output_buf, ref int out_row_ctr, int out_rows_avail)
		{
			if (m_next_row_out >= m_cinfo.m_max_v_samp_factor)
			{
				for (int i = 0; i < m_cinfo.m_num_components; i++)
				{
					m_perComponentOffsets[i] = 0;
					m_currentComponent = i;
					m_upsampleRowOffset = in_row_group_ctr * m_rowgroup_height[i];
					upsampleComponent(ref input_buf[i]);
				}
				m_next_row_out = 0;
			}
			int num = m_cinfo.m_max_v_samp_factor - m_next_row_out;
			if (num > m_rows_to_go)
			{
				num = m_rows_to_go;
			}
			out_rows_avail -= out_row_ctr;
			if (num > out_rows_avail)
			{
				num = out_rows_avail;
			}
			m_cinfo.m_cconvert.color_convert(m_color_buf, m_perComponentOffsets, m_next_row_out, output_buf, out_row_ctr, num);
			out_row_ctr += num;
			m_rows_to_go -= num;
			m_next_row_out += num;
			if (m_next_row_out >= m_cinfo.m_max_v_samp_factor)
			{
				in_row_group_ctr++;
			}
		}

		private void upsampleComponent(ref ComponentBuffer input_data)
		{
			switch (m_upsampleMethods[m_currentComponent])
			{
			case ComponentUpsampler.noop_upsampler:
				noop_upsample();
				break;
			case ComponentUpsampler.fullsize_upsampler:
				fullsize_upsample(ref input_data);
				break;
			case ComponentUpsampler.h2v1_fancy_upsampler:
				h2v1_fancy_upsample(m_cinfo.Comp_info[m_currentComponent].downsampled_width, ref input_data);
				break;
			case ComponentUpsampler.h2v1_upsampler:
				h2v1_upsample(ref input_data);
				break;
			case ComponentUpsampler.h2v2_fancy_upsampler:
				h2v2_fancy_upsample(m_cinfo.Comp_info[m_currentComponent].downsampled_width, ref input_data);
				break;
			case ComponentUpsampler.h2v2_upsampler:
				h2v2_upsample(ref input_data);
				break;
			case ComponentUpsampler.int_upsampler:
				int_upsample(ref input_data);
				break;
			default:
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
				break;
			}
		}

		private static void noop_upsample()
		{
		}

		private void fullsize_upsample(ref ComponentBuffer input_data)
		{
			m_color_buf[m_currentComponent] = input_data;
			m_perComponentOffsets[m_currentComponent] = m_upsampleRowOffset;
		}

		private void h2v1_fancy_upsample(int downsampled_width, ref ComponentBuffer input_data)
		{
			ComponentBuffer componentBuffer = m_color_buf[m_currentComponent];
			for (int i = 0; i < m_cinfo.m_max_v_samp_factor; i++)
			{
				int i2 = m_upsampleRowOffset + i;
				int num = 0;
				int num2 = 0;
				int num3 = input_data[i2][num];
				num++;
				componentBuffer[i][num2] = (byte)num3;
				num2++;
				componentBuffer[i][num2] = (byte)(num3 * 3 + input_data[i2][num] + 2 >> 2);
				num2++;
				for (int num4 = downsampled_width - 2; num4 > 0; num4--)
				{
					num3 = input_data[i2][num] * 3;
					num++;
					componentBuffer[i][num2] = (byte)(num3 + input_data[i2][num - 2] + 1 >> 2);
					num2++;
					componentBuffer[i][num2] = (byte)(num3 + input_data[i2][num] + 2 >> 2);
					num2++;
				}
				num3 = input_data[i2][num];
				componentBuffer[i][num2] = (byte)(num3 * 3 + input_data[i2][num - 1] + 1 >> 2);
				num2++;
				componentBuffer[i][num2] = (byte)num3;
				num2++;
			}
		}

		private void h2v1_upsample(ref ComponentBuffer input_data)
		{
			ComponentBuffer componentBuffer = m_color_buf[m_currentComponent];
			for (int i = 0; i < m_cinfo.m_max_v_samp_factor; i++)
			{
				int i2 = m_upsampleRowOffset + i;
				int num = 0;
				for (int j = 0; j < m_cinfo.m_output_width; j++)
				{
					byte b = input_data[i2][j];
					componentBuffer[i][num] = b;
					num++;
					componentBuffer[i][num] = b;
					num++;
				}
			}
		}

		private void h2v2_fancy_upsample(int downsampled_width, ref ComponentBuffer input_data)
		{
			ComponentBuffer componentBuffer = m_color_buf[m_currentComponent];
			int num = m_upsampleRowOffset;
			int num2 = 0;
			while (num2 < m_cinfo.m_max_v_samp_factor)
			{
				for (int i = 0; i < 2; i++)
				{
					int num3 = 0;
					int num4 = 0;
					int num5 = -1;
					num5 = ((i != 0) ? (num + 1) : (num - 1));
					int i2 = num2;
					int num6 = 0;
					num2++;
					int num7 = input_data[num][num3] * 3 + input_data[num5][num4];
					num3++;
					num4++;
					int num8 = input_data[num][num3] * 3 + input_data[num5][num4];
					num3++;
					num4++;
					componentBuffer[i2][num6] = (byte)(num7 * 4 + 8 >> 4);
					num6++;
					componentBuffer[i2][num6] = (byte)(num7 * 3 + num8 + 7 >> 4);
					num6++;
					int num9 = num7;
					num7 = num8;
					for (int num10 = downsampled_width - 2; num10 > 0; num10--)
					{
						num8 = input_data[num][num3] * 3 + input_data[num5][num4];
						num3++;
						num4++;
						componentBuffer[i2][num6] = (byte)(num7 * 3 + num9 + 8 >> 4);
						num6++;
						componentBuffer[i2][num6] = (byte)(num7 * 3 + num8 + 7 >> 4);
						num6++;
						num9 = num7;
						num7 = num8;
					}
					componentBuffer[i2][num6] = (byte)(num7 * 3 + num9 + 8 >> 4);
					num6++;
					componentBuffer[i2][num6] = (byte)(num7 * 4 + 7 >> 4);
					num6++;
				}
				num++;
			}
		}

		private void h2v2_upsample(ref ComponentBuffer input_data)
		{
			ComponentBuffer componentBuffer = m_color_buf[m_currentComponent];
			int num = 0;
			for (int i = 0; i < m_cinfo.m_max_v_samp_factor; i += 2)
			{
				int i2 = m_upsampleRowOffset + num;
				int num2 = 0;
				for (int j = 0; j < m_cinfo.m_output_width; j++)
				{
					byte b = input_data[i2][j];
					componentBuffer[i][num2] = b;
					num2++;
					componentBuffer[i][num2] = b;
					num2++;
				}
				JpegUtils.jcopy_sample_rows(componentBuffer, i, componentBuffer, i + 1, 1, m_cinfo.m_output_width);
				num++;
			}
		}

		private void int_upsample(ref ComponentBuffer input_data)
		{
			ComponentBuffer componentBuffer = m_color_buf[m_currentComponent];
			int num = m_h_expand[m_currentComponent];
			int num2 = m_v_expand[m_currentComponent];
			int num3 = 0;
			for (int i = 0; i < m_cinfo.m_max_v_samp_factor; i += num2)
			{
				int i2 = m_upsampleRowOffset + num3;
				for (int j = 0; j < m_cinfo.m_output_width; j++)
				{
					byte b = input_data[i2][j];
					int num4 = 0;
					for (int num5 = num; num5 > 0; num5--)
					{
						componentBuffer[i][num4] = b;
						num4++;
					}
				}
				if (num2 > 1)
				{
					JpegUtils.jcopy_sample_rows(componentBuffer, i, componentBuffer, i + 1, num2 - 1, m_cinfo.m_output_width);
				}
				num3++;
			}
		}




	}
}
