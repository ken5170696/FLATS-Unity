using System;
using System.IO;
namespace BitMiracle.LibJpeg
{
	internal class BitStream : IDisposable
	{
		private const int bitsInByte = 8;

		private bool m_alreadyDisposed;

		private Stream m_stream;

		private int m_positionInByte;

		private int m_size;

		public Stream UnderlyingStream
		{
			get
			{
				return m_stream;
			}
		}

		public BitStream()
		{
			m_stream = new MemoryStream();
		}

		public BitStream(byte[] buffer)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException("buffer");
			}
			m_stream = new MemoryStream(buffer);
			m_size = bitsAllocated();
		}

		public void Dispose()
		{
			Dispose(true);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!m_alreadyDisposed)
			{
				if (disposing && m_stream != null)
				{
					m_stream.Dispose();
				}
				m_stream = null;
				m_alreadyDisposed = true;
			}
		}

		public int Size()
		{
			return m_size;
		}

		public virtual int Read(int bitCount)
		{
			if (Tell() + bitCount > bitsAllocated())
			{
				throw new ArgumentOutOfRangeException("bitCount");
			}
			return read(bitCount);
		}

		public int Write(int bitStorage, int bitCount)
		{
			if (bitCount == 0)
			{
				return 0;
			}
			if (bitCount > 32)
			{
				throw new ArgumentOutOfRangeException("bitCount");
			}
			for (int i = 0; i < bitCount; i++)
			{
				byte bit = (byte)(bitStorage << 32 - (bitCount - i) >> 31);
				if (!writeBit(bit))
				{
					return i;
				}
			}
			return bitCount;
		}

		public void Seek(int pos, SeekOrigin mode)
		{
			switch (mode)
			{
			case SeekOrigin.Begin:
				seekSet(pos);
				break;
			case SeekOrigin.Current:
				seekCurrent(pos);
				break;
			case SeekOrigin.End:
				seekSet(Size() + pos);
				break;
			}
		}

		public int Tell()
		{
			return (int)m_stream.Position * 8 + m_positionInByte;
		}

		private int bitsAllocated()
		{
			return (int)m_stream.Length * 8;
		}

		private int read(int bitsCount)
		{
			switch (bitsCount)
			{
			default:
				throw new ArgumentOutOfRangeException("bitsCount");
			case 0:
				return 0;
			case 1:
			case 2:
			case 3:
			case 4:
			case 5:
			case 6:
			case 7:
			case 8:
			case 9:
			case 10:
			case 11:
			case 12:
			case 13:
			case 14:
			case 15:
			case 16:
			case 17:
			case 18:
			case 19:
			case 20:
			case 21:
			case 22:
			case 23:
			case 24:
			case 25:
			case 26:
			case 27:
			case 28:
			case 29:
			case 30:
			case 31:
			case 32:
			{
				int i = 0;
				int num = 0;
				byte[] array = new byte[1];
				for (; i == 0 || i - m_positionInByte < bitsCount; i += 8)
				{
					m_stream.Read(array, 0, 1);
					num <<= 8;
					num += array[0];
				}
				m_positionInByte = (m_positionInByte + bitsCount) % 8;
				if (m_positionInByte != 0)
				{
					num >>= 8 - m_positionInByte;
					m_stream.Seek(-1L, SeekOrigin.Current);
				}
				if (bitsCount < 32)
				{
					int num2 = (1 << bitsCount) - 1;
					num &= num2;
				}
				return num;
			}
			}
		}

		private bool writeBit(byte bit)
		{
			if (m_stream.Position == m_stream.Length)
			{
				byte[] buffer = new byte[1] { (byte)(bit << 7) };
				m_stream.Write(buffer, 0, 1);
				m_stream.Seek(-1L, SeekOrigin.Current);
			}
			else
			{
				byte[] array = new byte[1];
				byte[] array2 = array;
				m_stream.Read(array2, 0, 1);
				m_stream.Seek(-1L, SeekOrigin.Current);
				int num = (8 - m_positionInByte - 1) % 8;
				byte b = (byte)(bit << num);
				array2[0] |= b;
				m_stream.Write(array2, 0, 1);
				m_stream.Seek(-1L, SeekOrigin.Current);
			}
			Seek(1, SeekOrigin.Current);
			int num2 = Tell();
			if (num2 > m_size)
			{
				m_size = num2;
			}
			return true;
		}

		private void seekSet(int pos)
		{
			if (pos < 0)
			{
				throw new ArgumentOutOfRangeException("pos");
			}
			int num = pos / 8;
			m_stream.Seek(num, SeekOrigin.Begin);
			int positionInByte = pos - num * 8;
			m_positionInByte = positionInByte;
		}

		private void seekCurrent(int pos)
		{
			int num = Tell() + pos;
			if (num < 0 || num > bitsAllocated())
			{
				throw new ArgumentOutOfRangeException("pos");
			}
			seekSet(num);
		}




	}
}
