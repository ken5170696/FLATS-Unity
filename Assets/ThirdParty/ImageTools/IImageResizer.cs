namespace ImageTools
{
	public interface IImageResizer
	{
		void Resize(ImageBase source, ImageBase target, int width, int height);
	}
}
