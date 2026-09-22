using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class jpeg_input_controller
	{
		private jpeg_decompress_struct m_cinfo;

		private bool m_consumeData;

		private bool m_inheaders;

		private bool m_has_multiple_scans;

		private bool m_eoi_reached;

		public jpeg_input_controller(jpeg_decompress_struct cinfo)
		{
			m_cinfo = cinfo;
			m_inheaders = true;
		}

		public ReadResult consume_input()
		{
			if (m_consumeData)
			{
				return m_cinfo.m_coef.consume_data();
			}
			return consume_markers();
		}

		public void reset_input_controller()
		{
			m_consumeData = false;
			m_has_multiple_scans = false;
			m_eoi_reached = false;
			m_inheaders = true;
			m_cinfo.m_err.reset_error_mgr();
			m_cinfo.m_marker.reset_marker_reader();
			m_cinfo.m_coef_bits = null;
		}

		public void start_input_pass()
		{
			per_scan_setup();
			latch_quant_tables();
			m_cinfo.m_entropy.start_pass();
			m_cinfo.m_coef.start_input_pass();
			m_consumeData = true;
		}

		public void finish_input_pass()
		{
			m_consumeData = false;
		}

		public bool HasMultipleScans()
		{
			return m_has_multiple_scans;
		}

		public bool EOIReached()
		{
			return m_eoi_reached;
		}

		private ReadResult consume_markers()
		{
			if (m_eoi_reached)
			{
				return ReadResult.JPEG_REACHED_EOI;
			}
			ReadResult readResult = m_cinfo.m_marker.read_markers();
			switch (readResult)
			{
			case ReadResult.JPEG_REACHED_SOS:
				if (m_inheaders)
				{
					initial_setup();
					m_inheaders = false;
					break;
				}
				if (!m_has_multiple_scans)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_EOI_EXPECTED);
				}
				m_cinfo.m_inputctl.start_input_pass();
				break;
			case ReadResult.JPEG_REACHED_EOI:
				m_eoi_reached = true;
				if (m_inheaders)
				{
					if (m_cinfo.m_marker.SawSOF())
					{
						m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_SOF_NO_SOS);
					}
				}
				else if (m_cinfo.m_output_scan_number > m_cinfo.m_input_scan_number)
				{
					m_cinfo.m_output_scan_number = m_cinfo.m_input_scan_number;
				}
				break;
			}
			return readResult;
		}

		private void initial_setup()
		{
			if (m_cinfo.m_image_height > 65500 || m_cinfo.m_image_width > 65500)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_IMAGE_TOO_BIG, 65500);
			}
			if (m_cinfo.m_data_precision != 8)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_PRECISION, m_cinfo.m_data_precision);
			}
			if (m_cinfo.m_num_components > 10)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_COMPONENT_COUNT, m_cinfo.m_num_components, 10);
			}
			m_cinfo.m_max_h_samp_factor = 1;
			m_cinfo.m_max_v_samp_factor = 1;
			for (int i = 0; i < m_cinfo.m_num_components; i++)
			{
				if (m_cinfo.Comp_info[i].H_samp_factor <= 0 || m_cinfo.Comp_info[i].H_samp_factor > 4 || m_cinfo.Comp_info[i].V_samp_factor <= 0 || m_cinfo.Comp_info[i].V_samp_factor > 4)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_SAMPLING);
				}
				m_cinfo.m_max_h_samp_factor = Math.Max(m_cinfo.m_max_h_samp_factor, m_cinfo.Comp_info[i].H_samp_factor);
				m_cinfo.m_max_v_samp_factor = Math.Max(m_cinfo.m_max_v_samp_factor, m_cinfo.Comp_info[i].V_samp_factor);
			}
			m_cinfo.m_min_DCT_scaled_size = 8;
			for (int j = 0; j < m_cinfo.m_num_components; j++)
			{
				m_cinfo.Comp_info[j].DCT_scaled_size = 8;
				m_cinfo.Comp_info[j].Width_in_blocks = JpegUtils.jdiv_round_up(m_cinfo.m_image_width * m_cinfo.Comp_info[j].H_samp_factor, m_cinfo.m_max_h_samp_factor * 8);
				m_cinfo.Comp_info[j].height_in_blocks = JpegUtils.jdiv_round_up(m_cinfo.m_image_height * m_cinfo.Comp_info[j].V_samp_factor, m_cinfo.m_max_v_samp_factor * 8);
				m_cinfo.Comp_info[j].downsampled_width = JpegUtils.jdiv_round_up(m_cinfo.m_image_width * m_cinfo.Comp_info[j].H_samp_factor, m_cinfo.m_max_h_samp_factor);
				m_cinfo.Comp_info[j].downsampled_height = JpegUtils.jdiv_round_up(m_cinfo.m_image_height * m_cinfo.Comp_info[j].V_samp_factor, m_cinfo.m_max_v_samp_factor);
				m_cinfo.Comp_info[j].component_needed = true;
				m_cinfo.Comp_info[j].quant_table = null;
			}
			m_cinfo.m_total_iMCU_rows = JpegUtils.jdiv_round_up(m_cinfo.m_image_height, m_cinfo.m_max_v_samp_factor * 8);
			if (m_cinfo.m_comps_in_scan < m_cinfo.m_num_components || m_cinfo.m_progressive_mode)
			{
				m_cinfo.m_inputctl.m_has_multiple_scans = true;
			}
			else
			{
				m_cinfo.m_inputctl.m_has_multiple_scans = false;
			}
		}

		private void latch_quant_tables()
		{
			for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
			{
				jpeg_component_info jpeg_component_info = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[i]];
				if (jpeg_component_info.quant_table == null)
				{
					int quant_tbl_no = jpeg_component_info.Quant_tbl_no;
					if (quant_tbl_no < 0 || quant_tbl_no >= 4 || m_cinfo.m_quant_tbl_ptrs[quant_tbl_no] == null)
					{
						m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_QUANT_TABLE, quant_tbl_no);
					}
					JQUANT_TBL jQUANT_TBL = new JQUANT_TBL();
					Buffer.BlockCopy(m_cinfo.m_quant_tbl_ptrs[quant_tbl_no].quantval, 0, jQUANT_TBL.quantval, 0, jQUANT_TBL.quantval.Length * 2);
					jQUANT_TBL.Sent_table = m_cinfo.m_quant_tbl_ptrs[quant_tbl_no].Sent_table;
					jpeg_component_info.quant_table = jQUANT_TBL;
					m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[i]] = jpeg_component_info;
				}
			}
		}

		private void per_scan_setup()
		{
			if (m_cinfo.m_comps_in_scan == 1)
			{
				jpeg_component_info jpeg_component_info = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[0]];
				m_cinfo.m_MCUs_per_row = jpeg_component_info.Width_in_blocks;
				m_cinfo.m_MCU_rows_in_scan = jpeg_component_info.height_in_blocks;
				jpeg_component_info.MCU_width = 1;
				jpeg_component_info.MCU_height = 1;
				jpeg_component_info.MCU_blocks = 1;
				jpeg_component_info.MCU_sample_width = jpeg_component_info.DCT_scaled_size;
				jpeg_component_info.last_col_width = 1;
				int num = jpeg_component_info.height_in_blocks % jpeg_component_info.V_samp_factor;
				if (num == 0)
				{
					num = jpeg_component_info.V_samp_factor;
				}
				jpeg_component_info.last_row_height = num;
				m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[0]] = jpeg_component_info;
				m_cinfo.m_blocks_in_MCU = 1;
				m_cinfo.m_MCU_membership[0] = 0;
				return;
			}
			if (m_cinfo.m_comps_in_scan <= 0 || m_cinfo.m_comps_in_scan > 4)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_COMPONENT_COUNT, m_cinfo.m_comps_in_scan, 4);
			}
			m_cinfo.m_MCUs_per_row = JpegUtils.jdiv_round_up(m_cinfo.m_image_width, m_cinfo.m_max_h_samp_factor * 8);
			m_cinfo.m_MCU_rows_in_scan = JpegUtils.jdiv_round_up(m_cinfo.m_image_height, m_cinfo.m_max_v_samp_factor * 8);
			m_cinfo.m_blocks_in_MCU = 0;
			for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
			{
				jpeg_component_info jpeg_component_info2 = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[i]];
				jpeg_component_info2.MCU_width = jpeg_component_info2.H_samp_factor;
				jpeg_component_info2.MCU_height = jpeg_component_info2.V_samp_factor;
				jpeg_component_info2.MCU_blocks = jpeg_component_info2.MCU_width * jpeg_component_info2.MCU_height;
				jpeg_component_info2.MCU_sample_width = jpeg_component_info2.MCU_width * jpeg_component_info2.DCT_scaled_size;
				int num2 = jpeg_component_info2.Width_in_blocks % jpeg_component_info2.MCU_width;
				if (num2 == 0)
				{
					num2 = jpeg_component_info2.MCU_width;
				}
				jpeg_component_info2.last_col_width = num2;
				num2 = jpeg_component_info2.height_in_blocks % jpeg_component_info2.MCU_height;
				if (num2 == 0)
				{
					num2 = jpeg_component_info2.MCU_height;
				}
				jpeg_component_info2.last_row_height = num2;
				int mCU_blocks = jpeg_component_info2.MCU_blocks;
				if (m_cinfo.m_blocks_in_MCU + mCU_blocks > 10)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_MCU_SIZE);
				}
				m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[i]] = jpeg_component_info2;
				while (mCU_blocks-- > 0)
				{
					m_cinfo.m_MCU_membership[m_cinfo.m_blocks_in_MCU++] = i;
				}
			}
		}




	}
}
