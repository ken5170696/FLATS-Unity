using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class jpeg_color_converter
	{
		private const int SCALEBITS = 16;

		private const int CBCR_OFFSET = 8388608;

		private const int ONE_HALF = 32768;

		private const int R_Y_OFF = 0;

		private const int G_Y_OFF = 256;

		private const int B_Y_OFF = 512;

		private const int R_CB_OFF = 768;

		private const int G_CB_OFF = 1024;

		private const int B_CB_OFF = 1280;

		private const int R_CR_OFF = 1280;

		private const int G_CR_OFF = 1536;

		private const int B_CR_OFF = 1792;

		private const int TABLE_SIZE = 2048;

		private jpeg_compress_struct m_cinfo;

		private bool m_useNullStart;

		private bool m_useCmykYcckConvert;

		private bool m_useGrayscaleConvert;

		private bool m_useNullConvert;

		private bool m_useRgbGrayConvert;

		private bool m_useRgbYccConvert;

		private int[] m_rgb_ycc_tab;

		public jpeg_color_converter(jpeg_compress_struct cinfo)
		{
			m_cinfo = cinfo;
			m_useNullStart = true;
			switch (cinfo.m_in_color_space)
			{
			case J_COLOR_SPACE.JCS_GRAYSCALE:
				if (cinfo.m_input_components != 1)
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_IN_COLORSPACE);
				}
				break;
			case J_COLOR_SPACE.JCS_RGB:
			case J_COLOR_SPACE.JCS_YCbCr:
				if (cinfo.m_input_components != 3)
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_IN_COLORSPACE);
				}
				break;
			case J_COLOR_SPACE.JCS_CMYK:
			case J_COLOR_SPACE.JCS_YCCK:
				if (cinfo.m_input_components != 4)
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_IN_COLORSPACE);
				}
				break;
			default:
				if (cinfo.m_input_components < 1)
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_IN_COLORSPACE);
				}
				break;
			}
			clearConvertFlags();
			switch (cinfo.m_jpeg_color_space)
			{
			case J_COLOR_SPACE.JCS_GRAYSCALE:
				if (cinfo.m_num_components != 1)
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
				}
				if (cinfo.m_in_color_space == J_COLOR_SPACE.JCS_GRAYSCALE)
				{
					m_useGrayscaleConvert = true;
				}
				else if (cinfo.m_in_color_space == J_COLOR_SPACE.JCS_RGB)
				{
					m_useNullStart = false;
					m_useRgbGrayConvert = true;
				}
				else if (cinfo.m_in_color_space == J_COLOR_SPACE.JCS_YCbCr)
				{
					m_useGrayscaleConvert = true;
				}
				else
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
				}
				break;
			case J_COLOR_SPACE.JCS_RGB:
				if (cinfo.m_num_components != 3)
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
				}
				if (cinfo.m_in_color_space == J_COLOR_SPACE.JCS_RGB)
				{
					m_useNullConvert = true;
				}
				else
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
				}
				break;
			case J_COLOR_SPACE.JCS_YCbCr:
				if (cinfo.m_num_components != 3)
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
				}
				if (cinfo.m_in_color_space == J_COLOR_SPACE.JCS_RGB)
				{
					m_useNullStart = false;
					m_useRgbYccConvert = true;
				}
				else if (cinfo.m_in_color_space == J_COLOR_SPACE.JCS_YCbCr)
				{
					m_useNullConvert = true;
				}
				else
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
				}
				break;
			case J_COLOR_SPACE.JCS_CMYK:
				if (cinfo.m_num_components != 4)
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
				}
				if (cinfo.m_in_color_space == J_COLOR_SPACE.JCS_CMYK)
				{
					m_useNullConvert = true;
				}
				else
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
				}
				break;
			case J_COLOR_SPACE.JCS_YCCK:
				if (cinfo.m_num_components != 4)
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
				}
				if (cinfo.m_in_color_space == J_COLOR_SPACE.JCS_CMYK)
				{
					m_useNullStart = false;
					m_useCmykYcckConvert = true;
				}
				else if (cinfo.m_in_color_space == J_COLOR_SPACE.JCS_YCCK)
				{
					m_useNullConvert = true;
				}
				else
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
				}
				break;
			default:
				if (cinfo.m_jpeg_color_space != cinfo.m_in_color_space || cinfo.m_num_components != cinfo.m_input_components)
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
				}
				m_useNullConvert = true;
				break;
			}
		}

		public void start_pass()
		{
			if (!m_useNullStart)
			{
				rgb_ycc_start();
			}
		}

		public void color_convert(byte[][] input_buf, int input_row, byte[][][] output_buf, int output_row, int num_rows)
		{
			if (m_useCmykYcckConvert)
			{
				cmyk_ycck_convert(input_buf, input_row, output_buf, output_row, num_rows);
			}
			else if (m_useGrayscaleConvert)
			{
				grayscale_convert(input_buf, input_row, output_buf, output_row, num_rows);
			}
			else if (m_useRgbGrayConvert)
			{
				rgb_gray_convert(input_buf, input_row, output_buf, output_row, num_rows);
			}
			else if (m_useRgbYccConvert)
			{
				rgb_ycc_convert(input_buf, input_row, output_buf, output_row, num_rows);
			}
			else if (m_useNullConvert)
			{
				null_convert(input_buf, input_row, output_buf, output_row, num_rows);
			}
			else
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
			}
		}

		private void rgb_ycc_start()
		{
			m_rgb_ycc_tab = new int[2048];
			for (int i = 0; i <= 255; i++)
			{
				m_rgb_ycc_tab[i] = FIX(0.299) * i;
				m_rgb_ycc_tab[i + 256] = FIX(0.587) * i;
				m_rgb_ycc_tab[i + 512] = FIX(0.114) * i + 32768;
				m_rgb_ycc_tab[i + 768] = -FIX(0.16874) * i;
				m_rgb_ycc_tab[i + 1024] = -FIX(0.33126) * i;
				m_rgb_ycc_tab[i + 1280] = FIX(0.5) * i + 8388608 + 32768 - 1;
				m_rgb_ycc_tab[i + 1536] = -FIX(0.41869) * i;
				m_rgb_ycc_tab[i + 1792] = -FIX(0.08131) * i;
			}
		}

		private void clearConvertFlags()
		{
			m_useCmykYcckConvert = false;
			m_useGrayscaleConvert = false;
			m_useNullConvert = false;
			m_useRgbGrayConvert = false;
			m_useRgbYccConvert = false;
		}

		private static int FIX(double x)
		{
			return (int)(x * 65536.0 + 0.5);
		}

		private void rgb_ycc_convert(byte[][] input_buf, int input_row, byte[][][] output_buf, int output_row, int num_rows)
		{
			int image_width = m_cinfo.m_image_width;
			for (int i = 0; i < num_rows; i++)
			{
				int num = 0;
				for (int j = 0; j < image_width; j++)
				{
					int num2 = input_buf[input_row + i][num];
					int num3 = input_buf[input_row + i][num + 1];
					int num4 = input_buf[input_row + i][num + 2];
					num += 3;
					output_buf[0][output_row][j] = (byte)(m_rgb_ycc_tab[num2] + m_rgb_ycc_tab[num3 + 256] + m_rgb_ycc_tab[num4 + 512] >> 16);
					output_buf[1][output_row][j] = (byte)(m_rgb_ycc_tab[num2 + 768] + m_rgb_ycc_tab[num3 + 1024] + m_rgb_ycc_tab[num4 + 1280] >> 16);
					output_buf[2][output_row][j] = (byte)(m_rgb_ycc_tab[num2 + 1280] + m_rgb_ycc_tab[num3 + 1536] + m_rgb_ycc_tab[num4 + 1792] >> 16);
				}
				output_row++;
			}
		}

		private void rgb_gray_convert(byte[][] input_buf, int input_row, byte[][][] output_buf, int output_row, int num_rows)
		{
			int image_width = m_cinfo.m_image_width;
			for (int i = 0; i < num_rows; i++)
			{
				int num = 0;
				for (int j = 0; j < image_width; j++)
				{
					int num2 = input_buf[input_row + i][num];
					int num3 = input_buf[input_row + i][num + 1];
					int num4 = input_buf[input_row + i][num + 2];
					num += 3;
					output_buf[0][output_row][j] = (byte)(m_rgb_ycc_tab[num2] + m_rgb_ycc_tab[num3 + 256] + m_rgb_ycc_tab[num4 + 512] >> 16);
				}
				output_row++;
			}
		}

		private void cmyk_ycck_convert(byte[][] input_buf, int input_row, byte[][][] output_buf, int output_row, int num_rows)
		{
			int image_width = m_cinfo.m_image_width;
			for (int i = 0; i < num_rows; i++)
			{
				int num = 0;
				for (int j = 0; j < image_width; j++)
				{
					int num2 = 255 - input_buf[input_row + i][num];
					int num3 = 255 - input_buf[input_row + i][num + 1];
					int num4 = 255 - input_buf[input_row + i][num + 2];
					output_buf[3][output_row][j] = input_buf[input_row + i][num + 3];
					num += 4;
					output_buf[0][output_row][j] = (byte)(m_rgb_ycc_tab[num2] + m_rgb_ycc_tab[num3 + 256] + m_rgb_ycc_tab[num4 + 512] >> 16);
					output_buf[1][output_row][j] = (byte)(m_rgb_ycc_tab[num2 + 768] + m_rgb_ycc_tab[num3 + 1024] + m_rgb_ycc_tab[num4 + 1280] >> 16);
					output_buf[2][output_row][j] = (byte)(m_rgb_ycc_tab[num2 + 1280] + m_rgb_ycc_tab[num3 + 1536] + m_rgb_ycc_tab[num4 + 1792] >> 16);
				}
				output_row++;
			}
		}

		private void grayscale_convert(byte[][] input_buf, int input_row, byte[][][] output_buf, int output_row, int num_rows)
		{
			int image_width = m_cinfo.m_image_width;
			int input_components = m_cinfo.m_input_components;
			for (int i = 0; i < num_rows; i++)
			{
				int num = 0;
				for (int j = 0; j < image_width; j++)
				{
					output_buf[0][output_row][j] = input_buf[input_row + i][num];
					num += input_components;
				}
				output_row++;
			}
		}

		private void null_convert(byte[][] input_buf, int input_row, byte[][][] output_buf, int output_row, int num_rows)
		{
			int num_components = m_cinfo.m_num_components;
			int image_width = m_cinfo.m_image_width;
			for (int i = 0; i < num_rows; i++)
			{
				for (int j = 0; j < num_components; j++)
				{
					int num = 0;
					for (int k = 0; k < image_width; k++)
					{
						output_buf[j][output_row][k] = input_buf[input_row + i][num + j];
						num += num_components;
					}
				}
				output_row++;
			}
		}




	}
}
