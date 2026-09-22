using System;
namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class JpegUtils
	{
		public static int[] jpeg_natural_order = new int[80]
		{
			0, 1, 8, 16, 9, 2, 3, 10, 17, 24,
			32, 25, 18, 11, 4, 5, 12, 19, 26, 33,
			40, 48, 41, 34, 27, 20, 13, 6, 7, 14,
			21, 28, 35, 42, 49, 56, 57, 50, 43, 36,
			29, 22, 15, 23, 30, 37, 44, 51, 58, 59,
			52, 45, 38, 31, 39, 46, 53, 60, 61, 54,
			47, 55, 62, 63, 63, 63, 63, 63, 63, 63,
			63, 63, 63, 63, 63, 63, 63, 63, 63, 63
		};

		public static int RIGHT_SHIFT(int x, int shft)
		{
			return x >> shft;
		}

		public static int DESCALE(int x, int n)
		{
			return RIGHT_SHIFT(x + (1 << n - 1), n);
		}

		public static int jdiv_round_up(int a, int b)
		{
			return (a + b - 1) / b;
		}

		public static int jround_up(int a, int b)
		{
			a += b - 1;
			return a - a % b;
		}

		public static void jcopy_sample_rows(ComponentBuffer input_array, int source_row, byte[][] output_array, int dest_row, int num_rows, int num_cols)
		{
			for (int i = 0; i < num_rows; i++)
			{
				Buffer.BlockCopy(input_array[source_row + i], 0, output_array[dest_row + i], 0, num_cols);
			}
		}

		public static void jcopy_sample_rows(ComponentBuffer input_array, int source_row, ComponentBuffer output_array, int dest_row, int num_rows, int num_cols)
		{
			for (int i = 0; i < num_rows; i++)
			{
				Buffer.BlockCopy(input_array[source_row + i], 0, output_array[dest_row + i], 0, num_cols);
			}
		}

		public static void jcopy_sample_rows(byte[][] input_array, int source_row, byte[][] output_array, int dest_row, int num_rows, int num_cols)
		{
			for (int i = 0; i < num_rows; i++)
			{
				Buffer.BlockCopy(input_array[source_row++], 0, output_array[dest_row++], 0, num_cols);
			}
		}

		public JpegUtils()
		{
		}




	}
}
