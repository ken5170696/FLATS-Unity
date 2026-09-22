using System;
using System.IO;
using ImageTools.Helpers;

namespace ImageTools.IO.Gif
{
	internal sealed class LZWDecoder
	{
		private const int StackSize = 4096;

		private const int NullCode = -1;

		private Stream _stream;

		public LZWDecoder(Stream stream)
		{
			Guard.NotNull(stream, "stream");
			_stream = stream;
		}

		public byte[] DecodePixels(int width, int height, int dataSize)
		{
			byte[] array = new byte[width * height];
			int num = 1 << dataSize;
			if (dataSize == int.MaxValue)
			{
				throw new ArgumentOutOfRangeException("dataSize", "Must be less than Int32.MaxValue");
			}
			int num2 = dataSize + 1;
			int num3 = num + 1;
			int num4 = num + 2;
			int num5 = -1;
			int num6 = -1;
			int num7 = (1 << num2) - 1;
			int num8 = 0;
			int[] array2 = new int[4096];
			int[] array3 = new int[4096];
			int[] array4 = new int[4097];
			int num9 = 0;
			int num10 = 0;
			int num11 = 0;
			int num12 = 0;
			int num13 = 0;
			int num14 = 0;
			int num15 = -1;
			for (num5 = 0; num5 < num; num5++)
			{
				array2[num5] = 0;
				array3[num5] = (byte)num5;
			}
			byte[] array5 = null;
			while (num12 < array.Length)
			{
				if (num9 == 0)
				{
					if (num8 < num2)
					{
						if (num10 == 0)
						{
							array5 = ReadBlock();
							num10 = array5.Length;
							if (num10 == 0)
							{
								break;
							}
							num11 = 0;
						}
						num13 += array5[num11] << num8;
						num8 += 8;
						num11++;
						num10--;
						continue;
					}
					num5 = num13 & num7;
					num13 >>= num2;
					num8 -= num2;
					if (num5 > num4 || num5 == num3)
					{
						break;
					}
					if (num5 == num)
					{
						num2 = dataSize + 1;
						num7 = (1 << num2) - 1;
						num4 = num + 2;
						num6 = -1;
						continue;
					}
					if (num6 == -1)
					{
						array4[num9++] = array3[num5];
						num6 = num5;
						num14 = num5;
						continue;
					}
					num15 = num5;
					if (num5 == num4)
					{
						array4[num9++] = (byte)num14;
						num5 = num6;
					}
					while (num5 > num)
					{
						array4[num9++] = array3[num5];
						num5 = array2[num5];
					}
					num14 = array3[num5];
					array4[num9++] = array3[num5];
					if (num4 < 4096)
					{
						array2[num4] = num6;
						array3[num4] = num14;
						num4++;
						if (num4 == num7 + 1 && num4 < 4096)
						{
							num2++;
							num7 = (1 << num2) - 1;
						}
					}
					num6 = num15;
				}
				num9--;
				array[num12++] = (byte)array4[num9];
			}
			return array;
		}

		private byte[] ReadBlock()
		{
			int length = _stream.ReadByte();
			return ReadBytes(length);
		}

		private byte[] ReadBytes(int length)
		{
			byte[] array = new byte[length];
			_stream.Read(array, 0, length);
			return array;
		}


	}
}
