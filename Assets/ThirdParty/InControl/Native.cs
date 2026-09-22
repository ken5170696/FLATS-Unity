using System;

namespace InControl
{
	internal static class Native
	{
		private const string LibraryName = "InControlNative";

		public static void Init(NativeInputOptions options)
		{
		}

		public static void Stop()
		{
		}

		public static void GetVersionInfo(out NativeVersionInfo versionInfo)
		{
			versionInfo = default(NativeVersionInfo);
		}

		public static bool GetDeviceInfo(uint handle, out NativeDeviceInfo deviceInfo)
		{
			deviceInfo = default(NativeDeviceInfo);
			return false;
		}

		public static bool GetDeviceState(uint handle, out IntPtr deviceState)
		{
			deviceState = IntPtr.Zero;
			return false;
		}

		public static int GetDeviceEvents(out IntPtr deviceEvents)
		{
			deviceEvents = IntPtr.Zero;
			return 0;
		}

		public static void SetHapticState(uint handle, byte motor0, byte motor1)
		{
		}

		public static void SetLightColor(uint handle, byte red, byte green, byte blue)
		{
		}

		public static void SetLightFlash(uint handle, byte flashOnDuration, byte flashOffDuration)
		{
		}


	}
}
