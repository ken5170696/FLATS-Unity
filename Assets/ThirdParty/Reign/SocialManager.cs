using Reign.Plugin;
namespace Reign
{
	public static class SocialManager
	{
		private static ISocialPlugin plugin;

		static SocialManager()
		{
			// Store sharing is unavailable in the offline desktop build.
		}

		public static void Init(SocialDesc desc)
		{
			
		}

		public static void Share(byte[] data, string dataFilename, string text, string title, string desc, SocialShareDataTypes type)
		{
			
		}

		public static void Share(byte[] data, string dataFilename, string text, string title, string desc, int x, int y, int width, int height, SocialShareDataTypes type)
		{
			
		}


	}
}
