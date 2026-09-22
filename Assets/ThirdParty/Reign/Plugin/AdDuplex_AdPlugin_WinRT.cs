using System;
namespace Reign.Plugin
{
	public class AdDuplex_AdPlugin_WinRT : IAdPlugin
	{
		public delegate void InitNativeMethod(AdDuplex_AdPlugin_WinRT plugin, AdDesc desc, AdCreatedCallbackMethod createdCallback);

		public IAdPlugin Native;

		public static InitNativeMethod InitNative;

		public bool Visible
		{
			get
			{
				return Native.Visible;
			}
			set
			{
				Native.Visible = value;
			}
		}

		public AdDuplex_AdPlugin_WinRT(AdDesc desc, AdCreatedCallbackMethod createdCallback)
		{
			InitNative(this, desc, createdCallback);
		}

		public void Dispose()
		{
			Native.Dispose();
		}

		public void SetGravity(AdGravity gravity)
		{
			Native.SetGravity(gravity);
		}

		public void Refresh()
		{
			Native.Refresh();
		}

		public void Update()
		{
			Native.Update();
		}

		public void OnGUI()
		{
			Native.OnGUI();
		}

		public void OverrideOnGUI()
		{
			Native.OverrideOnGUI();
		}




	}
}
