using System;
using System.Diagnostics.Contracts.Reign;
using System.Windows.Media.Reign;
using ImageTools.Helpers;
namespace ImageTools.Filtering
{
	public sealed class Brightness : IImageFilter
	{
		private int _brightness;

		public Brightness(int brightness)
		{
			Contract.Requires<ArgumentException>(brightness >= -255, "Brightness must be greater than -255.");
			Contract.Requires<ArgumentException>(brightness <= 255, "Brightness must be less than 255.");
			_brightness = brightness;
		}

		public void Apply(ImageBase target, ImageBase source, Rectangle rectangle)
		{
			for (int i = rectangle.Y; i < rectangle.Bottom; i++)
			{
				for (int j = rectangle.X; j < rectangle.Right; j++)
				{
					Color value = source[j, i];
					int value2 = value.R + _brightness;
					int value3 = value.G + _brightness;
					int value4 = value.B + _brightness;
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
