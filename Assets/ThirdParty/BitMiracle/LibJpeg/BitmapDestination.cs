using System;
using System.IO;
using BitMiracle.LibJpeg.Classic;
namespace BitMiracle.LibJpeg
{
	internal class BitmapDestination : IDecompressDestination
	{
		private Stream m_output;

		private byte[][] m_pixels;

		private int m_rowWidth;

		private int m_currentRow;

		private LoadedImageAttributes m_parameters;

		public Stream Output
		{
			get
			{
				return m_output;
			}
		}

		public BitmapDestination(Stream output)
		{
			m_output = output;
		}

		public void SetImageAttributes(LoadedImageAttributes parameters)
		{
			if (parameters == null)
			{
				throw new ArgumentNullException("parameters");
			}
			m_parameters = parameters;
		}

		public void BeginWrite()
		{
			m_rowWidth = m_parameters.Width * m_parameters.Components;
			while (m_rowWidth % 4 != 0)
			{
				m_rowWidth++;
			}
			m_pixels = new byte[m_rowWidth][];
			for (int i = 0; i < m_rowWidth; i++)
			{
				m_pixels[i] = new byte[m_parameters.Height];
			}
			m_currentRow = 0;
		}

		public void ProcessPixelsRow(byte[] row)
		{
			if (m_parameters.Colorspace == Colorspace.Grayscale || m_parameters.QuantizeColors)
			{
				putGrayRow(row);
			}
			else if (m_parameters.Colorspace == Colorspace.CMYK)
			{
				putCmykRow(row);
			}
			else
			{
				putRgbRow(row);
			}
			m_currentRow++;
		}

		public void EndWrite()
		{
			writeHeader();
			writePixels();
			m_output.Flush();
		}

		private void putGrayRow(byte[] row)
		{
			for (int i = 0; i < m_parameters.Width; i++)
			{
				m_pixels[i][m_currentRow] = row[i];
			}
		}

		private void putRgbRow(byte[] row)
		{
			for (int i = 0; i < m_parameters.Width; i++)
			{
				int num = i * 3;
				byte b = row[num];
				byte b2 = row[num + 1];
				byte b3 = row[num + 2];
				m_pixels[num][m_currentRow] = b3;
				m_pixels[num + 1][m_currentRow] = b2;
				m_pixels[num + 2][m_currentRow] = b;
			}
		}

		private void putCmykRow(byte[] row)
		{
			for (int i = 0; i < m_parameters.Width; i++)
			{
				int num = i * 4;
				m_pixels[num][m_currentRow] = row[num + 2];
				m_pixels[num + 1][m_currentRow] = row[num + 1];
				m_pixels[num + 2][m_currentRow] = row[num];
				m_pixels[num + 3][m_currentRow] = row[num + 3];
			}
		}

		private void writeHeader()
		{
			int num;
			int num2;
			if (m_parameters.Colorspace == Colorspace.Grayscale || m_parameters.QuantizeColors)
			{
				num = 8;
				num2 = 256;
			}
			else
			{
				num2 = 0;
				if (m_parameters.Colorspace == Colorspace.RGB)
				{
					num = 24;
				}
				else
				{
					if (m_parameters.Colorspace != Colorspace.CMYK)
					{
						throw new InvalidOperationException();
					}
					num = 32;
				}
			}
			byte[] array = null;
			array = ((m_parameters.Colorspace != Colorspace.RGB) ? createBitmapV4InfoHeader(num) : createBitmapInfoHeader(num, num2));
			int num3 = array.Length;
			int num4 = num2 * 4;
			int num5 = 14 + num3 + num4;
			int fileSize = num5 + m_rowWidth * m_parameters.Height;
			byte[] array2 = createBitmapFileHeader(num5, fileSize);
			m_output.Write(array2, 0, array2.Length);
			m_output.Write(array, 0, array.Length);
			if (num2 > 0)
			{
				writeColormap(num2, 4);
			}
		}

		private static byte[] createBitmapFileHeader(int offsetToPixels, int fileSize)
		{
			byte[] array = new byte[14]
			{
				66, 77, 0, 0, 0, 0, 0, 0, 0, 0,
				0, 0, 0, 0
			};
			PUT_4B(array, 2, fileSize);
			PUT_4B(array, 10, offsetToPixels);
			return array;
		}

		private byte[] createBitmapInfoHeader(int bits_per_pixel, int cmap_entries)
		{
			byte[] array = new byte[40];
			fillBitmapInfoHeader(bits_per_pixel, cmap_entries, array);
			return array;
		}

		private void fillBitmapInfoHeader(int bitsPerPixel, int cmap_entries, byte[] infoHeader)
		{
			PUT_2B(infoHeader, 0, infoHeader.Length);
			PUT_4B(infoHeader, 4, m_parameters.Width);
			PUT_4B(infoHeader, 8, m_parameters.Height);
			PUT_2B(infoHeader, 12, 1);
			PUT_2B(infoHeader, 14, bitsPerPixel);
			if (m_parameters.DensityUnit == DensityUnit.DotsCm)
			{
				PUT_4B(infoHeader, 24, m_parameters.DensityX * 100);
				PUT_4B(infoHeader, 28, m_parameters.DensityY * 100);
			}
			PUT_2B(infoHeader, 32, cmap_entries);
		}

		private byte[] createBitmapV4InfoHeader(int bitsPerPixel)
		{
			byte[] array = new byte[108];
			fillBitmapInfoHeader(bitsPerPixel, 0, array);
			PUT_4B(array, 56, 2);
			return array;
		}

		private void writeColormap(int map_colors, int map_entry_size)
		{
			byte[][] colormap = m_parameters.Colormap;
			int actualNumberOfColors = m_parameters.ActualNumberOfColors;
			int num = 0;
			if (colormap != null)
			{
				if (m_parameters.ComponentsPerSample == 3)
				{
					for (num = 0; num < actualNumberOfColors; num++)
					{
						m_output.WriteByte(colormap[2][num]);
						m_output.WriteByte(colormap[1][num]);
						m_output.WriteByte(colormap[0][num]);
						if (map_entry_size == 4)
						{
							m_output.WriteByte(0);
						}
					}
				}
				else
				{
					for (num = 0; num < actualNumberOfColors; num++)
					{
						m_output.WriteByte(colormap[0][num]);
						m_output.WriteByte(colormap[0][num]);
						m_output.WriteByte(colormap[0][num]);
						if (map_entry_size == 4)
						{
							m_output.WriteByte(0);
						}
					}
				}
			}
			else
			{
				for (num = 0; num < 256; num++)
				{
					m_output.WriteByte((byte)num);
					m_output.WriteByte((byte)num);
					m_output.WriteByte((byte)num);
					if (map_entry_size == 4)
					{
						m_output.WriteByte(0);
					}
				}
			}
			if (num > map_colors)
			{
				throw new InvalidOperationException("Too many colors");
			}
			for (; num < map_colors; num++)
			{
				m_output.WriteByte(0);
				m_output.WriteByte(0);
				m_output.WriteByte(0);
				if (map_entry_size == 4)
				{
					m_output.WriteByte(0);
				}
			}
		}

		private void writePixels()
		{
			for (int num = m_parameters.Height - 1; num >= 0; num--)
			{
				for (int i = 0; i < m_rowWidth; i++)
				{
					m_output.WriteByte(m_pixels[i][num]);
				}
			}
		}

		private static void PUT_2B(byte[] array, int offset, int value)
		{
			array[offset] = (byte)(value & 0xFF);
			array[offset + 1] = (byte)((value >> 8) & 0xFF);
		}

		private static void PUT_4B(byte[] array, int offset, int value)
		{
			array[offset] = (byte)(value & 0xFF);
			array[offset + 1] = (byte)((value >> 8) & 0xFF);
			array[offset + 2] = (byte)((value >> 16) & 0xFF);
			array[offset + 3] = (byte)((value >> 24) & 0xFF);
		}




	}
}
