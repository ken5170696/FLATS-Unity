using System;
using System.Collections.Generic;
using FluxJpeg.Core.IO;
namespace FluxJpeg.Core.Decoder
{
	internal class JpegComponent
	{
		internal delegate void DecodeFunction(JPEGBinaryReader jpegReader, float[] zigzagMCU);

		public byte factorH;

		public byte factorV;

		public byte component_id;

		public byte quant_id;

		public int width;

		public int height;

		public HuffmanTable ACTable;

		public HuffmanTable DCTable;

		private int[] quantizationTable;

		public float previousDC;

		private JpegScan parent;

		private float[,][] scanMCUs;

		private List<float[,][]> scanData;

		private List<byte[,]> scanDecoded;

		public int spectralStart;

		public int spectralEnd;

		public int successiveLow;

		private DCT _dct;

		public DecodeFunction Decode;

		public int[] QuantizationTable
		{
			set
			{
				quantizationTable = value;
			}
		}

		public int BlockCount
		{
			get
			{
				return scanData.Count;
			}
		}

		private int factorUpV
		{
			get
			{
				return parent.MaxV / factorV;
			}
		}

		private int factorUpH
		{
			get
			{
				return parent.MaxH / factorH;
			}
		}

		public JpegComponent(JpegScan parentScan, byte id, byte factorHorizontal, byte factorVertical, byte quantizationID, byte colorMode)
		{
			scanData = new List<float[,][]>();
			scanDecoded = new List<byte[,]>();
			_dct = new DCT();

			parent = parentScan;
			if (colorMode == JPEGFrame.JPEG_COLOR_YCbCr)
			{
				if (id == 1)
				{
					ACTable = new HuffmanTable(JpegHuffmanTable.StdACLuminance);
					DCTable = new HuffmanTable(JpegHuffmanTable.StdDCLuminance);
				}
				else
				{
					ACTable = new HuffmanTable(JpegHuffmanTable.StdACChrominance);
					DCTable = new HuffmanTable(JpegHuffmanTable.StdACLuminance);
				}
			}
			component_id = id;
			factorH = factorHorizontal;
			factorV = factorVertical;
			quant_id = quantizationID;
		}

		public void padMCU(int index, int length)
		{
			scanMCUs = new float[factorH, factorV][];
			for (int i = 0; i < length; i++)
			{
				if (scanData.Count >= index + length)
				{
					continue;
				}
				for (int j = 0; j < factorH; j++)
				{
					for (int k = 0; k < factorV; k++)
					{
						scanMCUs[j, k] = (float[])scanData[index - 1][j, k].Clone();
					}
				}
				scanData.Add(scanMCUs);
			}
		}

		public void resetInterval()
		{
			previousDC = 0f;
		}

		public void quantizeData()
		{
			for (int i = 0; i < scanData.Count; i++)
			{
				for (int j = 0; j < factorV; j++)
				{
					for (int k = 0; k < factorH; k++)
					{
						float[] array = scanData[i][k, j];
						for (int l = 0; l < 64; l++)
						{
							array[l] *= quantizationTable[l];
						}
					}
				}
			}
		}

		public void setDCTable(JpegHuffmanTable table)
		{
			DCTable = new HuffmanTable(table);
		}

		public void setACTable(JpegHuffmanTable table)
		{
			ACTable = new HuffmanTable(table);
		}

		public void idctData()
		{
			float[] array = new float[64];
			float[] array2 = null;
			for (int i = 0; i < scanData.Count; i++)
			{
				for (int j = 0; j < factorV; j++)
				{
					for (int k = 0; k < factorH; k++)
					{
						array2 = scanData[i][k, j];
						ZigZag.UnZigZag(array2, array);
						scanDecoded.Add(_dct.FastIDCT(array));
					}
				}
			}
		}

		public void scaleByFactors(BlockUpsamplingMode mode)
		{
			int num = factorUpV;
			int num2 = factorUpH;
			if (num == 1 && num2 == 1)
			{
				return;
			}
			for (int i = 0; i < scanDecoded.Count; i++)
			{
				byte[,] array = scanDecoded[i];
				int length = array.GetLength(0);
				int length2 = array.GetLength(1);
				int num3 = length * num;
				int num4 = length2 * num2;
				byte[,] array2 = new byte[num3, num4];
				switch (mode)
				{
				case BlockUpsamplingMode.BoxFilter:
				{
					for (int n = 0; n < num4; n++)
					{
						int num8 = n / num2;
						for (int num9 = 0; num9 < num3; num9++)
						{
							int num10 = num9 / num;
							array2[num9, n] = array[num10, num8];
						}
					}
					break;
				}
				case BlockUpsamplingMode.Interpolate:
				{
					for (int j = 0; j < num4; j++)
					{
						for (int k = 0; k < num3; k++)
						{
							int num5 = 0;
							for (int l = 0; l < num2; l++)
							{
								int num6 = (j + l) / num2;
								if (num6 >= length2)
								{
									num6 = length2 - 1;
								}
								for (int m = 0; m < num; m++)
								{
									int num7 = (k + m) / num;
									if (num7 >= length)
									{
										num7 = length - 1;
									}
									num5 += array[num7, num6];
								}
							}
							array2[k, j] = (byte)(num5 / (num2 * num));
						}
					}
					break;
				}
				default:
					throw new ArgumentException("Upsampling mode not supported.");
				}
				scanDecoded[i] = array2;
			}
		}

		public void writeBlock(byte[][,] raster, byte[,] data, int compIndex, int x, int y)
		{
			int length = raster[0].GetLength(0);
			int length2 = raster[0].GetLength(1);
			byte[,] array = raster[compIndex];
			int num = data.GetLength(0);
			if (y + num > length2)
			{
				num = length2 - y;
			}
			int num2 = data.GetLength(1);
			if (x + num2 > length)
			{
				num2 = length - x;
			}
			for (int i = 0; i < num; i++)
			{
				for (int j = 0; j < num2; j++)
				{
					array[x + j, y + i] = data[i, j];
				}
			}
		}

		public void writeDataScaled(byte[][,] raster, int componentIndex, BlockUpsamplingMode mode)
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			int length = raster[0].GetLength(0);
			while (num5 < scanDecoded.Count)
			{
				int num6 = 0;
				int num7 = 0;
				if (num >= length)
				{
					num = 0;
					num2 += num4;
				}
				for (int i = 0; i < factorV; i++)
				{
					num6 = 0;
					for (int j = 0; j < factorH; j++)
					{
						byte[,] array = scanDecoded[num5++];
						writeBlockScaled(raster, array, componentIndex, num, num2, mode);
						num6 += array.GetLength(1) * factorUpH;
						num += array.GetLength(1) * factorUpH;
						num7 = array.GetLength(0) * factorUpV;
					}
					num2 += num7;
					num -= num6;
					num3 += num7;
				}
				num2 -= num3;
				num4 = num3;
				num3 = 0;
				num += num6;
			}
		}

		private void writeBlockScaled(byte[][,] raster, byte[,] blockdata, int compIndex, int x, int y, BlockUpsamplingMode mode)
		{
			int length = raster[0].GetLength(0);
			int length2 = raster[0].GetLength(1);
			int num = factorUpV;
			int num2 = factorUpH;
			int length3 = blockdata.GetLength(0);
			int length4 = blockdata.GetLength(1);
			int num3 = length3 * num;
			int num4 = length4 * num2;
			byte[,] array = raster[compIndex];
			int num5 = num3;
			if (y + num5 > length2)
			{
				num5 = length2 - y;
			}
			int num6 = num4;
			if (x + num6 > length)
			{
				num6 = length - x;
			}
			if (mode == BlockUpsamplingMode.BoxFilter)
			{
				if (num == 1 && num2 == 1)
				{
					for (int i = 0; i < num6; i++)
					{
						for (int j = 0; j < num5; j++)
						{
							array[i + x, y + j] = blockdata[j, i];
						}
					}
					return;
				}
				if (num2 == 2 && num == 2 && num6 == num4 && num5 == num3)
				{
					for (int k = 0; k < length4; k++)
					{
						int num7 = k * 2 + x;
						for (int l = 0; l < length3; l++)
						{
							byte b = blockdata[l, k];
							int num8 = l * 2 + y;
							array[num7, num8] = b;
							array[num7, num8 + 1] = b;
							array[num7 + 1, num8] = b;
							array[num7 + 1, num8 + 1] = b;
						}
					}
					return;
				}
				for (int m = 0; m < num6; m++)
				{
					int num9 = m / num2;
					for (int n = 0; n < num5; n++)
					{
						int num10 = n / num;
						array[m + x, y + n] = blockdata[num10, num9];
					}
				}
				return;
			}
			throw new ArgumentException("Upsampling mode not supported.");
		}

		public void DecodeBaseline(JPEGBinaryReader stream, float[] dest)
		{
			float num = decode_dc_coefficient(stream);
			decode_ac_coefficients(stream, dest);
			dest[0] = num;
		}

		public void DecodeDCFirst(JPEGBinaryReader stream, float[] dest)
		{
			int num = DCTable.Decode(stream);
			int diff = stream.ReadBits(num);
			num = HuffmanTable.Extend(diff, num);
			num = (int)previousDC + num;
			previousDC = num;
			dest[0] = num << successiveLow;
		}

		public void DecodeACFirst(JPEGBinaryReader stream, float[] zz)
		{
			if (stream.eob_run > 0)
			{
				stream.eob_run--;
				return;
			}
			int num;
			for (num = spectralStart; num <= spectralEnd; num++)
			{
				int num2 = ACTable.Decode(stream);
				int num3 = num2 >> 4;
				num2 &= 0xF;
				if (num2 != 0)
				{
					num += num3;
					num3 = stream.ReadBits(num2);
					num2 = HuffmanTable.Extend(num3, num2);
					zz[num] = num2 << successiveLow;
				}
				else
				{
					if (num3 != 15)
					{
						stream.eob_run = 1 << num3;
						if (num3 != 0)
						{
							stream.eob_run += stream.ReadBits(num3);
						}
						stream.eob_run--;
						break;
					}
					num += 15;
				}
			}
		}

		public void DecodeDCRefine(JPEGBinaryReader stream, float[] dest)
		{
			if (stream.ReadBits(1) == 1)
			{
				dest[0] = (int)dest[0] | (1 << successiveLow);
			}
		}

		public void DecodeACRefine(JPEGBinaryReader stream, float[] dest)
		{
			int num = 1 << successiveLow;
			int num2 = -1 << successiveLow;
			int i = spectralStart;
			if (stream.eob_run == 0)
			{
				for (; i <= spectralEnd; i++)
				{
					int num3 = ACTable.Decode(stream);
					int num4 = num3 >> 4;
					num3 &= 0xF;
					if (num3 != 0)
					{
						if (num3 != 1)
						{
							throw new Exception("Decode Error");
						}
						num3 = ((stream.ReadBits(1) != 1) ? num2 : num);
					}
					else if (num4 != 15)
					{
						stream.eob_run = 1 << num4;
						if (num4 > 0)
						{
							stream.eob_run += stream.ReadBits(num4);
						}
						break;
					}
					do
					{
						if (dest[i] != 0f)
						{
							if (stream.ReadBits(1) == 1 && ((int)dest[i] & num) == 0)
							{
								if (dest[i] >= 0f)
								{
									dest[i] += num;
								}
								else
								{
									dest[i] += num2;
								}
							}
						}
						else if (--num4 < 0)
						{
							break;
						}
						i++;
					}
					while (i <= spectralEnd);
					if (num3 != 0 && i < 64)
					{
						dest[i] = num3;
					}
				}
			}
			if (stream.eob_run <= 0)
			{
				return;
			}
			for (; i <= spectralEnd; i++)
			{
				if (dest[i] != 0f && stream.ReadBits(1) == 1 && ((int)dest[i] & num) == 0)
				{
					if (dest[i] >= 0f)
					{
						dest[i] += num;
					}
					else
					{
						dest[i] += num2;
					}
				}
			}
			stream.eob_run--;
		}

		public void SetBlock(int idx)
		{
			if (scanData.Count < idx)
			{
				throw new Exception("Invalid block ID.");
			}
			if (scanData.Count == idx)
			{
				scanMCUs = new float[factorH, factorV][];
				for (int i = 0; i < factorH; i++)
				{
					for (int j = 0; j < factorV; j++)
					{
						scanMCUs[i, j] = new float[64];
					}
				}
				scanData.Add(scanMCUs);
			}
			else
			{
				scanMCUs = scanData[idx];
			}
		}

		public void DecodeMCU(JPEGBinaryReader jpegReader, int i, int j)
		{
			Decode(jpegReader, scanMCUs[i, j]);
		}

		public float decode_dc_coefficient(JPEGBinaryReader JPEGStream)
		{
			int num = DCTable.Decode(JPEGStream);
			float num2 = JPEGStream.ReadBits(num);
			num2 = HuffmanTable.Extend((int)num2, num);
			return previousDC += num2;
		}

		internal void decode_ac_coefficients(JPEGBinaryReader JPEGStream, float[] zz)
		{
			int num;
			for (num = 1; num < 64; num++)
			{
				int num2 = ACTable.Decode(JPEGStream);
				int num3 = num2 >> 4;
				num2 &= 0xF;
				if (num2 != 0)
				{
					num += num3;
					num3 = JPEGStream.ReadBits(num2);
					num2 = HuffmanTable.Extend(num3, num2);
					zz[num] = num2;
				}
				else
				{
					if (num3 != 15)
					{
						break;
					}
					num += 15;
				}
			}
		}




	}
}
