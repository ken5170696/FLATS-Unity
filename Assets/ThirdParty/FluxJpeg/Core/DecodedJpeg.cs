using System;
using System.Collections.Generic;
using System.Text;
namespace FluxJpeg.Core
{
	internal class DecodedJpeg
	{
		private Image _image;

		internal int[] BlockWidth;

		internal int[] BlockHeight;

		internal int Precision;

		internal int[] HsampFactor;

		internal int[] VsampFactor;

		internal bool[] lastColumnIsDummy;

		internal bool[] lastRowIsDummy;

		internal int[] compWidth;

		internal int[] compHeight;

		internal int MaxHsampFactor;

		internal int MaxVsampFactor;

		private List<JpegHeader> _metaHeaders;

		public Image Image
		{
			get
			{
				return _image;
			}
		}

		public bool HasJFIF { get; private set; }

		public IList<JpegHeader> MetaHeaders
		{
			get
			{
				return _metaHeaders;
			}
		}

		public DecodedJpeg(Image image, IEnumerable<JpegHeader> metaHeaders)
		{
			Precision = 8;
			HsampFactor = new int[3] { 1, 1, 1 };
			VsampFactor = new int[3] { 1, 1, 1 };
			lastColumnIsDummy = new bool[3];
			lastRowIsDummy = new bool[3];

			_image = image;
			_metaHeaders = ((metaHeaders == null) ? new List<JpegHeader>(0) : new List<JpegHeader>(metaHeaders));
			foreach (JpegHeader metaHeader in _metaHeaders)
			{
				if (metaHeader.IsJFIF)
				{
					HasJFIF = true;
					break;
				}
			}
			int componentCount = _image.ComponentCount;
			compWidth = new int[componentCount];
			compHeight = new int[componentCount];
			BlockWidth = new int[componentCount];
			BlockHeight = new int[componentCount];
			Initialize();
		}

		public DecodedJpeg(Image image)
			: this(image, null)
		{
			_metaHeaders = new List<JpegHeader>();
			string s = "Jpeg Codec | fluxcapacity.net ";
			_metaHeaders.Add(new JpegHeader
			{
				Marker = 254,
				Data = Encoding.UTF8.GetBytes(s)
			});
		}

		private void Initialize()
		{
			int width = _image.Width;
			int height = _image.Height;
			MaxHsampFactor = 1;
			MaxVsampFactor = 1;
			for (int i = 0; i < _image.ComponentCount; i++)
			{
				MaxHsampFactor = Math.Max(MaxHsampFactor, HsampFactor[i]);
				MaxVsampFactor = Math.Max(MaxVsampFactor, VsampFactor[i]);
			}
			for (int i = 0; i < _image.ComponentCount; i++)
			{
				compWidth[i] = ((width % 8 != 0) ? ((int)Math.Ceiling((double)width / 8.0) * 8) : width) / MaxHsampFactor * HsampFactor[i];
				if (compWidth[i] != width / MaxHsampFactor * HsampFactor[i])
				{
					lastColumnIsDummy[i] = true;
				}
				BlockWidth[i] = (int)Math.Ceiling((double)compWidth[i] / 8.0);
				compHeight[i] = ((height % 8 != 0) ? ((int)Math.Ceiling((double)height / 8.0) * 8) : height) / MaxVsampFactor * VsampFactor[i];
				if (compHeight[i] != height / MaxVsampFactor * VsampFactor[i])
				{
					lastRowIsDummy[i] = true;
				}
				BlockHeight[i] = (int)Math.Ceiling((double)compHeight[i] / 8.0);
			}
		}




	}
}
