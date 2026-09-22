using System;
using System.Linq;

namespace FluxJpeg.Core
{
	internal class JpegHuffmanTable
	{
		private short[] lengths;

		private short[] values;

		public static JpegHuffmanTable StdACChrominance = new JpegHuffmanTable(new short[16]
		{
			0, 2, 1, 2, 4, 4, 3, 4, 7, 5,
			4, 4, 0, 1, 2, 119
		}, new short[162]
		{
			0, 1, 2, 3, 17, 4, 5, 33, 49, 6,
			18, 65, 81, 7, 97, 113, 19, 34, 50, 129,
			8, 20, 66, 145, 161, 177, 193, 9, 35, 51,
			82, 240, 21, 98, 114, 209, 10, 22, 36, 52,
			225, 37, 241, 23, 24, 25, 26, 38, 39, 40,
			41, 42, 53, 54, 55, 56, 57, 58, 67, 68,
			69, 70, 71, 72, 73, 74, 83, 84, 85, 86,
			87, 88, 89, 90, 99, 100, 101, 102, 103, 104,
			105, 106, 115, 116, 117, 118, 119, 120, 121, 122,
			130, 131, 132, 133, 134, 135, 136, 137, 138, 146,
			147, 148, 149, 150, 151, 152, 153, 154, 162, 163,
			164, 165, 166, 167, 168, 169, 170, 178, 179, 180,
			181, 182, 183, 184, 185, 186, 194, 195, 196, 197,
			198, 199, 200, 201, 202, 210, 211, 212, 213, 214,
			215, 216, 217, 218, 226, 227, 228, 229, 230, 231,
			232, 233, 234, 242, 243, 244, 245, 246, 247, 248,
			249, 250
		}, false);

		public static JpegHuffmanTable StdACLuminance = new JpegHuffmanTable(new short[16]
		{
			0, 2, 1, 3, 3, 2, 4, 3, 5, 5,
			4, 4, 0, 0, 1, 125
		}, new short[162]
		{
			1, 2, 3, 0, 4, 17, 5, 18, 33, 49,
			65, 6, 19, 81, 97, 7, 34, 113, 20, 50,
			129, 145, 161, 8, 35, 66, 177, 193, 21, 82,
			209, 240, 36, 51, 98, 114, 130, 9, 10, 22,
			23, 24, 25, 26, 37, 38, 39, 40, 41, 42,
			52, 53, 54, 55, 56, 57, 58, 67, 68, 69,
			70, 71, 72, 73, 74, 83, 84, 85, 86, 87,
			88, 89, 90, 99, 100, 101, 102, 103, 104, 105,
			106, 115, 116, 117, 118, 119, 120, 121, 122, 131,
			132, 133, 134, 135, 136, 137, 138, 146, 147, 148,
			149, 150, 151, 152, 153, 154, 162, 163, 164, 165,
			166, 167, 168, 169, 170, 178, 179, 180, 181, 182,
			183, 184, 185, 186, 194, 195, 196, 197, 198, 199,
			200, 201, 202, 210, 211, 212, 213, 214, 215, 216,
			217, 218, 225, 226, 227, 228, 229, 230, 231, 232,
			233, 234, 241, 242, 243, 244, 245, 246, 247, 248,
			249, 250
		}, false);

		public static JpegHuffmanTable StdDCChrominance = new JpegHuffmanTable(new short[16]
		{
			0, 3, 1, 1, 1, 1, 1, 1, 1, 1,
			1, 0, 0, 0, 0, 0
		}, new short[12]
		{
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
			10, 11
		}, false);

		public static JpegHuffmanTable StdDCLuminance = new JpegHuffmanTable(new short[16]
		{
			0, 1, 5, 1, 1, 1, 1, 1, 1, 0,
			0, 0, 0, 0, 0, 0
		}, new short[12]
		{
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
			10, 11
		}, false);

		public short[] Lengths
		{
			get
			{
				return lengths;
			}
		}

		public short[] Values
		{
			get
			{
				return values;
			}
		}

		public JpegHuffmanTable(short[] lengths, short[] values)
			: this(checkLengths(lengths), checkValues(values, lengths), true)
		{
		}

		private JpegHuffmanTable(short[] lengths, short[] values, bool copy)
		{
			this.lengths = (copy ? ((short[])lengths.Clone()) : lengths);
			this.values = (copy ? ((short[])values.Clone()) : values);
		}

		private static short[] checkLengths(short[] lengths)
		{
			if (lengths == null || lengths.Length > 16)
			{
				throw new ArgumentException("Length array is null or too long.");
			}
			if (lengths.Any((short x) => x < 0))
			{
				throw new ArgumentException("Negative values cannot appear in the length array.");
			}
			for (int num = 0; num < lengths.Length; num++)
			{
				if (lengths[num] > (1 << num + 1) - 1)
				{
					throw new ArgumentException(string.Format("Invalid number of codes for code length {0}", new object[1] { (num + 1).ToString() }));
				}
			}
			return lengths;
		}

		private static short[] checkValues(short[] values, short[] lengths)
		{
			if (values == null || values.Length > 256)
			{
				throw new ArgumentException("Values array is null or too long.");
			}
			if (values.Any((short x) => x < 0))
			{
				throw new ArgumentException("Negative values cannot appear in the values array.");
			}
			if (values.Length != lengths.Sum((short x) => x))
			{
				throw new ArgumentException("Number of values does not match code length sum.");
			}
			return values;
		}


	}
}
