using System;
using System.IO;
using System.Runtime.InteropServices;
using ImageTools.Helpers;
namespace ImageTools.IO.Gif
{
	public class GifDecoder : IImageDecoder
	{
		private const byte ExtensionIntroducer = 33;

		private const byte Terminator = 0;

		private const byte ImageLabel = 44;

		private const byte EndIntroducer = 59;

		private const byte ApplicationExtensionLabel = byte.MaxValue;

		private const byte CommentLabel = 254;

		private const byte ImageDescriptorLabel = 44;

		private const byte PlainTextLabel = 1;

		private const byte GraphicControlLabel = 249;

		private ExtendedImage _image;

		private Stream _stream;

		private GifLogicalScreenDescriptor _logicalScreenDescriptor;

		private byte[] _globalColorTable;

		private byte[] _currentFrame;

		private GifGraphicsControlExtension _graphicsControl;

		public int HeaderSize
		{
			get
			{
				return 6;
			}
		}

		public bool IsSupportedFileExtension(string extension)
		{
			Guard.NotNullOrEmpty(extension, "extension");
			string text = extension.ToUpper();
			return text == "GIF";
		}

		public bool IsSupportedFileFormat(byte[] header)
		{
			bool result = false;
			if (header.Length >= 6)
			{
				result = header[0] == 71 && header[1] == 73 && header[2] == 70 && header[3] == 56 && (header[4] == 57 || header[4] == 55) && header[5] == 97;
			}
			return result;
		}

		public void Decode(ExtendedImage image, Stream stream)
		{
			_image = image;
			_stream = stream;
			_stream.Seek(6L, SeekOrigin.Current);
			ReadLogicalScreenDescriptor();
			if (_logicalScreenDescriptor.GlobalColorTableFlag)
			{
				_globalColorTable = new byte[_logicalScreenDescriptor.GlobalColorTableSize * 3];
				stream.Read(_globalColorTable, 0, _globalColorTable.Length);
			}
			int num = stream.ReadByte();
			while (true)
			{
				switch (num)
				{
				case 44:
					ReadFrame();
					break;
				case 33:
					switch (stream.ReadByte())
					{
					case 249:
						ReadGraphicalControlExtension();
						break;
					case 254:
						ReadComments();
						break;
					case 255:
						Skip(12);
						break;
					case 1:
						Skip(13);
						break;
					}
					break;
				case 0:
				case 59:
					return;
				}
				num = stream.ReadByte();
			}
		}

		private void ReadGraphicalControlExtension()
		{
			byte[] array = new byte[6];
			_stream.Read(array, 0, array.Length);
			byte b = array[1];
			_graphicsControl = new GifGraphicsControlExtension();
			_graphicsControl.DelayTime = BitConverter.ToInt16(array, 2);
			_graphicsControl.TransparencyIndex = array[4];
			_graphicsControl.TransparencyFlag = (b & 1) == 1;
			_graphicsControl.DisposalMethod = (DisposalMethod)((b & 0x1C) >> 2);
		}

		private GifImageDescriptor ReadImageDescriptor()
		{
			byte[] array = new byte[9];
			_stream.Read(array, 0, array.Length);
			byte b = array[8];
			GifImageDescriptor gifImageDescriptor = new GifImageDescriptor();
			gifImageDescriptor.Left = BitConverter.ToInt16(array, 0);
			gifImageDescriptor.Top = BitConverter.ToInt16(array, 2);
			gifImageDescriptor.Width = BitConverter.ToInt16(array, 4);
			gifImageDescriptor.Height = BitConverter.ToInt16(array, 6);
			gifImageDescriptor.LocalColorTableFlag = (b & 0x80) >> 7 == 1;
			gifImageDescriptor.LocalColorTableSize = 2 << (b & 7);
			gifImageDescriptor.InterlaceFlag = (b & 0x40) >> 6 == 1;
			return gifImageDescriptor;
		}

		private void ReadLogicalScreenDescriptor()
		{
			byte[] array = new byte[7];
			_stream.Read(array, 0, array.Length);
			byte b = array[4];
			_logicalScreenDescriptor = new GifLogicalScreenDescriptor();
			_logicalScreenDescriptor.Width = BitConverter.ToInt16(array, 0);
			_logicalScreenDescriptor.Height = BitConverter.ToInt16(array, 2);
			_logicalScreenDescriptor.Background = array[5];
			_logicalScreenDescriptor.GlobalColorTableFlag = (b & 0x80) >> 7 == 1;
			_logicalScreenDescriptor.GlobalColorTableSize = 2 << (b & 7);
		}

		private void Skip(int length)
		{
			_stream.Seek(length, SeekOrigin.Current);
			int num = 0;
			while ((num = _stream.ReadByte()) != 0)
			{
				_stream.Seek(num, SeekOrigin.Current);
			}
		}

		private void ReadComments()
		{
			int num = 0;
			while ((num = _stream.ReadByte()) != 0)
			{
				byte[] array = new byte[num];
				_stream.Read(array, 0, num);
				_image.Properties.Add(new ImageProperty("Comments", BitConverter.ToString(array)));
			}
		}

		private void ReadFrame()
		{
			GifImageDescriptor gifImageDescriptor = ReadImageDescriptor();
			byte[] array = ReadFrameLocalColorTable(gifImageDescriptor);
			byte[] indices = ReadFrameIndices(gifImageDescriptor);
			byte[] colorTable = ((array != null) ? array : _globalColorTable);
			ReadFrameColors(indices, colorTable, gifImageDescriptor);
			Skip(0);
		}

		private byte[] ReadFrameIndices(GifImageDescriptor imageDescriptor)
		{
			int dataSize = _stream.ReadByte();
			LZWDecoder lZWDecoder = new LZWDecoder(_stream);
			return lZWDecoder.DecodePixels(imageDescriptor.Width, imageDescriptor.Height, dataSize);
		}

		private byte[] ReadFrameLocalColorTable(GifImageDescriptor imageDescriptor)
		{
			byte[] array = null;
			if (imageDescriptor.LocalColorTableFlag)
			{
				array = new byte[imageDescriptor.LocalColorTableSize * 3];
				_stream.Read(array, 0, array.Length);
			}
			return array;
		}

		private void ReadFrameColors(byte[] indices, byte[] colorTable, GifImageDescriptor descriptor)
		{
			int width = _logicalScreenDescriptor.Width;
			int height = _logicalScreenDescriptor.Height;
			if (_currentFrame == null)
			{
				_currentFrame = new byte[width * height * 4];
			}
			byte[] array = null;
			if (_graphicsControl != null && _graphicsControl.DisposalMethod == DisposalMethod.RestoreToPrevious)
			{
				array = new byte[width * height * 4];
				Array.Copy(_currentFrame, array, array.Length);
			}
			int num = 0;
			int num2 = 0;
			int num3 = -1;
			int num4 = 0;
			int num5 = 8;
			int num6 = 0;
			int num7 = 0;
			for (int i = descriptor.Top; i < descriptor.Top + descriptor.Height; i++)
			{
				if (descriptor.InterlaceFlag)
				{
					if (num6 >= descriptor.Height)
					{
						num4++;
						switch (num4)
						{
						case 1:
							num6 = 4;
							break;
						case 2:
							num6 = 2;
							num5 = 4;
							break;
						case 3:
							num6 = 1;
							num5 = 2;
							break;
						}
					}
					num7 = num6 + descriptor.Top;
					num6 += num5;
				}
				else
				{
					num7 = i;
				}
				for (int j = descriptor.Left; j < descriptor.Left + descriptor.Width; j++)
				{
					num = num7 * width + j;
					num3 = indices[num2];
					if (_graphicsControl == null || !_graphicsControl.TransparencyFlag || _graphicsControl.TransparencyIndex != num3)
					{
						_currentFrame[num * 4] = colorTable[num3 * 3];
						_currentFrame[num * 4 + 1] = colorTable[num3 * 3 + 1];
						_currentFrame[num * 4 + 2] = colorTable[num3 * 3 + 2];
						_currentFrame[num * 4 + 3] = byte.MaxValue;
					}
					num2++;
				}
			}
			byte[] array2 = new byte[width * height * 4];
			Array.Copy(_currentFrame, array2, array2.Length);
			ImageBase imageBase = null;
			if (_image.Pixels == null)
			{
				imageBase = _image;
				imageBase.SetPixels(width, height, array2);
			}
			else
			{
				ImageFrame imageFrame = new ImageFrame();
				imageBase = imageFrame;
				imageBase.SetPixels(width, height, array2);
				_image.Frames.Add(imageFrame);
			}
			if (_graphicsControl == null)
			{
				return;
			}
			if (_graphicsControl.DelayTime > 0)
			{
				imageBase.DelayTime = _graphicsControl.DelayTime;
			}
			if (_graphicsControl.DisposalMethod == DisposalMethod.RestoreToBackground)
			{
				for (int k = descriptor.Top; k < descriptor.Top + descriptor.Height; k++)
				{
					for (int l = descriptor.Left; l < descriptor.Left + descriptor.Width; l++)
					{
						num = k * width + l;
						_currentFrame[num * 4] = 0;
						_currentFrame[num * 4 + 1] = 0;
						_currentFrame[num * 4 + 2] = 0;
						_currentFrame[num * 4 + 3] = 0;
					}
				}
			}
			else if (_graphicsControl.DisposalMethod == DisposalMethod.RestoreToPrevious)
			{
				_currentFrame = array;
			}
		}

		public GifDecoder()
		{
		}




	}
}
