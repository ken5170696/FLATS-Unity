using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class jpeg_forward_dct
	{
		private const int FAST_INTEGER_CONST_BITS = 8;

		private const int FAST_INTEGER_FIX_0_382683433 = 98;

		private const int FAST_INTEGER_FIX_0_541196100 = 139;

		private const int FAST_INTEGER_FIX_0_707106781 = 181;

		private const int FAST_INTEGER_FIX_1_306562965 = 334;

		private const int SLOW_INTEGER_CONST_BITS = 13;

		private const int SLOW_INTEGER_PASS1_BITS = 2;

		private const int SLOW_INTEGER_FIX_0_298631336 = 2446;

		private const int SLOW_INTEGER_FIX_0_390180644 = 3196;

		private const int SLOW_INTEGER_FIX_0_541196100 = 4433;

		private const int SLOW_INTEGER_FIX_0_765366865 = 6270;

		private const int SLOW_INTEGER_FIX_0_899976223 = 7373;

		private const int SLOW_INTEGER_FIX_1_175875602 = 9633;

		private const int SLOW_INTEGER_FIX_1_501321110 = 12299;

		private const int SLOW_INTEGER_FIX_1_847759065 = 15137;

		private const int SLOW_INTEGER_FIX_1_961570560 = 16069;

		private const int SLOW_INTEGER_FIX_2_053119869 = 16819;

		private const int SLOW_INTEGER_FIX_2_562915447 = 20995;

		private const int SLOW_INTEGER_FIX_3_072711026 = 25172;

		private const int CONST_BITS = 14;

		private static short[] aanscales = new short[64]
		{
			16384, 22725, 21407, 19266, 16384, 12873, 8867, 4520, 22725, 31521,
			29692, 26722, 22725, 17855, 12299, 6270, 21407, 29692, 27969, 25172,
			21407, 16819, 11585, 5906, 19266, 26722, 25172, 22654, 19266, 15137,
			10426, 5315, 16384, 22725, 21407, 19266, 16384, 12873, 8867, 4520,
			12873, 17855, 16819, 15137, 12873, 10114, 6967, 3552, 8867, 12299,
			11585, 10426, 8867, 6967, 4799, 2446, 4520, 6270, 5906, 5315,
			4520, 3552, 2446, 1247
		};

		private static double[] aanscalefactor = new double[8] { 1.0, 1.387039845, 1.306562965, 1.175875602, 1.0, 0.785694958, 0.5411961, 0.275899379 };

		private jpeg_compress_struct m_cinfo;

		private bool m_useSlowMethod;

		private bool m_useFloatMethod;

		private int[][] m_divisors;

		private float[][] m_float_divisors;

		public jpeg_forward_dct(jpeg_compress_struct cinfo)
		{
			m_divisors = new int[4][];
			m_float_divisors = new float[4][];

			m_cinfo = cinfo;
			switch (cinfo.m_dct_method)
			{
			case J_DCT_METHOD.JDCT_ISLOW:
				m_useFloatMethod = false;
				m_useSlowMethod = true;
				break;
			case J_DCT_METHOD.JDCT_IFAST:
				m_useFloatMethod = false;
				m_useSlowMethod = false;
				break;
			case J_DCT_METHOD.JDCT_FLOAT:
				m_useFloatMethod = true;
				break;
			default:
				cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOT_COMPILED);
				break;
			}
			for (int i = 0; i < 4; i++)
			{
				m_divisors[i] = null;
				m_float_divisors[i] = null;
			}
		}

		public virtual void start_pass()
		{
			for (int i = 0; i < m_cinfo.m_num_components; i++)
			{
				int quant_tbl_no = m_cinfo.Component_info[i].Quant_tbl_no;
				if (quant_tbl_no < 0 || quant_tbl_no >= 4 || m_cinfo.m_quant_tbl_ptrs[quant_tbl_no] == null)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_QUANT_TABLE, quant_tbl_no);
				}
				JQUANT_TBL jQUANT_TBL = m_cinfo.m_quant_tbl_ptrs[quant_tbl_no];
				int num = 0;
				switch (m_cinfo.m_dct_method)
				{
				case J_DCT_METHOD.JDCT_ISLOW:
					if (m_divisors[quant_tbl_no] == null)
					{
						m_divisors[quant_tbl_no] = new int[64];
					}
					for (num = 0; num < 64; num++)
					{
						m_divisors[quant_tbl_no][num] = jQUANT_TBL.quantval[num] << 3;
					}
					break;
				case J_DCT_METHOD.JDCT_IFAST:
					if (m_divisors[quant_tbl_no] == null)
					{
						m_divisors[quant_tbl_no] = new int[64];
					}
					for (num = 0; num < 64; num++)
					{
						m_divisors[quant_tbl_no][num] = JpegUtils.DESCALE(jQUANT_TBL.quantval[num] * aanscales[num], 11);
					}
					break;
				case J_DCT_METHOD.JDCT_FLOAT:
				{
					if (m_float_divisors[quant_tbl_no] == null)
					{
						m_float_divisors[quant_tbl_no] = new float[64];
					}
					float[] array = m_float_divisors[quant_tbl_no];
					num = 0;
					for (int j = 0; j < 8; j++)
					{
						for (int k = 0; k < 8; k++)
						{
							array[num] = (float)(1.0 / ((double)jQUANT_TBL.quantval[num] * aanscalefactor[j] * aanscalefactor[k] * 8.0));
							num++;
						}
					}
					break;
				}
				default:
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOT_COMPILED);
					break;
				}
			}
		}

		public virtual void forward_DCT(int quant_tbl_no, byte[][] sample_data, JBLOCK[] coef_blocks, int start_row, int start_col, int num_blocks)
		{
			if (m_useFloatMethod)
			{
				forwardDCTFloatImpl(quant_tbl_no, sample_data, coef_blocks, start_row, start_col, num_blocks);
			}
			else
			{
				forwardDCTImpl(quant_tbl_no, sample_data, coef_blocks, start_row, start_col, num_blocks);
			}
		}

		private void forwardDCTImpl(int quant_tbl_no, byte[][] sample_data, JBLOCK[] coef_blocks, int start_row, int start_col, int num_blocks)
		{
			int[] array = new int[64];
			int num = 0;
			while (num < num_blocks)
			{
				int num2 = 0;
				for (int i = 0; i < 8; i++)
				{
					for (int j = 0; j < 8; j++)
					{
						array[num2] = sample_data[start_row + i][start_col + j] - 128;
						num2++;
					}
				}
				if (m_useSlowMethod)
				{
					jpeg_fdct_islow(array);
				}
				else
				{
					jpeg_fdct_ifast(array);
				}
				for (int k = 0; k < 64; k++)
				{
					int num3 = m_divisors[quant_tbl_no][k];
					int num4 = array[k];
					if (num4 < 0)
					{
						num4 = -num4;
						num4 += num3 >> 1;
						num4 = ((num4 >= num3) ? (num4 / num3) : 0);
						num4 = -num4;
					}
					else
					{
						num4 += num3 >> 1;
						num4 = ((num4 >= num3) ? (num4 / num3) : 0);
					}
					coef_blocks[num][k] = (short)num4;
				}
				num++;
				start_col += 8;
			}
		}

		private void forwardDCTFloatImpl(int quant_tbl_no, byte[][] sample_data, JBLOCK[] coef_blocks, int start_row, int start_col, int num_blocks)
		{
			float[] array = new float[64];
			int num = 0;
			while (num < num_blocks)
			{
				int num2 = 0;
				for (int i = 0; i < 8; i++)
				{
					for (int j = 0; j < 8; j++)
					{
						array[num2] = sample_data[start_row + i][start_col + j] - 128;
						num2++;
					}
				}
				jpeg_fdct_float(array);
				for (int k = 0; k < 64; k++)
				{
					float num3 = array[k] * m_float_divisors[quant_tbl_no][k];
					coef_blocks[num][k] = (short)((int)(num3 + 16384.5f) - 16384);
				}
				num++;
				start_col += 8;
			}
		}

		private static void jpeg_fdct_float(float[] data)
		{
			int num = 0;
			for (int num2 = 7; num2 >= 0; num2--)
			{
				float num3 = data[num] + data[num + 7];
				float num4 = data[num] - data[num + 7];
				float num5 = data[num + 1] + data[num + 6];
				float num6 = data[num + 1] - data[num + 6];
				float num7 = data[num + 2] + data[num + 5];
				float num8 = data[num + 2] - data[num + 5];
				float num9 = data[num + 3] + data[num + 4];
				float num10 = data[num + 3] - data[num + 4];
				float num11 = num3 + num9;
				float num12 = num3 - num9;
				float num13 = num5 + num7;
				float num14 = num5 - num7;
				data[num] = num11 + num13;
				data[num + 4] = num11 - num13;
				float num15 = (num14 + num12) * 0.70710677f;
				data[num + 2] = num12 + num15;
				data[num + 6] = num12 - num15;
				num11 = num10 + num8;
				num13 = num8 + num6;
				num14 = num6 + num4;
				float num16 = (num11 - num14) * 0.38268343f;
				float num17 = 0.5411961f * num11 + num16;
				float num18 = 1.306563f * num14 + num16;
				float num19 = num13 * 0.70710677f;
				float num20 = num4 + num19;
				float num21 = num4 - num19;
				data[num + 5] = num21 + num17;
				data[num + 3] = num21 - num17;
				data[num + 1] = num20 + num18;
				data[num + 7] = num20 - num18;
				num += 8;
			}
			num = 0;
			for (int num22 = 7; num22 >= 0; num22--)
			{
				float num23 = data[num] + data[num + 56];
				float num24 = data[num] - data[num + 56];
				float num25 = data[num + 8] + data[num + 48];
				float num26 = data[num + 8] - data[num + 48];
				float num27 = data[num + 16] + data[num + 40];
				float num28 = data[num + 16] - data[num + 40];
				float num29 = data[num + 24] + data[num + 32];
				float num30 = data[num + 24] - data[num + 32];
				float num31 = num23 + num29;
				float num32 = num23 - num29;
				float num33 = num25 + num27;
				float num34 = num25 - num27;
				data[num] = num31 + num33;
				data[num + 32] = num31 - num33;
				float num35 = (num34 + num32) * 0.70710677f;
				data[num + 16] = num32 + num35;
				data[num + 48] = num32 - num35;
				num31 = num30 + num28;
				num33 = num28 + num26;
				num34 = num26 + num24;
				float num36 = (num31 - num34) * 0.38268343f;
				float num37 = 0.5411961f * num31 + num36;
				float num38 = 1.306563f * num34 + num36;
				float num39 = num33 * 0.70710677f;
				float num40 = num24 + num39;
				float num41 = num24 - num39;
				data[num + 40] = num41 + num37;
				data[num + 24] = num41 - num37;
				data[num + 8] = num40 + num38;
				data[num + 56] = num40 - num38;
				num++;
			}
		}

		private static void jpeg_fdct_ifast(int[] data)
		{
			int num = 0;
			for (int num2 = 7; num2 >= 0; num2--)
			{
				int num3 = data[num] + data[num + 7];
				int num4 = data[num] - data[num + 7];
				int num5 = data[num + 1] + data[num + 6];
				int num6 = data[num + 1] - data[num + 6];
				int num7 = data[num + 2] + data[num + 5];
				int num8 = data[num + 2] - data[num + 5];
				int num9 = data[num + 3] + data[num + 4];
				int num10 = data[num + 3] - data[num + 4];
				int num11 = num3 + num9;
				int num12 = num3 - num9;
				int num13 = num5 + num7;
				int num14 = num5 - num7;
				data[num] = num11 + num13;
				data[num + 4] = num11 - num13;
				int num15 = FAST_INTEGER_MULTIPLY(num14 + num12, 181);
				data[num + 2] = num12 + num15;
				data[num + 6] = num12 - num15;
				num11 = num10 + num8;
				num13 = num8 + num6;
				num14 = num6 + num4;
				int num16 = FAST_INTEGER_MULTIPLY(num11 - num14, 98);
				int num17 = FAST_INTEGER_MULTIPLY(num11, 139) + num16;
				int num18 = FAST_INTEGER_MULTIPLY(num14, 334) + num16;
				int num19 = FAST_INTEGER_MULTIPLY(num13, 181);
				int num20 = num4 + num19;
				int num21 = num4 - num19;
				data[num + 5] = num21 + num17;
				data[num + 3] = num21 - num17;
				data[num + 1] = num20 + num18;
				data[num + 7] = num20 - num18;
				num += 8;
			}
			num = 0;
			for (int num22 = 7; num22 >= 0; num22--)
			{
				int num23 = data[num] + data[num + 56];
				int num24 = data[num] - data[num + 56];
				int num25 = data[num + 8] + data[num + 48];
				int num26 = data[num + 8] - data[num + 48];
				int num27 = data[num + 16] + data[num + 40];
				int num28 = data[num + 16] - data[num + 40];
				int num29 = data[num + 24] + data[num + 32];
				int num30 = data[num + 24] - data[num + 32];
				int num31 = num23 + num29;
				int num32 = num23 - num29;
				int num33 = num25 + num27;
				int num34 = num25 - num27;
				data[num] = num31 + num33;
				data[num + 32] = num31 - num33;
				int num35 = FAST_INTEGER_MULTIPLY(num34 + num32, 181);
				data[num + 16] = num32 + num35;
				data[num + 48] = num32 - num35;
				num31 = num30 + num28;
				num33 = num28 + num26;
				num34 = num26 + num24;
				int num36 = FAST_INTEGER_MULTIPLY(num31 - num34, 98);
				int num37 = FAST_INTEGER_MULTIPLY(num31, 139) + num36;
				int num38 = FAST_INTEGER_MULTIPLY(num34, 334) + num36;
				int num39 = FAST_INTEGER_MULTIPLY(num33, 181);
				int num40 = num24 + num39;
				int num41 = num24 - num39;
				data[num + 40] = num41 + num37;
				data[num + 24] = num41 - num37;
				data[num + 8] = num40 + num38;
				data[num + 56] = num40 - num38;
				num++;
			}
		}

		private static void jpeg_fdct_islow(int[] data)
		{
			int num = 0;
			for (int num2 = 7; num2 >= 0; num2--)
			{
				int num3 = data[num] + data[num + 7];
				int num4 = data[num] - data[num + 7];
				int num5 = data[num + 1] + data[num + 6];
				int num6 = data[num + 1] - data[num + 6];
				int num7 = data[num + 2] + data[num + 5];
				int num8 = data[num + 2] - data[num + 5];
				int num9 = data[num + 3] + data[num + 4];
				int num10 = data[num + 3] - data[num + 4];
				int num11 = num3 + num9;
				int num12 = num3 - num9;
				int num13 = num5 + num7;
				int num14 = num5 - num7;
				data[num] = num11 + num13 << 2;
				data[num + 4] = num11 - num13 << 2;
				int num15 = (num14 + num12) * 4433;
				data[num + 2] = JpegUtils.DESCALE(num15 + num12 * 6270, 11);
				data[num + 6] = JpegUtils.DESCALE(num15 + num14 * -15137, 11);
				num15 = num10 + num4;
				int num16 = num8 + num6;
				int num17 = num10 + num6;
				int num18 = num8 + num4;
				int num19 = (num17 + num18) * 9633;
				num10 *= 2446;
				num8 *= 16819;
				num6 *= 25172;
				num4 *= 12299;
				num15 *= -7373;
				num16 *= -20995;
				num17 *= -16069;
				num18 *= -3196;
				num17 += num19;
				num18 += num19;
				data[num + 7] = JpegUtils.DESCALE(num10 + num15 + num17, 11);
				data[num + 5] = JpegUtils.DESCALE(num8 + num16 + num18, 11);
				data[num + 3] = JpegUtils.DESCALE(num6 + num16 + num17, 11);
				data[num + 1] = JpegUtils.DESCALE(num4 + num15 + num18, 11);
				num += 8;
			}
			num = 0;
			for (int num20 = 7; num20 >= 0; num20--)
			{
				int num21 = data[num] + data[num + 56];
				int num22 = data[num] - data[num + 56];
				int num23 = data[num + 8] + data[num + 48];
				int num24 = data[num + 8] - data[num + 48];
				int num25 = data[num + 16] + data[num + 40];
				int num26 = data[num + 16] - data[num + 40];
				int num27 = data[num + 24] + data[num + 32];
				int num28 = data[num + 24] - data[num + 32];
				int num29 = num21 + num27;
				int num30 = num21 - num27;
				int num31 = num23 + num25;
				int num32 = num23 - num25;
				data[num] = JpegUtils.DESCALE(num29 + num31, 2);
				data[num + 32] = JpegUtils.DESCALE(num29 - num31, 2);
				int num33 = (num32 + num30) * 4433;
				data[num + 16] = JpegUtils.DESCALE(num33 + num30 * 6270, 15);
				data[num + 48] = JpegUtils.DESCALE(num33 + num32 * -15137, 15);
				num33 = num28 + num22;
				int num34 = num26 + num24;
				int num35 = num28 + num24;
				int num36 = num26 + num22;
				int num37 = (num35 + num36) * 9633;
				num28 *= 2446;
				num26 *= 16819;
				num24 *= 25172;
				num22 *= 12299;
				num33 *= -7373;
				num34 *= -20995;
				num35 *= -16069;
				num36 *= -3196;
				num35 += num37;
				num36 += num37;
				data[num + 56] = JpegUtils.DESCALE(num28 + num33 + num35, 15);
				data[num + 40] = JpegUtils.DESCALE(num26 + num34 + num36, 15);
				data[num + 24] = JpegUtils.DESCALE(num24 + num34 + num35, 15);
				data[num + 8] = JpegUtils.DESCALE(num22 + num33 + num36, 15);
				num++;
			}
		}

		private static int FAST_INTEGER_MULTIPLY(int var, int c)
		{
			return JpegUtils.RIGHT_SHIFT(var * c, 8);
		}




	}
}
