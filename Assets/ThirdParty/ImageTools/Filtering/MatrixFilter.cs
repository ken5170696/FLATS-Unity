using System;
using System.Diagnostics.Contracts.Reign;
using System.Windows.Media.Reign;
using ImageTools.Helpers;
namespace ImageTools.Filtering
{
	public abstract class MatrixFilter : IImageFilter
	{
		private double[,] _filter;

		private double _factor;

		private double _bias;

		protected void Initialize(double[,] filter)
		{
			Contract.Requires<ArgumentNullException>(filter != null, "Filter cannot be null.");
			Initialize(filter, 1.0, 0.0);
		}

		protected void Initialize(double[,] filter, double factor, double bias)
		{
			Contract.Requires<ArgumentNullException>(filter != null, "Filter cannot be null.");
			Guard.GreaterThan(filter.GetLength(0), 0, "filter.GetLength(0)");
			Guard.GreaterThan(filter.GetLength(1), 0, "filter.GetLength(1)");
			if (filter.GetLength(0) % 2 == 0)
			{
				throw new ArgumentException("The number of rows cannot be an even number.", "filter");
			}
			if (filter.GetLength(1) % 2 == 0)
			{
				throw new ArgumentException("The number of columns cannot be an even number.", "filter");
			}
			_filter = filter;
			_factor = factor;
			_bias = bias;
		}

		protected virtual void PrepareFilter()
		{
		}

		public void Apply(ImageBase target, ImageBase source, Rectangle rectangle)
		{
			PrepareFilter();
			if (_filter == null)
			{
				return;
			}
			int length = _filter.GetLength(0);
			for (int i = rectangle.Y; i < rectangle.Bottom; i++)
			{
				for (int j = rectangle.X; j < rectangle.Right; j++)
				{
					double num = 0.0;
					double num2 = 0.0;
					double num3 = 0.0;
					Color value = source[j, i];
					for (int k = 0; k < length; k++)
					{
						for (int l = 0; l < length; l++)
						{
							int x = (j - length / 2 + l + rectangle.Width) % rectangle.Width;
							int y = (i - length / 2 + k + rectangle.Height) % rectangle.Height;
							Color color = source[x, y];
							num += (double)(int)color.R * _filter[l, k];
							num2 += (double)(int)color.G * _filter[l, k];
							num3 += (double)(int)color.B * _filter[l, k];
						}
					}
					value.R = (byte)(_factor * num + _bias).RemainBetween(0.0, 255.0);
					value.G = (byte)(_factor * num2 + _bias).RemainBetween(0.0, 255.0);
					value.B = (byte)(_factor * num3 + _bias).RemainBetween(0.0, 255.0);
					target[j, i] = value;
				}
			}
		}

		protected MatrixFilter()
		{
		}




	}
}
