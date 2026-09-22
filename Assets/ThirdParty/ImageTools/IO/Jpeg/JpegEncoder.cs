using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Reign;
using FluxJpeg.Core;
using FluxJpeg.Core.Encoder;
using ImageTools.Helpers;
namespace ImageTools.IO.Jpeg
{
	public class JpegEncoder : IImageEncoder
	{
		private Color _transparentColor;

		private int _quality;

		public Color TransparentColor
		{
			get
			{
				return _transparentColor;
			}
			set
			{
				_transparentColor = value;
			}
		}

		public int Quality
		{
			get
			{
				return _quality;
			}
			set
			{
				_quality = value;
			}
		}

		public string Extension
		{
			get
			{
				return "JPG";
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

		public void Encode(ExtendedImage image, Stream stream)
		{
			Guard.NotNull(image, "image");
			Guard.NotNull(stream, "stream");
			int pixelWidth = image.PixelWidth;
			int pixelHeight = image.PixelHeight;
			byte[] pixels = image.Pixels;
			byte[][,] array = new byte[3][,];
			for (int i = 0; i < 3; i++)
			{
				array[i] = new byte[pixelWidth, pixelHeight];
			}
			for (int j = 0; j < pixelHeight; j++)
			{
				for (int k = 0; k < pixelWidth; k++)
				{
					int num = (j * pixelWidth + k) * 4;
					float num2 = (float)(int)pixels[num + 3] / 255f;
					array[0][k, j] = (byte)((float)(int)pixels[num] * num2 + (1f - num2) * (float)(int)_transparentColor.R);
					array[1][k, j] = (byte)((float)(int)pixels[num + 1] * num2 + (1f - num2) * (float)(int)_transparentColor.G);
					array[2][k, j] = (byte)((float)(int)pixels[num + 2] * num2 + (1f - num2) * (float)(int)_transparentColor.B);
				}
			}
			Image image2 = new Image(new ColorModel
			{
				ColorSpace = ColorSpace.RGB,
				Opaque = false
			}, array);
			if (image.DensityX > 0.0 && image.DensityY > 0.0)
			{
				image2.DensityX = image.DensityX;
				image2.DensityY = image.DensityY;
			}
			DecodedJpeg decodedJpeg = new DecodedJpeg(image2);
			FluxJpeg.Core.Encoder.JpegEncoder jpegEncoder = new FluxJpeg.Core.Encoder.JpegEncoder(decodedJpeg, _quality, stream);
			jpegEncoder.Encode();
		}

		public JpegEncoder()
		{
			_transparentColor = Color.FromArgb(0, byte.MaxValue, byte.MaxValue, byte.MaxValue);
			_quality = 100;

		}




	}
}
