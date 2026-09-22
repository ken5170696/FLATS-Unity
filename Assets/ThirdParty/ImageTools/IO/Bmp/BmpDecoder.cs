using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using ImageTools.Helpers;
namespace ImageTools.IO.Bmp
{
	public class BmpDecoder : IImageDecoder
	{
		private const int Rgb16RMask = 31744;

		private const int Rgb16GMask = 992;

		private const int Rgb16BMask = 31;

		private Stream _stream;

		private BmpFileHeader _fileHeader;

		private BmpInfoHeader _infoHeader;

		public int HeaderSize
		{
			get
			{
				return 2;
			}
		}

		public bool IsSupportedFileExtension(string extension)
		{
			Guard.NotNullOrEmpty(extension, "extension");
			string text = extension.ToUpper();
			if (!(text == "BMP"))
			{
				return text == "DIP";
			}
			return true;
		}

		public bool IsSupportedFileFormat(byte[] header)
		{
			Guard.NotNull(header, "header");
			bool result = false;
			if (header.Length >= 2)
			{
				result = header[0] == 66 && header[1] == 77;
			}
			return result;
		}

		public void Decode(ExtendedImage image, Stream stream)
		{
			_stream = stream;
			try
			{
				ReadFileHeader();
				ReadInfoHeader();
				int num = -1;
				if (_infoHeader.ClrUsed == 0)
				{
					if (_infoHeader.BitsPerPixel == 1 || _infoHeader.BitsPerPixel == 4 || _infoHeader.BitsPerPixel == 8)
					{
						num = (int)Math.Pow(2.0, _infoHeader.BitsPerPixel) * 4;
					}
				}
				else
				{
					num = _infoHeader.ClrUsed * 4;
				}
				byte[] array = null;
				if (num > 0)
				{
					array = new byte[num];
					_stream.Read(array, 0, num);
				}
				byte[] array2 = new byte[_infoHeader.Width * _infoHeader.Height * 4];
				if (_infoHeader.Compression == BmpCompression.RGB)
				{
					if (_infoHeader.HeaderSize != 40)
					{
						throw new ImageFormatException(string.Format(CultureInfo.CurrentCulture, "Header Size value '{0}' is not valid.", new object[1] { _infoHeader.HeaderSize }));
					}
					if (_infoHeader.BitsPerPixel == 32)
					{
						ReadRgb32(array2, _infoHeader.Width, _infoHeader.Height);
					}
					else if (_infoHeader.BitsPerPixel == 24)
					{
						ReadRgb24(array2, _infoHeader.Width, _infoHeader.Height);
					}
					else if (_infoHeader.BitsPerPixel == 16)
					{
						ReadRgb16(array2, _infoHeader.Width, _infoHeader.Height);
					}
					else if (_infoHeader.BitsPerPixel <= 8)
					{
						ReadRgbPalette(array2, array, _infoHeader.Width, _infoHeader.Height, _infoHeader.BitsPerPixel);
					}
					image.SetPixels(_infoHeader.Width, _infoHeader.Height, array2);
					return;
				}
				throw new NotSupportedException("Does not support this kind of bitmap files.");
			}
			catch (IndexOutOfRangeException innerEx)
			{
				throw new ImageFormatException("Bitmap does not have a valid format.", innerEx);
			}
		}

		private void ReadRgbPalette(byte[] imageData, byte[] colors, int width, int height, int bits)
		{
			int num = 8 / bits;
			int num2 = (width + num - 1) / num;
			int num3 = 255 >> 8 - bits;
			byte[] array = new byte[num2 * height];
			_stream.Read(array, 0, array.Length);
			int num4 = num2 % 4;
			if (num4 != 0)
			{
				num4 = 4 - num4;
			}
			for (int i = 0; i < height; i++)
			{
				int num5 = i * (num2 + num4);
				for (int j = 0; j < num2; j++)
				{
					int num6 = num5 + j;
					int num7 = Invert(i, height);
					int num8 = j * num;
					for (int k = 0; k < num && num8 + k < width; k++)
					{
						int num9 = (array[num6] >> 8 - bits - k * bits) & num3;
						int num10 = (num7 * width + (num8 + k)) * 4;
						imageData[num10] = colors[num9 * 4 + 2];
						imageData[num10 + 1] = colors[num9 * 4 + 1];
						imageData[num10 + 2] = colors[num9 * 4];
						imageData[num10 + 3] = byte.MaxValue;
					}
				}
			}
		}

		private void ReadRgb16(byte[] imageData, int width, int height)
		{
			int num = 8;
			int num2 = 4;
			int alignment = 0;
			byte[] imageArray = GetImageArray(width, height, 2, ref alignment);
			for (int i = 0; i < height; i++)
			{
				int num3 = i * (width * 2 + alignment);
				int num4 = Invert(i, height);
				for (int j = 0; j < width; j++)
				{
					int startIndex = num3 + j * 2;
					short num5 = BitConverter.ToInt16(imageArray, startIndex);
					byte b = (byte)(((num5 & 0x7C00) >> 11) * num);
					byte b2 = (byte)(((num5 & 0x3E0) >> 5) * num2);
					byte b3 = (byte)((num5 & 0x1F) * num);
					int num6 = (num4 * width + j) * 4;
					imageData[num6] = b;
					imageData[num6 + 1] = b2;
					imageData[num6 + 2] = b3;
					imageData[num6 + 3] = byte.MaxValue;
				}
			}
		}

		private void ReadRgb24(byte[] imageData, int width, int height)
		{
			int alignment = 0;
			byte[] imageArray = GetImageArray(width, height, 3, ref alignment);
			for (int i = 0; i < height; i++)
			{
				int num = i * (width * 3 + alignment);
				int num2 = Invert(i, height);
				for (int j = 0; j < width; j++)
				{
					int num3 = num + j * 3;
					int num4 = (num2 * width + j) * 4;
					imageData[num4] = imageArray[num3 + 2];
					imageData[num4 + 1] = imageArray[num3 + 1];
					imageData[num4 + 2] = imageArray[num3];
					imageData[num4 + 3] = byte.MaxValue;
				}
			}
		}

		private void ReadRgb32(byte[] imageData, int width, int height)
		{
			int alignment = 0;
			byte[] imageArray = GetImageArray(width, height, 4, ref alignment);
			for (int i = 0; i < height; i++)
			{
				int num = i * (width * 4 + alignment);
				int num2 = Invert(i, height);
				for (int j = 0; j < width; j++)
				{
					int num3 = num + j * 4;
					int num4 = (num2 * width + j) * 4;
					imageData[num4] = imageArray[num3 + 2];
					imageData[num4 + 1] = imageArray[num3 + 1];
					imageData[num4 + 2] = imageArray[num3];
					imageData[num4 + 3] = byte.MaxValue;
				}
			}
		}

		private static int Invert(int y, int height)
		{
			int num = 0;
			if (height > 0)
			{
				return height - y - 1;
			}
			return y;
		}

		private byte[] GetImageArray(int width, int height, int bytes, ref int alignment)
		{
			alignment = width * bytes % 4;
			if (alignment != 0)
			{
				alignment = 4 - alignment;
			}
			int num = (width * bytes + alignment) * height;
			byte[] array = new byte[num];
			_stream.Read(array, 0, num);
			return array;
		}

		private void ReadInfoHeader()
		{
			byte[] array = new byte[40];
			_stream.Read(array, 0, 40);
			_infoHeader = new BmpInfoHeader();
			_infoHeader.HeaderSize = BitConverter.ToInt32(array, 0);
			_infoHeader.Width = BitConverter.ToInt32(array, 4);
			_infoHeader.Height = BitConverter.ToInt32(array, 8);
			_infoHeader.Planes = BitConverter.ToInt16(array, 12);
			_infoHeader.BitsPerPixel = BitConverter.ToInt16(array, 14);
			_infoHeader.ImageSize = BitConverter.ToInt32(array, 20);
			_infoHeader.XPelsPerMeter = BitConverter.ToInt32(array, 24);
			_infoHeader.YPelsPerMeter = BitConverter.ToInt32(array, 28);
			_infoHeader.ClrUsed = BitConverter.ToInt32(array, 32);
			_infoHeader.ClrImportant = BitConverter.ToInt32(array, 36);
			_infoHeader.Compression = (BmpCompression)BitConverter.ToInt32(array, 16);
		}

		private void ReadFileHeader()
		{
			byte[] array = new byte[14];
			_stream.Read(array, 0, 14);
			_fileHeader = new BmpFileHeader();
			_fileHeader.Type = BitConverter.ToInt16(array, 0);
			_fileHeader.FileSize = BitConverter.ToInt32(array, 2);
			_fileHeader.Reserved = BitConverter.ToInt32(array, 6);
			_fileHeader.Offset = BitConverter.ToInt32(array, 10);
		}

		public BmpDecoder()
		{
		}




	}
}
