using System;
using System.IO;
using BitMiracle.LibJpeg.Classic.Internal;
namespace BitMiracle.LibJpeg.Classic
{
	internal class jpeg_compress_struct : jpeg_common_struct
	{
		private static int[] std_luminance_quant_tbl = new int[64]
		{
			16, 11, 10, 16, 24, 40, 51, 61, 12, 12,
			14, 19, 26, 58, 60, 55, 14, 13, 16, 24,
			40, 57, 69, 56, 14, 17, 22, 29, 51, 87,
			80, 62, 18, 22, 37, 56, 68, 109, 103, 77,
			24, 35, 55, 64, 81, 104, 113, 92, 49, 64,
			78, 87, 103, 121, 120, 101, 72, 92, 95, 98,
			112, 100, 103, 99
		};

		private static int[] std_chrominance_quant_tbl = new int[64]
		{
			17, 18, 24, 47, 99, 99, 99, 99, 18, 21,
			26, 66, 99, 99, 99, 99, 24, 26, 56, 99,
			99, 99, 99, 99, 47, 66, 99, 99, 99, 99,
			99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
			99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
			99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
			99, 99, 99, 99
		};

		private static byte[] bits_dc_luminance = new byte[17]
		{
			0, 0, 1, 5, 1, 1, 1, 1, 1, 1,
			0, 0, 0, 0, 0, 0, 0
		};

		private static byte[] val_dc_luminance = new byte[12]
		{
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
			10, 11
		};

		private static byte[] bits_dc_chrominance = new byte[17]
		{
			0, 0, 3, 1, 1, 1, 1, 1, 1, 1,
			1, 1, 0, 0, 0, 0, 0
		};

		private static byte[] val_dc_chrominance = new byte[12]
		{
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
			10, 11
		};

		private static byte[] bits_ac_luminance = new byte[17]
		{
			0, 0, 2, 1, 3, 3, 2, 4, 3, 5,
			5, 4, 4, 0, 0, 1, 125
		};

		private static byte[] val_ac_luminance = new byte[162]
		{
			1, 2, 3, 0, 4, 17, 5, 18, 33, 49,
			65, 6, 19, 81, 97, 7, 34, 113, 20, 50,
			129, 145, 161, 8, 35, 66, 177, 193, 21, 82,
			209, 240, 36, 51, 98, 114, 130, 9, 10, 22,
			23, 24, 25, 26, 37, 38, 39, 40, 41, 42,
			52, 53, 54, 55, 56, 57, 58, 67, 68, 69,
			70, 71, 72, 73, 74, 83, 84, 85, 86, 87,
			88, 89, 90, 99, 100, 101, 102, 103, 104, 105,
			106, 115, 116, 117, 118, 119, 120, 121, 122, 131,
			132, 133, 134, 135, 136, 137, 138, 146, 147, 148,
			149, 150, 151, 152, 153, 154, 162, 163, 164, 165,
			166, 167, 168, 169, 170, 178, 179, 180, 181, 182,
			183, 184, 185, 186, 194, 195, 196, 197, 198, 199,
			200, 201, 202, 210, 211, 212, 213, 214, 215, 216,
			217, 218, 225, 226, 227, 228, 229, 230, 231, 232,
			233, 234, 241, 242, 243, 244, 245, 246, 247, 248,
			249, 250
		};

		private static byte[] bits_ac_chrominance = new byte[17]
		{
			0, 0, 2, 1, 2, 4, 4, 3, 4, 7,
			5, 4, 4, 0, 1, 2, 119
		};

		private static byte[] val_ac_chrominance = new byte[162]
		{
			0, 1, 2, 3, 17, 4, 5, 33, 49, 6,
			18, 65, 81, 7, 97, 113, 19, 34, 50, 129,
			8, 20, 66, 145, 161, 177, 193, 9, 35, 51,
			82, 240, 21, 98, 114, 209, 10, 22, 36, 52,
			225, 37, 241, 23, 24, 25, 26, 38, 39, 40,
			41, 42, 53, 54, 55, 56, 57, 58, 67, 68,
			69, 70, 71, 72, 73, 74, 83, 84, 85, 86,
			87, 88, 89, 90, 99, 100, 101, 102, 103, 104,
			105, 106, 115, 116, 117, 118, 119, 120, 121, 122,
			130, 131, 132, 133, 134, 135, 136, 137, 138, 146,
			147, 148, 149, 150, 151, 152, 153, 154, 162, 163,
			164, 165, 166, 167, 168, 169, 170, 178, 179, 180,
			181, 182, 183, 184, 185, 186, 194, 195, 196, 197,
			198, 199, 200, 201, 202, 210, 211, 212, 213, 214,
			215, 216, 217, 218, 226, 227, 228, 229, 230, 231,
			232, 233, 234, 242, 243, 244, 245, 246, 247, 248,
			249, 250
		};

		internal jpeg_destination_mgr m_dest;

		internal int m_image_width;

		internal int m_image_height;

		internal int m_input_components;

		internal J_COLOR_SPACE m_in_color_space;

		internal int m_data_precision;

		internal int m_num_components;

		internal J_COLOR_SPACE m_jpeg_color_space;

		private jpeg_component_info[] m_comp_info;

		internal JQUANT_TBL[] m_quant_tbl_ptrs;

		internal JHUFF_TBL[] m_dc_huff_tbl_ptrs;

		internal JHUFF_TBL[] m_ac_huff_tbl_ptrs;

		internal int m_num_scans;

		internal jpeg_scan_info[] m_scan_info;

		internal bool m_raw_data_in;

		internal bool m_optimize_coding;

		internal bool m_CCIR601_sampling;

		internal int m_smoothing_factor;

		internal J_DCT_METHOD m_dct_method;

		internal int m_restart_interval;

		internal int m_restart_in_rows;

		internal bool m_write_JFIF_header;

		internal byte m_JFIF_major_version;

		internal byte m_JFIF_minor_version;

		internal DensityUnit m_density_unit;

		internal short m_X_density;

		internal short m_Y_density;

		internal bool m_write_Adobe_marker;

		internal int m_next_scanline;

		internal bool m_progressive_mode;

		internal int m_max_h_samp_factor;

		internal int m_max_v_samp_factor;

		internal int m_total_iMCU_rows;

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

		internal jpeg_comp_master m_master;

		internal jpeg_c_main_controller m_main;

		internal jpeg_c_prep_controller m_prep;

		internal jpeg_c_coef_controller m_coef;

		internal jpeg_marker_writer m_marker;

		internal jpeg_color_converter m_cconvert;

		internal jpeg_downsampler m_downsample;

		internal jpeg_forward_dct m_fdct;

		internal jpeg_entropy_encoder m_entropy;

		internal jpeg_scan_info[] m_script_space;

		internal int m_script_space_size;

		public override bool IsDecompressor
		{
			get
			{
				return false;
			}
		}

		public jpeg_destination_mgr Dest
		{
			get
			{
				return m_dest;
			}
			set
			{
				m_dest = value;
			}
		}

		public int Image_width
		{
			get
			{
				return m_image_width;
			}
			set
			{
				m_image_width = value;
			}
		}

		public int Image_height
		{
			get
			{
				return m_image_height;
			}
			set
			{
				m_image_height = value;
			}
		}

		public int Input_components
		{
			get
			{
				return m_input_components;
			}
			set
			{
				m_input_components = value;
			}
		}

		public J_COLOR_SPACE In_color_space
		{
			get
			{
				return m_in_color_space;
			}
			set
			{
				m_in_color_space = value;
			}
		}

		public int Data_precision
		{
			get
			{
				return m_data_precision;
			}
			set
			{
				m_data_precision = value;
			}
		}

		public int Num_components
		{
			get
			{
				return m_num_components;
			}
			set
			{
				m_num_components = value;
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

		public bool Raw_data_in
		{
			get
			{
				return m_raw_data_in;
			}
			set
			{
				m_raw_data_in = value;
			}
		}

		public bool Optimize_coding
		{
			get
			{
				return m_optimize_coding;
			}
			set
			{
				m_optimize_coding = value;
			}
		}

		public bool CCIR601_sampling
		{
			get
			{
				return m_CCIR601_sampling;
			}
			set
			{
				m_CCIR601_sampling = value;
			}
		}

		public int Smoothing_factor
		{
			get
			{
				return m_smoothing_factor;
			}
			set
			{
				m_smoothing_factor = value;
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

		public int Restart_interval
		{
			get
			{
				return m_restart_interval;
			}
			set
			{
				m_restart_interval = value;
			}
		}

		public int Restart_in_rows
		{
			get
			{
				return m_restart_in_rows;
			}
			set
			{
				m_restart_in_rows = value;
			}
		}

		public bool Write_JFIF_header
		{
			get
			{
				return m_write_JFIF_header;
			}
			set
			{
				m_write_JFIF_header = value;
			}
		}

		public byte JFIF_major_version
		{
			get
			{
				return m_JFIF_major_version;
			}
			set
			{
				m_JFIF_major_version = value;
			}
		}

		public byte JFIF_minor_version
		{
			get
			{
				return m_JFIF_minor_version;
			}
			set
			{
				m_JFIF_minor_version = value;
			}
		}

		public DensityUnit Density_unit
		{
			get
			{
				return m_density_unit;
			}
			set
			{
				m_density_unit = value;
			}
		}

		public short X_density
		{
			get
			{
				return m_X_density;
			}
			set
			{
				m_X_density = value;
			}
		}

		public short Y_density
		{
			get
			{
				return m_Y_density;
			}
			set
			{
				m_Y_density = value;
			}
		}

		public bool Write_Adobe_marker
		{
			get
			{
				return m_write_Adobe_marker;
			}
			set
			{
				m_write_Adobe_marker = value;
			}
		}

		public int Max_v_samp_factor
		{
			get
			{
				return m_max_v_samp_factor;
			}
		}

		public jpeg_component_info[] Component_info
		{
			get
			{
				return m_comp_info;
			}
		}

		public JQUANT_TBL[] Quant_tbl_ptrs
		{
			get
			{
				return m_quant_tbl_ptrs;
			}
		}

		public JHUFF_TBL[] Dc_huff_tbl_ptrs
		{
			get
			{
				return m_dc_huff_tbl_ptrs;
			}
		}

		public JHUFF_TBL[] Ac_huff_tbl_ptrs
		{
			get
			{
				return m_ac_huff_tbl_ptrs;
			}
		}

		public int Next_scanline
		{
			get
			{
				return m_next_scanline;
			}
		}

		public jpeg_compress_struct()
		{
			m_quant_tbl_ptrs = new JQUANT_TBL[4];
			m_dc_huff_tbl_ptrs = new JHUFF_TBL[4];
			m_ac_huff_tbl_ptrs = new JHUFF_TBL[4];
			m_cur_comp_info = new int[4];
			m_MCU_membership = new int[10];

			initialize();
		}

		public jpeg_compress_struct(jpeg_error_mgr errorManager) : base(errorManager)
		{
			m_quant_tbl_ptrs = new JQUANT_TBL[4];
			m_dc_huff_tbl_ptrs = new JHUFF_TBL[4];
			m_ac_huff_tbl_ptrs = new JHUFF_TBL[4];
			m_cur_comp_info = new int[4];
			m_MCU_membership = new int[10];
			
			initialize();
		}

		public void jpeg_abort_compress()
		{
			jpeg_abort();
		}

		public void jpeg_suppress_tables(bool suppress)
		{
			for (int i = 0; i < 4; i++)
			{
				if (m_quant_tbl_ptrs[i] != null)
				{
					m_quant_tbl_ptrs[i].Sent_table = suppress;
				}
			}
			for (int j = 0; j < 4; j++)
			{
				if (m_dc_huff_tbl_ptrs[j] != null)
				{
					m_dc_huff_tbl_ptrs[j].Sent_table = suppress;
				}
				if (m_ac_huff_tbl_ptrs[j] != null)
				{
					m_ac_huff_tbl_ptrs[j].Sent_table = suppress;
				}
			}
		}

		public void jpeg_finish_compress()
		{
			if (m_global_state == JpegState.CSTATE_SCANNING || m_global_state == JpegState.CSTATE_RAW_OK)
			{
				if (m_next_scanline < m_image_height)
				{
					ERREXIT(J_MESSAGE_CODE.JERR_TOO_LITTLE_DATA);
				}
				m_master.finish_pass();
			}
			else if (m_global_state != JpegState.CSTATE_WRCOEFS)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			while (!m_master.IsLastPass())
			{
				m_master.prepare_for_pass();
				for (int i = 0; i < m_total_iMCU_rows; i++)
				{
					if (m_progress != null)
					{
						m_progress.Pass_counter = i;
						m_progress.Pass_limit = m_total_iMCU_rows;
						m_progress.Updated();
					}
					if (!m_coef.compress_data(null))
					{
						ERREXIT(J_MESSAGE_CODE.JERR_CANT_SUSPEND);
					}
				}
				m_master.finish_pass();
			}
			m_marker.write_file_trailer();
			m_dest.term_destination();
			jpeg_abort();
		}

		public void jpeg_write_marker(int marker, byte[] data)
		{
			if (m_next_scanline != 0 || (m_global_state != JpegState.CSTATE_SCANNING && m_global_state != JpegState.CSTATE_RAW_OK && m_global_state != JpegState.CSTATE_WRCOEFS))
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			m_marker.write_marker_header(marker, data.Length);
			for (int i = 0; i < data.Length; i++)
			{
				m_marker.write_marker_byte(data[i]);
			}
		}

		public void jpeg_write_m_header(int marker, int datalen)
		{
			if (m_next_scanline != 0 || (m_global_state != JpegState.CSTATE_SCANNING && m_global_state != JpegState.CSTATE_RAW_OK && m_global_state != JpegState.CSTATE_WRCOEFS))
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			m_marker.write_marker_header(marker, datalen);
		}

		public void jpeg_write_m_byte(byte val)
		{
			m_marker.write_marker_byte(val);
		}

		public void jpeg_write_tables()
		{
			if (m_global_state != JpegState.CSTATE_START)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			m_err.reset_error_mgr();
			m_dest.init_destination();
			m_marker = new jpeg_marker_writer(this);
			m_marker.write_tables_only();
			m_dest.term_destination();
		}

		public void jpeg_stdio_dest(Stream outfile)
		{
			m_dest = new my_destination_mgr(this, outfile);
		}

		public void jpeg_set_defaults()
		{
			if (m_global_state != JpegState.CSTATE_START)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			if (m_comp_info == null)
			{
				m_comp_info = jpeg_component_info.createArrayOfComponents(10);
			}
			m_data_precision = 8;
			jpeg_set_quality(75, true);
			std_huff_tables();
			m_scan_info = null;
			m_num_scans = 0;
			m_raw_data_in = false;
			m_optimize_coding = false;
			if (m_data_precision > 8)
			{
				m_optimize_coding = true;
			}
			m_CCIR601_sampling = false;
			m_smoothing_factor = 0;
			m_dct_method = JpegConstants.JDCT_DEFAULT;
			m_restart_interval = 0;
			m_restart_in_rows = 0;
			m_JFIF_major_version = 1;
			m_JFIF_minor_version = 1;
			m_density_unit = DensityUnit.Unknown;
			m_X_density = 1;
			m_Y_density = 1;
			jpeg_default_colorspace();
		}

		public void jpeg_set_colorspace(J_COLOR_SPACE colorspace)
		{
			if (m_global_state != JpegState.CSTATE_START)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			m_jpeg_color_space = colorspace;
			m_write_JFIF_header = false;
			m_write_Adobe_marker = false;
			switch (colorspace)
			{
			case J_COLOR_SPACE.JCS_GRAYSCALE:
				m_write_JFIF_header = true;
				m_num_components = 1;
				jpeg_set_colorspace_SET_COMP(0, 1, 1, 1, 0, 0, 0);
				break;
			case J_COLOR_SPACE.JCS_RGB:
				m_write_Adobe_marker = true;
				m_num_components = 3;
				jpeg_set_colorspace_SET_COMP(0, 82, 1, 1, 0, 0, 0);
				jpeg_set_colorspace_SET_COMP(1, 71, 1, 1, 0, 0, 0);
				jpeg_set_colorspace_SET_COMP(2, 66, 1, 1, 0, 0, 0);
				break;
			case J_COLOR_SPACE.JCS_YCbCr:
				m_write_JFIF_header = true;
				m_num_components = 3;
				jpeg_set_colorspace_SET_COMP(0, 1, 2, 2, 0, 0, 0);
				jpeg_set_colorspace_SET_COMP(1, 2, 1, 1, 1, 1, 1);
				jpeg_set_colorspace_SET_COMP(2, 3, 1, 1, 1, 1, 1);
				break;
			case J_COLOR_SPACE.JCS_CMYK:
				m_write_Adobe_marker = true;
				m_num_components = 4;
				jpeg_set_colorspace_SET_COMP(0, 67, 1, 1, 0, 0, 0);
				jpeg_set_colorspace_SET_COMP(1, 77, 1, 1, 0, 0, 0);
				jpeg_set_colorspace_SET_COMP(2, 89, 1, 1, 0, 0, 0);
				jpeg_set_colorspace_SET_COMP(3, 75, 1, 1, 0, 0, 0);
				break;
			case J_COLOR_SPACE.JCS_YCCK:
				m_write_Adobe_marker = true;
				m_num_components = 4;
				jpeg_set_colorspace_SET_COMP(0, 1, 2, 2, 0, 0, 0);
				jpeg_set_colorspace_SET_COMP(1, 2, 1, 1, 1, 1, 1);
				jpeg_set_colorspace_SET_COMP(2, 3, 1, 1, 1, 1, 1);
				jpeg_set_colorspace_SET_COMP(3, 4, 2, 2, 0, 0, 0);
				break;
			case J_COLOR_SPACE.JCS_UNKNOWN:
			{
				m_num_components = m_input_components;
				if (m_num_components < 1 || m_num_components > 10)
				{
					ERREXIT(J_MESSAGE_CODE.JERR_COMPONENT_COUNT, m_num_components, 10);
				}
				for (int i = 0; i < m_num_components; i++)
				{
					jpeg_set_colorspace_SET_COMP(i, i, 1, 1, 0, 0, 0);
				}
				break;
			}
			default:
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
				break;
			}
		}

		public void jpeg_default_colorspace()
		{
			switch (m_in_color_space)
			{
			case J_COLOR_SPACE.JCS_GRAYSCALE:
				jpeg_set_colorspace(J_COLOR_SPACE.JCS_GRAYSCALE);
				break;
			case J_COLOR_SPACE.JCS_RGB:
				jpeg_set_colorspace(J_COLOR_SPACE.JCS_YCbCr);
				break;
			case J_COLOR_SPACE.JCS_YCbCr:
				jpeg_set_colorspace(J_COLOR_SPACE.JCS_YCbCr);
				break;
			case J_COLOR_SPACE.JCS_CMYK:
				jpeg_set_colorspace(J_COLOR_SPACE.JCS_CMYK);
				break;
			case J_COLOR_SPACE.JCS_YCCK:
				jpeg_set_colorspace(J_COLOR_SPACE.JCS_YCCK);
				break;
			case J_COLOR_SPACE.JCS_UNKNOWN:
				jpeg_set_colorspace(J_COLOR_SPACE.JCS_UNKNOWN);
				break;
			default:
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_IN_COLORSPACE);
				break;
			}
		}

		public void jpeg_set_quality(int quality, bool force_baseline)
		{
			quality = jpeg_quality_scaling(quality);
			jpeg_set_linear_quality(quality, force_baseline);
		}

		public void jpeg_set_linear_quality(int scale_factor, bool force_baseline)
		{
			jpeg_add_quant_table(0, std_luminance_quant_tbl, scale_factor, force_baseline);
			jpeg_add_quant_table(1, std_chrominance_quant_tbl, scale_factor, force_baseline);
		}

		public void jpeg_add_quant_table(int which_tbl, int[] basic_table, int scale_factor, bool force_baseline)
		{
			if (m_global_state != JpegState.CSTATE_START)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			if (which_tbl < 0 || which_tbl >= 4)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_DQT_INDEX, which_tbl);
			}
			if (m_quant_tbl_ptrs[which_tbl] == null)
			{
				m_quant_tbl_ptrs[which_tbl] = new JQUANT_TBL();
			}
			for (int i = 0; i < 64; i++)
			{
				int num = (basic_table[i] * scale_factor + 50) / 100;
				if (num <= 0)
				{
					num = 1;
				}
				if (num > 32767)
				{
					num = 32767;
				}
				if (force_baseline && num > 255)
				{
					num = 255;
				}
				m_quant_tbl_ptrs[which_tbl].quantval[i] = (short)num;
			}
			m_quant_tbl_ptrs[which_tbl].Sent_table = false;
		}

		public static int jpeg_quality_scaling(int quality)
		{
			if (quality <= 0)
			{
				quality = 1;
			}
			if (quality > 100)
			{
				quality = 100;
			}
			quality = ((quality >= 50) ? (200 - quality * 2) : (5000 / quality));
			return quality;
		}

		public void jpeg_simple_progression()
		{
			if (m_global_state != JpegState.CSTATE_START)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			int num = ((m_num_components == 3 && m_jpeg_color_space == J_COLOR_SPACE.JCS_YCbCr) ? 10 : ((m_num_components <= 4) ? (2 + 4 * m_num_components) : (6 * m_num_components)));
			if (m_script_space == null || m_script_space_size < num)
			{
				m_script_space_size = Math.Max(num, 10);
				m_script_space = new jpeg_scan_info[m_script_space_size];
				for (int i = 0; i < m_script_space_size; i++)
				{
					m_script_space[i] = new jpeg_scan_info();
				}
			}
			m_scan_info = m_script_space;
			m_num_scans = num;
			int scanIndex = 0;
			if (m_num_components == 3 && m_jpeg_color_space == J_COLOR_SPACE.JCS_YCbCr)
			{
				fill_dc_scans(ref scanIndex, m_num_components, 0, 1);
				fill_a_scan(ref scanIndex, 0, 1, 5, 0, 2);
				fill_a_scan(ref scanIndex, 2, 1, 63, 0, 1);
				fill_a_scan(ref scanIndex, 1, 1, 63, 0, 1);
				fill_a_scan(ref scanIndex, 0, 6, 63, 0, 2);
				fill_a_scan(ref scanIndex, 0, 1, 63, 2, 1);
				fill_dc_scans(ref scanIndex, m_num_components, 1, 0);
				fill_a_scan(ref scanIndex, 2, 1, 63, 1, 0);
				fill_a_scan(ref scanIndex, 1, 1, 63, 1, 0);
				fill_a_scan(ref scanIndex, 0, 1, 63, 1, 0);
			}
			else
			{
				fill_dc_scans(ref scanIndex, m_num_components, 0, 1);
				fill_scans(ref scanIndex, m_num_components, 1, 5, 0, 2);
				fill_scans(ref scanIndex, m_num_components, 6, 63, 0, 2);
				fill_scans(ref scanIndex, m_num_components, 1, 63, 2, 1);
				fill_dc_scans(ref scanIndex, m_num_components, 1, 0);
				fill_scans(ref scanIndex, m_num_components, 1, 63, 1, 0);
			}
		}

		public void jpeg_start_compress(bool write_all_tables)
		{
			if (m_global_state != JpegState.CSTATE_START)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			if (write_all_tables)
			{
				jpeg_suppress_tables(false);
			}
			m_err.reset_error_mgr();
			m_dest.init_destination();
			jinit_compress_master();
			m_master.prepare_for_pass();
			m_next_scanline = 0;
			m_global_state = (m_raw_data_in ? JpegState.CSTATE_RAW_OK : JpegState.CSTATE_SCANNING);
		}

		public int jpeg_write_scanlines(byte[][] scanlines, int num_lines)
		{
			if (m_global_state != JpegState.CSTATE_SCANNING)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			if (m_next_scanline >= m_image_height)
			{
				WARNMS(J_MESSAGE_CODE.JWRN_TOO_MUCH_DATA);
			}
			if (m_progress != null)
			{
				m_progress.Pass_counter = m_next_scanline;
				m_progress.Pass_limit = m_image_height;
				m_progress.Updated();
			}
			if (m_master.MustCallPassStartup())
			{
				m_master.pass_startup();
			}
			int num = m_image_height - m_next_scanline;
			if (num_lines > num)
			{
				num_lines = num;
			}
			int in_row_ctr = 0;
			m_main.process_data(scanlines, ref in_row_ctr, num_lines);
			m_next_scanline += in_row_ctr;
			return in_row_ctr;
		}

		public int jpeg_write_raw_data(byte[][][] data, int num_lines)
		{
			if (m_global_state != JpegState.CSTATE_RAW_OK)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			if (m_next_scanline >= m_image_height)
			{
				WARNMS(J_MESSAGE_CODE.JWRN_TOO_MUCH_DATA);
				return 0;
			}
			if (m_progress != null)
			{
				m_progress.Pass_counter = m_next_scanline;
				m_progress.Pass_limit = m_image_height;
				m_progress.Updated();
			}
			if (m_master.MustCallPassStartup())
			{
				m_master.pass_startup();
			}
			int num = m_max_v_samp_factor * 8;
			if (num_lines < num)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BUFFER_SIZE);
			}
			if (!m_coef.compress_data(data))
			{
				return 0;
			}
			m_next_scanline += num;
			return num;
		}

		public void jpeg_write_coefficients(jvirt_array<JBLOCK>[] coef_arrays)
		{
			if (m_global_state != JpegState.CSTATE_START)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_STATE, (int)m_global_state);
			}
			jpeg_suppress_tables(false);
			m_err.reset_error_mgr();
			m_dest.init_destination();
			transencode_master_selection(coef_arrays);
			m_next_scanline = 0;
			m_global_state = JpegState.CSTATE_WRCOEFS;
		}

		private void initialize()
		{
			m_progress = null;
			m_dest = null;
			m_comp_info = null;
			for (int i = 0; i < 4; i++)
			{
				m_quant_tbl_ptrs[i] = null;
			}
			for (int j = 0; j < 4; j++)
			{
				m_dc_huff_tbl_ptrs[j] = null;
				m_ac_huff_tbl_ptrs[j] = null;
			}
			m_script_space = null;
			m_global_state = JpegState.CSTATE_START;
		}

		private void jinit_compress_master()
		{
			jinit_c_master_control(false);
			if (!m_raw_data_in)
			{
				m_cconvert = new jpeg_color_converter(this);
				m_downsample = new jpeg_downsampler(this);
				m_prep = new jpeg_c_prep_controller(this);
			}
			m_fdct = new jpeg_forward_dct(this);
			if (m_progressive_mode)
			{
				m_entropy = new phuff_entropy_encoder(this);
			}
			else
			{
				m_entropy = new huff_entropy_encoder(this);
			}
			m_coef = new my_c_coef_controller(this, m_num_scans > 1 || m_optimize_coding);
			jinit_c_main_controller(false);
			m_marker = new jpeg_marker_writer(this);
			m_marker.write_file_header();
		}

		private void jinit_c_master_control(bool transcode_only)
		{
			initial_setup();
			if (m_scan_info != null)
			{
				validate_script();
			}
			else
			{
				m_progressive_mode = false;
				m_num_scans = 1;
			}
			if (m_progressive_mode)
			{
				m_optimize_coding = true;
			}
			m_master = new jpeg_comp_master(this, transcode_only);
		}

		private void jinit_c_main_controller(bool need_full_buffer)
		{
			if (!m_raw_data_in)
			{
				if (need_full_buffer)
				{
					ERREXIT(J_MESSAGE_CODE.JERR_BAD_BUFFER_MODE);
				}
				else
				{
					m_main = new jpeg_c_main_controller(this);
				}
			}
		}

		private void transencode_master_selection(jvirt_array<JBLOCK>[] coef_arrays)
		{
			m_input_components = 1;
			jinit_c_master_control(true);
			if (m_progressive_mode)
			{
				m_entropy = new phuff_entropy_encoder(this);
			}
			else
			{
				m_entropy = new huff_entropy_encoder(this);
			}
			m_coef = new my_trans_c_coef_controller(this, coef_arrays);
			m_marker = new jpeg_marker_writer(this);
			m_marker.write_file_header();
		}

		private void initial_setup()
		{
			if (m_image_height <= 0 || m_image_width <= 0 || m_num_components <= 0 || m_input_components <= 0)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_EMPTY_IMAGE);
			}
			if (m_image_height > 65500 || m_image_width > 65500)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_IMAGE_TOO_BIG, 65500);
			}
			long num = m_image_width * m_input_components;
			int num2 = (int)num;
			if (num2 != num)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_WIDTH_OVERFLOW);
			}
			if (m_data_precision != 8)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_PRECISION, m_data_precision);
			}
			if (m_num_components > 10)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_COMPONENT_COUNT, m_num_components, 10);
			}
			m_max_h_samp_factor = 1;
			m_max_v_samp_factor = 1;
			for (int i = 0; i < m_num_components; i++)
			{
				if (m_comp_info[i].H_samp_factor <= 0 || m_comp_info[i].H_samp_factor > 4 || m_comp_info[i].V_samp_factor <= 0 || m_comp_info[i].V_samp_factor > 4)
				{
					ERREXIT(J_MESSAGE_CODE.JERR_BAD_SAMPLING);
				}
				m_max_h_samp_factor = Math.Max(m_max_h_samp_factor, m_comp_info[i].H_samp_factor);
				m_max_v_samp_factor = Math.Max(m_max_v_samp_factor, m_comp_info[i].V_samp_factor);
			}
			for (int j = 0; j < m_num_components; j++)
			{
				m_comp_info[j].Component_index = j;
				m_comp_info[j].DCT_scaled_size = 8;
				m_comp_info[j].Width_in_blocks = JpegUtils.jdiv_round_up(m_image_width * m_comp_info[j].H_samp_factor, m_max_h_samp_factor * 8);
				m_comp_info[j].height_in_blocks = JpegUtils.jdiv_round_up(m_image_height * m_comp_info[j].V_samp_factor, m_max_v_samp_factor * 8);
				m_comp_info[j].downsampled_width = JpegUtils.jdiv_round_up(m_image_width * m_comp_info[j].H_samp_factor, m_max_h_samp_factor);
				m_comp_info[j].downsampled_height = JpegUtils.jdiv_round_up(m_image_height * m_comp_info[j].V_samp_factor, m_max_v_samp_factor);
				m_comp_info[j].component_needed = true;
			}
			m_total_iMCU_rows = JpegUtils.jdiv_round_up(m_image_height, m_max_v_samp_factor * 8);
		}

		private void validate_script()
		{
			if (m_num_scans <= 0)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_SCAN_SCRIPT, 0);
			}
			int[][] array = new int[10][];
			for (int i = 0; i < 10; i++)
			{
				array[i] = new int[64];
			}
			bool[] array2 = new bool[10];
			if (m_scan_info[0].Ss != 0 || m_scan_info[0].Se != 63)
			{
				m_progressive_mode = true;
				for (int j = 0; j < m_num_components; j++)
				{
					for (int k = 0; k < 64; k++)
					{
						array[j][k] = -1;
					}
				}
			}
			else
			{
				m_progressive_mode = false;
				for (int l = 0; l < m_num_components; l++)
				{
					array2[l] = false;
				}
			}
			for (int m = 1; m <= m_num_scans; m++)
			{
				jpeg_scan_info jpeg_scan_info = m_scan_info[m - 1];
				int comps_in_scan = jpeg_scan_info.comps_in_scan;
				if (comps_in_scan <= 0 || comps_in_scan > 4)
				{
					ERREXIT(J_MESSAGE_CODE.JERR_COMPONENT_COUNT, comps_in_scan, 4);
				}
				for (int n = 0; n < comps_in_scan; n++)
				{
					int num = jpeg_scan_info.component_index[n];
					if (num < 0 || num >= m_num_components)
					{
						ERREXIT(J_MESSAGE_CODE.JERR_BAD_SCAN_SCRIPT, m);
					}
					if (n > 0 && num <= jpeg_scan_info.component_index[n - 1])
					{
						ERREXIT(J_MESSAGE_CODE.JERR_BAD_SCAN_SCRIPT, m);
					}
				}
				int ss = jpeg_scan_info.Ss;
				int se = jpeg_scan_info.Se;
				int ah = jpeg_scan_info.Ah;
				int al = jpeg_scan_info.Al;
				if (m_progressive_mode)
				{
					if (ss < 0 || ss >= 64 || se < ss || se >= 64 || ah < 0 || ah > 10 || al < 0 || al > 10)
					{
						ERREXIT(J_MESSAGE_CODE.JERR_BAD_PROG_SCRIPT, m);
					}
					if (ss == 0)
					{
						if (se != 0)
						{
							ERREXIT(J_MESSAGE_CODE.JERR_BAD_PROG_SCRIPT, m);
						}
					}
					else if (comps_in_scan != 1)
					{
						ERREXIT(J_MESSAGE_CODE.JERR_BAD_PROG_SCRIPT, m);
					}
					for (int num2 = 0; num2 < comps_in_scan; num2++)
					{
						int num3 = jpeg_scan_info.component_index[num2];
						if (ss != 0 && array[num3][0] < 0)
						{
							ERREXIT(J_MESSAGE_CODE.JERR_BAD_PROG_SCRIPT, m);
						}
						for (int num4 = ss; num4 <= se; num4++)
						{
							if (array[num3][num4] < 0)
							{
								if (ah != 0)
								{
									ERREXIT(J_MESSAGE_CODE.JERR_BAD_PROG_SCRIPT, m);
								}
							}
							else if (ah != array[num3][num4] || al != ah - 1)
							{
								ERREXIT(J_MESSAGE_CODE.JERR_BAD_PROG_SCRIPT, m);
							}
							array[num3][num4] = al;
						}
					}
					continue;
				}
				if (ss != 0 || se != 63 || ah != 0 || al != 0)
				{
					ERREXIT(J_MESSAGE_CODE.JERR_BAD_PROG_SCRIPT, m);
				}
				for (int num5 = 0; num5 < comps_in_scan; num5++)
				{
					int num6 = jpeg_scan_info.component_index[num5];
					if (array2[num6])
					{
						ERREXIT(J_MESSAGE_CODE.JERR_BAD_SCAN_SCRIPT, m);
					}
					array2[num6] = true;
				}
			}
			if (m_progressive_mode)
			{
				for (int num7 = 0; num7 < m_num_components; num7++)
				{
					if (array[num7][0] < 0)
					{
						ERREXIT(J_MESSAGE_CODE.JERR_MISSING_DATA);
					}
				}
				return;
			}
			for (int num8 = 0; num8 < m_num_components; num8++)
			{
				if (!array2[num8])
				{
					ERREXIT(J_MESSAGE_CODE.JERR_MISSING_DATA);
				}
			}
		}

		private void std_huff_tables()
		{
			add_huff_table(ref m_dc_huff_tbl_ptrs[0], bits_dc_luminance, val_dc_luminance);
			add_huff_table(ref m_ac_huff_tbl_ptrs[0], bits_ac_luminance, val_ac_luminance);
			add_huff_table(ref m_dc_huff_tbl_ptrs[1], bits_dc_chrominance, val_dc_chrominance);
			add_huff_table(ref m_ac_huff_tbl_ptrs[1], bits_ac_chrominance, val_ac_chrominance);
		}

		private void add_huff_table(ref JHUFF_TBL htblptr, byte[] bits, byte[] val)
		{
			if (htblptr == null)
			{
				htblptr = new JHUFF_TBL();
			}
			Buffer.BlockCopy(bits, 0, htblptr.Bits, 0, htblptr.Bits.Length);
			int num = 0;
			for (int i = 1; i <= 16; i++)
			{
				num += bits[i];
			}
			if (num < 1 || num > 256)
			{
				ERREXIT(J_MESSAGE_CODE.JERR_BAD_HUFF_TABLE);
			}
			Buffer.BlockCopy(val, 0, htblptr.Huffval, 0, num);
			htblptr.Sent_table = false;
		}

		private void fill_a_scan(ref int scanIndex, int ci, int Ss, int Se, int Ah, int Al)
		{
			m_script_space[scanIndex].comps_in_scan = 1;
			m_script_space[scanIndex].component_index[0] = ci;
			m_script_space[scanIndex].Ss = Ss;
			m_script_space[scanIndex].Se = Se;
			m_script_space[scanIndex].Ah = Ah;
			m_script_space[scanIndex].Al = Al;
			scanIndex++;
		}

		private void fill_dc_scans(ref int scanIndex, int ncomps, int Ah, int Al)
		{
			if (ncomps <= 4)
			{
				m_script_space[scanIndex].comps_in_scan = ncomps;
				for (int i = 0; i < ncomps; i++)
				{
					m_script_space[scanIndex].component_index[i] = i;
				}
				m_script_space[scanIndex].Ss = 0;
				m_script_space[scanIndex].Se = 0;
				m_script_space[scanIndex].Ah = Ah;
				m_script_space[scanIndex].Al = Al;
				scanIndex++;
			}
			else
			{
				fill_scans(ref scanIndex, ncomps, 0, 0, Ah, Al);
			}
		}

		private void fill_scans(ref int scanIndex, int ncomps, int Ss, int Se, int Ah, int Al)
		{
			for (int i = 0; i < ncomps; i++)
			{
				m_script_space[scanIndex].comps_in_scan = 1;
				m_script_space[scanIndex].component_index[0] = i;
				m_script_space[scanIndex].Ss = Ss;
				m_script_space[scanIndex].Se = Se;
				m_script_space[scanIndex].Ah = Ah;
				m_script_space[scanIndex].Al = Al;
				scanIndex++;
			}
		}

		private void jpeg_set_colorspace_SET_COMP(int index, int id, int hsamp, int vsamp, int quant, int dctbl, int actbl)
		{
			m_comp_info[index].Component_id = id;
			m_comp_info[index].H_samp_factor = hsamp;
			m_comp_info[index].V_samp_factor = vsamp;
			m_comp_info[index].Quant_tbl_no = quant;
			m_comp_info[index].Dc_tbl_no = dctbl;
			m_comp_info[index].Ac_tbl_no = actbl;
		}




	}
}
