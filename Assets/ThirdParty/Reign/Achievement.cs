using System;
using System.Runtime.InteropServices;
using UnityEngine;
namespace Reign
{
	public class Achievement
	{
		public bool IsAchieved { get; internal set; }

		public float PercentComplete { get; internal set; }

		public string ID { get; private set; }

		public string Name { get; private set; }

		public string Desc { get; private set; }

		public Texture2D AchievedImage { get; private set; }

		public Texture2D UnachievedImage { get; private set; }

		public Sprite AchievedSprite { get; private set; }

		public Sprite UnachievedSprite { get; private set; }

		public Achievement(bool isAchieved, float percentComplete, string id, string name, string desc, Texture2D achievedImage, Texture2D unachievedImage)
		{
			IsAchieved = isAchieved;
			PercentComplete = percentComplete;
			ID = id;
			Name = name;
			Desc = desc;
			AchievedImage = achievedImage;
			UnachievedImage = unachievedImage;
			if (achievedImage != null)
			{
				AchievedSprite = Sprite.Create(achievedImage, new Rect(0f, 0f, achievedImage.width, achievedImage.height), Vector2.zero);
			}
			if (unachievedImage != null)
			{
				UnachievedSprite = Sprite.Create(unachievedImage, new Rect(0f, 0f, unachievedImage.width, unachievedImage.height), Vector2.zero);
			}
		}




	}
}
