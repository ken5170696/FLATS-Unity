using System;

namespace InControl.NativeProfile
{
	public class MadCatzFightPadTE2MacProfile : Xbox360DriverMacProfile
	{
		public MadCatzFightPadTE2MacProfile()
		{
			base.Name = "Mad Catz FightPad TE2";
			base.Meta = "Mad Catz FightPad TE2 on Mac";
			Matchers = new NativeInputDeviceMatcher[1]
			{
				new NativeInputDeviceMatcher
				{
					VendorID = 7085,
					ProductID = 61568
				}
			};
		}


	}
}
