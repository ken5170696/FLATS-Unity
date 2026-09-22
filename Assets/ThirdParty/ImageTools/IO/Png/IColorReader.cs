namespace ImageTools.IO.Png
{
	internal interface IColorReader
	{
		void ReadScanline(byte[] scanline, byte[] pixels, PngHeader header);
	}
}
