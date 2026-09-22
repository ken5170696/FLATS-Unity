using System;
using System.IO;
using System.Runtime.InteropServices;
using ImageTools.Helpers;
namespace ImageTools.IO.Bmp
{
	public class BmpEncoder : IImageEncoder
	{
		public string Extension
		{
			get
			{
				return "bmp";
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

		public void Encode(ExtendedImage image, Stream stream)
		{
			Guard.NotNull(image, "image");
			Guard.NotNull(stream, "stream");
			int num = image.PixelWidth;
			int num2 = image.PixelWidth * 3 % 4;
			if (num2 != 0)
			{
				num += 4 - num2;
			}
			BinaryWriter binaryWriter = new BinaryWriter(stream);
			BmpFileHeader bmpFileHeader = new BmpFileHeader();
			bmpFileHeader.Type = 19778;
			bmpFileHeader.Offset = 54;
			bmpFileHeader.FileSize = 54 + image.PixelHeight * num * 3;
			Write(binaryWriter, bmpFileHeader);
			BmpInfoHeader bmpInfoHeader = new BmpInfoHeader();
			bmpInfoHeader.HeaderSize = 40;
			bmpInfoHeader.Height = image.PixelHeight;
			bmpInfoHeader.Width = image.PixelWidth;
			bmpInfoHeader.BitsPerPixel = 24;
			bmpInfoHeader.Planes = 1;
			bmpInfoHeader.Compression = BmpCompression.RGB;
			bmpInfoHeader.ImageSize = image.PixelHeight * num * 3;
			bmpInfoHeader.ClrUsed = 0;
			bmpInfoHeader.ClrImportant = 0;
			Write(binaryWriter, bmpInfoHeader);
			WriteImage(binaryWriter, image);
			binaryWriter.Flush();
		}

		private static void WriteImage(BinaryWriter writer, ExtendedImage image)
		{
			int num = image.PixelWidth * 3 % 4;
			int num2 = 0;
			if (num != 0)
			{
				num = 4 - num;
			}
			byte[] pixels = image.Pixels;
			for (int num3 = image.PixelHeight - 1; num3 >= 0; num3--)
			{
				for (int i = 0; i < image.PixelWidth; i++)
				{
					num2 = (num3 * image.PixelWidth + i) * 4;
					writer.Write(pixels[num2 + 2]);
					writer.Write(pixels[num2 + 1]);
					writer.Write(pixels[num2]);
				}
				for (int j = 0; j < num; j++)
				{
					writer.Write((byte)0);
				}
			}
		}

		private static void Write(BinaryWriter writer, BmpFileHeader fileHeader)
		{
			writer.Write(fileHeader.Type);
			writer.Write(fileHeader.FileSize);
			writer.Write(fileHeader.Reserved);
			writer.Write(fileHeader.Offset);
		}

		private static void Write(BinaryWriter writer, BmpInfoHeader infoHeader)
		{
			writer.Write(infoHeader.HeaderSize);
			writer.Write(infoHeader.Width);
			writer.Write(infoHeader.Height);
			writer.Write(infoHeader.Planes);
			writer.Write(infoHeader.BitsPerPixel);
			writer.Write((int)infoHeader.Compression);
			writer.Write(infoHeader.ImageSize);
			writer.Write(infoHeader.XPelsPerMeter);
			writer.Write(infoHeader.YPelsPerMeter);
			writer.Write(infoHeader.ClrUsed);
			writer.Write(infoHeader.ClrImportant);
		}

		public BmpEncoder()
		{
		}




	}
}
