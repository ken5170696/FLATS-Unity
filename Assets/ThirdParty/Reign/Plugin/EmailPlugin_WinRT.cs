using System;
using System.Runtime.InteropServices;
namespace Reign.Plugin
{
	public class EmailPlugin_WinRT : IEmailPlugin
	{
		public delegate void InitNativeMethod(EmailPlugin_WinRT plugin);

		private IEmailPlugin native;

		public static InitNativeMethod InitNative;

		public object Native
		{
			set
			{
				native = (IEmailPlugin)value;
			}
		}

		public EmailPlugin_WinRT()
		{
			InitNative(this);
		}

		public void Send(string to, string subject, string body)
		{
			native.Send(to, subject, body);
		}




	}
}
