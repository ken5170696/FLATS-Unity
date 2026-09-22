namespace Reign.Plugin
{
	public interface IAdPlugin
	{
		bool Visible { get; set; }

		void Dispose();

		void SetGravity(AdGravity gravity);

		void Refresh();

		void Update();

		void OnGUI();

		void OverrideOnGUI();
	}
}
