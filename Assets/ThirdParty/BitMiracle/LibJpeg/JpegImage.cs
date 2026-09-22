using System;
using System.Collections.Generic;
using System.IO;
namespace BitMiracle.LibJpeg
{
	internal sealed class JpegImage : IDisposable
	{
		private bool m_alreadyDisposed;

		private List<SampleRow> m_rows;

		private int m_width;

		private int m_height;

		private byte m_bitsPerComponent;

		private byte m_componentsPerSample;

		private Colorspace m_colorspace;

		private MemoryStream m_compressedData;

		private CompressionParameters m_compressionParameters;

		private MemoryStream m_decompressedData;

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

		public byte ComponentsPerSample
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

		public byte BitsPerComponent
		{
			get
			{
				return m_bitsPerComponent;
			}
			internal set
			{
				m_bitsPerComponent = value;
			}
		}

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

		private MemoryStream compressedData
		{
			get
			{
				if (m_compressedData == null)
				{
					compress(new CompressionParameters());
				}
				return m_compressedData;
			}
		}

		private MemoryStream decompressedData
		{
			get
			{
				if (m_decompressedData == null)
				{
					fillDecompressedData();
				}
				return m_decompressedData;
			}
		}

		public JpegImage(Stream imageData)
		{
			m_rows = new List<SampleRow>();

			createFromStream(imageData);
		}

		public JpegImage(SampleRow[] sampleData, Colorspace colorspace)
		{
			m_rows = new List<SampleRow>();

			if (sampleData == null)
			{
				throw new ArgumentNullException("sampleData");
			}
			if (sampleData.Length == 0)
			{
				throw new ArgumentException("sampleData must be no empty");
			}
			if (colorspace == Colorspace.Unknown)
			{
				throw new ArgumentException("Unknown colorspace");
			}
			m_rows = new List<SampleRow>(sampleData);
			SampleRow sampleRow = m_rows[0];
			m_width = sampleRow.Length;
			m_height = m_rows.Count;
			Sample sample = sampleRow[0];
			m_bitsPerComponent = sample.BitsPerComponent;
			m_componentsPerSample = sample.ComponentCount;
			m_colorspace = colorspace;
		}

		public void Dispose()
		{
			Dispose(true);
		}

		private void Dispose(bool disposing)
		{
			if (m_alreadyDisposed)
			{
				return;
			}
			if (disposing)
			{
				if (m_compressedData != null)
				{
					m_compressedData.Dispose();
				}
				if (m_decompressedData != null)
				{
					m_decompressedData.Dispose();
				}
			}
			m_compressionParameters = null;
			m_compressedData = null;
			m_decompressedData = null;
			m_rows = null;
			m_alreadyDisposed = true;
		}

		public SampleRow GetRow(int rowNumber)
		{
			return m_rows[rowNumber];
		}

		public void WriteJpeg(Stream output)
		{
			WriteJpeg(output, new CompressionParameters());
		}

		public void WriteJpeg(Stream output, CompressionParameters parameters)
		{
			compress(parameters);
			compressedData.WriteTo(output);
		}

		public void WriteBitmap(Stream output)
		{
			decompressedData.WriteTo(output);
		}

		internal void addSampleRow(SampleRow row)
		{
			if (row == null)
			{
				throw new ArgumentNullException("row");
			}
			m_rows.Add(row);
		}

		private static bool isCompressed(Stream imageData)
		{
			if (imageData == null)
			{
				return false;
			}
			if (imageData.Length <= 2)
			{
				return false;
			}
			imageData.Seek(0L, SeekOrigin.Begin);
			int num = imageData.ReadByte();
			int num2 = imageData.ReadByte();
			if (num == 255)
			{
				return num2 == 216;
			}
			return false;
		}

		private void createFromStream(Stream imageData)
		{
			if (imageData == null)
			{
				throw new ArgumentNullException("imageData");
			}
			if (isCompressed(imageData))
			{
				m_compressedData = Utils.CopyStream(imageData);
				decompress();
				return;
			}
			throw new NotImplementedException("JpegImage.createFromStream(Stream)");
		}

		private void compress(CompressionParameters parameters)
		{
			RawImage source = new RawImage(m_rows, m_colorspace);
			compress(source, parameters);
		}

		private void compress(IRawImage source, CompressionParameters parameters)
		{
			if (needCompressWith(parameters))
			{
				m_compressedData = new MemoryStream();
				m_compressionParameters = new CompressionParameters(parameters);
				Jpeg jpeg = new Jpeg();
				jpeg.CompressionParameters = m_compressionParameters;
				jpeg.Compress(source, m_compressedData);
			}
		}

		private bool needCompressWith(CompressionParameters parameters)
		{
			if (m_compressedData != null && m_compressionParameters != null)
			{
				return !m_compressionParameters.Equals(parameters);
			}
			return true;
		}

		private void decompress()
		{
			Jpeg jpeg = new Jpeg();
			jpeg.Decompress(compressedData, new DecompressorToJpegImage(this));
		}

		private void fillDecompressedData()
		{
			m_decompressedData = new MemoryStream();
			BitmapDestination destination = new BitmapDestination(m_decompressedData);
			Jpeg jpeg = new Jpeg();
			jpeg.Decompress(compressedData, destination);
		}




	}
}
