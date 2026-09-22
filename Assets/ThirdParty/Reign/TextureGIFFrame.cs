using System;
using UnityEngine;
namespace Reign
{
	public class TextureGIFFrame
	{
		private static float time;

		internal TextureGIFFrame nextFrame;

		public Texture2D Texture { get; private set; }

		public Sprite Sprite { get; private set; }

		public TextureGIFFrame NextFrame
		{
			get
			{
				return nextFrame;
			}
		}

		public TimeSpan FrameTime { get; private set; }

		public TextureGIFFrame(Texture2D texture, TimeSpan timeSpan)
		{
			Texture = texture;
			Sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), Vector2.zero);
			FrameTime = timeSpan;
		}

		public TextureGIFFrame Update()
		{
			if (FrameTime.TotalSeconds == 0.0)
			{
				return this;
			}
			TextureGIFFrame result = this;
			time += Time.deltaTime;
			if ((double)time >= FrameTime.TotalSeconds)
			{
				time = 0f;
				result = NextFrame;
			}
			return result;
		}




	}
}
