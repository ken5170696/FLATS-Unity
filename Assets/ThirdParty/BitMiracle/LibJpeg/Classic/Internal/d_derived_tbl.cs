using System;

namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal class d_derived_tbl
	{
		public int[] maxcode;

		public int[] valoffset;

		public JHUFF_TBL pub;

		public int[] look_nbits;

		public byte[] look_sym;

		public d_derived_tbl()
		{
			maxcode = new int[18];
			valoffset = new int[17];
			look_nbits = new int[256];
			look_sym = new byte[256];

		}


	}
}
