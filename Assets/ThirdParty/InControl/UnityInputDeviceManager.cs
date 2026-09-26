using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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

		// Standard layouts that the platform itself guarantees. Android (API 12+) maps a
		// recognised gamepad to KEYCODE_BUTTON_* and the standard motion axes; browsers
		// use the W3C "standard" Gamepad mapping. Button names follow the controller family.
		private sealed class StandardGamepadProfile : UnityInputDeviceProfile
		{
			private static readonly Dictionary<InputDeviceStyle, StandardGamepadProfile> profiles = new Dictionary<InputDeviceStyle, StandardGamepadProfile>();

			public static StandardGamepadProfile For(string joystickName)
			{
				bool web = Application.platform == RuntimePlatform.WebGLPlayer;
				if (!web && Application.platform != RuntimePlatform.Android)
				{
					return null;
				}
				InputDeviceStyle style = StyleOf(joystickName);
				StandardGamepadProfile profile;
				if (!profiles.TryGetValue(style, out profile))
				{
					profile = new StandardGamepadProfile(style, web);
					profiles[style] = profile;
				}
				return profile;
			}

			private static InputDeviceStyle StyleOf(string joystickName)
			{
				string name = joystickName ?? string.Empty;
				// 054c is Sony's USB vendor id, which browsers include in the name.
				if (Regex.IsMatch(name, "dualsense|dualshock|playstation|sony|054c|^wireless controller", RegexOptions.IgnoreCase))
				{
					return InputDeviceStyle.PlayStation4;
				}
				if (Regex.IsMatch(name, "xbox|xinput|045e", RegexOptions.IgnoreCase))
				{
					return InputDeviceStyle.XboxOne;
				}
				return InputDeviceStyle.Unknown;
			}

			private StandardGamepadProfile(InputDeviceStyle style, bool web)
			{
				bool ps = style == InputDeviceStyle.PlayStation4;
				base.Name = ps ? "PlayStation Controller" : style == InputDeviceStyle.XboxOne ? "Xbox Controller" : "Controller";
				base.Meta = base.Name + (web ? " (browser standard layout)" : " (Android standard layout)");
				base.DeviceClass = InputDeviceClass.Controller;
				base.DeviceStyle = style;
				// Standard layout: A/Cross 0, B/Circle 1, X/Square 2, Y/Triangle 3, LB 4, RB 5.
				// Web: LT 6, RT 7, View/Share 8, Menu/Options 9, LS 10, RS 11, D-pad 12-15.
				// Android: L2 6, R2 7, LS 8, RS 9, Start/Options 10, Select/Share 11.
				var buttons = new List<InputControlMapping>
				{
					ButtonMapping(ps ? "Cross" : "A", InputControlType.Action1, 0),
					ButtonMapping(ps ? "Circle" : "B", InputControlType.Action2, 1),
					ButtonMapping(ps ? "Square" : "X", InputControlType.Action3, 2),
					ButtonMapping(ps ? "Triangle" : "Y", InputControlType.Action4, 3),
					ButtonMapping(ps ? "L1" : "Left Bumper", InputControlType.LeftBumper, 4),
					ButtonMapping(ps ? "R1" : "Right Bumper", InputControlType.RightBumper, 5),
					ButtonMapping(ps ? "L3" : "Left Stick Button", InputControlType.LeftStickButton, web ? 10 : 8),
					ButtonMapping(ps ? "R3" : "Right Stick Button", InputControlType.RightStickButton, web ? 11 : 9),
					ButtonMapping(ps ? "Options" : "Menu", ps ? InputControlType.Options : InputControlType.Menu, web ? 9 : 10),
					ButtonMapping(ps ? "Share" : "View", ps ? InputControlType.Share : InputControlType.View, web ? 8 : 11)
				};
				var analogs = new List<InputControlMapping>
				{
					LeftStickLeftMapping(Analog(0)),
					LeftStickRightMapping(Analog(0)),
					LeftStickUpMapping(Analog(1)),
					LeftStickDownMapping(Analog(1)),
					RightStickLeftMapping(Analog(2)),
					RightStickRightMapping(Analog(2)),
					RightStickUpMapping(Analog(3)),
					RightStickDownMapping(Analog(3))
				};
				if (web)
				{
					buttons.Add(ButtonMapping("D-Pad Up", InputControlType.DPadUp, 12));
					buttons.Add(ButtonMapping("D-Pad Down", InputControlType.DPadDown, 13));
					buttons.Add(ButtonMapping("D-Pad Left", InputControlType.DPadLeft, 14));
					buttons.Add(ButtonMapping("D-Pad Right", InputControlType.DPadRight, 15));
					analogs.Add(new InputControlMapping { Handle = ps ? "L2" : "Left Trigger", Target = InputControlType.LeftTrigger, Source = new TriggerSource(6, new int[0]) });
					analogs.Add(new InputControlMapping { Handle = ps ? "R2" : "Right Trigger", Target = InputControlType.RightTrigger, Source = new TriggerSource(7, new int[0]) });
				}
				else
				{
					analogs.Add(DPadLeftMapping(Analog(4)));
					analogs.Add(DPadRightMapping(Analog(4)));
					analogs.Add(DPadUpMapping(Analog(5)));
					analogs.Add(DPadDownMapping(Analog(5)));
					// Drivers report the triggers as LTRIGGER/RTRIGGER, BRAKE/GAS or only as buttons.
					analogs.Add(new InputControlMapping { Handle = ps ? "L2" : "Left Trigger", Target = InputControlType.LeftTrigger, Source = new TriggerSource(6, new[] { 6, 12 }) });
					analogs.Add(new InputControlMapping { Handle = ps ? "R2" : "Right Trigger", Target = InputControlType.RightTrigger, Source = new TriggerSource(7, new[] { 7, 11 }) });
				}
				base.ButtonMappings = buttons.ToArray();
				base.AnalogMappings = analogs.ToArray();
			}

			private static InputControlMapping ButtonMapping(string handle, InputControlType target, int button)
			{
				return new InputControlMapping { Handle = handle, Target = target, Source = Button(button) };
			}
		}

		// Largest of a trigger's analog axes (0..1) and its digital button.
		private sealed class TriggerSource : InputControlSource
		{
			private readonly int button;

			private readonly int[] analogs;

			public TriggerSource(int button, int[] analogs)
			{
				this.button = button;
				this.analogs = analogs;
			}

			public float GetValue(InputDevice inputDevice)
			{
				var device = (UnityInputDevice)inputDevice;
				float value = device.ReadRawButtonState(button) ? 1f : 0f;
				foreach (int analog in analogs)
				{
					value = Mathf.Max(value, Mathf.Clamp01(device.ReadRawAnalogValue(analog)));
				}
				return value;
			}

			public bool GetState(InputDevice inputDevice)
			{
				return Utility.IsNotZero(GetValue(inputDevice));
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
			// FLATS: browsers already report every controller in the standard layout, so the
			// desktop driver profiles (matched by name, e.g. "xbox") would read the wrong axes.
			if (Application.platform == RuntimePlatform.WebGLPlayer)
			{
				unityInputDeviceProfileBase = StandardGamepadProfile.For(unityJoystickName);
			}
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
				// FLATS: Android maps any recognised gamepad (DualSense, Switch Pro, 8BitDo...)
				// to its standard key layout, so an unlisted name still has usable controls.
				unityInputDeviceProfileBase = StandardGamepadProfile.For(unityJoystickName);
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
