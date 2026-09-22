using System;
using BitMiracle.LibJpeg.Classic;
namespace BitMiracle.LibJpeg
{
	internal class LoadedImageAttributes
	{
		private Colorspace m_colorspace;

		private bool m_quantizeColors;

		private int m_width;

		private int m_height;

		private int m_componentsPerSample;

		private int m_components;

		private int m_actualNumberOfColors;

		private byte[][] m_colormap;

		private DensityUnit m_densityUnit;

		private int m_densityX;

		private int m_densityY;

		public Colorspace Colorspace
		{
			get
			{
				return m_colorspace;
			}
			internal set
			{
				m_colorspace = value;
			}
		}

		public bool QuantizeColors
		{
			get
			{
				return m_quantizeColors;
			}
			internal set
			{
				m_quantizeColors = value;
			}
		}

		public int Width
		{
			get
			{
				return m_width;
			}
			internal set
			{
				m_width = value;
			}
		}

		public int Height
		{
			get
			{
				return m_height;
			}
			internal set
			{
				m_height = value;
			}
		}

		public int ComponentsPerSample
		{
			get
			{
				return m_componentsPerSample;
			}
			internal set
			{
				m_componentsPerSample = value;
			}
		}

		public int Components
		{
			get
			{
				return m_components;
			}
			internal set
			{
				m_components = value;
			}
		}

		public int ActualNumberOfColors
		{
			get
			{
				return m_actualNumberOfColors;
			}
			internal set
			{
				m_actualNumberOfColors = value;
			}
		}

		public byte[][] Colormap
		{
			get
			{
				return m_colormap;
			}
			internal set
			{
				m_colormap = value;
			}
		}

		public DensityUnit DensityUnit
		{
			get
			{
				return m_densityUnit;
			}
			internal set
			{
				m_densityUnit = value;
			}
		}

		public int DensityX
		{
			get
			{
				return m_densityX;
			}
			internal set
			{
				m_densityX = value;
			}
		}

		public int DensityY
		{
			get
			{
				return m_densityY;
			}
			internal set
			{
				m_densityY = value;
			}
		}

		public LoadedImageAttributes()
		{
		}




	}
}
