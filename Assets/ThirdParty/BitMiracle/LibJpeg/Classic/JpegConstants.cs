namespace BitMiracle.LibJpeg.Classic
{
	internal static class JpegConstants
	{
		public const int DCTSIZE = 8;

		public const int DCTSIZE2 = 64;

		public const int NUM_QUANT_TBLS = 4;

		public const int NUM_HUFF_TBLS = 4;

		public const int MAX_COMPS_IN_SCAN = 4;

		public const int C_MAX_BLOCKS_IN_MCU = 10;

		public const int D_MAX_BLOCKS_IN_MCU = 10;

		public const int MAX_SAMP_FACTOR = 4;

		public const int MAX_COMPONENTS = 10;

		public const int BITS_IN_JSAMPLE = 8;

		public const int JPEG_MAX_DIMENSION = 65500;

		public const int MAXJSAMPLE = 255;

		public const int CENTERJSAMPLE = 128;

		public const int RGB_RED = 0;

		public const int RGB_GREEN = 1;

		public const int RGB_BLUE = 2;

		public const int RGB_PIXELSIZE = 3;

		public const int HUFF_LOOKAHEAD = 8;

		public static J_DCT_METHOD JDCT_DEFAULT = J_DCT_METHOD.JDCT_ISLOW;

		public static J_DCT_METHOD JDCT_FASTEST = J_DCT_METHOD.JDCT_IFAST;
	}
}
