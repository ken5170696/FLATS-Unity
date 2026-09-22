namespace Reign.Plugin
{
	public interface IMessageBoxPlugin
	{
		void Show(string title, string message, MessageBoxTypes type, MessageBoxOptions options, MessageBoxCallback callback);

		void Update();
	}
}
