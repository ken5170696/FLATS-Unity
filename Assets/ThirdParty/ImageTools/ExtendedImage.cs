using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Contracts.Reign;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using ImageTools.IO;
namespace ImageTools
{
	[DebuggerDisplay("Image: {PixelWidth}x{PixelHeight}")]
	public sealed class ExtendedImage : ImageBase
	{
		public const double DefaultDensityX = 75.0;

		public const double DefaultDensityY = 75.0;

		private ImageFrameCollection _frames;

		private ImagePropertyCollection _properties;

		public bool IsLoading { get; private set; }

		public double DensityX { get; set; }

		public double DensityY { get; set; }

		public double InchWidth
		{
			get
			{
				double num = DensityX;
				if (num <= 0.0)
				{
					num = 75.0;
				}
				return (double)base.PixelWidth / num;
			}
		}

		public double InchHeight
		{
			get
			{
				double num = DensityY;
				if (num <= 0.0)
				{
					num = 75.0;
				}
				return (double)base.PixelHeight / num;
			}
		}

		public bool IsAnimated
		{
			get
			{
				return _frames.Count > 0;
			}
		}

		public ImageFrameCollection Frames
		{
			get
			{
				return _frames;
			}
		}

		public ImagePropertyCollection Properties
		{
			get
			{
				return _properties;
			}
		}

		public ExtendedImage(int width, int height) : base(width, height)
		{
			_frames = new ImageFrameCollection();
			_properties = new ImagePropertyCollection();
			
			Contract.Requires<ArgumentException>(width >= 0, "Width must be greater or equals than zero.");
			Contract.Requires<ArgumentException>(height >= 0, "Height must be greater or equals than zero.");
			DensityX = 75.0;
			DensityY = 75.0;
		}

		public ExtendedImage(ExtendedImage other) : base(other)
		{
			_frames = new ImageFrameCollection();
			_properties = new ImagePropertyCollection();
			
			Contract.Requires<ArgumentNullException>(other != null, "Other image cannot be null.");
			Contract.Requires<ArgumentException>(other.IsFilled, "Other image has not been loaded.");
			foreach (ImageFrame frame in other.Frames)
			{
				if (frame != null)
				{
					if (!frame.IsFilled)
					{
						throw new ArgumentException("The image contains a frame that has not been loaded yet.");
					}
					Frames.Add(new ImageFrame(frame));
				}
			}
			DensityX = 75.0;
			DensityY = 75.0;
		}

		public ExtendedImage()
		{
			_frames = new ImageFrameCollection();
			_properties = new ImagePropertyCollection();

			DensityX = 75.0;
			DensityY = 75.0;
		}

		private void Load(Stream stream)
		{
			try
			{
				if (!stream.CanRead)
				{
					throw new NotSupportedException("Cannot read from the stream.");
				}
				if (!stream.CanSeek)
				{
					throw new NotSupportedException("The stream does not support seeking.");
				}
				ReadOnlyCollection<IImageDecoder> availableDecoders = Decoders.GetAvailableDecoders();
				if (availableDecoders.Count > 0)
				{
					int num = availableDecoders.Max((IImageDecoder x) => x.HeaderSize);
					if (num > 0)
					{
						byte[] header = new byte[num];
						stream.Read(header, 0, num);
						stream.Position = 0L;
						IImageDecoder imageDecoder = availableDecoders.FirstOrDefault((IImageDecoder x) => x.IsSupportedFileFormat(header));
						if (imageDecoder != null)
						{
							imageDecoder.Decode(this, stream);
							IsLoading = false;
						}
					}
				}
				if (!IsLoading)
				{
					return;
				}
				IsLoading = false;
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.AppendLine("Image cannot be loaded. Available decoders:");
				foreach (IImageDecoder item in availableDecoders)
				{
					stringBuilder.AppendLine("-" + item);
				}
				throw new UnsupportedImageFormatException(stringBuilder.ToString());
			}
			finally
			{
				stream.Dispose();
			}
		}

		public ExtendedImage Clone()
		{
			return new ExtendedImage(this);
		}

		public static ExtendedImage ApplyFilters(ExtendedImage source, params IImageFilter[] filters)
		{
			Contract.Requires<ArgumentNullException>(source != null, "Source image cannot be null.");
			Contract.Requires<ArgumentException>(source.IsFilled, "Source image has not been loaded.");
			Contract.Requires<ArgumentNullException>(filters != null, "Filters cannot be null.");
			Rectangle bounds = source.Bounds;
			foreach (IImageFilter filter in filters)
			{
				ExtendedImage source2 = source;
				Action<ImageBase, ImageBase> action = delegate(ImageBase sourceImage, ImageBase targetImage)
				{
					filter.Apply(targetImage, sourceImage, bounds);
				};
				source = PerformAction(source2, true, action);
			}
			return source;
		}

		public static ExtendedImage ApplyFilters(ExtendedImage source, Rectangle rectangle, params IImageFilter[] filters)
		{
			Contract.Requires<ArgumentNullException>(source != null, "Source image cannot be null.");
			Contract.Requires<ArgumentException>(source.IsFilled, "Source image has not been loaded.");
			Contract.Requires<ArgumentNullException>(filters != null, "Filters cannot be null.");
			foreach (IImageFilter filter in filters)
			{
				ExtendedImage source2 = source;
				Action<ImageBase, ImageBase> action = delegate(ImageBase sourceImage, ImageBase targetImage)
				{
					filter.Apply(targetImage, sourceImage, rectangle);
				};
				source = PerformAction(source2, true, action);
			}
			return source;
		}

		public static ExtendedImage Crop(ExtendedImage source, Rectangle bounds)
		{
			Contract.Requires<ArgumentNullException>(source != null, "Source image cannot be null.");
			Contract.Requires<ArgumentException>(source.IsFilled, "Source image has not been loaded.");
			return PerformAction(source, false, delegate(ImageBase sourceImage, ImageBase targetImage)
			{
				ImageBase.Crop(sourceImage, targetImage, bounds);
			});
		}

		public static ExtendedImage Transform(ExtendedImage source, RotationType rotationType, FlippingType flippingType)
		{
			Contract.Requires<ArgumentNullException>(source != null, "Source image cannot be null.");
			Contract.Requires<ArgumentException>(source.IsFilled, "Source image has not been loaded.");
			return PerformAction(source, false, delegate(ImageBase sourceImage, ImageBase targetImage)
			{
				ImageBase.Transform(sourceImage, targetImage, rotationType, flippingType);
			});
		}

		public static ExtendedImage Resize(ExtendedImage source, int width, int height, IImageResizer resizer)
		{
			Contract.Requires<ArgumentNullException>(source != null, "Source image cannot be null.");
			Contract.Requires<ArgumentException>(source.IsFilled, "Source image has not been loaded.");
			Contract.Requires<ArgumentNullException>(resizer != null, "Image Resizer cannot be null.");
			return PerformAction(source, false, delegate(ImageBase sourceImage, ImageBase targetImage)
			{
				resizer.Resize(sourceImage, targetImage, width, height);
			});
		}

		public static ExtendedImage Resize(ExtendedImage source, int size, IImageResizer resizer)
		{
			Contract.Requires<ArgumentNullException>(source != null, "Source image cannot be null.");
			Contract.Requires<ArgumentException>(source.IsFilled, "Source image has not been loaded.");
			Contract.Requires<ArgumentNullException>(resizer != null, "Image Resizer cannot be null.");
			int width = 0;
			int height = 0;
			float num = (float)source.PixelWidth / (float)source.PixelHeight;
			if (source.PixelWidth > source.PixelHeight && num > 0f)
			{
				width = size;
				height = (int)((float)width / num);
			}
			else
			{
				height = size;
				width = (int)((float)height * num);
			}
			return PerformAction(source, false, delegate(ImageBase sourceImage, ImageBase targetImage)
			{
				resizer.Resize(sourceImage, targetImage, width, height);
			});
		}

		private static ExtendedImage PerformAction(ExtendedImage source, bool clone, Action<ImageBase, ImageBase> action)
		{
			VerifyHasLoaded(source);
			ExtendedImage extendedImage = (clone ? new ExtendedImage(source) : new ExtendedImage());
			action(source, extendedImage);
			foreach (ImageFrame frame in source.Frames)
			{
				ImageFrame imageFrame = new ImageFrame();
				action(frame, imageFrame);
				if (!clone)
				{
					extendedImage.Frames.Add(imageFrame);
				}
			}
			return extendedImage;
		}

		private static void VerifyHasLoaded(ExtendedImage image)
		{
			if (!image.IsFilled)
			{
				throw new InvalidOperationException("Image has not been loaded");
			}
			foreach (ImageFrame frame in image.Frames)
			{
				if (frame != null && frame.IsFilled)
				{
					throw new InvalidOperationException("Not all frames has been loaded yet.");
				}
			}
		}

		[CompilerGenerated]
		private static int _003CLoad_003Eb__0(IImageDecoder x)
		{
			return x.HeaderSize;
		}




	}
}
