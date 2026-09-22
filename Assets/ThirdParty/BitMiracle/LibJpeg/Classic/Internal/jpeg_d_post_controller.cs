using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class jpeg_d_post_controller
	{
		private enum ProcessorType
		{
			OnePass,
			PrePass,
			Upsample,
			SecondPass
		}

		private ProcessorType m_processor;

		private jpeg_decompress_struct m_cinfo;

		private jvirt_array<byte> m_whole_image;

		private byte[][] m_buffer;

		private int m_strip_height;

		private int m_starting_row;

		private int m_next_row;

		public jpeg_d_post_controller(jpeg_decompress_struct cinfo, bool need_full_buffer)
		{
			m_cinfo = cinfo;
			if (cinfo.m_quantize_colors)
			{
				m_strip_height = cinfo.m_max_v_samp_factor;
				if (need_full_buffer)
				{
					m_whole_image = jpeg_common_struct.CreateSamplesArray(cinfo.m_output_width * cinfo.m_out_color_components, JpegUtils.jround_up(cinfo.m_output_height, m_strip_height));
					m_whole_image.ErrorProcessor = cinfo;
				}
				else
				{
					m_buffer = jpeg_common_struct.AllocJpegSamples(cinfo.m_output_width * cinfo.m_out_color_components, m_strip_height);
				}
			}
		}

		public void start_pass(J_BUF_MODE pass_mode)
		{
			switch (pass_mode)
			{
			case J_BUF_MODE.JBUF_PASS_THRU:
				if (m_cinfo.m_quantize_colors)
				{
					m_processor = ProcessorType.OnePass;
					if (m_buffer == null)
					{
						m_buffer = m_whole_image.Access(0, m_strip_height);
					}
				}
				else
				{
					m_processor = ProcessorType.Upsample;
				}
				break;
			case J_BUF_MODE.JBUF_SAVE_AND_PASS:
				if (m_whole_image == null)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_BUFFER_MODE);
				}
				m_processor = ProcessorType.PrePass;
				break;
			case J_BUF_MODE.JBUF_CRANK_DEST:
				if (m_whole_image == null)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_BUFFER_MODE);
				}
				m_processor = ProcessorType.SecondPass;
				break;
			default:
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_BUFFER_MODE);
				break;
			}
			m_starting_row = (m_next_row = 0);
		}

		public void post_process_data(ComponentBuffer[] input_buf, ref int in_row_group_ctr, int in_row_groups_avail, byte[][] output_buf, ref int out_row_ctr, int out_rows_avail)
		{
			switch (m_processor)
			{
			case ProcessorType.OnePass:
				post_process_1pass(input_buf, ref in_row_group_ctr, in_row_groups_avail, output_buf, ref out_row_ctr, out_rows_avail);
				break;
			case ProcessorType.PrePass:
				post_process_prepass(input_buf, ref in_row_group_ctr, in_row_groups_avail, ref out_row_ctr);
				break;
			case ProcessorType.Upsample:
				m_cinfo.m_upsample.upsample(input_buf, ref in_row_group_ctr, in_row_groups_avail, output_buf, ref out_row_ctr, out_rows_avail);
				break;
			case ProcessorType.SecondPass:
				post_process_2pass(output_buf, ref out_row_ctr, out_rows_avail);
				break;
			default:
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
				break;
			}
		}

		private void post_process_1pass(ComponentBuffer[] input_buf, ref int in_row_group_ctr, int in_row_groups_avail, byte[][] output_buf, ref int out_row_ctr, int out_rows_avail)
		{
			int num = out_rows_avail - out_row_ctr;
			if (num > m_strip_height)
			{
				num = m_strip_height;
			}
			int out_row_ctr2 = 0;
			m_cinfo.m_upsample.upsample(input_buf, ref in_row_group_ctr, in_row_groups_avail, m_buffer, ref out_row_ctr2, num);
			m_cinfo.m_cquantize.color_quantize(m_buffer, 0, output_buf, out_row_ctr, out_row_ctr2);
			out_row_ctr += out_row_ctr2;
		}

		private void post_process_prepass(ComponentBuffer[] input_buf, ref int in_row_group_ctr, int in_row_groups_avail, ref int out_row_ctr)
		{
			if (m_next_row == 0)
			{
				m_buffer = m_whole_image.Access(m_starting_row, m_strip_height);
			}
			int next_row = m_next_row;
			m_cinfo.m_upsample.upsample(input_buf, ref in_row_group_ctr, in_row_groups_avail, m_buffer, ref m_next_row, m_strip_height);
			if (m_next_row > next_row)
			{
				int num = m_next_row - next_row;
				m_cinfo.m_cquantize.color_quantize(m_buffer, next_row, null, 0, num);
				out_row_ctr += num;
			}
			if (m_next_row >= m_strip_height)
			{
				m_starting_row += m_strip_height;
				m_next_row = 0;
			}
		}

		private void post_process_2pass(byte[][] output_buf, ref int out_row_ctr, int out_rows_avail)
		{
			if (m_next_row == 0)
			{
				m_buffer = m_whole_image.Access(m_starting_row, m_strip_height);
			}
			int num = m_strip_height - m_next_row;
			int num2 = out_rows_avail - out_row_ctr;
			if (num > num2)
			{
				num = num2;
			}
			num2 = m_cinfo.m_output_height - m_starting_row;
			if (num > num2)
			{
				num = num2;
			}
			m_cinfo.m_cquantize.color_quantize(m_buffer, m_next_row, output_buf, out_row_ctr, num);
			out_row_ctr += num;
			m_next_row += num;
			if (m_next_row >= m_strip_height)
			{
				m_starting_row += m_strip_height;
				m_next_row = 0;
			}
		}




	}
}
