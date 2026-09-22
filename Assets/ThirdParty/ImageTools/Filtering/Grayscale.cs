using System;
using System.Windows.Media.Reign;
namespace ImageTools.Filtering
{
	public abstract class Grayscale : IImageFilter
	{
		private double _cr;

		private double _cg;

		private double _cb;

		protected Grayscale(double redCoefficient, double greenCoefficient, double blueCoefficient)
		{
			_cr = redCoefficient;
			_cg = greenCoefficient;
			_cb = blueCoefficient;
		}

		public void Apply(ImageBase target, ImageBase source, Rectangle rectangle)
		{
			byte b = 0;
			for (int i = rectangle.Y; i < rectangle.Bottom; i++)
			{
				for (int j = rectangle.X; j < rectangle.Right; j++)
				{
					Color value = source[j, i];
					b = (value.R = (byte)((double)(int)value.R * _cr + (double)(int)value.G * _cg + (double)(int)value.B * _cb));
					value.G = b;
					value.B = b;
					target[j, i] = value;
				}
			}
		}




	}
}
