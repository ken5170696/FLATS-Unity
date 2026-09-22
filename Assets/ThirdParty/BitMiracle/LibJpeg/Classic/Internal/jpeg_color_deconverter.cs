using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class jpeg_color_deconverter
	{
		private enum ColorConverter
		{
			grayscale_converter,
			ycc_rgb_converter,
			gray_rgb_converter,
			null_converter,
			ycck_cmyk_converter
		}

		private const int SCALEBITS = 16;

		private const int ONE_HALF = 32768;

		private ColorConverter m_converter;

		private jpeg_decompress_struct m_cinfo;

		private int[] m_perComponentOffsets;

		private int[] m_Cr_r_tab;

		private int[] m_Cb_b_tab;

		private int[] m_Cr_g_tab;

		private int[] m_Cb_g_tab;

		public jpeg_color_deconverter(jpeg_decompress_struct cinfo)
		{
			m_cinfo = cinfo;
			switch (cinfo.m_jpeg_color_space)
			{
			case J_COLOR_SPACE.JCS_GRAYSCALE:
				if (cinfo.m_num_components != 1)
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
				}
				break;
			case J_COLOR_SPACE.JCS_RGB:
			case J_COLOR_SPACE.JCS_YCbCr:
				if (cinfo.m_num_components != 3)
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
				}
				break;
			case J_COLOR_SPACE.JCS_CMYK:
			case J_COLOR_SPACE.JCS_YCCK:
				if (cinfo.m_num_components != 4)
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
				}
				break;
			default:
				if (cinfo.m_num_components < 1)
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
				}
				break;
			}
			switch (cinfo.m_out_color_space)
			{
			case J_COLOR_SPACE.JCS_GRAYSCALE:
				cinfo.m_out_color_components = 1;
				if (cinfo.m_jpeg_color_space == J_COLOR_SPACE.JCS_GRAYSCALE || cinfo.m_jpeg_color_space == J_COLOR_SPACE.JCS_YCbCr)
				{
					m_converter = ColorConverter.grayscale_converter;
					for (int i = 1; i < cinfo.m_num_components; i++)
					{
						cinfo.Comp_info[i].component_needed = false;
					}
				}
				else
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
				}
				break;
			case J_COLOR_SPACE.JCS_RGB:
				cinfo.m_out_color_components = 3;
				if (cinfo.m_jpeg_color_space == J_COLOR_SPACE.JCS_YCbCr)
				{
					m_converter = ColorConverter.ycc_rgb_converter;
					build_ycc_rgb_table();
				}
				else if (cinfo.m_jpeg_color_space == J_COLOR_SPACE.JCS_GRAYSCALE)
				{
					m_converter = ColorConverter.gray_rgb_converter;
				}
				else if (cinfo.m_jpeg_color_space == J_COLOR_SPACE.JCS_RGB)
				{
					m_converter = ColorConverter.null_converter;
				}
				else
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
				}
				break;
			case J_COLOR_SPACE.JCS_CMYK:
				cinfo.m_out_color_components = 4;
				if (cinfo.m_jpeg_color_space == J_COLOR_SPACE.JCS_YCCK)
				{
					m_converter = ColorConverter.ycck_cmyk_converter;
					build_ycc_rgb_table();
				}
				else if (cinfo.m_jpeg_color_space == J_COLOR_SPACE.JCS_CMYK)
				{
					m_converter = ColorConverter.null_converter;
				}
				else
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
				}
				break;
			default:
				if (cinfo.m_out_color_space == cinfo.m_jpeg_color_space)
				{
					cinfo.m_out_color_components = cinfo.m_num_components;
					m_converter = ColorConverter.null_converter;
				}
				else
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
				}
				break;
			}
			if (cinfo.m_quantize_colors)
			{
				cinfo.m_output_components = 1;
			}
			else
			{
				cinfo.m_output_components = cinfo.m_out_color_components;
			}
		}

		public void color_convert(ComponentBuffer[] input_buf, int[] perComponentOffsets, int input_row, byte[][] output_buf, int output_row, int num_rows)
		{
			m_perComponentOffsets = perComponentOffsets;
			switch (m_converter)
			{
			case ColorConverter.grayscale_converter:
				grayscale_convert(input_buf, input_row, output_buf, output_row, num_rows);
				break;
			case ColorConverter.ycc_rgb_converter:
				ycc_rgb_convert(input_buf, input_row, output_buf, output_row, num_rows);
				break;
			case ColorConverter.gray_rgb_converter:
				gray_rgb_convert(input_buf, input_row, output_buf, output_row, num_rows);
				break;
			case ColorConverter.null_converter:
				null_convert(input_buf, input_row, output_buf, output_row, num_rows);
				break;
			case ColorConverter.ycck_cmyk_converter:
				ycck_cmyk_convert(input_buf, input_row, output_buf, output_row, num_rows);
				break;
			default:
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
				break;
			}
		}

		private void build_ycc_rgb_table()
		{
			m_Cr_r_tab = new int[256];
			m_Cb_b_tab = new int[256];
			m_Cr_g_tab = new int[256];
			m_Cb_g_tab = new int[256];
			int num = 0;
			int num2 = -128;
			while (num <= 255)
			{
				m_Cr_r_tab[num] = JpegUtils.RIGHT_SHIFT(FIX(1.402) * num2 + 32768, 16);
				m_Cb_b_tab[num] = JpegUtils.RIGHT_SHIFT(FIX(1.772) * num2 + 32768, 16);
				m_Cr_g_tab[num] = -FIX(0.71414) * num2;
				m_Cb_g_tab[num] = -FIX(0.34414) * num2 + 32768;
				num++;
				num2++;
			}
		}

		private void ycc_rgb_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
		{
			int num = m_perComponentOffsets[0];
			int num2 = m_perComponentOffsets[1];
			int num3 = m_perComponentOffsets[2];
			byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
			int sampleRangeLimitOffset = m_cinfo.m_sampleRangeLimitOffset;
			for (int i = 0; i < num_rows; i++)
			{
				int num4 = 0;
				for (int j = 0; j < m_cinfo.m_output_width; j++)
				{
					int num5 = input_buf[0][input_row + num][j];
					int num6 = input_buf[1][input_row + num2][j];
					int num7 = input_buf[2][input_row + num3][j];
					output_buf[output_row + i][num4] = sample_range_limit[sampleRangeLimitOffset + num5 + m_Cr_r_tab[num7]];
					output_buf[output_row + i][num4 + 1] = sample_range_limit[sampleRangeLimitOffset + num5 + JpegUtils.RIGHT_SHIFT(m_Cb_g_tab[num6] + m_Cr_g_tab[num7], 16)];
					output_buf[output_row + i][num4 + 2] = sample_range_limit[sampleRangeLimitOffset + num5 + m_Cb_b_tab[num6]];
					num4 += 3;
				}
				input_row++;
			}
		}

		private void ycck_cmyk_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
		{
			int num = m_perComponentOffsets[0];
			int num2 = m_perComponentOffsets[1];
			int num3 = m_perComponentOffsets[2];
			int num4 = m_perComponentOffsets[3];
			byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
			int sampleRangeLimitOffset = m_cinfo.m_sampleRangeLimitOffset;
			int output_width = m_cinfo.m_output_width;
			for (int i = 0; i < num_rows; i++)
			{
				int num5 = 0;
				for (int j = 0; j < output_width; j++)
				{
					int num6 = input_buf[0][input_row + num][j];
					int num7 = input_buf[1][input_row + num2][j];
					int num8 = input_buf[2][input_row + num3][j];
					output_buf[output_row + i][num5] = sample_range_limit[sampleRangeLimitOffset + 255 - (num6 + m_Cr_r_tab[num8])];
					output_buf[output_row + i][num5 + 1] = sample_range_limit[sampleRangeLimitOffset + 255 - (num6 + JpegUtils.RIGHT_SHIFT(m_Cb_g_tab[num7] + m_Cr_g_tab[num8], 16))];
					output_buf[output_row + i][num5 + 2] = sample_range_limit[sampleRangeLimitOffset + 255 - (num6 + m_Cb_b_tab[num7])];
					output_buf[output_row + i][num5 + 3] = input_buf[3][input_row + num4][j];
					num5 += 4;
				}
				input_row++;
			}
		}

		private void gray_rgb_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
		{
			int num = m_perComponentOffsets[0];
			int num2 = m_perComponentOffsets[1];
			int num3 = m_perComponentOffsets[2];
			int output_width = m_cinfo.m_output_width;
			for (int i = 0; i < num_rows; i++)
			{
				int num4 = 0;
				for (int j = 0; j < output_width; j++)
				{
					output_buf[output_row + i][num4] = input_buf[0][input_row + num][j];
					output_buf[output_row + i][num4 + 1] = input_buf[0][input_row + num2][j];
					output_buf[output_row + i][num4 + 2] = input_buf[0][input_row + num3][j];
					num4 += 3;
				}
				input_row++;
			}
		}

		private void grayscale_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
		{
			JpegUtils.jcopy_sample_rows(input_buf[0], input_row + m_perComponentOffsets[0], output_buf, output_row, num_rows, m_cinfo.m_output_width);
		}

		private void null_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
		{
			for (int i = 0; i < num_rows; i++)
			{
				for (int j = 0; j < m_cinfo.m_num_components; j++)
				{
					int num = 0;
					int num2 = 0;
					int num3 = m_perComponentOffsets[j];
					for (int num4 = m_cinfo.m_output_width; num4 > 0; num4--)
					{
						output_buf[output_row + i][j + num2] = input_buf[j][input_row + num3][num];
						num2 += m_cinfo.m_num_components;
						num++;
					}
				}
				input_row++;
			}
		}

		private static int FIX(double x)
		{
			return (int)(x * 65536.0 + 0.5);
		}




	}
}
