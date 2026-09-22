using System;
using FluxJpeg.Core.IO;
namespace FluxJpeg.Core.Decoder
{
	internal class JPEGFrame
	{
		public static byte JPEG_COLOR_GRAY = 1;

		public static byte JPEG_COLOR_RGB = 2;

		public static byte JPEG_COLOR_YCbCr = 3;

		public static byte JPEG_COLOR_CMYK = 4;

		public byte precision;

		public byte colorMode;

		public JpegScan Scan;

		public Action<long> ProgressUpdateMethod;

		public ushort Width { get; private set; }

		public ushort Height { get; private set; }

		public ushort ScanLines
		{
			set
			{
				Height = value;
			}
		}

		public ushort SamplesPerLine
		{
			set
			{
				Width = value;
			}
		}

		public byte ColorMode
		{
			get
			{
				if (ComponentCount != 1)
				{
					return JPEG_COLOR_YCbCr;
				}
				return JPEG_COLOR_GRAY;
			}
		}

		public byte ComponentCount { get; set; }

		public void AddComponent(byte componentID, byte sampleHFactor, byte sampleVFactor, byte quantizationTableID)
		{
			Scan.AddComponent(componentID, sampleHFactor, sampleVFactor, quantizationTableID, colorMode);
		}

		public void setPrecision(byte data)
		{
			precision = data;
		}

		public void setHuffmanTables(byte componentID, JpegHuffmanTable ACTable, JpegHuffmanTable DCTable)
		{
			JpegComponent componentById = Scan.GetComponentById(componentID);
			if (DCTable != null)
			{
				componentById.setDCTable(DCTable);
			}
			if (ACTable != null)
			{
				componentById.setACTable(ACTable);
			}
		}

		public void DecodeScanBaseline(byte numberOfComponents, byte[] componentSelector, int resetInterval, JPEGBinaryReader jpegReader, ref byte marker)
		{
			for (int i = 0; i < numberOfComponents; i++)
			{
				JpegComponent componentById = Scan.GetComponentById(componentSelector[i]);
				componentById.Decode = componentById.DecodeBaseline;
			}
			DecodeScan(numberOfComponents, componentSelector, resetInterval, jpegReader, ref marker);
		}

		private int mcus_per_row(JpegComponent c)
		{
			return ((Width * c.factorH + (Scan.MaxH - 1)) / Scan.MaxH + 7) / 8;
		}

		private void DecodeScan(byte numberOfComponents, byte[] componentSelector, int resetInterval, JPEGBinaryReader jpegReader, ref byte marker)
		{
			jpegReader.eob_run = 0;
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			long position = jpegReader.BaseStream.Position;
			while (true)
			{
				if (ProgressUpdateMethod != null && jpegReader.BaseStream.Position >= position + JpegDecoder.ProgressUpdateByteInterval)
				{
					position = jpegReader.BaseStream.Position;
					ProgressUpdateMethod(position);
				}
				try
				{
					if (numberOfComponents == 1)
					{
						JpegComponent componentById = Scan.GetComponentById(componentSelector[0]);
						componentById.SetBlock(num);
						componentById.DecodeMCU(jpegReader, num3, num4);
						int num6 = mcus_per_row(componentById);
						int num7 = (int)Math.Ceiling((double)(int)Width / (double)(8 * componentById.factorH));
						num3++;
						num5++;
						if (num3 == componentById.factorH)
						{
							num3 = 0;
							num++;
						}
						if (num5 % num6 != 0)
						{
							continue;
						}
						num5 = 0;
						num4++;
						if (num4 == componentById.factorV)
						{
							if (num3 != 0)
							{
								num++;
								num3 = 0;
							}
							num4 = 0;
						}
						else
						{
							num -= num7;
							if (num3 != 0)
							{
								num++;
								num3 = 0;
							}
						}
						continue;
					}
					for (int i = 0; i < numberOfComponents; i++)
					{
						JpegComponent componentById2 = Scan.GetComponentById(componentSelector[i]);
						componentById2.SetBlock(num2);
						for (int j = 0; j < componentById2.factorV; j++)
						{
							for (int k = 0; k < componentById2.factorH; k++)
							{
								componentById2.DecodeMCU(jpegReader, k, j);
							}
						}
					}
					num++;
					num2++;
				}
				catch (JPEGMarkerFoundException ex)
				{
					marker = ex.Marker;
					if (marker != 208 && marker != 209 && marker != 210 && marker != 211 && marker != 212 && marker != 213 && marker != 214 && marker != 215)
					{
						break;
					}
					for (int l = 0; l < numberOfComponents; l++)
					{
						JpegComponent componentById3 = Scan.GetComponentById(componentSelector[l]);
						if (l > 1)
						{
							componentById3.padMCU(num2, resetInterval - num);
						}
						componentById3.resetInterval();
					}
					num2 += resetInterval - num;
					num = 0;
					continue;
				}
			}
		}

		public void DecodeScanProgressive(byte successiveApproximation, byte startSpectralSelection, byte endSpectralSelection, byte numberOfComponents, byte[] componentSelector, int resetInterval, JPEGBinaryReader jpegReader, ref byte marker)
		{
			byte b = (byte)(successiveApproximation >> 4);
			byte successiveLow = (byte)(successiveApproximation & 0xF);
			if (startSpectralSelection > endSpectralSelection || endSpectralSelection > 63)
			{
				throw new Exception("Bad spectral selection.");
			}
			bool flag = startSpectralSelection == 0;
			bool flag2 = b != 0;
			if (flag)
			{
				if (endSpectralSelection != 0)
				{
					throw new Exception("Bad spectral selection for DC only scan.");
				}
			}
			else if (numberOfComponents > 1)
			{
				throw new Exception("Too many components for AC scan!");
			}
			for (int i = 0; i < numberOfComponents; i++)
			{
				JpegComponent componentById = Scan.GetComponentById(componentSelector[i]);
				componentById.successiveLow = successiveLow;
				if (flag)
				{
					if (flag2)
					{
						componentById.Decode = componentById.DecodeDCRefine;
					}
					else
					{
						componentById.Decode = componentById.DecodeDCFirst;
					}
					continue;
				}
				componentById.spectralStart = startSpectralSelection;
				componentById.spectralEnd = endSpectralSelection;
				if (flag2)
				{
					componentById.Decode = componentById.DecodeACRefine;
				}
				else
				{
					componentById.Decode = componentById.DecodeACFirst;
				}
			}
			DecodeScan(numberOfComponents, componentSelector, resetInterval, jpegReader, ref marker);
		}

		public JPEGFrame()
		{
			precision = 8;
			colorMode = JPEG_COLOR_YCbCr;
			Scan = new JpegScan();

		}




	}
}
