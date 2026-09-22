namespace ImageTools
{
	public interface IImageFilter
	{
		void Apply(ImageBase target, ImageBase source, Rectangle rectangle);
	}
}
