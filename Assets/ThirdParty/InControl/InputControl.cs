using System;
using System.Runtime.InteropServices;
namespace InControl
{
	public class InputControl : OneAxisInputControl
	{
		public static readonly InputControl Null = new InputControl();

		public bool Passive;

		private ulong zeroTick;

		public string Handle { get; protected set; }

		public InputControlType Target { get; protected set; }

		public bool IsButton { get; protected set; }

		public bool IsAnalog { get; protected set; }

		internal bool IsOnZeroTick
		{
			get
			{
				return base.UpdateTick == zeroTick;
			}
		}

		public bool IsStandard
		{
			get
			{
				return Utility.TargetIsStandard(Target);
			}
		}

		private InputControl()
		{
			Handle = "None";
			Target = InputControlType.None;
			Passive = false;
			IsButton = false;
			IsAnalog = false;
		}

		public InputControl(string handle, InputControlType target)
		{
			Handle = handle;
			Target = target;
			Passive = false;
			IsButton = Utility.TargetIsButton(target);
			IsAnalog = !IsButton;
		}

		public InputControl(string handle, InputControlType target, bool passive)
			: this(handle, target)
		{
			Passive = passive;
		}

		internal void SetZeroTick()
		{
			zeroTick = base.UpdateTick;
		}




	}
}
