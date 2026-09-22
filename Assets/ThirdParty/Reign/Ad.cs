using System;
using Reign.Plugin;
namespace Reign
{
	public class Ad
	{
		internal IAdPlugin plugin;

		public bool Visible
		{
			get
			{
				return plugin.Visible;
			}
			set
			{
				plugin.Visible = value;
			}
		}

		public Ad(IAdPlugin plugin)
		{
			this.plugin = plugin;
		}

		public void SetGravity(AdGravity gravity)
		{
			plugin.SetGravity(gravity);
		}

		public void Refresh()
		{
			plugin.Refresh();
		}

		public void Draw()
		{
			plugin.OverrideOnGUI();
		}




	}
}
