using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ICSharpCode.SharpZipLib.Checksums;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using ImageTools.Helpers;
namespace ImageTools.IO.Png
{
	public class PngDecoder : IImageDecoder
	{
		private static readonly Dictionary<int, PngColorTypeInformation> _colorTypes;

		private ExtendedImage _image;

		private Stream _stream;

		private PngHeader _header;

		public int HeaderSize
		{
			get
			{
				return 8;
			}
		}

		static PngDecoder()
		{
			_colorTypes = new Dictionary<int, PngColorTypeInformation>();
			_colorTypes.Add(0, new PngColorTypeInformation(1, new int[4] { 1, 2, 4, 8 }, (byte[] p, byte[] a) => new GrayscaleReader(false)));
			_colorTypes.Add(2, new PngColorTypeInformation(3, new int[1] { 8 }, (byte[] p, byte[] a) => new TrueColorReader(false)));
			_colorTypes.Add(3, new PngColorTypeInformation(1, new int[4] { 1, 2, 4, 8 }, (byte[] p, byte[] a) => new PaletteIndexReader(p, a)));
			_colorTypes.Add(4, new PngColorTypeInformation(2, new int[1] { 8 }, (byte[] p, byte[] a) => new GrayscaleReader(true)));
			_colorTypes.Add(6, new PngColorTypeInformation(4, new int[1] { 8 }, (byte[] p, byte[] a) => new TrueColorReader(true)));
		}

		public bool IsSupportedFileExtension(string extension)
		{
			string text = extension.ToUpper();
			return text == "PNG";
		}

		public bool IsSupportedFileFormat(byte[] header)
		{
			bool result = false;
			if (header.Length >= 8)
			{
				result = header[0] == 137 && header[1] == 80 && header[2] == 78 && header[3] == 71 && header[4] == 13 && header[5] == 10 && header[6] == 26 && header[7] == 10;
			}
			return result;
		}

		public void Decode(ExtendedImage image, Stream stream)
		{
			_image = image;
			_stream = stream;
			_stream.Seek(8L, SeekOrigin.Current);
			bool flag = false;
			PngChunk pngChunk = null;
			byte[] palette = null;
			byte[] paletteAlpha = null;
			using (MemoryStream memoryStream = new MemoryStream())
			{
				while ((pngChunk = ReadChunk()) != null)
				{
					if (flag)
					{
						throw new ImageFormatException("Image does not end with end chunk.");
					}
					if (pngChunk.Type == "IHDR")
					{
						ReadHeaderChunk(pngChunk.Data);
						ValidateHeader();
					}
					else if (pngChunk.Type == "pHYs")
					{
						ReadPhysicalChunk(pngChunk.Data);
					}
					else if (pngChunk.Type == "IDAT")
					{
						memoryStream.Write(pngChunk.Data, 0, pngChunk.Data.Length);
					}
					else if (pngChunk.Type == "PLTE")
					{
						palette = pngChunk.Data;
					}
					else if (pngChunk.Type == "tRNS")
					{
						paletteAlpha = pngChunk.Data;
					}
					else if (pngChunk.Type == "tEXt")
					{
						ReadTextChunk(pngChunk.Data);
					}
					else if (pngChunk.Type == "IEND")
					{
						flag = true;
					}
				}
				byte[] pixels = new byte[_header.Width * _header.Height * 4];
				PngColorTypeInformation pngColorTypeInformation = _colorTypes[_header.ColorType];
				if (pngColorTypeInformation != null)
				{
					IColorReader colorReader = pngColorTypeInformation.CreateColorReader(palette, paletteAlpha);
					ReadScanlines(memoryStream, pixels, colorReader, pngColorTypeInformation);
				}
				image.SetPixels(_header.Width, _header.Height, pixels);
			}
		}

		private void ReadPhysicalChunk(byte[] data)
		{
			Array.Reverse(data, 0, 4);
			Array.Reverse(data, 4, 4);
			_image.DensityX = (double)BitConverter.ToInt32(data, 0) / 39.3700787;
			_image.DensityY = (double)BitConverter.ToInt32(data, 4) / 39.3700787;
		}

		private int CalculateScanlineLength(PngColorTypeInformation colorTypeInformation)
		{
			int num = _header.Width * _header.BitDepth * colorTypeInformation.ChannelsPerColor;
			int num2 = num % 8;
			if (num2 != 0)
			{
				num += 8 - num2;
			}
			return num / 8;
		}

		private int CalculateScanlineStep(PngColorTypeInformation colorTypeInformation)
		{
			int result = 1;
			if (_header.BitDepth >= 8)
			{
				result = colorTypeInformation.ChannelsPerColor * _header.BitDepth / 8;
			}
			return result;
		}

		private void ReadScanlines(MemoryStream dataStream, byte[] pixels, IColorReader colorReader, PngColorTypeInformation colorTypeInformation)
		{
			dataStream.Position = 0L;
			int num = CalculateScanlineLength(colorTypeInformation);
			int num2 = CalculateScanlineStep(colorTypeInformation);
			byte[] rhs = new byte[num];
			byte[] lhs = new byte[num];
			byte b = 0;
			byte b2 = 0;
			byte b3 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = -1;
			using (InflaterInputStream inflaterInputStream = new InflaterInputStream(dataStream))
			{
				int num6 = 0;
				while ((num6 = inflaterInputStream.ReadByte()) >= 0)
				{
					if (num5 == -1)
					{
						num4 = num6;
						num5++;
						continue;
					}
					lhs[num5] = (byte)num6;
					if (num5 >= num2)
					{
						b = lhs[num5 - num2];
						b3 = rhs[num5 - num2];
					}
					else
					{
						b = 0;
						b3 = 0;
					}
					b2 = rhs[num5];
					switch (num4)
					{
					case 1:
						lhs[num5] += b;
						break;
					case 2:
						lhs[num5] += b2;
						break;
					case 3:
						lhs[num5] += (byte)Math.Floor((double)(b + b2) / 2.0);
						break;
					case 4:
						lhs[num5] += PaethPredicator(b, b2, b3);
						break;
					}
					num5++;
					if (num5 == num)
					{
						colorReader.ReadScanline(lhs, pixels, _header);
						num5 = -1;
						num3++;
						ImageTools.Helpers.Extensions.Swap(ref lhs, ref rhs);
					}
				}
			}
		}

		private static byte PaethPredicator(byte a, byte b, byte c)
		{
			byte b2 = 0;
			int num = a + b - c;
			int num2 = Math.Abs(num - a);
			int num3 = Math.Abs(num - b);
			int num4 = Math.Abs(num - c);
			if (num2 <= num3 && num2 <= num4)
			{
				return a;
			}
			if (num3 <= num4)
			{
				return b;
			}
			return c;
		}

		private void ReadTextChunk(byte[] data)
		{
			int num = 0;
			for (int i = 0; i < data.Length; i++)
			{
				if (data[i] == 0)
				{
					num = i;
					break;
				}
			}
			string name = Encoding.Unicode.GetString(data, 0, num);
			string value = Encoding.Unicode.GetString(data, num + 1, data.Length - num - 1);
			_image.Properties.Add(new ImageProperty(name, value));
		}

		private void ReadHeaderChunk(byte[] data)
		{
			_header = new PngHeader();
			Array.Reverse(data, 0, 4);
			Array.Reverse(data, 4, 4);
			_header.Width = BitConverter.ToInt32(data, 0);
			_header.Height = BitConverter.ToInt32(data, 4);
			_header.BitDepth = data[8];
			_header.ColorType = data[9];
			_header.FilterMethod = data[11];
			_header.InterlaceMethod = data[12];
			_header.CompressionMethod = data[10];
		}

		private void ValidateHeader()
		{
			if (!_colorTypes.ContainsKey(_header.ColorType))
			{
				throw new ImageFormatException("Color type is not supported or not valid.");
			}
			if (!_colorTypes[_header.ColorType].SupportedBitDepths.Contains(_header.BitDepth))
			{
				throw new ImageFormatException("Bit depth is not supported or not valid.");
			}
			if (_header.FilterMethod != 0)
			{
				throw new ImageFormatException("The png specification only defines 0 as filter method.");
			}
			if (_header.InterlaceMethod != 0)
			{
				throw new ImageFormatException("Interlacing is not supported.");
			}
		}

		private PngChunk ReadChunk()
		{
			PngChunk pngChunk = new PngChunk();
			if (ReadChunkLength(pngChunk) == 0)
			{
				return null;
			}
			byte[] typeBuffer = ReadChunkType(pngChunk);
			ReadChunkData(pngChunk);
			ReadChunkCrc(pngChunk, typeBuffer);
			return pngChunk;
		}

		private void ReadChunkCrc(PngChunk chunk, byte[] typeBuffer)
		{
			byte[] array = new byte[4];
			int value = _stream.Read(array, 0, 4);
			if (value.IsBetween(1, 3))
			{
				throw new ImageFormatException("Image stream is not valid!");
			}
			Array.Reverse(array);
			chunk.Crc = BitConverter.ToUInt32(array, 0);
			Crc32 crc = new Crc32();
			crc.Update(typeBuffer);
			crc.Update(chunk.Data);
			if (crc.Value != chunk.Crc)
			{
				throw new ImageFormatException("CRC Error. PNG Image chunk is corrupt!");
			}
		}

		private void ReadChunkData(PngChunk chunk)
		{
			chunk.Data = new byte[chunk.Length];
			_stream.Read(chunk.Data, 0, chunk.Length);
		}

		private byte[] ReadChunkType(PngChunk chunk)
		{
			byte[] array = new byte[4];
			int value = _stream.Read(array, 0, 4);
			if (value.IsBetween(1, 3))
			{
				throw new ImageFormatException("Image stream is not valid!");
			}
			chunk.Type = new string(new char[4]
			{
				(char)array[0],
				(char)array[1],
				(char)array[2],
				(char)array[3]
			});
			return array;
		}

		private int ReadChunkLength(PngChunk chunk)
		{
			byte[] array = new byte[4];
			int num = _stream.Read(array, 0, 4);
			if (num.IsBetween(1, 3))
			{
				throw new ImageFormatException("Image stream is not valid!");
			}
			Array.Reverse(array);
			chunk.Length = BitConverter.ToInt32(array, 0);
			return num;
		}

		public PngDecoder()
		{
		}




	}
}
