using System;

namespace ImageTools.Filtering
{
	public sealed class PrewittY : MatrixFilter
	{
		public PrewittY()
		{
			double[,] filter = new double[3, 3]
			{
				{ -1.0, -1.0, -1.0 },
				{ 0.0, 0.0, 0.0 },
				{ 1.0, 1.0, 1.0 }
			};
			Initialize(filter);
		}


	}
}
