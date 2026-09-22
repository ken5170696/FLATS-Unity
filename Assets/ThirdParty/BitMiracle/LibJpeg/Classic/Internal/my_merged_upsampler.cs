using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class my_merged_upsampler : jpeg_upsampler
	{
		private const int SCALEBITS = 16;

		private const int ONE_HALF = 32768;

		private jpeg_decompress_struct m_cinfo;

		private bool m_use_2v_upsample;

		private int[] m_Cr_r_tab;

		private int[] m_Cb_b_tab;

		private int[] m_Cr_g_tab;

		private int[] m_Cb_g_tab;

		private byte[] m_spare_row;

		private bool m_spare_full;

		private int m_out_row_width;

		private int m_rows_to_go;

		public my_merged_upsampler(jpeg_decompress_struct cinfo)
		{
			m_cinfo = cinfo;
			m_need_context_rows = false;
			m_out_row_width = cinfo.m_output_width * cinfo.m_out_color_components;
			if (cinfo.m_max_v_samp_factor == 2)
			{
				m_use_2v_upsample = true;
				m_spare_row = new byte[m_out_row_width];
			}
			else
			{
				m_use_2v_upsample = false;
			}
			build_ycc_rgb_table();
		}

		public override void start_pass()
		{
			m_spare_full = false;
			m_rows_to_go = m_cinfo.m_output_height;
		}

		public override void upsample(ComponentBuffer[] input_buf, ref int in_row_group_ctr, int in_row_groups_avail, byte[][] output_buf, ref int out_row_ctr, int out_rows_avail)
		{
			if (m_use_2v_upsample)
			{
				merged_2v_upsample(input_buf, ref in_row_group_ctr, output_buf, ref out_row_ctr, out_rows_avail);
			}
			else
			{
				merged_1v_upsample(input_buf, ref in_row_group_ctr, output_buf, ref out_row_ctr);
			}
		}

		private void merged_1v_upsample(ComponentBuffer[] input_buf, ref int in_row_group_ctr, byte[][] output_buf, ref int out_row_ctr)
		{
			h2v1_merged_upsample(input_buf, in_row_group_ctr, output_buf, out_row_ctr);
			out_row_ctr++;
			in_row_group_ctr++;
		}

		private void merged_2v_upsample(ComponentBuffer[] input_buf, ref int in_row_group_ctr, byte[][] output_buf, ref int out_row_ctr, int out_rows_avail)
		{
			int num;
			if (m_spare_full)
			{
				JpegUtils.jcopy_sample_rows(new byte[1][] { m_spare_row }, 0, output_buf, out_row_ctr, 1, m_out_row_width);
				num = 1;
				m_spare_full = false;
			}
			else
			{
				num = 2;
				if (num > m_rows_to_go)
				{
					num = m_rows_to_go;
				}
				out_rows_avail -= out_row_ctr;
				if (num > out_rows_avail)
				{
					num = out_rows_avail;
				}
				byte[][] array = new byte[2][]
				{
					output_buf[out_row_ctr],
					null
				};
				if (num > 1)
				{
					array[1] = output_buf[out_row_ctr + 1];
				}
				else
				{
					array[1] = m_spare_row;
					m_spare_full = true;
				}
				h2v2_merged_upsample(input_buf, in_row_group_ctr, array);
			}
			out_row_ctr += num;
			m_rows_to_go -= num;
			if (!m_spare_full)
			{
				in_row_group_ctr++;
			}
		}

		private void h2v1_merged_upsample(ComponentBuffer[] input_buf, int in_row_group_ctr, byte[][] output_buf, int outRow)
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
			int sampleRangeLimitOffset = m_cinfo.m_sampleRangeLimitOffset;
			for (int num5 = m_cinfo.m_output_width >> 1; num5 > 0; num5--)
			{
				int num6 = input_buf[1][in_row_group_ctr][num2];
				num2++;
				int num7 = input_buf[2][in_row_group_ctr][num3];
				num3++;
				int num8 = m_Cr_r_tab[num7];
				int num9 = JpegUtils.RIGHT_SHIFT(m_Cb_g_tab[num6] + m_Cr_g_tab[num7], 16);
				int num10 = m_Cb_b_tab[num6];
				int num11 = input_buf[0][in_row_group_ctr][num];
				num++;
				output_buf[outRow][num4] = sample_range_limit[sampleRangeLimitOffset + num11 + num8];
				output_buf[outRow][num4 + 1] = sample_range_limit[sampleRangeLimitOffset + num11 + num9];
				output_buf[outRow][num4 + 2] = sample_range_limit[sampleRangeLimitOffset + num11 + num10];
				num4 += 3;
				num11 = input_buf[0][in_row_group_ctr][num];
				num++;
				output_buf[outRow][num4] = sample_range_limit[sampleRangeLimitOffset + num11 + num8];
				output_buf[outRow][num4 + 1] = sample_range_limit[sampleRangeLimitOffset + num11 + num9];
				output_buf[outRow][num4 + 2] = sample_range_limit[sampleRangeLimitOffset + num11 + num10];
				num4 += 3;
			}
			if ((m_cinfo.m_output_width & 1) != 0)
			{
				int num12 = input_buf[1][in_row_group_ctr][num2];
				int num13 = input_buf[2][in_row_group_ctr][num3];
				int num14 = m_Cr_r_tab[num13];
				int num15 = JpegUtils.RIGHT_SHIFT(m_Cb_g_tab[num12] + m_Cr_g_tab[num13], 16);
				int num16 = m_Cb_b_tab[num12];
				int num17 = input_buf[0][in_row_group_ctr][num];
				output_buf[outRow][num4] = sample_range_limit[sampleRangeLimitOffset + num17 + num14];
				output_buf[outRow][num4 + 1] = sample_range_limit[sampleRangeLimitOffset + num17 + num15];
				output_buf[outRow][num4 + 2] = sample_range_limit[sampleRangeLimitOffset + num17 + num16];
			}
		}

		private void h2v2_merged_upsample(ComponentBuffer[] input_buf, int in_row_group_ctr, byte[][] output_buf)
		{
			int i = in_row_group_ctr * 2;
			int num = 0;
			int i2 = in_row_group_ctr * 2 + 1;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
			int sampleRangeLimitOffset = m_cinfo.m_sampleRangeLimitOffset;
			for (int num7 = m_cinfo.m_output_width >> 1; num7 > 0; num7--)
			{
				int num8 = input_buf[1][in_row_group_ctr][num3];
				num3++;
				int num9 = input_buf[2][in_row_group_ctr][num4];
				num4++;
				int num10 = m_Cr_r_tab[num9];
				int num11 = JpegUtils.RIGHT_SHIFT(m_Cb_g_tab[num8] + m_Cr_g_tab[num9], 16);
				int num12 = m_Cb_b_tab[num8];
				int num13 = input_buf[0][i][num];
				num++;
				output_buf[0][num5] = sample_range_limit[sampleRangeLimitOffset + num13 + num10];
				output_buf[0][num5 + 1] = sample_range_limit[sampleRangeLimitOffset + num13 + num11];
				output_buf[0][num5 + 2] = sample_range_limit[sampleRangeLimitOffset + num13 + num12];
				num5 += 3;
				num13 = input_buf[0][i][num];
				num++;
				output_buf[0][num5] = sample_range_limit[sampleRangeLimitOffset + num13 + num10];
				output_buf[0][num5 + 1] = sample_range_limit[sampleRangeLimitOffset + num13 + num11];
				output_buf[0][num5 + 2] = sample_range_limit[sampleRangeLimitOffset + num13 + num12];
				num5 += 3;
				num13 = input_buf[0][i2][num2];
				num2++;
				output_buf[1][num6] = sample_range_limit[sampleRangeLimitOffset + num13 + num10];
				output_buf[1][num6 + 1] = sample_range_limit[sampleRangeLimitOffset + num13 + num11];
				output_buf[1][num6 + 2] = sample_range_limit[sampleRangeLimitOffset + num13 + num12];
				num6 += 3;
				num13 = input_buf[0][i2][num2];
				num2++;
				output_buf[1][num6] = sample_range_limit[sampleRangeLimitOffset + num13 + num10];
				output_buf[1][num6 + 1] = sample_range_limit[sampleRangeLimitOffset + num13 + num11];
				output_buf[1][num6 + 2] = sample_range_limit[sampleRangeLimitOffset + num13 + num12];
				num6 += 3;
			}
			if ((m_cinfo.m_output_width & 1) != 0)
			{
				int num14 = input_buf[1][in_row_group_ctr][num3];
				int num15 = input_buf[2][in_row_group_ctr][num4];
				int num16 = m_Cr_r_tab[num15];
				int num17 = JpegUtils.RIGHT_SHIFT(m_Cb_g_tab[num14] + m_Cr_g_tab[num15], 16);
				int num18 = m_Cb_b_tab[num14];
				int num19 = input_buf[0][i][num];
				output_buf[0][num5] = sample_range_limit[sampleRangeLimitOffset + num19 + num16];
				output_buf[0][num5 + 1] = sample_range_limit[sampleRangeLimitOffset + num19 + num17];
				output_buf[0][num5 + 2] = sample_range_limit[sampleRangeLimitOffset + num19 + num18];
				num19 = input_buf[0][i2][num2];
				output_buf[1][num6] = sample_range_limit[sampleRangeLimitOffset + num19 + num16];
				output_buf[1][num6 + 1] = sample_range_limit[sampleRangeLimitOffset + num19 + num17];
				output_buf[1][num6 + 2] = sample_range_limit[sampleRangeLimitOffset + num19 + num18];
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

		private static int FIX(double x)
		{
			return (int)(x * 65536.0 + 0.5);
		}




	}
}
