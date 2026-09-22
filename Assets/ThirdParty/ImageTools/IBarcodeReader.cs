namespace ImageTools
{
	public interface IBarcodeReader
	{
		BarcodeResult ReadBarcode(ExtendedImage image);
	}
}
