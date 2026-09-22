using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
namespace InControl
{
	public abstract class InputDeviceProfile
	{
		private static HashSet<Type> hideList = new HashSet<Type>();

		private float sensitivity;

		private float lowerDeadZone;

		private float upperDeadZone;

		public string Name { get; protected set; }

		public string Meta { get; protected set; }

		public InputControlMapping[] AnalogMappings { get; protected set; }

		public InputControlMapping[] ButtonMappings { get; protected set; }

		public string[] IncludePlatforms { get; protected set; }

		public string[] ExcludePlatforms { get; protected set; }

		public int MaxSystemBuildNumber { get; protected set; }

		public int MinSystemBuildNumber { get; protected set; }

		public InputDeviceClass DeviceClass { get; protected set; }

		public InputDeviceStyle DeviceStyle { get; protected set; }

		public float Sensitivity
		{
			get
			{
				return sensitivity;
			}
			protected set
			{
				sensitivity = Mathf.Clamp01(value);
			}
		}

		public float LowerDeadZone
		{
			get
			{
				return lowerDeadZone;
			}
			protected set
			{
				lowerDeadZone = Mathf.Clamp01(value);
			}
		}

		public float UpperDeadZone
		{
			get
			{
				return upperDeadZone;
			}
			protected set
			{
				upperDeadZone = Mathf.Clamp01(value);
			}
		}

		[Obsolete("This property has been renamed to IncludePlatforms.", false)]
		public string[] SupportedPlatforms
		{
			get
			{
				return IncludePlatforms;
			}
			protected set
			{
				IncludePlatforms = value;
			}
		}

		public virtual bool IsSupportedOnThisPlatform
		{
			get
			{
				int systemBuildNumber = Utility.GetSystemBuildNumber();
				if (MaxSystemBuildNumber > 0 && systemBuildNumber > MaxSystemBuildNumber)
				{
					return false;
				}
				if (MinSystemBuildNumber > 0 && systemBuildNumber < MinSystemBuildNumber)
				{
					return false;
				}
				if (ExcludePlatforms != null)
				{
					int num = ExcludePlatforms.Length;
					for (int i = 0; i < num; i++)
					{
						if (InputManager.Platform.Contains(ExcludePlatforms[i].ToUpper()))
						{
							return false;
						}
					}
				}
				if (IncludePlatforms == null || IncludePlatforms.Length == 0)
				{
					return true;
				}
				if (IncludePlatforms != null)
				{
					int num2 = IncludePlatforms.Length;
					for (int j = 0; j < num2; j++)
					{
						if (InputManager.Platform.Contains(IncludePlatforms[j].ToUpper()))
						{
							return true;
						}
					}
				}
				return false;
			}
		}

		internal bool IsHidden
		{
			get
			{
				return hideList.Contains(GetType());
			}
		}

		public int AnalogCount
		{
			get
			{
				return AnalogMappings.Length;
			}
		}

		public int ButtonCount
		{
			get
			{
				return ButtonMappings.Length;
			}
		}

		public InputDeviceProfile()
		{
			sensitivity = 1f;
			upperDeadZone = 1f;

			Name = "";
			Meta = "";
			AnalogMappings = new InputControlMapping[0];
			ButtonMappings = new InputControlMapping[0];
			IncludePlatforms = new string[0];
			ExcludePlatforms = new string[0];
			MinSystemBuildNumber = 0;
			MaxSystemBuildNumber = 0;
			DeviceClass = InputDeviceClass.Unknown;
			DeviceStyle = InputDeviceStyle.Unknown;
		}

		internal static void Hide(Type type)
		{
			hideList.Add(type);
		}




	}
}
