using System;
using BitMiracle.LibJpeg.Classic;
namespace BitMiracle.LibJpeg
{
	internal class DecompressionParameters
	{
		private Colorspace m_outColorspace;

		private int m_scaleNumerator;

		private int m_scaleDenominator;

		private bool m_bufferedImage;

		private bool m_rawDataOut;

		private DCTMethod m_dctMethod;

		private DitherMode m_ditherMode;

		private bool m_doFancyUpsampling;

		private bool m_doBlockSmoothing;

		private bool m_quantizeColors;

		private bool m_twoPassQuantize;

		private int m_desiredNumberOfColors;

		private bool m_enableOnePassQuantizer;

		private bool m_enableExternalQuant;

		private bool m_enableTwoPassQuantizer;

		private int m_traceLevel;

		public int TraceLevel
		{
			get
			{
				return m_traceLevel;
			}
			set
			{
				m_traceLevel = value;
			}
		}

		public Colorspace OutColorspace
		{
			get
			{
				return m_outColorspace;
			}
			set
			{
				m_outColorspace = value;
			}
		}

		public int ScaleNumerator
		{
			get
			{
				return m_scaleNumerator;
			}
			set
			{
				m_scaleNumerator = value;
			}
		}

		public int ScaleDenominator
		{
			get
			{
				return m_scaleDenominator;
			}
			set
			{
				m_scaleDenominator = value;
			}
		}

		public bool BufferedImage
		{
			get
			{
				return m_bufferedImage;
			}
			set
			{
				m_bufferedImage = value;
			}
		}

		public bool RawDataOut
		{
			get
			{
				return m_rawDataOut;
			}
			set
			{
				m_rawDataOut = value;
			}
		}

		public DCTMethod DCTMethod
		{
			get
			{
				return m_dctMethod;
			}
			set
			{
				m_dctMethod = value;
			}
		}

		public bool DoFancyUpsampling
		{
			get
			{
				return m_doFancyUpsampling;
			}
			set
			{
				m_doFancyUpsampling = value;
			}
		}

		public bool DoBlockSmoothing
		{
			get
			{
				return m_doBlockSmoothing;
			}
			set
			{
				m_doBlockSmoothing = value;
			}
		}

		public bool QuantizeColors
		{
			get
			{
				return m_quantizeColors;
			}
			set
			{
				m_quantizeColors = value;
			}
		}

		public DitherMode DitherMode
		{
			get
			{
				return m_ditherMode;
			}
			set
			{
				m_ditherMode = value;
			}
		}

		public bool TwoPassQuantize
		{
			get
			{
				return m_twoPassQuantize;
			}
			set
			{
				m_twoPassQuantize = value;
			}
		}

		public int DesiredNumberOfColors
		{
			get
			{
				return m_desiredNumberOfColors;
			}
			set
			{
				m_desiredNumberOfColors = value;
			}
		}

		public bool EnableOnePassQuantizer
		{
			get
			{
				return m_enableOnePassQuantizer;
			}
			set
			{
				m_enableOnePassQuantizer = value;
			}
		}

		public bool EnableExternalQuant
		{
			get
			{
				return m_enableExternalQuant;
			}
			set
			{
				m_enableExternalQuant = value;
			}
		}

		public bool EnableTwoPassQuantizer
		{
			get
			{
				return m_enableTwoPassQuantizer;
			}
			set
			{
				m_enableTwoPassQuantizer = value;
			}
		}

		public DecompressionParameters()
		{
			m_scaleNumerator = 1;
			m_scaleDenominator = 1;
			m_dctMethod = (DCTMethod)JpegConstants.JDCT_DEFAULT;
			m_ditherMode = DitherMode.FloydSteinberg;
			m_doFancyUpsampling = true;
			m_doBlockSmoothing = true;
			m_twoPassQuantize = true;
			m_desiredNumberOfColors = 256;

		}




	}
}
