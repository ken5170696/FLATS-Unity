using System;
using System.IO;
namespace FluxJpeg.Core.IO
{
	internal class JPEGBinaryReader : BinaryReader
	{
		public int eob_run;

		private byte marker;

		private byte _bitBuffer;

		protected int _bitsLeft;

		public int BitOffset
		{
			get
			{
				return (8 - _bitsLeft) % 8;
			}
			set
			{
				if (_bitsLeft != 0)
				{
					base.BaseStream.Seek(-1L, SeekOrigin.Current);
				}
				_bitsLeft = (8 - value) % 8;
			}
		}

		public JPEGBinaryReader(Stream input)
			: base(input)
		{
		}

		public byte GetNextMarker()
		{
			try
			{
				while (true)
				{
					ReadJpegByte();
				}
			}
			catch (JPEGMarkerFoundException ex)
			{
				return ex.Marker;
			}
		}

		public int ReadBits(int n)
		{
			int num = 0;
			if (_bitsLeft >= n)
			{
				_bitsLeft -= n;
				num = _bitBuffer >> 8 - n;
				_bitBuffer = (byte)(_bitBuffer << n);
				return num;
			}
			while (n > 0)
			{
				if (_bitsLeft == 0)
				{
					_bitBuffer = ReadJpegByte();
					_bitsLeft = 8;
				}
				int num2 = ((n <= _bitsLeft) ? n : _bitsLeft);
				num |= _bitBuffer >> 8 - num2 << n - num2;
				_bitBuffer = (byte)(_bitBuffer << num2);
				_bitsLeft -= num2;
				n -= num2;
			}
			return num;
		}

		protected byte ReadJpegByte()
		{
			byte b = ReadByte();
			if (b == byte.MaxValue)
			{
				while ((b = ReadByte()) == byte.MaxValue)
				{
				}
				if (b != 0)
				{
					marker = b;
					throw new JPEGMarkerFoundException(marker);
				}
				b = byte.MaxValue;
			}
			return b;
		}




	}
}
