using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class jpeg_d_main_controller
	{
		private enum DataProcessor
		{
			context_main,
			simple_main,
			crank_post
		}

		private const int CTX_PREPARE_FOR_IMCU = 0;

		private const int CTX_PROCESS_IMCU = 1;

		private const int CTX_POSTPONED_ROW = 2;

		private DataProcessor m_dataProcessor;

		private jpeg_decompress_struct m_cinfo;

		private byte[][][] m_buffer;

		private bool m_buffer_full;

		private int m_rowgroup_ctr;

		private int[][][] m_funnyIndices;

		private int[] m_funnyOffsets;

		private int m_whichFunny;

		private int m_context_state;

		private int m_rowgroups_avail;

		private int m_iMCU_row_ctr;

		public jpeg_d_main_controller(jpeg_decompress_struct cinfo)
		{
			m_buffer = new byte[10][][];
			m_funnyIndices = new int[2][][]
			{
				new int[10][],
				new int[10][]
			};
			m_funnyOffsets = new int[10];

			m_cinfo = cinfo;
			int num = cinfo.m_min_DCT_scaled_size;
			if (cinfo.m_upsample.NeedContextRows())
			{
				if (cinfo.m_min_DCT_scaled_size < 2)
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
				}
				alloc_funny_pointers();
				num = cinfo.m_min_DCT_scaled_size + 2;
			}
			for (int i = 0; i < cinfo.m_num_components; i++)
			{
				int num2 = cinfo.Comp_info[i].V_samp_factor * cinfo.Comp_info[i].DCT_scaled_size / cinfo.m_min_DCT_scaled_size;
				m_buffer[i] = jpeg_common_struct.AllocJpegSamples(cinfo.Comp_info[i].Width_in_blocks * cinfo.Comp_info[i].DCT_scaled_size, num2 * num);
			}
		}

		public void start_pass(J_BUF_MODE pass_mode)
		{
			switch (pass_mode)
			{
			case J_BUF_MODE.JBUF_PASS_THRU:
				if (m_cinfo.m_upsample.NeedContextRows())
				{
					m_dataProcessor = DataProcessor.context_main;
					make_funny_pointers();
					m_whichFunny = 0;
					m_context_state = 0;
					m_iMCU_row_ctr = 0;
				}
				else
				{
					m_dataProcessor = DataProcessor.simple_main;
				}
				m_buffer_full = false;
				m_rowgroup_ctr = 0;
				break;
			case J_BUF_MODE.JBUF_CRANK_DEST:
				m_dataProcessor = DataProcessor.crank_post;
				break;
			default:
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_BUFFER_MODE);
				break;
			}
		}

		public void process_data(byte[][] output_buf, ref int out_row_ctr, int out_rows_avail)
		{
			switch (m_dataProcessor)
			{
			case DataProcessor.simple_main:
				process_data_simple_main(output_buf, ref out_row_ctr, out_rows_avail);
				break;
			case DataProcessor.context_main:
				process_data_context_main(output_buf, ref out_row_ctr, out_rows_avail);
				break;
			case DataProcessor.crank_post:
				process_data_crank_post(output_buf, ref out_row_ctr, out_rows_avail);
				break;
			default:
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
				break;
			}
		}

		private void process_data_simple_main(byte[][] output_buf, ref int out_row_ctr, int out_rows_avail)
		{
			ComponentBuffer[] array = new ComponentBuffer[10];
			for (int i = 0; i < 10; i++)
			{
				array[i] = new ComponentBuffer();
				array[i].SetBuffer(m_buffer[i], null, 0);
			}
			if (!m_buffer_full)
			{
				if (m_cinfo.m_coef.decompress_data(array) == ReadResult.JPEG_SUSPENDED)
				{
					return;
				}
				m_buffer_full = true;
			}
			int min_DCT_scaled_size = m_cinfo.m_min_DCT_scaled_size;
			m_cinfo.m_post.post_process_data(array, ref m_rowgroup_ctr, min_DCT_scaled_size, output_buf, ref out_row_ctr, out_rows_avail);
			if (m_rowgroup_ctr >= min_DCT_scaled_size)
			{
				m_buffer_full = false;
				m_rowgroup_ctr = 0;
			}
		}

		private void process_data_context_main(byte[][] output_buf, ref int out_row_ctr, int out_rows_avail)
		{
			ComponentBuffer[] array = new ComponentBuffer[m_cinfo.m_num_components];
			for (int i = 0; i < m_cinfo.m_num_components; i++)
			{
				array[i] = new ComponentBuffer();
				array[i].SetBuffer(m_buffer[i], m_funnyIndices[m_whichFunny][i], m_funnyOffsets[i]);
			}
			if (!m_buffer_full)
			{
				if (m_cinfo.m_coef.decompress_data(array) == ReadResult.JPEG_SUSPENDED)
				{
					return;
				}
				m_buffer_full = true;
				m_iMCU_row_ctr++;
			}
			if (m_context_state == 2)
			{
				m_cinfo.m_post.post_process_data(array, ref m_rowgroup_ctr, m_rowgroups_avail, output_buf, ref out_row_ctr, out_rows_avail);
				if (m_rowgroup_ctr < m_rowgroups_avail)
				{
					return;
				}
				m_context_state = 0;
				if (out_row_ctr >= out_rows_avail)
				{
					return;
				}
			}
			if (m_context_state == 0)
			{
				m_rowgroup_ctr = 0;
				m_rowgroups_avail = m_cinfo.m_min_DCT_scaled_size - 1;
				if (m_iMCU_row_ctr == m_cinfo.m_total_iMCU_rows)
				{
					set_bottom_pointers();
				}
				m_context_state = 1;
			}
			if (m_context_state != 1)
			{
				return;
			}
			m_cinfo.m_post.post_process_data(array, ref m_rowgroup_ctr, m_rowgroups_avail, output_buf, ref out_row_ctr, out_rows_avail);
			if (m_rowgroup_ctr >= m_rowgroups_avail)
			{
				if (m_iMCU_row_ctr == 1)
				{
					set_wraparound_pointers();
				}
				m_whichFunny ^= 1;
				m_buffer_full = false;
				m_rowgroup_ctr = m_cinfo.m_min_DCT_scaled_size + 1;
				m_rowgroups_avail = m_cinfo.m_min_DCT_scaled_size + 2;
				m_context_state = 2;
			}
		}

		private void process_data_crank_post(byte[][] output_buf, ref int out_row_ctr, int out_rows_avail)
		{
			int in_row_group_ctr = 0;
			m_cinfo.m_post.post_process_data(null, ref in_row_group_ctr, 0, output_buf, ref out_row_ctr, out_rows_avail);
		}

		private void alloc_funny_pointers()
		{
			int min_DCT_scaled_size = m_cinfo.m_min_DCT_scaled_size;
			for (int i = 0; i < m_cinfo.m_num_components; i++)
			{
				int num = m_cinfo.Comp_info[i].V_samp_factor * m_cinfo.Comp_info[i].DCT_scaled_size / m_cinfo.m_min_DCT_scaled_size;
				m_funnyIndices[0][i] = new int[num * (min_DCT_scaled_size + 4)];
				m_funnyIndices[1][i] = new int[num * (min_DCT_scaled_size + 4)];
				m_funnyOffsets[i] = num;
			}
		}

		private void make_funny_pointers()
		{
			int min_DCT_scaled_size = m_cinfo.m_min_DCT_scaled_size;
			for (int i = 0; i < m_cinfo.m_num_components; i++)
			{
				int num = m_cinfo.Comp_info[i].V_samp_factor * m_cinfo.Comp_info[i].DCT_scaled_size / m_cinfo.m_min_DCT_scaled_size;
				int[] array = m_funnyIndices[0][i];
				int[] array2 = m_funnyIndices[1][i];
				for (int j = 0; j < num * (min_DCT_scaled_size + 2); j++)
				{
					array[j + num] = j;
					array2[j + num] = j;
				}
				for (int k = 0; k < num * 2; k++)
				{
					array2[num * (min_DCT_scaled_size - 1) + k] = num * min_DCT_scaled_size + k;
					array2[num * (min_DCT_scaled_size + 1) + k] = num * (min_DCT_scaled_size - 2) + k;
				}
				for (int l = 0; l < num; l++)
				{
					array[l] = array[num];
				}
			}
		}

		private void set_wraparound_pointers()
		{
			int min_DCT_scaled_size = m_cinfo.m_min_DCT_scaled_size;
			for (int i = 0; i < m_cinfo.m_num_components; i++)
			{
				int num = m_cinfo.Comp_info[i].V_samp_factor * m_cinfo.Comp_info[i].DCT_scaled_size / m_cinfo.m_min_DCT_scaled_size;
				int[] array = m_funnyIndices[0][i];
				int[] array2 = m_funnyIndices[1][i];
				for (int j = 0; j < num; j++)
				{
					array[j] = array[num * (min_DCT_scaled_size + 2) + j];
					array2[j] = array2[num * (min_DCT_scaled_size + 2) + j];
					array[num * (min_DCT_scaled_size + 3) + j] = array[j + num];
					array2[num * (min_DCT_scaled_size + 3) + j] = array2[j + num];
				}
			}
		}

		private void set_bottom_pointers()
		{
			for (int i = 0; i < m_cinfo.m_num_components; i++)
			{
				int num = m_cinfo.Comp_info[i].V_samp_factor * m_cinfo.Comp_info[i].DCT_scaled_size;
				int num2 = num / m_cinfo.m_min_DCT_scaled_size;
				int num3 = m_cinfo.Comp_info[i].downsampled_height % num;
				if (num3 == 0)
				{
					num3 = num;
				}
				if (i == 0)
				{
					m_rowgroups_avail = (num3 - 1) / num2 + 1;
				}
				for (int j = 0; j < num2 * 2; j++)
				{
					m_funnyIndices[m_whichFunny][i][num3 + j + num2] = m_funnyIndices[m_whichFunny][i][num3 - 1 + num2];
				}
			}
		}




	}
}
