using System;
namespace BitMiracle.LibJpeg
{
	internal class CompressionParameters
	{
		private int m_quality;

		private int m_smoothingFactor;

		private bool m_simpleProgressive;

		public int Quality
		{
			get
			{
				return m_quality;
			}
			set
			{
				m_quality = value;
			}
		}

		public int SmoothingFactor
		{
			get
			{
				return m_smoothingFactor;
			}
			set
			{
				m_smoothingFactor = value;
			}
		}

		public bool SimpleProgressive
		{
			get
			{
				return m_simpleProgressive;
			}
			set
			{
				m_simpleProgressive = value;
			}
		}

		public CompressionParameters()
		{
			m_quality = 75;

		}

		internal CompressionParameters(CompressionParameters parameters)
		{
			m_quality = 75;

			if (parameters == null)
			{
				throw new ArgumentNullException("parameters");
			}
			m_quality = parameters.m_quality;
			m_smoothingFactor = parameters.m_smoothingFactor;
			m_simpleProgressive = parameters.m_simpleProgressive;
		}

		public override bool Equals(object obj)
		{
			CompressionParameters compressionParameters = obj as CompressionParameters;
			if (compressionParameters == null)
			{
				return false;
			}
			if (m_quality == compressionParameters.m_quality && m_smoothingFactor == compressionParameters.m_smoothingFactor)
			{
				return m_simpleProgressive == compressionParameters.m_simpleProgressive;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}




	}
}
