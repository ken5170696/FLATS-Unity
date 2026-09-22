using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluxJpeg.Core.IO;
namespace FluxJpeg.Core.Decoder
{
	internal class JpegDecoder
	{
		private enum UnitType
		{
			None,
			Inches,
			Centimeters
		}

		internal const byte MAJOR_VERSION = 1;

		internal const byte MINOR_VERSION = 2;

		public static long ProgressUpdateByteInterval = 100L;

		private JpegDecodeProgressChangedArgs DecodeProgress;

		private byte majorVersion;

		private byte minorVersion;

		private UnitType Units;

		private ushort XDensity;

		private ushort YDensity;

		private byte Xthumbnail;

		private byte Ythumbnail;

		private byte[] thumbnail;

		private Image image;

		private bool progressive;

		private byte marker;

		internal static short JFIF_FIXED_LENGTH = 16;

		internal static short JFXX_FIXED_LENGTH = 8;

		private JPEGBinaryReader jpegReader;

		private List<JPEGFrame> jpegFrames;

		private JpegHuffmanTable[] dcTables;

		private JpegHuffmanTable[] acTables;

		private JpegQuantizationTable[] qTables;

		public BlockUpsamplingMode BlockUpsamplingMode { get; set; }

		public event EventHandler<JpegDecodeProgressChangedArgs> DecodeProgressChanged;

		public JpegDecoder(Stream input)
		{
			DecodeProgress = new JpegDecodeProgressChangedArgs();
			jpegFrames = new List<JPEGFrame>();
			dcTables = new JpegHuffmanTable[4];
			acTables = new JpegHuffmanTable[4];
			qTables = new JpegQuantizationTable[4];

			jpegReader = new JPEGBinaryReader(input);
			if (jpegReader.GetNextMarker() != 216)
			{
				throw new Exception("Failed to find SOI marker.");
			}
		}

		private bool TryParseJFIF(byte[] data)
		{
			FluxJpeg.Core.IO.BinaryReader binaryReader = new FluxJpeg.Core.IO.BinaryReader(new MemoryStream(data));
			int num = data.Length + 2;
			if (num < JFIF_FIXED_LENGTH)
			{
				return false;
			}
			byte[] array = new byte[5];
			binaryReader.Read(array, 0, array.Length);
			if (array[0] != 74 || array[1] != 70 || array[2] != 73 || array[3] != 70 || array[4] != 0)
			{
				return false;
			}
			majorVersion = binaryReader.ReadByte();
			minorVersion = binaryReader.ReadByte();
			if (majorVersion != 1 || (majorVersion == 1 && minorVersion > 2))
			{
				return false;
			}
			Units = (UnitType)binaryReader.ReadByte();
			if (Units != UnitType.None && Units != UnitType.Inches && Units != UnitType.Centimeters)
			{
				return false;
			}
			XDensity = binaryReader.ReadShort();
			YDensity = binaryReader.ReadShort();
			Xthumbnail = binaryReader.ReadByte();
			Ythumbnail = binaryReader.ReadByte();
			int num2 = 3 * Xthumbnail * Ythumbnail;
			if (num > JFIF_FIXED_LENGTH && num2 != num - JFIF_FIXED_LENGTH)
			{
				return false;
			}
			if (num2 > 0)
			{
				thumbnail = new byte[num2];
				if (binaryReader.Read(thumbnail, 0, num2) != num2)
				{
					return false;
				}
			}
			return true;
		}

		public DecodedJpeg Decode()
		{
			JPEGFrame jPEGFrame = null;
			int resetInterval = 0;
			bool flag = false;
			bool flag2 = false;
			List<JpegHeader> list = new List<JpegHeader>();
			while (true)
			{
				switch (marker)
				{
				case 224:
				case 225:
				case 226:
				case 227:
				case 228:
				case 229:
				case 230:
				case 231:
				case 232:
				case 233:
				case 234:
				case 235:
				case 236:
				case 237:
				case 238:
				case 239:
				case 254:
				{
					JpegHeader jpegHeader = ExtractHeader();
					if (jpegHeader.Marker == 225 && jpegHeader.Data.Length >= 6)
					{
						byte[] data = jpegHeader.Data;
						if (data[0] == 69 && data[1] == 120 && data[2] == 105 && data[3] == 102 && data[4] == 0)
						{
							byte b9 = data[5];
						}
					}
					if (jpegHeader.Data.Length >= 5 && jpegHeader.Marker == 238)
					{
						string text = Encoding.UTF8.GetString(jpegHeader.Data, 0, 5);
						bool flag3 = text == "Adobe";
					}
					list.Add(jpegHeader);
					if (flag2 || marker != 224)
					{
						break;
					}
					flag2 = TryParseJFIF(jpegHeader.Data);
					if (flag2)
					{
						jpegHeader.IsJFIF = true;
						marker = jpegReader.GetNextMarker();
						if (marker == 224)
						{
							jpegHeader = ExtractHeader();
							list.Add(jpegHeader);
						}
						else
						{
							flag = true;
						}
					}
					break;
				}
				case 192:
				case 194:
				{
					progressive = marker == 194;
					jpegFrames.Add(new JPEGFrame());
					jPEGFrame = jpegFrames[jpegFrames.Count - 1];
					jPEGFrame.ProgressUpdateMethod = UpdateStreamProgress;
					jpegReader.ReadShort();
					jPEGFrame.setPrecision(jpegReader.ReadByte());
					jPEGFrame.ScanLines = jpegReader.ReadShort();
					jPEGFrame.SamplesPerLine = jpegReader.ReadShort();
					jPEGFrame.ComponentCount = jpegReader.ReadByte();
					DecodeProgress.Height = jPEGFrame.Height;
					DecodeProgress.Width = jPEGFrame.Width;
					DecodeProgress.SizeReady = true;
					if (this.DecodeProgressChanged != null)
					{
						this.DecodeProgressChanged(this, DecodeProgress);
					}
					for (int num2 = 0; num2 < jPEGFrame.ComponentCount; num2++)
					{
						byte componentID = jpegReader.ReadByte();
						byte b = jpegReader.ReadByte();
						byte quantizationTableID = jpegReader.ReadByte();
						byte sampleHFactor = (byte)(b >> 4);
						byte sampleVFactor = (byte)(b & 0xF);
						jPEGFrame.AddComponent(componentID, sampleHFactor, sampleVFactor, quantizationTableID);
					}
					break;
				}
				case 196:
				{
					int num7 = jpegReader.ReadShort() - 2;
					int num8 = num7;
					while (num8 > 0)
					{
						byte b3 = jpegReader.ReadByte();
						byte b4 = (byte)(b3 >> 4);
						byte b5 = (byte)(b3 & 0xF);
						short[] array2 = new short[16];
						for (int num9 = 0; num9 < array2.Length; num9++)
						{
							array2[num9] = jpegReader.ReadByte();
						}
						int num10 = 0;
						for (int num11 = 0; num11 < 16; num11++)
						{
							num10 += array2[num11];
						}
						num8 -= num10 + 17;
						short[] array3 = new short[num10];
						for (int num12 = 0; num12 < array3.Length; num12++)
						{
							array3[num12] = jpegReader.ReadByte();
						}
						if (b4 == HuffmanTable.JPEG_DC_TABLE)
						{
							dcTables[b5] = new JpegHuffmanTable(array2, array3);
						}
						else if (b4 == HuffmanTable.JPEG_AC_TABLE)
						{
							acTables[b5] = new JpegHuffmanTable(array2, array3);
						}
					}
					break;
				}
				case 219:
				{
					short num3 = (short)(jpegReader.ReadShort() - 2);
					for (int num4 = 0; num4 < num3 / 65; num4++)
					{
						byte b2 = jpegReader.ReadByte();
						int[] array = new int[64];
						if ((byte)(b2 >> 4) == 0)
						{
							for (int num5 = 0; num5 < 64; num5++)
							{
								array[num5] = jpegReader.ReadByte();
							}
						}
						else if ((byte)(b2 >> 4) == 1)
						{
							for (int num6 = 0; num6 < 64; num6++)
							{
								array[num6] = jpegReader.ReadShort();
							}
						}
						qTables[b2 & 0xF] = new JpegQuantizationTable(array);
					}
					break;
				}
				case 218:
				{
					byte b6 = jpegReader.ReadByte();
					byte[] array4 = new byte[b6];
					for (int num13 = 0; num13 < b6; num13++)
					{
						byte b7 = jpegReader.ReadByte();
						byte b8 = jpegReader.ReadByte();
						int num14 = (b8 >> 4) & 0xF;
						int num15 = b8 & 0xF;
						jPEGFrame.setHuffmanTables(b7, acTables[(byte)num15], dcTables[(byte)num14]);
						array4[num13] = b7;
					}
					byte startSpectralSelection = jpegReader.ReadByte();
					byte endSpectralSelection = jpegReader.ReadByte();
					byte successiveApproximation = jpegReader.ReadByte();
					if (!progressive)
					{
						jPEGFrame.DecodeScanBaseline(b6, array4, resetInterval, jpegReader, ref marker);
						flag = true;
					}
					if (progressive)
					{
						jPEGFrame.DecodeScanProgressive(successiveApproximation, startSpectralSelection, endSpectralSelection, b6, array4, resetInterval, jpegReader, ref marker);
						flag = true;
					}
					break;
				}
				case 221:
					jpegReader.BaseStream.Seek(2L, SeekOrigin.Current);
					resetInterval = jpegReader.ReadShort();
					break;
				case 220:
					jPEGFrame.ScanLines = jpegReader.ReadShort();
					break;
				case 217:
					if (jpegFrames.Count == 0)
					{
						throw new NotSupportedException("No JPEG frames could be located.");
					}
					if (jpegFrames.Count == 1)
					{
						byte[][,] raster = Image.CreateRaster(jPEGFrame.Width, jPEGFrame.Height, jPEGFrame.ComponentCount);
						IList<JpegComponent> components = jPEGFrame.Scan.Components;
						int stepsTotal = components.Count * 3;
						int num = 0;
						for (int i = 0; i < components.Count; i++)
						{
							JpegComponent jpegComponent = components[i];
							jpegComponent.QuantizationTable = qTables[jpegComponent.quant_id].Table;
							jpegComponent.quantizeData();
							UpdateProgress(++num, stepsTotal);
							jpegComponent.idctData();
							UpdateProgress(++num, stepsTotal);
							jpegComponent.writeDataScaled(raster, i, BlockUpsamplingMode);
							UpdateProgress(++num, stepsTotal);
							jpegComponent = null;
							GC.Collect();
						}
						if (jPEGFrame.ComponentCount == 1)
						{
							ColorModel cm = new ColorModel
							{
								ColorSpace = ColorSpace.Gray,
								Opaque = true
							};
							image = new Image(cm, raster);
						}
						else
						{
							if (jPEGFrame.ComponentCount != 3)
							{
								throw new NotSupportedException("Unsupported Color Mode: 4 Component Color Mode found.");
							}
							ColorModel cm2 = new ColorModel
							{
								ColorSpace = ColorSpace.YCbCr,
								Opaque = true
							};
							image = new Image(cm2, raster);
						}
						Func<double, double> func = (double x) => (Units != UnitType.Inches) ? (x / 2.54) : x;
						image.DensityX = func((int)XDensity);
						image.DensityY = func((int)YDensity);
						break;
					}
					throw new NotSupportedException("Unsupported Codec Type: Hierarchial JPEG");
				case 193:
				case 195:
				case 197:
				case 198:
				case 199:
				case 201:
				case 202:
				case 203:
				case 205:
				case 206:
				case 207:
					throw new NotSupportedException("Unsupported codec type.");
				}
				if (flag)
				{
					flag = false;
					continue;
				}
				try
				{
					marker = jpegReader.GetNextMarker();
				}
				catch (EndOfStreamException)
				{
					break;
				}
			}
			return new DecodedJpeg(image, list);
		}

		private JpegHeader ExtractHeader()
		{
			int num = jpegReader.ReadShort() - 2;
			byte[] array = new byte[num];
			jpegReader.Read(array, 0, num);
			JpegHeader jpegHeader = new JpegHeader();
			jpegHeader.Marker = marker;
			jpegHeader.Data = array;
			return jpegHeader;
		}

		private void UpdateStreamProgress(long StreamPosition)
		{
			if (this.DecodeProgressChanged != null)
			{
				DecodeProgress.ReadPosition = StreamPosition;
				this.DecodeProgressChanged(this, DecodeProgress);
			}
		}

		private void UpdateProgress(int stepsFinished, int stepsTotal)
		{
			if (this.DecodeProgressChanged != null)
			{
				DecodeProgress.DecodeProgress = (double)stepsFinished / (double)stepsTotal;
				this.DecodeProgressChanged(this, DecodeProgress);
			}
		}




	}
}
