using System;
namespace Reign.Plugin
{
	public class Dumy_AdPlugin : IAdPlugin
	{
		public bool Visible { get; set; }

		public Dumy_AdPlugin(AdDesc desc, AdCreatedCallbackMethod createdCallback)
		{
			Visible = desc.Visible;
			if (createdCallback != null)
			{
				createdCallback(true);
			}
		}

		public void Dispose()
		{
		}

		public void SetGravity(AdGravity gravity)
		{
		}

		public void Refresh()
		{
		}

		public void Update()
		{
		}

		public void OnGUI()
		{
		}

		public void OverrideOnGUI()
		{
		}




	}
}
