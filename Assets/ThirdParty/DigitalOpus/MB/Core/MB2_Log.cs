using System;
using System.Runtime.InteropServices;
using UnityEngine;
namespace DigitalOpus.MB.Core
{
	public class MB2_Log
	{
		public static void Log(MB2_LogLevel l, string msg, MB2_LogLevel currentThreshold)
		{
			if (l <= currentThreshold)
			{
				if (l == MB2_LogLevel.error)
				{
					Debug.LogError(msg);
				}
				if (l == MB2_LogLevel.warn)
				{
					Debug.LogWarning(string.Format("frm={0} WARN {1}", new object[2]
					{
						Time.frameCount,
						msg
					}));
				}
				if (l == MB2_LogLevel.info)
				{
					Debug.Log(string.Format("frm={0} INFO {1}", new object[2]
					{
						Time.frameCount,
						msg
					}));
				}
				if (l == MB2_LogLevel.debug)
				{
					Debug.Log(string.Format("frm={0} DEBUG {1}", new object[2]
					{
						Time.frameCount,
						msg
					}));
				}
				if (l == MB2_LogLevel.trace)
				{
					Debug.Log(string.Format("frm={0} TRACE {1}", new object[2]
					{
						Time.frameCount,
						msg
					}));
				}
			}
		}

		public static string Error(string msg, params object[] args)
		{
			string text = string.Format(msg, args);
			string text2 = string.Format("f={0} ERROR {1}", new object[2]
			{
				Time.frameCount,
				text
			});
			Debug.LogError(text2);
			return text2;
		}

		public static string Warn(string msg, params object[] args)
		{
			string text = string.Format(msg, args);
			string text2 = string.Format("f={0} WARN {1}", new object[2]
			{
				Time.frameCount,
				text
			});
			Debug.LogWarning(text2);
			return text2;
		}

		public static string Info(string msg, params object[] args)
		{
			string text = string.Format(msg, args);
			string text2 = string.Format("f={0} INFO {1}", new object[2]
			{
				Time.frameCount,
				text
			});
			Debug.Log(text2);
			return text2;
		}

		public static string LogDebug(string msg, params object[] args)
		{
			string text = string.Format(msg, args);
			string text2 = string.Format("f={0} DEBUG {1}", new object[2]
			{
				Time.frameCount,
				text
			});
			Debug.Log(text2);
			return text2;
		}

		public static string Trace(string msg, params object[] args)
		{
			string text = string.Format(msg, args);
			string text2 = string.Format("f={0} TRACE {1}", new object[2]
			{
				Time.frameCount,
				text
			});
			Debug.Log(text2);
			return text2;
		}

		public MB2_Log()
		{
		}




	}
}
