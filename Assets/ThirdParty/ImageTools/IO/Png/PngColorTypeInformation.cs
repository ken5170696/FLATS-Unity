using System;
namespace ImageTools.IO.Png
{
	internal sealed class PngColorTypeInformation
	{
		public int[] SupportedBitDepths { get; private set; }

		public Func<byte[], byte[], IColorReader> ScanlineReaderFactory { get; private set; }

		public int ChannelsPerColor { get; private set; }

		public PngColorTypeInformation(int scanlineFactor, int[] supportedBitDepths, Func<byte[], byte[], IColorReader> scanlineReaderFactory)
		{
			ChannelsPerColor = scanlineFactor;
			ScanlineReaderFactory = scanlineReaderFactory;
			SupportedBitDepths = supportedBitDepths;
		}

		public IColorReader CreateColorReader(byte[] palette, byte[] paletteAlpha)
		{
			return ScanlineReaderFactory(palette, paletteAlpha);
		}




	}
}
