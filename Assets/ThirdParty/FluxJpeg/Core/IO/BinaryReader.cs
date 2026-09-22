using System;
using System.IO;
namespace FluxJpeg.Core.IO
{
	internal class BinaryReader
	{
		private Stream _stream;

		private byte[] _buffer;

		public Stream BaseStream
		{
			get
			{
				return _stream;
			}
		}

		public BinaryReader(byte[] data)
			: this(new MemoryStream(data))
		{
		}

		public BinaryReader(Stream stream)
		{
			_stream = stream;
			_buffer = new byte[2];
		}

		public byte ReadByte()
		{
			int num = _stream.ReadByte();
			if (num == -1)
			{
				throw new EndOfStreamException();
			}
			return (byte)num;
		}

		public ushort ReadShort()
		{
			_stream.Read(_buffer, 0, 2);
			return (ushort)((_buffer[0] << 8) | (_buffer[1] & 0xFF));
		}

		public int Read(byte[] buffer, int offset, int count)
		{
			return _stream.Read(buffer, offset, count);
		}




	}
}
