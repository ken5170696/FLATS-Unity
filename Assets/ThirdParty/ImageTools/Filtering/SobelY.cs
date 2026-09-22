using System;

namespace ImageTools.Filtering
{
	public sealed class SobelY : MatrixFilter
	{
		public SobelY()
		{
			double[,] filter = new double[3, 3]
			{
				{ -1.0, -2.0, -1.0 },
				{ 0.0, 0.0, 0.0 },
				{ 1.0, 2.0, 1.0 }
			};
			Initialize(filter);
		}


	}
}
