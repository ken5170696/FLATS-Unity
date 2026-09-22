using System;
namespace FluxJpeg.Core
{
	internal class JpegQuantizationTable
	{
		private int[] table;

		public static JpegQuantizationTable K1Luminance = new JpegQuantizationTable(new int[64]
		{
			16, 11, 10, 16, 24, 40, 51, 61, 12, 12,
			14, 19, 26, 58, 60, 55, 14, 13, 16, 24,
			40, 57, 69, 56, 14, 17, 22, 29, 51, 87,
			80, 62, 18, 22, 37, 56, 68, 109, 103, 77,
			24, 35, 55, 64, 81, 104, 113, 92, 49, 64,
			78, 87, 103, 121, 120, 101, 72, 92, 95, 98,
			112, 100, 103, 99
		}, false);

		public static JpegQuantizationTable K1Div2Luminance = K1Luminance.getScaledInstance(0.5f, true);

		public static JpegQuantizationTable K2Chrominance = new JpegQuantizationTable(new int[64]
		{
			17, 18, 24, 47, 99, 99, 99, 99, 18, 21,
			26, 66, 99, 99, 99, 99, 24, 26, 56, 99,
			99, 99, 99, 99, 47, 66, 99, 99, 99, 99,
			99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
			99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
			99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
			99, 99, 99, 99
		}, false);

		public static JpegQuantizationTable K2Div2Chrominance = K2Chrominance.getScaledInstance(0.5f, true);

		public int[] Table
		{
			get
			{
				return table;
			}
		}

		public JpegQuantizationTable(int[] table)
			: this(checkTable(table), true)
		{
		}

		private JpegQuantizationTable(int[] table, bool copy)
		{
			this.table = (copy ? ((int[])table.Clone()) : table);
		}

		private static int[] checkTable(int[] table)
		{
			if (table == null || table.Length != 64)
			{
				throw new ArgumentException("Invalid JPEG quantization table");
			}
			return table;
		}

		public JpegQuantizationTable getScaledInstance(float scaleFactor, bool forceBaseline)
		{
			int[] array = (int[])table.Clone();
			int num = (forceBaseline ? 255 : 32767);
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = (int)Math.Round(scaleFactor * (float)array[i]);
				if (array[i] < 1)
				{
					array[i] = 1;
				}
				else if (array[i] > num)
				{
					array[i] = num;
				}
			}
			return new JpegQuantizationTable(array, false);
		}




	}
}
