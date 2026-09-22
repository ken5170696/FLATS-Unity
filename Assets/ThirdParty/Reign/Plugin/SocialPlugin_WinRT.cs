using System;
namespace Reign.Plugin
{
	public class SocialPlugin_WinRT : ISocialPlugin
	{
		public delegate void InitNativeMethod(SocialPlugin_WinRT plugin);

		public ISocialPlugin Native;

		public static InitNativeMethod InitNative;

		public SocialPlugin_WinRT()
		{
			InitNative(this);
		}

		public void Init(SocialDesc desc)
		{
			Native.Init(desc);
		}

		public void Share(byte[] data, string dataFilename, string text, string title, string desc, SocialShareDataTypes type)
		{
			Native.Share(data, dataFilename, text, title, desc, type);
		}

		public void Share(byte[] data, string dataFilename, string text, string title, string desc, int x, int y, int width, int height, SocialShareDataTypes type)
		{
			Native.Share(data, dataFilename, text, title, desc, x, y, width, height, type);
		}




	}
}
