using System;
using System.Diagnostics.Contracts.Reign;
namespace ImageTools
{
	internal abstract class IBarcodeReaderContract : IBarcodeReader
	{
		BarcodeResult IBarcodeReader.ReadBarcode(ExtendedImage image)
		{
			Contract.Requires<ArgumentNullException>(image != null, "Image cannot be null.");
			Contract.Requires<ArgumentException>(image.IsFilled, "Image cannot be empty.");
			throw new NotImplementedException();
		}

		protected IBarcodeReaderContract()
		{
		}




	}
}
