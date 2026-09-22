using System;
namespace ImageTools.Filtering
{
	public sealed class GaussianBlur : MatrixFilter
	{
		private double _oldVariance;

		public double Variance { get; set; }

		protected override void PrepareFilter()
		{
			if (_oldVariance == Variance || !(Variance > 0.0))
			{
				return;
			}
			int num = (int)(2.0 * Variance) * 2 + 1;
			double[,] array = new double[num, num];
			for (int i = 0; i < num; i++)
			{
				for (int j = 0; j < num; j++)
				{
					int num2 = j - num / 2;
					int num3 = i - num / 2;
					double num4 = Variance * Variance;
					int num5 = num2 * num2;
					int num6 = num3 * num3;
					double num7 = 1.0 / (Math.PI * 2.0 * num4);
					double y = (double)(-(num5 + num6)) / (2.0 * num4);
					array[j, i] = num7 * Math.Pow(Math.E, y);
				}
			}
			Initialize(array);
			_oldVariance = Variance;
		}

		public GaussianBlur()
		{
		}




	}
}
