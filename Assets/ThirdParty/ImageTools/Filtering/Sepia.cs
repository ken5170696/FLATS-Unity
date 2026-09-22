using System;
using System.Windows.Media.Reign;
namespace ImageTools.Filtering
{
	public sealed class Sepia : IImageFilter
	{
		public void Apply(ImageBase target, ImageBase source, Rectangle rectangle)
		{
			byte b = 0;
			for (int i = rectangle.Y; i < rectangle.Bottom; i++)
			{
				for (int j = rectangle.X; j < rectangle.Right; j++)
				{
					Color value = source[j, i];
					b = (byte)(0.299 * (double)(int)value.R + 0.587 * (double)(int)value.G + 0.114 * (double)(int)value.B);
					value.R = (byte)((b > 206) ? 255u : ((uint)(b + 49)));
					value.G = (byte)((b >= 14) ? ((uint)(b - 14)) : 0u);
					value.B = (byte)((b >= 56) ? ((uint)(b - 56)) : 0u);
					target[j, i] = value;
				}
			}
		}

		public Sepia()
		{
		}




	}
}
