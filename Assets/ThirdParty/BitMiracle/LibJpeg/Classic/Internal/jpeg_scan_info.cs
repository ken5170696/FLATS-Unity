using System;

namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class jpeg_scan_info
	{
		public int comps_in_scan;

		public int[] component_index;

		public int Ss;

		public int Se;

		public int Ah;

		public int Al;

		public jpeg_scan_info()
		{
			component_index = new int[4];

		}


	}
}
