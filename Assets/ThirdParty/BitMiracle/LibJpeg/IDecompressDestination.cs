using System.IO;

namespace BitMiracle.LibJpeg
{
	internal interface IDecompressDestination
	{
		Stream Output { get; }

		void SetImageAttributes(LoadedImageAttributes parameters);

		void BeginWrite();

		void ProcessPixelsRow(byte[] row);

		void EndWrite();
	}
}
