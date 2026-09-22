using System.IO;

namespace ImageTools.IO
{
	public interface IImageDecoder
	{
		int HeaderSize { get; }

		bool IsSupportedFileExtension(string extension);

		bool IsSupportedFileFormat(byte[] header);

		void Decode(ExtendedImage image, Stream stream);
	}
}
