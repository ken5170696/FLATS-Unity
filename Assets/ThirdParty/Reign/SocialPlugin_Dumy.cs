using System;
using Reign.Plugin;
using UnityEngine;
namespace Reign
{
	public class SocialPlugin_Dumy : ISocialPlugin
	{
		public void Init(SocialDesc desc)
		{
			Debug.Log("Share not supported in this environment!");
		}

		public void Share(byte[] data, string dataFilename, string text, string title, string desc, SocialShareDataTypes type)
		{
			Debug.Log("Share not supported in this environment!");
		}

		public void Share(byte[] data, string dataFilename, string text, string title, string desc, int x, int y, int width, int height, SocialShareDataTypes type)
		{
			Debug.Log("Share not supported in this environment!");
		}

		public SocialPlugin_Dumy()
		{
		}




	}
}
