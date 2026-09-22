using System;
using System.Windows.Media.Reign;
namespace ImageTools.Filtering
{
	public sealed class Inverter : IImageFilter
	{
		public void Apply(ImageBase target, ImageBase source, Rectangle rectangle)
		{
			for (int i = rectangle.Y; i < rectangle.Bottom; i++)
			{
				for (int j = rectangle.X; j < rectangle.Right; j++)
				{
					Color value = source[j, i];
					value.R = (byte)(255 - value.R);
					value.G = (byte)(255 - value.G);
					value.B = (byte)(255 - value.B);
					target[j, i] = value;
				}
			}
		}

		public Inverter()
		{
		}




	}
}
