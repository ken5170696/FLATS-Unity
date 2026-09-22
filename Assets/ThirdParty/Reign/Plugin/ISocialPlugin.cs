namespace Reign.Plugin
{
	public interface ISocialPlugin
	{
		void Init(SocialDesc desc);

		void Share(byte[] data, string dataFilename, string text, string title, string desc, SocialShareDataTypes type);

		void Share(byte[] data, string dataFilename, string text, string title, string desc, int x, int y, int width, int height, SocialShareDataTypes type);
	}
}
