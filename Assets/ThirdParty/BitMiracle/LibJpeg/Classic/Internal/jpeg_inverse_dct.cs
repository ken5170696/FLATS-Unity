using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class jpeg_inverse_dct
	{
		private enum InverseMethod
		{
			Unknown,
			idct_1x1_method,
			idct_2x2_method,
			idct_4x4_method,
			idct_islow_method,
			idct_ifast_method,
			idct_float_method
		}

		private class multiplier_table
		{
			public int[] int_array = new int[64];

			public float[] float_array = new float[64];
		}

		private const int IFAST_SCALE_BITS = 2;

		private const int RANGE_MASK = 1023;

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

		private const int FAST_INTEGER_CONST_BITS = 8;

		private const int FAST_INTEGER_PASS1_BITS = 2;

		private const int FAST_INTEGER_FIX_1_082392200 = 277;

		private const int FAST_INTEGER_FIX_1_414213562 = 362;

		private const int FAST_INTEGER_FIX_1_847759065 = 473;

		private const int FAST_INTEGER_FIX_2_613125930 = 669;

		private const int REDUCED_CONST_BITS = 13;

		private const int REDUCED_PASS1_BITS = 2;

		private const int REDUCED_FIX_0_211164243 = 1730;

		private const int REDUCED_FIX_0_509795579 = 4176;

		private const int REDUCED_FIX_0_601344887 = 4926;

		private const int REDUCED_FIX_0_720959822 = 5906;

		private const int REDUCED_FIX_0_765366865 = 6270;

		private const int REDUCED_FIX_0_850430095 = 6967;

		private const int REDUCED_FIX_0_899976223 = 7373;

		private const int REDUCED_FIX_1_061594337 = 8697;

		private const int REDUCED_FIX_1_272758580 = 10426;

		private const int REDUCED_FIX_1_451774981 = 11893;

		private const int REDUCED_FIX_1_847759065 = 15137;

		private const int REDUCED_FIX_2_172734803 = 17799;

		private const int REDUCED_FIX_2_562915447 = 20995;

		private const int REDUCED_FIX_3_624509785 = 29692;

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

		private InverseMethod[] m_inverse_DCT_method;

		private multiplier_table[] m_dctTables;

		private jpeg_decompress_struct m_cinfo;

		private int[] m_cur_method;

		private ComponentBuffer m_componentBuffer;

		public jpeg_inverse_dct(jpeg_decompress_struct cinfo)
		{
			m_inverse_DCT_method = new InverseMethod[10];
			m_cur_method = new int[10];

			m_cinfo = cinfo;
			m_dctTables = new multiplier_table[cinfo.m_num_components];
			for (int i = 0; i < cinfo.m_num_components; i++)
			{
				m_dctTables[i] = new multiplier_table();
				m_cur_method[i] = -1;
			}
		}

		public void start_pass()
		{
			for (int i = 0; i < m_cinfo.m_num_components; i++)
			{
				jpeg_component_info jpeg_component_info = m_cinfo.Comp_info[i];
				InverseMethod inverseMethod = InverseMethod.Unknown;
				int num = 0;
				switch (jpeg_component_info.DCT_scaled_size)
				{
				case 1:
					inverseMethod = InverseMethod.idct_1x1_method;
					num = 0;
					break;
				case 2:
					inverseMethod = InverseMethod.idct_2x2_method;
					num = 0;
					break;
				case 4:
					inverseMethod = InverseMethod.idct_4x4_method;
					num = 0;
					break;
				case 8:
					switch (m_cinfo.m_dct_method)
					{
					case J_DCT_METHOD.JDCT_ISLOW:
						inverseMethod = InverseMethod.idct_islow_method;
						num = 0;
						break;
					case J_DCT_METHOD.JDCT_IFAST:
						inverseMethod = InverseMethod.idct_ifast_method;
						num = 1;
						break;
					case J_DCT_METHOD.JDCT_FLOAT:
						inverseMethod = InverseMethod.idct_float_method;
						num = 2;
						break;
					default:
						m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOT_COMPILED);
						break;
					}
					break;
				default:
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_DCTSIZE, jpeg_component_info.DCT_scaled_size);
					break;
				}
				m_inverse_DCT_method[i] = inverseMethod;
				if (!jpeg_component_info.component_needed || m_cur_method[i] == num || jpeg_component_info.quant_table == null)
				{
					continue;
				}
				m_cur_method[i] = num;
				switch ((J_DCT_METHOD)num)
				{
				case J_DCT_METHOD.JDCT_ISLOW:
				{
					int[] int_array = m_dctTables[i].int_array;
					for (int l = 0; l < 64; l++)
					{
						int_array[l] = jpeg_component_info.quant_table.quantval[l];
					}
					break;
				}
				case J_DCT_METHOD.JDCT_IFAST:
				{
					int[] int_array2 = m_dctTables[i].int_array;
					for (int m = 0; m < 64; m++)
					{
						int_array2[m] = JpegUtils.DESCALE(jpeg_component_info.quant_table.quantval[m] * aanscales[m], 12);
					}
					break;
				}
				case J_DCT_METHOD.JDCT_FLOAT:
				{
					float[] float_array = m_dctTables[i].float_array;
					int num2 = 0;
					for (int j = 0; j < 8; j++)
					{
						for (int k = 0; k < 8; k++)
						{
							float_array[num2] = (float)((double)jpeg_component_info.quant_table.quantval[num2] * aanscalefactor[j] * aanscalefactor[k]);
							num2++;
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

		public void inverse(int component_index, short[] coef_block, ComponentBuffer output_buf, int output_row, int output_col)
		{
			m_componentBuffer = output_buf;
			switch (m_inverse_DCT_method[component_index])
			{
			case InverseMethod.idct_1x1_method:
				jpeg_idct_1x1(component_index, coef_block, output_row, output_col);
				break;
			case InverseMethod.idct_2x2_method:
				jpeg_idct_2x2(component_index, coef_block, output_row, output_col);
				break;
			case InverseMethod.idct_4x4_method:
				jpeg_idct_4x4(component_index, coef_block, output_row, output_col);
				break;
			case InverseMethod.idct_islow_method:
				jpeg_idct_islow(component_index, coef_block, output_row, output_col);
				break;
			case InverseMethod.idct_ifast_method:
				jpeg_idct_ifast(component_index, coef_block, output_row, output_col);
				break;
			case InverseMethod.idct_float_method:
				jpeg_idct_float(component_index, coef_block, output_row, output_col);
				break;
			default:
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOT_COMPILED);
				break;
			}
		}

		private void jpeg_idct_islow(int component_index, short[] coef_block, int output_row, int output_col)
		{
			int[] array = new int[64];
			int num = 0;
			int[] int_array = m_dctTables[component_index].int_array;
			int num2 = 0;
			int num3 = 0;
			for (int num4 = 8; num4 > 0; num4--)
			{
				if (coef_block[num + 8] == 0 && coef_block[num + 16] == 0 && coef_block[num + 24] == 0 && coef_block[num + 32] == 0 && coef_block[num + 40] == 0 && coef_block[num + 48] == 0 && coef_block[num + 56] == 0)
				{
					array[num3 + 56] = (array[num3 + 48] = (array[num3 + 40] = (array[num3 + 32] = (array[num3 + 24] = (array[num3 + 16] = (array[num3 + 8] = (array[num3] = SLOW_INTEGER_DEQUANTIZE(coef_block[num], int_array[num2]) << 2)))))));
					num++;
					num2++;
					num3++;
				}
				else
				{
					int num5 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 16], int_array[num2 + 16]);
					int num6 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 48], int_array[num2 + 48]);
					int num7 = (num5 + num6) * 4433;
					int num8 = num7 + num6 * -15137;
					int num9 = num7 + num5 * 6270;
					num5 = SLOW_INTEGER_DEQUANTIZE(coef_block[num], int_array[num2]);
					num6 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 32], int_array[num2 + 32]);
					int num10 = num5 + num6 << 13;
					int num11 = num5 - num6 << 13;
					int num12 = num10 + num9;
					int num13 = num10 - num9;
					int num14 = num11 + num8;
					int num15 = num11 - num8;
					num10 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 56], int_array[num2 + 56]);
					num11 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 40], int_array[num2 + 40]);
					num8 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 24], int_array[num2 + 24]);
					num9 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 8], int_array[num2 + 8]);
					num7 = num10 + num9;
					num5 = num11 + num8;
					num6 = num10 + num8;
					int num16 = num11 + num9;
					int num17 = (num6 + num16) * 9633;
					num10 *= 2446;
					num11 *= 16819;
					num8 *= 25172;
					num9 *= 12299;
					num7 *= -7373;
					num5 *= -20995;
					num6 *= -16069;
					num16 *= -3196;
					num6 += num17;
					num16 += num17;
					num10 += num7 + num6;
					num11 += num5 + num16;
					num8 += num5 + num6;
					num9 += num7 + num16;
					array[num3] = JpegUtils.DESCALE(num12 + num9, 11);
					array[num3 + 56] = JpegUtils.DESCALE(num12 - num9, 11);
					array[num3 + 8] = JpegUtils.DESCALE(num14 + num8, 11);
					array[num3 + 48] = JpegUtils.DESCALE(num14 - num8, 11);
					array[num3 + 16] = JpegUtils.DESCALE(num15 + num11, 11);
					array[num3 + 40] = JpegUtils.DESCALE(num15 - num11, 11);
					array[num3 + 24] = JpegUtils.DESCALE(num13 + num10, 11);
					array[num3 + 32] = JpegUtils.DESCALE(num13 - num10, 11);
					num++;
					num2++;
					num3++;
				}
			}
			num3 = 0;
			byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
			int num18 = m_cinfo.m_sampleRangeLimitOffset + 128;
			for (int i = 0; i < 8; i++)
			{
				int i2 = output_row + i;
				if (array[num3 + 1] == 0 && array[num3 + 2] == 0 && array[num3 + 3] == 0 && array[num3 + 4] == 0 && array[num3 + 5] == 0 && array[num3 + 6] == 0 && array[num3 + 7] == 0)
				{
					byte b = sample_range_limit[(num18 + JpegUtils.DESCALE(array[num3], 5)) & 0x3FF];
					m_componentBuffer[i2][output_col] = b;
					m_componentBuffer[i2][output_col + 1] = b;
					m_componentBuffer[i2][output_col + 2] = b;
					m_componentBuffer[i2][output_col + 3] = b;
					m_componentBuffer[i2][output_col + 4] = b;
					m_componentBuffer[i2][output_col + 5] = b;
					m_componentBuffer[i2][output_col + 6] = b;
					m_componentBuffer[i2][output_col + 7] = b;
					num3 += 8;
					continue;
				}
				int num19 = array[num3 + 2];
				int num20 = array[num3 + 6];
				int num21 = (num19 + num20) * 4433;
				int num22 = num21 + num20 * -15137;
				int num23 = num21 + num19 * 6270;
				int num24 = array[num3] + array[num3 + 4] << 13;
				int num25 = array[num3] - array[num3 + 4] << 13;
				int num26 = num24 + num23;
				int num27 = num24 - num23;
				int num28 = num25 + num22;
				int num29 = num25 - num22;
				num24 = array[num3 + 7];
				num25 = array[num3 + 5];
				num22 = array[num3 + 3];
				num23 = array[num3 + 1];
				num21 = num24 + num23;
				num19 = num25 + num22;
				num20 = num24 + num22;
				int num30 = num25 + num23;
				int num31 = (num20 + num30) * 9633;
				num24 *= 2446;
				num25 *= 16819;
				num22 *= 25172;
				num23 *= 12299;
				num21 *= -7373;
				num19 *= -20995;
				num20 *= -16069;
				num30 *= -3196;
				num20 += num31;
				num30 += num31;
				num24 += num21 + num20;
				num25 += num19 + num30;
				num22 += num19 + num20;
				num23 += num21 + num30;
				m_componentBuffer[i2][output_col] = sample_range_limit[(num18 + JpegUtils.DESCALE(num26 + num23, 18)) & 0x3FF];
				m_componentBuffer[i2][output_col + 7] = sample_range_limit[(num18 + JpegUtils.DESCALE(num26 - num23, 18)) & 0x3FF];
				m_componentBuffer[i2][output_col + 1] = sample_range_limit[(num18 + JpegUtils.DESCALE(num28 + num22, 18)) & 0x3FF];
				m_componentBuffer[i2][output_col + 6] = sample_range_limit[(num18 + JpegUtils.DESCALE(num28 - num22, 18)) & 0x3FF];
				m_componentBuffer[i2][output_col + 2] = sample_range_limit[(num18 + JpegUtils.DESCALE(num29 + num25, 18)) & 0x3FF];
				m_componentBuffer[i2][output_col + 5] = sample_range_limit[(num18 + JpegUtils.DESCALE(num29 - num25, 18)) & 0x3FF];
				m_componentBuffer[i2][output_col + 3] = sample_range_limit[(num18 + JpegUtils.DESCALE(num27 + num24, 18)) & 0x3FF];
				m_componentBuffer[i2][output_col + 4] = sample_range_limit[(num18 + JpegUtils.DESCALE(num27 - num24, 18)) & 0x3FF];
				num3 += 8;
			}
		}

		private static int SLOW_INTEGER_DEQUANTIZE(int coef, int quantval)
		{
			return coef * quantval;
		}

		private void jpeg_idct_ifast(int component_index, short[] coef_block, int output_row, int output_col)
		{
			int[] array = new int[64];
			int num = 0;
			int num2 = 0;
			int[] int_array = m_dctTables[component_index].int_array;
			int num3 = 0;
			for (int num4 = 8; num4 > 0; num4--)
			{
				if (coef_block[num + 8] == 0 && coef_block[num + 16] == 0 && coef_block[num + 24] == 0 && coef_block[num + 32] == 0 && coef_block[num + 40] == 0 && coef_block[num + 48] == 0 && coef_block[num + 56] == 0)
				{
					array[num2 + 56] = (array[num2 + 48] = (array[num2 + 40] = (array[num2 + 32] = (array[num2 + 24] = (array[num2 + 16] = (array[num2 + 8] = (array[num2] = FAST_INTEGER_DEQUANTIZE(coef_block[num], int_array[num3]))))))));
					num++;
					num3++;
					num2++;
				}
				else
				{
					int num5 = FAST_INTEGER_DEQUANTIZE(coef_block[num], int_array[num3]);
					int num6 = FAST_INTEGER_DEQUANTIZE(coef_block[num + 16], int_array[num3 + 16]);
					int num7 = FAST_INTEGER_DEQUANTIZE(coef_block[num + 32], int_array[num3 + 32]);
					int num8 = FAST_INTEGER_DEQUANTIZE(coef_block[num + 48], int_array[num3 + 48]);
					int num9 = num5 + num7;
					int num10 = num5 - num7;
					int num11 = num6 + num8;
					int num12 = FAST_INTEGER_MULTIPLY(num6 - num8, 362) - num11;
					num5 = num9 + num11;
					num8 = num9 - num11;
					num6 = num10 + num12;
					num7 = num10 - num12;
					int num13 = FAST_INTEGER_DEQUANTIZE(coef_block[num + 8], int_array[num3 + 8]);
					int num14 = FAST_INTEGER_DEQUANTIZE(coef_block[num + 24], int_array[num3 + 24]);
					int num15 = FAST_INTEGER_DEQUANTIZE(coef_block[num + 40], int_array[num3 + 40]);
					int num16 = FAST_INTEGER_DEQUANTIZE(coef_block[num + 56], int_array[num3 + 56]);
					int num17 = num15 + num14;
					int num18 = num15 - num14;
					int num19 = num13 + num16;
					int num20 = num13 - num16;
					num16 = num19 + num17;
					num10 = FAST_INTEGER_MULTIPLY(num19 - num17, 362);
					int num21 = FAST_INTEGER_MULTIPLY(num18 + num20, 473);
					num9 = FAST_INTEGER_MULTIPLY(num20, 277) - num21;
					num12 = FAST_INTEGER_MULTIPLY(num18, -669) + num21;
					num15 = num12 - num16;
					num14 = num10 - num15;
					num13 = num9 + num14;
					array[num2] = num5 + num16;
					array[num2 + 56] = num5 - num16;
					array[num2 + 8] = num6 + num15;
					array[num2 + 48] = num6 - num15;
					array[num2 + 16] = num7 + num14;
					array[num2 + 40] = num7 - num14;
					array[num2 + 32] = num8 + num13;
					array[num2 + 24] = num8 - num13;
					num++;
					num3++;
					num2++;
				}
			}
			num2 = 0;
			byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
			int num22 = m_cinfo.m_sampleRangeLimitOffset + 128;
			for (int i = 0; i < 8; i++)
			{
				int i2 = output_row + i;
				if (array[num2 + 1] == 0 && array[num2 + 2] == 0 && array[num2 + 3] == 0 && array[num2 + 4] == 0 && array[num2 + 5] == 0 && array[num2 + 6] == 0 && array[num2 + 7] == 0)
				{
					byte b = sample_range_limit[(num22 + FAST_INTEGER_IDESCALE(array[num2], 5)) & 0x3FF];
					m_componentBuffer[i2][output_col] = b;
					m_componentBuffer[i2][output_col + 1] = b;
					m_componentBuffer[i2][output_col + 2] = b;
					m_componentBuffer[i2][output_col + 3] = b;
					m_componentBuffer[i2][output_col + 4] = b;
					m_componentBuffer[i2][output_col + 5] = b;
					m_componentBuffer[i2][output_col + 6] = b;
					m_componentBuffer[i2][output_col + 7] = b;
					num2 += 8;
					continue;
				}
				int num23 = array[num2] + array[num2 + 4];
				int num24 = array[num2] - array[num2 + 4];
				int num25 = array[num2 + 2] + array[num2 + 6];
				int num26 = FAST_INTEGER_MULTIPLY(array[num2 + 2] - array[num2 + 6], 362) - num25;
				int num27 = num23 + num25;
				int num28 = num23 - num25;
				int num29 = num24 + num26;
				int num30 = num24 - num26;
				int num31 = array[num2 + 5] + array[num2 + 3];
				int num32 = array[num2 + 5] - array[num2 + 3];
				int num33 = array[num2 + 1] + array[num2 + 7];
				int num34 = array[num2 + 1] - array[num2 + 7];
				int num35 = num33 + num31;
				num24 = FAST_INTEGER_MULTIPLY(num33 - num31, 362);
				int num36 = FAST_INTEGER_MULTIPLY(num32 + num34, 473);
				num23 = FAST_INTEGER_MULTIPLY(num34, 277) - num36;
				num26 = FAST_INTEGER_MULTIPLY(num32, -669) + num36;
				int num37 = num26 - num35;
				int num38 = num24 - num37;
				int num39 = num23 + num38;
				m_componentBuffer[i2][output_col] = sample_range_limit[(num22 + FAST_INTEGER_IDESCALE(num27 + num35, 5)) & 0x3FF];
				m_componentBuffer[i2][output_col + 7] = sample_range_limit[(num22 + FAST_INTEGER_IDESCALE(num27 - num35, 5)) & 0x3FF];
				m_componentBuffer[i2][output_col + 1] = sample_range_limit[(num22 + FAST_INTEGER_IDESCALE(num29 + num37, 5)) & 0x3FF];
				m_componentBuffer[i2][output_col + 6] = sample_range_limit[(num22 + FAST_INTEGER_IDESCALE(num29 - num37, 5)) & 0x3FF];
				m_componentBuffer[i2][output_col + 2] = sample_range_limit[(num22 + FAST_INTEGER_IDESCALE(num30 + num38, 5)) & 0x3FF];
				m_componentBuffer[i2][output_col + 5] = sample_range_limit[(num22 + FAST_INTEGER_IDESCALE(num30 - num38, 5)) & 0x3FF];
				m_componentBuffer[i2][output_col + 4] = sample_range_limit[(num22 + FAST_INTEGER_IDESCALE(num28 + num39, 5)) & 0x3FF];
				m_componentBuffer[i2][output_col + 3] = sample_range_limit[(num22 + FAST_INTEGER_IDESCALE(num28 - num39, 5)) & 0x3FF];
				num2 += 8;
			}
		}

		private static int FAST_INTEGER_MULTIPLY(int var, int c)
		{
			return JpegUtils.RIGHT_SHIFT(var * c, 8);
		}

		private static int FAST_INTEGER_DEQUANTIZE(short coef, int quantval)
		{
			return coef * quantval;
		}

		private static int FAST_INTEGER_IRIGHT_SHIFT(int x, int shft)
		{
			return x >> shft;
		}

		private static int FAST_INTEGER_IDESCALE(int x, int n)
		{
			return FAST_INTEGER_IRIGHT_SHIFT(x, n);
		}

		private void jpeg_idct_float(int component_index, short[] coef_block, int output_row, int output_col)
		{
			float[] array = new float[64];
			int num = 0;
			int num2 = 0;
			float[] float_array = m_dctTables[component_index].float_array;
			int num3 = 0;
			for (int num4 = 8; num4 > 0; num4--)
			{
				if (coef_block[num + 8] == 0 && coef_block[num + 16] == 0 && coef_block[num + 24] == 0 && coef_block[num + 32] == 0 && coef_block[num + 40] == 0 && coef_block[num + 48] == 0 && coef_block[num + 56] == 0)
				{
					array[num2 + 56] = (array[num2 + 48] = (array[num2 + 40] = (array[num2 + 32] = (array[num2 + 24] = (array[num2 + 16] = (array[num2 + 8] = (array[num2] = FLOAT_DEQUANTIZE(coef_block[num], float_array[num3]))))))));
					num++;
					num3++;
					num2++;
				}
				else
				{
					float num5 = FLOAT_DEQUANTIZE(coef_block[num], float_array[num3]);
					float num6 = FLOAT_DEQUANTIZE(coef_block[num + 16], float_array[num3 + 16]);
					float num7 = FLOAT_DEQUANTIZE(coef_block[num + 32], float_array[num3 + 32]);
					float num8 = FLOAT_DEQUANTIZE(coef_block[num + 48], float_array[num3 + 48]);
					float num9 = num5 + num7;
					float num10 = num5 - num7;
					float num11 = num6 + num8;
					float num12 = (num6 - num8) * 1.4142135f - num11;
					num5 = num9 + num11;
					num8 = num9 - num11;
					num6 = num10 + num12;
					num7 = num10 - num12;
					float num13 = FLOAT_DEQUANTIZE(coef_block[num + 8], float_array[num3 + 8]);
					float num14 = FLOAT_DEQUANTIZE(coef_block[num + 24], float_array[num3 + 24]);
					float num15 = FLOAT_DEQUANTIZE(coef_block[num + 40], float_array[num3 + 40]);
					float num16 = FLOAT_DEQUANTIZE(coef_block[num + 56], float_array[num3 + 56]);
					float num17 = num15 + num14;
					float num18 = num15 - num14;
					float num19 = num13 + num16;
					float num20 = num13 - num16;
					num16 = num19 + num17;
					num10 = (num19 - num17) * 1.4142135f;
					float num21 = (num18 + num20) * 1.847759f;
					num9 = 1.0823922f * num20 - num21;
					num12 = -2.613126f * num18 + num21;
					num15 = num12 - num16;
					num14 = num10 - num15;
					num13 = num9 + num14;
					array[num2] = num5 + num16;
					array[num2 + 56] = num5 - num16;
					array[num2 + 8] = num6 + num15;
					array[num2 + 48] = num6 - num15;
					array[num2 + 16] = num7 + num14;
					array[num2 + 40] = num7 - num14;
					array[num2 + 32] = num8 + num13;
					array[num2 + 24] = num8 - num13;
					num++;
					num3++;
					num2++;
				}
			}
			num2 = 0;
			byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
			int num22 = m_cinfo.m_sampleRangeLimitOffset + 128;
			for (int i = 0; i < 8; i++)
			{
				float num23 = array[num2] + array[num2 + 4];
				float num24 = array[num2] - array[num2 + 4];
				float num25 = array[num2 + 2] + array[num2 + 6];
				float num26 = (array[num2 + 2] - array[num2 + 6]) * 1.4142135f - num25;
				float num27 = num23 + num25;
				float num28 = num23 - num25;
				float num29 = num24 + num26;
				float num30 = num24 - num26;
				float num31 = array[num2 + 5] + array[num2 + 3];
				float num32 = array[num2 + 5] - array[num2 + 3];
				float num33 = array[num2 + 1] + array[num2 + 7];
				float num34 = array[num2 + 1] - array[num2 + 7];
				float num35 = num33 + num31;
				num24 = (num33 - num31) * 1.4142135f;
				float num36 = (num32 + num34) * 1.847759f;
				num23 = 1.0823922f * num34 - num36;
				num26 = -2.613126f * num32 + num36;
				float num37 = num26 - num35;
				float num38 = num24 - num37;
				float num39 = num23 + num38;
				int i2 = output_row + i;
				m_componentBuffer[i2][output_col] = sample_range_limit[(num22 + JpegUtils.DESCALE((int)(num27 + num35), 3)) & 0x3FF];
				m_componentBuffer[i2][output_col + 7] = sample_range_limit[(num22 + JpegUtils.DESCALE((int)(num27 - num35), 3)) & 0x3FF];
				m_componentBuffer[i2][output_col + 1] = sample_range_limit[(num22 + JpegUtils.DESCALE((int)(num29 + num37), 3)) & 0x3FF];
				m_componentBuffer[i2][output_col + 6] = sample_range_limit[(num22 + JpegUtils.DESCALE((int)(num29 - num37), 3)) & 0x3FF];
				m_componentBuffer[i2][output_col + 2] = sample_range_limit[(num22 + JpegUtils.DESCALE((int)(num30 + num38), 3)) & 0x3FF];
				m_componentBuffer[i2][output_col + 5] = sample_range_limit[(num22 + JpegUtils.DESCALE((int)(num30 - num38), 3)) & 0x3FF];
				m_componentBuffer[i2][output_col + 4] = sample_range_limit[(num22 + JpegUtils.DESCALE((int)(num28 + num39), 3)) & 0x3FF];
				m_componentBuffer[i2][output_col + 3] = sample_range_limit[(num22 + JpegUtils.DESCALE((int)(num28 - num39), 3)) & 0x3FF];
				num2 += 8;
			}
		}

		private static float FLOAT_DEQUANTIZE(short coef, float quantval)
		{
			return (float)coef * quantval;
		}

		private void jpeg_idct_4x4(int component_index, short[] coef_block, int output_row, int output_col)
		{
			int[] array = new int[32];
			int num = 0;
			int num2 = 0;
			int[] int_array = m_dctTables[component_index].int_array;
			int num3 = 0;
			for (int num4 = 8; num4 > 0; num4--)
			{
				if (num4 != 4)
				{
					if (coef_block[num + 8] == 0 && coef_block[num + 16] == 0 && coef_block[num + 24] == 0 && coef_block[num + 40] == 0 && coef_block[num + 48] == 0 && coef_block[num + 56] == 0)
					{
						array[num2 + 24] = (array[num2 + 16] = (array[num2 + 8] = (array[num2] = REDUCED_DEQUANTIZE(coef_block[num], int_array[num3]) << 2)));
					}
					else
					{
						int num5 = REDUCED_DEQUANTIZE(coef_block[num], int_array[num3]);
						num5 <<= 14;
						int num6 = REDUCED_DEQUANTIZE(coef_block[num + 16], int_array[num3 + 16]);
						int num7 = REDUCED_DEQUANTIZE(coef_block[num + 48], int_array[num3 + 48]);
						int num8 = num6 * 15137 + num7 * -6270;
						int num9 = num5 + num8;
						int num10 = num5 - num8;
						int num11 = REDUCED_DEQUANTIZE(coef_block[num + 56], int_array[num3 + 56]);
						num6 = REDUCED_DEQUANTIZE(coef_block[num + 40], int_array[num3 + 40]);
						num7 = REDUCED_DEQUANTIZE(coef_block[num + 24], int_array[num3 + 24]);
						int num12 = REDUCED_DEQUANTIZE(coef_block[num + 8], int_array[num3 + 8]);
						num5 = num11 * -1730 + num6 * 11893 + num7 * -17799 + num12 * 8697;
						num8 = num11 * -4176 + num6 * -4926 + num7 * 7373 + num12 * 20995;
						array[num2] = JpegUtils.DESCALE(num9 + num8, 12);
						array[num2 + 24] = JpegUtils.DESCALE(num9 - num8, 12);
						array[num2 + 8] = JpegUtils.DESCALE(num10 + num5, 12);
						array[num2 + 16] = JpegUtils.DESCALE(num10 - num5, 12);
					}
				}
				num++;
				num3++;
				num2++;
			}
			byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
			int num13 = m_cinfo.m_sampleRangeLimitOffset + 128;
			num2 = 0;
			for (int i = 0; i < 4; i++)
			{
				int i2 = output_row + i;
				if (array[num2 + 1] == 0 && array[num2 + 2] == 0 && array[num2 + 3] == 0 && array[num2 + 5] == 0 && array[num2 + 6] == 0 && array[num2 + 7] == 0)
				{
					byte b = sample_range_limit[(num13 + JpegUtils.DESCALE(array[num2], 5)) & 0x3FF];
					m_componentBuffer[i2][output_col] = b;
					m_componentBuffer[i2][output_col + 1] = b;
					m_componentBuffer[i2][output_col + 2] = b;
					m_componentBuffer[i2][output_col + 3] = b;
					num2 += 8;
					continue;
				}
				int num14 = array[num2] << 14;
				int num15 = array[num2 + 2] * 15137 + array[num2 + 6] * -6270;
				int num16 = num14 + num15;
				int num17 = num14 - num15;
				int num18 = array[num2 + 7];
				int num19 = array[num2 + 5];
				int num20 = array[num2 + 3];
				int num21 = array[num2 + 1];
				num14 = num18 * -1730 + num19 * 11893 + num20 * -17799 + num21 * 8697;
				num15 = num18 * -4176 + num19 * -4926 + num20 * 7373 + num21 * 20995;
				m_componentBuffer[i2][output_col] = sample_range_limit[(num13 + JpegUtils.DESCALE(num16 + num15, 19)) & 0x3FF];
				m_componentBuffer[i2][output_col + 3] = sample_range_limit[(num13 + JpegUtils.DESCALE(num16 - num15, 19)) & 0x3FF];
				m_componentBuffer[i2][output_col + 1] = sample_range_limit[(num13 + JpegUtils.DESCALE(num17 + num14, 19)) & 0x3FF];
				m_componentBuffer[i2][output_col + 2] = sample_range_limit[(num13 + JpegUtils.DESCALE(num17 - num14, 19)) & 0x3FF];
				num2 += 8;
			}
		}

		private void jpeg_idct_2x2(int component_index, short[] coef_block, int output_row, int output_col)
		{
			int[] array = new int[16];
			int num = 0;
			int num2 = 0;
			int[] int_array = m_dctTables[component_index].int_array;
			int num3 = 0;
			for (int num4 = 8; num4 > 0; num4--)
			{
				if (num4 != 6 && num4 != 4 && num4 != 2)
				{
					if (coef_block[num + 8] == 0 && coef_block[num + 24] == 0 && coef_block[num + 40] == 0 && coef_block[num + 56] == 0)
					{
						array[num2 + 8] = (array[num2] = REDUCED_DEQUANTIZE(coef_block[num], int_array[num3]) << 2);
					}
					else
					{
						int num5 = REDUCED_DEQUANTIZE(coef_block[num], int_array[num3]);
						int num6 = num5 << 15;
						num5 = REDUCED_DEQUANTIZE(coef_block[num + 56], int_array[num3 + 56]);
						int num7 = num5 * -5906;
						num5 = REDUCED_DEQUANTIZE(coef_block[num + 40], int_array[num3 + 40]);
						num7 += num5 * 6967;
						num5 = REDUCED_DEQUANTIZE(coef_block[num + 24], int_array[num3 + 24]);
						num7 += num5 * -10426;
						num5 = REDUCED_DEQUANTIZE(coef_block[num + 8], int_array[num3 + 8]);
						num7 += num5 * 29692;
						array[num2] = JpegUtils.DESCALE(num6 + num7, 13);
						array[num2 + 8] = JpegUtils.DESCALE(num6 - num7, 13);
					}
				}
				num++;
				num3++;
				num2++;
			}
			num2 = 0;
			byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
			int num8 = m_cinfo.m_sampleRangeLimitOffset + 128;
			for (int i = 0; i < 2; i++)
			{
				int i2 = output_row + i;
				if (array[num2 + 1] == 0 && array[num2 + 3] == 0 && array[num2 + 5] == 0 && array[num2 + 7] == 0)
				{
					byte b = sample_range_limit[(num8 + JpegUtils.DESCALE(array[num2], 5)) & 0x3FF];
					m_componentBuffer[i2][output_col] = b;
					m_componentBuffer[i2][output_col + 1] = b;
					num2 += 8;
				}
				else
				{
					int num9 = array[num2] << 15;
					int num10 = array[num2 + 7] * -5906 + array[num2 + 5] * 6967 + array[num2 + 3] * -10426 + array[num2 + 1] * 29692;
					m_componentBuffer[i2][output_col] = sample_range_limit[(num8 + JpegUtils.DESCALE(num9 + num10, 20)) & 0x3FF];
					m_componentBuffer[i2][output_col + 1] = sample_range_limit[(num8 + JpegUtils.DESCALE(num9 - num10, 20)) & 0x3FF];
					num2 += 8;
				}
			}
		}

		private void jpeg_idct_1x1(int component_index, short[] coef_block, int output_row, int output_col)
		{
			int[] int_array = m_dctTables[component_index].int_array;
			int x = REDUCED_DEQUANTIZE(coef_block[0], int_array[0]);
			x = JpegUtils.DESCALE(x, 3);
			byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
			int num = m_cinfo.m_sampleRangeLimitOffset + 128;
			m_componentBuffer[output_row][output_col] = sample_range_limit[(num + x) & 0x3FF];
		}

		private static int REDUCED_DEQUANTIZE(short coef, int quantval)
		{
			return coef * quantval;
		}




	}
}
