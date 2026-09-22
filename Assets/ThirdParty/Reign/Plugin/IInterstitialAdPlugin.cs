namespace Reign.Plugin
{
	public interface IInterstitialAdPlugin
	{
		void Cache();

		void Show();

		void Dispose();

		void Update();

		void OnGUI();

		void OverrideOnGUI();
	}
}
