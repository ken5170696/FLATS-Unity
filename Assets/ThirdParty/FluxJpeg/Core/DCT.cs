using System;
namespace FluxJpeg.Core
{
	internal class DCT
	{
		public const int N = 8;

		private float[] _temp;

		private static readonly float[,] c = buildC();

		private static readonly float[,] cT = buildCT();

		public int[][] quantum;

		public double[][] divisors;

		public double[] DivisorsLuminance;

		public double[] DivisorsChrominance;

		internal DCT()
		{
			_temp = new float[64];
			quantum = new int[2][];
			divisors = new double[2][];
			DivisorsLuminance = new double[64];
			DivisorsChrominance = new double[64];

		}

		private static float[,] buildC()
		{
			float[,] array = new float[8, 8];
			for (int i = 0; i < 8; i++)
			{
				for (int j = 0; j < 8; j++)
				{
					array[i, j] = ((i == 0) ? 0.35355338f : ((float)(0.5 * Math.Cos((2.0 * (double)j + 1.0) * (double)i * Math.PI / 16.0))));
				}
			}
			return array;
		}

		private static float[,] buildCT()
		{
			float[,] array = new float[8, 8];
			for (int i = 0; i < 8; i++)
			{
				for (int j = 0; j < 8; j++)
				{
					array[j, i] = c[i, j];
				}
			}
			return array;
		}

		public static void SetValueClipped(byte[,] arr, int i, int j, float val)
		{
			arr[i, j] = (byte)((!(val < 0f)) ? ((val > 255f) ? byte.MaxValue : ((byte)((double)val + 0.5))) : (byte)0);
		}

		internal byte[,] FastIDCT(float[] input)
		{
			byte[,] array = new byte[8, 8];
			float num = 0f;
			int num2 = 0;
			for (int i = 0; i < 8; i++)
			{
				for (int j = 0; j < 8; j++)
				{
					num = 0f;
					for (int k = 0; k < 8; k++)
					{
						num += input[i * 8 + k] * c[k, j];
					}
					_temp[num2++] = num;
				}
			}
			for (int l = 0; l < 8; l++)
			{
				for (int m = 0; m < 8; m++)
				{
					float num3 = 128f;
					for (int n = 0; n < 8; n++)
					{
						num3 += cT[l, n] * _temp[n * 8 + m];
					}
					if (num3 < 0f)
					{
						array[l, m] = 0;
					}
					else if (num3 > 255f)
					{
						array[l, m] = byte.MaxValue;
					}
					else
					{
						array[l, m] = (byte)((double)num3 + 0.5);
					}
				}
			}
			return array;
		}

		public DCT(int quality)
			: this()
		{
			Initialize(quality);
		}

		private void Initialize(int quality)
		{
			double[] array = new double[8] { 1.0, 1.387039845, 1.306562965, 1.175875602, 1.0, 0.785694958, 0.5411961, 0.275899379 };
			int num = ((quality <= 0) ? 1 : ((quality > 100) ? 100 : ((quality >= 50) ? (200 - quality * 2) : (5000 / quality))));
			int[] table = JpegQuantizationTable.K1Luminance.getScaledInstance((float)num / 100f, true).Table;
			int num2 = 0;
			for (int i = 0; i < 8; i++)
			{
				for (int j = 0; j < 8; j++)
				{
					DivisorsLuminance[num2] = 1.0 / ((double)table[num2] * array[i] * array[j] * 8.0);
					num2++;
				}
			}
			int[] table2 = JpegQuantizationTable.K2Chrominance.getScaledInstance((float)num / 100f, true).Table;
			num2 = 0;
			for (int i = 0; i < 8; i++)
			{
				for (int j = 0; j < 8; j++)
				{
					DivisorsChrominance[num2] = 1.0 / ((double)table2[num2] * array[i] * array[j] * 8.0);
					num2++;
				}
			}
			quantum[0] = table;
			divisors[0] = DivisorsLuminance;
			quantum[1] = table2;
			divisors[1] = DivisorsChrominance;
		}

		internal float[,] FastFDCT(float[,] input)
		{
			float[,] array = new float[8, 8];
			for (int i = 0; i < 8; i++)
			{
				for (int j = 0; j < 8; j++)
				{
					array[i, j] = input[i, j] - 128f;
				}
			}
			for (int i = 0; i < 8; i++)
			{
				float num = array[i, 0] + array[i, 7];
				float num2 = array[i, 0] - array[i, 7];
				float num3 = array[i, 1] + array[i, 6];
				float num4 = array[i, 1] - array[i, 6];
				float num5 = array[i, 2] + array[i, 5];
				float num6 = array[i, 2] - array[i, 5];
				float num7 = array[i, 3] + array[i, 4];
				float num8 = array[i, 3] - array[i, 4];
				float num9 = num + num7;
				float num10 = num - num7;
				float num11 = num3 + num5;
				float num12 = num3 - num5;
				array[i, 0] = num9 + num11;
				array[i, 4] = num9 - num11;
				float num13 = (num12 + num10) * 0.70710677f;
				array[i, 2] = num10 + num13;
				array[i, 6] = num10 - num13;
				num9 = num8 + num6;
				num11 = num6 + num4;
				num12 = num4 + num2;
				float num14 = (num9 - num12) * 0.38268343f;
				float num15 = 0.5411961f * num9 + num14;
				float num16 = 1.306563f * num12 + num14;
				float num17 = num11 * 0.70710677f;
				float num18 = num2 + num17;
				float num19 = num2 - num17;
				array[i, 5] = num19 + num15;
				array[i, 3] = num19 - num15;
				array[i, 1] = num18 + num16;
				array[i, 7] = num18 - num16;
			}
			for (int i = 0; i < 8; i++)
			{
				float num = array[0, i] + array[7, i];
				float num2 = array[0, i] - array[7, i];
				float num3 = array[1, i] + array[6, i];
				float num4 = array[1, i] - array[6, i];
				float num5 = array[2, i] + array[5, i];
				float num6 = array[2, i] - array[5, i];
				float num7 = array[3, i] + array[4, i];
				float num8 = array[3, i] - array[4, i];
				float num9 = num + num7;
				float num10 = num - num7;
				float num11 = num3 + num5;
				float num12 = num3 - num5;
				array[0, i] = num9 + num11;
				array[4, i] = num9 - num11;
				float num13 = (num12 + num10) * 0.70710677f;
				array[2, i] = num10 + num13;
				array[6, i] = num10 - num13;
				num9 = num8 + num6;
				num11 = num6 + num4;
				num12 = num4 + num2;
				float num14 = (num9 - num12) * 0.38268343f;
				float num15 = 0.5411961f * num9 + num14;
				float num16 = 1.306563f * num12 + num14;
				float num17 = num11 * 0.70710677f;
				float num18 = num2 + num17;
				float num19 = num2 - num17;
				array[5, i] = num19 + num15;
				array[3, i] = num19 - num15;
				array[1, i] = num18 + num16;
				array[7, i] = num18 - num16;
			}
			return array;
		}

		internal int[] QuantizeBlock(float[,] inputData, int code)
		{
			int[] array = new int[64];
			int num = 0;
			for (int i = 0; i < 8; i++)
			{
				for (int j = 0; j < 8; j++)
				{
					array[num] = (int)Math.Round((double)inputData[i, j] * divisors[code][num]);
					num++;
				}
			}
			return array;
		}




	}
}
