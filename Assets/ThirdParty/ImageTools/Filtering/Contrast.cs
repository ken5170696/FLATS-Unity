using System;
using System.Diagnostics.Contracts.Reign;
using System.Windows.Media.Reign;
using ImageTools.Helpers;
namespace ImageTools.Filtering
{
	public sealed class Contrast : IImageFilter
	{
		private int _contrast;

		public Contrast(int contrast)
		{
			Contract.Requires<ArgumentException>(contrast >= -255, "Brightness must be greater than -255.");
			Contract.Requires<ArgumentException>(contrast <= 255, "Brightness must be less than 255.");
			_contrast = contrast;
		}

		public void Apply(ImageBase target, ImageBase source, Rectangle rectangle)
		{
			double num = 0.0;
			double num2 = (100.0 + (double)_contrast) / 100.0;
			for (int i = rectangle.Y; i < rectangle.Bottom; i++)
			{
				for (int j = rectangle.X; j < rectangle.Right; j++)
				{
					Color value = source[j, i];
					num = (double)(int)value.R / 255.0;
					num -= 0.5;
					num *= num2;
					num += 0.5;
					num *= 255.0;
					num = num.RemainBetween(0.0, 255.0);
					value.R = (byte)num;
					num = (double)(int)value.G / 255.0;
					num -= 0.5;
					num *= num2;
					num += 0.5;
					num *= 255.0;
					num = num.RemainBetween(0.0, 255.0);
					value.G = (byte)num;
					num = (double)(int)value.B / 255.0;
					num -= 0.5;
					num *= num2;
					num += 0.5;
					num *= 255.0;
					num = num.RemainBetween(0.0, 255.0);
					value.B = (byte)num;
					target[j, i] = value;
				}
			}
		}




	}
}
