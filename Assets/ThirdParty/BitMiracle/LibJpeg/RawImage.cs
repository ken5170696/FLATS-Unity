using System;
using System.Collections.Generic;
namespace BitMiracle.LibJpeg
{
	internal class RawImage : IRawImage
	{
		private List<SampleRow> m_samples;

		private Colorspace m_colorspace;

		private int m_currentRow;

		public int Width
		{
			get
			{
				return m_samples[0].Length;
			}
		}

		public int Height
		{
			get
			{
				return m_samples.Count;
			}
		}

		public Colorspace Colorspace
		{
			get
			{
				return m_colorspace;
			}
		}

		public int ComponentsPerPixel
		{
			get
			{
				return m_samples[0][0].ComponentCount;
			}
		}

		internal RawImage(List<SampleRow> samples, Colorspace colorspace)
		{
			m_currentRow = -1;

			m_samples = samples;
			m_colorspace = colorspace;
		}

		public void BeginRead()
		{
			m_currentRow = 0;
		}

		public byte[] GetPixelRow()
		{
			SampleRow sampleRow = m_samples[m_currentRow];
			List<byte> list = new List<byte>();
			for (int i = 0; i < sampleRow.Length; i++)
			{
				Sample sample = sampleRow[i];
				for (int j = 0; j < sample.ComponentCount; j++)
				{
					list.Add((byte)sample[j]);
				}
			}
			m_currentRow++;
			return list.ToArray();
		}

		public void EndRead()
		{
		}




	}
}
