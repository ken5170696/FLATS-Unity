using System;
namespace BitMiracle.LibJpeg
{
	internal class SampleRow
	{
		private Sample[] m_samples;

		public int Length
		{
			get
			{
				return m_samples.Length;
			}
		}

		public Sample this[int sampleNumber]
		{
			get
			{
				return GetAt(sampleNumber);
			}
		}

		public SampleRow(byte[] row, int sampleCount, byte bitsPerComponent, byte componentsPerSample)
		{
			if (row == null)
			{
				throw new ArgumentNullException("row");
			}
			if (row.Length == 0)
			{
				throw new ArgumentException("row is empty");
			}
			if (sampleCount <= 0)
			{
				throw new ArgumentOutOfRangeException("sampleCount");
			}
			if (bitsPerComponent <= 0 || bitsPerComponent > 16)
			{
				throw new ArgumentOutOfRangeException("bitsPerComponent");
			}
			if (componentsPerSample <= 0 || componentsPerSample > 5)
			{
				throw new ArgumentOutOfRangeException("componentsPerSample");
			}
			using (BitStream bitStream = new BitStream(row))
			{
				m_samples = new Sample[sampleCount];
				for (int i = 0; i != sampleCount; i++)
				{
					m_samples[i] = new Sample(bitStream, bitsPerComponent, componentsPerSample);
				}
			}
		}

		public Sample GetAt(int sampleNumber)
		{
			return m_samples[sampleNumber];
		}




	}
}
