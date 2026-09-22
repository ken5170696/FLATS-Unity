using System;
using System.Runtime.InteropServices;
using UnityEngine;
namespace InControl
{
	public class InputControlMapping
	{
		public InputControlSource Source;

		public InputControlType Target;

		public bool Invert;

		public float Scale;

		public bool Raw;

		public bool Passive;

		public bool IgnoreInitialZeroValue;

		public float Sensitivity;

		public float LowerDeadZone;

		public float UpperDeadZone;

		public InputRange SourceRange;

		public InputRange TargetRange;

		private string handle;

		public string Handle
		{
			get
			{
				if (!string.IsNullOrEmpty(handle))
				{
					return handle;
				}
				return Target.ToString();
			}
			set
			{
				handle = value;
			}
		}

		public float MapValue(float value)
		{
			if (Raw)
			{
				value *= Scale;
				value = (SourceRange.Excludes(value) ? 0f : value);
			}
			else
			{
				value = Mathf.Clamp(value * Scale, -1f, 1f);
				value = InputRange.Remap(value, SourceRange, TargetRange);
			}
			if (Invert)
			{
				value = 0f - value;
			}
			return value;
		}

		public InputControlMapping()
		{
			Scale = 1f;
			Sensitivity = 1f;
			UpperDeadZone = 1f;
			SourceRange = InputRange.MinusOneToOne;
			TargetRange = InputRange.MinusOneToOne;

		}




	}
}
