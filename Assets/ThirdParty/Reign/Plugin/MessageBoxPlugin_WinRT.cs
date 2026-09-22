using System;
using System.Runtime.InteropServices;
namespace Reign.Plugin
{
	public class MessageBoxPlugin_WinRT : IMessageBoxPlugin
	{
		public delegate void InitNativeMethod(MessageBoxPlugin_WinRT plugin);

		public IMessageBoxPlugin Native;

		public static InitNativeMethod InitNative;

		public MessageBoxPlugin_WinRT()
		{
			InitNative(this);
		}

		public void Show(string title, string message, MessageBoxTypes type, MessageBoxOptions options, MessageBoxCallback callback)
		{
			Native.Show(title, message, type, options, callback);
		}

		public void Update()
		{
			Native.Update();
		}




	}
}
