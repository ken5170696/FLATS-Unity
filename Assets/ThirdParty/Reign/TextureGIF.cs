using System;
using System.Collections.Generic;
using System.IO;
using ImageTools;
using ImageTools.IO.Gif;
using UnityEngine;
namespace Reign
{
	public class TextureGIF
	{
		private List<TextureGIFFrame> frames;

		public GIFFrameUpdatedCallbackMethod FrameUpdatedCallback;

		public TextureGIFFrame CurrentFrame { get; private set; }

		private Texture2D createTexture(int width, int height, byte[] pixels)
		{
			Texture2D texture2D = new Texture2D(width, height, TextureFormat.RGBA32, false);
			Color32[] array = new Color32[pixels.Length / 4];
			for (int i = 0; i != texture2D.height; i++)
			{
				for (int j = 0; j != texture2D.width; j++)
				{
					int num = j + i * texture2D.width;
					int num2 = (j + (texture2D.height - 1 - i) * texture2D.width) * 4;
					array[num] = new Color32(pixels[num2], pixels[num2 + 1], pixels[num2 + 2], pixels[num2 + 3]);
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply();
			return texture2D;
		}

		private void init(Stream stream, GIFFrameUpdatedCallbackMethod frameUpdatedCallback)
		{
			FrameUpdatedCallback = frameUpdatedCallback;
			GifDecoder gifDecoder = new GifDecoder();
			ExtendedImage extendedImage = new ExtendedImage();
			gifDecoder.Decode(extendedImage, stream);
			frames = new List<TextureGIFFrame>();
			TextureGIFFrame textureGIFFrame = new TextureGIFFrame(createTexture(extendedImage.PixelWidth, extendedImage.PixelHeight, extendedImage.Pixels), TimeSpan.FromSeconds((double)extendedImage.DelayTime / 100.0));
			frames.Add(textureGIFFrame);
			TextureGIFFrame textureGIFFrame2 = textureGIFFrame;
			foreach (ImageFrame frame in extendedImage.Frames)
			{
				TextureGIFFrame textureGIFFrame3 = new TextureGIFFrame(createTexture(frame.PixelWidth, frame.PixelHeight, frame.Pixels), TimeSpan.FromSeconds((double)frame.DelayTime / 100.0));
				frames.Add(textureGIFFrame3);
				if (textureGIFFrame2 != null)
				{
					textureGIFFrame2.nextFrame = textureGIFFrame3;
				}
				textureGIFFrame2 = textureGIFFrame3;
			}
			CurrentFrame = frames[0];
			textureGIFFrame2.nextFrame = textureGIFFrame;
		}

		public TextureGIF(Stream stream, GIFFrameUpdatedCallbackMethod frameUpdatedCallback)
		{
			init(stream, frameUpdatedCallback);
		}

		public TextureGIF(byte[] data, GIFFrameUpdatedCallbackMethod frameUpdatedCallback)
		{
			using (MemoryStream memoryStream = new MemoryStream(data))
			{
				memoryStream.Position = 0L;
				init(memoryStream, frameUpdatedCallback);
			}
		}

		public void Dispose()
		{
			if (frames == null)
			{
				return;
			}
			foreach (TextureGIFFrame frame in frames)
			{
				if (frame != null && frame.Texture != null)
				{
					UnityEngine.Object.DestroyImmediate(frame.Texture);
				}
			}
			frames = null;
		}

		public void Update()
		{
			TextureGIFFrame textureGIFFrame = CurrentFrame.Update();
			if (textureGIFFrame != CurrentFrame && FrameUpdatedCallback != null)
			{
				CurrentFrame = textureGIFFrame;
				FrameUpdatedCallback(textureGIFFrame);
			}
		}




	}
}
