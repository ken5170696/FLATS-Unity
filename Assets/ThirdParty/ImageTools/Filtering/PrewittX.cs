using System;

namespace ImageTools.Filtering
{
	public sealed class PrewittX : MatrixFilter
	{
		public PrewittX()
		{
			double[,] filter = new double[3, 3]
			{
				{ -1.0, 0.0, 1.0 },
				{ -1.0, 0.0, 1.0 },
				{ -1.0, 0.0, 1.0 }
			};
			Initialize(filter);
		}


	}
}
