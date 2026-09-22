using System;

namespace ImageTools.Filtering
{
	public sealed class SobelX : MatrixFilter
	{
		public SobelX()
		{
			double[,] filter = new double[3, 3]
			{
				{ -1.0, 0.0, 1.0 },
				{ -2.0, 0.0, 2.0 },
				{ -1.0, 0.0, 1.0 }
			};
			Initialize(filter);
		}


	}
}
