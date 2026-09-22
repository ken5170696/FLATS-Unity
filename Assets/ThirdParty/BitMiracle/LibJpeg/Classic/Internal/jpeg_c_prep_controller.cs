using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class jpeg_c_prep_controller
	{
		private jpeg_compress_struct m_cinfo;

		private byte[][][] m_color_buf;

		private int m_colorBufRowsOffset;

		private int m_rows_to_go;

		private int m_next_buf_row;

		private int m_this_row_group;

		private int m_next_buf_stop;

		public jpeg_c_prep_controller(jpeg_compress_struct cinfo)
		{
			m_color_buf = new byte[10][][];

			m_cinfo = cinfo;
			if (cinfo.m_downsample.NeedContextRows())
			{
				create_context_buffer();
				return;
			}
			for (int i = 0; i < cinfo.m_num_components; i++)
			{
				m_colorBufRowsOffset = 0;
				m_color_buf[i] = jpeg_common_struct.AllocJpegSamples(cinfo.Component_info[i].Width_in_blocks * 8 * cinfo.m_max_h_samp_factor / cinfo.Component_info[i].H_samp_factor, cinfo.m_max_v_samp_factor);
			}
		}

		public void start_pass(J_BUF_MODE pass_mode)
		{
			if (pass_mode != J_BUF_MODE.JBUF_PASS_THRU)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_BUFFER_MODE);
			}
			m_rows_to_go = m_cinfo.m_image_height;
			m_next_buf_row = 0;
			m_this_row_group = 0;
			m_next_buf_stop = 2 * m_cinfo.m_max_v_samp_factor;
		}

		public void pre_process_data(byte[][] input_buf, ref int in_row_ctr, int in_rows_avail, byte[][][] output_buf, ref int out_row_group_ctr, int out_row_groups_avail)
		{
			if (m_cinfo.m_downsample.NeedContextRows())
			{
				pre_process_context(input_buf, ref in_row_ctr, in_rows_avail, output_buf, ref out_row_group_ctr, out_row_groups_avail);
			}
			else
			{
				pre_process_WithoutContext(input_buf, ref in_row_ctr, in_rows_avail, output_buf, ref out_row_group_ctr, out_row_groups_avail);
			}
		}

		private void create_context_buffer()
		{
			int max_v_samp_factor = m_cinfo.m_max_v_samp_factor;
			for (int i = 0; i < m_cinfo.m_num_components; i++)
			{
				int num = m_cinfo.Component_info[i].Width_in_blocks * 8 * m_cinfo.m_max_h_samp_factor / m_cinfo.Component_info[i].H_samp_factor;
				byte[][] array = new byte[5 * max_v_samp_factor][];
				for (int j = 1; j < 4 * max_v_samp_factor; j++)
				{
					array[j] = new byte[num];
				}
				byte[][] array2 = jpeg_common_struct.AllocJpegSamples(num, 3 * max_v_samp_factor);
				for (int k = 0; k < 3 * max_v_samp_factor; k++)
				{
					array[max_v_samp_factor + k] = array2[k];
				}
				for (int l = 0; l < max_v_samp_factor; l++)
				{
					array[l] = array2[2 * max_v_samp_factor + l];
					array[4 * max_v_samp_factor + l] = array2[l];
				}
				m_color_buf[i] = array;
				m_colorBufRowsOffset = max_v_samp_factor;
			}
		}

		private void pre_process_WithoutContext(byte[][] input_buf, ref int in_row_ctr, int in_rows_avail, byte[][][] output_buf, ref int out_row_group_ctr, int out_row_groups_avail)
		{
			while (in_row_ctr < in_rows_avail && out_row_group_ctr < out_row_groups_avail)
			{
				int val = in_rows_avail - in_row_ctr;
				int val2 = m_cinfo.m_max_v_samp_factor - m_next_buf_row;
				val2 = Math.Min(val2, val);
				m_cinfo.m_cconvert.color_convert(input_buf, in_row_ctr, m_color_buf, m_colorBufRowsOffset + m_next_buf_row, val2);
				in_row_ctr += val2;
				m_next_buf_row += val2;
				m_rows_to_go -= val2;
				if (m_rows_to_go == 0 && m_next_buf_row < m_cinfo.m_max_v_samp_factor)
				{
					for (int i = 0; i < m_cinfo.m_num_components; i++)
					{
						expand_bottom_edge(m_color_buf[i], m_colorBufRowsOffset, m_cinfo.m_image_width, m_next_buf_row, m_cinfo.m_max_v_samp_factor);
					}
					m_next_buf_row = m_cinfo.m_max_v_samp_factor;
				}
				if (m_next_buf_row == m_cinfo.m_max_v_samp_factor)
				{
					m_cinfo.m_downsample.downsample(m_color_buf, m_colorBufRowsOffset, output_buf, out_row_group_ctr);
					m_next_buf_row = 0;
					out_row_group_ctr++;
				}
				if (m_rows_to_go == 0 && out_row_group_ctr < out_row_groups_avail)
				{
					for (int j = 0; j < m_cinfo.m_num_components; j++)
					{
						jpeg_component_info jpeg_component_info = m_cinfo.Component_info[j];
						expand_bottom_edge(output_buf[j], 0, jpeg_component_info.Width_in_blocks * 8, out_row_group_ctr * jpeg_component_info.V_samp_factor, out_row_groups_avail * jpeg_component_info.V_samp_factor);
					}
					out_row_group_ctr = out_row_groups_avail;
					break;
				}
			}
		}

		private void pre_process_context(byte[][] input_buf, ref int in_row_ctr, int in_rows_avail, byte[][][] output_buf, ref int out_row_group_ctr, int out_row_groups_avail)
		{
			while (out_row_group_ctr < out_row_groups_avail)
			{
				if (in_row_ctr < in_rows_avail)
				{
					int val = in_rows_avail - in_row_ctr;
					int val2 = m_next_buf_stop - m_next_buf_row;
					val2 = Math.Min(val2, val);
					m_cinfo.m_cconvert.color_convert(input_buf, in_row_ctr, m_color_buf, m_colorBufRowsOffset + m_next_buf_row, val2);
					if (m_rows_to_go == m_cinfo.m_image_height)
					{
						for (int i = 0; i < m_cinfo.m_num_components; i++)
						{
							for (int j = 1; j <= m_cinfo.m_max_v_samp_factor; j++)
							{
								JpegUtils.jcopy_sample_rows(m_color_buf[i], m_colorBufRowsOffset, m_color_buf[i], m_colorBufRowsOffset - j, 1, m_cinfo.m_image_width);
							}
						}
					}
					in_row_ctr += val2;
					m_next_buf_row += val2;
					m_rows_to_go -= val2;
				}
				else
				{
					if (m_rows_to_go != 0)
					{
						break;
					}
					if (m_next_buf_row < m_next_buf_stop)
					{
						for (int k = 0; k < m_cinfo.m_num_components; k++)
						{
							expand_bottom_edge(m_color_buf[k], m_colorBufRowsOffset, m_cinfo.m_image_width, m_next_buf_row, m_next_buf_stop);
						}
						m_next_buf_row = m_next_buf_stop;
					}
				}
				if (m_next_buf_row == m_next_buf_stop)
				{
					m_cinfo.m_downsample.downsample(m_color_buf, m_colorBufRowsOffset + m_this_row_group, output_buf, out_row_group_ctr);
					out_row_group_ctr++;
					m_this_row_group += m_cinfo.m_max_v_samp_factor;
					int num = m_cinfo.m_max_v_samp_factor * 3;
					if (m_this_row_group >= num)
					{
						m_this_row_group = 0;
					}
					if (m_next_buf_row >= num)
					{
						m_next_buf_row = 0;
					}
					m_next_buf_stop = m_next_buf_row + m_cinfo.m_max_v_samp_factor;
				}
			}
		}

		private static void expand_bottom_edge(byte[][] image_data, int rowsOffset, int num_cols, int input_rows, int output_rows)
		{
			for (int i = input_rows; i < output_rows; i++)
			{
				JpegUtils.jcopy_sample_rows(image_data, rowsOffset + input_rows - 1, image_data, i, 1, num_cols);
			}
		}




	}
}
