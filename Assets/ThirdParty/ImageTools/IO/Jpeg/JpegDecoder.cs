using System;
using System.IO;
using System.Runtime.InteropServices;
using BitMiracle.LibJpeg;
using FluxJpeg.Core;
using FluxJpeg.Core.Decoder;
using ImageTools.Helpers;
namespace ImageTools.IO.Jpeg
{
	public class JpegDecoder : IImageDecoder
	{
		public bool UseLegacyLibrary { get; set; }

		public int HeaderSize
		{
			get
			{
				return 11;
			}
		}

		public bool IsSupportedFileExtension(string extension)
		{
			Guard.NotNullOrEmpty(extension, "extension");
			string text = extension.ToUpper();
			if (!(text == "JPG") && !(text == "JPEG"))
			{
				return text == "JFIF";
			}
			return true;
		}

		public bool IsSupportedFileFormat(byte[] header)
		{
			Guard.NotNull(header, "header");
			bool result = false;
			if (header.Length >= 11)
			{
				bool flag = IsJpeg(header);
				bool flag2 = IsExif(header);
				result = flag || flag2;
			}
			return result;
		}

		private bool IsExif(byte[] header)
		{
			return header[6] == 69 && header[7] == 120 && header[8] == 105 && header[9] == 102 && header[10] == 0;
		}

		private static bool IsJpeg(byte[] header)
		{
			return header[6] == 74 && header[7] == 70 && header[8] == 73 && header[9] == 70 && header[10] == 0;
		}

		public void Decode(ExtendedImage image, Stream stream)
		{
			Guard.NotNull(image, "image");
			Guard.NotNull(stream, "stream");
			if (UseLegacyLibrary)
			{
				FluxJpeg.Core.Decoder.JpegDecoder jpegDecoder = new FluxJpeg.Core.Decoder.JpegDecoder(stream);
				DecodedJpeg decodedJpeg = jpegDecoder.Decode();
				decodedJpeg.Image.ChangeColorSpace(ColorSpace.RGB);
				int width = decodedJpeg.Image.Width;
				int height = decodedJpeg.Image.Height;
				byte[] array = new byte[width * height * 4];
				byte[][,] raster = decodedJpeg.Image.Raster;
				for (int i = 0; i < height; i++)
				{
					for (int j = 0; j < width; j++)
					{
						int num = (i * width + j) * 4;
						array[num] = raster[0][j, i];
						array[num + 1] = raster[1][j, i];
						array[num + 2] = raster[2][j, i];
						array[num + 3] = byte.MaxValue;
					}
				}
				image.DensityX = decodedJpeg.Image.DensityX;
				image.DensityY = decodedJpeg.Image.DensityY;
				image.SetPixels(width, height, array);
				return;
			}
			JpegImage jpegImage = new JpegImage(stream);
			int width2 = jpegImage.Width;
			int height2 = jpegImage.Height;
			byte[] array2 = new byte[width2 * height2 * 4];
			if (jpegImage.Colorspace != Colorspace.RGB || jpegImage.BitsPerComponent != 8)
			{
				throw new UnsupportedImageFormatException();
			}
			for (int k = 0; k < height2; k++)
			{
				SampleRow row = jpegImage.GetRow(k);
				for (int l = 0; l < width2; l++)
				{
					Sample at = row.GetAt(l);
					byte b = 0;
					byte b2 = 0;
					byte b3 = 0;
					b = (byte)at[0];
					b2 = (byte)at[1];
					b3 = (byte)at[2];
					int num2 = (k * width2 + l) * 4;
					array2[num2] = b;
					array2[num2 + 1] = b2;
					array2[num2 + 2] = b3;
					array2[num2 + 3] = byte.MaxValue;
				}
			}
			image.SetPixels(width2, height2, array2);
		}

		public JpegDecoder()
		{
		}




	}
}
