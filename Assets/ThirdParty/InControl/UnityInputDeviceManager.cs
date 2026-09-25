using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
namespace InControl
{
	public class UnityInputDeviceManager : InputDeviceManager
	{
		private const float deviceRefreshInterval = 1f;

		private float deviceRefreshTimer;

		private List<UnityInputDeviceProfileBase> systemDeviceProfiles;

		private List<UnityInputDeviceProfileBase> customDeviceProfiles;

		private string[] joystickNames;

		private int lastJoystickCount;

		private int lastJoystickHash;

		private int joystickCount;

		private int joystickHash;

		// FLATS: Windows reports some XInput controllers (for example a Bluetooth Xbox
		// Series controller) with an empty joystick name, which is also how it reports
		// empty slots. Such slots wait here and attach with the Xbox profile once they
		// produce input, so a real controller works and an empty slot adds nothing.
		private readonly List<int> unnamedJoysticks = new List<int>();

		private bool JoystickInfoHasChanged
		{
			get
			{
				if (joystickHash == lastJoystickHash)
				{
					return joystickCount != lastJoystickCount;
				}
				return true;
			}
		}

		public UnityInputDeviceManager()
		{
			systemDeviceProfiles = new List<UnityInputDeviceProfileBase>(UnityInputDeviceProfileList.Profiles.Length);
			customDeviceProfiles = new List<UnityInputDeviceProfileBase>();

			AddSystemDeviceProfiles();
			foreach (UnityInputDeviceProfileBase profile in systemDeviceProfiles)
			{
				if (profile is XboxOneWin10AEProfile || profile is XboxOneWin10Profile || profile is XboxOneWinProfile || profile is Xbox360WinProfile)
				{
					UseSeparateTriggerAxes((UnityInputDeviceProfile)profile);
				}
			}
			QueryJoystickInfo();
			AttachDevices();
		}

		public override void Update(ulong updateTick, float deltaTime)
		{
			deviceRefreshTimer += deltaTime;
			if (deviceRefreshTimer >= 1f)
			{
				deviceRefreshTimer = 0f;
				QueryJoystickInfo();
				if (JoystickInfoHasChanged)
				{
					Logger.LogInfo("Change in attached Unity joysticks detected; refreshing device list.");
					DetachDevices();
					AttachDevices();
				}
			}
			AttachActiveUnnamedJoysticks();
		}

		private void AttachActiveUnnamedJoysticks()
		{
			for (int i = unnamedJoysticks.Count - 1; i >= 0; i--)
			{
				int id = unnamedJoysticks[i];
				if (HasAttachedDeviceWithJoystickId(id))
				{
					unnamedJoysticks.RemoveAt(i);
				}
				else if (UnnamedJoystickHasInput(id))
				{
					unnamedJoysticks.RemoveAt(i);
					var profile = new UnnamedXInputProfile();
					AttachDevice(new UnityInputDevice(profile, id, string.Empty));
					Debug.Log("[InControl] Unnamed joystick " + id + " attached as " + profile.Name);
				}
			}
		}

		// The Xbox profiles read each trigger from its own axis (9th LT, 10th RT) and
		// also from the shared 3rd axis (LT positive, RT negative), keeping the larger
		// value. Some controllers (a Bluetooth Xbox Series controller among them)
		// report the shared axis with the opposite sign, so LT also pulled RT and aiming
		// fired. Once either separate trigger axis of a device has moved, the shared
		// axis is ignored for that device.
		private sealed class UnnamedXInputProfile : XboxOneWin10AEProfile
		{
			public UnnamedXInputProfile()
			{
				UseSeparateTriggerAxes(this);
			}
		}

		private static void UseSeparateTriggerAxes(UnityInputDeviceProfile profile)
		{
			var shared = new SharedTriggerSource();
			foreach (InputControlMapping mapping in profile.AnalogMappings)
			{
				var analog = mapping.Source as UnityAnalogSource;
				if (analog != null && analog.AnalogIndex == 2 &&
					(mapping.Target == InputControlType.LeftTrigger || mapping.Target == InputControlType.RightTrigger))
				{
					mapping.Source = shared;
				}
			}
		}

		private sealed class SharedTriggerSource : InputControlSource
		{
			private readonly HashSet<InputDevice> separateAxesSeen = new HashSet<InputDevice>();

			public float GetValue(InputDevice inputDevice)
			{
				var device = (UnityInputDevice)inputDevice;
				if (separateAxesSeen.Contains(device))
				{
					return 0f;
				}
				if (Mathf.Abs(device.ReadRawAnalogValue(8)) > 0.05f || Mathf.Abs(device.ReadRawAnalogValue(9)) > 0.05f)
				{
					separateAxesSeen.Add(device);
					return 0f;
				}
				return device.ReadRawAnalogValue(2);
			}

			public bool GetState(InputDevice inputDevice)
			{
				return Utility.IsNotZero(GetValue(inputDevice));
			}
		}

		private static bool UnnamedJoystickHasInput(int id)
		{
			if (id < 1 || id > 8)
			{
				return false;
			}
			int firstButton = (int)KeyCode.Joystick1Button0 + (id - 1) * 20;
			for (int button = 0; button < 10; button++)
			{
				if (Input.GetKey((KeyCode)(firstButton + button)))
				{
					return true;
				}
			}
			// Left and right stick axes; triggers rest at a nonzero value on some drivers.
			foreach (int analog in new[] { 0, 1, 3, 4 })
			{
				if (Mathf.Abs(Input.GetAxisRaw("joystick " + id + " analog " + analog)) > 0.3f)
				{
					return true;
				}
			}
			return false;
		}

		private void QueryJoystickInfo()
		{
			joystickNames = Input.GetJoystickNames();
			joystickCount = joystickNames.Length;
			joystickHash = 527 + joystickCount;
			for (int i = 0; i < joystickCount; i++)
			{
				joystickHash = joystickHash * 31 + joystickNames[i].GetHashCode();
			}
		}

		private void AttachDevices()
		{
			AttachKeyboardDevices();
			AttachJoystickDevices();
			lastJoystickCount = joystickCount;
			lastJoystickHash = joystickHash;
		}

		private void DetachDevices()
		{
			int count = devices.Count;
			for (int i = 0; i < count; i++)
			{
				InputManager.DetachDevice(devices[i]);
			}
			devices.Clear();
		}

		public void ReloadDevices()
		{
			QueryJoystickInfo();
			DetachDevices();
			AttachDevices();
		}

		private void AttachDevice(UnityInputDevice device)
		{
			devices.Add(device);
			InputManager.AttachDevice(device);
		}

		private void AttachKeyboardDevices()
		{
			int count = systemDeviceProfiles.Count;
			for (int i = 0; i < count; i++)
			{
				UnityInputDeviceProfileBase unityInputDeviceProfileBase = systemDeviceProfiles[i];
				if (unityInputDeviceProfileBase.IsNotJoystick && unityInputDeviceProfileBase.IsSupportedOnThisPlatform)
				{
					AttachDevice(new UnityInputDevice(unityInputDeviceProfileBase));
				}
			}
		}

		private void AttachJoystickDevices()
		{
			unnamedJoysticks.Clear();
			try
			{
				for (int i = 0; i < joystickCount; i++)
				{
					DetectJoystickDevice(i + 1, joystickNames[i]);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex.Message);
				Logger.LogError(ex.StackTrace);
			}
		}

		private bool HasAttachedDeviceWithJoystickId(int unityJoystickId)
		{
			int count = devices.Count;
			for (int i = 0; i < count; i++)
			{
				UnityInputDevice unityInputDevice = devices[i] as UnityInputDevice;
				if (unityInputDevice != null && unityInputDevice.JoystickId == unityJoystickId)
				{
					return true;
				}
			}
			return false;
		}

		private void DetectJoystickDevice(int unityJoystickId, string unityJoystickName)
		{
			if (HasAttachedDeviceWithJoystickId(unityJoystickId) || unityJoystickName.IndexOf("webcam", StringComparison.OrdinalIgnoreCase) != -1 || (InputManager.UnityVersion < new VersionInfo(4, 5, 0, 0) && (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer) && unityJoystickName == "Unknown Wireless Controller") || (InputManager.UnityVersion >= new VersionInfo(4, 6, 3, 0) && (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer) && string.IsNullOrEmpty(unityJoystickName)))
			{
				if (string.IsNullOrEmpty(unityJoystickName) && !HasAttachedDeviceWithJoystickId(unityJoystickId) && !unnamedJoysticks.Contains(unityJoystickId))
				{
					unnamedJoysticks.Add(unityJoystickId);
				}
				return;
			}
			UnityInputDeviceProfileBase unityInputDeviceProfileBase = null;
			if (unityInputDeviceProfileBase == null)
			{
				unityInputDeviceProfileBase = customDeviceProfiles.Find((UnityInputDeviceProfileBase config) => config.HasJoystickName(unityJoystickName));
			}
			if (unityInputDeviceProfileBase == null)
			{
				unityInputDeviceProfileBase = systemDeviceProfiles.Find((UnityInputDeviceProfileBase config) => config.HasJoystickName(unityJoystickName));
			}
			if (unityInputDeviceProfileBase == null)
			{
				unityInputDeviceProfileBase = customDeviceProfiles.Find((UnityInputDeviceProfileBase config) => config.HasLastResortRegex(unityJoystickName));
			}
			if (unityInputDeviceProfileBase == null)
			{
				unityInputDeviceProfileBase = systemDeviceProfiles.Find((UnityInputDeviceProfileBase config) => config.HasLastResortRegex(unityJoystickName));
			}
			if (unityInputDeviceProfileBase == null)
			{
				UnityInputDevice device = new UnityInputDevice(unityJoystickId, unityJoystickName);
				AttachDevice(device);
				Debug.Log("[InControl] Joystick " + unityJoystickId + ": \"" + unityJoystickName + "\"");
				Logger.LogWarning("Device " + unityJoystickId + " with name \"" + unityJoystickName + "\" does not match any supported profiles and will be considered an unknown controller.");
			}
			else if (!unityInputDeviceProfileBase.IsHidden)
			{
				UnityInputDevice device2 = new UnityInputDevice(unityInputDeviceProfileBase, unityJoystickId, unityJoystickName);
				AttachDevice(device2);
				Logger.LogInfo("Device " + unityJoystickId + " matched profile " + unityInputDeviceProfileBase.GetType().Name + " (" + unityInputDeviceProfileBase.Name + ")");
			}
			else
			{
				Logger.LogInfo("Device " + unityJoystickId + " matching profile " + unityInputDeviceProfileBase.GetType().Name + " (" + unityInputDeviceProfileBase.Name + ") is hidden and will not be attached.");
			}
		}

		private void AddSystemDeviceProfile(UnityInputDeviceProfile deviceProfile)
		{
			if (deviceProfile.IsSupportedOnThisPlatform)
			{
				systemDeviceProfiles.Add(deviceProfile);
			}
		}

		private void AddSystemDeviceProfiles()
		{
			string[] profiles = UnityInputDeviceProfileList.Profiles;
			foreach (string typeName in profiles)
			{
				UnityInputDeviceProfile deviceProfile = (UnityInputDeviceProfile)Activator.CreateInstance(Type.GetType(typeName));
				AddSystemDeviceProfile(deviceProfile);
			}
		}




	}
}
