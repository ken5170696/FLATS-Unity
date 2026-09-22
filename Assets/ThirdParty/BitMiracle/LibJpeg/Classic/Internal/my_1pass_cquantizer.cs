using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class my_1pass_cquantizer : jpeg_color_quantizer
	{
		private enum QuantizerType
		{
			color_quantizer3,
			color_quantizer,
			quantize3_ord_dither_quantizer,
			quantize_ord_dither_quantizer,
			quantize_fs_dither_quantizer
		}

		private const int MAX_Q_COMPS = 4;

		private const int ODITHER_SIZE = 16;

		private const int ODITHER_CELLS = 256;

		private const int ODITHER_MASK = 15;

		private static int[] RGB_order = new int[3] { 1, 0, 2 };

		private static byte[][] base_dither_matrix = new byte[16][]
		{
			new byte[16]
			{
				0, 192, 48, 240, 12, 204, 60, 252, 3, 195,
				51, 243, 15, 207, 63, 255
			},
			new byte[16]
			{
				128, 64, 176, 112, 140, 76, 188, 124, 131, 67,
				179, 115, 143, 79, 191, 127
			},
			new byte[16]
			{
				32, 224, 16, 208, 44, 236, 28, 220, 35, 227,
				19, 211, 47, 239, 31, 223
			},
			new byte[16]
			{
				160, 96, 144, 80, 172, 108, 156, 92, 163, 99,
				147, 83, 175, 111, 159, 95
			},
			new byte[16]
			{
				8, 200, 56, 248, 4, 196, 52, 244, 11, 203,
				59, 251, 7, 199, 55, 247
			},
			new byte[16]
			{
				136, 72, 184, 120, 132, 68, 180, 116, 139, 75,
				187, 123, 135, 71, 183, 119
			},
			new byte[16]
			{
				40, 232, 24, 216, 36, 228, 20, 212, 43, 235,
				27, 219, 39, 231, 23, 215
			},
			new byte[16]
			{
				168, 104, 152, 88, 164, 100, 148, 84, 171, 107,
				155, 91, 167, 103, 151, 87
			},
			new byte[16]
			{
				2, 194, 50, 242, 14, 206, 62, 254, 1, 193,
				49, 241, 13, 205, 61, 253
			},
			new byte[16]
			{
				130, 66, 178, 114, 142, 78, 190, 126, 129, 65,
				177, 113, 141, 77, 189, 125
			},
			new byte[16]
			{
				34, 226, 18, 210, 46, 238, 30, 222, 33, 225,
				17, 209, 45, 237, 29, 221
			},
			new byte[16]
			{
				162, 98, 146, 82, 174, 110, 158, 94, 161, 97,
				145, 81, 173, 109, 157, 93
			},
			new byte[16]
			{
				10, 202, 58, 250, 6, 198, 54, 246, 9, 201,
				57, 249, 5, 197, 53, 245
			},
			new byte[16]
			{
				138, 74, 186, 122, 134, 70, 182, 118, 137, 73,
				185, 121, 133, 69, 181, 117
			},
			new byte[16]
			{
				42, 234, 26, 218, 38, 230, 22, 214, 41, 233,
				25, 217, 37, 229, 21, 213
			},
			new byte[16]
			{
				170, 106, 154, 90, 166, 102, 150, 86, 169, 105,
				153, 89, 165, 101, 149, 85
			}
		};

		private QuantizerType m_quantizer;

		private jpeg_decompress_struct m_cinfo;

		private byte[][] m_sv_colormap;

		private int m_sv_actual;

		private byte[][] m_colorindex;

		private int[] m_colorindexOffset;

		private bool m_is_padded;

		private int[] m_Ncolors;

		private int m_row_index;

		private int[][][] m_odither;

		private short[][] m_fserrors;

		private bool m_on_odd_row;

		public my_1pass_cquantizer(jpeg_decompress_struct cinfo)
		{
			m_Ncolors = new int[4];
			m_odither = new int[4][][];
			m_fserrors = new short[4][];

			m_cinfo = cinfo;
			m_fserrors[0] = null;
			m_odither[0] = null;
			if (cinfo.m_out_color_components > 4)
			{
				cinfo.ERREXIT(J_MESSAGE_CODE.JERR_QUANT_COMPONENTS, 4);
			}
			if (cinfo.m_desired_number_of_colors > 256)
			{
				cinfo.ERREXIT(J_MESSAGE_CODE.JERR_QUANT_MANY_COLORS, 256);
			}
			create_colormap();
			create_colorindex();
			if (cinfo.m_dither_mode == J_DITHER_MODE.JDITHER_FS)
			{
				alloc_fs_workspace();
			}
		}

		public virtual void start_pass(bool is_pre_scan)
		{
			m_cinfo.m_colormap = m_sv_colormap;
			m_cinfo.m_actual_number_of_colors = m_sv_actual;
			switch (m_cinfo.m_dither_mode)
			{
			case J_DITHER_MODE.JDITHER_NONE:
				if (m_cinfo.m_out_color_components == 3)
				{
					m_quantizer = QuantizerType.color_quantizer3;
				}
				else
				{
					m_quantizer = QuantizerType.color_quantizer;
				}
				break;
			case J_DITHER_MODE.JDITHER_ORDERED:
				if (m_cinfo.m_out_color_components == 3)
				{
					m_quantizer = QuantizerType.quantize3_ord_dither_quantizer;
				}
				else
				{
					m_quantizer = QuantizerType.quantize3_ord_dither_quantizer;
				}
				m_row_index = 0;
				if (!m_is_padded)
				{
					create_colorindex();
				}
				if (m_odither[0] == null)
				{
					create_odither_tables();
				}
				break;
			case J_DITHER_MODE.JDITHER_FS:
			{
				m_quantizer = QuantizerType.quantize_fs_dither_quantizer;
				m_on_odd_row = false;
				if (m_fserrors[0] == null)
				{
					alloc_fs_workspace();
				}
				int length = m_cinfo.m_output_width + 2;
				for (int i = 0; i < m_cinfo.m_out_color_components; i++)
				{
					Array.Clear(m_fserrors[i], 0, length);
				}
				break;
			}
			default:
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOT_COMPILED);
				break;
			}
		}

		public virtual void color_quantize(byte[][] input_buf, int in_row, byte[][] output_buf, int out_row, int num_rows)
		{
			switch (m_quantizer)
			{
			case QuantizerType.color_quantizer3:
				quantize3(input_buf, in_row, output_buf, out_row, num_rows);
				break;
			case QuantizerType.color_quantizer:
				quantize(input_buf, in_row, output_buf, out_row, num_rows);
				break;
			case QuantizerType.quantize3_ord_dither_quantizer:
				quantize3_ord_dither(input_buf, in_row, output_buf, out_row, num_rows);
				break;
			case QuantizerType.quantize_ord_dither_quantizer:
				quantize_ord_dither(input_buf, in_row, output_buf, out_row, num_rows);
				break;
			case QuantizerType.quantize_fs_dither_quantizer:
				quantize_fs_dither(input_buf, in_row, output_buf, out_row, num_rows);
				break;
			default:
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
				break;
			}
		}

		public virtual void finish_pass()
		{
		}

		public virtual void new_color_map()
		{
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_MODE_CHANGE);
		}

		private void quantize(byte[][] input_buf, int in_row, byte[][] output_buf, int out_row, int num_rows)
		{
			int out_color_components = m_cinfo.m_out_color_components;
			for (int i = 0; i < num_rows; i++)
			{
				int num = 0;
				int num2 = in_row + i;
				int num3 = 0;
				int num4 = out_row + i;
				for (int num5 = m_cinfo.m_output_width; num5 > 0; num5--)
				{
					int num6 = 0;
					for (int j = 0; j < out_color_components; j++)
					{
						num6 += m_colorindex[j][m_colorindexOffset[j] + input_buf[num2][num]];
						num++;
					}
					output_buf[num4][num3] = (byte)num6;
					num3++;
				}
			}
		}

		private void quantize3(byte[][] input_buf, int in_row, byte[][] output_buf, int out_row, int num_rows)
		{
			int output_width = m_cinfo.m_output_width;
			for (int i = 0; i < num_rows; i++)
			{
				int num = 0;
				int num2 = in_row + i;
				int num3 = 0;
				int num4 = out_row + i;
				for (int num5 = output_width; num5 > 0; num5--)
				{
					int num6 = m_colorindex[0][m_colorindexOffset[0] + input_buf[num2][num]];
					num++;
					num6 += m_colorindex[1][m_colorindexOffset[1] + input_buf[num2][num]];
					num++;
					num6 += m_colorindex[2][m_colorindexOffset[2] + input_buf[num2][num]];
					num++;
					output_buf[num4][num3] = (byte)num6;
					num3++;
				}
			}
		}

		private void quantize_ord_dither(byte[][] input_buf, int in_row, byte[][] output_buf, int out_row, int num_rows)
		{
			int out_color_components = m_cinfo.m_out_color_components;
			int output_width = m_cinfo.m_output_width;
			for (int i = 0; i < num_rows; i++)
			{
				Array.Clear(output_buf[out_row + i], 0, output_width);
				int row_index = m_row_index;
				for (int j = 0; j < out_color_components; j++)
				{
					int num = j;
					int num2 = 0;
					int num3 = out_row + i;
					int num4 = 0;
					for (int num5 = output_width; num5 > 0; num5--)
					{
						output_buf[num3][num2] += m_colorindex[j][m_colorindexOffset[j] + input_buf[in_row + i][num] + m_odither[j][row_index][num4]];
						num += out_color_components;
						num2++;
						num4 = (num4 + 1) & 0xF;
					}
				}
				row_index = (row_index + 1) & 0xF;
				m_row_index = row_index;
			}
		}

		private void quantize3_ord_dither(byte[][] input_buf, int in_row, byte[][] output_buf, int out_row, int num_rows)
		{
			int output_width = m_cinfo.m_output_width;
			for (int i = 0; i < num_rows; i++)
			{
				int row_index = m_row_index;
				int num = in_row + i;
				int num2 = 0;
				int num3 = 0;
				int num4 = out_row + i;
				int num5 = 0;
				for (int num6 = output_width; num6 > 0; num6--)
				{
					int num7 = m_colorindex[0][m_colorindexOffset[0] + input_buf[num][num2] + m_odither[0][row_index][num5]];
					num2++;
					num7 += m_colorindex[1][m_colorindexOffset[1] + input_buf[num][num2] + m_odither[1][row_index][num5]];
					num2++;
					num7 += m_colorindex[2][m_colorindexOffset[2] + input_buf[num][num2] + m_odither[2][row_index][num5]];
					num2++;
					output_buf[num4][num3] = (byte)num7;
					num3++;
					num5 = (num5 + 1) & 0xF;
				}
				row_index = (row_index + 1) & 0xF;
				m_row_index = row_index;
			}
		}

		private void quantize_fs_dither(byte[][] input_buf, int in_row, byte[][] output_buf, int out_row, int num_rows)
		{
			int out_color_components = m_cinfo.m_out_color_components;
			int output_width = m_cinfo.m_output_width;
			byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
			int sampleRangeLimitOffset = m_cinfo.m_sampleRangeLimitOffset;
			for (int i = 0; i < num_rows; i++)
			{
				Array.Clear(output_buf[out_row + i], 0, output_width);
				for (int j = 0; j < out_color_components; j++)
				{
					int num = in_row + i;
					int num2 = j;
					int num3 = 0;
					int num4 = out_row + i;
					int num5 = 0;
					int num6;
					if (m_on_odd_row)
					{
						num2 += (output_width - 1) * out_color_components;
						num3 += output_width - 1;
						num6 = -1;
						num5 = output_width + 1;
					}
					else
					{
						num6 = 1;
						num5 = 0;
					}
					int num7 = num6 * out_color_components;
					int num8 = 0;
					int num9 = 0;
					int num10 = 0;
					for (int num11 = output_width; num11 > 0; num11--)
					{
						num8 = JpegUtils.RIGHT_SHIFT(num8 + m_fserrors[j][num5 + num6] + 8, 4);
						num8 += input_buf[num][num2];
						num8 = sample_range_limit[sampleRangeLimitOffset + num8];
						int num12 = m_colorindex[j][m_colorindexOffset[j] + num8];
						output_buf[num4][num3] += (byte)num12;
						num8 -= m_sv_colormap[j][num12];
						int num13 = num8;
						int num14 = num8 * 2;
						num8 += num14;
						m_fserrors[j][num5] = (short)(num10 + num8);
						num8 += num14;
						num10 = num9 + num8;
						num9 = num13;
						num8 += num14;
						num2 += num7;
						num3 += num6;
						num5 += num6;
					}
					m_fserrors[j][num5] = (short)num10;
				}
				m_on_odd_row = !m_on_odd_row;
			}
		}

		private void create_colormap()
		{
			int num = select_ncolors(m_Ncolors);
			if (m_cinfo.m_out_color_components == 3)
			{
				m_cinfo.TRACEMS(1, J_MESSAGE_CODE.JTRC_QUANT_3_NCOLORS, num, m_Ncolors[0], m_Ncolors[1], m_Ncolors[2]);
			}
			else
			{
				m_cinfo.TRACEMS(1, J_MESSAGE_CODE.JTRC_QUANT_NCOLORS, num);
			}
			byte[][] array = jpeg_common_struct.AllocJpegSamples(num, m_cinfo.m_out_color_components);
			int num2 = num;
			for (int i = 0; i < m_cinfo.m_out_color_components; i++)
			{
				int num3 = m_Ncolors[i];
				int num4 = num2 / num3;
				for (int j = 0; j < num3; j++)
				{
					int num5 = output_value(j, num3 - 1);
					for (int k = j * num4; k < num; k += num2)
					{
						for (int l = 0; l < num4; l++)
						{
							array[i][k + l] = (byte)num5;
						}
					}
				}
				num2 = num4;
			}
			m_sv_colormap = array;
			m_sv_actual = num;
		}

		private void create_colorindex()
		{
			int num;
			if (m_cinfo.m_dither_mode == J_DITHER_MODE.JDITHER_ORDERED)
			{
				num = 510;
				m_is_padded = true;
			}
			else
			{
				num = 0;
				m_is_padded = false;
			}
			m_colorindex = jpeg_common_struct.AllocJpegSamples(256 + num, m_cinfo.m_out_color_components);
			m_colorindexOffset = new int[m_cinfo.m_out_color_components];
			int num2 = m_sv_actual;
			for (int i = 0; i < m_cinfo.m_out_color_components; i++)
			{
				int num3 = m_Ncolors[i];
				num2 /= num3;
				if (num != 0)
				{
					m_colorindexOffset[i] += 255;
				}
				int num4 = 0;
				int num5 = largest_input_value(0, num3 - 1);
				for (int j = 0; j <= 255; j++)
				{
					while (j > num5)
					{
						num5 = largest_input_value(++num4, num3 - 1);
					}
					m_colorindex[i][m_colorindexOffset[i] + j] = (byte)(num4 * num2);
				}
				if (num != 0)
				{
					for (int k = 1; k <= 255; k++)
					{
						m_colorindex[i][m_colorindexOffset[i] + -k] = m_colorindex[i][m_colorindexOffset[i]];
						m_colorindex[i][m_colorindexOffset[i] + 255 + k] = m_colorindex[i][m_colorindexOffset[i] + 255];
					}
				}
			}
		}

		private void create_odither_tables()
		{
			for (int i = 0; i < m_cinfo.m_out_color_components; i++)
			{
				int num = m_Ncolors[i];
				int num2 = -1;
				for (int j = 0; j < i; j++)
				{
					if (num == m_Ncolors[j])
					{
						num2 = j;
						break;
					}
				}
				if (num2 == -1)
				{
					m_odither[i] = make_odither_array(num);
				}
				else
				{
					m_odither[i] = m_odither[num2];
				}
			}
		}

		private void alloc_fs_workspace()
		{
			for (int i = 0; i < m_cinfo.m_out_color_components; i++)
			{
				m_fserrors[i] = new short[m_cinfo.m_output_width + 2];
			}
		}

		private static int largest_input_value(int j, int maxj)
		{
			return ((2 * j + 1) * 255 + maxj) / (2 * maxj);
		}

		private static int output_value(int j, int maxj)
		{
			return (j * 255 + maxj / 2) / maxj;
		}

		private int select_ncolors(int[] Ncolors)
		{
			int out_color_components = m_cinfo.m_out_color_components;
			int desired_number_of_colors = m_cinfo.m_desired_number_of_colors;
			int num = 1;
			long num2 = 0L;
			do
			{
				num++;
				num2 = num;
				for (int i = 1; i < out_color_components; i++)
				{
					num2 *= num;
				}
			}
			while (num2 <= desired_number_of_colors);
			num--;
			if (num < 2)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_QUANT_FEW_COLORS, (int)num2);
			}
			int num3 = 1;
			for (int j = 0; j < out_color_components; j++)
			{
				Ncolors[j] = num;
				num3 *= num;
			}
			bool flag = false;
			do
			{
				flag = false;
				for (int k = 0; k < out_color_components; k++)
				{
					int num4 = ((m_cinfo.m_out_color_space == J_COLOR_SPACE.JCS_RGB) ? RGB_order[k] : k);
					num2 = num3 / Ncolors[num4];
					num2 *= Ncolors[num4] + 1;
					if (num2 > desired_number_of_colors)
					{
						break;
					}
					Ncolors[num4]++;
					num3 = (int)num2;
					flag = true;
				}
			}
			while (flag);
			return num3;
		}

		private static int[][] make_odither_array(int ncolors)
		{
			int[][] array = new int[16][];
			for (int i = 0; i < 16; i++)
			{
				array[i] = new int[16];
			}
			int num = 512 * (ncolors - 1);
			for (int j = 0; j < 16; j++)
			{
				for (int k = 0; k < 16; k++)
				{
					int num2 = (255 - 2 * base_dither_matrix[j][k]) * 255;
					array[j][k] = ((num2 < 0) ? (-(-num2 / num)) : (num2 / num));
				}
			}
			return array;
		}




	}
}
