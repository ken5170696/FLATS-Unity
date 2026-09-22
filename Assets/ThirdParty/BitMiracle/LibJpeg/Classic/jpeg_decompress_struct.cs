using System;
using System.Collections.Generic;
using System.IO;
using BitMiracle.LibJpeg.Classic.Internal;
namespace BitMiracle.LibJpeg.Classic
{
	internal class jpeg_decompress_struct : jpeg_common_struct
	{
		public delegate bool jpeg_marker_parser_method(jpeg_decompress_struct cinfo);

		internal jpeg_source_mgr m_src;

		internal int m_image_width;

		internal int m_image_height;

		internal int m_num_components;

		internal J_COLOR_SPACE m_jpeg_color_space;

		internal J_COLOR_SPACE m_out_color_space;

		internal int m_scale_num;

		internal int m_scale_denom;

		internal bool m_buffered_image;

		internal bool m_raw_data_out;

		internal J_DCT_METHOD m_dct_method;

		internal bool m_do_fancy_upsampling;

		internal bool m_do_block_smoothing;

		internal bool m_quantize_colors;

		internal J_DITHER_MODE m_dither_mode;

		internal bool m_two_pass_quantize;

		internal int m_desired_number_of_colors;

		internal bool m_enable_1pass_quant;

		internal bool m_enable_external_quant;

		internal bool m_enable_2pass_quant;

		internal int m_output_width;

		internal int m_output_height;

		internal int m_out_color_components;

		internal int m_output_components;

		internal int m_rec_outbuf_height;

		internal int m_actual_number_of_colors;

		internal byte[][] m_colormap;

		internal int m_output_scanline;

		internal int m_input_scan_number;

		internal int m_input_iMCU_row;

		internal int m_output_scan_number;

		internal int m_output_iMCU_row;

		internal int[][] m_coef_bits;

		internal JQUANT_TBL[] m_quant_tbl_ptrs;

		internal JHUFF_TBL[] m_dc_huff_tbl_ptrs;

		internal JHUFF_TBL[] m_ac_huff_tbl_ptrs;

		internal int m_data_precision;

		private jpeg_component_info[] m_comp_info;

		internal bool m_progressive_mode;

		internal int m_restart_interval;

		internal bool m_saw_JFIF_marker;

		internal byte m_JFIF_major_version;

		internal byte m_JFIF_minor_version;

		internal DensityUnit m_density_unit;

		internal short m_X_density;

		internal short m_Y_density;

		internal bool m_saw_Adobe_marker;

		internal byte m_Adobe_transform;

		internal bool m_CCIR601_sampling;

		internal List<jpeg_marker_struct> m_marker_list;

		internal int m_max_h_samp_factor;

		internal int m_max_v_samp_factor;

		internal int m_min_DCT_scaled_size;

		internal int m_total_iMCU_rows;

		internal byte[] m_sample_range_limit;

		internal int m_sampleRangeLimitOffset;

		internal int m_comps_in_scan;

		internal int[] m_cur_comp_info;

		internal int m_MCUs_per_row;

		internal int m_MCU_rows_in_scan;

		internal int m_blocks_in_MCU;

		internal int[] m_MCU_membership;

		internal int m_Ss;

		internal int m_Se;

		internal int m_Ah;

		internal int m_Al;

		internal int m_unread_marker;

		internal jpeg_decomp_master m_master;

		internal jpeg_d_main_controller m_main;

		internal jpeg_d_coef_controller m_coef;

		internal jpeg_d_post_controller m_post;

		internal jpeg_input_controller m_inputctl;

		internal jpeg_marker_reader m_marker;

		internal jpeg_entropy_decoder m_entropy;

		internal jpeg_inverse_dct m_idct;

		internal jpeg_upsampler m_upsample;

		internal jpeg_color_deconverter m_cconvert;

		internal jpeg_color_quantizer m_cquantize;

		public override bool IsDecompressor
		{
			get
			{
				return true;
			}
		}

		public jpeg_source_mgr Src
		{
			get
			{
				return m_src;
			}
			set
			{
				m_src = value;
			}
		}

		public int Image_width
		{
			get
			{
				return m_image_width;
			}
		}

		public int Image_height
		{
			get
			{
				return m_image_height;
			}
		}

		public int Num_components
		{
			get
			{
				return m_num_components;
			}
		}

		public J_COLOR_SPACE Jpeg_color_space
		{
			get
			{
				return m_jpeg_color_space;
			}
			set
			{
				m_jpeg_color_space = value;
			}
		}

		public List<jpeg_marker_struct> Marker_list
		{
			get
			{
				return m_marker_list;
			}
		}

		public J_COLOR_SPACE Out_color_space
		{
			get
			{
				return m_out_color_space;
			}
			set
			{
				m_out_color_space = value;
			}
		}

		public int Scale_num
		{
			get
			{
				return m_scale_num;
			}
			set
			{
				m_scale_num = value;
			}
		}

		public int Scale_denom
		{
			get
			{
				return m_scale_denom;
			}
			set
			{
				m_scale_denom = value;
			}
		}

		public bool Buffered_image
		{
			get
			{
				return m_buffered_image;
			}
			set
			{
				m_buffered_image = value;
			}
		}

		public bool Raw_data_out
		{
			get
			{
				return m_raw_data_out;
			}
			set
			{
				m_raw_data_out = value;
			}
		}

		public J_DCT_METHOD Dct_method
		{
			get
			{
				return m_dct_method;
			}
			set
			{
				m_dct_method = value;
			}
		}

		public bool Do_fancy_upsampling
		{
			get
			{
				return m_do_fancy_upsampling;
			}
			set
			{
				m_do_fancy_upsampling = value;
			}
		}

		public bool Do_block_smoothing
		{
			get
			{
				return m_do_block_smoothing;
			}
			set
			{
				m_do_block_smoothing = value;
			}
		}

		public bool Quantize_colors
		{
			get
			{
				return m_quantize_colors;
			}
			set
			{
				m_quantize_colors = value;
			}
		}

		public J_DITHER_MODE Dither_mode
		{
			get
			{
				return m_dither_mode;
			}
			set
			{
				m_dither_mode = value;
			}
		}

		public bool Two_pass_quantize
		{
			get
			{
				return m_two_pass_quantize;
			}
			set
			{
				m_two_pass_quantize = value;
			}
		}

		public int Desired_number_of_colors
		{
			get
			{
				return m_desired_number_of_colors;
			}
			set
			{
				m_desired_number_of_colors = value;
			}
		}

		public bool Enable_1pass_quant
		{
			get
			{
				return m_enable_1pass_quant;
			}
			set
			{
				m_enable_1pass_quant = value;
			}
		}

		public bool Enable_external_quant
		{
			get
			{
				return m_enable_external_quant;
			}
			set
			{
				m_enable_external_quant = value;
			}
		}

		public bool Enable_2pass_quant
		{
			get
			{
				return m_enable_2pass_quant;
			}
			set
			{
				m_enable_2pass_quant = value;
			}
		}

		public int Output_width
		{
			get
			{
				return m_output_width;
			}
		}

		public int Output_height
		{
			get
			{
				return m_output_height;
			}
		}

		public int Out_color_components
		{
			get
			{
				return m_out_color_components;
			}
		}

		public int Output_components
		{
			get
			{
				return m_output_components;
			}
		}

		public int Rec_outbuf_height
		{
			get
			{
				return m_rec_outbuf_height;
			}
		}

		public int Actual_number_of_colors
		{
			get
			{
				return m_actual_number_of_colors;
			}
			set
			{
				m_actual_number_of_colors = value;
			}
		}

		public byte[][] Colormap
		{
			get
			{
				return m_colormap;
			}
			set
			{
				m_colormap = value;
			}
		}

		public int Output_scanline
		{
			get
			{
				return m_output_scanline;
			}
		}

		public int Input_scan_number
		{
			get
			{
				return m_input_scan_number;
			}
		}

		public int Input_iMCU_row
		{
			get
			{
				return m_input_iMCU_row;
			}
		}

		public int Output_scan_number
		{
			get
			{
				return m_output_scan_number;
			}
		}

		public int Output_iMCU_row
		{
			get
			{
				return m_output_iMCU_row;
			}
		}

		public int[][] Coef_bits
		{
			get
			{
				return m_coef_bits;
			}
		}

		public DensityUnit Density_unit
		{
			get
			{
				return m_density_unit;
			}
		}

		public short X_density
		{
			get
			{
				return m_X_density;
			}
		}

		public short Y_density
		{
			get
			{
				return m_Y_density;
			}
		}

		public int Data_precision
		{
			get
			{
				return m_data_precision;
			}
		}

		public int Max_v_samp_factor
		{
			get
			{
				return m_max_v_samp_factor;
			}
		}

		public int Unread_marker
		{
			get
			{
				return m_unread_marker;
			}
		}

		public jpeg_component_info[] Comp_info
		{
			get
			{
				return m_comp_info;
			}
			internal set
			{
				m_comp_info = value;
			}
		}

		public jpeg_decompress_struct()
		{
			m_quant_tbl_ptrs = new JQUANT_TBL[4];
			m_dc_huff_tbl_ptrs = new JHUFF_TBL[4];
			m_ac_huff_tbl_ptrs = new JHUFF_TBL[4];
			m_cur_comp_info = new int[4];
			m_MCU_membership = new int[10];

			initialize();
		}

		public jpeg_decompress_struct(jpeg_error_mgr errorManager) : base(errorManager)
		{
			m_quant_tbl_ptrs = new JQUANT_TBL[4];
			m_dc_huff_tbl_ptrs = new JHUFF_TBL[4];
			m_ac_huff_tbl_ptrs = new JHUFF_TBL[4];
			m_cur_comp_info = new int[4];
			m_MCU_membership = new int[10];
			
			initialize();
		}

		public void jpeg_stdio_src(Stream infile)
		{
			if (m_src == null)
			{
				m_src = new my_source_mgr(this);
			}
			my_source_mgr my_source_mgr = m_src as my_source_mgr;
			if (my_source_mgr != null)
			{
				my_source_mgr.Attach(infile);
			}
		}

		public ReadResult jpeg_read_header(bool require_image)
		{
			if (m_global_state != JpegState.DSTATE_START && m_global_state != JpegState.DSTATE_INHEADER)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			switch (jpeg_consume_input())
			{
			case ReadResult.JPEG_REACHED_SOS:
				return ReadResult.JPEG_HEADER_OK;
			case ReadResult.JPEG_REACHED_EOI:
				if (require_image)
				{
					ERREXIT(J_MESSAGE_CODE.JERR_NO_IMAGE);
				}
				jpeg_abort();
				return ReadResult.JPEG_HEADER_TABLES_ONLY;
			default:
				return ReadResult.JPEG_SUSPENDED;
			}
		}

		public bool jpeg_start_decompress()
		{
			if (m_global_state == JpegState.DSTATE_READY)
			{
				m_master = new jpeg_decomp_master(this);
				if (m_buffered_image)
				{
					m_global_state = JpegState.DSTATE_BUFIMAGE;
					return true;
				}
				m_global_state = JpegState.DSTATE_PRELOAD;
			}
			if (m_global_state == JpegState.DSTATE_PRELOAD)
			{
				if (m_inputctl.HasMultipleScans())
				{
					while (true)
					{
						if (m_progress != null)
						{
							m_progress.Updated();
						}
						ReadResult readResult = m_inputctl.consume_input();
						switch (readResult)
						{
						case ReadResult.JPEG_SUSPENDED:
							return false;
						default:
							if (m_progress != null && (readResult == ReadResult.JPEG_ROW_COMPLETED || readResult == ReadResult.JPEG_REACHED_SOS))
							{
								m_progress.Pass_counter++;
								if (m_progress.Pass_counter >= m_progress.Pass_limit)
								{
									m_progress.Pass_limit += m_total_iMCU_rows;
								}
							}
							continue;
						case ReadResult.JPEG_REACHED_EOI:
							break;
						}
						break;
					}
				}
				m_output_scan_number = m_input_scan_number;
			}
			else if (m_global_state != JpegState.DSTATE_PRESCAN)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			return output_pass_setup();
		}

		public int jpeg_read_scanlines(byte[][] scanlines, int max_lines)
		{
			if (m_global_state != JpegState.DSTATE_SCANNING)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			if (m_output_scanline >= m_output_height)
			{
				WARNMS(J_MESSAGE_CODE.JWRN_TOO_MUCH_DATA);
				return 0;
			}
			if (m_progress != null)
			{
				m_progress.Pass_counter = m_output_scanline;
				m_progress.Pass_limit = m_output_height;
				m_progress.Updated();
			}
			int out_row_ctr = 0;
			m_main.process_data(scanlines, ref out_row_ctr, max_lines);
			m_output_scanline += out_row_ctr;
			return out_row_ctr;
		}

		public bool jpeg_finish_decompress()
		{
			if ((m_global_state == JpegState.DSTATE_SCANNING || m_global_state == JpegState.DSTATE_RAW_OK) && !m_buffered_image)
			{
				if (m_output_scanline < m_output_height)
				{
					ERREXIT(J_MESSAGE_CODE.JERR_TOO_LITTLE_DATA);
				}
				m_master.finish_output_pass();
				m_global_state = JpegState.DSTATE_STOPPING;
			}
			else if (m_global_state == JpegState.DSTATE_BUFIMAGE)
			{
				m_global_state = JpegState.DSTATE_STOPPING;
			}
			else if (m_global_state != JpegState.DSTATE_STOPPING)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			while (!m_inputctl.EOIReached())
			{
				if (m_inputctl.consume_input() == ReadResult.JPEG_SUSPENDED)
				{
					return false;
				}
			}
			m_src.term_source();
			jpeg_abort();
			return true;
		}

		public int jpeg_read_raw_data(byte[][][] data, int max_lines)
		{
			if (m_global_state != JpegState.DSTATE_RAW_OK)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			if (m_output_scanline >= m_output_height)
			{
				WARNMS(J_MESSAGE_CODE.JWRN_TOO_MUCH_DATA);
				return 0;
			}
			if (m_progress != null)
			{
				m_progress.Pass_counter = m_output_scanline;
				m_progress.Pass_limit = m_output_height;
				m_progress.Updated();
			}
			int num = m_max_v_samp_factor * m_min_DCT_scaled_size;
			if (max_lines < num)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BUFFER_SIZE);
			}
			int num2 = data.Length;
			ComponentBuffer[] array = new ComponentBuffer[num2];
			for (int i = 0; i < num2; i++)
			{
				array[i] = new ComponentBuffer();
				array[i].SetBuffer(data[i], null, 0);
			}
			if (m_coef.decompress_data(array) == ReadResult.JPEG_SUSPENDED)
			{
				return 0;
			}
			m_output_scanline += num;
			return num;
		}

		public bool jpeg_has_multiple_scans()
		{
			if (m_global_state < JpegState.DSTATE_READY || m_global_state > JpegState.DSTATE_STOPPING)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			return m_inputctl.HasMultipleScans();
		}

		public bool jpeg_start_output(int scan_number)
		{
			if (m_global_state != JpegState.DSTATE_BUFIMAGE && m_global_state != JpegState.DSTATE_PRESCAN)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			if (scan_number <= 0)
			{
				scan_number = 1;
			}
			if (m_inputctl.EOIReached() && scan_number > m_input_scan_number)
			{
				scan_number = m_input_scan_number;
			}
			m_output_scan_number = scan_number;
			return output_pass_setup();
		}

		public bool jpeg_finish_output()
		{
			if ((m_global_state == JpegState.DSTATE_SCANNING || m_global_state == JpegState.DSTATE_RAW_OK) && m_buffered_image)
			{
				m_master.finish_output_pass();
				m_global_state = JpegState.DSTATE_BUFPOST;
			}
			else if (m_global_state != JpegState.DSTATE_BUFPOST)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			while (m_input_scan_number <= m_output_scan_number && !m_inputctl.EOIReached())
			{
				if (m_inputctl.consume_input() == ReadResult.JPEG_SUSPENDED)
				{
					return false;
				}
			}
			m_global_state = JpegState.DSTATE_BUFIMAGE;
			return true;
		}

		public bool jpeg_input_complete()
		{
			if (m_global_state < JpegState.DSTATE_START || m_global_state > JpegState.DSTATE_STOPPING)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			return m_inputctl.EOIReached();
		}

		public ReadResult jpeg_consume_input()
		{
			ReadResult result = ReadResult.JPEG_SUSPENDED;
			switch (m_global_state)
			{
			case JpegState.DSTATE_START:
				jpeg_consume_input_start();
				result = jpeg_consume_input_inHeader();
				break;
			case JpegState.DSTATE_INHEADER:
				result = jpeg_consume_input_inHeader();
				break;
			case JpegState.DSTATE_READY:
				result = ReadResult.JPEG_REACHED_SOS;
				break;
			case JpegState.DSTATE_PRELOAD:
			case JpegState.DSTATE_PRESCAN:
			case JpegState.DSTATE_SCANNING:
			case JpegState.DSTATE_RAW_OK:
			case JpegState.DSTATE_BUFIMAGE:
			case JpegState.DSTATE_BUFPOST:
			case JpegState.DSTATE_STOPPING:
				result = m_inputctl.consume_input();
				break;
			default:
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
				break;
			}
			return result;
		}

		public void jpeg_calc_output_dimensions()
		{
			if (m_global_state != JpegState.DSTATE_READY)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			if (m_scale_num * 8 <= m_scale_denom)
			{
				m_output_width = JpegUtils.jdiv_round_up(m_image_width, 8);
				m_output_height = JpegUtils.jdiv_round_up(m_image_height, 8);
				m_min_DCT_scaled_size = 1;
			}
			else if (m_scale_num * 4 <= m_scale_denom)
			{
				m_output_width = JpegUtils.jdiv_round_up(m_image_width, 4);
				m_output_height = JpegUtils.jdiv_round_up(m_image_height, 4);
				m_min_DCT_scaled_size = 2;
			}
			else if (m_scale_num * 2 <= m_scale_denom)
			{
				m_output_width = JpegUtils.jdiv_round_up(m_image_width, 2);
				m_output_height = JpegUtils.jdiv_round_up(m_image_height, 2);
				m_min_DCT_scaled_size = 4;
			}
			else
			{
				m_output_width = m_image_width;
				m_output_height = m_image_height;
				m_min_DCT_scaled_size = 8;
			}
			for (int i = 0; i < m_num_components; i++)
			{
				int num = m_min_DCT_scaled_size;
				while (num < 8 && m_comp_info[i].H_samp_factor * num * 2 <= m_max_h_samp_factor * m_min_DCT_scaled_size && m_comp_info[i].V_samp_factor * num * 2 <= m_max_v_samp_factor * m_min_DCT_scaled_size)
				{
					num *= 2;
				}
				m_comp_info[i].DCT_scaled_size = num;
			}
			for (int j = 0; j < m_num_components; j++)
			{
				m_comp_info[j].downsampled_width = JpegUtils.jdiv_round_up(m_image_width * m_comp_info[j].H_samp_factor * m_comp_info[j].DCT_scaled_size, m_max_h_samp_factor * 8);
				m_comp_info[j].downsampled_height = JpegUtils.jdiv_round_up(m_image_height * m_comp_info[j].V_samp_factor * m_comp_info[j].DCT_scaled_size, m_max_v_samp_factor * 8);
			}
			switch (m_out_color_space)
			{
			case J_COLOR_SPACE.JCS_GRAYSCALE:
				m_out_color_components = 1;
				break;
			case J_COLOR_SPACE.JCS_RGB:
			case J_COLOR_SPACE.JCS_YCbCr:
				m_out_color_components = 3;
				break;
			case J_COLOR_SPACE.JCS_CMYK:
			case J_COLOR_SPACE.JCS_YCCK:
				m_out_color_components = 4;
				break;
			default:
				m_out_color_components = m_num_components;
				break;
			}
			m_output_components = (m_quantize_colors ? 1 : m_out_color_components);
			if (use_merged_upsample())
			{
				m_rec_outbuf_height = m_max_v_samp_factor;
			}
			else
			{
				m_rec_outbuf_height = 1;
			}
		}

		public jvirt_array<JBLOCK>[] jpeg_read_coefficients()
		{
			if (m_global_state == JpegState.DSTATE_READY)
			{
				transdecode_master_selection();
				m_global_state = JpegState.DSTATE_RDCOEFS;
			}
			if (m_global_state == JpegState.DSTATE_RDCOEFS)
			{
				while (true)
				{
					if (m_progress != null)
					{
						m_progress.Updated();
					}
					ReadResult readResult = m_inputctl.consume_input();
					switch (readResult)
					{
					case ReadResult.JPEG_SUSPENDED:
						return null;
					default:
						if (m_progress != null && (readResult == ReadResult.JPEG_ROW_COMPLETED || readResult == ReadResult.JPEG_REACHED_SOS))
						{
							m_progress.Pass_counter++;
							if (m_progress.Pass_counter >= m_progress.Pass_limit)
							{
								m_progress.Pass_limit += m_total_iMCU_rows;
							}
						}
						continue;
					case ReadResult.JPEG_REACHED_EOI:
						break;
					}
					break;
				}
				m_global_state = JpegState.DSTATE_STOPPING;
			}
			if ((m_global_state == JpegState.DSTATE_STOPPING || m_global_state == JpegState.DSTATE_BUFIMAGE) && m_buffered_image)
			{
				return m_coef.GetCoefArrays();
			}
			ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			return null;
		}

		public void jpeg_copy_critical_parameters(jpeg_compress_struct dstinfo)
		{
			if (dstinfo.m_global_state != JpegState.CSTATE_START)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)dstinfo.m_global_state);
			}
			dstinfo.m_image_width = m_image_width;
			dstinfo.m_image_height = m_image_height;
			dstinfo.m_input_components = m_num_components;
			dstinfo.m_in_color_space = m_jpeg_color_space;
			dstinfo.jpeg_set_defaults();
			dstinfo.jpeg_set_colorspace(m_jpeg_color_space);
			dstinfo.m_data_precision = m_data_precision;
			dstinfo.m_CCIR601_sampling = m_CCIR601_sampling;
			for (int i = 0; i < 4; i++)
			{
				if (m_quant_tbl_ptrs[i] != null)
				{
					if (dstinfo.m_quant_tbl_ptrs[i] == null)
					{
						dstinfo.m_quant_tbl_ptrs[i] = new JQUANT_TBL();
					}
					Buffer.BlockCopy(m_quant_tbl_ptrs[i].quantval, 0, dstinfo.m_quant_tbl_ptrs[i].quantval, 0, dstinfo.m_quant_tbl_ptrs[i].quantval.Length * 2);
					dstinfo.m_quant_tbl_ptrs[i].Sent_table = false;
				}
			}
			dstinfo.m_num_components = m_num_components;
			if (dstinfo.m_num_components < 1 || dstinfo.m_num_components > 10)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_COMPONENT_COUNT, dstinfo.m_num_components, 10);
			}
			for (int j = 0; j < dstinfo.m_num_components; j++)
			{
				dstinfo.Component_info[j].Component_id = m_comp_info[j].Component_id;
				dstinfo.Component_info[j].H_samp_factor = m_comp_info[j].H_samp_factor;
				dstinfo.Component_info[j].V_samp_factor = m_comp_info[j].V_samp_factor;
				dstinfo.Component_info[j].Quant_tbl_no = m_comp_info[j].Quant_tbl_no;
				int quant_tbl_no = dstinfo.Component_info[j].Quant_tbl_no;
				if (quant_tbl_no < 0 || quant_tbl_no >= 4 || m_quant_tbl_ptrs[quant_tbl_no] == null)
				{
					ERREXIT(J_MESSAGE_CODE.JERR_NO_QUANT_TABLE, quant_tbl_no);
				}
				JQUANT_TBL quant_table = m_comp_info[j].quant_table;
				if (quant_table == null)
				{
					continue;
				}
				JQUANT_TBL jQUANT_TBL = m_quant_tbl_ptrs[quant_tbl_no];
				for (int k = 0; k < 64; k++)
				{
					if (quant_table.quantval[k] != jQUANT_TBL.quantval[k])
					{
						ERREXIT(J_MESSAGE_CODE.JERR_MISMATCHED_QUANT_TABLE, quant_tbl_no);
					}
				}
			}
			if (m_saw_JFIF_marker)
			{
				if (m_JFIF_major_version == 1)
				{
					dstinfo.m_JFIF_major_version = m_JFIF_major_version;
					dstinfo.m_JFIF_minor_version = m_JFIF_minor_version;
				}
				dstinfo.m_density_unit = m_density_unit;
				dstinfo.m_X_density = m_X_density;
				dstinfo.m_Y_density = m_Y_density;
			}
		}

		public void jpeg_abort_decompress()
		{
			jpeg_abort();
		}

		public void jpeg_set_marker_processor(int marker_code, jpeg_marker_parser_method routine)
		{
			m_marker.jpeg_set_marker_processor(marker_code, routine);
		}

		public void jpeg_save_markers(int marker_code, int length_limit)
		{
			m_marker.jpeg_save_markers(marker_code, length_limit);
		}

		internal bool use_merged_upsample()
		{
			if (m_do_fancy_upsampling || m_CCIR601_sampling)
			{
				return false;
			}
			if (m_jpeg_color_space != J_COLOR_SPACE.JCS_YCbCr || m_num_components != 3 || m_out_color_space != J_COLOR_SPACE.JCS_RGB || m_out_color_components != 3)
			{
				return false;
			}
			if (m_comp_info[0].H_samp_factor != 2 || m_comp_info[1].H_samp_factor != 1 || m_comp_info[2].H_samp_factor != 1 || m_comp_info[0].V_samp_factor > 2 || m_comp_info[1].V_samp_factor != 1 || m_comp_info[2].V_samp_factor != 1)
			{
				return false;
			}
			if (m_comp_info[0].DCT_scaled_size != m_min_DCT_scaled_size || m_comp_info[1].DCT_scaled_size != m_min_DCT_scaled_size || m_comp_info[2].DCT_scaled_size != m_min_DCT_scaled_size)
			{
				return false;
			}
			return true;
		}

		private void initialize()
		{
			m_progress = null;
			m_src = null;
			for (int i = 0; i < 4; i++)
			{
				m_quant_tbl_ptrs[i] = null;
			}
			for (int j = 0; j < 4; j++)
			{
				m_dc_huff_tbl_ptrs[j] = null;
				m_ac_huff_tbl_ptrs[j] = null;
			}
			m_marker_list = new List<jpeg_marker_struct>();
			m_marker = new jpeg_marker_reader(this);
			m_inputctl = new jpeg_input_controller(this);
			m_global_state = JpegState.DSTATE_START;
		}

		private void transdecode_master_selection()
		{
			m_buffered_image = true;
			if (m_progressive_mode)
			{
				m_entropy = new phuff_entropy_decoder(this);
			}
			else
			{
				m_entropy = new huff_entropy_decoder(this);
			}
			m_coef = new jpeg_d_coef_controller(this, true);
			m_inputctl.start_input_pass();
			if (m_progress != null)
			{
				int num = 1;
				if (m_progressive_mode)
				{
					num = 2 + 3 * m_num_components;
				}
				else if (m_inputctl.HasMultipleScans())
				{
					num = m_num_components;
				}
				m_progress.Pass_counter = 0;
				m_progress.Pass_limit = m_total_iMCU_rows * num;
				m_progress.Completed_passes = 0;
				m_progress.Total_passes = 1;
			}
		}

		private bool output_pass_setup()
		{
			if (m_global_state != JpegState.DSTATE_PRESCAN)
			{
				m_master.prepare_for_output_pass();
				m_output_scanline = 0;
				m_global_state = JpegState.DSTATE_PRESCAN;
			}
			while (m_master.IsDummyPass())
			{
				while (m_output_scanline < m_output_height)
				{
					if (m_progress != null)
					{
						m_progress.Pass_counter = m_output_scanline;
						m_progress.Pass_limit = m_output_height;
						m_progress.Updated();
					}
					int output_scanline = m_output_scanline;
					m_main.process_data(null, ref m_output_scanline, 0);
					if (m_output_scanline == output_scanline)
					{
						return false;
					}
				}
				m_master.finish_output_pass();
				m_master.prepare_for_output_pass();
				m_output_scanline = 0;
			}
			m_global_state = (m_raw_data_out ? JpegState.DSTATE_RAW_OK : JpegState.DSTATE_SCANNING);
			return true;
		}

		private void default_decompress_parms()
		{
			switch (m_num_components)
			{
			case 1:
				m_jpeg_color_space = J_COLOR_SPACE.JCS_GRAYSCALE;
				m_out_color_space = J_COLOR_SPACE.JCS_GRAYSCALE;
				break;
			case 3:
				if (m_saw_JFIF_marker)
				{
					m_jpeg_color_space = J_COLOR_SPACE.JCS_YCbCr;
				}
				else if (m_saw_Adobe_marker)
				{
					switch (m_Adobe_transform)
					{
					case 0:
						m_jpeg_color_space = J_COLOR_SPACE.JCS_RGB;
						break;
					case 1:
						m_jpeg_color_space = J_COLOR_SPACE.JCS_YCbCr;
						break;
					default:
						WARNMS(J_MESSAGE_CODE.JWRN_ADOBE_XFORM, m_Adobe_transform);
						m_jpeg_color_space = J_COLOR_SPACE.JCS_YCbCr;
						break;
					}
				}
				else
				{
					int component_id = m_comp_info[0].Component_id;
					int component_id2 = m_comp_info[1].Component_id;
					int component_id3 = m_comp_info[2].Component_id;
					if (component_id == 1 && component_id2 == 2 && component_id3 == 3)
					{
						m_jpeg_color_space = J_COLOR_SPACE.JCS_YCbCr;
					}
					else if (component_id == 82 && component_id2 == 71 && component_id3 == 66)
					{
						m_jpeg_color_space = J_COLOR_SPACE.JCS_RGB;
					}
					else
					{
						TRACEMS(1, J_MESSAGE_CODE.JTRC_UNKNOWN_IDS, component_id, component_id2, component_id3);
						m_jpeg_color_space = J_COLOR_SPACE.JCS_YCbCr;
					}
				}
				m_out_color_space = J_COLOR_SPACE.JCS_RGB;
				break;
			case 4:
				if (m_saw_Adobe_marker)
				{
					switch (m_Adobe_transform)
					{
					case 0:
						m_jpeg_color_space = J_COLOR_SPACE.JCS_CMYK;
						break;
					case 2:
						m_jpeg_color_space = J_COLOR_SPACE.JCS_YCCK;
						break;
					default:
						WARNMS(J_MESSAGE_CODE.JWRN_ADOBE_XFORM, m_Adobe_transform);
						m_jpeg_color_space = J_COLOR_SPACE.JCS_YCCK;
						break;
					}
				}
				else
				{
					m_jpeg_color_space = J_COLOR_SPACE.JCS_CMYK;
				}
				m_out_color_space = J_COLOR_SPACE.JCS_CMYK;
				break;
			default:
				m_jpeg_color_space = J_COLOR_SPACE.JCS_UNKNOWN;
				m_out_color_space = J_COLOR_SPACE.JCS_UNKNOWN;
				break;
			}
			m_scale_num = 1;
			m_scale_denom = 1;
			m_buffered_image = false;
			m_raw_data_out = false;
			m_dct_method = JpegConstants.JDCT_DEFAULT;
			m_do_fancy_upsampling = true;
			m_do_block_smoothing = true;
			m_quantize_colors = false;
			m_dither_mode = J_DITHER_MODE.JDITHER_FS;
			m_two_pass_quantize = true;
			m_desired_number_of_colors = 256;
			m_colormap = null;
			m_enable_1pass_quant = false;
			m_enable_external_quant = false;
			m_enable_2pass_quant = false;
		}

		private void jpeg_consume_input_start()
		{
			m_inputctl.reset_input_controller();
			m_src.init_source();
			m_global_state = JpegState.DSTATE_INHEADER;
		}

		private ReadResult jpeg_consume_input_inHeader()
		{
			ReadResult readResult = m_inputctl.consume_input();
			if (readResult == ReadResult.JPEG_REACHED_SOS)
			{
				default_decompress_parms();
				m_global_state = JpegState.DSTATE_READY;
			}
			return readResult;
		}




	}
}
