namespace BitMiracle.LibJpeg.Classic.Internal
{
	internal struct bitread_working_state
	{
		public int get_buffer;

		public int bits_left;

		public jpeg_decompress_struct cinfo;
	}
}
