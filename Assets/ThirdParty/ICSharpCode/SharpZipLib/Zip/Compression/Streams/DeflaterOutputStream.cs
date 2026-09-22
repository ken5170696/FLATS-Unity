using System;
using System.IO;
using System.Runtime.InteropServices;
using ICSharpCode.SharpZipLib.Checksums;
namespace ICSharpCode.SharpZipLib.Zip.Compression.Streams
{
	public class DeflaterOutputStream : Stream
	{
		private string password;

		private uint[] keys;

		private byte[] buffer_;

		protected Deflater deflater_;

		protected Stream baseOutputStream_;

		private bool isClosed_;

		private bool isStreamOwner_;

		public bool IsStreamOwner
		{
			get
			{
				return isStreamOwner_;
			}
			set
			{
				isStreamOwner_ = value;
			}
		}

		public bool CanPatchEntries
		{
			get
			{
				return baseOutputStream_.CanSeek;
			}
		}

		public string Password
		{
			get
			{
				return password;
			}
			set
			{
				if (value != null && value.Length == 0)
				{
					password = null;
				}
				else
				{
					password = value;
				}
			}
		}

		public override bool CanRead
		{
			get
			{
				return false;
			}
		}

		public override bool CanSeek
		{
			get
			{
				return false;
			}
		}

		public override bool CanWrite
		{
			get
			{
				return baseOutputStream_.CanWrite;
			}
		}

		public override long Length
		{
			get
			{
				return baseOutputStream_.Length;
			}
		}

		public override long Position
		{
			get
			{
				return baseOutputStream_.Position;
			}
			set
			{
				throw new NotSupportedException("Position property not supported");
			}
		}

		public DeflaterOutputStream(Stream baseOutputStream)
			: this(baseOutputStream, new Deflater(), 512)
		{
		}

		public DeflaterOutputStream(Stream baseOutputStream, Deflater deflater)
			: this(baseOutputStream, deflater, 512)
		{
		}

		public DeflaterOutputStream(Stream baseOutputStream, Deflater deflater, int bufferSize)
		{
			isStreamOwner_ = true;

			if (baseOutputStream == null)
			{
				throw new ArgumentNullException("baseOutputStream");
			}
			if (!baseOutputStream.CanWrite)
			{
				throw new ArgumentException("Must support writing", "baseOutputStream");
			}
			if (deflater == null)
			{
				throw new ArgumentNullException("deflater");
			}
			if (bufferSize < 512)
			{
				throw new ArgumentOutOfRangeException("bufferSize");
			}
			baseOutputStream_ = baseOutputStream;
			buffer_ = new byte[bufferSize];
			deflater_ = deflater;
		}

		public virtual void Finish()
		{
			deflater_.Finish();
			while (!deflater_.IsFinished)
			{
				int num = deflater_.Deflate(buffer_, 0, buffer_.Length);
				if (num <= 0)
				{
					break;
				}
				if (keys != null)
				{
					EncryptBlock(buffer_, 0, num);
				}
				baseOutputStream_.Write(buffer_, 0, num);
			}
			if (!deflater_.IsFinished)
			{
				throw new SharpZipBaseException("Can't deflate all input?");
			}
			baseOutputStream_.Flush();
			if (keys != null)
			{
				keys = null;
			}
		}

		protected void EncryptBlock(byte[] buffer, int offset, int length)
		{
			for (int i = offset; i < offset + length; i++)
			{
				byte ch = buffer[i];
				buffer[i] ^= EncryptByte();
				UpdateKeys(ch);
			}
		}

		protected void InitializePassword(string password)
		{
			keys = new uint[3] { 305419896u, 591751049u, 878082192u };
			byte[] array = ZipConstants.ConvertToArray(password);
			for (int i = 0; i < array.Length; i++)
			{
				UpdateKeys(array[i]);
			}
		}

		protected byte EncryptByte()
		{
			uint num = (keys[2] & 0xFFFF) | 2;
			return (byte)(num * (num ^ 1) >> 8);
		}

		protected void UpdateKeys(byte ch)
		{
			keys[0] = Crc32.ComputeCrc32(keys[0], ch);
			keys[1] = keys[1] + (byte)keys[0];
			keys[1] = keys[1] * 134775813 + 1;
			keys[2] = Crc32.ComputeCrc32(keys[2], (byte)(keys[1] >> 24));
		}

		protected void Deflate()
		{
			while (!deflater_.IsNeedingInput)
			{
				int num = deflater_.Deflate(buffer_, 0, buffer_.Length);
				if (num <= 0)
				{
					break;
				}
				if (keys != null)
				{
					EncryptBlock(buffer_, 0, num);
				}
				baseOutputStream_.Write(buffer_, 0, num);
			}
			if (!deflater_.IsNeedingInput)
			{
				throw new SharpZipBaseException("DeflaterOutputStream can't deflate all input?");
			}
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException("DeflaterOutputStream Seek not supported");
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException("DeflaterOutputStream SetLength not supported");
		}

		public override int ReadByte()
		{
			throw new NotSupportedException("DeflaterOutputStream ReadByte not supported");
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException("DeflaterOutputStream Read not supported");
		}

		public new IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
		{
			throw new NotSupportedException("DeflaterOutputStream BeginRead not currently supported");
		}

		public new IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
		{
			throw new NotSupportedException("BeginWrite is not supported");
		}

		public override void Flush()
		{
			deflater_.Flush();
			Deflate();
			baseOutputStream_.Flush();
		}

		public new void Close()
		{
			if (isClosed_)
			{
				return;
			}
			isClosed_ = true;
			try
			{
				Finish();
				keys = null;
			}
			finally
			{
				if (isStreamOwner_)
				{
					baseOutputStream_.Dispose();
				}
			}
		}

		private void GetAuthCodeIfAES()
		{
		}

		public override void WriteByte(byte value)
		{
			Write(new byte[1] { value }, 0, 1);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			deflater_.SetInput(buffer, offset, count);
			Deflate();
		}




	}
}
