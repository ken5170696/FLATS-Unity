using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class my_2pass_cquantizer : jpeg_color_quantizer
	{
		private struct box
		{
			public int c0min;

			public int c0max;

			public int c1min;

			public int c1max;

			public int c2min;

			public int c2max;

			public int volume;

			public long colorcount;
		}

		private enum QuantizerType
		{
			prescan_quantizer,
			pass2_fs_dither_quantizer,
			pass2_no_dither_quantizer
		}

		private const int MAXNUMCOLORS = 256;

		private const int HIST_C0_BITS = 5;

		private const int HIST_C1_BITS = 6;

		private const int HIST_C2_BITS = 5;

		private const int HIST_C0_ELEMS = 32;

		private const int HIST_C1_ELEMS = 64;

		private const int HIST_C2_ELEMS = 32;

		private const int C0_SHIFT = 3;

		private const int C1_SHIFT = 2;

		private const int C2_SHIFT = 3;

		private const int R_SCALE = 2;

		private const int G_SCALE = 3;

		private const int B_SCALE = 1;

		private const int BOX_C0_LOG = 2;

		private const int BOX_C1_LOG = 3;

		private const int BOX_C2_LOG = 2;

		private const int BOX_C0_ELEMS = 4;

		private const int BOX_C1_ELEMS = 8;

		private const int BOX_C2_ELEMS = 4;

		private const int BOX_C0_SHIFT = 5;

		private const int BOX_C1_SHIFT = 5;

		private const int BOX_C2_SHIFT = 5;

		private QuantizerType m_quantizer;

		private bool m_useFinishPass1;

		private jpeg_decompress_struct m_cinfo;

		private byte[][] m_sv_colormap;

		private int m_desired;

		private ushort[][] m_histogram;

		private bool m_needs_zeroed;

		private short[] m_fserrors;

		private bool m_on_odd_row;

		private int[] m_error_limiter;

		public my_2pass_cquantizer(jpeg_decompress_struct cinfo)
		{
			m_cinfo = cinfo;
			if (cinfo.m_out_color_components != 3)
			{
				cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
			}
			m_histogram = new ushort[32][];
			for (int i = 0; i < 32; i++)
			{
				m_histogram[i] = new ushort[2048];
			}
			m_needs_zeroed = true;
			if (cinfo.m_enable_2pass_quant)
			{
				int desired_number_of_colors = cinfo.m_desired_number_of_colors;
				if (desired_number_of_colors < 8)
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_QUANT_FEW_COLORS, 8);
				}
				if (desired_number_of_colors > 256)
				{
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_QUANT_MANY_COLORS, 256);
				}
				m_sv_colormap = jpeg_common_struct.AllocJpegSamples(desired_number_of_colors, 3);
				m_desired = desired_number_of_colors;
			}
			if (cinfo.m_dither_mode != J_DITHER_MODE.JDITHER_NONE)
			{
				cinfo.m_dither_mode = J_DITHER_MODE.JDITHER_FS;
			}
			if (cinfo.m_dither_mode == J_DITHER_MODE.JDITHER_FS)
			{
				m_fserrors = new short[(cinfo.m_output_width + 2) * 3];
				init_error_limit();
			}
		}

		public virtual void start_pass(bool is_pre_scan)
		{
			if (m_cinfo.m_dither_mode != J_DITHER_MODE.JDITHER_NONE)
			{
				m_cinfo.m_dither_mode = J_DITHER_MODE.JDITHER_FS;
			}
			if (is_pre_scan)
			{
				m_quantizer = QuantizerType.prescan_quantizer;
				m_useFinishPass1 = true;
				m_needs_zeroed = true;
			}
			else
			{
				if (m_cinfo.m_dither_mode == J_DITHER_MODE.JDITHER_FS)
				{
					m_quantizer = QuantizerType.pass2_fs_dither_quantizer;
				}
				else
				{
					m_quantizer = QuantizerType.pass2_no_dither_quantizer;
				}
				m_useFinishPass1 = false;
				int actual_number_of_colors = m_cinfo.m_actual_number_of_colors;
				if (actual_number_of_colors < 1)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_QUANT_FEW_COLORS, 1);
				}
				if (actual_number_of_colors > 256)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_QUANT_MANY_COLORS, 256);
				}
				if (m_cinfo.m_dither_mode == J_DITHER_MODE.JDITHER_FS)
				{
					if (m_fserrors == null)
					{
						int num = (m_cinfo.m_output_width + 2) * 3;
						m_fserrors = new short[num];
					}
					else
					{
						Array.Clear(m_fserrors, 0, m_fserrors.Length);
					}
					if (m_error_limiter == null)
					{
						init_error_limit();
					}
					m_on_odd_row = false;
				}
			}
			if (m_needs_zeroed)
			{
				for (int i = 0; i < 32; i++)
				{
					Array.Clear(m_histogram[i], 0, m_histogram[i].Length);
				}
				m_needs_zeroed = false;
			}
		}

		public virtual void color_quantize(byte[][] input_buf, int in_row, byte[][] output_buf, int out_row, int num_rows)
		{
			switch (m_quantizer)
			{
			case QuantizerType.prescan_quantizer:
				prescan_quantize(input_buf, in_row, num_rows);
				break;
			case QuantizerType.pass2_fs_dither_quantizer:
				pass2_fs_dither(input_buf, in_row, output_buf, out_row, num_rows);
				break;
			case QuantizerType.pass2_no_dither_quantizer:
				pass2_no_dither(input_buf, in_row, output_buf, out_row, num_rows);
				break;
			default:
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
				break;
			}
		}

		public virtual void finish_pass()
		{
			if (m_useFinishPass1)
			{
				finish_pass1();
			}
		}

		public virtual void new_color_map()
		{
			m_needs_zeroed = true;
		}

		private void prescan_quantize(byte[][] input_buf, int in_row, int num_rows)
		{
			for (int i = 0; i < num_rows; i++)
			{
				int num = 0;
				for (int num2 = m_cinfo.m_output_width; num2 > 0; num2--)
				{
					int num3 = input_buf[in_row + i][num] >> 3;
					int num4 = (input_buf[in_row + i][num + 1] >> 2) * 32 + (input_buf[in_row + i][num + 2] >> 3);
					m_histogram[num3][num4]++;
					if (m_histogram[num3][num4] <= 0)
					{
						m_histogram[num3][num4]--;
					}
					num += 3;
				}
			}
		}

		private void pass2_fs_dither(byte[][] input_buf, int in_row, byte[][] output_buf, int out_row, int num_rows)
		{
			byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
			int sampleRangeLimitOffset = m_cinfo.m_sampleRangeLimitOffset;
			for (int i = 0; i < num_rows; i++)
			{
				int num = 0;
				int num2 = 0;
				int num3 = 0;
				int num4;
				int num5;
				if (m_on_odd_row)
				{
					num += (m_cinfo.m_output_width - 1) * 3;
					num2 += m_cinfo.m_output_width - 1;
					num4 = -1;
					num5 = -3;
					num3 = (m_cinfo.m_output_width + 1) * 3;
					m_on_odd_row = false;
				}
				else
				{
					num4 = 1;
					num5 = 3;
					num3 = 0;
					m_on_odd_row = true;
				}
				int num6 = 0;
				int num7 = 0;
				int num8 = 0;
				int num9 = 0;
				int num10 = 0;
				int num11 = 0;
				int num12 = 0;
				int num13 = 0;
				int num14 = 0;
				for (int num15 = m_cinfo.m_output_width; num15 > 0; num15--)
				{
					num6 = JpegUtils.RIGHT_SHIFT(num6 + m_fserrors[num3 + num5] + 8, 4);
					num7 = JpegUtils.RIGHT_SHIFT(num7 + m_fserrors[num3 + num5 + 1] + 8, 4);
					num8 = JpegUtils.RIGHT_SHIFT(num8 + m_fserrors[num3 + num5 + 2] + 8, 4);
					num6 = m_error_limiter[255 + num6];
					num7 = m_error_limiter[255 + num7];
					num8 = m_error_limiter[255 + num8];
					num6 += input_buf[in_row + i][num];
					num7 += input_buf[in_row + i][num + 1];
					num8 += input_buf[in_row + i][num + 2];
					num6 = sample_range_limit[sampleRangeLimitOffset + num6];
					num7 = sample_range_limit[sampleRangeLimitOffset + num7];
					num8 = sample_range_limit[sampleRangeLimitOffset + num8];
					int num16 = num6 >> 3;
					int num17 = (num7 >> 2) * 32 + (num8 >> 3);
					if (m_histogram[num16][num17] == 0)
					{
						fill_inverse_cmap(num6 >> 3, num7 >> 2, num8 >> 3);
					}
					int num18 = m_histogram[num16][num17] - 1;
					output_buf[out_row + i][num2] = (byte)num18;
					num6 -= m_cinfo.m_colormap[0][num18];
					num7 -= m_cinfo.m_colormap[1][num18];
					num8 -= m_cinfo.m_colormap[2][num18];
					int num19 = num6;
					int num20 = num6 * 2;
					num6 += num20;
					m_fserrors[num3] = (short)(num12 + num6);
					num6 += num20;
					num12 = num9 + num6;
					num9 = num19;
					num6 += num20;
					num19 = num7;
					num20 = num7 * 2;
					num7 += num20;
					m_fserrors[num3 + 1] = (short)(num13 + num7);
					num7 += num20;
					num13 = num10 + num7;
					num10 = num19;
					num7 += num20;
					num19 = num8;
					num20 = num8 * 2;
					num8 += num20;
					m_fserrors[num3 + 2] = (short)(num14 + num8);
					num8 += num20;
					num14 = num11 + num8;
					num11 = num19;
					num8 += num20;
					num += num5;
					num2 += num4;
					num3 += num5;
				}
				m_fserrors[num3] = (short)num12;
				m_fserrors[num3 + 1] = (short)num13;
				m_fserrors[num3 + 2] = (short)num14;
			}
		}

		private void pass2_no_dither(byte[][] input_buf, int in_row, byte[][] output_buf, int out_row, int num_rows)
		{
			for (int i = 0; i < num_rows; i++)
			{
				int num = i + in_row;
				int num2 = 0;
				int num3 = 0;
				int num4 = out_row + i;
				for (int num5 = m_cinfo.m_output_width; num5 > 0; num5--)
				{
					int num6 = input_buf[num][num2] >> 3;
					num2++;
					int num7 = input_buf[num][num2] >> 2;
					num2++;
					int num8 = input_buf[num][num2] >> 3;
					num2++;
					int num9 = num6;
					int num10 = num7 * 32 + num8;
					if (m_histogram[num9][num10] == 0)
					{
						fill_inverse_cmap(num6, num7, num8);
					}
					output_buf[num4][num3] = (byte)(m_histogram[num9][num10] - 1);
					num3++;
				}
			}
		}

		private void finish_pass1()
		{
			m_cinfo.m_colormap = m_sv_colormap;
			select_colors(m_desired);
			m_needs_zeroed = true;
		}

		private void compute_color(box[] boxlist, int boxIndex, int icolor)
		{
			long num = 0L;
			long num2 = 0L;
			long num3 = 0L;
			long num4 = 0L;
			box box = boxlist[boxIndex];
			for (int i = box.c0min; i <= box.c0max; i++)
			{
				for (int j = box.c1min; j <= box.c1max; j++)
				{
					int num5 = j * 32 + box.c2min;
					for (int k = box.c2min; k <= box.c2max; k++)
					{
						long num6 = m_histogram[i][num5];
						num5++;
						if (num6 != 0)
						{
							num += num6;
							num2 += ((i << 3) + 4) * num6;
							num3 += ((j << 2) + 2) * num6;
							num4 += ((k << 3) + 4) * num6;
						}
					}
				}
			}
			m_cinfo.m_colormap[0][icolor] = (byte)((num2 + (num >> 1)) / num);
			m_cinfo.m_colormap[1][icolor] = (byte)((num3 + (num >> 1)) / num);
			m_cinfo.m_colormap[2][icolor] = (byte)((num4 + (num >> 1)) / num);
		}

		private void select_colors(int desired_colors)
		{
			box[] array = new box[desired_colors];
			int numboxes = 1;
			array[0].c0min = 0;
			array[0].c0max = 31;
			array[0].c1min = 0;
			array[0].c1max = 63;
			array[0].c2min = 0;
			array[0].c2max = 31;
			update_box(array, 0);
			numboxes = median_cut(array, numboxes, desired_colors);
			for (int i = 0; i < numboxes; i++)
			{
				compute_color(array, i, i);
			}
			m_cinfo.m_actual_number_of_colors = numboxes;
			m_cinfo.TRACEMS(1, J_MESSAGE_CODE.JTRC_QUANT_SELECTED, numboxes);
		}

		private int median_cut(box[] boxlist, int numboxes, int desired_colors)
		{
			while (numboxes < desired_colors)
			{
				int num = ((numboxes * 2 > desired_colors) ? find_biggest_volume(boxlist, numboxes) : find_biggest_color_pop(boxlist, numboxes));
				if (num == -1)
				{
					break;
				}
				boxlist[numboxes].c0max = boxlist[num].c0max;
				boxlist[numboxes].c1max = boxlist[num].c1max;
				boxlist[numboxes].c2max = boxlist[num].c2max;
				boxlist[numboxes].c0min = boxlist[num].c0min;
				boxlist[numboxes].c1min = boxlist[num].c1min;
				boxlist[numboxes].c2min = boxlist[num].c2min;
				int num2 = (boxlist[num].c0max - boxlist[num].c0min << 3) * 2;
				int num3 = (boxlist[num].c1max - boxlist[num].c1min << 2) * 3;
				int num4 = boxlist[num].c2max - boxlist[num].c2min << 3;
				int num5 = num3;
				int num6 = 1;
				if (num2 > num5)
				{
					num5 = num2;
					num6 = 0;
				}
				if (num4 > num5)
				{
					num6 = 2;
				}
				switch (num6)
				{
				case 0:
				{
					int num7 = (boxlist[num].c0max + boxlist[num].c0min) / 2;
					boxlist[num].c0max = num7;
					boxlist[numboxes].c0min = num7 + 1;
					break;
				}
				case 1:
				{
					int num7 = (boxlist[num].c1max + boxlist[num].c1min) / 2;
					boxlist[num].c1max = num7;
					boxlist[numboxes].c1min = num7 + 1;
					break;
				}
				case 2:
				{
					int num7 = (boxlist[num].c2max + boxlist[num].c2min) / 2;
					boxlist[num].c2max = num7;
					boxlist[numboxes].c2min = num7 + 1;
					break;
				}
				}
				update_box(boxlist, num);
				update_box(boxlist, numboxes);
				numboxes++;
			}
			return numboxes;
		}

		private static int find_biggest_color_pop(box[] boxlist, int numboxes)
		{
			long num = 0L;
			int result = -1;
			for (int i = 0; i < numboxes; i++)
			{
				if (boxlist[i].colorcount > num && boxlist[i].volume > 0)
				{
					result = i;
					num = boxlist[i].colorcount;
				}
			}
			return result;
		}

		private static int find_biggest_volume(box[] boxlist, int numboxes)
		{
			int num = 0;
			int result = -1;
			for (int i = 0; i < numboxes; i++)
			{
				if (boxlist[i].volume > num)
				{
					result = i;
					num = boxlist[i].volume;
				}
			}
			return result;
		}

		private void update_box(box[] boxlist, int boxIndex)
		{
			box box = boxlist[boxIndex];
			bool flag = false;
			if (box.c0max > box.c0min)
			{
				for (int i = box.c0min; i <= box.c0max; i++)
				{
					for (int j = box.c1min; j <= box.c1max; j++)
					{
						int num = j * 32 + box.c2min;
						for (int k = box.c2min; k <= box.c2max; k++)
						{
							if (m_histogram[i][num++] != 0)
							{
								box.c0min = i;
								flag = true;
								break;
							}
						}
						if (flag)
						{
							break;
						}
					}
					if (flag)
					{
						break;
					}
				}
			}
			bool flag2 = false;
			if (box.c0max > box.c0min)
			{
				for (int num2 = box.c0max; num2 >= box.c0min; num2--)
				{
					for (int l = box.c1min; l <= box.c1max; l++)
					{
						int num3 = l * 32 + box.c2min;
						for (int m = box.c2min; m <= box.c2max; m++)
						{
							if (m_histogram[num2][num3++] != 0)
							{
								box.c0max = num2;
								flag2 = true;
								break;
							}
						}
						if (flag2)
						{
							break;
						}
					}
					if (flag2)
					{
						break;
					}
				}
			}
			bool flag3 = false;
			if (box.c1max > box.c1min)
			{
				for (int n = box.c1min; n <= box.c1max; n++)
				{
					for (int num4 = box.c0min; num4 <= box.c0max; num4++)
					{
						int num5 = n * 32 + box.c2min;
						for (int num6 = box.c2min; num6 <= box.c2max; num6++)
						{
							if (m_histogram[num4][num5++] != 0)
							{
								box.c1min = n;
								flag3 = true;
								break;
							}
						}
						if (flag3)
						{
							break;
						}
					}
					if (flag3)
					{
						break;
					}
				}
			}
			bool flag4 = false;
			if (box.c1max > box.c1min)
			{
				for (int num7 = box.c1max; num7 >= box.c1min; num7--)
				{
					for (int num8 = box.c0min; num8 <= box.c0max; num8++)
					{
						int num9 = num7 * 32 + box.c2min;
						for (int num10 = box.c2min; num10 <= box.c2max; num10++)
						{
							if (m_histogram[num8][num9++] != 0)
							{
								box.c1max = num7;
								flag4 = true;
								break;
							}
						}
						if (flag4)
						{
							break;
						}
					}
					if (flag4)
					{
						break;
					}
				}
			}
			bool flag5 = false;
			if (box.c2max > box.c2min)
			{
				for (int num11 = box.c2min; num11 <= box.c2max; num11++)
				{
					for (int num12 = box.c0min; num12 <= box.c0max; num12++)
					{
						int num13 = box.c1min * 32 + num11;
						int num14 = box.c1min;
						while (num14 <= box.c1max)
						{
							if (m_histogram[num12][num13] != 0)
							{
								box.c2min = num11;
								flag5 = true;
								break;
							}
							num14++;
							num13 += 32;
						}
						if (flag5)
						{
							break;
						}
					}
					if (flag5)
					{
						break;
					}
				}
			}
			bool flag6 = false;
			if (box.c2max > box.c2min)
			{
				for (int num15 = box.c2max; num15 >= box.c2min; num15--)
				{
					for (int num16 = box.c0min; num16 <= box.c0max; num16++)
					{
						int num17 = box.c1min * 32 + num15;
						int num18 = box.c1min;
						while (num18 <= box.c1max)
						{
							if (m_histogram[num16][num17] != 0)
							{
								box.c2max = num15;
								flag6 = true;
								break;
							}
							num18++;
							num17 += 32;
						}
						if (flag6)
						{
							break;
						}
					}
					if (flag6)
					{
						break;
					}
				}
			}
			int num19 = (box.c0max - box.c0min << 3) * 2;
			int num20 = (box.c1max - box.c1min << 2) * 3;
			int num21 = box.c2max - box.c2min << 3;
			box.volume = num19 * num19 + num20 * num20 + num21 * num21;
			long num22 = 0L;
			for (int num23 = box.c0min; num23 <= box.c0max; num23++)
			{
				for (int num24 = box.c1min; num24 <= box.c1max; num24++)
				{
					int num25 = num24 * 32 + box.c2min;
					int num26 = box.c2min;
					while (num26 <= box.c2max)
					{
						if (m_histogram[num23][num25] != 0)
						{
							num22++;
						}
						num26++;
						num25++;
					}
				}
			}
			box.colorcount = num22;
			boxlist[boxIndex] = box;
		}

		private void init_error_limit()
		{
			m_error_limiter = new int[511];
			int num = 255;
			int num2 = 0;
			int i = 0;
			while (i < 16)
			{
				m_error_limiter[num + i] = num2;
				m_error_limiter[num - i] = -num2;
				i++;
				num2++;
			}
			for (; i < 48; i++)
			{
				m_error_limiter[num + i] = num2;
				m_error_limiter[num - i] = -num2;
				num2 += (((i & 1) != 0) ? 1 : 0);
			}
			for (; i <= 255; i++)
			{
				m_error_limiter[num + i] = num2;
				m_error_limiter[num - i] = -num2;
			}
		}

		private int find_nearby_colors(int minc0, int minc1, int minc2, byte[] colorlist)
		{
			int num = minc0 + 24;
			int num2 = minc0 + num >> 1;
			int num3 = minc1 + 28;
			int num4 = minc1 + num3 >> 1;
			int num5 = minc2 + 24;
			int num6 = minc2 + num5 >> 1;
			int num7 = int.MaxValue;
			int[] array = new int[256];
			for (int i = 0; i < m_cinfo.m_actual_number_of_colors; i++)
			{
				int num8 = m_cinfo.m_colormap[0][i];
				int num10;
				int num11;
				if (num8 < minc0)
				{
					int num9 = (num8 - minc0) * 2;
					num10 = num9 * num9;
					num9 = (num8 - num) * 2;
					num11 = num9 * num9;
				}
				else if (num8 > num)
				{
					int num12 = (num8 - num) * 2;
					num10 = num12 * num12;
					num12 = (num8 - minc0) * 2;
					num11 = num12 * num12;
				}
				else
				{
					num10 = 0;
					if (num8 <= num2)
					{
						int num13 = (num8 - num) * 2;
						num11 = num13 * num13;
					}
					else
					{
						int num14 = (num8 - minc0) * 2;
						num11 = num14 * num14;
					}
				}
				num8 = m_cinfo.m_colormap[1][i];
				if (num8 < minc1)
				{
					int num15 = (num8 - minc1) * 3;
					num10 += num15 * num15;
					num15 = (num8 - num3) * 3;
					num11 += num15 * num15;
				}
				else if (num8 > num3)
				{
					int num16 = (num8 - num3) * 3;
					num10 += num16 * num16;
					num16 = (num8 - minc1) * 3;
					num11 += num16 * num16;
				}
				else if (num8 <= num4)
				{
					int num17 = (num8 - num3) * 3;
					num11 += num17 * num17;
				}
				else
				{
					int num18 = (num8 - minc1) * 3;
					num11 += num18 * num18;
				}
				num8 = m_cinfo.m_colormap[2][i];
				if (num8 < minc2)
				{
					int num19 = num8 - minc2;
					num10 += num19 * num19;
					num19 = num8 - num5;
					num11 += num19 * num19;
				}
				else if (num8 > num5)
				{
					int num20 = num8 - num5;
					num10 += num20 * num20;
					num20 = num8 - minc2;
					num11 += num20 * num20;
				}
				else if (num8 <= num6)
				{
					int num21 = num8 - num5;
					num11 += num21 * num21;
				}
				else
				{
					int num22 = num8 - minc2;
					num11 += num22 * num22;
				}
				array[i] = num10;
				if (num11 < num7)
				{
					num7 = num11;
				}
			}
			int result = 0;
			for (int j = 0; j < m_cinfo.m_actual_number_of_colors; j++)
			{
				if (array[j] <= num7)
				{
					colorlist[result++] = (byte)j;
				}
			}
			return result;
		}

		private void find_best_colors(int minc0, int minc1, int minc2, int numcolors, byte[] colorlist, byte[] bestcolor)
		{
			int[] array = new int[128];
			int num = 0;
			for (int num2 = 127; num2 >= 0; num2--)
			{
				array[num] = int.MaxValue;
				num++;
			}
			for (int i = 0; i < numcolors; i++)
			{
				int num3 = colorlist[i];
				int num4 = (minc0 - m_cinfo.m_colormap[0][num3]) * 2;
				int num5 = num4 * num4;
				int num6 = (minc1 - m_cinfo.m_colormap[1][num3]) * 3;
				num5 += num6 * num6;
				int num7 = minc2 - m_cinfo.m_colormap[2][num3];
				num5 += num7 * num7;
				num4 = num4 * 32 + 256;
				num6 = num6 * 24 + 144;
				num7 = num7 * 16 + 64;
				num = 0;
				int num8 = 0;
				int num9 = num4;
				for (int num10 = 3; num10 >= 0; num10--)
				{
					int num11 = num5;
					int num12 = num6;
					for (int num13 = 7; num13 >= 0; num13--)
					{
						int num14 = num11;
						int num15 = num7;
						for (int num16 = 3; num16 >= 0; num16--)
						{
							if (num14 < array[num])
							{
								array[num] = num14;
								bestcolor[num8] = (byte)num3;
							}
							num14 += num15;
							num15 += 128;
							num++;
							num8++;
						}
						num11 += num12;
						num12 += 288;
					}
					num5 += num9;
					num9 += 512;
				}
			}
		}

		private void fill_inverse_cmap(int c0, int c1, int c2)
		{
			c0 >>= 2;
			c1 >>= 3;
			c2 >>= 2;
			int minc = (c0 << 5) + 4;
			int minc2 = (c1 << 5) + 2;
			int minc3 = (c2 << 5) + 4;
			byte[] colorlist = new byte[256];
			int numcolors = find_nearby_colors(minc, minc2, minc3, colorlist);
			byte[] array = new byte[128];
			find_best_colors(minc, minc2, minc3, numcolors, colorlist, array);
			c0 <<= 2;
			c1 <<= 3;
			c2 <<= 2;
			int num = 0;
			for (int i = 0; i < 4; i++)
			{
				for (int j = 0; j < 8; j++)
				{
					int num2 = (c1 + j) * 32 + c2;
					for (int k = 0; k < 4; k++)
					{
						m_histogram[c0 + i][num2] = (ushort)(array[num] + 1);
						num2++;
						num++;
					}
				}
			}
		}




	}
}
