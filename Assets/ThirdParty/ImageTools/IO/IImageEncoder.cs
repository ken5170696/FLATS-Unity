using System.IO;

namespace ImageTools.IO
{
	public interface IImageEncoder
	{
		string Extension { get; }

		bool IsSupportedFileExtension(string extension);

		void Encode(ExtendedImage image, Stream stream);
	}
}
