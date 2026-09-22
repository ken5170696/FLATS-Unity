using System;
using System.Diagnostics.Contracts.Reign;
using System.Windows.Media.Reign;
using ImageTools.Helpers;
namespace ImageTools.Filtering
{
	public sealed class BlendingFilter : IImageFilter
	{
		private readonly ImageBase _blendedImage;

		public double? GlobalAlphaFactor { get; set; }

		public BlendingFilter(ImageBase blendedImage)
		{
			Contract.Requires<ArgumentException>(blendedImage != null, "Pased image is not allowed to be null!");
			_blendedImage = blendedImage;
		}

		public void Apply(ImageBase target, ImageBase source, Rectangle rectangle)
		{
			if (rectangle.Right > _blendedImage.PixelWidth)
			{
				rectangle.Width = _blendedImage.PixelWidth - rectangle.Left;
			}
			if (rectangle.Bottom > _blendedImage.PixelHeight)
			{
				rectangle.Height = _blendedImage.PixelHeight - rectangle.Top;
			}
			for (int i = rectangle.Y; i < rectangle.Bottom; i++)
			{
				for (int j = rectangle.X; j < rectangle.Right; j++)
				{
					Color value = source[j, i];
					Color color = _blendedImage[j, i];
					double num = (GlobalAlphaFactor.HasValue ? GlobalAlphaFactor.Value : ((double)(int)color.A / 255.0));
					double num2 = 1.0 - num;
					int value2 = (int)((double)(int)value.R * num2) + (int)((double)(int)color.R * num);
					int value3 = (int)((double)(int)value.G * num2) + (int)((double)(int)color.G * num);
					int value4 = (int)((double)(int)value.B * num2) + (int)((double)(int)color.B * num);
					value2 = value2.RemainBetween(0, 255);
					value3 = value3.RemainBetween(0, 255);
					value4 = value4.RemainBetween(0, 255);
					value.R = (byte)value2;
					value.G = (byte)value3;
					value.B = (byte)value4;
					target[j, i] = value;
				}
			}
		}




	}
}
