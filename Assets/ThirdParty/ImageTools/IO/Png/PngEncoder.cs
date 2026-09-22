using System;
using System.IO;
using System.Runtime.InteropServices;
using ICSharpCode.SharpZipLib.Checksums;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using ImageTools.Helpers;
namespace ImageTools.IO.Png
{
	public class PngEncoder : IImageEncoder
	{
		private const int MaxBlockSize = 65535;

		private Stream _stream;

		private ExtendedImage _image;

		public bool IsWritingUncompressed { get; set; }

		public bool IsWritingGamma { get; set; }

		public double Gamma { get; set; }

		public string Extension
		{
			get
			{
				return "PNG";
			}
		}

		public PngEncoder()
		{
			Gamma = 2.200000047683716;
		}

		public bool IsSupportedFileExtension(string extension)
		{
			Guard.NotNullOrEmpty(extension, "extension");
			string text = extension.ToUpper();
			return text == "PNG";
		}

		public void Encode(ExtendedImage image, Stream stream)
		{
			Guard.NotNull(image, "image");
			Guard.NotNull(stream, "stream");
			_image = image;
			_stream = stream;
			stream.Write(new byte[8] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
			PngHeader pngHeader = new PngHeader();
			pngHeader.Width = image.PixelWidth;
			pngHeader.Height = image.PixelHeight;
			pngHeader.ColorType = 6;
			pngHeader.BitDepth = 8;
			pngHeader.FilterMethod = 0;
			pngHeader.CompressionMethod = 0;
			pngHeader.InterlaceMethod = 0;
			WriteHeaderChunk(pngHeader);
			WritePhysicsChunk();
			WriteGammaChunk();
			if (IsWritingUncompressed)
			{
				WriteDataChunksFast();
			}
			else
			{
				WriteDataChunks();
			}
			WriteEndChunk();
			stream.Flush();
		}

		private void WritePhysicsChunk()
		{
			if (_image.DensityX > 0.0 && _image.DensityY > 0.0)
			{
				int value = (int)Math.Round(_image.DensityX * 39.3700787);
				int value2 = (int)Math.Round(_image.DensityY * 39.3700787);
				byte[] array = new byte[9];
				WriteInteger(array, 0, value);
				WriteInteger(array, 4, value2);
				array[8] = 1;
				WriteChunk("pHYs", array);
			}
		}

		private void WriteGammaChunk()
		{
			if (IsWritingGamma)
			{
				int value = (int)(Gamma * 100000.0);
				byte[] array = new byte[4];
				byte[] bytes = BitConverter.GetBytes(value);
				array[0] = bytes[3];
				array[1] = bytes[2];
				array[2] = bytes[1];
				array[3] = bytes[0];
				WriteChunk("gAMA", array);
			}
		}

		private void WriteDataChunksFast()
		{
			byte[] pixels = _image.Pixels;
			byte[] array = new byte[_image.PixelWidth * _image.PixelHeight * 4 + _image.PixelHeight];
			int num = _image.PixelWidth * 4 + 1;
			for (int i = 0; i < _image.PixelHeight; i++)
			{
				array[i * num] = 0;
				Array.Copy(pixels, i * _image.PixelWidth * 4, array, i * num + 1, _image.PixelWidth * 4);
			}
			Adler32 adler = new Adler32();
			adler.Update(array);
			using (MemoryStream memoryStream = new MemoryStream())
			{
				int num2 = array.Length;
				int num3 = ((array.Length % 65535 != 0) ? (array.Length / 65535 + 1) : (array.Length / 65535));
				memoryStream.WriteByte(120);
				memoryStream.WriteByte(218);
				for (int j = 0; j < num3; j++)
				{
					ushort num4 = (ushort)((num2 < 65535) ? ((uint)num2) : 65535u);
					if (num4 == num2)
					{
						memoryStream.WriteByte(1);
					}
					else
					{
						memoryStream.WriteByte(0);
					}
					memoryStream.Write(BitConverter.GetBytes(num4), 0, 2);
					memoryStream.Write(BitConverter.GetBytes((ushort)(~num4)), 0, 2);
					memoryStream.Write(array, j * 65535, num4);
					num2 -= 65535;
				}
				WriteInteger(memoryStream, (int)adler.Value);
				memoryStream.Seek(0L, SeekOrigin.Begin);
				byte[] array2 = new byte[memoryStream.Length];
				memoryStream.Read(array2, 0, (int)memoryStream.Length);
				WriteChunk("IDAT", array2);
			}
		}

		private void WriteDataChunks()
		{
			byte[] pixels = _image.Pixels;
			byte[] array = new byte[_image.PixelWidth * _image.PixelHeight * 4 + _image.PixelHeight];
			int num = _image.PixelWidth * 4 + 1;
			for (int i = 0; i < _image.PixelHeight; i++)
			{
				byte b = 0;
				if (i > 0)
				{
					b = 2;
				}
				array[i * num] = b;
				for (int j = 0; j < _image.PixelWidth; j++)
				{
					int num2 = i * num + j * 4 + 1;
					int num3 = (i * _image.PixelWidth + j) * 4;
					array[num2] = pixels[num3];
					array[num2 + 1] = pixels[num3 + 1];
					array[num2 + 2] = pixels[num3 + 2];
					array[num2 + 3] = pixels[num3 + 3];
					if (i > 0)
					{
						int num4 = ((i - 1) * _image.PixelWidth + j) * 4;
						array[num2] -= pixels[num4];
						array[num2 + 1] -= pixels[num4 + 1];
						array[num2 + 2] -= pixels[num4 + 2];
						array[num2 + 3] -= pixels[num4 + 3];
					}
				}
			}
			byte[] data = null;
			int num5 = 0;
			MemoryStream memoryStream = null;
			try
			{
				memoryStream = new MemoryStream();
				using (DeflaterOutputStream deflaterOutputStream = new DeflaterOutputStream(memoryStream))
				{
					deflaterOutputStream.Write(array, 0, array.Length);
					deflaterOutputStream.Flush();
					deflaterOutputStream.Finish();
					num5 = (int)memoryStream.Length;
					data = memoryStream.ToArray();
				}
			}
			finally
			{
				if (memoryStream != null)
				{
					memoryStream.Dispose();
				}
			}
			int num6 = num5 / 65535;
			if (num5 % 65535 != 0)
			{
				num6++;
			}
			for (int k = 0; k < num6; k++)
			{
				int num7 = num5 - k * 65535;
				if (num7 > 65535)
				{
					num7 = 65535;
				}
				WriteChunk("IDAT", data, k * 65535, num7);
			}
		}

		private void WriteEndChunk()
		{
			WriteChunk("IEND", null);
		}

		private void WriteHeaderChunk(PngHeader header)
		{
			byte[] array = new byte[13];
			WriteInteger(array, 0, header.Width);
			WriteInteger(array, 4, header.Height);
			array[8] = header.BitDepth;
			array[9] = header.ColorType;
			array[10] = header.CompressionMethod;
			array[11] = header.FilterMethod;
			array[12] = header.InterlaceMethod;
			WriteChunk("IHDR", array);
		}

		private void WriteChunk(string type, byte[] data)
		{
			WriteChunk(type, data, 0, (data != null) ? data.Length : 0);
		}

		private void WriteChunk(string type, byte[] data, int offset, int length)
		{
			WriteInteger(_stream, length);
			byte[] buffer = new byte[4]
			{
				(byte)type[0],
				(byte)type[1],
				(byte)type[2],
				(byte)type[3]
			};
			_stream.Write(buffer, 0, 4);
			if (data != null)
			{
				_stream.Write(data, offset, length);
			}
			Crc32 crc = new Crc32();
			crc.Update(buffer);
			if (data != null)
			{
				crc.Update(data, offset, length);
			}
			WriteInteger(_stream, (uint)crc.Value);
		}

		private static void WriteInteger(byte[] data, int offset, int value)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			Array.Reverse(bytes);
			Array.Copy(bytes, 0, data, offset, 4);
		}

		private static void WriteInteger(Stream stream, int value)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			Array.Reverse(bytes);
			stream.Write(bytes, 0, 4);
		}

		private static void WriteInteger(Stream stream, uint value)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			Array.Reverse(bytes);
			stream.Write(bytes, 0, 4);
		}




	}
}
